#nullable enable
using Lib.DB.Abstractions;
using Microsoft.Data.SqlClient;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace Lib.DB.Services;

/// <summary>
/// 고성능 파라미터 바인더:
/// - DTO/익명객체/Dictionary/이미 SqlParameter[] 지원
/// - 리플렉션 Expression 캐시로 런타임 오버헤드 최소화
/// - DateOnly/TimeOnly, TVP, OUT 파라미터, 길이/정밀도/스케일 처리
/// </summary>
public sealed class ParameterBinder : IParameterBinder
{
    private static readonly ConcurrentDictionary<Type, Func<object, IEnumerable<SqlParameter>>> _factoryCache = new();

    public IEnumerable<SqlParameter> BindEnumerable(object? args)
    {
        if (args is null)
            yield break;

        // 이미 SqlParameter 목록이면 그대로 패스
        if (args is IEnumerable<SqlParameter> ready)
        {
            foreach (var p in ready) yield return p;
            yield break;
        }

        // Dictionary<string, object?> 지원
        if (args is IDictionary dict)
        {
            foreach (DictionaryEntry e in dict)
                yield return CreateParameterFromPair(e.Key?.ToString() ?? string.Empty, e.Value);
            yield break;
        }

        // DTO/익명객체
        var type = args.GetType();
        var factory = _factoryCache.GetOrAdd(type, BuildFactory);
        foreach (var p in factory(args))
            yield return p;
    }

    public SqlParameter[] Bind(object? args) => BindEnumerable(args).ToArray();

    public SqlParameter Create(string name, object? value) => CreateParameterFromPair(name, value);

    // -------- 내부 구현 --------

    private static Func<object, IEnumerable<SqlParameter>> BuildFactory(Type type)
    {
        var members = type
            .GetMembers(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m is PropertyInfo { CanRead: true } or FieldInfo)
            .ToArray();

        var obj = Expression.Parameter(typeof(object), "o");
        var cast = Expression.Convert(obj, type);

        var listVar = Expression.Variable(typeof(List<SqlParameter>), "list");
        var blockVars = new List<ParameterExpression> { listVar };
        var blockExprs = new List<Expression>
        {
            Expression.Assign(listVar, Expression.New(typeof(List<SqlParameter>)))
        };

        foreach (var m in members)
        {
            Type valueType;
            Expression valueExpr;

            DbParamAttribute? dbAttr;
            TvpAttribute? tvpAttr;

            if (m is PropertyInfo pi)
            {
                valueType = pi.PropertyType;
                valueExpr = Expression.Property(cast, pi);
                dbAttr = pi.GetCustomAttribute<DbParamAttribute>();
                tvpAttr = pi.GetCustomAttribute<TvpAttribute>();
            }
            else
            {
                var fi = (FieldInfo)m;
                valueType = fi.FieldType;
                valueExpr = Expression.Field(cast, fi);
                dbAttr = fi.GetCustomAttribute<DbParamAttribute>();
                tvpAttr = fi.GetCustomAttribute<TvpAttribute>();
            }

            var paramName = dbAttr?.Name ?? ("@" + m.Name);
            var nameConst = Expression.Constant(paramName);
            var valObj = Expression.Convert(valueExpr, typeof(object));
            var dbAttrConst = Expression.Constant(dbAttr, typeof(DbParamAttribute));
            var tvpAttrConst = Expression.Constant(tvpAttr, typeof(TvpAttribute));
            var typeConst = Expression.Constant(valueType, typeof(Type));

            var call = Expression.Call(
                typeof(ParameterBinder).GetMethod(nameof(BuildParameter), BindingFlags.NonPublic | BindingFlags.Static)!,
                nameConst, valObj, dbAttrConst, tvpAttrConst, typeConst);

            blockExprs.Add(Expression.Call(listVar, typeof(List<SqlParameter>).GetMethod("Add")!, call));
        }

        blockExprs.Add(listVar);
        var body = Expression.Block(blockVars, blockExprs);
        return Expression.Lambda<Func<object, IEnumerable<SqlParameter>>>(body, obj).Compile();
    }

    private static SqlParameter CreateParameterFromPair(string name, object? value)
        => BuildParameter(name, value, null, null);

    private static SqlParameter BuildParameter(
    string name, object? value, DbParamAttribute? a, TvpAttribute? tvp)
    {
        if (string.IsNullOrWhiteSpace(name))
            name = "@p";

        // TVP 우선
        if (tvp is not null)
        {
            var tvpParam = new SqlParameter(name, SqlDbType.Structured) { TypeName = tvp.TypeName };
            tvpParam.Value = value ?? DBNull.Value;
            return tvpParam;
        }

        var p = new SqlParameter
        {
            ParameterName = name,
            Direction = a?.Direction ?? ParameterDirection.Input
        };

        // ===== a(특성) 처리 - null 안전 =====
        if (a is not null)
        {
            // DbType/TypeName 힌트
            if (a.DbType is DbType dbt)
            {
                p.DbType = dbt;
            }
            else if (!string.IsNullOrWhiteSpace(a.TypeName))
            {
                // UDT/TVP 타입명 힌트
                p.TypeName = a.TypeName;
            }

            // 크기/정밀도/스케일/Nullability
            if (a.Size is int size) p.Size = size;
            if (a.Precision is byte prec) p.Precision = prec;
            if (a.Scale is byte scale) p.Scale = scale;
            if (a.IsNullable is bool isNullable) p.IsNullable = isNullable;
        }

        // 특수 타입 변환
        if (value is DateOnly d)
        {
            p.SqlDbType = SqlDbType.Date;
            p.Value = d.ToDateTime(TimeOnly.MinValue);
        }
        else if (value is TimeOnly t)
        {
            p.SqlDbType = SqlDbType.Time;
            p.Value = t.ToTimeSpan();
        }
        else
        {
            p.Value = value ?? DBNull.Value;
        }

        // 문자열 길이 자동 추정 (DbType/SqlDbType 혼용 없이 안전)
        if (p.Size == 0 && value is string s && s.Length > 0)
        {
            int maxBySqlDbType = p.SqlDbType switch
            {
                SqlDbType.VarChar or SqlDbType.Char => 8000, // ANSI
                SqlDbType.NVarChar or SqlDbType.NChar => 4000, // Unicode
                _ => 0
            };

            int maxByDbType = a?.DbType switch
            {
                DbType.AnsiString or DbType.AnsiStringFixedLength => 8000,
                DbType.String or DbType.StringFixedLength or null => 4000, // 힌트 없으면 Unicode 가정
                _ => 0
            };

            int max = maxBySqlDbType != 0 ? maxBySqlDbType : maxByDbType;
            if (max > 0)
                p.Size = Math.Min(s.Length, max);
        }

        return p;
    }

}
