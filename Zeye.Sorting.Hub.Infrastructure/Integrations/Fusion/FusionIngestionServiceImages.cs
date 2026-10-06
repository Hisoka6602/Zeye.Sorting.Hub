using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>来源图片的耐久分块、重放校验和完整对象确认。</summary>
public sealed partial class FusionIngestionService {
    /// <summary>仅允许图片或二进制媒体类型，禁止可执行 HTML 与 SVG 内容。</summary>
    private static readonly HashSet<string> ImageTypes = new(StringComparer.Ordinal) {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp", "image/tiff", "application/octet-stream"
    };

    /// <summary>只根据服务端摘要键构建路径，不接受来源文件名或来源设备路径。</summary>
    private string ImagePath(string key, bool complete) {
        if (!FusionProtocol.IsHex(key, 64)) throw new ArgumentException("InvalidImageKey");
        return Path.Combine(_imageDirectory, complete ? "objects" : "uploads", key + (complete ? ".bin" : ".part"));
    }

    /// <summary>同来源图片编号仅允许同一内容，明确关联与字节上传均可先到达。</summary>
    public Task<HubImageBeginReceipt> BeginImageAsync(string connectionId, HubImageDescriptor descriptor, CancellationToken cancellationToken) =>
        LockedAsync(descriptor.SourceInstanceId, async () => {
            var connection = Connection(connectionId, descriptor.SourceInstanceId, descriptor.JournalId, descriptor.LeaseId);
            if (string.IsNullOrWhiteSpace(descriptor.SourceImageId) || descriptor.SourceImageId.Length > 128 || descriptor.SourceImageId.Any(char.IsControl)
                || !FusionProtocol.IsHex(descriptor.ContentSha256, 64) || descriptor.SizeBytes <= 0 || descriptor.SizeBytes > _options.MaxImageBytes
                || descriptor.FileName is null || descriptor.FileName.Length > 256 || !ImageTypes.Contains(descriptor.ContentType))
                throw new ArgumentException("InvalidImageDescriptor");
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var key = FusionProtocol.Key(connection.Source.SourceInstanceId, descriptor.SourceImageId);
            var image = await db.Set<FusionImageUpload>().AsTracking().SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (image is not null && image.SizeBytes > 0 && (image.ContentSha256 != descriptor.ContentSha256 || image.SizeBytes != descriptor.SizeBytes))
                throw new InvalidOperationException("ImageContentConflict");
            if (image is { IsStored: true }) {
                if (!File.Exists(ImagePath(key, true))) throw new IOException("StoredImageUnavailable");
                return new HubImageBeginReceipt(image.SourceImageId, image.ContentSha256, image.UploadId!, image.SizeBytes, _options.MaxImageChunkBytes, Stored(image));
            }
            if (image is null || image.SizeBytes == 0) {
                if (await db.Set<FusionImageUpload>().CountAsync(x => x.SourceInstanceId == descriptor.SourceInstanceId && !x.IsStored && x.SizeBytes > 0, cancellationToken)
                    >= _options.MaxPendingImagesPerSource) throw new InvalidOperationException("PendingImagesLimit");
            }
            if (image is null) { image = new() { Key = key, SourceInstanceId = descriptor.SourceInstanceId, SourceImageId = descriptor.SourceImageId }; db.Add(image); }
            image.SizeBytes = descriptor.SizeBytes; image.ContentSha256 = descriptor.ContentSha256;
            image.FileName = descriptor.FileName; image.ContentType = descriptor.ContentType;
            image.UploadId ??= Guid.NewGuid().ToString("N"); image.ModifiedAt = DateTime.Now; image.Revision++;
            var partial = ImagePath(key, false);
            if (image.NextOffset > 0 && !File.Exists(partial) && !File.Exists(ImagePath(key, true)))
                throw new IOException("UploadBytesUnavailable");
            await db.SaveChangesAsync(cancellationToken);
            _uploadConnections[image.UploadId] = connectionId;
            return new HubImageBeginReceipt(image.SourceImageId, image.ContentSha256, image.UploadId, image.NextOffset, _options.MaxImageChunkBytes);
        }, cancellationToken);

