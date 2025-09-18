namespace Lib.Log.Internal;

using System.Text.RegularExpressions;

internal static partial class Masking
{
    [GeneratedRegex(@"(?i)(password|pwd|token|apikey|api_key)\s*[=:]\s*([^\s;,&]+)", RegexOptions.Compiled)]
    private static partial Regex SensitiveKeyValueRegex();

    internal static string Apply(string text)
        => string.IsNullOrEmpty(text) ? text : SensitiveKeyValueRegex().Replace(text, m => $"{m.Groups[1].Value}=****");
}
