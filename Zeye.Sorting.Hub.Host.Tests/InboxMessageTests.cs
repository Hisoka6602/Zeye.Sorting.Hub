using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Application.Services.Events;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Enums.Events;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Inbox 消息幂等消费测试。
/// </summary>
public sealed class InboxMessageTests {
    /// <summary>
    /// 验证场景：首次消费应创建记录并返回成功结果。
    /// </summary>
    [Fact]
    public async Task InboxMessageGuardService_WhenFirstConsumptionSucceeds_ShouldPersistSucceededRecord() {
        var databaseName = $"inbox-first-consume-{Guid.NewGuid():N}";
        try {
            var options = BuildOptions(databaseName);
            var factory = new SortingHubTestDbContextFactory(options);
            var repository = new InboxMessageRepository(factory);
            var service = new InboxMessageGuardService(repository);
            var expiresAt = LocalTimeTestConstraint.CreateLocalTime(2026, 6, 1, 8, 0, 0);

            var result = await service.ExecuteAsync(
                "WCS",
                "MSG-1001",
                "ParcelCreated",
                expiresAt,
                3,
                static _ => Task.FromResult("consumed"),
                static _ => Task.FromResult<string?>(null),
                CancellationToken.None);

            Assert.False(result.IsReplay);
            Assert.Equal("consumed", result.Response);

            await using var dbContext = new SortingHubDbContext(options);
            var record = await dbContext.Set<InboxMessage>().SingleAsync();
            Assert.Equal("WCS", record.SourceSystem);
            Assert.Equal("MSG-1001", record.MessageId);
            Assert.Equal(InboxMessageStatus.Succeeded, record.Status);
            Assert.Equal(expiresAt, record.ExpiresAt);
            Assert.NotNull(record.ProcessedAt);
            LocalTimeTestConstraint.AssertIsLocalTime(record.CreatedAt);
            LocalTimeTestConstraint.AssertIsLocalTime(record.UpdatedAt);
            LocalTimeTestConstraint.AssertIsLocalTime(record.ExpiresAt);
            LocalTimeTestConstraint.AssertIsLocalTime(record.ProcessedAt!.Value);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：已成功消费的消息再次进入时应回放已有结果。
    /// </summary>
    [Fact]
    public async Task InboxMessageGuardService_WhenSucceededMessageRepeated_ShouldReplayExistingResponse() {
        var databaseName = $"inbox-replay-{Guid.NewGuid():N}";
        try {
            var options = BuildOptions(databaseName);
            var factory = new SortingHubTestDbContextFactory(options);
            var repository = new InboxMessageRepository(factory);
            var service = new InboxMessageGuardService(repository);

            var firstResult = await service.ExecuteAsync(
                "WCS",
                "MSG-1002",
                "ParcelCreated",
                DateTime.Now.AddDays(7),
                3,
                static _ => Task.FromResult("first-consume"),
                static _ => Task.FromResult<string?>(null),
                CancellationToken.None);

            var replayResult = await service.ExecuteAsync(
                "WCS",
                "MSG-1002",
                "ParcelCreated",
                DateTime.Now.AddDays(7),
                3,
                static _ => Task.FromResult("should-not-run"),
                static _ => Task.FromResult<string?>("first-consume"),
                CancellationToken.None);

            Assert.False(firstResult.IsReplay);
            Assert.True(replayResult.IsReplay);
            Assert.Equal("first-consume", replayResult.Response);

            await using var dbContext = new SortingHubDbContext(options);
            Assert.Equal(1, await dbContext.Set<InboxMessage>().CountAsync());
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：仍在处理中的消息应明确拒绝重复消费。
    /// </summary>
    [Fact]
    public async Task InboxMessageGuardService_WhenMessageIsProcessing_ShouldRejectRepeatedConsumption() {
        var databaseName = $"inbox-processing-{Guid.NewGuid():N}";
        try {
            var options = BuildOptions(databaseName);
            await using (var dbContext = new SortingHubDbContext(options)) {
                var record = InboxMessage.CreatePending("ERP", "MSG-1003", "ParcelUpdated", DateTime.Now.AddDays(5));
                record.MarkProcessing();
                await dbContext.Set<InboxMessage>().AddAsync(record);
                await dbContext.SaveChangesAsync();
            }

            var factory = new SortingHubTestDbContextFactory(options);
            var repository = new InboxMessageRepository(factory);
            var service = new InboxMessageGuardService(repository);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
                "ERP",
                "MSG-1003",
                "ParcelUpdated",
                DateTime.Now.AddDays(5),
                3,
                static _ => Task.FromResult("should-not-run"),
                static _ => Task.FromResult<string?>(null),
                CancellationToken.None));

            Assert.Equal(InboxMessageGuardService.MessageInProgressMessage, exception.Message);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：失败记录在重试上限内应允许重新消费并累计重试次数。
    /// </summary>
    [Fact]
    public async Task InboxMessageGuardService_WhenFailedMessageCanRetry_ShouldReconsumeSuccessfully() {
        var databaseName = $"inbox-retry-{Guid.NewGuid():N}";
        try {
            var options = BuildOptions(databaseName);
            await using (var dbContext = new SortingHubDbContext(options)) {
                var failedRecord = InboxMessage.CreatePending("MES", "MSG-1004", "ParcelRetryRequested", DateTime.Now.AddDays(5));
                failedRecord.MarkProcessing();
                failedRecord.MarkFailed("首次消费失败。");
                await dbContext.Set<InboxMessage>().AddAsync(failedRecord);
                await dbContext.SaveChangesAsync();
            }

            var factory = new SortingHubTestDbContextFactory(options);
            var repository = new InboxMessageRepository(factory);
            var service = new InboxMessageGuardService(repository);

            var result = await service.ExecuteAsync(
                "MES",
                "MSG-1004",
                "ParcelRetryRequested",
                DateTime.Now.AddDays(5),
                3,
                static _ => Task.FromResult("retry-success"),
                static _ => Task.FromResult<string?>(null),
                CancellationToken.None);

            Assert.False(result.IsReplay);
            Assert.Equal("retry-success", result.Response);

            await using var verificationContext = new SortingHubDbContext(options);
            var record = await verificationContext.Set<InboxMessage>().SingleAsync();
            Assert.Equal(InboxMessageStatus.Succeeded, record.Status);
            Assert.Equal(1, record.RetryCount);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：已到过期治理时间的记录应能被清理规划查询到。
    /// </summary>
    [Fact]
    public async Task InboxMessageRepository_WhenRecordExpired_ShouldReturnCleanupCandidate() {
        var databaseName = $"inbox-cleanup-{Guid.NewGuid():N}";
        try {
            var options = BuildOptions(databaseName);
            var expiredAt = LocalTimeTestConstraint.CreateLocalTime(2026, 5, 1, 8, 0, 0);
            var provisionalExpiresAt = DateTime.Now.AddDays(2);
            var activeExpiresAt = LocalTimeTestConstraint.CreateLocalTime(2026, 7, 1, 8, 0, 0);
            await using (var dbContext = new SortingHubDbContext(options)) {
                var expiredRecord = InboxMessage.CreatePending("WCS", "MSG-1005", "ParcelArchived", provisionalExpiresAt);
                expiredRecord.MarkProcessing();
                expiredRecord.MarkSucceeded();
                var activeRecord = InboxMessage.CreatePending("WCS", "MSG-1006", "ParcelArchived", activeExpiresAt);
                await dbContext.Set<InboxMessage>().AddRangeAsync(expiredRecord, activeRecord);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new SortingHubDbContext(options)) {
                var persistedRecord = await dbContext.Set<InboxMessage>().SingleAsync(x => x.MessageId == "MSG-1005");
                dbContext.Entry(persistedRecord).Property(x => x.ExpiresAt).CurrentValue = expiredAt;
                await dbContext.SaveChangesAsync();
            }

            var factory = new SortingHubTestDbContextFactory(options);
            var repository = new InboxMessageRepository(factory);
            var candidates = await repository.GetCleanupCandidatesAsync(
                LocalTimeTestConstraint.CreateLocalTime(2026, 5, 2, 0, 0, 0),
                10,
                CancellationToken.None);

            Assert.Single(candidates);
            Assert.Equal("MSG-1005", candidates[0].MessageId);
            LocalTimeTestConstraint.AssertIsLocalTime(candidates[0].ExpiresAt);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 构建 InMemory DbContext 选项。
    /// </summary>
    /// <param name="databaseName">数据库名。</param>
    /// <returns>上下文选项。</returns>
    private static DbContextOptions<SortingHubDbContext> BuildOptions(string databaseName) {
        return new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    /// <summary>
    /// 删除测试数据库。
    /// </summary>
    /// <param name="databaseName">数据库名。</param>
    /// <returns>异步任务。</returns>
    private static async Task CleanupDatabaseAsync(string databaseName) {
        var options = BuildOptions(databaseName);
        await using var dbContext = new SortingHubDbContext(options);
        await dbContext.Database.EnsureDeletedAsync();
    }
}
