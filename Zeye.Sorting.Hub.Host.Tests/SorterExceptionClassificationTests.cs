using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证 Fusion 的真实异常编码、不可缺失的未知异常兜底及原始事实保留。</summary>
public sealed class SorterExceptionClassificationTests {
    /// <summary>已知编码精确匹配且忽略大小写；未知、空值和相似编码始终兜底。</summary>
    [Theory]
    [InlineData("ParcelSpacingViolation", ParcelExceptionType.ParcelSpacingViolation)]
    [InlineData("TargetChuteAssignmentRejected", ParcelExceptionType.TargetChuteAssignmentRejected)]
    [InlineData("RoutingTimeout", ParcelExceptionType.WaitTargetChuteTimeout)]
    [InlineData(" routingtimeout ", ParcelExceptionType.WaitTargetChuteTimeout)]
    [InlineData("FutureDeviceFailure", ParcelExceptionType.Unknown)]
    [InlineData("DEVICE_MOTOR", ParcelExceptionType.Unknown)]
    [InlineData("RoutingTimeoutExtra", ParcelExceptionType.Unknown)]
    [InlineData("13", ParcelExceptionType.Unknown)]
    [InlineData("", ParcelExceptionType.Unknown)]
    [InlineData(null, ParcelExceptionType.Unknown)]
    public void ClassifiesProtocolCodesWithUnknownFallback(string? code, ParcelExceptionType expected) =>
        Assert.Equal(expected, SorterExceptionClassifier.Classify(code));

    /// <summary>通过真实关系仓储入库后，已知与未知异常分类可读取，原始编码和报文不丢失。</summary>
    [Theory]
    [InlineData("ParcelSpacingViolation", ParcelExceptionType.ParcelSpacingViolation)]
    [InlineData("TargetChuteAssignmentRejected", ParcelExceptionType.TargetChuteAssignmentRejected)]
    [InlineData("RoutingTimeout", ParcelExceptionType.WaitTargetChuteTimeout)]
    [InlineData("FutureDeviceFailure", ParcelExceptionType.Unknown)]
    public async Task PersistsClassificationAndOriginalPayload(string code, ParcelExceptionType expected) {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var detected = Fact("detected");
        var created = await database.Processing.AppendAsync(detected, default);
        Assert.True(created.IsSuccess, created.ErrorMessage);
        var exception = detected with {
            RecordId = "exception", Stage = ParcelProcessingStage.ParcelException,
            OccurredAt = detected.OccurredAt.AddSeconds(1), ExceptionCode = code,
            RawPayload = "original-sorter-frame", ErrorMessage = "设备原始异常说明"
        };
        var written = await database.Processing.AppendAsync(exception, default);
        Assert.True(written.IsSuccess, written.ErrorMessage);
        var parcel = await database.Parcels.GetByIdAsync(created.Value!.ParcelId!.Value, default);
        Assert.NotNull(parcel);
        Assert.Equal(ParcelStatus.SortingException, parcel.Status);
        Assert.Equal(expected, parcel.ExceptionType);
        Assert.Equal(code, parcel.SourceExceptionCode);
        var saved = Assert.Single(parcel.ProcessingRecords.Where(record => record.Stage == ParcelProcessingStage.ParcelException));
        Assert.Equal(code, saved.ExceptionCode);
        Assert.Equal("original-sorter-frame", saved.RawPayload);
        Assert.Equal("设备原始异常说明", saved.ErrorMessage);
    }

    /// <summary>实际落格完成后，迟到未知异常只保留事实，不倒退完成状态。</summary>
    [Fact]
    public void LateUnknownExceptionDoesNotUndoCompletion() {
        var detected = Fact("detected") with { ParcelId = 1 };
        var parcel = Parcel.CreateDetected(1, detected, detected.RecordedAt);
        var completed = detected with { RecordId = "completed", Stage = ParcelProcessingStage.SortingCompleted, OccurredAt = detected.OccurredAt.AddSeconds(1), ActualChuteCode = "A01" };
        var exception = detected with { RecordId = "exception", Stage = ParcelProcessingStage.ParcelException, OccurredAt = detected.OccurredAt.AddSeconds(2), ExceptionCode = "FutureDeviceFailure" };
        parcel.ApplyProcessingRecords([detected, completed, exception]);
        Assert.Equal(ParcelStatus.Completed, parcel.Status);
        Assert.Null(parcel.ExceptionType);
        Assert.Equal("FutureDeviceFailure", parcel.SourceExceptionCode);
    }

    /// <summary>建立独立测试来源身份和本地时间，不依赖生产设备或数据库。</summary>
    private static ParcelProcessingRecord Fact(string id) => new() {
        RecordId = id, SourceInstanceId = "sorter-classification-test", SourceRunId = "test-run", SourceParcelId = 42,
        Stage = ParcelProcessingStage.Detected, OccurredAt = new(2026, 9, 28, 10, 0, 0),
        RecordedAt = new(2026, 9, 28, 10, 0, 0), PartitionTime = new(2026, 9, 28, 10, 0, 0), PayloadHash = "test", IsSuccess = true
    };
}