#nullable enable
using Lib.DB.Services;
using System.Data;

namespace Lib.DB.Extensions;

public static class QueryExecutorDatasetFacadeExtensions
{
    public static async Task FillDataSetAsync(
        this QueryExecutorFacade facade,
        string connectionString,
        string commandText,
        DataSet target,
        string[]? tableNames = null,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false,
        CancellationToken ct = default)
    {
        var ds = await facade.ExecuteDataSetAsync(connectionString, commandText, commandType, args, useReadUncommitted, ct)
                             .ConfigureAwait(false);

        target.Clear();
        foreach (DataTable t in ds.Tables)
            target.Tables.Add(t.Copy());

        if (tableNames is not null && tableNames.Length == target.Tables.Count)
            for (int i = 0; i < tableNames.Length; i++)
                target.Tables[i].TableName = tableNames[i];
    }
}
