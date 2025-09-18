#nullable enable
using Microsoft.Data.SqlClient;

namespace Lib.DB.Abstractions;

/// <summary>
/// 익명/DTO/레코드 등에서 SqlParameter 배열을 생성합니다.
/// 복잡한 OUT/RETURN/TVP 매핑은 필요 시 구현체에서 확장합니다.
/// </summary>
public interface IParameterBinder
{
    /// <summary>레거시 호환: args를 즉시 배열로 바인딩하여 반환합니다.</summary>
    SqlParameter[] Bind(object? args);

    /// <summary>레거시 호환: 단일 파라미터를 생성합니다. 필요 시 내부 규칙/특성 반영.</summary>
    SqlParameter Create(string name, object? value);

    /// <summary>
    /// 가장 일반적인 진입점. args를 순회 가능한 SqlParameter 열거로 바인딩합니다.
    /// DTO/익명객체/Dictionary를 SqlParameter[]로 변환
    /// </summary>
    IEnumerable<SqlParameter> BindEnumerable(object? args);
}
