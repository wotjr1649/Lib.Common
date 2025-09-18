#nullable enable
using System.Data;

namespace Lib.DB.Utils;

/// <summary>UI-독립 바인딩 헬퍼.</summary>
public static class DataBindingExtensions
{
    public static DataTable? FirstTable(this DataSet? ds) => (ds?.Tables.Count ?? 0) > 0 ? ds!.Tables[0] : null;

    public static DataRow? FirstRow(this DataTable? dt) => (dt?.Rows.Count ?? 0) > 0 ? dt!.Rows[0] : null;

    public static bool HasColumn(this DataRow row, string name) => row.Table.Columns.Contains(name);
}
