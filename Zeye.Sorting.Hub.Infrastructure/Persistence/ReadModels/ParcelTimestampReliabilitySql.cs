using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>在同一物理分表内核对历史时间端点，仅排除明确不可靠的来源事实，不重写已保存快照。</summary>
internal static class ParcelTimestampReliabilitySql {
    /// <summary>按实际EF索引映射定位单票端点，基础表与历史物理表使用相同列序。</summary>
    private static readonly string[] EndpointIndexProperties = [nameof(ParcelProcessingRecord.ParcelId), nameof(ParcelProcessingRecord.OccurredAt),
        nameof(ParcelProcessingRecord.Stage), nameof(ParcelProcessingRecord.HasReliableTimestamp), nameof(ParcelProcessingRecord.SourceInstanceId),
        nameof(ParcelProcessingRecord.SourceRunId), nameof(ParcelProcessingRecord.SourceParcelId), nameof(ParcelProcessingRecord.AttemptNumber),
        nameof(ParcelProcessingRecord.RecordId), nameof(ParcelProcessingRecord.IsSuccess)];

    /// <summary>完成耗时读取仍由数据库筛选与聚合；不存在可靠性标记的历史端点保持兼容。</summary>
    internal static string ReliableLifecycleSource(IEntityType parcel, ISqlGenerationHelper helper, string suffix, string? provider) {
        var facts = parcel.Model.FindEntityType(typeof(ParcelProcessingRecord))!;
        string Column(string name) => QualifiedColumn(parcel, helper, "p", name);
        return $"SELECT p.* FROM {Table(parcel, helper, suffix)} p WHERE "
            + $"{Column(nameof(Parcel.Status))} = {(int)ParcelStatus.Completed} "
            + $"AND {Column(nameof(Parcel.DetectedTime))} IS NOT NULL AND {Column(nameof(Parcel.CompletedTime))} IS NOT NULL "
            + $"AND {Column(nameof(Parcel.CompletedTime))} >= {Column(nameof(Parcel.DetectedTime))} "
            + $"AND {Column(nameof(Parcel.LifecycleMilliseconds))} >= 0 AND "
            + ReliableEndpoints(parcel, facts, helper, suffix, provider, "p", includeCompletion: true);
    }

    /// <summary>检测节拍只核对检测端点，不因其他阶段或其他发生时间的不可靠记录排除包裹。</summary>
    internal static string ReliableDetection(IEntityType parcel, ISqlGenerationHelper helper, string suffix, string? provider, string alias) =>
        ReliableEndpoints(parcel, parcel.Model.FindEntityType(typeof(ParcelProcessingRecord))!, helper, suffix, provider, alias, includeCompletion: false);

    /// <summary>完整来源身份与当前快照端点同时匹配，失败的完成尝试不参与实际完成端点判断。</summary>
    private static string ReliableEndpoints(IEntityType parcel, IEntityType facts, ISqlGenerationHelper helper, string suffix,
        string? provider, string alias, bool includeCompletion) {
        var detection = ReliableEndpoint(parcel, facts, helper, suffix, provider, alias, firstDetection: true);
        // 两个端点各自按 ParcelId + OccurredAt 等值查找，避免 OR 使复合索引退化成逐票读取全部处理事实。
        return includeCompletion
            ? detection + " AND " + ReliableEndpoint(parcel, facts, helper, suffix, provider, alias, firstDetection: false)
            : detection;
    }

