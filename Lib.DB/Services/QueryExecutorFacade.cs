#nullable enable
using Lib.DB.Abstractions;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Xml;

namespace Lib.DB.Services;

/// <summary>
/// 파라미터 바인딩을 내장한 실행 파사드(레거시 편의 + 새 구조 결합)
/// </summary>
public sealed class QueryExecutorFacade
{
    private readonly IQueryExecutor _exec;
    private readonly IParameterBinder _binder;

    public QueryExecutorFacade(IQueryExecutor exec, IParameterBinder binder)
    {
        _exec = exec;
        _binder = binder;
    }

    public Task<int> ExecuteNonQueryAsync(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false,
        CancellationToken ct = default)
        => _exec.ExecuteNonQueryAsync(connectionString, commandText, commandType,
            _binder.BindEnumerable(args), useReadUncommitted, ct);

    public Task<T?> ExecuteScalarAsync<T>(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false,
        CancellationToken ct = default)
        => _exec.ExecuteScalarAsync<T>(connectionString, commandText, commandType,
            _binder.BindEnumerable(args), useReadUncommitted, ct);

    public Task ExecuteReaderAsync(
        string connectionString,
        string commandText,
        Func<SqlDataReader, Task> handle,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false,
        CancellationToken ct = default)
        => _exec.ExecuteReaderAsync(connectionString, commandText, handle, commandType,
            _binder.BindEnumerable(args), useReadUncommitted, ct);

    public Task<DataSet> ExecuteDataSetAsync(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false,
        CancellationToken ct = default)
        => _exec.ExecuteDataSetAsync(connectionString, commandText, commandType,
            _binder.BindEnumerable(args), useReadUncommitted, ct);

    public Task<XmlReader> ExecuteXmlReaderAsync(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false,
        CancellationToken ct = default)
        => _exec.ExecuteXmlReaderAsync(connectionString, commandText, commandType,
            _binder.BindEnumerable(args), useReadUncommitted, ct);
}
