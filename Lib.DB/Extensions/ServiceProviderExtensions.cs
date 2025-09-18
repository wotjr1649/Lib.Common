#nullable enable
using Lib.DB.Compat;

namespace Lib.DB.Extensions;

/// <summary>
/// 순수 ServiceProvider 사용 시(WinForms/WPF) 수동 초기화를 위한 확장.
/// </summary>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// 레거시 정적 API(SqlHelperCompat)를 현재 ServiceProvider에 연결합니다.
    /// 체이닝이 가능하도록 동일 인스턴스를 반환합니다.
    /// </summary>
    public static IServiceProvider UseBowooLibCompat(this IServiceProvider sp)
    {
        SqlHelperCompat.Configure(sp);
        return sp;
    }
}
