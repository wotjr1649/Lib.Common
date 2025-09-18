#nullable enable
namespace Lib.DB.Abstractions;

/// <summary>DB 메트릭 싱크(카운터/타이머/태그). OpenTelemetry/Prometheus 등으로 교체 가능.</summary>
public interface IQueryMetricsSink
{
    void Increment(string name, double value = 1, IReadOnlyDictionary<string, string>? tags = null);
    void Observe(string name, double value, IReadOnlyDictionary<string, string>? tags = null);
}
