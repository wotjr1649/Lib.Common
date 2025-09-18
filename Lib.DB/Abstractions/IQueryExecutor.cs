#nullable enable
using Microsoft.Data.SqlClient;
using System.Data;
using System.Xml;

namespace Lib.DB.Abstractions;

/// <summary>
/// DB 명령 실행 파사드. 내부에서 연결/정책/바인딩/진단을 통합합니다.
/// </summary>
public interface IQueryExecutor
{
    Task<int> ExecuteNonQueryAsync(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default);

    Task<T?> ExecuteScalarAsync<T>(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default);

    Task ExecuteReaderAsync(
        string connectionString,
        string commandText,
        Func<SqlDataReader, Task> handle,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default);

    Task<DataSet> ExecuteDataSetAsync(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// XmlReader를 반환합니다. 반환된 Reader를 Dispose하면 내부 연결도 함께 종료됩니다(ConnectionOwnedXmlReader).
    /// </summary>
    Task<XmlReader> ExecuteXmlReaderAsync(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default);
}
