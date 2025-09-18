#nullable enable
namespace Lib.DB.Diagnostics;

/// <summary>진단 로깅 옵션.</summary>
public sealed class DiagnosticsOptions
{
    /// <summary>0.0~1.0 샘플링 비율. 1.0 = 전량 로깅.</summary>
    public double SampleRate { get; set; } = 0.1;

    /// <summary>로그 시 CommandText 잘라낼 최대 길이.</summary>
    public int CommandTextMaxLength { get; set; } = 800;

    /// <summary>성공 쿼리 로깅 여부.</summary>
    public bool LogOnSuccess { get; set; } = false;

    /// <summary>실패 쿼리 반드시 로깅.</summary>
    public bool LogOnFailure { get; set; } = true;
}
