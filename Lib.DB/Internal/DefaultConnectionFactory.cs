#nullable enable
using Lib.DB.Abstractions;
using Microsoft.Data.SqlClient;

namespace Lib.DB.Internal;

/// <summary>기본 SqlConnection 팩토리.</summary>
public sealed class DefaultConnectionFactory : IConnectionFactory
{
    public SqlConnection Create(string connectionString) => new(connectionString);
}
