#nullable enable
using Microsoft.Data.SqlClient;
using System.Data;

namespace Lib.DB.Extensions;

/// <summary>DataSet 변경사항을 DB에 반영(Insert/Update/Delete 명령 직접 제공)</summary>
public static class QueryExecutorUpdateExtensions
{
    public static async Task UpdateDataSetAsync(
        string connectionString,
        SqlCommand insertCommand,
        SqlCommand updateCommand,
        SqlCommand deleteCommand,
        DataSet source,
        string tableName,
        int? batchSize = 500,
        CancellationToken ct = default)
    {
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        insertCommand.Connection = conn;
        updateCommand.Connection = conn;
        deleteCommand.Connection = conn;

        using var da = new SqlDataAdapter
        {
            InsertCommand = insertCommand,
            UpdateCommand = updateCommand,
            DeleteCommand = deleteCommand
        };
        if (batchSize is > 0) da.UpdateBatchSize = batchSize.Value;

        await Task.Run(() => da.Update(source, tableName), ct).ConfigureAwait(false);
    }
}
