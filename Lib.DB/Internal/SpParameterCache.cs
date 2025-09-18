#nullable enable
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;

namespace Lib.DB.Internal;

/// <summary>
/// Sp 파라미터 캐시(경량). DeriveParameters 결과를 캐시하고 클론을 반환합니다.
/// </summary>
public sealed class SpParameterCache : Abstractions.ISpParameterCache
{
    private readonly ConcurrentDictionary<string, SqlParameter[]> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SqlParameter[] Get(string connectionString, string storedProcedureName)
    {
        var key = $"{connectionString}|{storedProcedureName}";
        if (_cache.TryGetValue(key, out var cached))
            return CloneArray(cached);

        using var conn = new SqlConnection(connectionString);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = storedProcedureName;
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        conn.Open();
        SqlCommandBuilder.DeriveParameters(cmd);

        var derived = cmd.Parameters.Cast<SqlParameter>().Select(Clone).ToArray();
        _cache[key] = CloneArray(derived);
        return derived;
    }

    private static SqlParameter Clone(SqlParameter p)
    {
        var c = (SqlParameter)((ICloneable)p).Clone();
        c.Value = DBNull.Value;
        return c;
    }

    private static SqlParameter[] CloneArray(SqlParameter[] src)
        => src.Select(Clone).ToArray();
}
