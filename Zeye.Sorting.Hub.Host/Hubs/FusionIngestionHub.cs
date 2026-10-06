using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Zeye.Sorting.Hub.Application.Abstractions.Integrations;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Host.Authentication;

namespace Zeye.Sorting.Hub.Host.Hubs;

/// <summary>Fusion 1.0 的六个独立机器调用入口，不接受网页账号权限作为机器凭据。</summary>
[Authorize(AuthenticationSchemes = FusionMachineAuthenticationHandler.SchemeName)]
public sealed class FusionIngestionHub(IFusionIngestionGateway ingress, RealtimeResourceSignal changes, Zeye.Sorting.Hub.Host.Queries.FusionConfigurationService configuration) : Microsoft.AspNetCore.SignalR.Hub {
    /// <summary>入口异常日志，错误返回不暴露异常详情或机器凭据。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>允许返回给机器客户端的稳定错误编码。</summary>
    private static readonly HashSet<string> Codes = new(StringComparer.Ordinal) {
        "InvalidLease", "RegistrationMismatch", "SourceInstanceAlreadyConnected", "InvalidBatchLimits",
        "InvalidHeartbeatCounters", "HeartbeatCountersRegressed", "InvalidProtocolTimestamp", "InvalidImageDescriptor",
        "ImageContentConflict", "PendingImagesLimit", "InvalidUploadLease", "InvalidImageChunk", "InvalidImageChunkHash",
        "InvalidImageOffset", "ImageChunkConflict", "IncompleteImageOrContentConflict", "ImageContentHashMismatch"
    };
    /// <summary>统一记录异常并使用稳定错误编码，成功调用唤醒现有前端实时订阅。</summary>
    private async Task<T> InvokeAsync<T>(Func<Task<T>> action) {
        try { var response = await action(); changes.Notify(); return response; }
        catch (Exception exception) when (exception is not OperationCanceledException) {
            Logger.Warn(exception, "Fusion 接入调用失败，Connection={Connection}", Context.ConnectionId);
            throw new HubException(Codes.Contains(exception.Message) ? exception.Message : "FusionRequestFailed");
        }
    }
    /// <summary>验证当前机器身份与登记信息，返回租约和限额。</summary>
    public Zeye.Sorting.Hub.Host.Queries.FusionConfigurationCheck CheckFusionConfiguration(Zeye.Sorting.Hub.Host.Queries.FusionConfigurationProbe probe) =>
        configuration.Check(Context.User!.FindFirstValue(FusionMachineAuthenticationHandler.SourceClaim)!, probe);

    /// <summary>验证机器登记身份并创建正式投递租约。</summary>
    public Task<FusionRegistration> RegisterFusion(FusionHello hello) => InvokeAsync(() =>
        ingress.RegisterAsync(Context.ConnectionId, Context.User!.FindFirstValue(FusionMachineAuthenticationHandler.SourceClaim)!,
            hello ?? throw new ArgumentException("RegistrationMismatch"), Context.ConnectionAborted));
    /// <summary>保存不可变事实和可恢复投影任务后返回逐条确认。</summary>
    public Task<HubBatchReceipt> PublishFacts(HubFactBatch batch) => InvokeAsync(() =>
        ingress.PublishAsync(Context.ConnectionId, batch ?? throw new ArgumentException("InvalidBatchLimits"), Context.ConnectionAborted));
    /// <summary>保存当前来源心跳及累计缓存舍弃指标。</summary>
    public Task<HubHeartbeatReceipt> Heartbeat(FusionHeartbeat heartbeat) => InvokeAsync(() =>
        ingress.HeartbeatAsync(Context.ConnectionId, heartbeat ?? throw new ArgumentException("InvalidHeartbeatCounters"), Context.ConnectionAborted));
    /// <summary>校验图片身份并恢复耐久偏移。</summary>
    public Task<HubImageBeginReceipt> BeginImageUpload(HubImageDescriptor descriptor) => InvokeAsync(() =>
        ingress.BeginImageAsync(Context.ConnectionId, descriptor ?? throw new ArgumentException("InvalidImageDescriptor"), Context.ConnectionAborted));
    /// <summary>校验并刷盘图片分块后确认新偏移。</summary>
    public Task<HubImageChunkReceipt> UploadImageChunk(HubImageChunk chunk) => InvokeAsync(() =>
        ingress.WriteImageAsync(Context.ConnectionId, chunk ?? throw new ArgumentException("InvalidImageChunk"), Context.ConnectionAborted));
    /// <summary>校验完整对象存在及内容摘要后确认存储。</summary>
    public Task<HubImageStoredReceipt> CompleteImageUpload(HubImageComplete image) => InvokeAsync(() =>
        ingress.CompleteImageAsync(Context.ConnectionId, image ?? throw new ArgumentException("IncompleteImageOrContentConflict"), Context.ConnectionAborted));
    /// <summary>释放断开的机器连接，不影响其他来源租约。</summary>
    public override async Task OnDisconnectedAsync(Exception? exception) {
        try { await ingress.DisconnectAsync(Context.ConnectionId, CancellationToken.None); changes.Notify(); }
        catch (Exception failure) { Logger.Error(failure, "Fusion 断线租约释放失败。"); }
        if (exception is not null) Logger.Warn(exception, "Fusion 连接断开。");
        await base.OnDisconnectedAsync(exception);
    }
}
