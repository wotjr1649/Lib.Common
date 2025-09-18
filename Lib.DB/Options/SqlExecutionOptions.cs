#nullable enable

namespace Lib.DB.Options;

/// <summary>쿼리 실행 공통 옵션.</summary>
public sealed class SqlExecutionOptions
{
    /// <summary>기본 CommandTimeout.</summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>기본 RU(Read Uncommitted) 사용 여부(읽기 전용 권장).</summary>
    public bool DefaultReadUncommitted { get; set; } = false;

    /// <summary>StoredProcedure에도 RU를 강제할지(세션 레벨 SET).</summary>
    public bool ForceRuForStoredProcedure { get; set; } = false;
}
