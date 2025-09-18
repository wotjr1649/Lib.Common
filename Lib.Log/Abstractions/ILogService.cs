
using Microsoft.Extensions.Logging;

namespace Lib.Log.Abstractions;

/// <summary>
/// 도메인 친화적 로깅 파사드. 보통은 ILogger/ILogger만으로 충분하지만
/// 장비 ID 등 도메인 파라미터를 직접 넘기고 싶을 때 사용.
/// </summary>
public interface ILogService
{
    void Trace(string category, string message, string? deviceId = null);
    void Debug(string category, string message, string? deviceId = null);
    void Info(string category, string message, string? deviceId = null);
    void Warn(string category, string message, string? deviceId = null);
    void Error(string category, string message, string? deviceId = null, Exception? ex = null);
    void Critical(string category, string message, string? deviceId = null, Exception? ex = null);


    // DB Sink에만 로그를 보내는 편의 메서드들
    void InfoDb(string category, string message, string? deviceId = null);
    void WarnDb(string category, string message, string? deviceId = null);
    void ErrorDb(string category, string message, string? deviceId = null, Exception? ex = null);

    // File Sink에만 로그를 보내는 편의 메서드들
    void InfoFile(string category, string message, string? deviceId = null);
    void WarnFile(string category, string message, string? deviceId = null);
    void ErrorFile(string category, string message, string? deviceId = null, Exception? ex = null);


    ILogger CreateLogger(string category);
}
