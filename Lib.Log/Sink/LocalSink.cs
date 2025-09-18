namespace Lib.Log.Sink;

using Lib.Log.Format;
using Lib.Log.Internal;
using Lib.Log.Model;
using Lib.Log.Option;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using System.Collections.Concurrent;
using System.Text;

public sealed class LocalSink(
    LogOptions opt,
    IFormat textFormatter,
    IFormat jsonFormatter,
    ILogger<LocalSink> logger,
    TimeProvider timeProvider) : ISink
{
    private readonly LogOptions _opt = opt;
    private readonly ILogger<LocalSink> _logger = logger;
    private readonly IFormat _textFormatter = textFormatter;
    private readonly IFormat _jsonFormatter = jsonFormatter;
    private readonly TimeProvider _timeProvider = timeProvider;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.8,
            MinimumThroughput = 5,
            SamplingDuration = TimeSpan.FromSeconds(15),
            BreakDuration = TimeSpan.FromSeconds(opt.Local.CircuitBreaker.BreakSec),
            OnOpened = args =>
            {
                logger.LogWarning("LocalFileSink circuit breaker opened for {BreakDuration}. Reason: {Exception}", args.BreakDuration, args.Outcome.Exception);
                return default;
            },
            OnClosed = _ =>
            {
                logger.LogInformation("LocalFileSink circuit breaker closed. Resuming operations.");
                return default;
            }
        })
        .Build();

    public string Name => "Local";

    public async Task WriteBatchAsync(IReadOnlyList<LogEntry> entries, CancellationToken ct)
    {
        if (!_opt.Local.Enabled)
        {
            return;
        }

        var groups = GroupByFile(entries);

        foreach (var group in groups)
        {
            var state = (Sink: this, Path: group.Key, Lines: (IReadOnlyList<string>)group.Value);
            var context = ResilienceContextPool.Shared.Get(ct);
            try
            {
                await _pipeline.ExecuteAsync(
                    static (ResilienceContext context, (LocalSink Sink, string Path, IReadOnlyList<string> Lines) state) =>
                        new ValueTask(state.Sink.AppendToFileAsync(state.Path, state.Lines, context.CancellationToken)),
                    context,
                    state).ConfigureAwait(false);
            }
            finally
            {
                ResilienceContextPool.Shared.Return(context);
            }
        }
    }

    private Dictionary<string, List<string>> GroupByFile(IReadOnlyList<LogEntry> entries)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            var root = RootResolver.ResolveRoot(e, _opt);
            var path = TemplateRenderer.RenderPath(_opt.Local.Directory, _opt.Local.FileTemplate,
                                                   new RouteKey("FileSink", e.Category, e.DeviceId),
                                                   e.Timestamp, _opt, root);

            if (!groups.TryGetValue(path, out var lines))
            {
                lines = new List<string>();
                groups[path] = lines;
            }

            if (_opt.Formatting.Text)
            {
                lines.Add(_textFormatter.FormatLine(e));
            }

            if (_opt.Formatting.Json)
            {
                lines.Add(_jsonFormatter.FormatLine(e));
            }
        }
        return groups;
    }

    private async Task AppendToFileAsync(string path, IReadOnlyList<string> lines, CancellationToken ct)
    {
        var gate = _fileLocks.GetOrAdd(path, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);

            HandleRollover(path, lines);

            var encoding = new UTF8Encoding(false);
            var newlineBytes = encoding.GetBytes(Environment.NewLine);

            await using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite,
                bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

            foreach (var line in lines)
            {
                var buffer = encoding.GetBytes(line);
                await fs.WriteAsync(buffer, ct).ConfigureAwait(false);
                await fs.WriteAsync(newlineBytes, ct).ConfigureAwait(false);
            }

            if (_opt.Local.FlushOnError && lines.Any(static l => l.Contains(" | E | ") || l.Contains(" | C | ")))
            {
                await fs.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private void HandleRollover(string path, IReadOnlyList<string> lines)
    {
        if (_opt.Local.Rollover.Type is LogOptions.RolloverType.Size or LogOptions.RolloverType.Both)
        {
            try
            {
                var fi = new FileInfo(path);
                if (fi.Exists)
                {
                    var approxNewBytes = lines.Sum(l => l.Length + Environment.NewLine.Length);
                    if (fi.Length + approxNewBytes > _opt.Local.Rollover.MaxSizeMB * 1024L * 1024L)
                    {
                        var now = _timeProvider.GetLocalNow().DateTime;
                        var rolled = path + $".rolled.{now:yyyyMMddHHmmss}";
                        File.Move(path, rolled, overwrite: false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to roll log file {Path}", path);
            }
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}