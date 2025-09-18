#nullable enable
namespace Lib.DB.Abstractions;

/// <summary>
/// TVP(Table-Valued Parameter) 매핑 특성.
/// - TypeName에는 dbo.MyTvpType처럼 정규화된 TVP 타입명을 지정합니다.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class TvpAttribute : Attribute
{
    public TvpAttribute(string typeName) => TypeName = typeName;
    public string TypeName { get; }
}
