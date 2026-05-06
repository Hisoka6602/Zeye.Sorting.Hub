namespace Zeye.Sorting.Hub.Infrastructure.Persistence.QueryGovernance;

/// <summary>
/// 查询模板注册表。
/// </summary>
public sealed class QueryTemplateRegistry {
    /// <summary>
    /// 已登记模板列表。
    /// </summary>
    private readonly IReadOnlyList<QueryTemplateDescriptor> _registeredTemplates;

    /// <summary>
    /// 按模板名称建立的索引。
    /// </summary>
    private readonly IReadOnlyDictionary<string, QueryTemplateDescriptor> _templatesByName;

    /// <summary>
    /// 初始化查询模板注册表。
    /// </summary>
    public QueryTemplateRegistry() {
        _registeredTemplates = CreateRegisteredTemplates();
        _templatesByName = _registeredTemplates.ToDictionary(static item => item.TemplateName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取全部已登记模板。
    /// </summary>
    /// <returns>模板集合。</returns>
    public IReadOnlyList<QueryTemplateDescriptor> GetAll() {
        return _registeredTemplates;
    }

    /// <summary>
    /// 按模板名称查找模板。
    /// </summary>
    /// <param name="templateName">模板名称。</param>
    /// <param name="descriptor">模板描述。</param>
    /// <returns>是否命中。</returns>
    public bool TryGet(string templateName, out QueryTemplateDescriptor? descriptor) {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

        if (_templatesByName.TryGetValue(templateName, out var match)) {
            descriptor = match;
            return true;
        }

        descriptor = null;
        return false;
    }

    /// <summary>
    /// 创建当前阶段必须登记的模板集合。
    /// </summary>
    /// <returns>模板集合。</returns>
    private static IReadOnlyList<QueryTemplateDescriptor> CreateRegisteredTemplates() {
        return [
            CreateDescriptor(
                templateName: "ParcelRecentCursorQuery",
                purpose: "Parcel 高频只读列表与游标翻页基线查询。",
                serviceName: "GetParcelCursorPagedQueryService",
                tableNames: ["Parcels"],
                filterColumns: ["ScannedTime", "BarCodes", "BagCode", "WorkstationName", "Status", "ExceptionType", "ActualChuteId", "TargetChuteId"],
                sortColumns: ["ScannedTime", "Id"],
                recommendedIndexes: ["Parcels(ScannedTime DESC, Id DESC)"],
                maxTimeRangeHours: 24,
                isCountAllowed: false,
                isDeepPagingAllowed: false),
            CreateDescriptor(
                templateName: "ParcelByBarcodeQuery",
                purpose: "按条码关键字检索 Parcel 摘要。",
                serviceName: "GetParcelPagedQueryService",
                tableNames: ["Parcels"],
                filterColumns: ["BarCodes", "ScannedTime"],
                sortColumns: ["ScannedTime", "Id"],
                recommendedIndexes: ["Parcels(BarCodes, ScannedTime DESC, Id DESC)"],
                maxTimeRangeHours: 24,
                isCountAllowed: false,
                isDeepPagingAllowed: false),
            CreateDescriptor(
                templateName: "ParcelByChuteQuery",
                purpose: "按实际格口或目标格口检索 Parcel 摘要。",
                serviceName: "GetParcelPagedQueryService",
                tableNames: ["Parcels"],
                filterColumns: ["ActualChuteId", "TargetChuteId", "ScannedTime"],
                sortColumns: ["ScannedTime", "Id"],
                recommendedIndexes: ["Parcels(ActualChuteId, TargetChuteId, ScannedTime DESC, Id DESC)"],
                maxTimeRangeHours: 24,
                isCountAllowed: false,
                isDeepPagingAllowed: false),
            CreateDescriptor(
                templateName: "ParcelByWorkstationQuery",
                purpose: "按工作台检索 Parcel 摘要。",
                serviceName: "GetParcelPagedQueryService",
                tableNames: ["Parcels"],
                filterColumns: ["WorkstationName", "ScannedTime"],
                sortColumns: ["ScannedTime", "Id"],
                recommendedIndexes: ["Parcels(WorkstationName, ScannedTime DESC, Id DESC)"],
                maxTimeRangeHours: 24,
                isCountAllowed: false,
                isDeepPagingAllowed: false),
            CreateDescriptor(
                templateName: "WebRequestAuditLogCursorQuery",
                purpose: "按时间窗口与条件过滤检索 Web 请求审计日志。",
                serviceName: "GetWebRequestAuditLogPagedQueryService",
                tableNames: ["WebRequestAuditLogs"],
                filterColumns: ["StartedAt", "StatusCode", "IsSuccess", "TraceId", "CorrelationId", "RequestPath"],
                sortColumns: ["StartedAt", "Id"],
                recommendedIndexes: ["WebRequestAuditLogs(StartedAt DESC, Id DESC)"],
                maxTimeRangeHours: 168,
                isCountAllowed: true,
                isDeepPagingAllowed: false),
            CreateDescriptor(
                templateName: "ArchiveTaskListQuery",
                purpose: "分页检索归档任务与治理结果。",
                serviceName: "GetArchiveTaskPagedQueryService",
                tableNames: ["ArchiveTasks"],
                filterColumns: ["Status", "TaskType", "CreatedAt"],
                sortColumns: ["CreatedAt", "Id"],
                recommendedIndexes: ["ArchiveTasks(Status, TaskType, CreatedAt DESC, Id DESC)"],
                maxTimeRangeHours: 720,
                isCountAllowed: true,
                isDeepPagingAllowed: false)
        ];
    }

    /// <summary>
    /// 创建模板描述实例。
    /// </summary>
    /// <param name="templateName">模板名称。</param>
    /// <param name="purpose">业务用途。</param>
    /// <param name="serviceName">应用服务名称。</param>
    /// <param name="tableNames">表名列表。</param>
    /// <param name="filterColumns">过滤字段列表。</param>
    /// <param name="sortColumns">排序字段列表。</param>
    /// <param name="recommendedIndexes">建议索引列表。</param>
    /// <param name="maxTimeRangeHours">最大时间范围（小时）。</param>
    /// <param name="isCountAllowed">是否允许 Count。</param>
    /// <param name="isDeepPagingAllowed">是否允许深分页。</param>
    /// <returns>模板描述。</returns>
    private static QueryTemplateDescriptor CreateDescriptor(
        string templateName,
        string purpose,
        string serviceName,
        IReadOnlyList<string> tableNames,
        IReadOnlyList<string> filterColumns,
        IReadOnlyList<string> sortColumns,
        IReadOnlyList<string> recommendedIndexes,
        int maxTimeRangeHours,
        bool isCountAllowed,
        bool isDeepPagingAllowed) {
        if (tableNames.Count == 0 || tableNames.Any(string.IsNullOrWhiteSpace)) {
            throw new InvalidOperationException($"查询模板 {templateName} 至少需要声明一个有效表名。");
        }

        return new QueryTemplateDescriptor {
            TemplateName = templateName,
            Purpose = purpose,
            ServiceName = serviceName,
            TableNames = tableNames,
            FilterColumns = filterColumns,
            SortColumns = sortColumns,
            RecommendedIndexes = recommendedIndexes,
            MaxTimeRangeHours = maxTimeRangeHours,
            IsCountAllowed = isCountAllowed,
            IsDeepPagingAllowed = isDeepPagingAllowed
        };
    }
}
