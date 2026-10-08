using Microsoft.EntityFrameworkCore.Storage;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>耗时大范围只读查询不启用内部整批重试缓冲；失败由请求重试，不保留半成品。</summary>
internal sealed class StreamingReadExecutionStrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory {
    /// <summary>保持真正的流式读取，避免重试策略提前缓存全部宽记录。</summary>
    public IExecutionStrategy Create() => new NonRetryingExecutionStrategy(dependencies);
}
