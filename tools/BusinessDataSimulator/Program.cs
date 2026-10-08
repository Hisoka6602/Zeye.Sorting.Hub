using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Aggregates.Idempotency;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.AuditLogs;
using Zeye.Sorting.Hub.Domain.Enums.DataGovernance;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Configuration;

namespace Zeye.Sorting.Hub.Tools.BusinessDataSimulator;

/// <summary>仅显式执行的本机造数入口，复用EF映射及物理分表能力。</summary>
internal static class Program {
    /// <summary>可查看的模拟批次和草稿文档序列化格式。</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>验证、写入、复核；不运行生产宿主或后台消费服务。</summary>
    private static async Task<int> Main(string[] args) {
        try {
            var asOf = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
            var options = new SimulationOptions(int.Parse(Value(args, "--days", "30"), CultureInfo.InvariantCulture), int.Parse(Value(args, "--count", "10000"), CultureInfo.InvariantCulture), asOf, Value(args, "--public-base-url", "http://127.0.0.1:4187"));
            options.Validate();
            if (!args.Contains("--write") && !args.Contains("--verify-only")) {
                var preview = SimulationScenario.Generate(options);
                PrintSummary(options, preview, 0, "生成与关联校验通过，未写数据库");
                return 0;
            }
            if (!args.Contains("--local-docker") || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "LocalDocker")
                throw new InvalidOperationException("写入及复核仅支持显式指定--local-docker的LocalDocker环境。");
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__MySql") ?? throw new InvalidOperationException("容器未配置MySQL连接。");
            var address = new MySqlConnectionStringBuilder(connection);
            if (address.Database != "zeye_sorting_hub" || address.Server is not ("mysql" or "localhost" or "127.0.0.1"))
                throw new InvalidOperationException("模拟工具仅能访问本机Sorting Hub Docker数据库。");
            var configDirectory = Value(args, "--config-directory", "/app");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(ConfigurationDocument.Flatten(ConfigurationReadOnlyLoader.Load(configDirectory, "LocalDocker")))
                .AddInMemoryCollection(new[] { "Persistence:Sharding:Strategy:Time:Granularity", "Persistence:Sharding:WriteRouting:AllowTableCreation", "Persistence:Sharding:WriteRouting:DryRun" }.Select(key => new KeyValuePair<string, string?>(key, Environment.GetEnvironmentVariable(key.Replace(":", "__")))) .Where(p => p.Value is not null)).Build();
            var dbOptions = new DbContextOptionsBuilder<SortingHubDbContext>().UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 0)), mysql => mysql.CommandTimeout(60)).Options;
            IDbContextFactory<SortingHubDbContext> factory = new PooledDbContextFactory<SortingHubDbContext>(dbOptions);
            await using var batchLock = await factory.CreateDbContextAsync();
            await batchLock.Database.OpenConnectionAsync();
            await using (var command = batchLock.Database.GetDbConnection().CreateCommand()) {
                command.CommandText = "SELECT GET_LOCK('zeye-business-simulation-v1', 0)";
                if (Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) != 1) throw new InvalidOperationException("另一个模拟数据任务正在运行。");
            }
            try {
                var document = await batchLock.Set<ManagedDocument>().AsNoTracking().SingleOrDefaultAsync(d => d.Key == options.BatchKey);
                if (document is not null) {
                    var saved = JsonSerializer.Deserialize<SimulationOptions>(document.Json, Json)!;
                    if (saved.Days != options.Days || saved.Count != options.Count || saved.PublicBaseUrl != options.PublicBaseUrl)
                        throw new InvalidOperationException("当前日期范围已有不同参数的模拟批次，请沿用原参数。");
                    options = saved;
                }
                else {
                    if (args.Contains("--verify-only")) throw new InvalidOperationException("尚未写入当前模拟批次。");
                    batchLock.Add(new ManagedDocument { Key = options.BatchKey, Json = JsonSerializer.Serialize(options, Json), Revision = 1, ModifiedAt = DateTime.Now });
                    await batchLock.SaveChangesAsync();
                }
                var data = SimulationScenario.Generate(options);
                var partitions = new ParcelPartitionStore(factory, configuration);
                var inserted = 0;
                if (args.Contains("--write")) {
                    foreach (var period in data.Select(s => partitions.Resolve(s.Parcel.CreatedTime)).Distinct()) await partitions.EnsureCreatedAsync(period, CancellationToken.None);
                    await CheckCollisionsAsync(factory, partitions, data);
                    var bags = await SeedBagsAsync(factory, options, data);
                    foreach (var group in data.GroupBy(s => partitions.Resolve(s.Parcel.CreatedTime).Suffix)) {
                        foreach (var chunk in group.Chunk(100)) inserted += await WriteChunkAsync(partitions, group.Key, chunk, bags);
                        Console.WriteLine($"分表 {group.Key}：{group.Count()} 票及关联明细已就绪。");
                    }
                    await SeedManagementAsync(factory, options, data, configDirectory);
                }
                await VerifyAsync(factory, partitions, options, data);
                PrintSummary(options, data, inserted, "数据库关联与业务一致性校验通过");
            }
            finally {
                await using var release = batchLock.Database.GetDbConnection().CreateCommand();
                release.CommandText = "SELECT RELEASE_LOCK('zeye-business-simulation-v1')";
                await release.ExecuteScalarAsync();
            }
            return 0;
        }
        catch (Exception exception) {
            // 不输出连接串、环境变量或完整数据库异常上下文。
            Console.Error.WriteLine($"模拟任务失败：{exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    /// <summary>所有编号预检完成后才写业务数据；既有记录不被覆盖。</summary>
    private static async Task CheckCollisionsAsync(IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions, SimulationParcel[] data) {
        foreach (var group in data.GroupBy(s => partitions.Resolve(s.Parcel.CreatedTime).Suffix)) {
            await using var db = await partitions.CreateContextAsync(group.Key, CancellationToken.None);
            await using var legacy = await factory.CreateDbContextAsync();
            foreach (var chunk in group.Chunk(300)) {
                var ids = chunk.Select(s => s.Parcel.Id).ToArray();
                if (await legacy.Set<Parcel>().AnyAsync(p => ids.Contains(p.Id))) throw new InvalidOperationException("预留编号与基础表记录冲突。");
                var existing = await db.Set<Parcel>().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.SourceInstanceId, p.SourceRunId, p.CreatedTime }).ToDictionaryAsync(p => p.Id);
                var locations = await db.Set<ParcelLocation>().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
                foreach (var sample in chunk) {
                    var p = sample.Parcel;
                    if (existing.TryGetValue(p.Id, out var old) && (old.SourceInstanceId != p.SourceInstanceId || old.SourceRunId != p.SourceRunId || old.CreatedTime != p.CreatedTime)) throw new InvalidOperationException("预留编号与其他批次或业务记录冲突。");
                    if (locations.TryGetValue(p.Id, out var location) && (location.Suffix != group.Key || location.SourceKey != SourceKey(p))) throw new InvalidOperationException("预留编号与来源定位索引冲突。");
                    if (existing.ContainsKey(p.Id) != locations.ContainsKey(p.Id)) throw new InvalidOperationException("已有模拟包裹的全局定位不完整，请先核查该记录。");
                }
            }
        }
    }

    /// <summary>Bags为每格口当前集包快照，只关联最近一天末批最多25票，避免虚构跨月超大集包。</summary>
    private static async Task<Dictionary<long, BagBinding>> SeedBagsAsync(IDbContextFactory<SortingHubDbContext> factory, SimulationOptions options, SimulationParcel[] data) {
        var result = new Dictionary<long, BagBinding>();
        await using var db = await factory.CreateDbContextAsync();
        foreach (var group in data.Where(s => s.Parcel.CreatedTime.Date == options.End && s.Parcel.Status == ParcelStatus.Completed).GroupBy(s => s.Parcel.ActualChuteId!.Value)) {
            var members = group.OrderBy(s => s.Parcel.CompletedTime).TakeLast(25).ToArray();
            var code = $"SIM-BAG-{options.End:yyyyMMdd}-{group.Key}";
            var bag = await db.Set<BagInfo>().SingleOrDefaultAsync(b => b.ChuteId == group.Key);
            if (bag is not null && bag.BagCode != code) continue;
            if (bag is null) {
                bag = new() { ChuteId = group.Key, ChuteName = $"模拟格口 {group.Key}", BagCode = code, ParcelCount = members.Length, BaggingTime = members.Max(s => s.Parcel.CompletedTime)!.Value.AddSeconds(15) };
                db.Add(bag);
                await db.SaveChangesAsync();
            }
            var binding = new BagBinding(db.Entry(bag).Property<long>("BagId").CurrentValue, code);
            foreach (var member in members) result.Add(member.Parcel.Id, binding);
        }
        return result;
    }

    /// <summary>分批事务内原子写入快照、明细、事实、去重凭据及全局定位。</summary>
    private static async Task<int> WriteChunkAsync(ParcelPartitionStore partitions, string suffix, SimulationParcel[] samples, Dictionary<long, BagBinding> bags) {
        await using var db = await partitions.CreateContextAsync(suffix, CancellationToken.None);
        var ids = samples.Select(s => s.Parcel.Id).ToArray();
        var existing = await db.Set<Parcel>().Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToHashSetAsync();
        var inserted = 0;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "parcel-sample.svg"));
        var imageHash = Convert.ToHexString(SHA256.HashData(bytes));
        foreach (var sample in samples.Where(s => !existing.Contains(s.Parcel.Id))) {
            var p = sample.Parcel;
            db.Add(p);
            // 上游类型、NoRead与辅助设备信息属于模拟快照，时间主状态由事实领域重放产生。
            db.Entry(p).Property(x => x.Type).CurrentValue = sample.Kind;
            db.Entry(p).Property(x => x.NoReadType).CurrentValue = sample.NoRead;
            db.Entry(p).Property(x => x.SorterCarrierId).CurrentValue = sample.CarrierId;
            db.Entry(p).Property(x => x.IsSticking).CurrentValue = sample.Sticking;
            db.Entry(p).Property(x => x.HasVideos).CurrentValue = p.VideoInfos.Count > 0;
            db.Entry(p).Property(x => x.Coordinate).CurrentValue = $"{p.ParcelPositionInfo!.CenterX},{p.ParcelPositionInfo.CenterY}";
            p.ModifyIp = "127.0.0.1";
            if (bags.TryGetValue(p.Id, out var bag)) { db.Entry(p).Property<long?>("BagId").CurrentValue = bag.Id; db.Entry(p).Property(x => x.BagCode).CurrentValue = bag.Code; }
            foreach (var image in p.ImageInfos) { db.Entry(image).Property(x => x.Sha256).CurrentValue = imageHash; db.Entry(image).Property(x => x.ObjectSizeBytes).CurrentValue = bytes.LongLength; }
            db.Add(new ParcelLocation { Id = p.Id, SourceKey = SourceKey(p), Suffix = suffix, CreatedTime = p.CreatedTime });
            db.AddRange(sample.Records);
            db.AddRange(sample.Records.Select(r => new ParcelProcessingReceipt { Key = r.Key, PayloadHash = r.PayloadHash, ParcelId = p.Id, Suffix = suffix, RecordedAt = r.RecordedAt }));
            var idempotency = IdempotencyRecord.CreatePending(SimulationScenario.Marker, "CreateSimulatedParcel", p.Id.ToString(CultureInfo.InvariantCulture), SimulationScenario.IdentityHash(sample.Records.Select(r => r.PayloadHash).ToArray()));
            idempotency.Id = AuxiliaryId('4', p);
            idempotency.MarkCompleted();
            db.Add(idempotency);
            SetTimes(db, idempotency, p.CreatedTime, p.CreatedTime.AddMilliseconds(1), "CompletedAt");
            if (p.Id % 16 == 0) AddInboxAndAudit(db, sample);
            inserted++;
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return inserted;
    }

    /// <summary>审计记录明确标注模拟，采用与真实入口一致的成功/重复/校验失败含义。</summary>
    private static void AddInboxAndAudit(SortingHubDbContext db, SimulationParcel sample) {
        var p = sample.Parcel;
        var time = p.CreatedTime.AddSeconds(1);
        var message = InboxMessage.CreatePending(SimulationScenario.Marker, $"SIM-OBSERVED-{p.Id}", "Simulation.ParcelObserved", DateTime.Now.AddDays(30));
        message.Id = AuxiliaryId('5', p);
        message.MarkProcessing(); message.MarkSucceeded(); db.Add(message);
        SetTimes(db, message, time, time.AddMilliseconds(5), "ProcessedAt");
        db.Entry(message).Property(nameof(InboxMessage.LastAttemptedAt)).CurrentValue = time.AddMilliseconds(1);
        db.Entry(message).Property(nameof(InboxMessage.ExpiresAt)).CurrentValue = time.AddDays(30);
        var status = p.Id % 11 == 0 ? 400 : p.Id % 7 == 0 ? 200 : 201;
        var response = status == 400 ? "{\"detail\":\"模拟：错误请求被校验拒绝；正确请求已另行接收\"}" : JsonSerializer.Serialize(new { parcelId = p.Id.ToString(CultureInfo.InvariantCulture), isDuplicate = status == 200, simulation = true });
        var request = JsonSerializer.Serialize(new { simulation = true, p.SourceInstanceId, p.SourceRunId, p.SourceParcelId, recordId = sample.Records[0].RecordId });
        var duration = 12 + (int)(p.Id % 79);
        var audit = new WebRequestAuditLog {
            Id = AuxiliaryId('6', p), TraceId = $"SIM-TRACE-{p.Id}", CorrelationId = $"SIM-PARCEL-{p.Id}", SpanId = $"sim-{p.Id}", OperationName = "模拟：接收包裹处理事实",
            RequestMethod = "POST", RequestScheme = "http", RequestHost = "localhost", RequestPath = "/api/admin/parcels/processing-records", RequestRouteTemplate = "/api/admin/parcels/processing-records",
            UserName = "模拟设备", IsAuthenticated = true, RequestPayloadType = WebRequestPayloadType.Json, RequestSizeBytes = Encoding.UTF8.GetByteCount(request), HasRequestBody = true,
            ResponsePayloadType = WebResponsePayloadType.Json, ResponseSizeBytes = Encoding.UTF8.GetByteCount(response), HasResponseBody = true, StatusCode = status, IsSuccess = status < 400,
            AuditResourceType = AuditResourceType.BusinessObject, ResourceId = p.Id.ToString(CultureInfo.InvariantCulture), StartedAt = time, EndedAt = time.AddMilliseconds(duration), DurationMs = duration, CreatedAt = time.AddMilliseconds(duration),
            Detail = new() { WebRequestAuditLogId = AuxiliaryId('6', p), StartedAt = time, RequestUrl = "http://localhost/api/admin/parcels/processing-records", RequestHeadersJson = "{\"X-Simulation\":\"true\"}", ResponseHeadersJson = "{}", RequestContentType = "application/json", ResponseContentType = "application/json", UserAgent = "Zeye-Business-Simulator/1", RequestBody = request, ResponseBody = response, ResourceName = $"模拟包裹 {p.Id}", Tags = "simulation", Remark = SimulationScenario.Marker, ErrorMessage = status == 400 ? "模拟：输入校验失败" : "" }
        };
        db.Add(audit);
    }

    /// <summary>草稿与归档演练记录可供页面展示，不执行真实删除、归档或规则发布。</summary>
    private static async Task SeedManagementAsync(IDbContextFactory<SortingHubDbContext> factory, SimulationOptions options, SimulationParcel[] data, string configDirectory) {
        await using var db = await factory.CreateDbContextAsync();
        var stored = ConfigurationReadOnlyLoader.Load(configDirectory, "LocalDocker");
        var values = ConfigurationDocument.Flatten(stored);
        var root = Path.GetFullPath(Environment.GetEnvironmentVariable("ZEYE_HUB_CONFIG_ROOT") ?? configDirectory);
        var history = new ConfigurationHistoryStore(Path.GetFullPath(Environment.GetEnvironmentVariable("ConfigurationStorage__HistorySqlitePath")
            ?? values.GetValueOrDefault("ConfigurationStorage:HistorySqlitePath") ?? "data/business-history/configuration-history.db", root));
        using var configurations = new LiteDbConfigurationStore(Path.GetFullPath(Environment.GetEnvironmentVariable("ConfigurationStorage__LiteDbPath")
            ?? values.GetValueOrDefault("ConfigurationStorage:LiteDbPath") ?? "data/configuration/settings.db", root), history, ConfigurationDocument.Defaults("LocalDocker"), stored);
        configurations.Import(await db.Set<ManagedDocument>().AsNoTracking().Where(x => x.Key == "rules-parcel" || x.Key == "rules-exception").ToArrayAsync());
        foreach (var category in new[] { "parcel", "exception" }) {
            var key = "rules-" + category;
            var document = configurations.Read(key);
            var old = document is null ? category == "exception" ? ClassificationRuleDefaults.Create() : [] : JsonSerializer.Deserialize<ClassificationRule[]>(document.Json, Json)!;
            var examples = SimulationScenario.ExampleRules(category == "exception", options.AsOf);
            if (examples.Any(e => old.Any(r => r.Id == e.Id && r.Note != SimulationScenario.Marker))) throw new InvalidOperationException("规则示例编号与现有规则冲突。");
            var additions = examples.Where(e => old.All(r => r.Id != e.Id)).ToArray();
            if (additions.Length == 0) continue;
            if (old.Length + additions.Length > 200) throw new InvalidOperationException("现有规则达到上限，不能追加模拟草稿。");
            if (configurations.Write(key, JsonSerializer.Serialize(old.Concat(additions), Json), document?.Revision ?? 0) is null)
                throw new InvalidOperationException("规则配置存在并发修改，请重试。");
        }
        for (var n = 1; n <= 6; n++) {
            var id = SimulationScenario.Id('3', options.End, n);
            if (await db.Set<ArchiveTask>().AnyAsync(t => t.Id == id && t.Remark != SimulationScenario.Marker)) throw new InvalidOperationException("归档模拟编号与既有任务冲突。");
            if (await db.Set<ArchiveTask>().AnyAsync(t => t.Id == id)) continue;
            var time = options.AsOf.AddMinutes(-120 - n * 5);
            var retention = n % 2 == 0 ? 14 : 7;
            var count = data.Count(s => s.Parcel.Id % 16 == 0 && s.Parcel.CreatedTime.AddSeconds(1) < time.AddDays(-retention));
            var task = ArchiveTask.CreateDryRun(ArchiveTaskType.WebRequestAuditLogHistory, retention, "模拟数据工具", SimulationScenario.Marker);
            task.Id = id; task.MarkRunning();
            if (n == 2 || n == 5) task.MarkFailed("模拟演练：归档目标暂时不可用；未移动或删除任何数据。");
            else task.MarkCompleted(count, $"模拟归档计划：保留{retention}天，历史请求审计{count}条；仅展示演练结果。", JsonSerializer.Serialize(new { simulation = true, plannedItemCount = count }));
            db.Add(task); SetTimes(db, task, time, time.AddSeconds(2), "CompletedAt");
            db.Entry(task).Property(nameof(ArchiveTask.LastAttemptedAt)).CurrentValue = time.AddMilliseconds(20);
        }
        await db.SaveChangesAsync();
    }

    /// <summary>独立读取落库结果，核对每票快照、物理位置、事实与去重凭据。</summary>
    private static async Task VerifyAsync(IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions, SimulationOptions options, SimulationParcel[] data) {
        foreach (var group in data.GroupBy(s => partitions.Resolve(s.Parcel.CreatedTime).Suffix)) {
            await using var db = await partitions.CreateContextAsync(group.Key, CancellationToken.None);
            var min = group.Min(s => s.Parcel.Id); var max = group.Max(s => s.Parcel.Id);
            var expected = group.ToDictionary(s => s.Parcel.Id);
            var stored = await db.Set<Parcel>().AsNoTracking().Where(p => p.Id >= min && p.Id <= max).ToArrayAsync();
            if (stored.Length != expected.Count) throw new InvalidOperationException("落库包裹总量不一致。");
            var locations = await db.Set<ParcelLocation>().AsNoTracking().Where(p => p.Id >= min && p.Id <= max).ToDictionaryAsync(p => p.Id);
            var records = await db.Set<ParcelProcessingRecord>().AsNoTracking().Where(r => r.ParcelId >= min && r.ParcelId <= max).ToArrayAsync();
            var receipts = await db.Set<ParcelProcessingReceipt>().AsNoTracking().Where(r => r.ParcelId >= min && r.ParcelId <= max).ToDictionaryAsync(r => r.Key);
            if (records.Length != group.Sum(s => s.Records.Length) || receipts.Count != records.Length) throw new InvalidOperationException("处理事实或全局去重数量不一致。");
            foreach (var p in stored) {
                var sample = expected[p.Id];
                if (p.Status != sample.Parcel.Status || p.Type != sample.Kind || p.NoReadType != sample.NoRead || p.Weight != sample.Parcel.Weight || p.Volume != p.Length * p.Width * p.Height || p.CreatedTime != sample.Parcel.CreatedTime || p.CompletedTime != sample.Parcel.CompletedTime || p.ExceptionType != sample.Parcel.ExceptionType || p.ApiRequests.Count != sample.Parcel.ApiRequests.Count || p.BarCodeInfos.Count != sample.Parcel.BarCodeInfos.Count || p.CommandInfos.Count != sample.Parcel.CommandInfos.Count || p.ImageInfos.Count != sample.Parcel.ImageInfos.Count || p.DeviceInfo?.MachineCode != p.SourceInstanceId || p.HasVideos != (p.VideoInfos.Count > 0)) throw new InvalidOperationException($"模拟包裹 {p.Id} 快照或明细不一致。");
                if (!locations.TryGetValue(p.Id, out var location) || location.SourceKey != SourceKey(p) || location.Suffix != group.Key || location.CreatedTime != p.CreatedTime) throw new InvalidOperationException("来源定位与分表不一致。");
                if (p.BagInfo is not null && p.BagCode != p.BagInfo.BagCode) throw new InvalidOperationException("包裹集包关系不一致。");
                var commands = p.CommandInfos.OrderBy(c => c.GeneratedTime).ToArray();
                var expectedCommands = sample.Parcel.CommandInfos.OrderBy(c => c.GeneratedTime).ToArray();
                if (commands.Length != expectedCommands.Length || commands.Where((command, index) => command.ActionType != expectedCommands[index].ActionType || command.Direction != expectedCommands[index].Direction || command.GeneratedTime != expectedCommands[index].GeneratedTime).Any()) throw new InvalidOperationException("模拟通信指令与收发方向不一致。");
            }
            var hashes = group.SelectMany(s => s.Records).ToDictionary(r => r.Key, r => r.PayloadHash);
            foreach (var record in records) if (hashes[record.Key] != record.PayloadHash || record.RecordedAt > options.AsOf || !receipts.TryGetValue(record.Key, out var receipt) || receipt.PayloadHash != record.PayloadHash || receipt.ParcelId != record.ParcelId || receipt.Suffix != group.Key) throw new InvalidOperationException("处理记录和幂等凭据不一致。");
        }
        await using var primary = await factory.CreateDbContextAsync();
        var auditIds = data.Where(s => s.Parcel.Id % 16 == 0).Select(s => AuxiliaryId('6', s.Parcel)).ToArray();
        var audits = await primary.Set<WebRequestAuditLog>().Include(a => a.Detail).AsNoTracking().Where(a => auditIds.Contains(a.Id)).ToArrayAsync();
        if (audits.Length != auditIds.Length || audits.Any(a => a.Detail is null || a.Detail.Remark != SimulationScenario.Marker || a.StartedAt != a.Detail.StartedAt || a.DurationMs != (a.EndedAt - a.StartedAt).Ticks / TimeSpan.TicksPerMillisecond || a.ResponseSizeBytes != Encoding.UTF8.GetByteCount(a.Detail.ResponseBody))) throw new InvalidOperationException("审计主详情关联或耗时不一致。");
        var minIdempotencyId = AuxiliaryId('4', data.MinBy(s => s.Parcel.Id)!.Parcel);
        var maxIdempotencyId = AuxiliaryId('4', data.MaxBy(s => s.Parcel.Id)!.Parcel);
        var idempotencyCount = await primary.Set<IdempotencyRecord>().CountAsync(r => r.SourceSystem == SimulationScenario.Marker && r.Id >= minIdempotencyId && r.Id <= maxIdempotencyId);
        if (idempotencyCount != data.Length) throw new InvalidOperationException("造数幂等记录总量不一致。");
        Console.WriteLine($"复核：{data.Length} 票、{data.Sum(s => s.Records.Length)} 条处理事实、{audits.Length} 条审计主详情，无关联错误。");
    }

    /// <summary>规范化来源三元组的真实仓储身份键。</summary>
    private static string SourceKey(Parcel p) => SimulationScenario.IdentityHash(p.SourceInstanceId!, p.SourceRunId!, p.SourceParcelId!.Value.ToString(CultureInfo.InvariantCulture));
    /// <summary>与包裹尾号对应的辅助记录身份。</summary>
    private static long AuxiliaryId(char prefix, Parcel p) => SimulationScenario.Id(prefix, p.CreatedTime.Date, (int)(p.Id % 10000));
    /// <summary>模拟历史记录的本地时间回填，仅用于新记录。</summary>
    private static void SetTimes(SortingHubDbContext db, object entity, DateTime created, DateTime updated, string completion) {
        db.Entry(entity).Property("CreatedAt").CurrentValue = created;
        db.Entry(entity).Property("UpdatedAt").CurrentValue = updated;
        db.Entry(entity).Property(completion).CurrentValue = updated;
    }
    /// <summary>读取参数值，拒绝遗漏值的选项。</summary>
    private static string Value(string[] args, string key, string fallback) {
        var index = Array.IndexOf(args, key);
        return index >= 0 ? index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal) ? args[index + 1] : throw new ArgumentException(key + "缺少参数。") : fallback;
    }
    /// <summary>输出模拟批次汇总，不输出部署秘密。</summary>
    private static void PrintSummary(SimulationOptions options, SimulationParcel[] data, int inserted, string result) => Console.WriteLine(JsonSerializer.Serialize(new {
        result, batch = options.BatchKey, start = options.Start.ToString("yyyy-MM-dd"), end = options.End.ToString("yyyy-MM-dd"), parcelCount = data.Length, inserted,
        completed = data.Count(s => s.Parcel.Status == ParcelStatus.Completed), exceptions = data.Count(s => s.Parcel.Status == ParcelStatus.SortingException), pending = data.Count(s => s.Parcel.Status == ParcelStatus.Pending), noRead = data.Count(s => s.NoRead != NoReadType.None),
        workstations = data.Select(s => s.Parcel.SourceInstanceId).Distinct().Count(), processingRecords = data.Sum(s => s.Records.Length), images = data.Sum(s => s.Parcel.ImageInfos.Count), apiRequests = data.Sum(s => s.Parcel.ApiRequests.Count), commands = data.Sum(s => s.Parcel.CommandInfos.Count), videoMetadata = data.Sum(s => s.Parcel.VideoInfos.Count)
    }, Json));
}
