using System.Text.Json;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>工作台登记及编辑请求。</summary>
public sealed record FusionSourceWrite(string SourceInstanceId, string WorkstationName, bool Enabled,
    string TenantId, string StoragePartitionId, string LineId, string? SiteCode, string? DeviceCode, string TimeZoneId) {
    /// <summary>转换工作台字段并规范可选编码。</summary>
    public FusionSourceOptions ToOptions() => new() { SourceInstanceId = SourceInstanceId ?? "", WorkstationName = WorkstationName ?? "",
        Enabled = Enabled, TenantId = TenantId ?? "", StoragePartitionId = StoragePartitionId ?? "", LineId = LineId ?? "",
        SiteCode = FusionConfigurationService.Optional(SiteCode), DeviceCode = FusionConfigurationService.Optional(DeviceCode), TimeZoneId = TimeZoneId ?? "" };
}
