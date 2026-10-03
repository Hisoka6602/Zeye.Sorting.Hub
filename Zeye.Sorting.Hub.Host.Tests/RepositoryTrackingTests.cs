using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;
using Zeye.Sorting.Hub.Domain.Aggregates.Idempotency;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Enums.Events;
using Zeye.Sorting.Hub.Domain.Enums.DataGovernance;
using Zeye.Sorting.Hub.Domain.Enums.Idempotency;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>在生产默认无跟踪配置下验证后台任务领取、状态更新及幂等完成真正落库。</summary>
public sealed class RepositoryTrackingTests {
    /// <summary>遗留执行可限次恢复，旧执行器在重新领取前后都不能覆盖恢复结果。</summary>
    [Fact]
    public async Task AbandonedArchiveRecoveryFencesOldExecutorsAndLimitsRetries() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var repository = new ArchiveTaskRepository(database.Factory);
        var task = ArchiveTask.CreateDryRun(ArchiveTaskType.WebRequestAuditLogHistory, 90, "恢复验收", "中断恢复");
        Assert.True((await repository.AddAsync(task, default)).IsSuccess);
        Assert.NotNull(await repository.TryAcquireNextPendingAsync(default));
        await AgeArchiveAttemptAsync(database, task.Id);
        var oldOwner = (await repository.GetByIdAsync(task.Id, default))!;
        Assert.Equal(1, await repository.RecoverAbandonedAsync(DateTime.Now.AddHours(-1), 1, 1, default));
        Assert.Equal(0, await repository.RecoverAbandonedAsync(DateTime.Now.AddHours(-1), 1, 1, default));
        oldOwner.MarkCompleted(100, "旧执行器结果", "{}");
        Assert.False((await repository.UpdateAsync(oldOwner, default)).IsSuccess);
        var newOwner = await repository.TryAcquireNextPendingAsync(default);
        Assert.NotNull(newOwner);
        Assert.Equal(1, newOwner.RetryCount);
        Assert.False((await repository.UpdateAsync(oldOwner, default)).IsSuccess);
        newOwner.MarkCompleted(5, "当前执行器结果", "{}");
        Assert.True((await repository.UpdateAsync(newOwner, default)).IsSuccess);
        Assert.Equal(5, (await repository.GetByIdAsync(task.Id, default))!.PlannedItemCount);
        var completed = (await repository.GetByIdAsync(task.Id, default))!;
        completed.Requeue(); Assert.True((await repository.UpdateAsync(completed, default)).IsSuccess);
        Assert.NotNull(await repository.TryAcquireNextPendingAsync(default));
        await AgeArchiveAttemptAsync(database, task.Id);
        Assert.Equal(1, await repository.RecoverAbandonedAsync(DateTime.Now.AddHours(-1), 1, 1, default));
        Assert.Equal(ArchiveTaskStatus.Failed, (await repository.GetByIdAsync(task.Id, default))!.Status);
        Assert.Null(await repository.TryAcquireNextPendingAsync(default));
    }

    /// <summary>遗留恢复每轮受批次限制，不接管仍在有效执行窗口内的任务。</summary>
    [Fact]
    public async Task ArchiveRecoveryIsBoundedAndDoesNotStealFreshAttempts() {
        await using var database = new RelationalParcelTestDatabase(); await database.InitializeAsync();
        var repository = new ArchiveTaskRepository(database.Factory);
        for (var index = 0; index < 3; index++) {
            var task = ArchiveTask.CreateDryRun(ArchiveTaskType.WebRequestAuditLogHistory, 90, "验收", "批次边界");
            Assert.True((await repository.AddAsync(task, default)).IsSuccess);
            var owner = await repository.TryAcquireNextPendingAsync(default); Assert.NotNull(owner);
            if (index < 2) await AgeArchiveAttemptAsync(database, task.Id);
        }
        Assert.Equal(1, await repository.RecoverAbandonedAsync(DateTime.Now.AddHours(-1), 3, 1, default));
        Assert.Equal(1, await repository.RecoverAbandonedAsync(DateTime.Now.AddHours(-1), 3, 1, default));
        Assert.Equal(0, await repository.RecoverAbandonedAsync(DateTime.Now.AddHours(-1), 3, 1, default));
        await using var context = await database.Factory.CreateDbContextAsync();
        Assert.Equal(1, await context.Set<ArchiveTask>().CountAsync(task => task.Status == ArchiveTaskStatus.Running));
        Assert.Equal(2, await context.Set<ArchiveTask>().CountAsync(task => task.Status == ArchiveTaskStatus.Pending));
    }

    /// <summary>生产默认无跟踪配置下，收件领取必须提交 Processing，不能被再次领取。</summary>
    [Fact]
    public async Task InboxAcquisitionPersistsUnderNoTrackingDefault() {
        await using var database = new RelationalParcelTestDatabase(); await database.InitializeAsync();
        var repository = new InboxMessageRepository(database.Factory);
        var message = InboxMessage.CreatePending("fusion-test", "one", "parcel");
        Assert.True((await repository.AddAsync(message, default)).IsSuccess);
        Assert.NotNull(await repository.TryAcquireForConsumptionAsync("fusion-test", "one", 3, default));
        Assert.Null(await repository.TryAcquireForConsumptionAsync("fusion-test", "one", 3, default));
        await using var context = await database.Factory.CreateDbContextAsync();
        Assert.Equal(InboxMessageStatus.Processing, (await context.Set<InboxMessage>().SingleAsync()).Status);
    }

    /// <summary>只在本测试数据库中模拟中断超过两小时的领取检查点。</summary>
    private static async Task AgeArchiveAttemptAsync(RelationalParcelTestDatabase database, long taskId) {
        await using var context = await database.Factory.CreateDbContextAsync();
        var task = await context.Set<ArchiveTask>().AsTracking().SingleAsync(task => task.Id == taskId);
        context.Entry(task).Property(task => task.LastAttemptedAt).CurrentValue = DateTime.Now.AddHours(-2);
        await context.SaveChangesAsync();
    }

    /// <summary>归档领取只能发生一次，完成和重新排队必须在独立读取中可见。</summary>
    [Fact]
    public async Task ArchiveAcquisitionAndCompletionPersistWithNoTrackingDefault() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var repository = new ArchiveTaskRepository(db.Factory);
        var task = ArchiveTask.CreateDryRun(ArchiveTaskType.WebRequestAuditLogHistory, 90, "验收", "真实领取");
        Assert.True((await repository.AddAsync(task, default)).IsSuccess);
        var acquired = await repository.TryAcquireNextPendingAsync(default);
        Assert.NotNull(acquired);
        Assert.Equal(ArchiveTaskStatus.Running, (await repository.GetByIdAsync(task.Id, default))!.Status);
        Assert.Null(await repository.TryAcquireNextPendingAsync(default));
        acquired.MarkCompleted(5, "完成预演计划", "{}");
        Assert.True((await repository.UpdateAsync(acquired, default)).IsSuccess);
        var completed = await repository.GetByIdAsync(task.Id, default);
        Assert.Equal(ArchiveTaskStatus.Completed, completed!.Status); Assert.Equal(5, completed.PlannedItemCount);
        completed.Requeue(); Assert.True((await repository.UpdateAsync(completed, default)).IsSuccess);
        Assert.Equal(ArchiveTaskStatus.Pending, (await repository.GetByIdAsync(task.Id, default))!.Status);
    }

    /// <summary>完成幂等记录的状态和时间在新上下文中保留。</summary>
    [Fact]
    public async Task IdempotencyCompletionPersistsWithNoTrackingDefault() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var repository = new IdempotencyRepository(db.Factory);
        var record = IdempotencyRecord.CreatePending("qa", "write", "key", new string('a', 64));
        Assert.True((await repository.AddAsync(record, default)).IsSuccess);
        record.MarkCompleted(); Assert.True((await repository.UpdateAsync(record, default)).IsSuccess);
        await using var context = await db.Factory.CreateDbContextAsync();
        var saved = await context.Set<IdempotencyRecord>().AsNoTracking().SingleAsync(x => x.Id == record.Id);
        Assert.Equal(IdempotencyRecordStatus.Completed, saved.Status); Assert.NotNull(saved.CompletedAt);
    }
}
