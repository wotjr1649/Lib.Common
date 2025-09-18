// File: Extensions/QueryExecutorFacadeDefaultExtensions.cs
#nullable enable
using System.Data;
using System.Xml;
using Microsoft.Extensions.Options;
using Lib.DB.Options;

namespace Lib.DB.Services
{
    /// <summary>
    /// LibOptions.DefaultConnectionString를 사용하는 간편 호출 확장.
    /// </summary>
    public static class QueryExecutorFacadeDefaultExtensions
    {
        public static Task<int> ExecuteNonQueryAsync(this QueryExecutorFacade facade, IOptionsMonitor<LibOptions> opt,
            string commandText, CommandType commandType = CommandType.StoredProcedure, object? args = null,
            bool useReadUncommitted = false, CancellationToken ct = default)
            => facade.ExecuteNonQueryAsync(opt.CurrentValue.DefaultConnectionString ?? throw new InvalidOperationException("DefaultConnectionString is not set."),
                                           commandText, commandType, args, useReadUncommitted, ct);

        public static Task<T?> ExecuteScalarAsync<T>(this QueryExecutorFacade facade, IOptionsMonitor<LibOptions> opt,
            string commandText, CommandType commandType = CommandType.StoredProcedure, object? args = null,
            bool useReadUncommitted = false, CancellationToken ct = default)
            => facade.ExecuteScalarAsync<T>(opt.CurrentValue.DefaultConnectionString ?? throw new InvalidOperationException("DefaultConnectionString is not set."),
                                            commandText, commandType, args, useReadUncommitted, ct);

        public static Task ExecuteReaderAsync(this QueryExecutorFacade facade, IOptionsMonitor<LibOptions> opt,
            string commandText, Func<Microsoft.Data.SqlClient.SqlDataReader, Task> handle,
            CommandType commandType = CommandType.StoredProcedure, object? args = null,
            bool useReadUncommitted = false, CancellationToken ct = default)
            => facade.ExecuteReaderAsync(opt.CurrentValue.DefaultConnectionString ?? throw new InvalidOperationException("DefaultConnectionString is not set."),
                                         commandText, handle, commandType, args, useReadUncommitted, ct);

        public static Task<DataSet> ExecuteDataSetAsync(this QueryExecutorFacade facade, IOptionsMonitor<LibOptions> opt,
            string commandText, CommandType commandType = CommandType.StoredProcedure, object? args = null,
            bool useReadUncommitted = false, CancellationToken ct = default)
            => facade.ExecuteDataSetAsync(opt.CurrentValue.DefaultConnectionString ?? throw new InvalidOperationException("DefaultConnectionString is not set."),
                                          commandText, commandType, args, useReadUncommitted, ct);

        public static Task<XmlReader> ExecuteXmlReaderAsync(this QueryExecutorFacade facade, IOptionsMonitor<LibOptions> opt,
            string commandText, CommandType commandType = CommandType.StoredProcedure, object? args = null,
            bool useReadUncommitted = false, CancellationToken ct = default)
            => facade.ExecuteXmlReaderAsync(opt.CurrentValue.DefaultConnectionString ?? throw new InvalidOperationException("DefaultConnectionString is not set."),
                                            commandText, commandType, args, useReadUncommitted, ct);
    }
}
