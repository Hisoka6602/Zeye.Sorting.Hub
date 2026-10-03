namespace Zeye.Sorting.Hub.Tools.BusinessDataSimulator;

/// <summary>固定批次时钟，重跑不会改变身份、历史或当前日数据。</summary>
public sealed record SimulationOptions(int Days, int Count, DateTime AsOf, string PublicBaseUrl) {
    /// <summary>凌晨前五分钟使用最近一个完整日，避免生成未来时间。</summary>
    public DateTime End => AsOf.TimeOfDay < TimeSpan.FromMinutes(5) ? AsOf.Date.AddDays(-1) : AsOf.Date;
    /// <summary>含首尾日期的窗口。</summary>
    public DateTime Start => End.AddDays(1 - Days);
    /// <summary>与数量无关，防止同日期范围换参数后覆盖既有批次。</summary>
    public string BatchKey => $"simulation-business-v1-{Start:yyyyMMdd}-{End:yyyyMMdd}";
    /// <summary>限制本地造数规模及时间语义。</summary>
    public void Validate() {
        if (Days is < 1 or > 90 || Count < Days * 20 || Count > Math.Min(100000, Days * 5000))
            throw new ArgumentException("模拟窗口为1至90天，每天至少20票，总量不超过10万票。");
        if (AsOf.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified) || AsOf == default)
            throw new ArgumentException("批次时钟必须是本地时间。");
        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !uri.IsLoopback || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new ArgumentException("示例图片地址仅允许无凭据的本机HTTP地址。");
    }
}
