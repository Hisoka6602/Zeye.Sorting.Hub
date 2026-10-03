using Xunit;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Tools.BusinessDataSimulator;

namespace Zeye.Sorting.Hub.Tools.BusinessDataSimulator.Tests;

/// <summary>验证合理场景及报告底层数据，不复制生成器的计数或随机算法。</summary>
public sealed class SimulationScenarioTests {
    /// <summary>全量窗口有多设备、有实际异常、有恢复尝试，历史不留伪造待处理数据。</summary>
    [Fact]
    public void ThirtyDaysHaveCoherentLifecycleAndProtocolCoverage() {
        var options = new SimulationOptions(30, 10000, new DateTime(2026, 10, 3, 8, 30, 0), "http://127.0.0.1:4187");
        var data = SimulationScenario.Generate(options);
        Assert.Equal(10000, data.Length);
        Assert.Equal(30, data.Select(s => s.Parcel.CreatedTime.Date).Distinct().Count());
        Assert.Equal(4, data.Select(s => s.Parcel.SourceInstanceId).Distinct().Count());
        Assert.InRange(data.Count(s => s.Parcel.Status == ParcelStatus.SortingException), 400, 800);
        Assert.InRange(data.Count(s => s.NoRead != NoReadType.None), 80, 220);
        Assert.All(data.Where(s => s.Parcel.Status == ParcelStatus.Pending), s => {
            Assert.Equal(options.End, s.Parcel.CreatedTime.Date);
            Assert.True(options.AsOf - s.Parcel.CreatedTime < TimeSpan.FromMinutes(1));
            Assert.Null(s.Parcel.CompletedTime);
            Assert.Null(s.Parcel.ActualChuteId);
        });
        var types = data.Where(s => s.Parcel.Status == ParcelStatus.SortingException).Select(s => s.Parcel.ExceptionType).ToHashSet();
        Assert.Contains(ParcelExceptionType.Unknown, types);
        Assert.Contains(ParcelExceptionType.ParcelSpacingViolation, types);
        Assert.Contains(ParcelExceptionType.TargetChuteAssignmentRejected, types);
        Assert.Contains(ParcelExceptionType.WaitTargetChuteTimeout, types);
        Assert.Contains(data, s => s.Records.Any(r => r.Stage == ParcelProcessingStage.ScanUploaded && r.IsSuccess == false) && s.Records.Any(r => r.Stage == ParcelProcessingStage.ScanUploaded && r.AttemptNumber == 2 && r.IsSuccess == true) && s.Parcel.Status == ParcelStatus.Completed);
        Assert.All(data.SelectMany(s => s.Records), r => Assert.True(r.OccurredAt <= r.RecordedAt && r.RecordedAt <= options.AsOf));
        Assert.All(data.Where(s => s.Parcel.Status == ParcelStatus.Completed), s => {
            Assert.Equal(s.Parcel.ActualChuteId, s.Parcel.TargetChuteId);
            Assert.Null(s.Parcel.ExceptionType);
            Assert.Equal((s.Parcel.CompletedTime!.Value - s.Parcel.DetectedTime!.Value).Ticks / TimeSpan.TicksPerMillisecond, s.Parcel.LifecycleMilliseconds);
        });
    }

    /// <summary>重新运行同一批次得到相同来源身份和载荷，避免幂等冲突。</summary>
    [Fact]
    public void FixedBatchClockProducesIdenticalIdentitiesAndPayloads() {
        var options = new SimulationOptions(3, 600, new DateTime(2026, 10, 3, 10, 15, 30), "http://localhost:4187");
        var first = SimulationScenario.Generate(options);
        var second = SimulationScenario.Generate(options);
        Assert.Equal(first.Select(s => s.Parcel.Id), second.Select(s => s.Parcel.Id));
        Assert.Equal(first.SelectMany(s => s.Records).Select(r => (r.Key, r.PayloadHash, r.PartitionTime, r.RecordedAt)), second.SelectMany(s => s.Records).Select(r => (r.Key, r.PayloadHash, r.PartitionTime, r.RecordedAt)));
        Assert.All(first, s => Assert.Equal(0, s.Parcel.CreatedTime.Ticks % 10));
    }

    /// <summary>跨年、闰年、凌晨均只生成本地窗口内的真实可用时间。</summary>
    [Theory]
    [InlineData(2026, 1, 1, 8, 1)]
    [InlineData(2028, 2, 29, 9, 29)]
    [InlineData(2026, 10, 3, 0, 2)]
    public void DateBoundariesNeverInventFutureEvents(int year, int month, int day, int hour, int minute) {
        var options = new SimulationOptions(30, 1200, new DateTime(year, month, day, hour, minute, 0), "http://127.0.0.1:4187");
        var data = SimulationScenario.Generate(options);
        Assert.Equal(options.Start, data.Min(s => s.Parcel.CreatedTime.Date));
        Assert.Equal(options.End, data.Max(s => s.Parcel.CreatedTime.Date));
        Assert.All(data.SelectMany(s => s.Records), r => Assert.InRange(r.RecordedAt, options.Start, options.AsOf));
    }

    /// <summary>重量克/千克和毫米/立方毫米关联正确，形态与尺寸合理。</summary>
    [Fact]
    public void MeasurementsDetailsAndProviderResponsesUseConsistentUnits() {
        var options = new SimulationOptions(7, 2100, new DateTime(2026, 10, 3, 10, 0, 0), "http://127.0.0.1:4187");
        var data = SimulationScenario.Generate(options);
        Assert.All(data, s => {
            var measurement = Assert.Single(s.Records.Where(r => r.Stage == ParcelProcessingStage.DwsBound));
            Assert.Equal(s.Parcel.Weight * 1000, measurement.WeightGrams);
            Assert.Equal(s.Parcel.Length * s.Parcel.Width * s.Parcel.Height, measurement.VolumeMm3);
            Assert.Equal(s.Parcel.Volume, s.Parcel.VolumeInfo!.FormattedVolume);
            Assert.Equal(s.Parcel.Weight, Assert.Single(s.Parcel.WeightInfos).FormattedWeight);
            Assert.All(s.Parcel.ApiRequests, request => Assert.Equal(request.ElapsedMilliseconds, (request.ResponseTime!.Value - request.RequestTime).Ticks / TimeSpan.TicksPerMillisecond));
            Assert.All(s.Parcel.CommandInfos.Where(c => c.Direction == CommandDirection.Send), command => Assert.Equal(ActionType.None, command.ActionType));
            Assert.All(s.Parcel.CommandInfos.Where(c => c.ActionType == ActionType.DischargeConfirmation), command => {
                Assert.Equal(CommandDirection.Receive, command.Direction);
                Assert.Contains(s.Records, record => record.Stage == ParcelProcessingStage.SortingCompleted && record.OccurredAt == command.GeneratedTime);
            });
            Assert.All(s.Records, r => Assert.Equal(s.Parcel.CreatedTime, r.PartitionTime));
        });
        Assert.All(data.Where(s => s.Kind == ParcelType.UltraThin), s => Assert.InRange(s.Parcel.Height!.Value, 8m, 25m));
        Assert.All(data.Where(s => s.Kind == ParcelType.Large), s => Assert.True(s.Parcel.Length >= 550));
        Assert.All(data.Where(s => s.NoRead != NoReadType.None), s => Assert.Equal("NoRead", s.Parcel.BarCodes));
    }
}
