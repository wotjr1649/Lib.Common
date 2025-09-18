#nullable enable
using Microsoft.Data.SqlClient;
using System.Data;

namespace Lib.DB.Extensions;

/// <summary>간단한 키 기반 테이블은 CommandBuilder로 자동 생성하여 반영</summary>
public static class QueryExecutorUpdateAutoExtensions
{
    public static async Task UpdateDataSetAutoAsync(
        string connectionString,
        string selectSql,
        DataSet source,
        string tableName,
        int? batchSize = 500,
        CancellationToken ct = default)
    {
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var da = new SqlDataAdapter(selectSql, conn);
        using var cb = new SqlCommandBuilder(da);

        da.InsertCommand = cb.GetInsertCommand(true);
        da.UpdateCommand = cb.GetUpdateCommand(true);
        da.DeleteCommand = cb.GetDeleteCommand(true);
        if (batchSize is > 0) da.UpdateBatchSize = batchSize.Value;

        await Task.Run(() => da.Update(source, tableName), ct).ConfigureAwait(false);
    }
}
