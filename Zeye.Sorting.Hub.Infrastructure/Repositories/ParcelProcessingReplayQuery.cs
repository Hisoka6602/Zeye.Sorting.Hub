using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories;

/// <summary>两个写入口共用的完整历史窄投影，重放不搬运原始报文、请求正文或上传描述。</summary>
internal static class ParcelProcessingReplayQuery {
    /// <summary>只投影领域派生与规则实际读取的字段，规则正文保留完整值，不按展示预算截断。</summary>
    private static readonly Expression<Func<ParcelProcessingRecord, ParcelProcessingRecord>> Projection = row => new() {
        Key = row.Key, RecordId = row.RecordId, ParcelId = row.ParcelId,
        SourceInstanceId = row.SourceInstanceId, SourceRunId = row.SourceRunId, SourceParcelId = row.SourceParcelId,
        WorkstationName = row.WorkstationName, Stage = row.Stage, OccurredAt = row.OccurredAt,
        RecordedAt = row.RecordedAt, PartitionTime = row.PartitionTime, IsSuccess = row.IsSuccess,
        AttemptNumber = row.AttemptNumber, HasReliableTimestamp = row.HasReliableTimestamp,
        Barcode = row.Barcode,
        BarcodesJson = row.Stage == ParcelProcessingStage.Detected || row.Stage == ParcelProcessingStage.DwsBound
            || row.Stage == ParcelProcessingStage.ScanUploaded || row.Stage == ParcelProcessingStage.ParcelException ? row.BarcodesJson : null,
        WeightGrams = row.WeightGrams, LengthMm = row.LengthMm, WidthMm = row.WidthMm, HeightMm = row.HeightMm,
        VolumeMm3 = row.VolumeMm3, VolumetricWeightGrams = row.VolumetricWeightGrams, MeasuredAt = row.MeasuredAt,
        TaskCode = row.TaskCode, TargetChuteCode = row.TargetChuteCode, ActualChuteCode = row.ActualChuteCode,
        IsFallback = row.IsFallback, IsRoutingBlocked = row.IsRoutingBlocked, IsSpacingViolation = row.IsSpacingViolation,
        ExceptionCode = row.ExceptionCode,
        ErrorMessage = row.Stage == ParcelProcessingStage.Detected || row.Stage == ParcelProcessingStage.DwsBound
            || row.Stage == ParcelProcessingStage.ScanUploaded || row.Stage == ParcelProcessingStage.ParcelException ? row.ErrorMessage : null,
        ResponseBody = row.Stage == ParcelProcessingStage.ScanUploaded ? row.ResponseBody : null,
        Provider = row.Stage == ParcelProcessingStage.ScanUploaded ? row.Provider : null,
        ResponseStatusCode = row.Stage == ParcelProcessingStage.ScanUploaded ? row.ResponseStatusCode : null
    };

    /// <summary>在包裹所属物理上下文内读取全部历史，保留晚到事实重放，不加载无用LOB。</summary>
    internal static Task<List<ParcelProcessingRecord>> ReadAsync(SortingHubDbContext db, long parcelId, CancellationToken cancellationToken) =>
        BuildQuery(db, parcelId).ToListAsync(cancellationToken);

    /// <summary>显式SELECT允许四库翻译回归检查，原始事实的写入与详情读取不使用该投影。</summary>
    internal static IQueryable<ParcelProcessingRecord> BuildQuery(SortingHubDbContext db, long parcelId) =>
        db.Set<ParcelProcessingRecord>().AsNoTracking().Where(row => row.ParcelId == parcelId).Select(Projection);
}
