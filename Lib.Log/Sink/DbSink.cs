namespace Lib.Log.Sink;

using Lib.Log.Option;
using Lib.Log.Model;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 데이터베이스(MSSQL)에 로그를 적재하는 Sink.
/// </summary>
public sealed class DbSink : ISink
{
    private readonly LogOptions _opt;
    private readonly ILogger<DbSink> _logger;
    private readonly ResiliencePipeline _pipeline;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly DataTable _logTableSchema;

    public string Name => "Database";

    public DbSink(LogOptions opt, ILogger<DbSink> logger)
    {
        _opt = opt;
        _logger = logger;
        _concurrencyLimiter = new SemaphoreSlim(Math.Max(1, opt.Database.MaxConcurrency));

        var cbOpt = opt.Database.CircuitBreaker;
        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args => ValueTask.FromResult(args.Outcome.Exception is SqlException)
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.9,
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromSeconds(cbOpt.BreakSec),
                OnOpened = args =>
                {
                    _logger.LogWarning("MssqlLogSink circuit breaker opened for {BreakDuration}. Reason: {Exception}", args.BreakDuration, args.Outcome.Exception);
                    return default;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("MssqlLogSink circuit breaker closed. Resuming operations.");
                    return default;
                }
            })
            .Build();

        _logTableSchema = new DataTable(_opt.Database.TableName);
        _logTableSchema.Columns.Add("Timestamp", typeof(DateTime));
        _logTableSchema.Columns.Add("Level", typeof(string));
        _logTableSchema.Columns.Add("Category", typeof(string));
        _logTableSchema.Columns.Add("DeviceId", typeof(string));
        _logTableSchema.Columns.Add("Message", typeof(string));
        _logTableSchema.Columns.Add("Exception", typeof(string));
        _logTableSchema.Columns.Add("Scope", typeof(string));
    }

    public async Task WriteBatchAsync(IReadOnlyList<LogEntry> entries, CancellationToken ct)
    {
        if (!_opt.Database.Enabled || string.IsNullOrEmpty(_opt.Database.ConnectionString))
        {
            return;
        }

        await _concurrencyLimiter.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _pipeline.ExecuteAsync(async token =>
            {
                foreach (var batch in entries.Chunk(_opt.Database.BatchSize))
                {
                    await WriteToDatabaseAsync(batch, token).ConfigureAwait(false);
                }
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "All option types are preserved via DynamicDependency; only public getters/setters are bound.")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.FormattingOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.RoutingOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.PartitionGroupOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.LocalSinkOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.RolloverOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.DbSinkOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.FtpSinkOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.CircuitBreakerOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.RootingOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.RootRule))]
    private async Task WriteToDatabaseAsync(IEnumerable<LogEntry> batch, CancellationToken ct)
    {
        var dataTable = CreateDataTable(batch);

        await using var connection = new SqlConnection(_opt.Database.ConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null)
        {
            DestinationTableName = _opt.Database.TableName,
            BatchSize = dataTable.Rows.Count
        };

        foreach (DataColumn col in dataTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(dataTable, ct).ConfigureAwait(false);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
    private DataTable CreateDataTable(IEnumerable<LogEntry> entries)
    {
        var table = _logTableSchema.Clone();

        foreach (var e in entries)
        {
            var row = table.NewRow();
            row["Timestamp"] = _opt.Formatting.UseUtcTimestamp ? e.Timestamp.ToUniversalTime() : e.Timestamp;
            row["Level"] = e.Level.ToString();
            row["Category"] = e.Category;
            row["DeviceId"] = (object?)e.DeviceId ?? DBNull.Value;
            row["Message"] = e.Message;
            row["Exception"] = (object?)e.Exception?.ToString() ?? DBNull.Value;
            row["Scope"] = e.Scope is { Count: > 0 }
                ? JsonSerializer.Serialize(e.Scope.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString()))
                : DBNull.Value;
            table.Rows.Add(row);
        }

        return table;
    }

    public ValueTask DisposeAsync()
    {
        _concurrencyLimiter.Dispose();
        return ValueTask.CompletedTask;
    }
}
