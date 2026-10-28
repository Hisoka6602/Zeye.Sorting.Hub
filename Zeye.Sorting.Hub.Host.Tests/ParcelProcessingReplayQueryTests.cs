using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>完整重放使用窄SELECT，规则保留完整输入，入库审计及详情读取仍保留全部原文。</summary>
public sealed class ParcelProcessingReplayQueryTests {
    /// <summary>四库都只选择重放需要的列，ScanUploaded规则响应保持完整且不加载原始请求及设备报文。</summary>
    [Theory]
    [InlineData("MySql")]
    [InlineData("SqlServer")]
    [InlineData("Oracle")]
    [InlineData("SQLite")]
    public void ReplayProjectionTranslatesWithoutUnusedMessageColumns(string provider) {
        var options = new DbContextOptionsBuilder<SortingHubDbContext>().EnableServiceProviderCaching(false);
        if (provider == "MySql") options.UseMySql("Server=127.0.0.1;Database=design_time_only;User Id=design_time_only", new MySqlServerVersion(new Version(8, 4, 0)));
        else if (provider == "SqlServer") options.UseSqlServer("Server=127.0.0.1;Database=design_time_only;Integrated Security=True;TrustServerCertificate=True");
        else AdditionalDbContextOptions.Configure(options, provider, provider == "Oracle"
            ? "User Id=design_time_only;Password=design_time_only;Data Source=127.0.0.1:1521/FREEPDB1"
            : "Data Source=replay-query-design-time-only.db");
        using var db = new SortingHubDbContext(options.Options);
        AssertReplayColumns(ParcelProcessingReplayQuery.BuildQuery(db, 17).ToQueryString());
    }

