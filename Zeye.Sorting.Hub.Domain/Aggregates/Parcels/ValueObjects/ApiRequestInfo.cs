using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 外部接口请求记录（值对象）
    /// 说明：
    /// 1) 属于 Parcel 聚合的属性集合
    /// 2) ORM 映射与独立表结构由 Infrastructure 层负责
    /// </summary>
    public sealed record class ApiRequestInfo {
        /// <summary>
        /// 接口类型（请求格口、锁格、解锁、落格报告、推送图片、扫描等）
        /// </summary>
        public required ApiRequestType ApiType { get; init; }

        /// <summary>
        /// 请求状态（未访问、成功、失败）
        /// </summary>
        public required ApiRequestStatus RequestStatus { get; init; }

        /// <summary>
        /// 请求地址
        /// </summary>
        [MaxLength(512)]
        public required string RequestUrl { get; init; }

        /// <summary>
        /// 参数（URL 或 Query 参数）
        /// </summary>
        [MaxLength(1024)]
        public string QueryParams { get; init; } = string.Empty;

        /// <summary>
        /// 协议头
        /// </summary>
        [MaxLength(2048)]
        public string Headers { get; init; } = string.Empty;

        /// <summary>
        /// 请求内容（原始请求体）
        /// </summary>
        public string RequestBody { get; init; } = string.Empty;

        /// <summary>
        /// 响应内容（原始响应体）
        /// </summary>
        public string ResponseBody { get; init; } = string.Empty;

        /// <summary>
        /// 请求时间
        /// </summary>
        public required DateTime RequestTime { get; init; }

        /// <summary>
        /// 响应时间（为空表示尚未获得响应）
        /// </summary>
        public DateTime? ResponseTime { get; init; }

        /// <summary>
        /// 耗时（毫秒）
        /// </summary>
        public int ElapsedMilliseconds { get; init; }

        /// <summary>
        /// 异常信息（如发生异常则记录）
        /// </summary>
        [MaxLength(2048)]
        public string Exception { get; init; } = string.Empty;

        /// <summary>
        /// 直接可访问的原始数据
        /// </summary>
        public string RawData { get; init; } = string.Empty;

        /// <summary>
        /// 格式化后的业务消息（如用于界面展示的提示文本）
        /// </summary>
        [MaxLength(1024)]
        public string FormattedMessage { get; init; } = string.Empty;

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess => RequestStatus == ApiRequestStatus.Success;
    }
}
