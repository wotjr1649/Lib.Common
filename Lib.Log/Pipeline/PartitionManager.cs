namespace Lib.Log.Pipeline;

using Lib.Log.Model;
using Lib.Log.Routing;
using Lib.Log.Option;
using Lib.Log.Sink;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

public sealed class PartitionManager(
    IOptionsMonitor<LogOptions> options,
    ILogRouter router,
    SinkFactory sinkFactory,
    ObjectPool<LogEntry> logEntryPool,
    TimeProvider timeProvider,
    ILogSamplingStrategy samplingStrategy) : IHostedService, IAsyncDisposable
{
    private readonly IOptionsMonitor<LogOptions> _options = options;
    private readonly ILogRouter _router = router;
    private readonly SinkFactory _sinkFactory = sinkFactory;
    private readonly ObjectPool<LogEntry> _logEntryPool = logEntryPool;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogSamplingStrategy _samplingStrategy = samplingStrategy;

    private List<ISink> _sinks = [];
    private readonly Dictionary<string, ShardGroup> _groups = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _sinks = _sinkFactory.CreateSinks().ToList();
        var opt = _options.CurrentValue;

        foreach (var (groupName, pg) in opt.Partitions)
        {
            var shards = new List<ShardWorker>(pg.Shards);
            for (var i = 0; i < pg.Shards; i++)
            {
                var worker = new ShardWorker(pg, _options, _sinks, _logEntryPool, _timeProvider, _samplingStrategy);
                worker.Start(_cts.Token);
                shards.Add(worker);
            }
            _groups[groupName] = new ShardGroup(groupName, pg, shards);
        }

        if (!_groups.ContainsKey("Default"))
        {
            var pg = new LogOptions.PartitionGroupOptions();
            var worker = new ShardWorker(pg, _options, _sinks, _logEntryPool, _timeProvider, _samplingStrategy);
            worker.Start(_cts.Token);
            _groups["Default"] = new ShardGroup("Default", pg, new List<ShardWorker> { worker });
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();

        var completionTasks = new List<Task>(_groups.Values.Sum(g => g.Shards.Count));
        foreach (var group in _groups.Values)
        {
            foreach (var shard in group.Shards)
            {
                completionTasks.Add(shard.CompleteAsync());
            }
        }

        await Task.WhenAll(completionTasks).ConfigureAwait(false);

        _groups.Clear();
        _cts.Dispose();
        _cts = null;

        await DisposeAsync().ConfigureAwait(false);
    }

    public bool Enqueue(LogEntry entry, CancellationToken cancellationToken = default)
    {
        if (_cts is null || _cts.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            var (groupName, routeKey) = _router.Resolve(entry);
            if (!_groups.TryGetValue(groupName, out var group) && !_groups.TryGetValue("Default", out group))
            {
                return false;
            }

            var shardIndex = Hash.HashToShard(routeKey, group.Shards.Count);
            var shard = group.Shards[shardIndex];
            return shard.TryWrite(entry, cancellationToken);
        }
        catch
        {
            _logEntryPool.Return(entry);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sink in _sinks)
        {
            await sink.DisposeAsync().ConfigureAwait(false);
        }

        _sinks.Clear();
    }

    private sealed record ShardGroup(string Name, LogOptions.PartitionGroupOptions Options, List<ShardWorker> Shards);
}

internal sealed class ShardWorker
{
    private readonly LogOptions.PartitionGroupOptions _opt;
    private readonly IOptionsMonitor<LogOptions> _optionsMonitor;
    private readonly IReadOnlyList<ISink> _sinks;
    private readonly ObjectPool<LogEntry> _logEntryPool;
    private readonly TimeProvider _timeProvider;
    private readonly ILogSamplingStrategy _samplingStrategy;
    private readonly Channel<LogEntry> _channel;
    private CancellationToken _shutdownToken = CancellationToken.None;
    private Task? _consumer;

    public ShardWorker(
        LogOptions.PartitionGroupOptions opt,
        IOptionsMonitor<LogOptions> optionsMonitor,
        IReadOnlyList<ISink> sinks,
        ObjectPool<LogEntry> logEntryPool,
        TimeProvider timeProvider,
        ILogSamplingStrategy samplingStrategy)
    {
        _opt = opt;
        _optionsMonitor = optionsMonitor;
        _sinks = sinks;
        _logEntryPool = logEntryPool;
        _timeProvider = timeProvider;
        _samplingStrategy = samplingStrategy;

        var boundedOptions = new BoundedChannelOptions(Math.Max(1, opt.QueueCapacity))
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = opt.Backpressure switch
            {
                LogOptions.BackpressurePolicy.Block => BoundedChannelFullMode.Wait,
                LogOptions.BackpressurePolicy.DropLatest => BoundedChannelFullMode.Wait,
                LogOptions.BackpressurePolicy.Sample1Percent => BoundedChannelFullMode.Wait,
                _ => BoundedChannelFullMode.Wait
            }
        };

