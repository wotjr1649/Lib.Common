namespace Lib.Log.Internal;

using Lib.Log.Model;
using Lib.Log.Option;
using System.Text;

internal static class TemplateRenderer
{
    /// <summary>
    /// 템플릿을 실제 파일 경로로 변환합니다.
    /// 지원 토큰: {Root}, {RootByLevel}, {Project}, {Category}, {DeviceId}, {DeviceId?},
    /// {yyyy},{MM},{dd},{HH},{mm},{ss}
    /// </summary>
    public static string RenderPath(
        string baseDir,
        string template,
        in RouteKey rk,
        DateTime ts,
        LogOptions options,
        string? rootName = null)
    {
        var root = string.IsNullOrWhiteSpace(rootName) ? options.Rooting.DefaultRoot : rootName;
        var year = ts.Year.ToString("D4");
        var month = ts.Month.ToString("D2");
        var day = ts.Day.ToString("D2");
        var hh = ts.Hour.ToString("D2");
        var mm = ts.Minute.ToString("D2");
        var ss = ts.Second.ToString("D2");

        string device = rk.DeviceId ?? "";
        string deviceOpt = string.IsNullOrEmpty(device) ? "" : device;

        // 기본 치환
        var path = template
            .Replace("{Root}", root)
            .Replace("{RootByLevel}", root) // 하위호환
            .Replace("{Category}", Sanitize(rk.Category))
            .Replace("{DeviceId}", Sanitize(device))
            .Replace("{DeviceId?}", Sanitize(deviceOpt))
            .Replace("{yyyy}", year)
            .Replace("{MM}", month)
            .Replace("{dd}", day)
            .Replace("{HH}", hh)
            .Replace("{mm}", mm)
            .Replace("{ss}", ss);

        // 세이프 가드(경로 모든 조각 sanitize)
        var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++) parts[i] = Sanitize(parts[i]);

        var safeRel = string.Join(System.IO.Path.DirectorySeparatorChar, parts);
        var full = System.IO.Path.Combine(baseDir ?? string.Empty, safeRel);
        return full;
    }

    public static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
            sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_');
        var s = sb.ToString();
        return s.Length > 128 ? s[..128] : s;
    }
}