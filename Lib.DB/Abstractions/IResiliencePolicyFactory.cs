#nullable enable
using Lib.DB.Options;
using Polly;

namespace Lib.DB.Abstractions;

/// <summary>
/// 명령 유형별 Resilience 파이프라인(Polly v8)을 제공합니다.
/// 필요 시 Query/NonQuery/Scalar/Xml 등으로 분기하여 상이한 정책을 적용할 수 있습니다.
/// </summary>
/// <summary>
/// Polly v8 파이프라인 팩토리.
/// </summary>
public interface IResiliencePolicyFactory
{
    /// <summary>
    /// ResilienceOptions를 기반으로 ResiliencePipeline을 생성합니다.
    /// </summary>
    ResiliencePipeline Create(ResilienceOptions options);
}