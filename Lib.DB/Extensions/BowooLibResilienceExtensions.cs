// File: Extensions/BowooLibResilienceExtensions.cs
#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Lib.DB.Abstractions;
using Lib.DB.Services;
using Lib.DB.Options;
using Microsoft.Extensions.Options;

namespace Lib.DB.Extensions
{
    /// <summary>
    /// IQueryExecutor에 회복성(Polly) 데코레이터를 적용하는 선택적 확장.
    /// AddBowooLibAll 이후에 호출하세요.
    /// </summary>
    public static class BowooLibResilienceExtensions
    {
        public static IServiceCollection UseResilientQueryExecutor(this IServiceCollection services)
        {
            // SqlExecutorService(구현체)를 래핑하는 ResilientQueryExecutor를 등록하고 마지막에 IQueryExecutor를 교체
            services.AddSingleton<ResilientQueryExecutor>(sp =>
                new ResilientQueryExecutor(
                    inner: sp.GetRequiredService<SqlExecutorService>(),
                    factory: sp.GetRequiredService<IResiliencePolicyFactory>(),
                    opt: sp.GetRequiredService<IOptionsMonitor<ResilienceOptions>>()
                ));
            services.Replace(ServiceDescriptor.Singleton<IQueryExecutor>(sp => sp.GetRequiredService<ResilientQueryExecutor>()));
            return services;
        }
    }
}
