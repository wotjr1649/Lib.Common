// File: Services/SqlExecutorService.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Lib.DB.Abstractions;
using Lib.DB.Internal;
using Lib.DB.Options;

namespace Lib.DB.Services;

/// <summary>
/// 표준 SQL 실행 서비스.
/// - IQueryExecutor 규약을 충실히 따르고 IEnumerable&lt;SqlParameter&gt; 입력을 처리합니다.
/// - CommandTimeout, ReadUncommitted 옵션을 적용합니다.
/// - Diagnostics 훅(로거/메트릭)을 일관되게 호출합니다.
/// </summary>
public sealed class SqlExecutorService : IQueryExecutor
{
    private readonly IOptionsMonitor<LibOptions> _opt;
    private readonly ILogger<SqlExecutorService>? _log;
    private readonly IQueryLogger _queryLogger;
    private readonly IQueryMetricsSink _metrics;

    public SqlExecutorService(
        IOptionsMonitor<LibOptions> options,
        IQueryLogger queryLogger,
        IQueryMetricsSink metrics,
        ILogger<SqlExecutorService>? logger = null)
    {
        _opt = options ?? throw new ArgumentNullException(nameof(options));
        _queryLogger = queryLogger ?? throw new ArgumentNullException(nameof(queryLogger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _log = logger;
    }

    // -------------------------
    // ExecuteNonQueryAsync
    // -------------------------
    public async Task<int> ExecuteNonQueryAsync(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);

        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            var forceRuForSp = _opt.CurrentValue.Sql.ForceRuForStoredProcedure;
            int affected;

            if (commandType == CommandType.StoredProcedure && useReadUncommitted && forceRuForSp)
            {
                affected = await WithStoredProcedureIsolationAsync(conn, async () =>
                {
                    await using var cmd = CreateCommand(conn, commandText, commandType, useReadUncommitted);
                    AddParameters(cmd, parameters);
                    return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            else
            {
                await using var command = CreateCommand(conn, commandText, commandType, useReadUncommitted);
                AddParameters(command, parameters);
                affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            sw.Stop();
            TrackSuccess(commandText, commandType, sw.Elapsed);
            _log?.LogDebug("ExecuteNonQueryAsync OK ({ms} ms): {ct}", sw.ElapsedMilliseconds, Preview(commandText));
            return affected;
        }
        catch (Exception ex)
        {
            sw.Stop();
            TrackFailure(commandText, commandType, sw.Elapsed, ex);
            _log?.LogError(ex, "ExecuteNonQueryAsync failed: {ct}", Preview(commandText));
            throw;
        }
    }

    // -------------------------
    // ExecuteScalarAsync<T>
    // -------------------------
    public async Task<T?> ExecuteScalarAsync<T>(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);

        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            var forceRuForSp = _opt.CurrentValue.Sql.ForceRuForStoredProcedure;
            object? raw;

            if (commandType == CommandType.StoredProcedure && useReadUncommitted && forceRuForSp)
            {
                raw = await WithStoredProcedureIsolationAsync(conn, async () =>
                {
                    await using var cmd = CreateCommand(conn, commandText, commandType, useReadUncommitted);
                    AddParameters(cmd, parameters);
                    return await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            else
            {
                await using var command = CreateCommand(conn, commandText, commandType, useReadUncommitted);
                AddParameters(command, parameters);
                raw = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }

            sw.Stop();
            TrackSuccess(commandText, commandType, sw.Elapsed);
            _log?.LogDebug("ExecuteScalarAsync OK ({ms} ms): {ct}", sw.ElapsedMilliseconds, Preview(commandText));
            return ConvertTo<T>(raw);
        }
        catch (Exception ex)
        {
            sw.Stop();
            TrackFailure(commandText, commandType, sw.Elapsed, ex);
            _log?.LogError(ex, "ExecuteScalarAsync failed: {ct}", Preview(commandText));
            throw;
        }
    }

    // -------------------------
    // ExecuteReaderAsync(handler)
    // -------------------------
    public async Task ExecuteReaderAsync(
        string connectionString,
        string commandText,
        Func<SqlDataReader, Task> handler,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);

        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            var forceRuForSp = _opt.CurrentValue.Sql.ForceRuForStoredProcedure;

            async Task ExecuteAsync()
            {
                await using var cmd = CreateCommand(conn, commandText, commandType, useReadUncommitted);
                AddParameters(cmd, parameters);
                await using var reader = (SqlDataReader)await cmd.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken).ConfigureAwait(false);
                await handler(reader).ConfigureAwait(false);
            }

            if (commandType == CommandType.StoredProcedure && useReadUncommitted && forceRuForSp)
            {
                await WithStoredProcedureIsolationAsync(conn, ExecuteAsync).ConfigureAwait(false);
            }
            else
            {
                await ExecuteAsync().ConfigureAwait(false);
            }

            sw.Stop();
            TrackSuccess(commandText, commandType, sw.Elapsed);
            _log?.LogDebug("ExecuteReaderAsync OK ({ms} ms): {ct}", sw.ElapsedMilliseconds, Preview(commandText));
        }
        catch (Exception ex)
        {
            sw.Stop();
            TrackFailure(commandText, commandType, sw.Elapsed, ex);
            _log?.LogError(ex, "ExecuteReaderAsync failed: {ct}", Preview(commandText));
            throw;
        }
    }

    // -------------------------
    // ExecuteDataSetAsync
    // -------------------------
    public async Task<DataSet> ExecuteDataSetAsync(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);

        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            var forceRuForSp = _opt.CurrentValue.Sql.ForceRuForStoredProcedure;
            DataSet result;

            if (commandType == CommandType.StoredProcedure && useReadUncommitted && forceRuForSp)
            {
                result = await WithStoredProcedureIsolationAsync(conn, async () =>
                {
                    await using var cmd = CreateCommand(conn, commandText, commandType, useReadUncommitted);
                    AddParameters(cmd, parameters);
                    using var adapter = new SqlDataAdapter(cmd);
                    var ds = new DataSet();
                    adapter.Fill(ds);
                    return ds;
                }).ConfigureAwait(false);
            }
            else
            {
                await using var command = CreateCommand(conn, commandText, commandType, useReadUncommitted);
                AddParameters(command, parameters);
                using var adapter = new SqlDataAdapter(command);
                result = new DataSet();
                adapter.Fill(result);
            }

            sw.Stop();
            TrackSuccess(commandText, commandType, sw.Elapsed);
            _log?.LogDebug("ExecuteDataSetAsync OK ({ms} ms): {ct}", sw.ElapsedMilliseconds, Preview(commandText));
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            TrackFailure(commandText, commandType, sw.Elapsed, ex);
            _log?.LogError(ex, "ExecuteDataSetAsync failed: {ct}", Preview(commandText));
            throw;
        }
    }