    /// <summary>两个写入口重放旧事实时仍使用响应尾部、多条码及Provider规则，完整审计原文仍可从详情读取。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WritePathsReplayNarrowHistoryButKeepFullRulesAndAudit(bool batch) {
        var capture = new UnboundQueryCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase(queryInterceptor: capture);
        await database.InitializeAsync();
        var detected = Fact("detected", ParcelProcessingStage.Detected);
        var response = new string('r', 20000) + "尾部业务拒绝";
        var barcodes = JsonSerializer.Serialize(new[] { new string('b', 9000), "SECONDARY" });
        var raw = new string('p', 16000);
        var headers = new string('h', 16000);
        var body = new string('q', 16000);
        var scanned = Fact("scanned", ParcelProcessingStage.ScanUploaded, 1) with {
            IsSuccess = true, Barcode = "PRIMARY", BarcodesJson = barcodes,
            Provider = "完整响应供应商", ResponseBody = response, ResponseStatusCode = 200,
            RawPayload = raw, RequestHeaders = headers, RequestBody = body,
            RequestAt = detected.OccurredAt, ResponseAt = detected.OccurredAt.AddSeconds(1), ElapsedMilliseconds = 1000
        };
        var initial = await database.Processing.AppendBatchAsync([detected, scanned], default);
        Assert.All(initial, result => Assert.True(result.IsSuccess, result.ErrorMessage));
        var id = initial[0].Value!.ParcelId!.Value;
        var rule = new ClassificationRule {
            Id = 1, Name = "完整响应规则", Status = "已发布", ExceptionType = (int)ParcelExceptionType.MechanicalFailure,
            Conditions = [
                new() { Field = "Provider 名称", Operator = "等于", Value = "完整响应供应商" },
                new() { Field = "Provider 响应内容", Operator = "包含", Value = "尾部业务拒绝" },
                new() { Field = "响应状态码", Operator = "等于", Value = "200" },
                new() { Field = "条码", Operator = "等于", Value = "SECONDARY" }
            ]
        };
        ClassificationRuleSnapshotCache.For(database.Factory).Publish([new ManagedDocument {
            Key = "rules-exception", Revision = 1, Json = JsonSerializer.Serialize(new[] { rule }, JsonSerializerOptions.Web)
        }]);
        var completed = Fact("completed", ParcelProcessingStage.SortingCompleted, 10) with {
            IsSuccess = true, TargetChuteCode = "01", ActualChuteCode = "01"
        };
        capture.Commands.Clear();
        if (batch) {
            var image = Fact("image", ParcelProcessingStage.ImageRegistered, 11) with { ImagePath = "/source/image", RawPayload = "image-original" };
            var outcomes = await database.Processing.AppendBatchAsync([completed, image], default);
            Assert.All(outcomes, result => Assert.True(result.IsSuccess, result.ErrorMessage));
        } else {
            var outcome = await database.Processing.AppendAsync(completed, default);
            Assert.True(outcome.IsSuccess, outcome.ErrorMessage);
        }
        var replay = Assert.Single(capture.Commands, command => command.Sql.Contains(nameof(ParcelProcessingRecord.ResponseBody), StringComparison.Ordinal));
        AssertReplayColumns(replay.Sql);
        var parcel = await database.Parcels.GetByIdAsync(id, default);
        Assert.NotNull(parcel);
        Assert.Equal(ParcelStatus.Completed, parcel.Status);
        Assert.Equal(ParcelExceptionType.MechanicalFailure, parcel.ExceptionType);
        Assert.Equal(10000L, parcel.LifecycleMilliseconds);
        var saved = Assert.Single(parcel.ProcessingRecords, record => record.RecordId == scanned.RecordId);
        Assert.Equal(raw, saved.RawPayload);
        Assert.Equal(headers, saved.RequestHeaders);
        Assert.Equal(body, saved.RequestBody);
        Assert.Equal(response, saved.ResponseBody);
        Assert.Equal(barcodes, saved.BarcodesJson);
        Assert.Equal(scanned.RequestAt, saved.RequestAt);
        Assert.Equal(scanned.ResponseAt, saved.ResponseAt);
    }

    /// <summary>未绑定DWS仍保存并返回完整事实，不经过包裹快照重放投影。</summary>
    [Fact]
    public async Task UnboundMessagesRetainAllOriginalFields() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var unbound = Fact("unbound", ParcelProcessingStage.DwsReceived) with {
            SourceParcelId = null, RawPayload = "unbound-original", RequestHeaders = "unbound-headers", RequestBody = "unbound-request",
            ResponseBody = "unbound-response", MessageIdentity = "unbound-message", ReceivedAt = new DateTime(2026, 10, 1, 10, 0, 0, 100)
        };
        var result = await database.Processing.AppendAsync(unbound, default);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Null(result.Value!.ParcelId);
        var saved = Assert.Single(await database.Processing.GetUnboundAsync(10, default));
        Assert.Equal(unbound.RawPayload, saved.RawPayload);
        Assert.Equal(unbound.RequestHeaders, saved.RequestHeaders);
        Assert.Equal(unbound.RequestBody, saved.RequestBody);
        Assert.Equal(unbound.ResponseBody, saved.ResponseBody);
        Assert.Equal(unbound.MessageIdentity, saved.MessageIdentity);
        Assert.Equal(unbound.ReceivedAt, saved.ReceivedAt);
    }

    /// <summary>无规则匹配用途的阶段仍执行内置兜底、路由阻断和完成状态，已发布规则不能误改这些阶段。</summary>
    [Theory]
    [InlineData(ParcelProcessingStage.ChuteAssigned)]
    [InlineData(ParcelProcessingStage.SorterDispatched)]
    [InlineData(ParcelProcessingStage.SortingCompleted)]
    [InlineData(ParcelProcessingStage.LandingReported)]
    [InlineData(ParcelProcessingStage.ImageRegistered)]
    [InlineData(ParcelProcessingStage.ImageUploaded)]
    public void UnusedRuleStagesKeepBuiltInRoutingAndFallbackBehavior(ParcelProcessingStage stage) {
        var detected = Fact("detected", ParcelProcessingStage.Detected) with { ParcelId = 1 };
        var other = Fact("other", stage, 1) with {
            ParcelId = 1, IsSuccess = true, IsFallback = true, IsRoutingBlocked = true,
            TargetChuteCode = "01", ActualChuteCode = "01", ImagePath = "/source/image", ErrorMessage = "TRIGGER"
        };
        var rule = new ClassificationRule {
            Id = 1, Name = "非业务阶段不会应用的规则", Status = "已发布", ExceptionType = (int)ParcelExceptionType.MechanicalFailure,
            Conditions = [new() { Field = "异常信息", Operator = "等于", Value = "TRIGGER" }]
        };
        var parcel = Parcel.CreateDetected(1, detected, detected.RecordedAt);
        parcel.ApplyProcessingRecords([other, detected], [rule]);
        Assert.True(parcel.IsFallbackChuteAssigned);
        Assert.True(parcel.IsRoutingBlocked);
        Assert.Equal(ParcelExceptionType.AbnormalDischarge, parcel.ExceptionType);
        Assert.Equal(stage == ParcelProcessingStage.SortingCompleted ? ParcelStatus.Completed : ParcelStatus.SortingException, parcel.Status);
        if (stage is ParcelProcessingStage.ImageRegistered or ParcelProcessingStage.ImageUploaded) Assert.True(parcel.HasImages);
    }

    /// <summary>所有实际重放SQL均排除无需参与领域计算的LOB，规则必需字段不做截断。</summary>
    private static void AssertReplayColumns(string sql) {
        foreach (var name in new[] { nameof(ParcelProcessingRecord.RawPayload), nameof(ParcelProcessingRecord.RequestHeaders),
            nameof(ParcelProcessingRecord.RequestBody), nameof(ParcelProcessingRecord.ImagePath), nameof(ParcelProcessingRecord.RequestUrl),
            nameof(ParcelProcessingRecord.ReceivedAt), nameof(ParcelProcessingRecord.MessageIdentity) })
            Assert.DoesNotContain(name, sql, StringComparison.Ordinal);
        Assert.Contains(nameof(ParcelProcessingRecord.ResponseBody), sql, StringComparison.Ordinal);
        Assert.Contains(nameof(ParcelProcessingRecord.BarcodesJson), sql, StringComparison.Ordinal);
        Assert.DoesNotContain("SUBSTRING", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SUBSTR(", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>构造固定来源及入库分表锚点，原始事实的必要字段由各业务场景补齐。</summary>
    private static ParcelProcessingRecord Fact(string id, ParcelProcessingStage stage, int seconds = 0) => new() {
        RecordId = id, SourceInstanceId = "replay-query", SourceRunId = "run-a", SourceParcelId = 42,
        Stage = stage, OccurredAt = new DateTime(2026, 10, 1, 10, 0, 0).AddSeconds(seconds),
        RecordedAt = new(2026, 10, 1, 10, 0, 0), PartitionTime = new(2026, 10, 1, 10, 0, 0), PayloadHash = id
    };
}
