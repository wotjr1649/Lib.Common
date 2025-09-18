namespace Lib.Log;

using Lib.Log.Abstractions;
using Lib.Log.Hosting;
using Lib.Log.Model;
using Lib.Log.Option;
using Lib.Log.Pipeline;
using Lib.Log.Provider;
using Lib.Log.Routing;
using Lib.Log.Sink;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

/// <summary>
/// IServiceCollection에 대한 확장 메서드를 정의하여 라이브러리의 서비스를 초기화합니다.
/// </summary>
public static class ServiceCollectionExtension
{
    /// <summary>
    /// DI 컨테이너에 Lib.Log 라이브러리의 모든 필수 서비스를 등록합니다.
    /// </summary>
    /// <param name="services">서비스를 추가할 IServiceCollection입니다.</param>
    /// <param name="configure">로그 옵션을 코드에서 직접 구성하기 위한 Action입니다.</param>
    /// <returns>체이닝을 위해 IServiceCollection을 반환합니다.</returns>
    public static IServiceCollection AddLibLog(this IServiceCollection services, Action<LogOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<LogOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }
        optionsBuilder.ValidateOnStart();

        services.AddSingleton<IConfigureOptions<LogOptions>, ConfigureLogOptions>();
        services.AddSingleton<IValidateOptions<LogOptions>, LogOptionsValidator>();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ILogSamplingStrategy, RandomSamplingStrategy>();

        // --- 2. 다시 사용 풀 ---
        services.AddSingleton<SinkFactory>();
        services.AddSingleton<IPooledObjectPolicy<LogEntry>, LogEntryPooledObjectPolicy>();
        services.AddSingleton<ObjectPool<LogEntry>>(sp =>
        {
            var policy = sp.GetRequiredService<IPooledObjectPolicy<LogEntry>>();
            return new DefaultObjectPool<LogEntry>(policy, 1024); // 최대 1024개 객체 유지
        });

        services.TryAddSingleton<ILogRouter, LogRouter>();

        // --- 3. 백그라운드 작업(IHostedService) 등록 ---
        services.AddSingleton<PartitionManager>();
        services.AddHostedService(sp => sp.GetRequiredService<PartitionManager>());
        services.AddHostedService<DatabaseInitializer>();
        services.AddHostedService<FtpUpload>();

        // --- 4. 공개 API 및 .NET 로깅 연동 ---
        services.AddSingleton<ILoggerProvider, LoggerProvider>();
        services.TryAddSingleton<ILogService, LogService>();

        services.Configure<LoggerFilterOptions>(opts =>
        {
            var categoriesToSuppress = new[]
            {
                "Microsoft.Hosting.Lifetime",
                typeof(FtpUpload).FullName,
                typeof(DatabaseInitializer).FullName,
                typeof(LocalSink).FullName,
                typeof(DbSink).FullName,
                typeof(PartitionManager).FullName,
                typeof(LoggerProvider).FullName,
            };

            foreach (var cat in categoriesToSuppress)
            {
                opts.Rules.Add(new LoggerFilterRule(
                    providerName: typeof(LoggerProvider).FullName,
                    categoryName: cat,
                    logLevel: LogLevel.None,
                    filter: null));
            }
        });

        return services;
    }
}
/// <summary>
/// ILogService에 대한 구현체. 라이브러리 외부에서는 숨깁니다.
/// </summary>
internal sealed class LogService(ILoggerFactory factory) : ILogService
{
    private readonly ILoggerFactory _factory = factory;

    public void Trace(string category, string message, string? deviceId = null)
        => WithScope(category, deviceId, null, l => l.LogTrace(message));
    public void Debug(string category, string message, string? deviceId = null)
        => WithScope(category, deviceId, null, l => l.LogDebug(message));
    public void Info(string category, string message, string? deviceId = null)
        => WithScope(category, deviceId, null, l => l.LogInformation(message));
    public void Warn(string category, string message, string? deviceId = null)
        => WithScope(category, deviceId, null, l => l.LogWarning(message));
    public void Error(string category, string message, string? deviceId = null, Exception? ex = null)
        => WithScope(category, deviceId, null, l => l.LogError(ex, message));
    public void Critical(string category, string message, string? deviceId = null, Exception? ex = null)
        => WithScope(category, deviceId, null, l => l.LogCritical(ex, message));

    public void InfoDb(string category, string message, string? deviceId = null)
        => WithScope(category, deviceId, "Database", l => l.LogInformation(message));
    public void WarnDb(string category, string message, string? deviceId = null)
        => WithScope(category, deviceId, "Database", l => l.LogWarning(message));
    public void ErrorDb(string category, string message, string? deviceId = null, Exception? ex = null)
        => WithScope(category, deviceId, "Database", l => l.LogError(ex, message));

    public void InfoFile(string category, string message, string? deviceId = null)
        => WithScope(category, deviceId, "Local", l => l.LogInformation(message));
    public void WarnFile(string category, string message, string? deviceId = null)
        => WithScope(category, deviceId, "Local", l => l.LogWarning(message));
    public void ErrorFile(string category, string message, string? deviceId = null, Exception? ex = null)
        => WithScope(category, deviceId, "Local", l => l.LogError(ex, message));

    public ILogger CreateLogger(string category) => _factory.CreateLogger(category);

    /// <summary>
    /// 로깅 범위(Scope)를 설정하고 실제 로그 작업을 수행하는 중앙 헬퍼 메서드.
    /// </summary>
    private void WithScope(string category, string? deviceId, string? targetSinks, Action<ILogger> action)
    {
        var logger = _factory.CreateLogger(category);

        if (deviceId != null || targetSinks != null)
        {
            var scope = new List<KeyValuePair<string, object?>>();
            if (deviceId != null)
            {
                scope.Add(new("DeviceId", deviceId));
            }
            if (targetSinks != null)
            {
                scope.Add(new("Sinks", targetSinks));
            }

            using (logger.BeginScope(scope))
            {
                action(logger);
            }
        }
        else
        {
            action(logger);
        }
    }
}