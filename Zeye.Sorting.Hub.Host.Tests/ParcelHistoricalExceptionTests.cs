using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.Json;
using ParcelExceptionRepair;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>完成状态与处理质量独立，验证异常保留、真实落格及全部报表的一致口径。</summary>
public sealed class ParcelHistoricalExceptionTests {
    /// <summary>目标实际一致且非兜底、非强排才属于正常落格；空值和无效编码不能作为成功证据。</summary>
    [Theory]
    [InlineData("01", "01", false, false, false)]
    [InlineData("0001", "1", false, false, false)]
    [InlineData("0001", "2", false, false, true)]
    [InlineData("A01", " a01 ", false, false, false)]
    [InlineData("01", "60", false, false, true)]
    [InlineData("60", "60", true, false, true)]
    [InlineData("0060", "60", true, false, true)]
    [InlineData("01", "01", false, true, true)]
    [InlineData(null, "01", false, false, true)]
    [InlineData("01", null, false, false, true)]
    [InlineData("0", "0", false, false, true)]
    public void ActualLandingDeterminesQualityWithoutUndoingCompletion(string? target, string? actual, bool fallback, bool blocked, bool abnormal) {
        var detected = Fact("detected") with { ParcelId = 1 };
        var landed = detected with { RecordId = "landed", Stage = ParcelProcessingStage.SortingCompleted,
            OccurredAt = detected.OccurredAt.AddSeconds(2), TargetChuteCode = target, ActualChuteCode = actual,
            IsFallback = fallback, IsRoutingBlocked = blocked };
        var parcel = Parcel.CreateDetected(1, detected, detected.RecordedAt);
        parcel.ApplyProcessingRecords([landed, detected]);
        Assert.Equal(ParcelStatus.Completed, parcel.Status);
        Assert.Equal(abnormal ? ParcelExceptionType.AbnormalDischarge : null, parcel.ExceptionType);
        Assert.Equal(landed.OccurredAt, parcel.CompletedTime);
        Assert.Equal(actual, parcel.ActualChuteCode);
        Assert.Equal(2000, parcel.LifecycleMilliseconds);
    }

    /// <summary>包含无信息的真实响应命中后，后续空响应、正常落格及迟到事实都不能抹除异常。</summary>
    [Fact]
    public void ResponseRuleSurvivesCompletionAndEmptyAttemptResponses() {
        var detected = Fact("detected") with { ParcelId = 1 };
        var response = detected with { RecordId = "response", Stage = ParcelProcessingStage.ScanUploaded,
            OccurredAt = detected.OccurredAt.AddMilliseconds(100), ResponseBody = "<return>#MSG::-1::无信息::||#END</return>" };
        var ended = response with { RecordId = "ended", OccurredAt = detected.OccurredAt.AddMilliseconds(200), ResponseBody = null };
        var landed = detected with { RecordId = "landed", Stage = ParcelProcessingStage.SortingCompleted,
            OccurredAt = detected.OccurredAt.AddSeconds(1), TargetChuteCode = "01", ActualChuteCode = "01" };
        var rule = new ClassificationRule { Id = 1, Status = "已发布", Scope = "全部产线", ExceptionType = 14,
            Conditions = [new() { Field = "Provider 响应内容", Operator = "包含", Value = "无信息" }] };
        var parcel = Parcel.CreateDetected(1, detected, detected.RecordedAt);
        parcel.ApplyProcessingRecords([landed, ended, detected, response], [rule]);
        Assert.Equal(ParcelStatus.Completed, parcel.Status);
        Assert.Equal(ParcelExceptionType.TargetChuteAssignmentRejected, parcel.ExceptionType);
    }

