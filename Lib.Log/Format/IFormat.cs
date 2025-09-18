namespace Lib.Log.Format;

using Lib.Log.Model;

public interface IFormat
{
    string FormatLine(LogEntry entry);
}
