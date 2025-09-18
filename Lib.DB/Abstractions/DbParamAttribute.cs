#nullable enable
using System.Data;

namespace Lib.DB.Abstractions;

/// <summary>
/// 통합 Db 파라미터 특성.
/// - 레거시 및 내부 구현의 혼선을 없애기 위해 공개용으로 1종만 사용합니다.
/// - IParameterBinder가 이 특성을 기반으로 SqlParameter를 생성합니다.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class DbParamAttribute : Attribute
{
    /// <summary>명시적 파라미터 이름. 생략 시 멤버명을 사용합니다.</summary>
    public string? Name { get; init; }

    /// <summary>IN/OUT/INOUT/ReturnValue</summary>
    public ParameterDirection Direction { get; init; } = ParameterDirection.Input;

    /// <summary>문자열/바이너리 Size</summary>
    public int? Size { get; init; }

    /// <summary>Precision(소수점 포함 자릿수)</summary>
    public byte? Precision { get; init; }

    /// <summary>Scale(소수점 자리)</summary>
    public byte? Scale { get; init; }

    /// <summary>DbType 힌트(지정 시 우선)</summary>
    public DbType? DbType { get; init; }

    /// <summary>UDT/TVP/특수형 TypeName</summary>
    public string? TypeName { get; init; }

    /// <summary>고정 길이 여부</summary>
    public bool? IsFixedLength { get; init; }

    /// <summary>Nullable 힌트</summary>
    public bool? IsNullable { get; init; }
}