    /// <summary>有过超时、兜底后完成的包裹，在实际数据库中的概览、分析、工作台均只计一次异常。</summary>
    [Fact]
    public async Task CompletedExceptionsRemainVisibleInEveryReportAndDrilldown() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var detected = Fact("detected");
        var timeout = detected with { RecordId = "timeout", Stage = ParcelProcessingStage.ParcelException,
            OccurredAt = detected.OccurredAt.AddSeconds(1), ExceptionCode = "RoutingTimeout" };
        var landed = detected with { RecordId = "landed", Stage = ParcelProcessingStage.SortingCompleted,
            OccurredAt = detected.OccurredAt.AddSeconds(2), TargetChuteCode = "60", ActualChuteCode = "60", IsFallback = true };
        var results = await database.Processing.AppendBatchAsync([detected, landed, timeout], default);
        Assert.All(results, result => Assert.True(result.IsSuccess, result.ErrorMessage));
        var parcel = await database.Parcels.GetByIdAsync(results[0].Value!.ParcelId!.Value, default);
        Assert.Equal(ParcelStatus.Completed, parcel!.Status);
        Assert.Equal(ParcelExceptionType.WaitTargetChuteTimeout, parcel.ExceptionType);
        var budget = new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions()));
        var overview = await new ParcelAnalyticsReadService(database.Factory, budget, database.Partitions)
            .GetAsync(detected.RecordedAt.Date, detected.RecordedAt.Date, default);
        Assert.Equal(1, overview.CompletedCount);
        Assert.Equal(1, overview.ExceptionCount);
        Assert.Equal(1, Assert.Single(overview.ExceptionTypes).Count);
        var analysis = await new ParcelAnalysisReadService(database.Factory, budget, database.Partitions)
            .ReadAsync(new ParcelAnalysisRequest { View = "exceptions", FromDate = detected.RecordedAt.Date,
                ToDate = detected.RecordedAt.Date, ExceptionType = 3 }, default);
        Assert.Equal(1, analysis.CompletedCount);
        Assert.Equal(1, analysis.ExceptionCount);
        Assert.Equal(0, analysis.PendingCount);
        Assert.Equal("等待目标格口超时", Assert.Single(analysis.Items).ExceptionName);
        Assert.Equal((int)ParcelStatus.Completed, analysis.Items[0].Status);
        var workbench = await new ParcelWorkbenchReadService(database.Factory, database.Partitions)
            .GetAsync(detected.RecordedAt.AddHours(1), default);
        Assert.Equal(1, Assert.Single(workbench.Workstations).CompletedCount);
        Assert.Equal(1, workbench.Workstations[0].ExceptionCount);
    }

    /// <summary>实际落格后到达的异常事实不倒退生命周期，但仍计入质量统计。</summary>
    [Fact]
    public void LateTimeoutPreservesCompletionAndAddsException() {
        var detected = Fact("detected") with { ParcelId = 1 };
        var landed = detected with { RecordId = "landed", Stage = ParcelProcessingStage.SortingCompleted,
            OccurredAt = detected.OccurredAt.AddSeconds(1), TargetChuteCode = "01", ActualChuteCode = "01" };
        var timeout = detected with { RecordId = "timeout", Stage = ParcelProcessingStage.ParcelException,
            OccurredAt = detected.OccurredAt.AddSeconds(2), ExceptionCode = "RoutingTimeout" };
        var parcel = Parcel.CreateDetected(1, detected, detected.RecordedAt);
        parcel.ApplyProcessingRecords([detected, landed, timeout]);
        Assert.Equal(ParcelStatus.Completed, parcel.Status);
        Assert.Equal(landed.OccurredAt, parcel.CompletedTime);
        Assert.Equal(ParcelExceptionType.WaitTargetChuteTimeout, parcel.ExceptionType);
    }

    /// <summary>旧版本完成时清空的异常可预览和恢复；修复可重入，回滚只接受尚未变化的修复结果。</summary>
    [Fact]
    public async Task HistoricalRepairIsDryRunByDefaultIdempotentAndConditionallyReversible() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var detected = Fact("detected");
        var timeout = detected with { RecordId = "timeout", Stage = ParcelProcessingStage.ParcelException,
            OccurredAt = detected.OccurredAt.AddSeconds(1), ExceptionCode = "RoutingTimeout" };
        var landed = detected with { RecordId = "landed", Stage = ParcelProcessingStage.SortingCompleted,
            OccurredAt = detected.OccurredAt.AddSeconds(2), TargetChuteCode = "60", ActualChuteCode = "60", IsFallback = true };
        var saved = await database.Processing.AppendBatchAsync([detected, timeout, landed], default);
        Assert.All(saved, item => Assert.True(item.IsSuccess, item.ErrorMessage));
        var result = saved[0].Value!;
        await using var db = await database.Partitions.CreateContextAsync(result.PartitionSuffix, default);
        var legacy = await db.Set<Parcel>().AsTracking().SingleAsync(row => row.Id == result.ParcelId);
        db.Entry(legacy).Property(row => row.ExceptionType).CurrentValue = null;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var path = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        var runner = new ExceptionRepairRunner(database.Partitions, path, ClassificationRuleDefaults.Create());
        using var stream = new MemoryStream();
        await using var audit = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        Assert.Equal(1, await runner.RepairAsync(detected.RecordedAt.Date, detected.RecordedAt.Date.AddDays(1), false, audit, default));
        Assert.Null((await db.Set<Parcel>().AsNoTracking().SingleAsync()).ExceptionType);
        var entry = JsonSerializer.Deserialize<ExceptionRepairEntry>(Encoding.UTF8.GetString(stream.ToArray()).Trim())!;
        Assert.Equal(1, await runner.RepairAsync(detected.RecordedAt.Date, detected.RecordedAt.Date.AddDays(1), true, audit, default));
        Assert.Equal(0, await runner.RepairAsync(detected.RecordedAt.Date, detected.RecordedAt.Date.AddDays(1), true, audit, default));
        var repaired = await db.Set<Parcel>().AsNoTracking().SingleAsync();
        Assert.Equal(ParcelStatus.Completed, repaired.Status);
        Assert.Equal(landed.OccurredAt, repaired.CompletedTime);
        Assert.Equal(ParcelExceptionType.WaitTargetChuteTimeout, repaired.ExceptionType);
        Assert.Equal(3, await db.Set<ParcelProcessingRecord>().CountAsync());
        await runner.UpdateAsync(entry, true, default);
        Assert.Null((await db.Set<Parcel>().AsNoTracking().SingleAsync()).ExceptionType);
        await runner.UpdateAsync(entry, false, default);
        var reclassified = await db.Set<Parcel>().AsTracking().SingleAsync();
        reclassified.MarkSortingException(ParcelExceptionType.MechanicalFailure);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.UpdateAsync(entry, true, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.UpdateAsync(entry with { DatabasePath = path + ".other" }, true, default));
    }

    /// <summary>真实命令行读取部署 LiteDB 中的数据库及规则，验证预览、执行开关与审计回滚。</summary>
    [Fact]
    public async Task RepairCliUsesAuthoritativeConfigurationAndRequiresExplicitWriteFlags() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var detected = Fact("detected");
        var response = detected with { RecordId = "response", Stage = ParcelProcessingStage.ScanUploaded,
            OccurredAt = detected.OccurredAt.AddSeconds(1), ResponseBody = "<return>无信息</return>" };
        var landed = detected with { RecordId = "landed", Stage = ParcelProcessingStage.SortingCompleted,
            OccurredAt = detected.OccurredAt.AddSeconds(2), TargetChuteCode = "0001", ActualChuteCode = "1" };
        var saved = await database.Processing.AppendBatchAsync([detected, response, landed], default);
        Assert.All(saved, item => Assert.True(item.IsSuccess, item.ErrorMessage));
        await using var db = await database.Partitions.CreateContextAsync(saved[0].Value!.PartitionSuffix, default);
        Assert.Null((await db.Set<Parcel>().AsNoTracking().SingleAsync()).ExceptionType);
        var root = Directory.CreateTempSubdirectory("hub-exception-cli-").FullName;
        try {
            var configuration = new { Persistence = new { Provider = "SQLite" },
                ConnectionStrings = new { SQLite = db.Database.GetConnectionString() } };
            await File.WriteAllTextAsync(Path.Combine(root, "appsettings.json"), JsonSerializer.Serialize(new {
                ConfigurationStorage = new { LiteDbPath = "configuration.db" },
                ConnectionStrings = new { SQLite = "Data Source=must-not-create.db" }
            }));
            using (var lite = new LiteDB.LiteDatabase(Path.Combine(root, "configuration.db"))) {
                lite.GetCollection<LiteDB.BsonDocument>("runtime_configuration").Insert(new LiteDB.BsonDocument {
                    ["_id"] = "current", ["json"] = JsonSerializer.Serialize(configuration)
                });
                lite.GetCollection<LiteDB.BsonDocument>("configuration_documents").Insert(new LiteDB.BsonDocument {
                    ["_id"] = "rules-exception", ["json"] = JsonSerializer.Serialize(new[] { new ClassificationRule {
                        Id = 1, Status = "已发布", Scope = "全部产线", ExceptionType = 14,
                        Conditions = [new() { Field = "Provider 响应内容", Operator = "包含", Value = "无信息" }]
                    } })
                });
            }
            var preview = Path.Combine(root, "preview");
            Assert.Equal(0, await RunRepairCliAsync(root, "--from", "2026-10-16", "--to", "2026-10-16", "--output", preview));
            Assert.Single(File.ReadAllLines(Path.Combine(preview, "preview.jsonl")));
            Assert.Null((await db.Set<Parcel>().AsNoTracking().SingleAsync()).ExceptionType);
            Assert.False(File.Exists(Path.Combine(root, "must-not-create.db")));
            Assert.Equal(1, await RunRepairCliAsync(root, "--apply", "--output", Path.Combine(root, "denied")));
            Assert.Null((await db.Set<Parcel>().AsNoTracking().SingleAsync()).ExceptionType);
            var applied = Path.Combine(root, "applied");
            Assert.Equal(0, await RunRepairCliAsync(root, "--from", "2026-10-16", "--to", "2026-10-16", "--apply", "--confirm-stopped", "--output", applied));
            var parcel = await db.Set<Parcel>().AsNoTracking().SingleAsync();
            Assert.Equal(ParcelStatus.Completed, parcel.Status);
            Assert.Equal(ParcelExceptionType.TargetChuteAssignmentRejected, parcel.ExceptionType);
            Assert.Equal(0, await RunRepairCliAsync(root, "--rollback", Path.Combine(applied, "applied.jsonl"), "--confirm-stopped", "--output", Path.Combine(root, "rollback")));
            Assert.Null((await db.Set<Parcel>().AsNoTracking().SingleAsync()).ExceptionType);
        } finally { Directory.Delete(root, true); }
    }

    /// <summary>独立进程隔离命令行日志和环境，最多等待三十秒，避免测试遗留后台进程。</summary>
    private static async Task<int> RunRepairCliAsync(string root, params string[] arguments) {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(typeof(ExceptionRepairRunner).Assembly.Location);
        start.ArgumentList.Add("--config-root"); start.ArgumentList.Add(root);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment.Remove("ZEYE_HUB_CONFIG_ROOT");
        start.Environment.Remove("ConfigurationStorage__LiteDbPath");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) process.Kill(true); }
        await Task.WhenAll(output, error);
        var denied = arguments.Contains("--apply") && !arguments.Contains("--confirm-stopped");
        Assert.True(process.ExitCode == (denied ? 1 : 0), await error + await output);
        if (denied) Assert.Contains("--confirm-stopped", await error);
        return process.ExitCode;
    }

    /// <summary>构造真实来源身份与固定本地时间。</summary>
    private static ParcelProcessingRecord Fact(string record) => new() {
        RecordId = record, SourceInstanceId = "historical-exceptions", SourceRunId = "run", SourceParcelId = 42,
        WorkstationName = "测试工作台", Stage = ParcelProcessingStage.Detected, IsSuccess = true,
        OccurredAt = new(2026, 10, 16, 20, 10, 0), RecordedAt = new(2026, 10, 16, 20, 10, 0),
        PartitionTime = new(2026, 10, 16, 20, 10, 0), PayloadHash = record
    };
}
