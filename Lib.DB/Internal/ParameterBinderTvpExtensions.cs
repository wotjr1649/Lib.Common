#nullable enable
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;
using System.Data;


namespace Lib.DB.Internal
{
    /// <summary>TVP 바인딩 헬퍼(데이터테이블/SqlDataRecord 양쪽 지원).</summary>
    public static class ParameterBinderTvpExtensions
    {
        public static SqlParameter AsTvp(this DataTable table, string paramName, string typeName)
        {
            var p = new SqlParameter(paramName.StartsWith("@") ? paramName : "@" + paramName, table)
            {
                SqlDbType = SqlDbType.Structured,
                TypeName = typeName
            };
            return p;
        }

        public static SqlParameter AsTvp(this IEnumerable<SqlDataRecord> records, string paramName, string typeName)
        {
            var p = new SqlParameter(paramName.StartsWith("@") ? paramName : "@" + paramName, records)
            {
                SqlDbType = SqlDbType.Structured,
                TypeName = typeName
            };
            return p;
        }
    }
}
