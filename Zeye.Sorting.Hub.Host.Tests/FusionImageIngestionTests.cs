using System.Text;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>测试图片字节耐久性、身份隔离、断点续传及关联先后顺序。</summary>
public sealed class FusionImageIngestionTests {
    /// <summary>首块重复必须相同，断线后新连接重新 Begin 并从已确认偏移继续。</summary>
    [Fact]
    public async Task UploadResumesAfterReconnectAndTruncatesUnacknowledgedTail() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var bytes = Encoding.ASCII.GetBytes("first-part-second-part");
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var descriptor = new HubImageDescriptor(lease.SourceInstanceId, lease.JournalId, lease.LeaseId, "image-1", "../escaped.png", "image/png", bytes.Length, FusionProtocol.Hash(bytes));
        var begin = await env.Ingress.BeginImageAsync("a", descriptor, default);
        var chunk = Chunk(begin.UploadId, 0, bytes.AsSpan(0, 10).ToArray());
        Assert.Equal(10, (await env.Ingress.WriteImageAsync("a", chunk, default)).NextOffset);
        Assert.Equal(10, (await env.Ingress.WriteImageAsync("a", chunk, default)).NextOffset);
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Ingress.WriteImageAsync("a", Chunk(begin.UploadId, 0, new byte[10]), default));
        await env.Ingress.DisconnectAsync("a", default);
        var restarted = env.NewIngress();
        var next = await restarted.RegisterAsync("b", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.WriteImageAsync("b", chunk, default));
        var resume = await restarted.BeginImageAsync("b", descriptor with { LeaseId = next.LeaseId }, default);
        Assert.Equal(10, resume.NextOffset);
        var path = Path.Combine(env.Root, "images", "uploads", FusionProtocol.Key("fusion-line-01", "image-1") + ".part");
        await using (var file = new FileStream(path, FileMode.Append, FileAccess.Write)) await file.WriteAsync(new byte[] { 9, 9, 9 });
        await restarted.WriteImageAsync("b", Chunk(resume.UploadId, 10, bytes.AsSpan(10).ToArray()), default);
        var stored = await restarted.CompleteImageAsync("b", new(resume.UploadId, "image-1", bytes.Length, descriptor.ContentSha256), default);
        Assert.Equal(descriptor.ContentSha256, stored.ContentSha256);
        Assert.False(File.Exists(Path.Combine(env.Root, "escaped.png")));
        var duplicate = await restarted.BeginImageAsync("b", descriptor with { LeaseId = next.LeaseId }, default);
        Assert.NotNull(duplicate.Stored); Assert.Equal(bytes.Length, duplicate.NextOffset);
        var read = await restarted.ReadImageAsync(stored.ObjectKey, default);
        Assert.NotNull(read);
        await using var content = read.Value.Content; using var copy = new MemoryStream(); await content.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());
    }
    /// <summary>描述内容变更与提前完成被拒绝，上传身份不能被另一来源使用。</summary>
    [Fact]
    public async Task ImageImmutableIdentityAndUploadOwnershipAreEnforced() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var one = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        await env.Ingress.RegisterAsync("b", "fusion-line-02", FusionIngressTestEnvironment.Hello("fusion-line-02"), default);
        var descriptor = new HubImageDescriptor(one.SourceInstanceId, one.JournalId, one.LeaseId, "one", "sample.jpg", "image/jpeg", 3, FusionProtocol.Hash(new byte[] { 1, 2, 3 }));
        var begin = await env.Ingress.BeginImageAsync("a", descriptor, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Ingress.CompleteImageAsync("a", new(begin.UploadId, "one", 3, descriptor.ContentSha256), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Ingress.BeginImageAsync("a", descriptor with { SizeBytes = 4 }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Ingress.WriteImageAsync("b", Chunk(begin.UploadId, 0, new byte[] { 1, 2, 3 }), default));
        await Assert.ThrowsAsync<ArgumentException>(() => env.Ingress.WriteImageAsync("a", new(begin.UploadId, 0, "AQID", new string('0', 64)), default));
        Assert.Null(await env.Ingress.ReadImageAsync("../escaped", default));
    }
    /// <summary>多图片明确关联可先于或晚于字节存储，多候选图片不推断包裹。</summary>
    [Fact]
    public async Task MultipleImagesSupportAssociationBeforeAndAfterStorage() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        foreach (var imageId in new[] { "front", "back" }) {
            if (imageId == "front") await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease,
                FusionIngressTestEnvironment.Fact("image.association", 1, data: new { sourceImageId = imageId, cameraName = "前视相机", associationConfirmed = true, candidateCount = 1 })), default);
            var bytes = new byte[] { 1, 2, 3 };
            var descriptor = new HubImageDescriptor(lease.SourceInstanceId, lease.JournalId, lease.LeaseId, imageId, imageId + ".jpg", "image/jpeg", bytes.Length, FusionProtocol.Hash(bytes));
            var upload = await env.Ingress.BeginImageAsync("a", descriptor, default);
            await env.Ingress.WriteImageAsync("a", Chunk(upload.UploadId, 0, bytes), default);
            await env.Ingress.CompleteImageAsync("a", new(upload.UploadId, imageId, bytes.Length, descriptor.ContentSha256), default);
            if (imageId == "back") await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease,
                FusionIngressTestEnvironment.Fact("image.association", 2, data: new { sourceImageId = imageId, cameraName = "后视相机", associationConfirmed = true, candidateCount = 1 })), default);
        }
        var ambiguous = FusionIngressTestEnvironment.Fact("image.association", 3, data: new { sourceImageId = "ambiguous", associationConfirmed = false, candidateCount = 2 });
        Assert.Equal("stored", Assert.Single((await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, ambiguous), default)).Records).Status);
        Assert.Equal(2, await env.Projector().ProjectAsync(default));
        var rows = await env.Ingress.GetFactsAsync("fusion-line-01", null, 20, default);
        var id = rows.First(x => x.ParcelId is not null).ParcelId!;
        var parcel = (await env.Database.Parcels.GetByIdAsync(long.Parse(id), default))!;
        Assert.True(parcel.HasImages); Assert.Null(parcel.DetectedTime); Assert.Equal(2, parcel.ProcessingRecords.Count);
        await using var db = await env.Database.Factory.CreateDbContextAsync();
        Assert.Equal(2, await db.Set<FusionImageUpload>().CountAsync(x => x.SourceParcelId == 9007199254740993 && x.IsStored));
        Assert.Null(rows.Single(x => x.RecordId == ambiguous.RecordId).ParcelId);
    }
    /// <summary>有界过期维护仅重置临时上传偏移，完整图片和关联身份保留。</summary>
    [Fact]
    public async Task ExpiredPartialUploadCanRestartWithoutDeletingAssociation() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var descriptor = new HubImageDescriptor(lease.SourceInstanceId, lease.JournalId, lease.LeaseId, "expired", "x.png", "image/png", 3, FusionProtocol.Hash(new byte[] { 1, 2, 3 }));
        var begin = await env.Ingress.BeginImageAsync("a", descriptor, default);
        await env.Ingress.WriteImageAsync("a", Chunk(begin.UploadId, 0, new byte[] { 1 }), default);
        await using (var db = await env.Database.Factory.CreateDbContextAsync()) {
            await db.Set<FusionImageUpload>().ExecuteUpdateAsync(p => p.SetProperty(x => x.ModifiedAt, DateTime.Now.AddDays(-2)));
        }
        await env.Ingress.MaintainUploadsAsync(default);
        var again = await env.Ingress.BeginImageAsync("a", descriptor, default);
        Assert.Equal(0, again.NextOffset); Assert.NotEqual(begin.UploadId, again.UploadId);
    }
    /// <summary>构造不可变块摘要，实际字节参与比较。</summary>
    private static HubImageChunk Chunk(string id, long offset, byte[] bytes) => new(id, offset, Convert.ToBase64String(bytes), FusionProtocol.Hash(bytes));
}
