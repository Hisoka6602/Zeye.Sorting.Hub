using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories {

    /// <summary>
    /// EF Core 仓储基类（Infrastructure 层实现细节）
    /// 说明：
    /// 1) 使用 IDbContextFactory，降低 DbContext 生命周期耦合
    /// 2) 不在仓储内隐式开启事务，事务边界建议由 Application 统一控制
    /// 3) 通过 Result 返回错误信息，隔离异常，不影响调用链
    /// </summary>
    public abstract class RepositoryBase<TEntity, TContext>
        where TEntity : class
        where TContext : DbContext {

        protected RepositoryBase(
            IDbContextFactory<TContext> contextFactory,
            ILogger logger) {
            ContextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected IDbContextFactory<TContext> ContextFactory { get; }

        protected ILogger Logger { get; }

        /// <summary>
        /// 获取 IQueryable（仅用于查询路径）
        /// 注意：返回的 IQueryable 绑定 DbContext 生命周期，建议仅在方法内部使用并立即物化
        /// </summary>
        protected IQueryable<TEntity> Query(TContext db, bool asNoTracking = true) {
            var set = db.Set<TEntity>();
            return asNoTracking ? set.AsNoTracking() : set;
        }

        /// <summary>
        /// 新增（不调用 SaveChanges）
        /// </summary>
        public virtual async Task<RepositoryResult> AddAsync(TEntity entity, CancellationToken cancellationToken) {
            if (entity is null) {
                return RepositoryResult.Fail("实体不能为空");
            }

            try {
                await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
                await db.Set<TEntity>().AddAsync(entity, cancellationToken);
                return RepositoryResult.Success();
            }
            catch (OperationCanceledException) {
                return RepositoryResult.Fail("操作已取消");
            }
            catch (Exception ex) {
                Logger.LogError(ex, "新增实体失败，实体类型={EntityType}", typeof(TEntity).Name);
                return RepositoryResult.Fail("新增实体失败");
            }
        }

        /// <summary>
        /// 批量新增（不调用 SaveChanges）
        /// </summary>
        public virtual async Task<RepositoryResult> AddRangeAsync(
            IReadOnlyCollection<TEntity> entities,
            CancellationToken cancellationToken) {
            if (entities is null || entities.Count == 0) {
                return RepositoryResult.Fail("实体集合不能为空");
            }

            try {
                await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
                await db.Set<TEntity>().AddRangeAsync(entities, cancellationToken);
                return RepositoryResult.Success();
            }
            catch (OperationCanceledException) {
                return RepositoryResult.Fail("操作已取消");
            }
            catch (Exception ex) {
                Logger.LogError(ex, "批量新增失败，实体类型={EntityType}", typeof(TEntity).Name);
                return RepositoryResult.Fail("批量新增失败");
            }
        }

        /// <summary>
        /// 更新（不调用 SaveChanges）
        /// </summary>
        public virtual async Task<RepositoryResult> UpdateAsync(TEntity entity, CancellationToken cancellationToken) {
            if (entity is null) {
                return RepositoryResult.Fail("实体不能为空");
            }

            try {
                await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
                db.Set<TEntity>().Update(entity);
                return RepositoryResult.Success();
            }
            catch (OperationCanceledException) {
                return RepositoryResult.Fail("操作已取消");
            }
            catch (Exception ex) {
                Logger.LogError(ex, "更新实体失败，实体类型={EntityType}", typeof(TEntity).Name);
                return RepositoryResult.Fail("更新实体失败");
            }
        }

        /// <summary>
        /// 删除（不调用 SaveChanges）
        /// </summary>
        public virtual async Task<RepositoryResult> RemoveAsync(TEntity entity, CancellationToken cancellationToken) {
            if (entity is null) {
                return RepositoryResult.Fail("实体不能为空");
            }

            try {
                await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
                db.Set<TEntity>().Remove(entity);
                return RepositoryResult.Success();
            }
            catch (OperationCanceledException) {
                return RepositoryResult.Fail("操作已取消");
            }
            catch (Exception ex) {
                Logger.LogError(ex, "删除实体失败，实体类型={EntityType}", typeof(TEntity).Name);
                return RepositoryResult.Fail("删除实体失败");
            }
        }

        /// <summary>
        /// 按条件查询并物化
        /// </summary>
        public virtual async Task<RepositoryResult<List<TEntity>>> ToListAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken,
            bool asNoTracking = true) {
            if (predicate is null) {
                return RepositoryResult<List<TEntity>>.Fail("查询条件不能为空");
            }

            try {
                await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
                var list = await Query(db, asNoTracking)
                    .Where(predicate)
                    .ToListAsync(cancellationToken);

                return RepositoryResult<List<TEntity>>.Success(list);
            }
            catch (OperationCanceledException) {
                return RepositoryResult<List<TEntity>>.Fail("操作已取消");
            }
            catch (Exception ex) {
                Logger.LogError(ex, "查询失败，实体类型={EntityType}", typeof(TEntity).Name);
                return RepositoryResult<List<TEntity>>.Fail("查询失败");
            }
        }

        /// <summary>
        /// 查询单条（FirstOrDefault）
        /// </summary>
        public virtual async Task<RepositoryResult<TEntity?>> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken,
            bool asNoTracking = true) {
            if (predicate is null) {
                return RepositoryResult<TEntity?>.Fail("查询条件不能为空");
            }

            try {
                await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
                var entity = await Query(db, asNoTracking)
                    .FirstOrDefaultAsync(predicate, cancellationToken);

                return RepositoryResult<TEntity?>.Success(entity);
            }
            catch (OperationCanceledException) {
                return RepositoryResult<TEntity?>.Fail("操作已取消");
            }
            catch (Exception ex) {
                Logger.LogError(ex, "查询单条失败，实体类型={EntityType}", typeof(TEntity).Name);
                return RepositoryResult<TEntity?>.Fail("查询单条失败");
            }
        }
    }
}
