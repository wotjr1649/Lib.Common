#nullable enable

namespace Lib.DB.Services;

using System.Data;
using System.Xml;
using Microsoft.Data.SqlClient;
using Lib.DB.Abstractions;
using Lib.DB.Options;
using Microsoft.Extensions.Options;
using Polly;



/// <summary>
/// IQueryExecutor 데코레이터: Polly v8 ResiliencePipeline 적용.
/// ExecuteAsync의 (CancellationToken) 단일 오버로드만 사용하여 오버로드 모호성을 제거합니다.
/// </summary>
public sealed class ResilientQueryExecutor : IQueryExecutor
{
    private readonly ResiliencePipeline _pipe;
    private readonly IQueryExecutor _inner;

    public ResilientQueryExecutor(
        IQueryExecutor inner,
        IResiliencePolicyFactory factory,
        IOptionsMonitor<ResilienceOptions> opt)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _pipe = factory.Create(opt.CurrentValue);
    }

    public Task<int> ExecuteNonQueryAsync(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
        => _pipe.ExecuteAsync(async ct =>
            await _inner.ExecuteNonQueryAsync(connectionString, commandText, commandType, parameters, useReadUncommitted, ct),
            cancellationToken
        ).AsTask();

    public Task<T?> ExecuteScalarAsync<T>(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
        => _pipe.ExecuteAsync(async ct =>
            await _inner.ExecuteScalarAsync<T>(connectionString, commandText, commandType, parameters, useReadUncommitted, ct),
            cancellationToken
        ).AsTask();

    public Task ExecuteReaderAsync(
        string connectionString,
        string commandText,
        Func<SqlDataReader, Task> handle,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
        => _pipe.ExecuteAsync(async ct =>
            await _inner.ExecuteReaderAsync(connectionString, commandText, handle, commandType, parameters, useReadUncommitted, ct),
            cancellationToken
        ).AsTask();

    public Task<DataSet> ExecuteDataSetAsync(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
        => _pipe.ExecuteAsync(async ct =>
            await _inner.ExecuteDataSetAsync(connectionString, commandText, commandType, parameters, useReadUncommitted, ct),
            cancellationToken
        ).AsTask();

    public Task<XmlReader> ExecuteXmlReaderAsync(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
        => _pipe.ExecuteAsync(async ct =>
            await _inner.ExecuteXmlReaderAsync(connectionString, commandText, commandType, parameters, useReadUncommitted, ct),
            cancellationToken
        ).AsTask();
}