        _channel = Channel.CreateBounded<LogEntry>(boundedOptions);
    }

    public void Start(CancellationToken ct)
    {
        _shutdownToken = ct;
        _consumer = Task.Run(() => ConsumeAsync(ct), CancellationToken.None);
    }

    public bool TryWrite(LogEntry entry, CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        if (_opt.Backpressure == LogOptions.BackpressurePolicy.Sample1Percent &&
            _samplingStrategy.ShouldSample(entry, options, _opt))
        {
            _logEntryPool.Return(entry);
            return true;
        }

        if (_channel.Writer.TryWrite(entry))
        {
            return true;
        }

        return _opt.Backpressure switch
        {
            LogOptions.BackpressurePolicy.Block => WriteWithWait(entry, cancellationToken),
            LogOptions.BackpressurePolicy.DropLatest => false,
            LogOptions.BackpressurePolicy.Sample1Percent => WriteWithWait(entry, cancellationToken),
            _ => false
        };
    }

    public async Task CompleteAsync()
    {
        _channel.Writer.TryComplete();
        if (_consumer is not null)
        {
            await _consumer.ConfigureAwait(false);
        }
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var batch = new List<LogEntry>(Math.Max(1, _opt.BatchSize));
        var flushInterval = TimeSpan.FromMilliseconds(Math.Max(50, _opt.FlushIntervalMs));

        try
        {
            while (await _channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    var start = _timeProvider.GetUtcNow();

                    while (batch.Count < _opt.BatchSize && _channel.Reader.TryRead(out var item))
                    {
                        batch.Add(item);
                        if (_timeProvider.GetUtcNow() - start > flushInterval)
                        {
                            break;
                        }
                    }

                    if (batch.Count == 0 && _channel.Reader.TryRead(out var first))
                    {
                        batch.Add(first);
                    }

                    if (batch.Count > 0)
                    {
                        await FlushBatchToSinksAsync(batch, CancellationToken.None).ConfigureAwait(false);
                        batch.Clear();
                    }
                }
                catch (Exception)
                {
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            while (_channel.Reader.TryRead(out var remaining))
            {
                batch.Add(remaining);
            }

            if (batch.Count > 0)
            {
                try
                {
                    await FlushBatchToSinksAsync(batch, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }
    }

    private async Task FlushBatchToSinksAsync(IReadOnlyList<LogEntry> entries, CancellationToken ct)
    {
        if (_sinks.Count == 0)
        {
            foreach (var entry in entries)
            {
                _logEntryPool.Return(entry);
            }
            return;
        }

        try
        {
            var groupedBySink = new Dictionary<string, List<LogEntry>>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                var targetSinkKey = "default";

                if (entry.Scope is not null)
                {
                    var sinkPair = entry.Scope.FirstOrDefault(kv =>
                        string.Equals(kv.Key, "Sinks", StringComparison.OrdinalIgnoreCase));

                    if (sinkPair.Key is not null && sinkPair.Value is string sinkName && !string.IsNullOrWhiteSpace(sinkName))
                    {
                        targetSinkKey = sinkName;
                    }
                }

                if (!groupedBySink.TryGetValue(targetSinkKey, out var list))
                {
                    list = new List<LogEntry>();
                    groupedBySink[targetSinkKey] = list;
                }

                list.Add(entry);
            }

            var tasks = new List<Task>();

            foreach (var group in groupedBySink)
            {
                var sinkKey = group.Key;
                var batch = group.Value;

                if (sinkKey.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var sink in _sinks)
                    {
                        tasks.Add(sink.WriteBatchAsync(batch, ct));
                    }
                }
                else
                {
                    var targetSinkNames = sinkKey.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                 .Select(static s => s.Trim());

                    foreach (var targetName in targetSinkNames)
                    {
                        var targetSink = _sinks.FirstOrDefault(s => s.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
                        if (targetSink is not null)
                        {
                            tasks.Add(targetSink.WriteBatchAsync(batch, ct));
                        }
                    }
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }
        finally
        {
            foreach (var entry in entries)
            {
                _logEntryPool.Return(entry);
            }
        }
    }

    private bool WriteWithWait(LogEntry entry, CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownToken, cancellationToken);
        try
        {
            _channel.Writer.WriteAsync(entry, linkedCts.Token).AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