    /// <summary>按单票端点、阶段及可靠性索引查找事实，再按完整身份及重试顺序确认端点可靠性。</summary>
    private static string ReliableEndpoint(IEntityType parcel, IEntityType facts, ISqlGenerationHelper helper, string suffix,
        string? provider, string alias, bool firstDetection) {
        string ParcelColumn(string name) => QualifiedColumn(parcel, helper, alias, name);
        string FactColumn(string name) => QualifiedColumn(facts, helper, "f", name);
        var stage = firstDetection ? ParcelProcessingStage.Detected : ParcelProcessingStage.SortingCompleted;
        var endpoint = firstDetection ? nameof(Parcel.DetectedTime) : nameof(Parcel.CompletedTime);
        var success = firstDetection ? ""
            : $"AND ({FactColumn(nameof(ParcelProcessingRecord.IsSuccess))} IS NULL OR {FactColumn(nameof(ParcelProcessingRecord.IsSuccess))} <> 0) ";
        var reliable = $"NOT EXISTS (SELECT 1 FROM {EndpointFactSource(facts, helper, suffix, provider, "f")} WHERE "
            + $"{FactColumn(nameof(ParcelProcessingRecord.ParcelId))} = {ParcelColumn(nameof(Parcel.Id))} "
            + $"AND {FactColumn(nameof(ParcelProcessingRecord.OccurredAt))} = {ParcelColumn(endpoint)} "
            + $"AND {FactColumn(nameof(ParcelProcessingRecord.Stage))} = {(int)stage} {success}"
            + $"AND {EqualIdentity(FactColumn(nameof(ParcelProcessingRecord.SourceInstanceId)), ParcelColumn(nameof(Parcel.SourceInstanceId)), provider)} "
            + $"AND {EqualIdentity(FactColumn(nameof(ParcelProcessingRecord.SourceRunId)), ParcelColumn(nameof(Parcel.SourceRunId)), provider)} "
            + $"AND {FactColumn(nameof(ParcelProcessingRecord.SourceParcelId))} = {ParcelColumn(nameof(Parcel.SourceParcelId))} "
            + $"AND {FactColumn(nameof(ParcelProcessingRecord.HasReliableTimestamp))} = 0 "
            + $"AND {SelectedEndpoint(facts, helper, suffix, provider, firstDetection)})";
        if (provider != DbProviderNames.MySql) return reliable;
        // 该探测与包裹无关，MySQL仅执行一次；全可靠周期不再为每票重复探测空端点。
        var probeIndex = facts.GetIndexes().Single(index => index.Properties.Count >= 2
            && index.Properties[0].Name == nameof(ParcelProcessingRecord.Stage)
            && index.Properties[1].Name == nameof(ParcelProcessingRecord.PartitionTime));
        var probeSource = IndexedFactSource(facts, helper, suffix, "g", probeIndex);
        return $"(NOT EXISTS (SELECT 1 FROM {probeSource} WHERE "
            + $"{QualifiedColumn(facts, helper, "g", nameof(ParcelProcessingRecord.Stage))} = {(int)stage} "
            + $"AND {QualifiedColumn(facts, helper, "g", nameof(ParcelProcessingRecord.HasReliableTimestamp))} = 0) OR {reliable})";
    }

    /// <summary>MySQL固定端点查找索引，避免按发生时间反复回表或大量旧不可靠端点触发整期物化；索引随启动迁移补齐。</summary>
    private static string EndpointFactSource(IEntityType facts, ISqlGenerationHelper helper, string suffix, string? provider, string alias) {
        if (provider != DbProviderNames.MySql) return Table(facts, helper, suffix) + " " + alias;
        var index = facts.GetIndexes().Single(candidate => candidate.Properties.Select(property => property.Name).SequenceEqual(EndpointIndexProperties));
        return IndexedFactSource(facts, helper, suffix, alias, index);
    }

    /// <summary>索引名来自EF实际映射，仅分表后缀由已验证的本地路由追加。</summary>
    private static string IndexedFactSource(IEntityType facts, ISqlGenerationHelper helper, string suffix, string alias, IReadOnlyIndex index) {
        var name = index.GetDatabaseName()! + (suffix.Length == 0 ? "" : "_" + suffix);
        return Table(facts, helper, suffix) + " " + alias + " FORCE INDEX (" + helper.DelimitIdentifier(name) + ")";
    }

