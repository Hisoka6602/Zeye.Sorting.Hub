using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using NLog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

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

        /// <summary>
        /// 重写新增操作，并在成功后失效相关缓存键。
        /// </summary>
        public override async Task<RepositoryResult> AddAsync(TEntity entity, CancellationToken cancellationToken) {
            var result = await base.AddAsync(entity, cancellationToken);
            if (result.IsSuccess) {
                InvalidateCache(entity);
            }

            return result;
        }

        /// <summary>
        /// 重写更新操作，并在成功后失效相关缓存键。
        /// </summary>
        public override async Task<RepositoryResult> UpdateAsync(TEntity entity, CancellationToken cancellationToken) {
            var result = await base.UpdateAsync(entity, cancellationToken);
            if (result.IsSuccess) {
                InvalidateCache(entity);
            }

            return result;
        }

        /// <summary>
        /// 重写删除操作，并在成功后失效相关缓存键。
        /// </summary>
        public override async Task<RepositoryResult> RemoveAsync(TEntity entity, CancellationToken cancellationToken) {
            var result = await base.RemoveAsync(entity, cancellationToken);
            if (result.IsSuccess) {
                InvalidateCache(entity);
            }

            return result;
        }

        /// <summary>
        /// 根据实体关联缓存键执行批量失效。
        /// </summary>
        private void InvalidateCache(TEntity entity) {
            try {
                foreach (var key in GetRelatedCacheKeys(entity)) {
                    if (!string.IsNullOrWhiteSpace(key)) {
                        MemoryCache.Remove(key);
                    }
                }
            }
            catch (Exception ex) {
                Logger.Warn(ex, "缓存失效执行失败，实体类型={EntityType}", typeof(TEntity).Name);
            }
        }
    }
}
