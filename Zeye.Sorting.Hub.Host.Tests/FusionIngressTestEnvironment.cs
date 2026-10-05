using System.Text.Json;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;
using Zeye.Sorting.Hub.Application.Services.Fusion;
using Zeye.Sorting.Hub.Application.Services.Parcels;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>独立数据库、临时图片根目录与固定协议身份，不连接生产来源。</summary>
public sealed class FusionIngressTestEnvironment : IAsyncDisposable {
    /// <summary>测试机器一的独立凭据。</summary>
    public const string FirstKey = "fusion-test-first-key-01234567890123456789";
    /// <summary>测试机器二的独立凭据。</summary>
    public const string SecondKey = "fusion-test-second-key-01234567890123456789";
    /// <summary>固定发送数据库身份。</summary>
    public const string Journal = "11111111111111111111111111111111";
    /// <summary>固定设备计数周期。</summary>
    public const string Run = "22222222222222222222222222222222";
    /// <summary>隔离真实关系数据库。</summary>
    public RelationalParcelTestDatabase Database { get; } = new();
    /// <summary>仅属于当前测试的文件目录。</summary>
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "zeye-fusion-test-" + Guid.NewGuid().ToString("N"));
    /// <summary>接收端测试配置。</summary>
    public FusionIngestionOptions Options { get; } = new() { AllowInsecureHttp = true, ImageDirectory = "images",
        Sources = [
            new() { SourceInstanceId = "fusion-line-01", LineId = "line-01", MachineApiKey = FirstKey, WorkstationName = "工作台一" },
            new() { SourceInstanceId = "fusion-line-02", LineId = "line-02", MachineApiKey = SecondKey, WorkstationName = "工作台二" }
        ] };
    /// <summary>接收实现，可重建以测试重启恢复。</summary>
    public FusionIngestionService Ingress { get; private set; } = null!;
    /// <summary>初始化隔离模型与接收端。</summary>
    public async Task InitializeAsync() { await Database.InitializeAsync(); Ingress = NewIngress(); }
    /// <summary>对同一耐久数据库和图片根目录创建新接收进程实例。</summary>
    public FusionIngestionService NewIngress() => new(Database.Factory, Microsoft.Extensions.Options.Options.Create(Options), Root);
    /// <summary>创建复用实际业务仓储的投影用例。</summary>
    public FusionProjectionService Projector(FusionIngestionService? ingress = null) => new(ingress ?? Ingress, new ParcelProcessingApplicationService(Database.Processing));
    /// <summary>创建符合来源登记的注册消息。</summary>
    public static FusionHello Hello(string source = "fusion-line-01", string journal = Journal, string run = Run) =>
        new("1.0", source, "sorting-hub", journal, run, "33333333333333333333333333333333", "test/1.0",
            "Asia/Shanghai", source == "fusion-line-01" ? "line-01" : "line-02", null, null);
    /// <summary>创建原始事实信封，保留毫秒级绝对事件时间与字符串 Int64。</summary>
    public static HubFactEnvelope Fact(string kind, long sequence, string? parcel = "9007199254740993",
        object? data = null, string source = "fusion-line-01", string journal = Journal, string run = Run, DateTimeOffset? time = null) {
        var occurred = (time ?? DateTimeOffset.Now.AddSeconds(-1)).ToOffset(TimeSpan.Zero);
        var record = Guid.NewGuid().ToString("N");
        var body = new HubFactBody("1.0", source, journal, run, parcel, record,
            sequence.ToString(System.Globalization.CultureInfo.InvariantCulture), kind, occurred, occurred, "Asia/Shanghai",
            "33333333333333333333333333333333", "test/1.0", new string('a', 64),
            JsonSerializer.SerializeToElement(data ?? new { barcode = "SAME-BARCODE", hasSorterDetection = true,
                detectedAtUtc = occurred, actualLandingConfirmed = false }, FusionProtocol.Json));
        var json = JsonSerializer.Serialize(body, FusionProtocol.Json);
        return new(record, body.SourceSequence, json, FusionProtocol.Hash(json));
    }
    /// <summary>通过登记连接提交原始批次。</summary>
    public static HubFactBatch Batch(FusionRegistration registration, params HubFactEnvelope[] records) =>
        new("1.0", registration.SourceInstanceId, registration.JournalId, registration.LeaseId, Guid.NewGuid().ToString("N"), records);
    /// <summary>仅删除当前测试生成的临时目录及独立数据库。</summary>
    public async ValueTask DisposeAsync() {
        await Database.DisposeAsync();
        if (Directory.Exists(Root) && Path.GetFullPath(Root).StartsWith(Path.Combine(Path.GetTempPath(), "zeye-fusion-test-"), StringComparison.OrdinalIgnoreCase))
            Directory.Delete(Root, recursive: true);
    }
}
