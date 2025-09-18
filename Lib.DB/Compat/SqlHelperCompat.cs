// File: Compat/SqlHelperCompat.cs
#nullable enable

using Lib.DB.Abstractions;
using Lib.DB.Options;
using Lib.DB.Services;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading; // ✅ CancellationToken 사용을 위한 네임스페이스
using System.Xml;

namespace Lib.DB.Compat;

/// <summary>
/// 레거시 SqlHelper 공개 API를 v7 현대화 경로로 연결하는 "슬림 래퍼" 통합판.
/// - Configure(IServiceProvider) 한 번으로 전체 의존성 주입 완료
/// - object? args / SqlParameter[] / object?[] (TypedParams) / Reader / Row / Table / SP 파라미터 캐시 / UpdateDataSet 모두 지원
/// - 기본 CommandType = StoredProcedure (레거시 호환 극대화)
/// - useReadUncommitted 지원(Reader/Row/Table/Execute* 에서 반영)
/// </summary>
public static class SqlHelperCompat
{
    // 주입 대상들 (Configure에서 반드시 설정)
    private static QueryExecutorFacade _facade = default!;
    private static IQueryExecutor _exec = default!;
    private static IParameterBinder _binder = default!;
    private static ISpParameterCache _spCache = default!;
    private static IConnectionFactory _connFactory = default!;
    private static LibOptions _options = new();

    private static volatile bool _initialized;

    /// <summary>
    /// DI로 등록된 서비스를 한 번에 주입합니다.
    /// 필요한 모든 구성요소가 없으면 상세 메시지로 예외를 던집니다.
    /// </summary>
    public static void Configure(IServiceProvider sp)
    {
        if (sp is null) throw new ArgumentNullException(nameof(sp));

        _facade = Resolve<QueryExecutorFacade>(sp, nameof(QueryExecutorFacade));
        _exec = Resolve<IQueryExecutor>(sp, nameof(IQueryExecutor));
        _binder = Resolve<IParameterBinder>(sp, nameof(IParameterBinder));
        _spCache = Resolve<ISpParameterCache>(sp, nameof(ISpParameterCache));
        _connFactory = Resolve<IConnectionFactory>(sp, nameof(IConnectionFactory));
        _options = Resolve<Microsoft.Extensions.Options.IOptions<LibOptions>>(sp, "IOptions<LibOptions>").Value;

        _initialized = true;
    }

    private static T Resolve<T>(IServiceProvider sp, string name) where T : class
        => (T?)sp.GetService(typeof(T)) ?? throw new InvalidOperationException($"{name} 가(이) DI에 등록되지 않았습니다.");

