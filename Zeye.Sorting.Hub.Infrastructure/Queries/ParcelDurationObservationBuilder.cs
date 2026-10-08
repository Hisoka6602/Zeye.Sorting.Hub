using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>前后台共用唯一调用归并口径：窗口诊断去重，独立 HTTP 交互、失败和重试分别保留。</summary>
internal static class ParcelDurationObservationBuilder {
    /// <summary>只归并完整来源身份相同的诊断，未知和无效耗时仍输出一个可计数观察。</summary>
    internal static IReadOnlyList<ParcelDurationCall> Build(IReadOnlyList<ParcelDurationCallEvent> events) {
        var result = new List<ParcelDurationCall>();
        var operations = events.Where(item => !item.Transport && item.OperationId.Length > 0)
            .GroupBy(item => (item.Record.ParcelId, item.Record.SourceInstanceId, item.Record.SourceRunId, item.Record.SourceParcelId, item.OperationId, item.AttemptId, item.Record.AttemptNumber))
            .Select(group => new ParcelDurationCallWindow(group.OrderBy(item => item.Record.OccurredAt).ThenBy(item => item.Record.RecordId).ToArray())).ToArray();
        var windowsByParcel = operations.ToLookup(window => window.Anchor.Record.ParcelId);
        var used = new HashSet<ParcelDurationCallWindow>();
        foreach (var item in events.Where(item => item.Transport)) {
            var matches = windowsByParcel[item.Record.ParcelId].Where(window => window.Contains(item)).Take(2).ToArray();
            var window = matches.Length == 1 ? matches[0] : null;
            if (window is not null) used.Add(window);
            var row = item.Record;
            var valid = ParcelDurationAnalysisReader.TryDuration(row.RequestAt, row.ResponseAt, row.ElapsedMilliseconds, row.HasReliableTimestamp, out var elapsed, out var source);
            result.Add(ParcelDurationCall.Create(window?.Anchor.Type ?? item.Type, "fact:" + row.RecordId, row, row.RequestAt, row.ResponseAt,
                valid ? elapsed : null, source, item.Provider, window?.Anchor.Record.AttemptNumber ?? row.AttemptNumber, window?.Terminal?.Success ?? item.Success));
        }
        foreach (var window in operations.Where(window => !used.Contains(window))) {
            var row = window.Terminal?.Record ?? window.Anchor.Record;
            var start = window.Start?.Record.RequestAt ?? window.Start?.Record.OccurredAt ?? row.RequestAt;
            var end = window.Terminal?.Record.ResponseAt ?? window.Terminal?.Record.OccurredAt ?? row.ResponseAt;
            var valid = ParcelDurationAnalysisReader.TryDuration(start, end, row.ElapsedMilliseconds, row.HasReliableTimestamp, out var elapsed, out var source)
                && window.Terminal is not null && !window.Skipped && window.Start?.Record.HasReliableTimestamp != false;
            result.Add(ParcelDurationCall.Create(window.Anchor.Type, "attempt:" + window.Anchor.Record.RecordId, row, start, end,
                valid ? elapsed : null, source, window.Anchor.Provider, row.AttemptNumber, window.Terminal?.Success));
        }
        foreach (var item in events.Where(item => !item.Transport && item.OperationId.Length == 0)) {
            var row = item.Record;
            var valid = ParcelDurationAnalysisReader.TryDuration(row.RequestAt, row.ResponseAt, row.ElapsedMilliseconds, row.HasReliableTimestamp, out var elapsed, out var source)
                && !item.Skipped && !item.Outcome.StartsWith("late-", StringComparison.Ordinal) && item.Outcome != "started";
            result.Add(ParcelDurationCall.Create(item.Type, "fact:" + row.RecordId, row, row.RequestAt, row.ResponseAt,
                valid ? elapsed : null, source, item.Provider, row.AttemptNumber, item.Success));
        }
        return result;
    }
}
