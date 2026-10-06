using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Workbench;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>数据库内聚合全部窗口记录，向页面只返回工作台汇总，不传输海量包裹。</summary>
public sealed class ParcelWorkbenchReadService(IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions) : IParcelWorkbenchReadService {
    /// <summary>扫码时间可以晚于首次入库；查询所有已登记物理表，避免误裁剪迟到测量。</summary>
    public async Task<ParcelWorkbenchResponse> GetAsync(DateTime nowLocal, CancellationToken cancellationToken) {
        if (nowLocal <= DateTime.MinValue.AddDays(1) || nowLocal.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified))
            throw new ArgumentException("统计终点必须是有效本地时间。", nameof(nowLocal));
        var from = nowLocal.AddHours(-24);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var suffixes = await partitions.GetReadSuffixesAsync(cancellationToken);
        await using var read = ParcelPartitionReadContext<ParcelWorkbenchSnapshot>.Create<Parcel>(db, suffixes);
        var parcels = read.Query(
            suffixes, nameof(Parcel.ScannedTime), from, nowLocal, includeEnd: true);
        var rows = await parcels.GroupBy(x => new {
            // 来源身份与接入认证一样区分大小写，避免数据库默认排序规则合并不同 Fusion。
            SourceInstanceId = x.SourceInstanceId == null || x.SourceInstanceId.Trim() == "" ? null
                : db.Database.ProviderName == DbProviderNames.MySql ? EF.Functions.Collate(x.SourceInstanceId.Trim(), "utf8mb4_bin")
                : db.Database.ProviderName == DbProviderNames.SqlServer ? EF.Functions.Collate(x.SourceInstanceId.Trim(), "Latin1_General_100_BIN2")
                : x.SourceInstanceId.Trim(),
            LegacyName = x.SourceInstanceId == null || x.SourceInstanceId.Trim() == "" ? x.WorkstationName.Trim() : ""
        }).Select(group => new ParcelWorkstationSummary {
            SourceInstanceId = group.Key.SourceInstanceId,
            WorkstationName = (group.Key.SourceInstanceId == null ? group.Key.LegacyName
                : group.OrderByDescending(x => x.CreatedTime).ThenByDescending(x => x.Id).Select(x => x.WorkstationName).FirstOrDefault())
                ?? group.Key.SourceInstanceId ?? "",
            ParcelCount = group.LongCount(),
            PendingCount = group.LongCount(x => x.Status == ParcelStatus.Pending),
            CompletedCount = group.LongCount(x => x.Status == ParcelStatus.Completed),
            ExceptionCount = group.LongCount(x => x.Status == ParcelStatus.SortingException),
            OtherCount = group.LongCount(x => x.Status != ParcelStatus.Pending && x.Status != ParcelStatus.Completed && x.Status != ParcelStatus.SortingException),
            LastParcelAt = group.Max(x => (DateTime?)x.CreatedTime)
        }).ToListAsync(cancellationToken);
        var unassigned = rows.Where(x => x.SourceInstanceId is null && string.IsNullOrWhiteSpace(x.WorkstationName)).Sum(x => x.ParcelCount);
        return new() {
            WindowStartLocal = from, WindowEndLocal = nowLocal, ParcelCount = rows.Sum(x => x.ParcelCount), UnassignedCount = unassigned,
            Workstations = rows.Where(x => x.SourceInstanceId is not null || !string.IsNullOrWhiteSpace(x.WorkstationName))
                .OrderByDescending(x => x.LastParcelAt).ThenBy(x => x.SourceInstanceId, StringComparer.Ordinal)
                .ThenBy(x => x.WorkstationName, StringComparer.Ordinal).ToArray()
        };
    }
}
