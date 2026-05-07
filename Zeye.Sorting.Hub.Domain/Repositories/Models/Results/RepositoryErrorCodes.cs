namespace Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

/// <summary>
/// 仓储层稳定错误码定义。
/// </summary>
public static class RepositoryErrorCodes {
    /// <summary>
    /// Parcel 主键冲突错误码。
    /// </summary>
    public const string ParcelIdConflict = "Parcel.Id.Conflict";

    /// <summary>
    /// 幂等记录唯一键冲突错误码。
    /// </summary>
    public const string IdempotencyRecordConflict = "Idempotency.Record.Conflict";

    /// <summary>
    /// Inbox 消息唯一键冲突错误码。
    /// </summary>
    public const string InboxMessageConflict = "Inbox.Message.Conflict";
}
