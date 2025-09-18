#nullable enable
using Lib.DB.Diagnostics;
namespace Lib.DB.Options;

/// <summary>DI 등록 시 최상위 옵션 컨테이너.</summary>
public sealed class LibOptions
{
    /// <summary>기본 연결 문자열(선택).</summary>
    public string? DefaultConnectionString { get; set; }

    public SqlExecutionOptions Sql { get; set; } = new();
    public ResilienceOptions Resilience { get; set; } = new();
    public DiagnosticsOptions Diagnostics { get; set; } = new();
}