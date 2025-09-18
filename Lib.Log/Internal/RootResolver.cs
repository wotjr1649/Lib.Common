namespace Lib.Log.Internal;

using Lib.Log.Model;
using Lib.Log.Option;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

internal static class RootResolver
{
    internal static string ResolveRoot(in LogEntry e, LogOptions opt)
    {
        // 0) 스코프 강제 오버라이드
        if (opt.Rooting.AllowScopeOverride && TryGetScopeValue(e.Scope, "Root", out var forced) && !string.IsNullOrWhiteSpace(forced))
            return Sanitize(forced);

        // 1) 규칙 순차 평가 (첫 매칭 우선)
        foreach (var r in opt.Rooting.Rules)
        {
            if (!MatchLevel(e.Level, r)) continue;
            if (!MatchCategory(e.Category, r)) continue;
            if (!MatchDevice(e.DeviceId, r)) continue;
            if (!MatchScope(e.Scope, r)) continue;
            return Sanitize(r.Root);
        }

        // 2) 기본 루트
        return Sanitize(opt.Rooting.DefaultRoot);
    }

    private static bool MatchLevel(LogLevel lvl, LogOptions.RootRule r)
        => (r.MinLevel.HasValue && lvl < r.MinLevel.Value) || (r.MaxLevel.HasValue && lvl > r.MaxLevel.Value) ? false : true;

    private static bool MatchCategory(string category, LogOptions.RootRule r)
    {
        if (r.Categories is { Length: > 0 })
        {
            foreach (var pat in r.Categories)
                if (Glob.IsMatch(category, pat)) return true;
            return false;
        }

        return r.CategoryStartsWith is { Length: > 0 } ? r.CategoryStartsWith.Any(p => category.StartsWith(p, StringComparison.OrdinalIgnoreCase)) : true;
    }

    private static bool MatchDevice(string? deviceId, LogOptions.RootRule r)
    {
        if (r.DeviceIdPatterns is { Length: > 0 })
        {
            if (string.IsNullOrEmpty(deviceId)) return false;
            foreach (var pat in r.DeviceIdPatterns)
                if (Glob.IsMatch(deviceId, pat)) return true;
            return false;
        }
        return true;
    }

    private static bool MatchScope(IReadOnlyList<KeyValuePair<string, object?>>? scope, LogOptions.RootRule r)
    {
        if (r.ScopeEquals is null || r.ScopeEquals.Count == 0) return true;
        if (scope is null) return false;

        foreach (var kv in r.ScopeEquals)
        {
            if (!TryGetScopeValue(scope, kv.Key, out var val)) return false;
            if (!string.Equals(val, kv.Value, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static bool TryGetScopeValue(IReadOnlyList<KeyValuePair<string, object?>>? scope, string key, out string value)
    {
        value = "";
        if (scope is null) return false;

        // 대소문자 무시 키 검색
        var kv = scope.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
        if (kv.Key is null) return false;

        value = kv.Value?.ToString() ?? "";
        return true;
    }

    private static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "logs";
        var chars = input.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_').ToArray();
        var s = new string(chars);
        return s.Length > 64 ? s[..64] : s;
    }
}
