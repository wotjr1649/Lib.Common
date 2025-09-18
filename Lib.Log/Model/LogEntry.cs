namespace Lib.Log.Model;

using Microsoft.Extensions.Logging;
using System.Collections.Generic;

public sealed class LogEntry
{
    // init -> set 으로 변경하여 재사용 가능하도록 수정
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public string? DeviceId { get; set; }
    public IReadOnlyList<KeyValuePair<string, object?>>? State { get; set; }
    public IReadOnlyList<KeyValuePair<string, object?>>? Scope { get; set; }

    /// <summary>
    /// 객체 풀에 반환될 때 호출될 상태 초기화 메서드
    /// </summary>
    public void Reset()
    {
        Timestamp = default;
        Level = default;
        Category = string.Empty;
        Message = string.Empty;
        Exception = null;
        DeviceId = null;
        State = null;
        Scope = null;
    }
}