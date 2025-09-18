#nullable enable
using Lib.DB.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;

namespace Lib.DB.Diagnostics;

/// <summary>
/// 기본 로거: Microsoft.Extensions.Logging에 위임.
/// 샘플링/Truncate/성공/실패 정책을 적용합니다.
/// </summary>
public sealed class DefaultQueryLogger : IQueryLogger
{
    private readonly ILogger<DefaultQueryLogger> _logger;
    private readonly IOptionsMonitor<DiagnosticsOptions> _opt;
    private readonly Random _rnd = new();

    public DefaultQueryLogger(ILogger<DefaultQueryLogger> logger, IOptionsMonitor<DiagnosticsOptions> opt)
    {
        _logger = logger;
        _opt = opt;
    }

    public void LogCommand(string commandText, CommandType type, TimeSpan elapsed, bool success, Exception? ex = null)
    {
        var o = _opt.CurrentValue;
        if (!success && o.LogOnFailure == false) return;
        if (success && o.LogOnSuccess == false) return;

        if (_rnd.NextDouble() > o.SampleRate) return;

        var text = commandText ?? string.Empty;
        if (text.Length > o.CommandTextMaxLength)
            text = text.Substring(0, o.CommandTextMaxLength) + "...";

        if (success)
        {
            _logger.LogInformation("DB[{Type}] {Elapsed}ms OK :: {Text}",
                type, (int)elapsed.TotalMilliseconds, text);
        }
        else
        {
            _logger.LogError(ex, "DB[{Type}] {Elapsed}ms FAIL :: {Text}",
                type, (int)elapsed.TotalMilliseconds, text);
        }
    }
}
