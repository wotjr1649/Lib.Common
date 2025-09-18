// File: Extensions/LegacyRowTableExtensions.cs
#nullable enable
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Lib.DB.Abstractions;
using Lib.DB.Services;

namespace Lib.DB.Extensions;

/// <summary>
/// 레거시 ExecuteRow/ExecuteTable 계열 보강을 위한 확장 메서드.
/// - 복잡한 Reader 핸들러 없이 빠르게 Row/Table을 얻을 때 사용.
/// - 기본 CommandType = StoredProcedure (레거시 호환성 극대화).
/// </summary>
public static class QueryExecutorRowTableExtensions
{
    /// <summary>
    /// 쿼리 결과의 첫 번째 행을 반환합니다. 없으면 null.
    /// IQueryExecutor 직접 사용 버전 (SqlParameter[] 기반).
    /// </summary>
    public static async Task<DataRow?> ExecuteRowAsync(
        this IQueryExecutor executor,
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
    {
        var table = await executor.ExecuteTableAsync(
            connectionString, commandText, commandType, parameters, useReadUncommitted, cancellationToken
        ).ConfigureAwait(false);

        return table.Rows.Count > 0 ? table.Rows[0] : null;
    }

    /// <summary>
    /// 쿼리 결과를 DataTable로 반환합니다.
    /// IQueryExecutor 직접 사용 버전 (SqlParameter[] 기반).
    /// </summary>
    public static async Task<DataTable> ExecuteTableAsync(
        this IQueryExecutor executor,
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
    {
        var table = new DataTable();

        await executor.ExecuteReaderAsync(
            connectionString,
            commandText,
            reader =>
            {
                // DataTable.Load는 동기 실행. 콜백 내에서 한 번만 호출하여 전체 로드.
                table.Load(reader);
                return Task.CompletedTask;
            },
            commandType,
            parameters,
            useReadUncommitted,
            cancellationToken
        ).ConfigureAwait(false);

        return table;
    }

    // ---------- Facade 버전: Facade는 object? 인자 바인딩을 내장(가정) ----------

    /// <summary>
    /// 쿼리 결과의 첫 번째 행을 반환합니다. 없으면 null.
    /// QueryExecutorFacade 사용 버전 (object? args 기반).
    /// </summary>
    public static async Task<DataRow?> ExecuteRowAsync(
        this QueryExecutorFacade facade,
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
    {
        var table = await facade.ExecuteTableAsync(
            connectionString, commandText, commandType, args, useReadUncommitted, cancellationToken
        ).ConfigureAwait(false);

        return table.Rows.Count > 0 ? table.Rows[0] : null;
    }

    /// <summary>
    /// 쿼리 결과를 DataTable로 반환합니다.
    /// QueryExecutorFacade 사용 버전 (object? args 기반).
    /// ※ Facade에 ExecuteReaderAsync(object? args) 오버로드가 없는 경우를 대비해 DataSet 경유.
    /// </summary>
    public static async Task<DataTable> ExecuteTableAsync(
        this QueryExecutorFacade facade,
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
    {
        // DataSet으로 받아 첫 테이블만 반환 (레거시 ExecuteTable 동작과 동일)
        var ds = await facade.ExecuteDataSetAsync(
            connectionString, commandText, commandType, args, useReadUncommitted, cancellationToken
        ).ConfigureAwait(false);

        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
    }
}
