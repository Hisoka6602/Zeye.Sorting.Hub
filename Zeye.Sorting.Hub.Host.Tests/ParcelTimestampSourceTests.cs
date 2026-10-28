using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>直接验证来源事实重放的时间优先级、乱序稳定性及明确不可靠时间的耗时边界。</summary>
public sealed class ParcelTimestampSourceTests {
    /// <summary>晚到检测必须纠正首次上传产生的扫码摘要，最终时间与到达顺序无关。</summary>
    [Fact]
    public void DetectionDeterminesScanTimeRegardlessOfFirstArrivingFact() {
        var at = new DateTime(2026, 10, 28, 10, 0, 0);
        var detected = Fact("detected", ParcelProcessingStage.Detected, at);
        var uploaded = Fact("uploaded", ParcelProcessingStage.ScanUploaded, at.AddSeconds(3));
        var registeredAt = at.AddDays(2);
        var detectionFirst = Replay(detected, registeredAt, [detected, uploaded]);
        var uploadFirst = Replay(uploaded, registeredAt, [uploaded, detected]);

        Assert.Equal(at, detectionFirst.ScannedTime);
        Assert.Equal(detectionFirst.ScannedTime, uploadFirst.ScannedTime);
        Assert.Equal(at, uploadFirst.DetectedTime);
        Assert.Null(uploadFirst.MeasurementTime);
        Assert.Equal(registeredAt, detectionFirst.CreatedTime);
        Assert.Equal(registeredAt, uploadFirst.CreatedTime);
    }

    /// <summary>成功DWS的设备量测时间优先于检测；缺失量测时使用来源绑定事件，均不取Hub时间。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SuccessfulDwsTimeTakesPriorityOverDetection(bool hasMeasurementTime) {
        var at = new DateTime(2026, 10, 28, 10, 0, 0);
        var detected = Fact("detected", ParcelProcessingStage.Detected, at);
        var bound = Fact("bound", ParcelProcessingStage.DwsBound, at.AddSeconds(1)) with {
            IsSuccess = true, FinalSourceParcelId = 7,
            MeasuredAt = hasMeasurementTime ? at.AddMilliseconds(500) : null
        };
        var uploaded = Fact("uploaded", ParcelProcessingStage.ScanUploaded, at.AddSeconds(3));
        var registeredAt = at.AddDays(2);
        var detectionFirst = Replay(detected, registeredAt, [uploaded, bound, detected]);
        var uploadFirst = Replay(uploaded, registeredAt, [detected, bound, uploaded]);
        var expected = bound.MeasuredAt ?? bound.OccurredAt;

