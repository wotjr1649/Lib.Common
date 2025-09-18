#nullable enable
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Lib.DB.Options;
using Lib.DB.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.DB;

/// <summary>
/// 새 Bowoo 데이터 액세스 진입점.
/// QueryExecutorFacade 기반 비동기 API를 제공하여 호출부 단순화를 돕습니다.
/// </summary>
public sealed class DbClient
{
    private readonly QueryExecutorFacade _facade;
    private readonly IOptionsMonitor<LibOptions> _options;
    private readonly string? _overrideConnectionString;

    private DbClient(IOptionsMonitor<LibOptions> options, QueryExecutorFacade facade, string? overrideConnectionString)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _facade = facade ?? throw new ArgumentNullException(nameof(facade));
        _overrideConnectionString = overrideConnectionString;
    }

    private DbClient(IOptionsMonitor<LibOptions> options, QueryExecutorFacade facade)
        : this(options, facade, null)
    {
    }

    /// <summary>
    /// 서비스 제공자에서 필요한 의존성을 해결해 DbClient를 생성합니다.
    /// </summary>
    public static DbClient Create(IServiceProvider serviceProvider)
    {
        if (serviceProvider is null) throw new ArgumentNullException(nameof(serviceProvider));
        return new DbClient(
            serviceProvider.GetRequiredService<IOptionsMonitor<LibOptions>>(),
            serviceProvider.GetRequiredService<QueryExecutorFacade>());
    }

    /// <summary>
    /// INSERT/UPDATE/DELETE 등 영향을 받은 행 수를 반환합니다.
    /// </summary>
    public Task<int> ExecuteNonQueryAsync(
        string commandText,
        object? args = null,
        CommandType commandType = CommandType.StoredProcedure,
        string? connectionString = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
        => _facade.ExecuteNonQueryAsync(
            ResolveConnectionString(connectionString),
            commandText,
            commandType,
            args,
            useReadUncommitted,
            cancellationToken);

    /// <summary>
    /// 단일 값을 조회합니다.
    /// </summary>
    public Task<T?> ExecuteScalarAsync<T>(
        string commandText,
        object? args = null,
        CommandType commandType = CommandType.StoredProcedure,
        string? connectionString = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
        => _facade.ExecuteScalarAsync<T>(
            ResolveConnectionString(connectionString),
            commandText,
            commandType,
            args,
            useReadUncommitted,
            cancellationToken);

    /// <summary>
    /// SqlDataReader를 직접 처리하는 경우 사용합니다.
    /// </summary>
    public Task ExecuteReaderAsync(
        string commandText,
        Func<SqlDataReader, Task> handler,
        object? args = null,
        CommandType commandType = CommandType.StoredProcedure,
        string? connectionString = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
        => _facade.ExecuteReaderAsync(
            ResolveConnectionString(connectionString),
            commandText,
            handler,
            commandType,
            args,
            useReadUncommitted,
            cancellationToken);

    /// <summary>
    /// DataSet 전체를 조회합니다.
    /// </summary>
    public Task<DataSet> ExecuteDataSetAsync(
        string commandText,
        object? args = null,
        CommandType commandType = CommandType.StoredProcedure,
        string? connectionString = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
        => _facade.ExecuteDataSetAsync(
            ResolveConnectionString(connectionString),
            commandText,
            commandType,
            args,
            useReadUncommitted,
            cancellationToken);

    /// <summary>
    /// 기본 연결 문자열을 변경한 새 DbClient를 반환합니다.
    /// </summary>
    public DbClient WithConnection(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("연결 문자열이 필요합니다.", nameof(connectionString));
        return new DbClient(_options, _facade, connectionString);
    }

    /// <summary>
    /// 명령 체이닝을 지원하는 빌더를 생성합니다.
    /// </summary>
    public DbCallBuilder Call(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText)) throw new ArgumentException("명령문을 입력하세요.", nameof(commandText));
        return new DbCallBuilder(this, commandText);
    }

    private string ResolveConnectionString(string? connectionString)
        => connectionString
            ?? _overrideConnectionString
            ?? _options.CurrentValue.DefaultConnectionString
            ?? throw new InvalidOperationException("LibOptions.DefaultConnectionString이 설정되지 않았습니다.");

    public sealed class DbCallBuilder
    {
        private readonly DbClient _client;
        private readonly string _commandText;
        private object? _args;
        private CommandType _commandType = CommandType.StoredProcedure;
        private string? _connection;
        private bool _useReadUncommitted;

        internal DbCallBuilder(DbClient client, string commandText)
        {
            _client = client;
            _commandText = commandText;
        }

        public DbCallBuilder WithArgs(object? args)
        {
            _args = args;
            return this;
        }

        public DbCallBuilder WithCommandType(CommandType commandType)
        {
            _commandType = commandType;
            return this;
        }

        public DbCallBuilder AsText()
            => WithCommandType(CommandType.Text);

        public DbCallBuilder WithConnection(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("연결 문자열이 필요합니다.", nameof(connectionString));
            _connection = connectionString;
            return this;
        }

        public DbCallBuilder UseReadUncommitted(bool enable = true)
        {
            _useReadUncommitted = enable;
            return this;
        }

        public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default)
            => _client.ExecuteNonQueryAsync(_commandText, _args, _commandType, _connection, _useReadUncommitted, cancellationToken);

        public Task<T?> ExecuteScalarAsync<T>(CancellationToken cancellationToken = default)
            => _client.ExecuteScalarAsync<T>(_commandText, _args, _commandType, _connection, _useReadUncommitted, cancellationToken);

        public Task ExecuteReaderAsync(Func<SqlDataReader, Task> handler, CancellationToken cancellationToken = default)
            => _client.ExecuteReaderAsync(_commandText, handler, _args, _commandType, _connection, _useReadUncommitted, cancellationToken);

        public Task<DataSet> ExecuteDataSetAsync(CancellationToken cancellationToken = default)
            => _client.ExecuteDataSetAsync(_commandText, _args, _commandType, _connection, _useReadUncommitted, cancellationToken);
    }
}
