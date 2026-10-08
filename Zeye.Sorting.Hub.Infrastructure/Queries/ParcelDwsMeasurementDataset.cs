using Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>只缓存去重后的窄量测样本，筛选、阈值和参考值不会污染快照。</summary>
internal sealed record ParcelDwsMeasurementDataset(DateTime GeneratedAt, IReadOnlyList<DwsMeasurementSample> Samples,
    int MissingIdentityCount, int ConflictingMeasurementCount, int MissingBarcodeCount, int DuplicateRecordCount);
