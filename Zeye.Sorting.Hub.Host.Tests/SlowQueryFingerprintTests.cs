using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Application.Services.Diagnostics;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 慢查询指纹与画像测试。
/// </summary>
public sealed class SlowQueryFingerprintTests {
    /// <summary>
    /// 验证场景：相同结构 SQL 在不同参数下应产生相同指纹。
    /// </summary>
    [Fact]
    public void SlowQueryFingerprintAggregator_ShouldNormalizeParameterizedSql() {
        var first = SlowQueryFingerprintAggregator.Create("SELECT * FROM Parcels WHERE Id = @__id_0 AND BagCode = 'A-01'");
        var second = SlowQueryFingerprintAggregator.Create(" select  *  from parcels where id = @__id_1 and bagcode = 'B-02' ");

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal("select * from parcels where id = ? and bagcode = ?", first.NormalizedSql);
    }

    /// <summary>
    /// 验证场景：字符串字面量归一化应正确处理 SQL 单引号转义。
    /// </summary>
    [Fact]
    public void SlowQueryFingerprintAggregator_ShouldHandleEscapedQuoteLiteral() {
        var fingerprint = SlowQueryFingerprintAggregator.Create("SELECT * FROM Parcels WHERE ReceiverName = 'O''Reilly' AND Id = 1");

        Assert.Equal("select * from parcels where receivername = ? and id = ?", fingerprint.NormalizedSql);
    }

    /// <summary>
    /// 验证场景：画像存储应聚合同一指纹并输出 TopN 快照。
    /// </summary>
    [Fact]
    public void SlowQueryProfileStore_ShouldAggregateMetricsByFingerprint() {
        var store = new SlowQueryProfileStore(BuildConfiguration());

        store.Record("SELECT * FROM Parcels WHERE Id = @__id_0", TimeSpan.FromMilliseconds(600));
        store.Record("SELECT * FROM Parcels WHERE Id = @__id_1", TimeSpan.FromMilliseconds(900));
        store.Record("SELECT * FROM Parcels WHERE Id = @__id_2", TimeSpan.FromMilliseconds(1200), exception: new TimeoutException("超时"));

        var (snapshots, totalFingerprintCount) = store.GetTopProfiles();

        var snapshot = Assert.Single(snapshots);
        Assert.Equal(1, totalFingerprintCount);
        Assert.Equal(3, snapshot.CallCount);
        Assert.Equal(1, snapshot.TimeoutCount);
        Assert.Equal(1, snapshot.ErrorCount);
        Assert.Equal(1200d, snapshot.MaxMilliseconds, 3);
        Assert.Equal(1200d, snapshot.P99Milliseconds, 3);
        Assert.Equal("select * from parcels where id = ?", snapshot.NormalizedSql);
        Assert.Equal(snapshot.NormalizedSql, snapshot.SampleSql);
    }

    /// <summary>
    /// 验证场景：超出最大指纹数量时应淘汰最久未更新项。
    /// </summary>
    [Fact]
    public void SlowQueryProfileStore_ShouldEvictOldestFingerprint_WhenCapacityExceeded() {
        var store = new SlowQueryProfileStore(BuildConfiguration(new Dictionary<string, string?> {
            ["Persistence:AutoTuning:SlowQueryProfile:MaxFingerprintCount"] = "1",
            ["Persistence:AutoTuning:SlowQueryProfile:TopN"] = "1"
        }));

        store.Record("SELECT * FROM Parcels WHERE Id = 1", TimeSpan.FromMilliseconds(800));
        store.Record("SELECT * FROM ParcelHistory WHERE Id = 2", TimeSpan.FromMilliseconds(900));

        Assert.False(store.TryGetProfile(SlowQueryFingerprintAggregator.Create("SELECT * FROM Parcels WHERE Id = 1").Fingerprint, out _));
        Assert.True(store.TryGetProfile(SlowQueryFingerprintAggregator.Create("SELECT * FROM ParcelHistory WHERE Id = 2").Fingerprint, out _));
    }

    /// <summary>
    /// 验证场景：查询服务应返回列表与详情快照。
    /// </summary>
    [Fact]
    public void GetSlowQueryProfileQueryService_ShouldReturnListAndDetail() {
        var store = new SlowQueryProfileStore(BuildConfiguration());
        store.Record("SELECT * FROM Parcels WHERE Id = 99", TimeSpan.FromMilliseconds(700));
        var queryService = new GetSlowQueryProfileQueryService(store);

        var list = queryService.Execute();
        var listItem = Assert.Single(list.Items);
        var detail = queryService.Execute(listItem.Fingerprint);

        Assert.Equal(1, list.TotalFingerprintCount);
        Assert.NotNull(detail);
        Assert.Equal(listItem.Fingerprint, detail!.Fingerprint);
    }

    /// <summary>
    /// 验证场景：单指纹样本数量超过上限时应裁剪最旧样本。
    /// </summary>
    [Fact]
    public void SlowQueryProfileStore_ShouldTrimOldSamples_WhenPerFingerprintCapacityExceeded() {
        var store = new SlowQueryProfileStore(BuildConfiguration(new Dictionary<string, string?> {
            ["Persistence:AutoTuning:SlowQueryProfile:MaxSampleCountPerFingerprint"] = "2"
        }));

        store.Record("SELECT * FROM Parcels WHERE Id = 1", TimeSpan.FromMilliseconds(600));
        store.Record("SELECT * FROM Parcels WHERE Id = 2", TimeSpan.FromMilliseconds(700));
        store.Record("SELECT * FROM Parcels WHERE Id = 3", TimeSpan.FromMilliseconds(900));

        var (snapshots, _) = store.GetTopProfiles();

        var snapshot = Assert.Single(snapshots);
        Assert.Equal(2, snapshot.CallCount);
        Assert.Equal(900d, snapshot.P99Milliseconds, 3);
        Assert.Equal(800d, snapshot.AverageElapsedMilliseconds, 3);
    }

    /// <summary>
    /// 构建测试配置。
    /// </summary>
    /// <param name="overrides">覆盖项。</param>
    /// <returns>配置实例。</returns>
    private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?>? overrides = null) {
        var values = new Dictionary<string, string?> {
            ["Persistence:AutoTuning:SlowQueryThresholdMilliseconds"] = "500",
            ["Persistence:AutoTuning:SlowQueryProfile:IsEnabled"] = "true",
            ["Persistence:AutoTuning:SlowQueryProfile:WindowMinutes"] = "30",
            ["Persistence:AutoTuning:SlowQueryProfile:TopN"] = "50",
            ["Persistence:AutoTuning:SlowQueryProfile:MaxFingerprintCount"] = "1000",
            ["Persistence:AutoTuning:SlowQueryProfile:MaxSampleCountPerFingerprint"] = "256"
        };
        if (overrides is not null) {
            foreach (var pair in overrides) {
                values[pair.Key] = pair.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
