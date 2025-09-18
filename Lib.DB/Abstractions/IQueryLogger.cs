#nullable enable
using System.Data;

namespace Lib.DB.Abstractions;

/// <summary>DB 명령 로깅 계약: 샘플링, 문장 Truncate 등은 구현체에서 처리</summary>
public interface IQueryLogger
{
    void LogCommand(string commandText, CommandType type, TimeSpan elapsed, bool success, Exception? ex = null);
}
