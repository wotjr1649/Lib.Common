#nullable enable

using Microsoft.Data.SqlClient;

namespace Lib.DB.Abstractions;

/// <summary>
/// Stored Procedure 파라미터(이름/형식/방향 등) 캐시를 제공합니다.
/// </summary>
public interface ISpParameterCache
{
    /// <summary>DeriveParameters 결과를 캐시에서 꺼내거나 새로 유도합니다.</summary>
    SqlParameter[] Get(string connectionString, string storedProcedureName);
}