        Assert.Equal(expected, detectionFirst.ScannedTime);
        Assert.Equal(expected, uploadFirst.ScannedTime);
        Assert.Equal(expected, uploadFirst.MeasurementTime);
        Assert.Equal(at, uploadFirst.DetectedTime);
        Assert.Equal(registeredAt, uploadFirst.CreatedTime);
    }

    /// <summary>没有检测和成功DWS时，仅按相同包裹身份的最早来源事件回退，不受首条到达或外来事实影响。</summary>
    [Fact]
    public void MissingDetectionUsesStableMatchingSourceEventFallback() {
        var at = new DateTime(2026, 10, 28, 10, 0, 0);
        var earlier = Fact("earlier", ParcelProcessingStage.ScanUploaded, at);
        var later = Fact("later", ParcelProcessingStage.ScanUploaded, at.AddSeconds(3));
        var foreign = earlier with {
            RecordId = "foreign", ParcelId = 2, SourceParcelId = 8, OccurredAt = at.AddDays(-1), RecordedAt = at.AddYears(5)
        };
        var registeredAt = at.AddDays(2);
        var earlyFirst = Replay(earlier, registeredAt, [later, foreign, earlier]);
        var lateFirst = Replay(later, registeredAt, [earlier, foreign, later]);

        Assert.Equal(at, earlyFirst.ScannedTime);
        Assert.Equal(at, lateFirst.ScannedTime);
        Assert.Null(lateFirst.DetectedTime);
        Assert.Null(lateFirst.MeasurementTime);
        Assert.Equal(registeredAt, lateFirst.CreatedTime);
        Assert.Equal(later.RecordedAt, earlyFirst.ModifyTime);
        Assert.Equal(later.RecordedAt, lateFirst.ModifyTime);
    }

    /// <summary>检测或完成任一端明确不可靠即不产生生命周期；未知标记继续兼容且原始端点保留。</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, null, false)]
    [InlineData(null, false, false)]
    [InlineData(null, null, true)]
    [InlineData(null, true, true)]
    [InlineData(true, null, true)]
    [InlineData(true, true, true)]
    public void LifecycleRejectsExplicitlyUnreliableEndpoints(bool? detectionReliable, bool? completionReliable, bool available) {
        var at = new DateTime(2026, 10, 28, 10, 0, 0);
        var detected = Fact("detected", ParcelProcessingStage.Detected, at) with { HasReliableTimestamp = detectionReliable };
        var completed = Fact("completed", ParcelProcessingStage.SortingCompleted, at.AddSeconds(10)) with {
            IsSuccess = true, HasReliableTimestamp = completionReliable, TargetChuteCode = "01", ActualChuteCode = "01"
        };
        var parcel = Replay(completed, at.AddDays(2), [completed, detected]);

        Assert.Equal(available ? 10000L : (long?)null, parcel.LifecycleMilliseconds);
        Assert.Equal(at, parcel.DetectedTime);
        Assert.Equal(completed.OccurredAt, parcel.CompletedTime);
        Assert.Equal(completed.OccurredAt, parcel.DischargeTime);
        Assert.Equal(ParcelStatus.Completed, parcel.Status);
        Assert.Equal(at.AddDays(2), parcel.CreatedTime);
    }

    /// <summary>同刻先检测和路由再完成；随机事实编号、跨阶段尝试号及输入顺序不改变真零耗时和最终格口质量。</summary>
    [Theory]
    [InlineData(false, "02", false)]
    [InlineData(true, "02", false)]
    [InlineData(false, "03", true)]
    [InlineData(true, "03", true)]
    public void CoincidentStagesKeepCompletionTimeAndFinalTargetStable(bool reverseIds, string actualChute, bool mismatch) {
        var at = new DateTime(2026, 10, 28, 10, 0, 0);
        var detected = Fact(reverseIds ? "z-detected" : "a-detected", ParcelProcessingStage.Detected, at) with { AttemptNumber = 9 };
        var assigned = Fact(reverseIds ? "a-assigned" : "z-assigned", ParcelProcessingStage.ChuteAssigned, at) with {
            AttemptNumber = 7, IsSuccess = true, TargetChuteCode = "01"
        };
        var completed = Fact(reverseIds ? "m-completed" : "b-completed", ParcelProcessingStage.SortingCompleted, at) with {
            IsSuccess = true, TargetChuteCode = "02", ActualChuteCode = actualChute
        };
        var registeredAt = at.AddDays(2);
        var detectionFirst = Replay(detected, registeredAt, [completed, assigned, detected]);
        var completionFirst = Replay(completed, registeredAt, [detected, assigned, completed]);

        foreach (var parcel in new[] { detectionFirst, completionFirst }) {
            Assert.Equal(0L, parcel.LifecycleMilliseconds);
            Assert.Equal(at, parcel.DetectedTime);
            Assert.Equal(at, parcel.CompletedTime);
            Assert.Equal("02", parcel.TargetChuteCode);
            Assert.Equal(actualChute, parcel.ActualChuteCode);
            Assert.Equal(mismatch ? ParcelExceptionType.AbnormalDischarge : (ParcelExceptionType?)null, parcel.ExceptionType);
            Assert.Equal(ParcelStatus.Completed, parcel.Status);
            Assert.Equal(registeredAt, parcel.CreatedTime);
        }
    }

    /// <summary>同阶段仍按尝试号及编号选取首检测和末次成功完成，其他同刻可靠性标记不能覆盖所用端点。</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void CoincidentEndpointReliabilityKeepsSameStageAttemptAndRecordOrdering(bool distinctAttempts, bool detectionReliable, bool completionReliable) {
        var at = new DateTime(2026, 10, 28, 10, 0, 0);
        var detected = Fact(distinctAttempts ? "z-detected" : "a-detected", ParcelProcessingStage.Detected, at) with {
            HasReliableTimestamp = detectionReliable
        };
        var laterDetection = Fact(distinctAttempts ? "a-detected" : "z-detected", ParcelProcessingStage.Detected, at) with {
            AttemptNumber = distinctAttempts ? 2 : 1, HasReliableTimestamp = !detectionReliable
        };
        var earlierCompletion = Fact(distinctAttempts ? "z-completed" : "a-completed", ParcelProcessingStage.SortingCompleted, at) with {
            IsSuccess = true, TargetChuteCode = "01", ActualChuteCode = "01", HasReliableTimestamp = !completionReliable
        };
        var completed = Fact(distinctAttempts ? "a-completed" : "z-completed", ParcelProcessingStage.SortingCompleted, at) with {
            AttemptNumber = distinctAttempts ? 2 : 1, IsSuccess = true,
            TargetChuteCode = "02", ActualChuteCode = "02", HasReliableTimestamp = completionReliable
        };
        var parcel = Replay(completed, at.AddDays(2), [completed, laterDetection, earlierCompletion, detected]);

        Assert.Equal(detectionReliable && completionReliable ? 0L : (long?)null, parcel.LifecycleMilliseconds);
        Assert.Equal(at, parcel.DetectedTime);
        Assert.Equal(at, parcel.CompletedTime);
        Assert.Equal("02", parcel.TargetChuteCode);
        Assert.Equal("02", parcel.ActualChuteCode);
        Assert.Null(parcel.ExceptionType);
        Assert.Equal(ParcelStatus.Completed, parcel.Status);
    }

    /// <summary>没有分类规则时仍保留内置上传失败异常，成功完成不能清除历史失败。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuiltInFailureClassificationRemainsActiveWithoutRules(bool emptyRules) {
        var at = new DateTime(2026, 10, 28, 10, 0, 0);
        var detected = Fact("detected", ParcelProcessingStage.Detected, at);
        var failed = Fact("failed", ParcelProcessingStage.ScanUploaded, at.AddSeconds(1)) with { IsSuccess = false };
        var completed = Fact("completed", ParcelProcessingStage.SortingCompleted, at.AddSeconds(2)) with {
            IsSuccess = true, TargetChuteCode = "01", ActualChuteCode = "01"
        };
        var parcel = Parcel.CreateDetected(1, detected, at.AddDays(2));
        parcel.ApplyProcessingRecords([completed, failed, detected], emptyRules ? [] : null);

        Assert.Equal(ParcelStatus.Completed, parcel.Status);
        Assert.Equal(ParcelExceptionType.InterfaceResponseException, parcel.ExceptionType);
        Assert.Equal(2000L, parcel.LifecycleMilliseconds);
    }

    /// <summary>先重放首到事实，再以完整历史恢复快照，模拟持久化路径的晚到消息处理。</summary>
    private static Parcel Replay(ParcelProcessingRecord first, DateTime registeredAt, ParcelProcessingRecord[] history) {
        var parcel = Parcel.CreateDetected(1, first, registeredAt);
        parcel.ApplyProcessingRecords([first]);
        parcel.ApplyProcessingRecords(history);
        return parcel;
    }

    /// <summary>构造具有同一完整来源身份的有效本地事实，Hub记录时间与源端事件时间分离。</summary>
    private static ParcelProcessingRecord Fact(string id, ParcelProcessingStage stage, DateTime occurredAt) => new() {
        Key = id, RecordId = id, ParcelId = 1, SourceParcelId = 7, SourceInstanceId = "timestamp-source", SourceRunId = "run-a",
        Stage = stage, OccurredAt = occurredAt, RecordedAt = occurredAt.AddDays(2), PartitionTime = occurredAt.AddDays(2),
        PayloadHash = id, WorkstationName = "时间测试台"
    };
}
