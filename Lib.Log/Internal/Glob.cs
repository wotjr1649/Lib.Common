namespace Lib.Log.Internal;

using System.Text.RegularExpressions;

internal static class Glob
{
    // 간단 글롭: * 만 지원, 대소문자 무시
    internal static bool IsMatch(string text, string pattern)
        => Regex.IsMatch(text, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase);
}
