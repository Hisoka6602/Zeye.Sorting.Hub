using System.Globalization;
using System.Text.Json;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>按源端实际发送结构验证DWS时间、元数据和接收事实引用，不使用模拟的同名替代字段。</summary>
public sealed class FusionSourceMetadataTests {
    /// <summary>字典元数据和绑定顶层的本地时间保持原始精度，并关联同一测量身份。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DwsMetadataAndBindingShareExplicitIdentityAndSourceTimes(bool deviceIdentity) {
        var receivedAt = new DateTime(2026, 10, 1, 10, 0, 0, 123);
        var measuredAt = receivedAt.AddSeconds(-5).AddTicks(4567);
        var metadata = new Dictionary<string, string> {
            ["ReceivedAt"] = receivedAt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
            ["MeasuredAt"] = measuredAt.ToString("O", CultureInfo.InvariantCulture),
            ["FrameBoundaryReliable"] = "false", ["CorrelationId"] = "9007199254740993",
            ["TriggerBatch"] = "batch-01", ["ScanSequence"] = "sequence-07"
        };
        if (deviceIdentity) metadata["MessageIdentity"] = "device-message-07";
        var received = Body("dws.received", 1, null, new { barcode = "PKG", sourceMetadata = metadata });
        var binding = Body("dws.binding", 2, "17", new {
            decision = "Committed", finalParcelId = "17", candidateParcelId = "17", barcode = "PKG",
            receivedAt = metadata["ReceivedAt"], measuredAt = metadata["MeasuredAt"], receivedRecordId = received.RecordId,
            messageIdentity = deviceIdentity ? "device-message-07" : null
        });
        var registered = new FusionSourceOptions { SourceInstanceId = received.SourceInstanceId, TimeZoneId = "Asia/Shanghai" };
        var reception = FusionProtocol.Map(received, registered)!;
        var association = FusionProtocol.Map(binding, registered)!;
        Assert.Equal(receivedAt, reception.ReceivedAt); Assert.Equal(receivedAt, association.ReceivedAt);
        Assert.Equal(measuredAt, reception.MeasuredAt); Assert.Equal(measuredAt, association.MeasuredAt);
        Assert.Equal(reception.MessageIdentity, association.MessageIdentity); Assert.False(string.IsNullOrEmpty(reception.MessageIdentity));
        Assert.Equal("batch-01", reception.TriggerBatch); Assert.Equal("sequence-07", reception.ScanSequence);
        Assert.Equal(9007199254740993L, reception.CorrelationId); Assert.False(reception.HasReliableFrameBoundary);
        Assert.Null(reception.HasReliableTimestamp); Assert.Null(reception.SourceParcelId);
        Assert.True(association.IsSuccess); Assert.Equal(17L, association.FinalSourceParcelId);
        Assert.NotEqual(reception.MessageIdentity, FusionProtocol.Map(received with {
            JournalId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Data = JsonSerializer.SerializeToElement(new { barcode = "PKG" }, FusionProtocol.Json)
        }, registered)!.MessageIdentity);
    }

    /// <summary>坏的显式来源时间不能被机器当前时间或其他事件时间静默替换。</summary>
    [Fact]
    public void InvalidSourceTimeIsRejectedAndFrameReliabilityDoesNotInventTimestampReliability() {
        var fact = Body("dws.received", 1, null, new { barcode = "PKG", sourceMetadata = new Dictionary<string, string> { ["ReceivedAt"] = "invalid-time" } });
        var registered = new FusionSourceOptions { SourceInstanceId = fact.SourceInstanceId, TimeZoneId = "Asia/Shanghai" };
        Assert.Throws<ArgumentException>(() => FusionProtocol.Map(fact, registered));
        var unreliable = Body("dws.received", 2, null, new { barcode = "PKG", hasReliableTimestamp = false });
        Assert.False(FusionProtocol.Map(unreliable, registered)!.HasReliableTimestamp);
    }

    /// <summary>带偏移的源时间在接入边界解释为来源业务时区，规范协议字段具有明确优先级。</summary>
    [Fact]
    public void OffsetAndCanonicalSourceTimesDoNotDependOnHubTimeZone() {
        var fact = Body("dws.received", 1, null, new { barcode = "PKG", receivedAt = "2026-10-01T10:00:00.1234567+08:00" });
        var registered = new FusionSourceOptions { SourceInstanceId = fact.SourceInstanceId, TimeZoneId = "Asia/Shanghai" };
        var expected = new DateTime(2026, 10, 1, 10, 0, 0).AddTicks(1234567);
        Assert.Equal(expected, FusionProtocol.Map(fact, registered)!.ReceivedAt);
        var canonical = Body("dws.received", 2, null, new {
            barcode = "PKG", receivedAtUtc = new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero), receivedAt = "invalid-time"
        });
        Assert.Equal(new DateTime(2026, 10, 1, 11, 0, 0), FusionProtocol.Map(canonical, registered)!.ReceivedAt);
    }

    /// <summary>短身份保持兼容，超长身份按完整原值生成摘要，同前缀不同尾部不得合并接收和绑定。</summary>
    [Theory]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(1024)]
    public void CompleteExplicitMessageIdentityRemainsDistinctAndMatchesBinding(int length) {
        var prefix = new string('a', length - 1);
        var first = prefix + "甲";
        var second = prefix + "乙";
        var received = Body("dws.received", 1, null, new { barcode = "PKG", sourceMetadata = new Dictionary<string, string> { ["MessageIdentity"] = first } });
        var binding = Body("dws.binding", 2, "17", new { decision = "Committed", finalParcelId = "17", barcode = "PKG", messageIdentity = first });
        var other = Body("dws.received", 3, null, new { barcode = "PKG", sourceMetadata = new Dictionary<string, string> { ["MessageIdentity"] = second } });
        var registered = new FusionSourceOptions { SourceInstanceId = received.SourceInstanceId, TimeZoneId = "Asia/Shanghai" };
        var identity = FusionProtocol.Map(received, registered)!.MessageIdentity;

        Assert.Equal(length <= 256 ? first : "explicit-sha256:" + FusionProtocol.Hash(first), identity);
        Assert.Equal(identity, FusionProtocol.Map(binding, registered)!.MessageIdentity);
        Assert.NotEqual(identity, FusionProtocol.Map(other, registered)!.MessageIdentity);
        Assert.InRange(identity!.Length, 1, 256);
    }

    /// <summary>显式短文本不能伪装成接收引用或摘要命名空间，缺失身份也不能按同条码生成关联。</summary>
    [Fact]
    public void ExplicitIdentitiesCannotAliasReservedNamespacesOrGuessByBarcode() {
        var received = Body("dws.received", 1, null, new { barcode = "PKG" });
        var registered = new FusionSourceOptions { SourceInstanceId = received.SourceInstanceId, TimeZoneId = "Asia/Shanghai" };
        var fallback = FusionProtocol.Map(received, registered)!.MessageIdentity!;
        var digest = FusionProtocol.Map(Body("dws.received", 2, null, new { messageIdentity = new string('x', 257) }), registered)!.MessageIdentity!;
        foreach (var reserved in new[] { fallback, digest, fallback.ToUpperInvariant(), digest.ToUpperInvariant() }) {
            var explicitReceived = Body("dws.received", 3, null, new { barcode = "PKG", sourceMetadata = new Dictionary<string, string> { ["MessageIdentity"] = reserved } });
            var binding = Body("dws.binding", 4, "17", new { decision = "Committed", finalParcelId = "17", barcode = "PKG", messageIdentity = reserved });
            var normalized = FusionProtocol.Map(explicitReceived, registered)!.MessageIdentity;
            Assert.Equal("explicit-sha256:" + FusionProtocol.Hash(reserved), normalized);
            Assert.Equal(normalized, FusionProtocol.Map(binding, registered)!.MessageIdentity);
            Assert.NotEqual(reserved, normalized);
            Assert.NotEqual(fallback, normalized);
            Assert.NotEqual(digest, normalized);
        }
        var unreferenced = Body("dws.binding", 5, "17", new { decision = "Committed", finalParcelId = "17", barcode = "PKG" });
        Assert.Null(FusionProtocol.Map(unreferenced, registered)!.MessageIdentity);
    }

    /// <summary>真实接入保留长身份原文，同前缀的两条设备消息独立分析，不因截断产生测量冲突。</summary>
    [Fact]
    public async Task LongMessageIdentitiesKeepSeparateMeasurementsAndOriginalFacts() {
        await using var environment = new FusionIngressTestEnvironment();
        await environment.InitializeAsync();
        var lease = await environment.Ingress.RegisterAsync("long-identity-test", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var at = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero);
        var prefix = new string('m', 256);
        var firstIdentity = prefix + "甲";
        var secondIdentity = prefix + "乙";
        var firstDetection = FusionIngressTestEnvironment.Fact("parcel.detected", 1, "17", time: at);
        var firstReceived = FusionIngressTestEnvironment.Fact("dws.received", 2, null, new {
            barcode = "PKG", weightGrams = 1000m, sourceMetadata = new Dictionary<string, string> { ["MessageIdentity"] = firstIdentity }
        }, time: at.AddMilliseconds(200));
        var firstBinding = FusionIngressTestEnvironment.Fact("dws.binding", 3, "17", new {
            decision = "Committed", finalParcelId = "17", barcode = "PKG", weightGrams = 1000m, messageIdentity = firstIdentity
        }, time: at.AddMilliseconds(300));
        var secondDetection = FusionIngressTestEnvironment.Fact("parcel.detected", 4, "18", time: at.AddSeconds(1));
        var secondReceived = FusionIngressTestEnvironment.Fact("dws.received", 5, null, new {
            barcode = "PKG", weightGrams = 1000m, sourceMetadata = new Dictionary<string, string> { ["MessageIdentity"] = secondIdentity }
        }, time: at.AddMilliseconds(1200));
        var secondBinding = FusionIngressTestEnvironment.Fact("dws.binding", 6, "18", new {
            decision = "Committed", finalParcelId = "18", barcode = "PKG", weightGrams = 1000m, messageIdentity = secondIdentity
        }, time: at.AddMilliseconds(1300));
        var published = await environment.Ingress.PublishAsync("long-identity-test", FusionIngressTestEnvironment.Batch(lease,
            firstDetection, firstReceived, firstBinding, secondDetection, secondReceived, secondBinding), default);
        Assert.All(published.Records, receipt => Assert.Equal("stored", receipt.Status));
        Assert.Equal(6, await environment.Projector().ProjectAsync(default));
        var reader = new ParcelDwsConsistencyReadService(environment.Database.Factory,
            new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions())), environment.Database.Partitions);
        var report = await reader.ReadAsync(new ParcelDwsConsistencyRequest {
            FromDate = DateTime.Now.Date, ToDate = DateTime.Now.Date, DetailBarcode = "PKG"
        }, default);
        Assert.Equal(2, report.MeasurementCount);
        Assert.Equal(0, report.ConflictingMeasurementCount);
        Assert.Equal(2, report.ScanTimingSampleCount);
        Assert.All(report.Detail!.Items, item => Assert.Equal(200m, item.ScanDurationMilliseconds));
        var facts = await environment.Ingress.GetFactsAsync("fusion-line-01", null, 20, default);
        Assert.Equal(firstReceived.BodyJson, facts.Single(fact => fact.RecordId == firstReceived.RecordId).BodyJson);
        Assert.Equal(secondReceived.BodyJson, facts.Single(fact => fact.RecordId == secondReceived.RecordId).BodyJson);
    }

    /// <summary>真实接入、投影和DWS分析贯通源端元数据，未绑定接收通过明确事实引用补齐200毫秒扫码耗时。</summary>
    [Fact]
    public async Task ActualSourceDwsShapeSurvivesIngestionAndProducesRealScanEndpoints() {
        await using var environment = new FusionIngressTestEnvironment(); await environment.InitializeAsync();
        var lease = await environment.Ingress.RegisterAsync("metadata-test", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var at = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero);
        var receivedAt = new DateTime(2026, 10, 1, 10, 0, 0, 200);
        var detection = FusionIngressTestEnvironment.Fact("parcel.detected", 1, "17", time: at);
        var reception = FusionIngressTestEnvironment.Fact("dws.received", 2, null, new {
            barcode = "PKG", weightGrams = 1000m, sourceMetadata = new Dictionary<string, string> {
                ["ReceivedAt"] = receivedAt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture), ["FrameBoundaryReliable"] = "true"
            }
        }, time: at.AddMilliseconds(200));
        var binding = FusionIngressTestEnvironment.Fact("dws.binding", 3, "17", new {
            decision = "Committed", finalParcelId = "17", barcode = "PKG", receivedRecordId = reception.RecordId,
            receivedAt = receivedAt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
        }, time: at.AddMilliseconds(300));
        var published = await environment.Ingress.PublishAsync("metadata-test", FusionIngressTestEnvironment.Batch(lease, detection, reception, binding), default);
        Assert.All(published.Records, receipt => Assert.Equal("stored", receipt.Status));
        Assert.Equal(3, await environment.Projector().ProjectAsync(default));
        var reader = new ParcelDwsConsistencyReadService(environment.Database.Factory,
            new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions())), environment.Database.Partitions);
        var today = DateTime.Now.Date;
        var report = await reader.ReadAsync(new ParcelDwsConsistencyRequest { FromDate = today, ToDate = today, DetailBarcode = "PKG" }, default);
        Assert.Equal(1, report.MeasurementCount); Assert.Equal(1, report.ScanTimingSampleCount);
        var sample = Assert.Single(report.Detail!.Items);
        Assert.Equal(receivedAt, sample.ScanCompletedAt); Assert.Equal(200m, sample.ScanDurationMilliseconds);
        Assert.Equal("source-received", sample.ScanTimingBasis); Assert.Null(sample.ScanTimingUnavailableReason);
        Assert.Equal(reception.BodyJson, (await environment.Ingress.GetFactsAsync("fusion-line-01", null, 20, default))
            .Single(fact => fact.RecordId == reception.RecordId).BodyJson);
    }

    /// <summary>生成与真实发送端一致的不可变事实载荷。</summary>
    private static HubFactBody Body(string kind, long sequence, string? parcel, object data) =>
        JsonSerializer.Deserialize<HubFactBody>(FusionIngressTestEnvironment.Fact(kind, sequence, parcel, data).BodyJson, FusionProtocol.Json)!;
}
