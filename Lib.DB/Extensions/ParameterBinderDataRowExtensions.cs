#nullable enable
using System;
using System.Data;
using Lib.DB.Abstractions;
using Microsoft.Data.SqlClient;

namespace Lib.DB.Extensions;

/// <summary>
/// DataRow → SqlParameter 바인딩 헬퍼.
/// - 레거시 AssignParameterValues(DataRow) 호환을 위한 확장입니다.
/// - 열 이름 == 파라미터 이름 매칭 규칙을 사용합니다(@ 제외).
/// </summary>
public static class ParameterBinderDataRowExtensions
{
    /// <summary>
    /// DataRow를 기반으로 SqlCommand.Parameters를 채웁니다. 기존 동일 이름 파라미터는 덮어씁니다.
    /// </summary>
    public static void Bind(this IParameterBinder binder, SqlCommand command, DataRow row)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));
        if (row is null) throw new ArgumentNullException(nameof(row));

        var cols = row.Table?.Columns?.Cast<DataColumn>().ToArray() ?? Array.Empty<DataColumn>();
        foreach (var col in cols)
        {
            var name = col.ColumnName;
            var value = row[name];
            if (value == DBNull.Value) value = null;

            var p = command.Parameters.Contains("@" + name) ? command.Parameters["@" + name] : command.Parameters.Add("@" + name, MapDbType(col.DataType));
            p.Value = value ?? DBNull.Value;
        }
    }

    private static SqlDbType MapDbType(Type t)
    {
        // 단순 맵핑. 필요 시 세분화 가능
        if (t == typeof(string)) return SqlDbType.NVarChar;
        if (t == typeof(int)) return SqlDbType.Int;
        if (t == typeof(long)) return SqlDbType.BigInt;
        if (t == typeof(short)) return SqlDbType.SmallInt;
        if (t == typeof(byte)) return SqlDbType.TinyInt;
        if (t == typeof(bool)) return SqlDbType.Bit;
        if (t == typeof(DateTime)) return SqlDbType.DateTime2;
        if (t == typeof(decimal)) return SqlDbType.Decimal;
        if (t == typeof(double)) return SqlDbType.Float;
        if (t == typeof(float)) return SqlDbType.Real;
        if (t == typeof(Guid)) return SqlDbType.UniqueIdentifier;
        if (t == typeof(byte[])) return SqlDbType.VarBinary;
        return SqlDbType.Variant;
    }
}
