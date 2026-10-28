using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>在数据库按单个格口聚合，避免从截断的流向分布推导错误的使用热度。</summary>
internal static class ParcelChuteHeatmapReader {
    /// <summary>排除缺失编码，仅下载有界聚合行；下钻条件不改变热力总体。</summary>
    internal static async Task<ParcelChuteHeatmapResponse> ReadAsync(IQueryable<ParcelAnalysisSnapshot> cohort,
        bool actual, long sampleCount, long parcelCount, int rowLimit, string? sourceCollation, CancellationToken cancellationToken) {
        var cells = await Cells(cohort, actual, sourceCollation)
            .OrderByDescending(cell => cell.Count).ThenBy(cell => cell.SourceInstanceId).ThenBy(cell => cell.WorkstationName)
            .ThenBy(cell => cell.ChuteCode).Take(rowLimit + 1).ToArrayAsync(cancellationToken);
        return new ParcelChuteHeatmapResponse {
            SampleCount = sampleCount, MissingCodeCount = parcelCount - sampleCount,
            Cells = cells.Take(rowLimit).ToArray(), Truncated = cells.Length > rowLimit
        };
    }

    /// <summary>同名格口按二进制来源身份独立聚合，保持与来源筛选及流向统计相同的边界。</summary>
    internal static IQueryable<ParcelChuteHeatmapCellResponse> Cells(IQueryable<ParcelAnalysisSnapshot> cohort, bool actual, string? sourceCollation) {
        var valid = actual
            ? cohort.Where(parcel => parcel.ActualChuteCode != null && parcel.ActualChuteCode.Trim() != "")
            : cohort.Where(parcel => parcel.TargetChuteCode != null && parcel.TargetChuteCode.Trim() != "");
        return valid.GroupBy(parcel => new {
                SourceInstanceId = sourceCollation == null ? parcel.SourceInstanceId : EF.Functions.Collate(parcel.SourceInstanceId!, sourceCollation),
                parcel.WorkstationName,
                ChuteCode = actual ? parcel.ActualChuteCode : parcel.TargetChuteCode
            }).Select(group => new ParcelChuteHeatmapCellResponse {
                SourceInstanceId = group.Key.SourceInstanceId, WorkstationName = group.Key.WorkstationName,
                ChuteCode = group.Key.ChuteCode!, Count = group.LongCount(),
                MismatchCount = group.LongCount(parcel => parcel.TargetChuteCode != null && parcel.TargetChuteCode.Trim() != ""
                    && parcel.ActualChuteCode != null && parcel.ActualChuteCode.Trim() != ""
                    && DatabaseTextFunctions.Lower(parcel.TargetChuteCode.Trim()) != DatabaseTextFunctions.Lower(parcel.ActualChuteCode.Trim())),
                FallbackCount = group.LongCount(parcel => parcel.IsFallbackChuteAssigned == true)
            });
    }
}
