// File: Extensions/BowooLibAllExtensions.cs
#nullable enable
using System;
using Lib.DB.Abstractions;
using Lib.DB.Internal;
using Lib.DB.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Lib.DB.Extensions
{
    /// <summary>
    /// '한 줄 등록 + 레거시 초기화 + 런타임 파사드'를 모두 포함하는 확장.
    /// </summary>
    public static class BowooLibAllExtensions
    {
        /// <summary>
        /// 모든 구성요소 등록 + HostedService 초기화 + IRuntime 싱글톤 제공.
        /// </summary>
        public static IServiceCollection AddBowooLibAll(this IServiceCollection services, Action<LibOptions>? configure = null)
        {
            // 코어 서비스 등록
            services.AddBowooLibCore(configure);

            // 런타임 파사드 등록
            services.AddSingleton<IRuntime, BowooRuntime>();

            // 호스트 기반 앱에서는 정적 Compat 자동 초기화
            services.AddHostedService<CompatHostedService>();
            return services;
        }

        /// <summary>
        /// IConfiguration 바인딩 기반 등록.
        /// appsettings.json의 특정 섹션을 LibOptions에 바인딩합니다(기본: "Bowoo").
        /// </summary>
        public static IServiceCollection AddBowooLibAll(this IServiceCollection services, IConfiguration config, string sectionName = "Bowoo")
            => services.AddBowooLibAll(opt => config.GetSection(sectionName).Bind(opt));

        /// <summary>
        /// HostApplicationBuilder용 sugar. builder.Services.AddBowooLibAll(...)과 동일.
        /// </summary>
        public static IHostApplicationBuilder AddBowooLibAll(this IHostApplicationBuilder builder, Action<LibOptions>? configure = null)
        {
            builder.Services.AddBowooLibAll(configure);
            return builder;
        }
    }
}
