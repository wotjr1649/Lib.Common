// File: Internal/BowooLibCompatHostedService.cs
#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lib.DB.Compat;

namespace Lib.DB.Internal
{
    /// <summary>
    /// 호스트 시작 시 한 번 SqlHelperCompat.Configure를 호출해 레거시 정적 API를 활성화합니다.
    /// 웹/콘솔/Worker 등 IHost 기반에서 자동 동작합니다.
    /// WPF/WinForms 등 순수 ServiceProvider만 사용하는 경우에는 ServiceProviderExtensions.UseBowooLibCompat()를 호출하세요.
    /// </summary>
    internal sealed class CompatHostedService : IHostedService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<CompatHostedService>? _log;

        public CompatHostedService(IServiceProvider sp, ILogger<CompatHostedService>? log = null)
        {
            _sp = sp;
            _log = log;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            SqlHelperCompat.Configure(_sp);
            _log?.LogInformation("Bowoo.Lib Compat initialized.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
