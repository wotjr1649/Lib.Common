// File: Internal/Runtime.cs
#nullable enable
using Lib.DB.Abstractions;
using Lib.DB.Services;

namespace Lib.DB.Internal
{
    /// <summary>
    /// IRuntime 구현. DI에서 내보내는 런타임 파사드입니다.
    /// </summary>
    internal sealed class BowooRuntime : IRuntime
    {
        public IQueryExecutor Executor { get; }
        public QueryExecutorFacade Facade { get; }
        public IParameterBinder Binder { get; }
        public ISpParameterCache SpCache { get; }

        public BowooRuntime(IQueryExecutor executor, QueryExecutorFacade facade, IParameterBinder binder, ISpParameterCache spCache)
        {
            Executor = executor;
            Facade = facade;
            Binder = binder;
            SpCache = spCache;
        }
    }
}
