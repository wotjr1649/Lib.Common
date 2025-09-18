#nullable enable
namespace Lib.DB.Options;

/// <summary>Polly v8 파이프라인 옵션.</summary>
public sealed class ResilienceOptions
{
    // Retry
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    public bool UseJitter { get; set; } = true;
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(3);

    // Timeout per attempt
    public TimeSpan TimeoutPerAttempt { get; set; } = TimeSpan.FromSeconds(5);

    // CircuitBreaker (옵션)
    public bool CircuitBreakerEnabled { get; set; } = false;
    public double FailureRatio { get; set; } = 0.5;
    public int MinimumThroughput { get; set; } = 20;
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(10);

    public bool UseCircuitBreaker { get; set; } = false;
    public TimeSpan CircuitBreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
