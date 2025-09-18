using Lib.DB.Services;

namespace Lib.DB.Abstractions;

/// <summary>
/// Bowoo.Lib의 핵심 진입점(파사드). DI로 한 번만 받아서 내부 서비스들에 접근합니다.
/// </summary>
public interface IRuntime
{
    /// <summary>쿼리 실행기(저수준). 파라미터가 이미 준비되어 있을 때 사용.</summary>
    IQueryExecutor Executor { get; }

    /// <summary>바인딩 내장 파사드(권장). object/DTO/Dictionary → SqlParameter[] 자동 변환.</summary>
    QueryExecutorFacade Facade { get; }

    /// <summary>파라미터 바인더(고급 제어용).</summary>
    IParameterBinder Binder { get; }

    /// <summary>Stored Procedure 파라미터 메타데이터 캐시.</summary>
    ISpParameterCache SpCache { get; }
}