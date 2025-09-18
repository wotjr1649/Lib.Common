#nullable enable
using Lib.DB.Abstractions;
using Lib.DB.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using System.Net.Sockets;

namespace Lib.DB.Internal;

internal sealed class DefaultResiliencePolicyFactory : IResiliencePolicyFactory
{
    public ResiliencePipeline Create(ResilienceOptions options)
    {
        var builder = new ResiliencePipelineBuilder();

        // 1) Timeout per attempt
        if (options.TimeoutPerAttempt > TimeSpan.Zero)
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.TimeoutPerAttempt
            });
        }

        // 2) Retry with exponential backoff + optional jitter
        if (options.MaxRetryAttempts > 0)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = options.BaseDelay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(200) : options.BaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = options.UseJitter,
                MaxDelay = options.MaxDelay > TimeSpan.Zero ? options.MaxDelay : TimeSpan.FromSeconds(3),
                ShouldHandle = new PredicateBuilder().Handle<SqlException>().Handle<TimeoutRejectedException>()
            });
        }

        // 3) Circuit Breaker (옵션)
        if (options.UseCircuitBreaker)
        {
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                // 기본값 예시 (필요 시 Options에 상세 항목 추가 권장)
                FailureRatio = 0.5,              // 샘플 윈도우에서 50% 이상 실패 시
                MinimumThroughput = 10,          // 최소 요청 수
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = options.CircuitBreakDuration > TimeSpan.Zero ? options.CircuitBreakDuration : TimeSpan.FromSeconds(30)
            });
        }

        return builder.Build();
    }
}