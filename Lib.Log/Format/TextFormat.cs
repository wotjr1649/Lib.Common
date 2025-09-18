namespace Lib.Log.Format;

using Lib.Log.Model;
using Lib.Log.Option;
using Microsoft.Extensions.Logging;
using System.Text;

public sealed class TextFormat : IFormat
{
    private readonly LogOptions _opt;

    public TextFormat(LogOptions opt) => _opt = opt;

    public string FormatLine(LogEntry e)
    {
        var ts = _opt.Formatting.UseUtcTimestamp ? e.Timestamp.ToUniversalTime() : e.Timestamp;
        var tsStr = ts.ToString(_opt.Formatting.TimestampFormat);
        var msg = e.Message;
        if (_opt.MaskSecrets) msg = Internal.Masking.Apply(msg);
        if (msg.Length > _opt.Formatting.MaxMessageLength) msg = msg[.._opt.Formatting.MaxMessageLength] + "...(truncated)";

        var sb = new StringBuilder(256 + msg.Length);
        sb.Append(tsStr).Append(" | ").Append(LevelToChar(e.Level)).Append(" | ").Append(e.Category);
        if (!string.IsNullOrEmpty(e.DeviceId)) sb.Append(" | ").Append(e.DeviceId);
        sb.Append(" | ").Append(msg);

        if (e.Exception is not null)
        {
            sb.Append(" | EX: ").Append(e.Exception.GetType().Name).Append(": ").Append(e.Exception.Message);
        }

        return sb.ToString();
    }

    private static char LevelToChar(LogLevel level) => level switch
    {
        LogLevel.Trace => 'T',
        LogLevel.Debug => 'D',
        LogLevel.Information => 'I',
        LogLevel.Warning => 'W',
        LogLevel.Error => 'E',
        LogLevel.Critical => 'C',
        _ => 'I'
    };
}