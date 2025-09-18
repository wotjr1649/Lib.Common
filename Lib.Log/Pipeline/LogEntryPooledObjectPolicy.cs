namespace Lib.Log.Pipeline;

using Lib.Log.Model;
using Microsoft.Extensions.ObjectPool;

/// <summary>
/// LogEntry 객체 풀의 정책을 정의하는 클래스
/// </summary>
public sealed class LogEntryPooledObjectPolicy : IPooledObjectPolicy<LogEntry>
{
    public LogEntry Create() => new();

    public bool Return(LogEntry obj)
    {
        // 객체를 풀에 반환하기 전에 초기화
        obj.Reset();
        return true;
    }
}
