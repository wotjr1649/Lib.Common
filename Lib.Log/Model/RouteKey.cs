namespace Lib.Log.Model;

public readonly record struct RouteKey(string CategoryGroup, string Category, string? DeviceId)
{
    public override string ToString() => $"{CategoryGroup}:{Category}:{DeviceId}";
}
