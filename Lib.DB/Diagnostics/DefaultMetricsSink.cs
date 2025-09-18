#nullable enable
using Lib.DB.Abstractions;
using Microsoft.Extensions.Logging;

namespace Lib.DB.Diagnostics;

/// <summary>
/// 기본 메트릭 싱크(예: 단순 로그). 실제 환경에서는 OpenTelemetry/Prometheus 등으로 교체하세요.
/// </summary>
public sealed class DefaultMetricsSink : IQueryMetricsSink
{
    private readonly ILogger<DefaultMetricsSink> _logger;
    public DefaultMetricsSink(ILogger<DefaultMetricsSink> logger) => _logger = logger;

    public void Increment(string name, double value = 1, IReadOnlyDictionary<string, string>? tags = null)
        => _logger.LogDebug("METRIC+ {Name} {Value} {Tags}", name, value, tags);

    public void Observe(string name, double value, IReadOnlyDictionary<string, string>? tags = null)
        => _logger.LogDebug("METRIC~ {Name} {Value} {Tags}", name, value, tags);
}
