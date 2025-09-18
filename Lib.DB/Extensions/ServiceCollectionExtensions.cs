#nullable enable
using Lib.DB.Abstractions;
using Lib.DB.Diagnostics;
using Lib.DB.Internal;
using Lib.DB.Options;
using Lib.DB.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.DB.Extensions;

/// <summary>DI 등록 확장.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Bowoo.Lib 핵심 구성요소를 DI에 등록합니다.
    /// </summary>
    public static IServiceCollection AddBowooLibCore(
        this IServiceCollection services,
        Action<LibOptions>? configure = null)
    {
        services.AddOptions<LibOptions>();
        if (configure is not null) services.Configure(configure);

        // 하위 옵션 분리 바인딩(선택)
        services.AddOptions<DiagnosticsOptions>()
            .Configure<IOptionsMonitor<LibOptions>>((d, all) => d.SampleRate = all.CurrentValue.Diagnostics.SampleRate);

        services.AddSingleton<IConnectionFactory, DefaultConnectionFactory>();
        services.AddSingleton<IParameterBinder, ParameterBinder>();
        services.AddSingleton<ISpParameterCache, SpParameterCache>();
        services.AddSingleton<IResiliencePolicyFactory, DefaultResiliencePolicyFactory>();
        services.AddSingleton<IQueryLogger, DefaultQueryLogger>();
        services.AddSingleton<IQueryMetricsSink, DefaultMetricsSink>();
        services.AddSingleton<IQueryExecutor, SqlExecutorService>();
        services.AddSingleton<QueryExecutorFacade>();
        return services;
    }
}