    // -------------------------
    // ExecuteXmlReaderAsync
    // -------------------------
    public async Task<XmlReader> ExecuteXmlReaderAsync(
        string connectionString,
        string commandText,
        CommandType commandType = CommandType.StoredProcedure,
        IEnumerable<SqlParameter>? parameters = null,
        bool useReadUncommitted = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);

        var sw = Stopwatch.StartNew();
        SqlConnection? conn = null;
        SqlCommand? command = null;
        try
        {
            conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            command = CreateCommand(conn, commandText, commandType, useReadUncommitted);
            AddParameters(command, parameters);

            var reader = await command.ExecuteXmlReaderAsync(cancellationToken).ConfigureAwait(false);
            var owned = new ConnectionOwnedXmlReader(conn, command, reader);

            sw.Stop();
            TrackSuccess(commandText, commandType, sw.Elapsed);
            _log?.LogDebug("ExecuteXmlReaderAsync OK ({ms} ms): {ct}", sw.ElapsedMilliseconds, Preview(commandText));
            return owned;
        }
        catch (Exception ex)
        {
            sw.Stop();
            TrackFailure(commandText, commandType, sw.Elapsed, ex);
            _log?.LogError(ex, "ExecuteXmlReaderAsync failed: {ct}", Preview(commandText));

            if (command is not null)
            {
                await command.DisposeAsync().ConfigureAwait(false);
            }

            if (conn is not null)
            {
                await conn.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    // =========================
    // Helpers
    // =========================

    private SqlCommand CreateCommand(SqlConnection conn, string commandText, CommandType commandType, bool useReadUncommitted)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = commandText;
        cmd.CommandType = commandType;

        var seconds = (int)_opt.CurrentValue.Sql.CommandTimeout.TotalSeconds;
        if (seconds >= 0)
        {
            cmd.CommandTimeout = seconds; // 0 == 무한대
        }

        if (useReadUncommitted && commandType == CommandType.Text)
        {
            cmd.CommandText = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;\n" + cmd.CommandText;
        }

        return cmd;
    }

    private static void AddParameters(SqlCommand cmd, IEnumerable<SqlParameter>? parameters)
    {
        if (parameters is null) return;
        foreach (var p in parameters)
        {
            if (p is null) continue;
            cmd.Parameters.Add(p);
        }
    }

    private static async Task<T> WithStoredProcedureIsolationAsync<T>(SqlConnection conn, Func<Task<T>> body)
    {
        await using (var iso = conn.CreateCommand())
        {
            iso.CommandText = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;";
            await iso.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        return await body().ConfigureAwait(false);
    }

    private static async Task WithStoredProcedureIsolationAsync(SqlConnection conn, Func<Task> body)
    {
        await using (var iso = conn.CreateCommand())
        {
            iso.CommandText = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;";
            await iso.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await body().ConfigureAwait(false);
    }

    private void TrackSuccess(string commandText, CommandType commandType, TimeSpan elapsed)
    {
        var tags = CreateTags(commandType);
        _metrics.Increment("db.command.success", 1, tags);
        _metrics.Observe("db.command.duration_ms", elapsed.TotalMilliseconds, tags);
        _queryLogger.LogCommand(commandText, commandType, elapsed, success: true);
    }

    private void TrackFailure(string commandText, CommandType commandType, TimeSpan elapsed, Exception ex)
    {
        var tags = CreateTags(commandType);
        _metrics.Increment("db.command.failure", 1, tags);
        _metrics.Observe("db.command.duration_ms", elapsed.TotalMilliseconds, tags);
        _queryLogger.LogCommand(commandText, commandType, elapsed, success: false, ex: ex);
    }

    private static IReadOnlyDictionary<string, string> CreateTags(CommandType commandType)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["command_type"] = commandType.ToString()
        };

    private static T? ConvertTo<T>(object? value)
    {
        if (value is null || value is DBNull) return default;
        return (T)Convert.ChangeType(value, typeof(T));
    }

    private static string Preview(string sql)
    {
        if (string.IsNullOrEmpty(sql)) return string.Empty;
        const int Max = 256;
        return sql.Length <= Max ? sql : sql[..Max] + "...";
    }
}
