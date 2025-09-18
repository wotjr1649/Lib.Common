namespace Lib.Log.Sink;

using Lib.Log.Format;
using Lib.Log.Option;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// 활성화된 Sink 인스턴스를 생성하는 팩토리.
/// </summary>
public sealed class SinkFactory(IOptions<LogOptions> options, IServiceProvider serviceProvider, TimeProvider timeProvider)
{
    private readonly LogOptions _options = options.Value;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly TimeProvider _timeProvider = timeProvider;

    public IEnumerable<ISink> CreateSinks()
    {
        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();

        if (_options.Local.Enabled)
        {
            var textFmt = new TextFormat(_options);
            var jsonFmt = new JsonFormat(_options);
            var logger = loggerFactory.CreateLogger<LocalSink>();
            yield return new LocalSink(_options, textFmt, jsonFmt, logger, _timeProvider);
        }

        if (_options.Database.Enabled)
        {
            var logger = loggerFactory.CreateLogger<DbSink>();
            yield return new DbSink(_options, logger);
        }
    }
}