    private static void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("SqlHelperCompat가 초기화되지 않았습니다. 앱 시작 시 SqlHelperCompat.Configure(serviceProvider)를 호출하세요.");
    }

    private static int GetDefaultCommandTimeoutSeconds()
        => (int)(_options.Sql.CommandTimeout.TotalSeconds <= 0 ? 30 : _options.Sql.CommandTimeout.TotalSeconds);

    // -----------------------------------------------
    // A. Facade 경유 Execute* (object? args 바인딩 포함)
    // -----------------------------------------------

    public static int ExecuteNonQuery(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        return _facade.ExecuteNonQueryAsync(connectionString, commandText, commandType, args, useReadUncommitted, cancellationToken)
                      .GetAwaiter().GetResult();
    }

    public static T? ExecuteScalar<T>(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        return _facade.ExecuteScalarAsync<T>(connectionString, commandText, commandType, args, useReadUncommitted, cancellationToken)
                      .GetAwaiter().GetResult();
    }

    public static DataSet ExecuteDataset(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        return _facade.ExecuteDataSetAsync(connectionString, commandText, commandType, args, useReadUncommitted, cancellationToken)
                      .GetAwaiter().GetResult();
    }

    public static XmlReader ExecuteXmlReader(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        return _facade.ExecuteXmlReaderAsync(connectionString, commandText, commandType, args, useReadUncommitted, cancellationToken)
                      .GetAwaiter().GetResult();
    }

    // ---------------------------------------------------------
    // B. IQueryExecutor 경유 Execute* (SqlParameter[] 직접 전달)
    // ---------------------------------------------------------

    public static int ExecuteNonQuery(
        string connectionString,
        string commandText,
        CommandType commandType,
        params SqlParameter[] commandParameters)
    {
        EnsureInitialized();
        return _exec.ExecuteNonQueryAsync(connectionString, commandText, commandType, commandParameters, false, default)
                    .GetAwaiter().GetResult();
    }

    public static T? ExecuteScalar<T>(
        string connectionString,
        string commandText,
        CommandType commandType,
        params SqlParameter[] commandParameters)
    {
        EnsureInitialized();
        return _exec.ExecuteScalarAsync<T>(connectionString, commandText, commandType, commandParameters, false, default)
                    .GetAwaiter().GetResult();
    }

    public static DataSet ExecuteDataSet(
        string connectionString,
        string commandText,
        CommandType commandType,
        params SqlParameter[] commandParameters)
    {
        EnsureInitialized();
        return _exec.ExecuteDataSetAsync(connectionString, commandText, commandType, commandParameters, false, default)
                    .GetAwaiter().GetResult();
    }

    public static XmlReader ExecuteXmlReader(
        string connectionString,
        string commandText,
        CommandType commandType,
        params SqlParameter[] commandParameters)
    {
        EnsureInitialized();
        return _exec.ExecuteXmlReaderAsync(connectionString, commandText, commandType, commandParameters, false, default)
                    .GetAwaiter().GetResult();
    }

    // -----------------------------------------------------
    // C. Typed Params (SP 파라미터 캐시 + 위치 기반 값 주입)
    // -----------------------------------------------------

    public static int ExecuteNonQueryTypedParams(
        string connectionString,
        string storedProcedureName,
        params object?[] parameterValues)
    {
        EnsureInitialized();
        var ps = BindByOrdinalFromCache(connectionString, storedProcedureName, parameterValues);
        return _exec.ExecuteNonQueryAsync(connectionString, storedProcedureName, CommandType.StoredProcedure, ps, false, default)
                    .GetAwaiter().GetResult();
    }

    public static T? ExecuteScalarTypedParams<T>(
        string connectionString,
        string storedProcedureName,
        params object?[] parameterValues)
    {
        EnsureInitialized();
        var ps = BindByOrdinalFromCache(connectionString, storedProcedureName, parameterValues);
        return _exec.ExecuteScalarAsync<T>(connectionString, storedProcedureName, CommandType.StoredProcedure, ps, false, default)
                    .GetAwaiter().GetResult();
    }

    public static DataSet ExecuteDatasetTypedParams(
        string connectionString,
        string storedProcedureName,
        params object?[] parameterValues)
    {
        EnsureInitialized();
        var ps = BindByOrdinalFromCache(connectionString, storedProcedureName, parameterValues);
        return _exec.ExecuteDataSetAsync(connectionString, storedProcedureName, CommandType.StoredProcedure, ps, false, default)
                    .GetAwaiter().GetResult();
    }

    public static XmlReader ExecuteXmlReaderTypedParams(
        string connectionString,
        string storedProcedureName,
        params object?[] parameterValues)
    {
        EnsureInitialized();
        var ps = BindByOrdinalFromCache(connectionString, storedProcedureName, parameterValues);
        return _exec.ExecuteXmlReaderAsync(connectionString, storedProcedureName, CommandType.StoredProcedure, ps, false, default)
                    .GetAwaiter().GetResult();
    }

    // =========================================================
    // D. UpdateDataSet 계열 (SP 3종 / Command 3개 / 자동 생성)
    // =========================================================

    /// <summary>
    /// SP 이름(Insert/Update/Delete) 기반으로 DataSet의 특정 테이블을 갱신합니다.
    /// 파라미터는 SP 파라미터 캐시에서 유도되며, SourceColumn은 컬럼명 = 파라미터명(앞의 '@' 제외) 로 매핑합니다.
    /// Insert/Update는 Current, Delete는 Original 버전을 사용합니다.
    /// </summary>
    public static int UpdateDataSet(
        string connectionString,
        string insertStoredProcedure,
        string updateStoredProcedure,
        string deleteStoredProcedure,
        DataSet dataSet,
        string tableName)
    {
        EnsureInitialized();

        if (dataSet is null) throw new ArgumentNullException(nameof(dataSet));
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("tableName is required", nameof(tableName));
        if (!dataSet.Tables.Contains(tableName)) throw new ArgumentException($"DataSet에 '{tableName}' 테이블이 없습니다.", nameof(tableName));

        // ✅ CS8604 방지: 비-널 보장 후 재사용
        var table = GetRequiredTable(dataSet, tableName);

        using var conn = _connFactory.Create(connectionString);
        conn.Open();

        using var adapter = new SqlDataAdapter
        {
            InsertCommand = BuildSpCommandForAdapter(conn, insertStoredProcedure, AdapterOp.Insert, table),
            UpdateCommand = BuildSpCommandForAdapter(conn, updateStoredProcedure, AdapterOp.Update, table),
            DeleteCommand = BuildSpCommandForAdapter(conn, deleteStoredProcedure, AdapterOp.Delete, table)
        };

        adapter.AcceptChangesDuringUpdate = true; // 기본 동작 유지
        adapter.UpdateBatchSize = 0;             // 드라이버 기본(최적)

        return adapter.Update(table);            // ✅ 비-널 보장된 table 사용
    }

    /// <summary>
    /// SqlCommand(Insert/Update/Delete) 직접 전달로 DataSet의 특정 테이블을 갱신합니다.
    /// 전달된 SqlCommand의 Connection은 내부에서 설정되며, CommandTimeout은 옵션을 따릅니다.
    /// </summary>
    public static int UpdateDataSet(
        string connectionString,
        SqlCommand? insertCommand,
        SqlCommand? updateCommand,
        SqlCommand? deleteCommand,
        DataSet dataSet,
        string tableName)
    {
        EnsureInitialized();

        if (dataSet is null) throw new ArgumentNullException(nameof(dataSet));
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("tableName is required", nameof(tableName));
        if (!dataSet.Tables.Contains(tableName)) throw new ArgumentException($"DataSet에 '{tableName}' 테이블이 없습니다.", nameof(tableName));

        // ✅ CS8604 방지: 비-널 보장 후 재사용
        var table = GetRequiredTable(dataSet, tableName);

        using var conn = _connFactory.Create(connectionString);
        conn.Open();

        // 커맨드 연결/타임아웃 설정
        void Wire(SqlCommand? cmd)
        {
            if (cmd is null) return;
            cmd.Connection = conn;
            if (cmd.CommandTimeout <= 0) cmd.CommandTimeout = GetDefaultCommandTimeoutSeconds();
        }

        Wire(insertCommand);
        Wire(updateCommand);
        Wire(deleteCommand);

        using var adapter = new SqlDataAdapter
        {
            InsertCommand = insertCommand,
            UpdateCommand = updateCommand,
            DeleteCommand = deleteCommand
        };

        adapter.AcceptChangesDuringUpdate = true;
        adapter.UpdateBatchSize = 0;

        return adapter.Update(table); // ✅ 비-널 보장된 table 사용
    }

    /// <summary>
    /// Auto 모드: SELECT 텍스트를 기반으로 CommandBuilder가 Insert/Update/Delete를 자동 생성합니다.
    /// 테이블에 기본키가 있어야 Update/Delete 가 생성됩니다.
    /// </summary>
    public static int UpdateDataSetAuto(
        string connectionString,
        string selectCommandText,
        DataSet dataSet,
        string tableName,
        CommandType selectCommandType = CommandType.Text)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(selectCommandText)) throw new ArgumentException("selectCommandText is required", nameof(selectCommandText));
        if (dataSet is null) throw new ArgumentNullException(nameof(dataSet));
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("tableName is required", nameof(tableName));
        if (!dataSet.Tables.Contains(tableName)) throw new ArgumentException($"DataSet에 '{tableName}' 테이블이 없습니다.", nameof(tableName));

        // ✅ CS8604 방지: 비-널 보장 후 재사용
        var table = GetRequiredTable(dataSet, tableName);

        using var conn = _connFactory.Create(connectionString);
        conn.Open();

        using var select = new SqlCommand(selectCommandText, conn)
        {
            CommandType = selectCommandType,
            CommandTimeout = GetDefaultCommandTimeoutSeconds()
        };

        using var adapter = new SqlDataAdapter(select)
        {
            // 스키마에 PK가 필요하므로 스키마를 가져오도록 설정
            MissingSchemaAction = MissingSchemaAction.AddWithKey,
            AcceptChangesDuringUpdate = true
        };

        using var builder = new SqlCommandBuilder(adapter);
        // 필요한 경우: builder.ConflictOption = ConflictOption.CompareAllSearchableValues;

        adapter.InsertCommand = builder.GetInsertCommand();
        adapter.UpdateCommand = builder.GetUpdateCommand();
        adapter.DeleteCommand = builder.GetDeleteCommand();

        return adapter.Update(table); // ✅ 비-널 보장된 table 사용
    }

    // -----------------------------------------------------------
    // E. ExecuteReader / ExecuteRow / ExecuteTable (동기 조회)
    // -----------------------------------------------------------

    /// <summary>
    /// 동기 SqlDataReader 반환. Dispose 시 연결 자동 해제(CommandBehavior.CloseConnection).
    /// READ UNCOMMITTED 사용 시 트랜잭션으로 적용.
    /// </summary>
    public static SqlDataReader ExecuteReader(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false)
    {
        EnsureInitialized();

        var conn = _connFactory.Create(connectionString);
        conn.Open();

        SqlTransaction? tx = null;
        if (useReadUncommitted)
            tx = conn.BeginTransaction(IsolationLevel.ReadUncommitted);

        try
        {
            using var cmd = new SqlCommand(commandText, conn)
            {
                CommandType = commandType,
                CommandTimeout = GetDefaultCommandTimeoutSeconds(),
                Transaction = tx
            };

            foreach (var p in _binder.BindEnumerable(args))
                cmd.Parameters.Add(p);

            // CloseConnection: 리더 Dispose 시 연결 자동 닫힘(트랜잭션도 함께 정리)
            var reader = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            return reader;
        }
        catch
        {
            try { tx?.Dispose(); } catch { /* ignore */ }
            try { conn.Dispose(); } catch { /* ignore */ }
            throw;
        }
    }

    /// <summary>DataSet의 첫 테이블 첫 행을 반환(없으면 null).</summary>
    public static DataRow? ExecuteRow(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false)
    {
        EnsureInitialized();

        var ds = _facade.ExecuteDataSetAsync(
                    connectionString, commandText, commandType, args, useReadUncommitted, default)
                    .GetAwaiter().GetResult();

        return ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0 ? ds.Tables[0].Rows[0] : null;
    }

    /// <summary>DataSet의 첫 테이블을 반환(없으면 빈 테이블).</summary>
    public static DataTable ExecuteTable(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        object? args = null,
        bool useReadUncommitted = false)
    {
        EnsureInitialized();

        var ds = _facade.ExecuteDataSetAsync(
                    connectionString, commandText, commandType, args, useReadUncommitted, default)
                    .GetAwaiter().GetResult();

        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
    }

    // --------------------------------------
    // F. Parameter Cache 호환(Discover/Cache)
    // --------------------------------------

    public static SqlParameter[] DiscoverSpParameterSet(string connectionString, string storedProcedureName)
    {
        EnsureInitialized();
        return _spCache.Get(connectionString, storedProcedureName);
    }

    public static void CacheParameterSet(string connectionString, string storedProcedureName)
    {
        EnsureInitialized();
        _ = _spCache.Get(connectionString, storedProcedureName);
    }

    public static SqlParameter[] GetCachedParameterSet(string connectionString, string storedProcedureName)
    {
        EnsureInitialized();
        return _spCache.Get(connectionString, storedProcedureName);
    }

    // ----------------
    // 내부 유틸
    // ----------------

    private enum AdapterOp { Insert, Update, Delete }

    /// <summary>
    /// SP 파라미터 캐시에서 복제한 템플릿에 위치 기반 값들을 주입합니다.
    /// </summary>
    private static IEnumerable<SqlParameter> BindByOrdinalFromCache(
        string connectionString,
        string storedProcedureName,
        object?[] values)
    {
        var template = _spCache.Get(connectionString, storedProcedureName); // 클론된 템플릿
        if (values.Length > template.Length)
            throw new ArgumentException($"Too many parameter values: {values.Length} > {template.Length}");

        for (int i = 0; i < values.Length; i++)
            template[i].Value = values[i] ?? DBNull.Value;

        for (int i = values.Length; i < template.Length; i++)
        {
            if (template[i].Direction is ParameterDirection.Input or ParameterDirection.InputOutput)
                template[i].Value = DBNull.Value;
        }

        return template;
    }

    /// <summary>
    /// SP 이름으로 SqlCommand를 만들고, DataTable 컬럼명과 파라미터명을 일치 매핑(@제외)해 SourceColumn/SourceVersion을 설정합니다.
    /// Insert/Update는 Current, Delete는 Original 버전을 사용합니다.
    /// </summary>
    private static SqlCommand BuildSpCommandForAdapter(SqlConnection conn, string storedProcedureName, AdapterOp op, DataTable table)
    {
        if (string.IsNullOrWhiteSpace(storedProcedureName))
            throw new ArgumentException("storedProcedureName is required", nameof(storedProcedureName));

        var cmd = new SqlCommand(storedProcedureName, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = GetDefaultCommandTimeoutSeconds(),
            UpdatedRowSource = UpdateRowSource.None
        };

        // 파라미터 캐시에서 복제본을 받아 커맨드에 부착
        var ps = _spCache.Get(conn.ConnectionString!, storedProcedureName);
        foreach (var p in ps)
        {
            var cloned = (SqlParameter)((ICloneable)p).Clone();

            // 입력/입출력 파라미터만 SourceColumn 매핑
            if (cloned.Direction is ParameterDirection.Input or ParameterDirection.InputOutput)
            {
                var col = cloned.ParameterName.TrimStart('@');
                if (table.Columns.Contains(col))
                {
                    cloned.SourceColumn = col;
                    cloned.SourceVersion = op == AdapterOp.Delete ? DataRowVersion.Original : DataRowVersion.Current;
                }
                else
                {
                    // 컬럼이 없는 파라미터: 값은 호출 시점의 기본값/DB 계산에 맡김
                }
            }

            cmd.Parameters.Add(cloned);
        }

        return cmd;
    }

    /// <summary>
    /// DataSet.Tables[name]을 **반드시** 비-널로 보장하여 반환합니다.
    /// - 존재하지 않으면 상세한 예외를 던져 호출자 실수를 조기에 드러냅니다.
    /// - 이 유틸을 거치면 CS8604(가능한 null 참조 인수) 경고가 발생하지 않습니다.
    /// </summary>
    private static DataTable GetRequiredTable(DataSet dataSet, string tableName)
        => dataSet.Tables[tableName]
            ?? throw new ArgumentException($"DataSet에 '{tableName}' 테이블이 없습니다.", nameof(tableName));
}
