using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Tools.BusinessDataSimulator;

/// <summary>一票包裹及其不可变事实、分类和附属数据。</summary>
public sealed record SimulationParcel(Parcel Parcel, ParcelProcessingRecord[] Records, ParcelType Kind, NoReadType NoRead, long CarrierId, bool Sticking);