    /// <summary>验证上传授权属于当前登记连接，断线后必须重新调用 Begin。</summary>
    private async Task<FusionImageUpload> AuthorizedImageAsync(string connectionId, string uploadId, Persistence.SortingHubDbContext db, CancellationToken cancellationToken) {
        var connection = Connection(connectionId);
        if (!FusionProtocol.IsHex(uploadId, 32) || !_uploadConnections.TryGetValue(uploadId, out var owner) || owner != connectionId)
            throw new InvalidOperationException("InvalidUploadLease");
        return await db.Set<FusionImageUpload>().AsTracking().SingleOrDefaultAsync(x => x.UploadId == uploadId
            && x.SourceInstanceId == connection.Source.SourceInstanceId, cancellationToken) ?? throw new InvalidOperationException("InvalidUploadLease");
    }

    /// <summary>顺序分块先写盘并刷入持久介质，再保存下一偏移；同偏移重放必须逐字节相同。</summary>
    public Task<HubImageChunkReceipt> WriteImageAsync(string connectionId, HubImageChunk chunk, CancellationToken cancellationToken) {
        var connection = Connection(connectionId);
        return LockedAsync(connection.Source.SourceInstanceId, async () => {
            if (chunk.Offset < 0 || chunk.DataBase64 is null || chunk.DataBase64.Length > ((_options.MaxImageChunkBytes + 2) / 3) * 4
                || !FusionProtocol.IsHex(chunk.ChunkSha256, 64)) throw new ArgumentException("InvalidImageChunk");
            var bytes = Convert.FromBase64String(chunk.DataBase64);
            if (bytes.Length is < 1 || bytes.Length > _options.MaxImageChunkBytes || FusionProtocol.Hash(bytes) != chunk.ChunkSha256)
                throw new ArgumentException("InvalidImageChunkHash");
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var image = await AuthorizedImageAsync(connectionId, chunk.UploadId, db, cancellationToken);
            if (chunk.Offset > image.NextOffset || chunk.Offset > image.SizeBytes - bytes.Length) throw new InvalidOperationException("InvalidImageOffset");
            var path = ImagePath(image.Key, image.IsStored);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                65536, FileOptions.Asynchronous | FileOptions.WriteThrough)) {
                if (file.Length < image.NextOffset) throw new IOException("UploadBytesUnavailable");
                if (chunk.Offset < image.NextOffset || image.IsStored) {
                    if (chunk.Offset + bytes.Length > image.NextOffset) throw new InvalidOperationException("ImageChunkConflict");
                    file.Position = chunk.Offset; var previous = new byte[bytes.Length];
                    await file.ReadExactlyAsync(previous, cancellationToken);
                    if (!CryptographicOperations.FixedTimeEquals(previous, bytes)) throw new InvalidOperationException("ImageChunkConflict");
                    return new HubImageChunkReceipt(chunk.UploadId, image.NextOffset);
                }
                // 崩溃可能留下未确认尾部；依据数据库已确认偏移截断后继续。
                file.SetLength(image.NextOffset); file.Position = image.NextOffset;
                await file.WriteAsync(bytes, cancellationToken); await file.FlushAsync(cancellationToken); file.Flush(flushToDisk: true);
            }
            image.NextOffset += bytes.Length; image.ModifiedAt = DateTime.Now; image.Revision++;
            await db.SaveChangesAsync(cancellationToken);
            return new HubImageChunkReceipt(chunk.UploadId, image.NextOffset);
        }, cancellationToken);
    }

    /// <summary>完整大小和摘要再次验证，原子移动后的对象存在且元数据提交才返回完成。</summary>
    public Task<HubImageStoredReceipt> CompleteImageAsync(string connectionId, HubImageComplete complete, CancellationToken cancellationToken) {
        var connection = Connection(connectionId);
        return LockedAsync(connection.Source.SourceInstanceId, async () => {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var image = await AuthorizedImageAsync(connectionId, complete.UploadId, db, cancellationToken);
            if (complete.SourceImageId != image.SourceImageId || complete.SizeBytes != image.SizeBytes
                || complete.ContentSha256 != image.ContentSha256 || image.NextOffset != image.SizeBytes)
                throw new InvalidOperationException("IncompleteImageOrContentConflict");
            var target = ImagePath(image.Key, true); var partial = ImagePath(image.Key, false);
            var path = File.Exists(target) ? target : partial;
            await using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 65536, FileOptions.Asynchronous)) {
                if (file.Length != image.SizeBytes || Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken)) != image.ContentSha256)
                    throw new InvalidOperationException("ImageContentHashMismatch");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (path != target) File.Move(partial, target);
            image.IsStored = true; image.ModifiedAt = DateTime.Now; image.Revision++;
            await db.SaveChangesAsync(cancellationToken);
            return Stored(image);
        }, cancellationToken);
    }

    /// <summary>存储凭据不携带任意文件系统路径，也不冒充外部 Provider 上传。</summary>
    private HubImageStoredReceipt Stored(FusionImageUpload image) => new(_options.HubId, image.SourceInstanceId, image.SourceImageId,
        image.SizeBytes, image.ContentSha256, "LocalDurable", "fusion-images", image.Key);

    /// <summary>受包裹读取权限保护的完整对象流，缺失文件不返回成功凭据。</summary>
    public async Task<(Stream Content, string ContentType)?> ReadImageAsync(string key, CancellationToken cancellationToken) {
        if (!FusionProtocol.IsHex(key, 64)) return null;
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var image = await db.Set<FusionImageUpload>().AsNoTracking().SingleOrDefaultAsync(x => x.Key == key && x.IsStored, cancellationToken);
        if (image is null || !File.Exists(ImagePath(key, true))) return null;
        return (new FileStream(ImagePath(key, true), FileMode.Open, FileAccess.Read, FileShare.Read, 65536,
            FileOptions.Asynchronous | FileOptions.SequentialScan), image.ContentType);
    }

    /// <summary>只清理有界超时临时上传，保留完整对象和图片关联元数据供重新 Begin。</summary>
    public async Task MaintainUploadsAsync(CancellationToken cancellationToken) {
        await using var probe = await _factory.CreateDbContextAsync(cancellationToken);
        var cutoff = DateTime.Now.AddHours(-_options.UploadRetentionHours);
        var expired = await probe.Set<FusionImageUpload>().AsNoTracking().Where(x => !x.IsStored && x.SizeBytes > 0
            && x.NextOffset < x.SizeBytes && x.ModifiedAt < cutoff).OrderBy(x => x.ModifiedAt).Take(100).ToListAsync(cancellationToken);
        foreach (var item in expired) {
            await LockedAsync(item.SourceInstanceId, async () => {
                await using var db = await _factory.CreateDbContextAsync(cancellationToken);
                var image = await db.Set<FusionImageUpload>().AsTracking().SingleAsync(x => x.Key == item.Key, cancellationToken);
                if (image.IsStored || image.ModifiedAt >= cutoff || image.NextOffset == image.SizeBytes) return false;
                var previousUploadId = image.UploadId;
                image.UploadId = null; image.NextOffset = 0; image.ModifiedAt = DateTime.Now; image.Revision++;
                // 先重置持久偏移；清理中断时残留字节会在下一次写入前被安全截断。
                await db.SaveChangesAsync(cancellationToken);
                if (previousUploadId is not null) _uploadConnections.TryRemove(previousUploadId, out _);
                File.Delete(ImagePath(image.Key, false));
                return true;
            }, cancellationToken);
        }
    }
}