    /// <summary>同刻多事实沿用领域重放顺序：检测选最先，完成选最后一个未失败事实。</summary>
    private static string SelectedEndpoint(IEntityType facts, ISqlGenerationHelper helper, string suffix, string? provider, bool firstDetection) {
        string Candidate(string name) => QualifiedColumn(facts, helper, "e", name);
        string Current(string name) => QualifiedColumn(facts, helper, "f", name);
        var comparison = firstDetection ? "<" : ">";
        var stage = firstDetection ? ParcelProcessingStage.Detected : ParcelProcessingStage.SortingCompleted;
        var success = firstDetection ? "" : $"AND ({Candidate(nameof(ParcelProcessingRecord.IsSuccess))} IS NULL OR {Candidate(nameof(ParcelProcessingRecord.IsSuccess))} <> 0) ";
        return $"NOT EXISTS (SELECT 1 FROM {EndpointFactSource(facts, helper, suffix, provider, "e")} WHERE "
            + $"{Candidate(nameof(ParcelProcessingRecord.ParcelId))} = {Current(nameof(ParcelProcessingRecord.ParcelId))} "
            + $"AND {EqualIdentity(Candidate(nameof(ParcelProcessingRecord.SourceInstanceId)), Current(nameof(ParcelProcessingRecord.SourceInstanceId)), provider)} "
            + $"AND {EqualIdentity(Candidate(nameof(ParcelProcessingRecord.SourceRunId)), Current(nameof(ParcelProcessingRecord.SourceRunId)), provider)} "
            + $"AND {Candidate(nameof(ParcelProcessingRecord.SourceParcelId))} = {Current(nameof(ParcelProcessingRecord.SourceParcelId))} "
            + $"AND {Candidate(nameof(ParcelProcessingRecord.Stage))} = {(int)stage} {success}"
            + $"AND {Candidate(nameof(ParcelProcessingRecord.OccurredAt))} = {Current(nameof(ParcelProcessingRecord.OccurredAt))} "
            + $"AND ({Candidate(nameof(ParcelProcessingRecord.AttemptNumber))} {comparison} {Current(nameof(ParcelProcessingRecord.AttemptNumber))} "
            + $"OR ({Candidate(nameof(ParcelProcessingRecord.AttemptNumber))} = {Current(nameof(ParcelProcessingRecord.AttemptNumber))} "
            + $"AND {CompareOrdinal(Candidate(nameof(ParcelProcessingRecord.RecordId)), Current(nameof(ParcelProcessingRecord.RecordId)), comparison, provider)})))";
    }

    /// <summary>来源身份使用大小写精确比较，与写入身份和领域重放的匹配规则保持一致。</summary>
    private static string EqualIdentity(string left, string right, string? provider) => CompareOrdinal(left, right, "=", provider);

    /// <summary>同刻事实的稳定编号排序与完整来源身份比较共用二进制排序规则。</summary>
    private static string CompareOrdinal(string left, string right, string comparison, string? provider) {
        var collation = provider switch {
            DbProviderNames.MySql => "utf8mb4_bin",
            DbProviderNames.SqlServer => "Latin1_General_100_BIN2",
            _ => null
        };
        return collation is null ? $"{left} {comparison} {right}" : $"{left} COLLATE {collation} {comparison} {right} COLLATE {collation}";
    }

    /// <summary>物理表名只来自EF映射和已验证的本地分表后缀。</summary>
    private static string Table(IEntityType entity, ISqlGenerationHelper helper, string suffix) {
        ParcelPartitionStore.ValidateSuffix(suffix);
        return helper.DelimitIdentifier(entity.GetTableName()! + (suffix.Length == 0 ? "" : "_" + suffix), entity.GetSchema());
    }

    /// <summary>端点查询复用实际映射列名，避免不同提供器的标识符和模型列名漂移。</summary>
    private static string QualifiedColumn(IEntityType entity, ISqlGenerationHelper helper, string alias, string property) {
        var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        return alias + "." + helper.DelimitIdentifier(entity.FindProperty(property)!.GetColumnName(store)!);
    }
}
