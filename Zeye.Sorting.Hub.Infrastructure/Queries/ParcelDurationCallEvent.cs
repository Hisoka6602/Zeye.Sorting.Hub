using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>一次调用诊断的轻量元数据，解析后立即释放报文正文和临时字典。</summary>
internal sealed class ParcelDurationCallEvent {
    /// <summary>已去除正文的标量记录。</summary>
    public ParcelDurationFactSnapshot Record { get; }
    /// <summary>是否为实际HTTP交互，不把业务调用诊断重复计为HTTP请求。</summary>
    public bool Transport { get; }
    /// <summary>来源操作身份。</summary>
    public string OperationId { get; }
    /// <summary>来源尝试身份。</summary>
    public string AttemptId { get; }
    /// <summary>明确的调用状态。</summary>
    public string Outcome { get; }
    /// <summary>业务Provider。</summary>
    public string Provider { get; }
    /// <summary>明确业务结果，HTTP成功不能替代业务接受。</summary>
    public bool? Success { get; }
    /// <summary>业务分类，不能从请求正文或地址猜测。</summary>
    public string Type { get; }
    /// <summary>来源明确记录配置禁用了扫描上传。</summary>
    public bool Skipped { get; }

    /// <summary>提取必要字段，不在大规模样本缓存中保留JSON和请求正文。</summary>
    public ParcelDurationCallEvent(ParcelDurationFactSnapshot record, Dictionary<string, string> metadata) {
        string Get(string name) => metadata.GetValueOrDefault(name) ?? string.Empty;
        Record = record with { RawPayload = null, ErrorMessage = null,
            AttemptNumber = int.TryParse(Get("attemptNumber"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var attempt) && attempt > 0 ? attempt : record.AttemptNumber };
        Transport = Get("kind") == "provider-call" || Get("outcomeLevel") == "transport";
        OperationId = Get("operationId"); AttemptId = Get("attemptId");
        Outcome = Get("outcome").Length > 0 ? Get("outcome") : Get("status");
        Provider = record.Provider ?? (Get("kind") == "provider-attempt" ? Get("category") : Transport ? Get("name") : Get("provider"));
        Success = record.IsSuccess ?? (Get("businessAccepted") == "true" || Outcome == "accepted" ? true
            : Get("businessAccepted") == "false" || Outcome == "failed" ? false : null);
        var text = (Get("operation").Length > 0 ? Get("operation") : Transport ? Get("category") : Get("name")).ToLowerInvariant();
        Type = text.Contains("格口") || text.Contains("chute-assignment") || text.Contains("chute_assignment") || text.Contains("request-chute") ? "chute-request"
            : text.Contains("扫描上传") || text.Contains("scan-upload") || text.Contains("scan_upload") || text.Contains("scan-result") ? "scan-upload"
            : text.Contains("落格") || text.Contains("landing") || text.Contains("discharge-report") || record.Stage == ParcelProcessingStage.LandingReported ? "landing-report"
            : text.Contains("图片") || text.Contains("image-upload") || record.Stage == ParcelProcessingStage.ImageUploaded ? "image-upload" : "other-api";
        Skipped = record.RawPayload?.Contains("Scan upload disabled by config.", StringComparison.Ordinal) == true
            || record.ErrorMessage?.Contains("Scan upload disabled by config.", StringComparison.Ordinal) == true;
    }

    /// <summary>直接使用耐久分类，不在报表请求上解析任何历史 JSON。</summary>
    public ParcelDurationCallEvent(ParcelDurationFact fact) {
        Record = new() {
            RecordId = fact.RecordId, ParcelId = fact.ParcelId, SourceInstanceId = fact.SourceInstanceId,
            SourceRunId = fact.SourceRunId, SourceParcelId = fact.SourceParcelId, PartitionTime = fact.PartitionTime,
            OccurredAt = fact.OccurredAt, Stage = fact.Stage, HasReliableTimestamp = fact.HasReliableTimestamp,
            AttemptNumber = fact.AttemptNumber, Provider = fact.Provider, RequestUrl = fact.RequestUrl,
            RequestAt = fact.RequestAt, ResponseAt = fact.ResponseAt, ElapsedMilliseconds = fact.ElapsedMilliseconds, IsSuccess = fact.IsSuccess
        };
        Transport = fact.Transport; OperationId = fact.OperationId; AttemptId = fact.AttemptId;
        Outcome = fact.Outcome; Provider = fact.Provider; Success = fact.IsSuccess; Type = fact.Type; Skipped = fact.Skipped;
    }
}
