namespace Lib.Log.Routing;

using Lib.Log.Internal;
using Lib.Log.Model;
using Lib.Log.Option;
using Microsoft.Extensions.Options;

public interface ILogRouter
{
    (string Group, RouteKey RouteKey) Resolve(LogEntry entry);
}

public sealed class LogRouter : ILogRouter
{
    private readonly IOptionsMonitor<LogOptions> _options;

    public LogRouter(IOptionsMonitor<LogOptions> options) => _options = options;

    public (string Group, RouteKey RouteKey) Resolve(LogEntry entry)
    {
        var opt = _options.CurrentValue;

        // 카테고리 → 그룹 매핑
        foreach (var kv in opt.Routing.CategoryGroups)
        {
            foreach (var pattern in kv.Value)
            {
                if (Glob.IsMatch(entry.Category, pattern))
                {
                    var rk = new RouteKey(kv.Key, entry.Category, entry.DeviceId);
                    return (kv.Key, rk);
                }
            }
        }
        return ("Default", new RouteKey("Default", entry.Category, entry.DeviceId));
    }
}
