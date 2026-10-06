using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;

namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>已经耐久提交且被当前投影工作者认领的包裹用例。</summary>
/// <param name="Key">接收凭据键。</param>
/// <param name="ClaimId">原子认领身份。</param>
/// <param name="Request">原有包裹处理用例输入。</param>
public sealed record FusionProjectionItem(string Key, string ClaimId, ParcelProcessingRecordRequest Request);
