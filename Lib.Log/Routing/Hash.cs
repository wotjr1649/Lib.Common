namespace Lib.Log.Routing;

using Lib.Log.Model;
using System.Text;

internal static class Hash
{
    public static int HashToShard(in RouteKey key, int shards)
    {
        var s = key.ToString();
        var bytes = Encoding.UTF8.GetBytes(s);
        uint hash = 2166136261;
        foreach (var b in bytes) hash = (hash ^ b) * 16777619; // FNV-1a
        return (int)(hash % (uint)shards);
    }
}
