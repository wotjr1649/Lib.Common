namespace Lib.Log.Hosting;

using Lib.Log.Option;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

internal sealed partial class DatabaseInitializer(IOptions<LogOptions> options, ILogger<DatabaseInitializer> logger) : IHostedService
{
    private readonly LogOptions _options = options.Value;
    private readonly ILogger<DatabaseInitializer> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Database.Enabled || !_options.Database.AutoCreateTable)
        {
            return;
        }

        try
        {
            await using var connection = new SqlConnection(_options.Database.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CreateLogsTableIfNotExistsAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database table initialization failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CreateLogsTableIfNotExistsAsync(SqlConnection connection, CancellationToken ct)
    {
        var tableName = _options.Database.TableName;

        if (string.IsNullOrWhiteSpace(tableName) || !IsValidSqlObjectName(tableName))
        {
            _logger.LogError("Invalid table name '{TableName}' specified in configuration. Aborting table creation.", tableName);
            return;
        }

        _logger.LogInformation("Checking if '{TableName}' table exists...", tableName);

        var query = $@"
        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{tableName}]') AND type in (N'U'))
        BEGIN
            CREATE TABLE [dbo].[{tableName}](
                [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [Timestamp] DATETIME2(7) NOT NULL,
                [Level] NVARCHAR(20) NOT NULL,
                [Category] NVARCHAR(256) NOT NULL,
                [DeviceId] NVARCHAR(128) NULL,
                [Message] NVARCHAR(MAX) NOT NULL,
                [Exception] NVARCHAR(MAX) NULL,
                [Scope] NVARCHAR(MAX) NULL
            );
        END;

        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_{tableName}_Timestamp_Level' AND object_id = OBJECT_ID('dbo.{tableName}'))
        BEGIN
            CREATE NONCLUSTERED INDEX [IX_{tableName}_Timestamp_Level] ON [dbo].[{tableName}]
            (
                [Timestamp] DESC,
                [Level] ASC
            )
            INCLUDE([Category], [DeviceId]);
        END;";

        await using var command = new SqlCommand(query, connection);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Successfully ensured '{TableName}' table exists.", tableName);
    }

    [GeneratedRegex("^[a-zA-Z0-9_]+$")]
    private static partial Regex SqlObjectNameRegex();

    private static bool IsValidSqlObjectName(string name) => SqlObjectNameRegex().IsMatch(name);
}
