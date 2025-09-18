namespace Lib.Log.Sink;

using Lib.Log.Model;

/// <summary>
/// 모든 로그 출력 대상(Sink)이 구현해야 하는 범용 인터페이스.
/// 파일, 데이터베이스, 클라우드 서비스 등 다양한 출력으로 확장 가능합니다.
/// </summary>
public interface ISink : IAsyncDisposable
{
    // Sink의 고유 이름을 정의합니다. (예: "File", "Db")
    string Name { get; }

    /// <summary>
    /// 로그 항목 배치를 비동기적으로 기록합니다.
    /// </summary>
    Task WriteBatchAsync(IReadOnlyList<LogEntry> entries, CancellationToken ct);
}