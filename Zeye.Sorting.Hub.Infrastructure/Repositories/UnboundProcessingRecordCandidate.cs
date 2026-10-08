namespace Zeye.Sorting.Hub.Infrastructure.Repositories;

/// <summary>未关联记录的窄查询候选，先选择全局最新主键再读取完整报文。</summary>
/// <param name="Key">不可变处理记录主键。</param>
/// <param name="RecordedAt">本地入库时间。</param>
/// <param name="Suffix">实际物理分表后缀；空值对应历史基础表。</param>
internal sealed record UnboundProcessingRecordCandidate(string Key, DateTime RecordedAt, string Suffix);
