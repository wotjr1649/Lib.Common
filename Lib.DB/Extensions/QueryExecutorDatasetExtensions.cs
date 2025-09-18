#nullable enable
using Lib.DB.Abstractions;
using System.Data;

namespace Lib.DB.Extensions;

/// <summary>DataSet 채움(여러 테이블) + 테이블명 매핑</summary>
public static class QueryExecutorDatasetExtensions
{
    public static async Task FillDataSetAsync(
        this IQueryExecutor exec,
        string connectionString,
        string commandText,
        DataSet target,
        string[]? tableNames = null,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<Microsoft.Data.SqlClient.SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken ct = default)
    {
        var ds = await exec.ExecuteDataSetAsync(connectionString, commandText, commandType, parameters, useReadUncommitted, ct)
                           .ConfigureAwait(false);

        target.Clear();
        foreach (DataTable t in ds.Tables)
            target.Tables.Add(t.Copy());

        if (tableNames is not null && tableNames.Length == target.Tables.Count)
            for (int i = 0; i < tableNames.Length; i++)
                target.Tables[i].TableName = tableNames[i];
    }
}
