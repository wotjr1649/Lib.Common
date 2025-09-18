namespace Lib.Log.Provider;

using Lib.Log.Model;
using Lib.Log.Option;
using Lib.Log.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

public sealed class LoggerProvider(
    PartitionManager pipeline,
    IOptionsMonitor<LogOptions> options,
    ObjectPool<LogEntry> logEntryPool,
    TimeProvider timeProvider) : ILoggerProvider, ISupportExternalScope
{
    private readonly PartitionManager _pipeline = pipeline;
    private readonly IOptionsMonitor<LogOptions> _options = options;
    private readonly ObjectPool<LogEntry> _logEntryPool = logEntryPool;
    private readonly TimeProvider _timeProvider = timeProvider;

    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public ILogger CreateLogger(string categoryName)
        => new LibLogger(categoryName, _pipeline, _scopes, _options, _logEntryPool, _timeProvider);

    public void Dispose()
    {
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;
}

internal sealed class LibLogger(
    string category,
    PartitionManager pipeline,
    IExternalScopeProvider scopes,
    IOptionsMonitor<LogOptions> options,
    ObjectPool<LogEntry> logEntryPool,
    TimeProvider timeProvider) : ILogger
{
    private readonly string _category = category;
    private readonly PartitionManager _pipeline = pipeline;
    private readonly IExternalScopeProvider _scopes = scopes;
    private readonly IOptionsMonitor<LogOptions> _options = options;
    private readonly ObjectPool<LogEntry> _logEntryPool = logEntryPool;
    private readonly TimeProvider _timeProvider = timeProvider;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _scopes.Push(state);

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var entry = _logEntryPool.Get();
        try
        {
            string? deviceId = null;
            List<KeyValuePair<string, object?>>? scopeKvp = null;

            _scopes.ForEachScope((scope, _) =>
            {
                switch (scope)
                {
                    case IReadOnlyDictionary<string, object?> dict:
                        scopeKvp ??= new();
                        foreach (var kv in dict)
                        {
                            scopeKvp.Add(new(kv.Key, kv.Value));
                        }
                        break;
                    case IEnumerable<KeyValuePair<string, object?>> kvps:
                        scopeKvp ??= new();
                        foreach (var kv in kvps)
                        {
                            scopeKvp.Add(kv);
                        }
                        break;
                }
            }, state);

            var deviceKey = _options.CurrentValue.Routing.DeviceKeyField;
            if (scopeKvp is not null)
            {
                var dev = scopeKvp.FirstOrDefault(kv => string.Equals(kv.Key, deviceKey, StringComparison.OrdinalIgnoreCase)).Value;
                deviceId = dev?.ToString();
            }

            if (deviceId is null && state is IEnumerable<KeyValuePair<string, object?>> kvps2)
            {
                var dev = kvps2.FirstOrDefault(kv => string.Equals(kv.Key, deviceKey, StringComparison.OrdinalIgnoreCase)).Value;
                deviceId = dev?.ToString();
            }

            entry.Timestamp = _timeProvider.GetLocalNow().DateTime;
            entry.Level = logLevel;
            entry.Category = _category;
            entry.Message = formatter(state, exception);
            entry.Exception = exception;
            entry.DeviceId = deviceId;
            entry.State = state as IEnumerable<KeyValuePair<string, object?>> is { } kvps ? kvps.ToList() : null;
            entry.Scope = scopeKvp?.Count > 0 ? scopeKvp : null;

            if (!_pipeline.Enqueue(entry))
            {
                _logEntryPool.Return(entry);
            }
        }
        catch
        {
            _logEntryPool.Return(entry);
        }
    }
}

