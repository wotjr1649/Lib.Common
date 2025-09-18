#nullable enable

namespace Lib.DB.Internal;

/// <summary>
/// DTO/레코드 속성에 부착하여 파라미터 메타정보를 지정합니다.
/// 예: [DbParam(Direction=Output, Size=50)]
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DbParamAttribute : Attribute
{
    public System.Data.ParameterDirection Direction { get; set; } = System.Data.ParameterDirection.Input;
    public int Size { get; set; } = 0;
    public byte Precision { get; set; } = 0;
    public byte Scale { get; set; } = 0;
    public string? TypeName { get; set; } // TVP/datetime2 등
    public bool IsReturnValue { get; set; } = false;
}

/// <summary>
/// TVP 지정(간편): [Tvp("dbo.MyTableType")]
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class TvpAttribute : Attribute
{
    public TvpAttribute(string typeName) => TypeName = typeName;
    public string TypeName { get; }
}
