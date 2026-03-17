# EF Core 9 升级记录

> **状态：✅ 已完成**（`Zeye.Sorting.Hub.Infrastructure.csproj` 已升级至 EF Core 9.0.14；`HasPendingModelChanges()` 守卫已集成）

---

## 1. 升级可行性结论

| 问题 | 结论 |
|------|------|
| EF Core 9 是否强依赖 .NET 9？ | **否。** EF Core 9 最低支持 `.NET 8`，无需同步升级运行时框架。 |
| 是否需要修改 `TargetFramework`？ | **不需要**（保持 `net8.0` 即可）。如需 .NET 9 新特性可单独评估。 |
| 升级风险等级 | 低（仅升级 NuGet 包版本）。 |

---

## 2. 已升级的 NuGet 包

| 包名 | 升级前 | 升级后 |
|------|--------|--------|
| `Microsoft.EntityFrameworkCore` | 8.0.23 | **9.0.14** |
| `Microsoft.EntityFrameworkCore.Design` | 8.0.23 | **9.0.14** |
| `Microsoft.EntityFrameworkCore.SqlServer` | 8.0.23 | **9.0.14** |
| `Pomelo.EntityFrameworkCore.MySql` | 8.0.3 | **9.0.0** |
| `Pomelo.EntityFrameworkCore.MySql.NetTopologySuite` | *(传递依赖 8.0.3)* | **9.0.0**（显式覆盖） |
| `EFCore.Sharding.MySql` | 8.0.9 | **9.0.10** |
| `EFCore.Sharding.SqlServer` | 8.0.9 | **9.0.10** |

> **注意**：`Pomelo.EntityFrameworkCore.MySql` 9.0.0 最低支持 MySQL 8.0+。若生产数据库为 MySQL 5.7，升级前须确认版本兼容性。

---

## 3. 升级操作说明（已执行）

### 步骤 1：升级 Infrastructure 项目的 NuGet 包

`Zeye.Sorting.Hub.Infrastructure/Zeye.Sorting.Hub.Infrastructure.csproj` 已按上表更新所有包版本。

同时添加了 `Pomelo.EntityFrameworkCore.MySql.NetTopologySuite 9.0.0` 以覆盖 `EFCore.Sharding.MySql` 引入的旧版传递依赖，消除 NU1608 警告。

### 步骤 2：更新迁移快照 ProductVersion

`SortingHubDbContextModelSnapshot.cs` 及 `20260316184030_InitialCreate.Designer.cs` 中的 `ProductVersion` 注解已从 `8.0.23` 更新为 `9.0.14`，使其与已安装的 EF Core 版本保持一致。

### 步骤 3：集成 `HasPendingModelChanges()` 守卫

`DatabaseInitializerHostedService.AssertMigrationConsistencyAsync()` 已增加第三项校验：

```csharp
// 检查 3（EF Core 9+）：检测代码模型是否存在尚未生成迁移的变更
if (db.Database.HasPendingModelChanges()) {
    _logger.LogCritical(
        "[CodeFirst 守卫] 检测到代码模型存在尚未生成迁移的变更（HasPendingModelChanges=true）。" +
        "当前实体模型与最新迁移快照不一致，请执行 'dotnet ef migrations add <名称>' 生成新迁移，" +
        "以维护 CodeFirst 原则。Provider={Provider}",
        _dialect.ProviderName);
}
```

`HasPendingModelChanges()` 通过对比当前 `DbContext` 的实体模型与最新迁移快照（`ModelSnapshot`），检测出手工修改实体类/配置后遗漏 `dotnet ef migrations add` 的情况——这是 EF Core 8 所不具备的模型级一致性检测能力。

---

## 4. EF Core 8 → 9 重要变更说明

| 变更点 | 影响 | 应对措施 |
|--------|------|----------|
| `HasPendingModelChanges()` 新增 | ✅ 正向新增 | 已集成至 `AssertMigrationConsistencyAsync()` |
| `OwnsOne`/`OwnsMany` 映射规则微调 | 低风险 | 已通过全量测试验证，无影响 |
| JSON 列支持增强 | 无影响 | 本项目未使用 JSON 列类型 |
| Pomelo 9 MySQL 最低版本要求 | ⚠️ 需确认 | 确保 MySQL ≥ 8.0 |
| `Microsoft.Data.SqlClient` 依赖版本 | 低风险 | 随 EFCore.SqlServer 自动更新 |

---

## 5. 回滚方案

若升级后出现问题，将所有包版本恢复为升级前的值（见上表"升级前"列），重新 `dotnet restore` 即可恢复。

---

## 6. 升级核查清单

- [x] 所有 EF Core 相关包升级至 9.0.x  
- [x] Pomelo ≥ 9.0.0，生产 MySQL ≥ 8.0  
- [x] `dotnet build` 无 warning/error  
- [x] 迁移快照 `ProductVersion` 更新至 `9.0.14`  
- [x] `dotnet test` 全部通过  
- [x] `DatabaseInitializerHostedService.AssertMigrationConsistencyAsync()` 集成 `HasPendingModelChanges()`  
- [x] `EFCore9-UpgradePlan.md` 状态更新为"已完成"
