# EF Core 9 升级计划

> 背景：EF Core 9 新增 `HasPendingModelChanges()` API，可在运行时检测代码模型是否与已应用迁移存在差异，从而进一步增强 CodeFirst 守卫能力。

---

## 1. 升级可行性结论

| 问题 | 结论 |
|------|------|
| EF Core 9 是否强依赖 .NET 9？ | **否。** EF Core 9 最低支持 `.NET 8`，无需同步升级运行时框架。 |
| 是否需要修改 `TargetFramework`？ | **不需要**（保持 `net8.0` 即可）。如需 .NET 9 新特性可单独评估。 |
| 升级风险等级 | 低（仅升级 NuGet 包版本）。 |

---

## 2. 受影响的 NuGet 包

以下包需从 `8.0.x` 升级至 `9.0.x`（建议 `9.0.6` 或最新稳定版）：

| 包名 | 当前版本 | 目标版本 |
|------|----------|----------|
| `Microsoft.EntityFrameworkCore` | 8.0.23 | 9.0.x |
| `Microsoft.EntityFrameworkCore.Design` | 8.0.23 | 9.0.x |
| `Microsoft.EntityFrameworkCore.SqlServer` | 8.0.23 | 9.0.x |
| `Pomelo.EntityFrameworkCore.MySql` | 8.0.x | 9.0.x（需 Pomelo ≥ 9.0.0） |
| `Microsoft.Extensions.Configuration.Json` | 8.* | 9.*（可选） |

> ⚠️ `Pomelo.EntityFrameworkCore.MySql` 9.0.0 最低支持 MySQL 8.0+，如果生产数据库为 MySQL 5.7，升级前须确认版本兼容性。

---

## 3. 升级步骤

### 步骤 1：升级 Infrastructure 项目的 NuGet 包

编辑 `Zeye.Sorting.Hub.Infrastructure/Zeye.Sorting.Hub.Infrastructure.csproj`，将所有 EF Core 相关包版本号改为 `9.0.6`（或最新稳定版）：

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.6" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.6">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.6" />
<PackageReference Include="Pomelo.EntityFrameworkCore.MySql" Version="9.0.0" />
```

然后运行：

```bash
dotnet restore
dotnet build
```

### 步骤 2：重新生成迁移快照（可选但推荐）

EF Core 9 的模型快照格式与 8 存在细微差异，建议重新生成：

```bash
# 先删除旧迁移（或保留旧迁移仅重生成快照）
cd Zeye.Sorting.Hub.Infrastructure

# 若要保留已有迁移历史，仅刷新 Designer + Snapshot，直接运行：
dotnet ef migrations add RefreshSnapshot --context SortingHubDbContext --project . --startup-project ../Zeye.Sorting.Hub.Host
```

### 步骤 3：增强 `AssertMigrationConsistencyAsync()` 守卫

升级完成后，在 `DatabaseInitializerHostedService.cs` 中利用 `HasPendingModelChanges()` 增强守卫，检测代码模型与已应用迁移之间的实际差异（不仅是历史记录差异）：

> **注意**：`HasPendingModelChanges()` 仅在 EF Core 9+ 中可用；在执行此步骤之前，请确保步骤 1 的包升级已完成。

```csharp
/// <summary>
/// CodeFirst 一致性守卫（EF Core 9 增强版，需 EF Core ≥ 9.0）
/// 在 MigrateAsync() 后执行三项校验：
///   1. GetPendingMigrationsAsync() 非空 → 迁移未完全应用
///   2. 代码迁移集合与 __EFMigrationsHistory 差异 → 历史记录不一致
///   3. HasPendingModelChanges() → 代码模型与最新迁移存在 diff（需 EF Core 9+）
/// </summary>
private async Task AssertMigrationConsistencyAsync(DbContext db, CancellationToken cancellationToken) {
    // —— 校验 1 & 2（已有逻辑，保持不变）——
    // ... 现有代码 ...

    // —— 校验 3：模型变更检测（仅 EF Core 9+ 可用）——
    // 如仍在使用 EF Core 8，删除或注释此块，并按 EFCore9-UpgradePlan.md 完成升级后启用。
    if (db.Database.HasPendingModelChanges()) {
        _logger.LogCritical(
            "[CodeFirst 守卫] 检测到代码模型与最新迁移存在差异：" +
            "当前 DbContext 中的实体配置与最新迁移文件不一致。" +
            "请运行 'dotnet ef migrations add <名称>' 生成新迁移以同步模型变更，" +
            "严禁直接修改数据库表结构。");
    }
}
```

### 步骤 4：运行全量测试

```bash
dotnet test Zeye.Sorting.Hub.Host.Tests/Zeye.Sorting.Hub.Host.Tests.csproj
```

---

## 4. EF Core 8 → 9 重要变更说明

| 变更点 | 影响 | 应对措施 |
|--------|------|----------|
| `HasPendingModelChanges()` 新增 | ✅ 正向新增 | 按步骤 3 集成 |
| `OwnsOne`/`OwnsMany` 映射规则微调 | 低风险 | 运行测试验证，若有报错修复对应配置 |
| JSON 列支持增强 | 无影响 | 本项目未使用 JSON 列类型 |
| Pomelo 9 MySQL 最低版本要求 | ⚠️ 需确认 | 确保 MySQL ≥ 8.0 |
| `Microsoft.Data.SqlClient` 依赖版本 | 低风险 | 随 EFCore.SqlServer 自动更新 |

---

## 5. 回滚方案

若升级后出现问题，将所有包版本恢复为 `8.0.23`（`Pomelo` 对应 `8.0.x`），重新 `dotnet restore` 即可恢复。

---

## 6. 升级核查清单

- [ ] 所有 EF Core 相关包升级至 9.0.x  
- [ ] Pomelo ≥ 9.0.0，生产 MySQL ≥ 8.0  
- [ ] `dotnet build` 无 warning/error  
- [ ] 迁移快照重新生成  
- [ ] `dotnet test` 全部通过  
- [ ] `DatabaseInitializerHostedService.AssertMigrationConsistencyAsync()` 集成 `HasPendingModelChanges()`  
- [ ] `EFCore-Migration.md` 守卫行为说明更新为"三项校验"  
