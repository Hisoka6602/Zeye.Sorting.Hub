using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories.Models;

namespace Zeye.Sorting.Hub.Domain.Repositories {

    /// <summary>
    /// Parcel 聚合仓储契约（第一阶段可落地实现）。
    /// </summary>
    public interface IParcelRepository {
        /// <summary>
        /// 根据主键获取包裹主实体（不包含完整聚合图）。
        /// </summary>
        Task<Parcel?> GetByIdAsync(long id, CancellationToken cancellationToken);

        /// <summary>
        /// 根据主键获取包裹完整聚合详情（包含值对象与集合）。
        /// </summary>
        Task<Parcel?> GetDetailByIdAsync(long id, CancellationToken cancellationToken);

        /// <summary>
        /// 按过滤条件执行分页查询（返回摘要读模型）。
        /// </summary>
        Task<ParcelPageResult<ParcelSummaryReadModel>> GetPagedAsync(
            ParcelQueryFilter filter,
            ParcelPageRequest pageRequest,
            CancellationToken cancellationToken);

        /// <summary>
        /// 按集包号分页查询包裹摘要。
        /// </summary>
        Task<ParcelPageResult<ParcelSummaryReadModel>> GetByBagCodeAsync(
            string bagCode,
            ParcelPageRequest pageRequest,
            CancellationToken cancellationToken);

        /// <summary>
        /// 按工作台分页查询包裹摘要。
        /// </summary>
        Task<ParcelPageResult<ParcelSummaryReadModel>> GetByWorkstationNameAsync(
            string workstationName,
            ParcelPageRequest pageRequest,
            CancellationToken cancellationToken);

        /// <summary>
        /// 按包裹状态分页查询包裹摘要。
        /// </summary>
        Task<ParcelPageResult<ParcelSummaryReadModel>> GetByStatusAsync(
            ParcelStatus status,
            ParcelPageRequest pageRequest,
            CancellationToken cancellationToken);

        /// <summary>
        /// 按实际/目标格口条件分页查询包裹摘要。
        /// </summary>
        Task<ParcelPageResult<ParcelSummaryReadModel>> GetByChuteAsync(
            long? actualChuteId,
            long? targetChuteId,
            ParcelPageRequest pageRequest,
            CancellationToken cancellationToken);

        /// <summary>
        /// 按扫描时间查询前后邻近记录（时间顺序）。
        /// </summary>
        Task<IReadOnlyList<ParcelSummaryReadModel>> GetAdjacentByScannedTimeAsync(
            DateTime scannedTime,
            int beforeCount,
            int afterCount,
            CancellationToken cancellationToken);

        /// <summary>
        /// 新增包裹聚合。
        /// </summary>
        Task AddAsync(Parcel parcel, CancellationToken cancellationToken);

        /// <summary>
        /// 更新包裹聚合。
        /// </summary>
        Task UpdateAsync(Parcel parcel, CancellationToken cancellationToken);

        /// <summary>
        /// 删除包裹聚合。
        /// </summary>
        Task RemoveAsync(Parcel parcel, CancellationToken cancellationToken);

        /// <summary>
        /// 按创建时间删除过期包裹，返回删除条数。
        /// </summary>
        Task<int> RemoveExpiredAsync(DateTime createdBefore, CancellationToken cancellationToken);

        /// <summary>
        /// 批量新增包裹聚合。
        /// </summary>
        Task AddRangeAsync(IReadOnlyCollection<Parcel> parcels, CancellationToken cancellationToken);
    }
}
