namespace Lib.Log.Format;

using Lib.Log.Model;
using Lib.Log.Option;
using System.Text.Json;

public sealed class JsonFormat : IFormat
{
    private readonly LogOptions _opt;
    private readonly JsonWriterOptions _writerOptions = new() { Indented = false, SkipValidation = false };

    public JsonFormat(LogOptions opt) => _opt = opt;

    public string FormatLine(LogEntry e)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer, _writerOptions))
        {
            writer.WriteStartObject();
            var ts = _opt.Formatting.UseUtcTimestamp ? e.Timestamp.ToUniversalTime() : e.Timestamp;
            writer.WriteString("ts", ts);
            writer.WriteString("level", e.Level.ToString());
            writer.WriteString("category", e.Category);
            if (e.DeviceId is not null) writer.WriteString("device", e.DeviceId);
            var msg = _opt.MaskSecrets ? Log.Internal.Masking.Apply(e.Message) : e.Message;
            if (msg.Length > _opt.Formatting.MaxMessageLength) msg = msg[.._opt.Formatting.MaxMessageLength] + "...(truncated)";
            writer.WriteString("message", msg);
            if (e.Exception is not null)
            {
                writer.WriteStartObject("exception");
                writer.WriteString("type", e.Exception.GetType().Name);
                writer.WriteString("message", e.Exception.Message);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}