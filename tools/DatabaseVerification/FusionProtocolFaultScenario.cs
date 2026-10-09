using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Tools.DatabaseVerification;

/// <summary>在已隔离的真实关系库验证坏消息、唯一身份冲突、分块损坏和恢复，不使用内存提供器。</summary>
internal static class FusionProtocolFaultScenario {
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedStoredDuplicateRejectedRejectedValues = new[] { "stored", "duplicate", "rejected", "rejected", "rejected" };
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedStoredStoredRejectedValues = new[] { "stored", "stored", "rejected" };

    /// <summary>异常验证日志，只输出错误编码，不输出凭据和正文。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    /// <summary>协议序列化格式与服务端一致。</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>独立来源只产生无包裹投影的原始事实及一个测试对象，结束后保留证据。</summary>
    internal static async Task<object> ExecuteAsync(IDbContextFactory<SortingHubDbContext> factory) {
        var source = "protocol-fault-" + Guid.NewGuid().ToString("N");
        var journal = Guid.NewGuid().ToString("N");
        var run = Guid.NewGuid().ToString("N");
        var checks = new List<string>();
        // 使用真实 Fusion 已接收报文的绝对协议时间，验收入口不重新进行时区转换。
        await using var sampleDatabase = await factory.CreateDbContextAsync();
        var sample = await sampleDatabase.Set<FusionFactReceipt>().AsNoTracking().OrderBy(fact => fact.Key).Select(fact => fact.BodyJson).FirstAsync();
        var protocolTimestamp = JsonSerializer.Deserialize<HubFactBody>(sample, Json)!.OccurredAtUtc;
        var options = new FusionIngestionOptions { HubId = "database-verification", Sources = [new() {
            SourceInstanceId = source, LineId = "protocol-verification", WorkstationName = "数据库异常验收",
            MachineApiKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)) }] };
        var ingress = new FusionIngestionService(factory, Options.Create(options), Directory.GetCurrentDirectory());
        var hello = new FusionHello("1.0", source, options.HubId, journal, run, Guid.NewGuid().ToString("N"),
            "verification/1.0", "Asia/Shanghai", "protocol-verification", null, null);
        Ensure(!ingress.Authenticate(source, "invalid-credential") && !ingress.Authenticate(source + "-other", options.Sources[0].MachineApiKey), "credential-isolation", checks);
        await RejectAsync(() => ingress.RegisterAsync("invalid", source, hello with { LineId = "wrong-line" }, default), "RegistrationMismatch", checks);
        var registration = await ingress.RegisterAsync("protocol-initial", source, hello, default);
        var competing = new FusionIngestionService(factory, Options.Create(options), Directory.GetCurrentDirectory());
        await RejectAsync(() => competing.RegisterAsync("competing", source, hello, default), "SourceInstanceAlreadyConnected", checks);

        // 步骤1：同批正确事实、相同重放及错误信封互相隔离，只有正确原文进入耐久库。
        var text = "protocol-raw-text\n\"中文\"\u0001😀" + new string('文', 4096);
        var first = Fact(hello, 9007199254740993, protocolTimestamp, new { reason = text, empty = "", nullable = (string?)null });
        var invalidHash = Fact(hello, 1, protocolTimestamp) with { BodySha256 = new string('0', 64) };
        var invalidKind = Fact(hello, 2, protocolTimestamp, kind: "future.unknown");
        var invalidSource = Fact(hello with { SourceInstanceId = source + "-other" }, 3, protocolTimestamp);
        var reply = await ingress.PublishAsync("protocol-initial", Batch(registration, first, first, invalidHash, invalidKind, invalidSource), default);
        Ensure(reply.Records.Select(item => item.Status).SequenceEqual(CachedStoredDuplicateRejectedRejectedValues), "mixed-batch-isolation", checks);
        Ensure((await ingress.PublishAsync("protocol-initial", Batch(registration, first), default)).Records.Single().Status == "duplicate", "exact-replay-idempotency", checks);
        var changedJson = first.BodyJson.Replace("protocol-raw-text", "changed-raw-text", StringComparison.Ordinal);
        var changed = first with { BodyJson = changedJson, BodySha256 = Hash(Encoding.UTF8.GetBytes(changedJson)) };
        var collision = Fact(hello, 9007199254740993, protocolTimestamp);
        reply = await ingress.PublishAsync("protocol-initial", Batch(registration, changed, collision), default);
        Ensure(reply.Records.All(item => item.Status == "conflict"), "immutable-id-and-sequence-conflicts", checks);
        var late = Fact(hello, 4, protocolTimestamp, new { reason = "late sequence" });
        var maximum = Fact(hello, long.MaxValue, protocolTimestamp, new { reason = "Int64 maximum" });
        var overflow = Fact(hello, 5, protocolTimestamp) with { SourceSequence = "9223372036854775808" };
        reply = await ingress.PublishAsync("protocol-initial", Batch(registration, maximum, late, overflow), default);
        Ensure(reply.Records.Select(item => item.Status).SequenceEqual(CachedStoredStoredRejectedValues), "int64-boundary-and-out-of-order", checks);
        await RejectAsync(() => ingress.PublishAsync("protocol-initial", Batch(registration, Enumerable.Repeat(first, options.MaxBatchRecords + 1).ToArray()), default), "InvalidBatchLimits", checks);
        var oversized = first with { BodyJson = new string('x', options.MaxBatchBytes + 1) };
        await RejectAsync(() => ingress.PublishAsync("protocol-initial", Batch(registration, oversized), default), "InvalidBatchLimits", checks);
        await RejectAsync(() => ingress.PublishAsync("protocol-initial", Batch(registration, first) with { LeaseId = Guid.NewGuid().ToString("N") }, default), "InvalidLease", checks);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => ingress.PublishAsync("protocol-initial", Batch(registration, first), default)));
        Ensure(concurrent.All(batch => batch.Records.Single().Status == "duplicate"), "32-concurrent-identical-replays", checks);

        // 步骤2：断块、错误摘要、重复偏移、内容冲突和接收实例重建都必须保持耐久偏移。
        var bytes = Enumerable.Range(0, 256).Select(number => (byte)number).ToArray();
        var descriptor = new HubImageDescriptor(source, journal, registration.LeaseId, Guid.NewGuid().ToString("N"),
            "../显示图片.bin", "application/octet-stream", bytes.Length, Hash(bytes));
        await RejectAsync(() => ingress.BeginImageAsync("protocol-initial", descriptor with { ContentType = "text/html" }, default), "InvalidImageDescriptor", checks);
        var begun = await ingress.BeginImageAsync("protocol-initial", descriptor, default);
        var complete = new HubImageComplete(begun.UploadId, descriptor.SourceImageId, bytes.Length, descriptor.ContentSha256);
        await RejectAsync(() => ingress.CompleteImageAsync("protocol-initial", complete, default), "IncompleteImageOrContentConflict", checks);
        var chunk = Chunk(begun.UploadId, 0, bytes[..128]);
        await RejectAsync(() => ingress.WriteImageAsync("protocol-initial", chunk with { ChunkSha256 = new string('0', 64) }, default), "InvalidImageChunkHash", checks);
        await RejectAsync(() => ingress.WriteImageAsync("protocol-initial", chunk with { Offset = 128 }, default), "InvalidImageOffset", checks);
        Ensure((await ingress.WriteImageAsync("protocol-initial", chunk, default)).NextOffset == 128, "first-image-chunk-durable", checks);
        Ensure((await ingress.WriteImageAsync("protocol-initial", chunk, default)).NextOffset == 128, "identical-image-chunk-replay", checks);
        var differentBytes = bytes[..128]; differentBytes[0] ^= 1;
        await RejectAsync(() => ingress.WriteImageAsync("protocol-initial", Chunk(begun.UploadId, 0, differentBytes), default), "ImageChunkConflict", checks);
        await ingress.DisconnectAsync("protocol-initial", default);
        registration = await competing.RegisterAsync("protocol-resumed", source, hello, default);
        descriptor = descriptor with { LeaseId = registration.LeaseId };
        var resumed = await competing.BeginImageAsync("protocol-resumed", descriptor, default);
        Ensure(resumed.NextOffset == 128 && resumed.UploadId == begun.UploadId, "recreated-receiver-resumes-durable-offset", checks);
        await RejectAsync(() => ingress.WriteImageAsync("protocol-initial", chunk, default), "InvalidLease", checks);
        Ensure((await competing.WriteImageAsync("protocol-resumed", Chunk(resumed.UploadId, 128, bytes.AsSpan(128).ToArray()), default)).NextOffset == bytes.Length,
            "resumed-image-fully-written", checks);
        var stored = await competing.CompleteImageAsync("protocol-resumed", complete, default);
        Ensure((await competing.CompleteImageAsync("protocol-resumed", complete, default)) == stored, "image-completion-idempotency", checks);
        Ensure((await competing.BeginImageAsync("protocol-resumed", descriptor, default)).Stored == stored, "stored-image-begin-idempotency", checks);
        await RejectAsync(() => competing.BeginImageAsync("protocol-resumed", descriptor with { ContentSha256 = new string('0', 64) }, default), "ImageContentConflict", checks);
        var content = await competing.ReadImageAsync(stored.ObjectKey, default) ?? throw new InvalidOperationException("已确认图片对象不能读取。");
        await using (content.Content) {
            using var buffer = new MemoryStream();
            await content.Content.CopyToAsync(buffer);
            Ensure(buffer.ToArray().SequenceEqual(bytes), "exact-image-bytes-after-recovery", checks);
        }

        // 步骤3：读取最终原文，错误输入和并发重放不能增加事实，NCLOB/TEXT 不能改写 Unicode。
        await using var database = await factory.CreateDbContextAsync();
        var facts = await database.Set<FusionFactReceipt>().AsNoTracking().Where(fact => fact.SourceInstanceId == source).ToListAsync();
        Ensure(facts.Count == 3 && facts.All(fact => fact.ProjectionState == "complete"), "only-three-valid-facts-persisted", checks);
        var persisted = facts.Single(fact => fact.RecordId == first.RecordId);
        Ensure(persisted.BodyJson == first.BodyJson && persisted.BodySha256 == Hash(Encoding.UTF8.GetBytes(first.BodyJson))
            && persisted.SourceSequence == 9007199254740993
            && JsonSerializer.Deserialize<HubFactBody>(persisted.BodyJson, Json)!.Data.GetProperty("reason").GetString() == text,
            "unicode-lob-empty-values-and-large-id-roundtrip", checks);
        Ensure(facts.Any(fact => fact.SourceSequence == long.MaxValue), "int64-maximum-persisted-exactly", checks);
        await competing.DisconnectAsync("protocol-resumed", default);
        return new { source, checks, passed = checks.Count, validFacts = facts.Count, imageBytes = bytes.Length };
    }

    /// <summary>构造合法但无业务投影的来源事实，保持已有协议时间原值。</summary>
    private static HubFactEnvelope Fact(FusionHello hello, long sequence, DateTimeOffset protocolTimestamp, object? data = null, string kind = "source.counter-run") {
        var record = Guid.NewGuid().ToString("N");
        var body = new HubFactBody("1.0", hello.SourceInstanceId, hello.JournalId, hello.SourceRunId, null, record,
            sequence.ToString(CultureInfo.InvariantCulture), kind, protocolTimestamp, protocolTimestamp, hello.TimeZoneId, hello.ProducerSessionId,
            hello.ProducerVersion, new string('a', 64), JsonSerializer.SerializeToElement(data ?? new { reason = "verification" }, Json));
        var text = JsonSerializer.Serialize(body, Json);
        return new(record, body.SourceSequence, text, Hash(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>将信封绑定到当前耐久租约。</summary>
    private static HubFactBatch Batch(FusionRegistration registration, params HubFactEnvelope[] facts) =>
        new("1.0", registration.SourceInstanceId, registration.JournalId, registration.LeaseId, Guid.NewGuid().ToString("N"), facts);

    /// <summary>构造具有真实摘要的图片块。</summary>
    private static HubImageChunk Chunk(string upload, long offset, byte[] bytes) => new(upload, offset, Convert.ToBase64String(bytes), Hash(bytes));

    /// <summary>协议 SHA-256 只对原始 UTF-8 或图片字节计算。</summary>
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>必须匹配预期错误编码，数据库不可用和查询错误不能冒充输入拒绝通过。</summary>
    private static async Task RejectAsync(Func<Task> action, string expected, List<string> checks) {
        try { await action(); }
        catch (Exception exception) when ((exception is ArgumentException or InvalidOperationException) && exception.Message == expected) {
            Logger.Warn(exception, "独立协议异常验收符合预期，Code={Code}", expected);
            checks.Add(expected);
            return;
        }
        throw new InvalidOperationException("异常输入未被拒绝：" + expected);
    }

    /// <summary>记录成功断言，失败直接中止验收而不更改业务数据。</summary>
    private static void Ensure(bool condition, string name, List<string> checks) {
        if (!condition) throw new InvalidOperationException("协议异常验收失败：" + name);
        checks.Add(name);
    }
}
