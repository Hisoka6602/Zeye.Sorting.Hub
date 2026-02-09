using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories {

    /// <summary>
    /// 带内存缓存失效能力的仓储基类
    /// </summary>
    public abstract class MemoryCacheRepositoryBase<TEntity, TContext> : RepositoryBase<TEntity, TContext>
        where TEntity : class
        where TContext : DbContext {

        protected MemoryCacheRepositoryBase(
            IDbContextFactory<TContext> contextFactory,
            IMemoryCache memoryCache,
            ILogger logger)
            : base(contextFactory, logger) {
            MemoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        protected IMemoryCache MemoryCache { get; }

        /// <summary>
        /// 获取受影响的缓存 Key 列表（由派生类实现）
        /// </summary>
        protected abstract IEnumerable<string> GetRelatedCacheKeys(TEntity entity);

        public override async Task<RepositoryResult> AddAsync(TEntity entity, CancellationToken cancellationToken) {
            var result = await base.AddAsync(entity, cancellationToken);
            if (result.IsSuccess) {
                InvalidateCache(entity);
            }

            return result;
        }

        public override async Task<RepositoryResult> UpdateAsync(TEntity entity, CancellationToken cancellationToken) {
            var result = await base.UpdateAsync(entity, cancellationToken);
            if (result.IsSuccess) {
                InvalidateCache(entity);
            }

            return result;
        }

        public override async Task<RepositoryResult> RemoveAsync(TEntity entity, CancellationToken cancellationToken) {
            var result = await base.RemoveAsync(entity, cancellationToken);
            if (result.IsSuccess) {
                InvalidateCache(entity);
            }

            return result;
        }

        private void InvalidateCache(TEntity entity) {
            try {
                foreach (var key in GetRelatedCacheKeys(entity)) {
                    if (!string.IsNullOrWhiteSpace(key)) {
                        MemoryCache.Remove(key);
                    }
                }
            }
            catch (Exception ex) {
                Logger.LogWarning(ex, "缓存失效执行失败，实体类型={EntityType}", typeof(TEntity).Name);
            }
        }
    }
}
