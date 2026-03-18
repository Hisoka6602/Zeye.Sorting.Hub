using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Primitives;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels {

    /// <summary>
    /// 包裹实体（领域层聚合根）
    /// 说明：
    /// 1) 仅包含领域语义与状态；
    /// 2) 主表索引与 decimal 精度通过 EF Core 特征标记就近声明；
    /// 3) 其余持久化映射（表名、架构、关系、影子属性等）在 Infrastructure/EntityConfigurations 中完成。
    /// </summary>
    public sealed class Parcel : AuditableEntity {

        /// <summary>
        /// 包裹时间戳
        /// </summary>
        public long ParcelTimestamp { get; private set; }

        /// <summary>
        /// 包裹类型（例如普通、异常、大件等）
        /// </summary>
        public ParcelType Type { get; private set; } = ParcelType.Normal;

        /// <summary>
        /// 包裹状态（例如待处理、已分拣、异常等）
        /// </summary>
        public ParcelStatus Status { get; private set; }

        /// <summary>
        /// 包裹异常类型（仅当状态为分拣异常时有值）
        /// </summary>
        public ParcelExceptionType? ExceptionType { get; private set; }

        /// <summary>
        /// NoRead 类型
        /// </summary>
        public NoReadType NoReadType { get; private set; } = NoReadType.None;

        /// <summary>
        /// 小车编号（可选）
        /// </summary>
        public int? SorterCarrierId { get; private set; }

        /// <summary>
        /// 三段码
        /// </summary>
        [MaxLength(512)]
        public string? SegmentCodes { get; private set; }

        /// <summary>
        /// 包裹生命周期（毫秒）
        /// </summary>
        public long? LifecycleMilliseconds { get; private set; }

        /// <summary>
        /// 目标格口 Id（系统路由分配的理论落格位置）
        /// </summary>
        public long TargetChuteId { get; private set; }

        /// <summary>
        /// 实际落格 Id（包裹实际到达的格口位置）
        /// </summary>
        public long ActualChuteId { get; private set; }

        /// <summary>
        /// 条码（主条码）
        /// </summary>
        [MaxLength(1024)]
        public string BarCodes { get; private set; } = string.Empty;

        /// <summary>
        /// 重量
        /// </summary>
        [Precision(18, 3)]
        public decimal Weight { get; private set; }

        /// <summary>
        /// 外部接口访问状态
        /// </summary>
        public ApiRequestStatus RequestStatus { get; private set; }

        /// <summary>
        /// 集包号
        /// </summary>
        [MaxLength(128)]
        public string BagCode { get; private set; } = string.Empty;

        /// <summary>
        /// 工作台
        /// </summary>
        [MaxLength(128)]
        public string WorkstationName { get; private set; } = string.Empty;

        /// <summary>
        /// 是否叠包
        /// </summary>
        public bool IsSticking { get; private set; }

        /// <summary>
        /// 长度
        /// </summary>
        [Precision(18, 3)]
        public decimal Length { get; private set; }

        /// <summary>
        /// 宽度
        /// </summary>
        [Precision(18, 3)]
        public decimal Width { get; private set; }

        /// <summary>
        /// 高度
        /// </summary>
        [Precision(18, 3)]
        public decimal Height { get; private set; }

        /// <summary>
        /// 体积
        /// </summary>
        [Precision(18, 3)]
        public decimal Volume { get; private set; }

        /// <summary>
        /// 扫码时间
        /// </summary>
        public DateTime ScannedTime { get; private set; }

        /// <summary>
        /// 落格时间
        /// </summary>
        public DateTime DischargeTime { get; private set; }

        /// <summary>
        /// 包裹完结时间（生命周期结束时间点）
        /// </summary>
        public DateTime? CompletedTime { get; private set; }

        /// <summary>
        /// 是否有图片
        /// </summary>
        public bool HasImages { get; private set; }

        /// <summary>
        /// 是否有视频
        /// </summary>
        public bool HasVideos { get; private set; }

        /// <summary>
        /// 包裹坐标位置（原始字符串表达）
        /// </summary>
        [MaxLength(1024)]
        public string Coordinate { get; private set; } = string.Empty;

        // ------------------------------
        // 关联信息（领域层不使用 virtual，避免把 ORM 行为混入领域模型）
        // 建议仅暴露只读集合，并通过方法维护一致性
        // ------------------------------

        private readonly List<BarCodeInfo> _barCodeInfos = new();
        /// <summary>
        /// 条码明细集合（只读视图）
        /// </summary>
        public IReadOnlyList<BarCodeInfo> BarCodeInfos => _barCodeInfos;

        /// <summary>
        /// 字段：_weightInfos。
        /// </summary>
        private readonly List<WeightInfo> _weightInfos = new();
        /// <summary>
        /// 称重明细集合（只读视图）
        /// </summary>
        public IReadOnlyList<WeightInfo> WeightInfos => _weightInfos;

        /// <summary>
        /// 体积信息（单值对象）
        /// </summary>
        public VolumeInfo? VolumeInfo { get; private set; }

        /// <summary>
        /// 字段：_apiRequests。
        /// </summary>
        private readonly List<ApiRequestInfo> _apiRequests = new();
        /// <summary>
        /// 外部接口请求记录集合（只读视图）
        /// </summary>
        public IReadOnlyList<ApiRequestInfo> ApiRequests => _apiRequests;

        /// <summary>
        /// 格口信息（单值对象）
        /// </summary>
        public ChuteInfo? ChuteInfo { get; private set; }

        /// <summary>
        /// 字段：_commandInfos。
        /// </summary>
        private readonly List<CommandInfo> _commandInfos = new();
        /// <summary>
        /// 通信指令记录集合（只读视图）
        /// </summary>
        public IReadOnlyList<CommandInfo> CommandInfos => _commandInfos;

        /// <summary>
        /// 字段：_imageInfos。
        /// </summary>
        private readonly List<ImageInfo> _imageInfos = new();
        /// <summary>
        /// 图片信息集合（只读视图）
        /// </summary>
        public IReadOnlyList<ImageInfo> ImageInfos => _imageInfos;

        /// <summary>
        /// 字段：_videoInfos。
        /// </summary>
        private readonly List<VideoInfo> _videoInfos = new();
        /// <summary>
        /// 视频信息集合（只读视图）
        /// </summary>
        public IReadOnlyList<VideoInfo> VideoInfos => _videoInfos;

        /// <summary>
        /// 小车信息（单值对象）
        /// </summary>
        public SorterCarrierInfo? SorterCarrierInfo { get; private set; }

        /// <summary>
        /// 集包信息（单值对象）
        /// </summary>
        public BagInfo? BagInfo { get; private set; }

        /// <summary>
        /// 设备信息（单值对象）
        /// </summary>
        public ParcelDeviceInfo? DeviceInfo { get; private set; }

        /// <summary>
        /// 灰度仪判断信息（单值对象）
        /// </summary>
        public GrayDetectorInfo? GrayDetectorInfo { get; private set; }

        /// <summary>
        /// 除叠仪判断信息（单值对象）
        /// </summary>
        public StickingParcelInfo? StickingParcelInfo { get; private set; }

        /// <summary>
        /// 坐标检测信息（单值对象）
        /// </summary>
        public ParcelPositionInfo? ParcelPositionInfo { get; private set; }

        private Parcel() {
            // 说明：保留无参构造，便于持久化层（如 EF Core）构造实体
        }

        /// <summary>
        /// 创建包裹（领域工厂方法）
        /// </summary>
        public static Parcel Create(
            long parcelTimestamp,
            ParcelType type,
            string barCodes,
            decimal weight,
            string workstationName,
            DateTime scannedTime,
            DateTime dischargeTime,
            long targetChuteId,
            long actualChuteId,
            ApiRequestStatus requestStatus,
            string bagCode,
            bool isSticking,
            decimal length,
            decimal width,
            decimal height,
            decimal volume,
            bool hasImages,
            bool hasVideos,
            string coordinate,
            NoReadType noReadType = NoReadType.None,
            int? sorterCarrierId = null,
            string? segmentCodes = null,
            long? lifecycleMilliseconds = null) {
            if (parcelTimestamp <= 0) {
                throw new ArgumentOutOfRangeException(nameof(parcelTimestamp), "包裹时间戳必须大于 0");
            }

            if (string.IsNullOrWhiteSpace(barCodes)) {
                throw new ArgumentException("条码不能为空", nameof(barCodes));
            }

            if (string.IsNullOrWhiteSpace(workstationName)) {
                throw new ArgumentException("工作台不能为空", nameof(workstationName));
            }

            if (targetChuteId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(targetChuteId), "目标 Chute Id 必须大于 0");
            }

            if (actualChuteId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(actualChuteId), "实际 Chute Id 必须大于 0");
            }

            var entity = new Parcel {
                ParcelTimestamp = parcelTimestamp,
                Type = type,
                NoReadType = noReadType,
                SorterCarrierId = sorterCarrierId,
                SegmentCodes = segmentCodes,
                LifecycleMilliseconds = lifecycleMilliseconds,
                TargetChuteId = targetChuteId,
                ActualChuteId = actualChuteId,
                BarCodes = barCodes.Trim(),
                Weight = weight,
                RequestStatus = requestStatus,
                BagCode = bagCode ?? string.Empty,
                WorkstationName = workstationName.Trim(),
                IsSticking = isSticking,
                Length = length,
                Width = width,
                Height = height,
                Volume = volume,
                ScannedTime = scannedTime,
                DischargeTime = dischargeTime,
                HasImages = hasImages,
                HasVideos = hasVideos,
                Coordinate = coordinate ?? string.Empty,
                CreatedTime = DateTime.Now,
            };
            entity.ApplyStatus(ParcelStatus.Pending, null);
            return entity;
        }

        /// <summary>
        /// 标记包裹完结
        /// </summary>
        /// <remarks>
        /// 标记完成时会清空 <see cref="ExceptionType"/>，避免“已完成”与“异常类型”并存的状态歧义。
        /// </remarks>
        public void MarkCompleted(DateTime completedTime) {
            CompletedTime = completedTime;
            ApplyStatus(ParcelStatus.Completed, null);
        }

        /// <summary>
        /// 标记包裹为分拣异常并记录异常类型。
        /// </summary>
        /// <param name="exceptionType">分拣异常的具体类型。</param>
        public void MarkSortingException(ParcelExceptionType exceptionType) {
            if (Status == ParcelStatus.Completed) {
                throw new InvalidOperationException("已完成的包裹不允许再标记为分拣异常");
            }

            if (!Enum.IsDefined(exceptionType)) {
                throw new ArgumentOutOfRangeException(nameof(exceptionType), "异常类型无效");
            }

            ApplyStatus(ParcelStatus.SortingException, exceptionType);
        }

        /// <summary>
        /// 应用状态与异常类型，确保两者领域语义一致。
        /// </summary>
        /// <param name="status">目标状态。</param>
        /// <param name="exceptionType">异常类型。</param>
        private void ApplyStatus(ParcelStatus status, ParcelExceptionType? exceptionType) {
            EnsureStatusExceptionTypeConsistency(status, exceptionType);
            Status = status;
            ExceptionType = exceptionType;
        }

        /// <summary>
        /// 校验状态与异常类型的一致性约束。
        /// </summary>
        /// <param name="status">目标状态。</param>
        /// <param name="exceptionType">异常类型。</param>
        /// <exception cref="InvalidOperationException">状态与异常类型组合无效时抛出。</exception>
        private static void EnsureStatusExceptionTypeConsistency(ParcelStatus status, ParcelExceptionType? exceptionType) {
            if (status == ParcelStatus.SortingException) {
                if (exceptionType is null) {
                    throw new InvalidOperationException("分拣异常状态必须提供异常类型");
                }

                return;
            }

            if (exceptionType is not null) {
                throw new InvalidOperationException("非分拣异常状态不允许包含异常类型");
            }
        }

        /// <summary>
        /// 更新外部接口访问状态
        /// </summary>
        public void UpdateRequestStatus(ApiRequestStatus requestStatus) {
            RequestStatus = requestStatus;
        }

        /// <summary>
        /// 追加条码明细
        /// </summary>
        public void AddBarCodeInfo(BarCodeInfo info) {
            if (info is null) {
                throw new ArgumentNullException(nameof(info), "条码明细不能为空");
            }

            _barCodeInfos.Add(info);
        }

        /// <summary>
        /// 追加称重明细
        /// </summary>
        public void AddWeightInfo(WeightInfo info) {
            if (info is null) {
                throw new ArgumentNullException(nameof(info), "称重明细不能为空");
            }

            _weightInfos.Add(info);
        }

        /// <summary>
        /// 设置体积信息
        /// </summary>
        public void SetVolumeInfo(VolumeInfo info) {
            VolumeInfo = info ?? throw new ArgumentNullException(nameof(info), "体积信息不能为空");
        }

        /// <summary>
        /// 追加接口请求信息
        /// </summary>
        public void AddApiRequest(ApiRequestInfo info) {
            if (info is null) {
                throw new ArgumentNullException(nameof(info), "接口请求信息不能为空");
            }

            _apiRequests.Add(info);
        }

        /// <summary>
        /// 设置格口信息
        /// </summary>
        public void SetChuteInfo(ChuteInfo info) {
            ChuteInfo = info ?? throw new ArgumentNullException(nameof(info), "Chute 信息不能为空");
        }

        /// <summary>
        /// 追加通信指令信息
        /// </summary>
        public void AddCommandInfo(CommandInfo info) {
            if (info is null) {
                throw new ArgumentNullException(nameof(info), "通信指令信息不能为空");
            }

            _commandInfos.Add(info);
        }

        /// <summary>
        /// 追加图片信息
        /// </summary>
        public void AddImageInfo(ImageInfo info) {
            if (info is null) {
                throw new ArgumentNullException(nameof(info), "图片信息不能为空");
            }

            _imageInfos.Add(info);
        }

        /// <summary>
        /// 追加视频信息
        /// </summary>
        public void AddVideoInfo(VideoInfo info) {
            if (info is null) {
                throw new ArgumentNullException(nameof(info), "视频信息不能为空");
            }

            _videoInfos.Add(info);
        }

        /// <summary>
        /// 设置小车信息
        /// </summary>
        public void SetSorterCarrierInfo(SorterCarrierInfo info) {
            SorterCarrierInfo = info ?? throw new ArgumentNullException(nameof(info), "SorterCarrier 信息不能为空");
        }

        /// <summary>
        /// 设置集包信息
        /// </summary>
        public void SetBagInfo(BagInfo info) {
            BagInfo = info ?? throw new ArgumentNullException(nameof(info), "集包信息不能为空");
        }

        /// <summary>
        /// 设置设备信息
        /// </summary>
        public void SetDeviceInfo(ParcelDeviceInfo info) {
            DeviceInfo = info ?? throw new ArgumentNullException(nameof(info), "设备信息不能为空");
        }

        /// <summary>
        /// 设置灰度仪判断信息
        /// </summary>
        public void SetGrayDetectorInfo(GrayDetectorInfo info) {
            GrayDetectorInfo = info ?? throw new ArgumentNullException(nameof(info), "灰度仪判断信息不能为空");
        }

        /// <summary>
        /// 设置除叠仪判断信息
        /// </summary>
        public void SetStickingParcelInfo(StickingParcelInfo info) {
            StickingParcelInfo = info ?? throw new ArgumentNullException(nameof(info), "Sticking 判断信息不能为空");
        }

        /// <summary>
        /// 设置包裹坐标信息
        /// </summary>
        public void SetParcelPositionInfo(ParcelPositionInfo info) {
            ParcelPositionInfo = info ?? throw new ArgumentNullException(nameof(info), "包裹坐标信息不能为空");
        }
    }
}
