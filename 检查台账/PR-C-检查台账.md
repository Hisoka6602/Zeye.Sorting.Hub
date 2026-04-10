# PR-C 检查台账：`Zeye.Sorting.Hub.Application/` + `Zeye.Sorting.Hub.Contracts/`

> **批次说明**：第三批次，覆盖 Application 应用层（服务、工具）及 Contracts 合同层（请求/响应 DTO），依据 copilot-instructions.md 全量规则执行逐文件审查。
> **基线版本**：`ef1389e`
> **检查时间**：2026-04-09
> **检查人**：Copilot

---

## 一、本批次覆盖文件列表（与基线映射）

| 序号 | 文件路径 | 基线是否存在 |
|------|----------|-------------|
| 1 | `Zeye.Sorting.Hub.Application/Services/AuditLogs/GetWebRequestAuditLogByIdQueryService.cs` | ✅ |
| 2 | `Zeye.Sorting.Hub.Application/Services/AuditLogs/GetWebRequestAuditLogPagedQueryService.cs` | ✅ |
| 3 | `Zeye.Sorting.Hub.Application/Services/AuditLogs/WebRequestAuditLogContractMapper.cs` | ✅ |
| 4 | `Zeye.Sorting.Hub.Application/Services/AuditLogs/WriteWebRequestAuditLogCommandService.cs` | ✅ |
| 5 | `Zeye.Sorting.Hub.Application/Services/Parcels/CleanupExpiredParcelsCommandService.cs` | ✅ |
| 6 | `Zeye.Sorting.Hub.Application/Services/Parcels/CreateParcelCommandService.cs` | ✅ |
| 7 | `Zeye.Sorting.Hub.Application/Services/Parcels/DeleteParcelCommandService.cs` | ✅ |
| 8 | `Zeye.Sorting.Hub.Application/Services/Parcels/GetAdjacentParcelsQueryService.cs` | ✅ |
| 9 | `Zeye.Sorting.Hub.Application/Services/Parcels/GetParcelByIdQueryService.cs` | ✅ |
| 10 | `Zeye.Sorting.Hub.Application/Services/Parcels/GetParcelPagedQueryService.cs` | ✅ |
| 11 | `Zeye.Sorting.Hub.Application/Services/Parcels/ParcelContractMapper.cs` | ✅ |
| 12 | `Zeye.Sorting.Hub.Application/Services/Parcels/ParcelResponseArgs.cs` | ✅ |
| 13 | `Zeye.Sorting.Hub.Application/Services/Parcels/UpdateParcelStatusCommandService.cs` | ✅ |
| 14 | `Zeye.Sorting.Hub.Application/Utilities/EnumGuard.cs` | ✅ |
| 15 | `Zeye.Sorting.Hub.Application/Utilities/Guard.cs` | ✅ |
| 16 | `Zeye.Sorting.Hub.Application/Zeye.Sorting.Hub.Application.csproj` | ✅ |
| 17 | `Zeye.Sorting.Hub.Contracts/Models/AuditLogs/WebRequests/WebRequestAuditLogDetailResponse.cs` | ✅ |
| 18 | `Zeye.Sorting.Hub.Contracts/Models/AuditLogs/WebRequests/WebRequestAuditLogListItemResponse.cs` | ✅ |
| 19 | `Zeye.Sorting.Hub.Contracts/Models/AuditLogs/WebRequests/WebRequestAuditLogListRequest.cs` | ✅ |
| 20 | `Zeye.Sorting.Hub.Contracts/Models/AuditLogs/WebRequests/WebRequestAuditLogListResponse.cs` | ✅ |
| 21 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/Admin/ParcelCleanupExpiredRequest.cs` | ✅ |
| 22 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/Admin/ParcelCleanupExpiredResponse.cs` | ✅ |
| 23 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/Admin/ParcelCreateRequest.cs` | ✅ |
| 24 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/Admin/ParcelUpdateRequest.cs` | ✅ |
| 25 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ParcelAdjacentRequest.cs` | ✅ |
| 26 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ParcelAdjacentResponse.cs` | ✅ |
| 27 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ParcelDetailResponse.cs` | ✅ |
| 28 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ParcelListItemResponse.cs` | ✅ |
| 29 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ParcelListRequest.cs` | ✅ |
| 30 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ParcelListResponse.cs` | ✅ |
| 31 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/ApiRequestInfoResponse.cs` | ✅ |
| 32 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/BagInfoResponse.cs` | ✅ |
| 33 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/BarCodeInfoResponse.cs` | ✅ |
| 34 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/ChuteInfoResponse.cs` | ✅ |
| 35 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/CommandInfoResponse.cs` | ✅ |
| 36 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/GrayDetectorInfoResponse.cs` | ✅ |
| 37 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/ImageInfoResponse.cs` | ✅ |
| 38 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/ParcelDeviceInfoResponse.cs` | ✅ |
| 39 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/ParcelPositionInfoResponse.cs` | ✅ |
| 40 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/SorterCarrierInfoResponse.cs` | ✅ |
| 41 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/StickingParcelInfoResponse.cs` | ✅ |
| 42 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/VideoInfoResponse.cs` | ✅ |
| 43 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/VolumeInfoResponse.cs` | ✅ |
| 44 | `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/WeightInfoResponse.cs` | ✅ |
| 45 | `Zeye.Sorting.Hub.Contracts/Zeye.Sorting.Hub.Contracts.csproj` | ✅ |

---

## 二、逐文件检查台账（本批次增量）

| 文件路径 | 检查状态 | 问题数(P0/P1/P2) | 主要问题标签 | 证据位置 | 建议修复PR | 检查时间/版本 |
|----------|----------|-----------------|-------------|---------|-----------|-------------|
| `Application/.../GetWebRequestAuditLogByIdQueryService.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Application/.../GetWebRequestAuditLogPagedQueryService.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Application/.../WebRequestAuditLogContractMapper.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Application/.../WriteWebRequestAuditLogCommandService.cs` | ❌ 有问题 | 0/2/1 | 单层转发，无异常日志，命名空间风格 | L4,L29-33 | PR-FIX-C1 | 2026-04-09 / ef1389e |
| `Application/.../CleanupExpiredParcelsCommandService.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Application/.../CreateParcelCommandService.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Application/.../DeleteParcelCommandService.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Application/.../GetAdjacentParcelsQueryService.cs` | ⚠️ 有问题 | 0/0/2 | 业务异常误级 Error，全限定名内嵌 | L59,L68-76 | PR-FIX-C2 | 2026-04-09 / ef1389e |
| `Application/.../GetParcelByIdQueryService.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Application/.../GetParcelPagedQueryService.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Application/.../ParcelContractMapper.cs` | ❌ 有问题 | 0/1/0 | 影分身代码 | L154-227 | PR-FIX-C1 | 2026-04-09 / ef1389e |
| `Application/.../ParcelResponseArgs.cs` | ⚠️ 有问题 | 0/0/1 | 中间层冗余 | L6 | PR-FIX-C1 | 2026-04-09 / ef1389e |
| `Application/.../UpdateParcelStatusCommandService.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Application/Utilities/EnumGuard.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Application/Utilities/Guard.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Application/Zeye.Sorting.Hub.Application.csproj` | ⚠️ 有问题 | 0/0/1 | 空 Folder 占位冗余 | L12-16 | PR-FIX-C2 | 2026-04-09 / ef1389e |
| `Contracts/.../WebRequestAuditLogDetailResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../WebRequestAuditLogListItemResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../WebRequestAuditLogListRequest.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../WebRequestAuditLogListResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ParcelCleanupExpiredRequest.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ParcelCleanupExpiredResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ParcelCreateRequest.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ParcelUpdateRequest.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ParcelAdjacentRequest.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ParcelAdjacentResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ParcelDetailResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ParcelListItemResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ParcelListRequest.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ParcelListResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ApiRequestInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../BagInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../BarCodeInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ChuteInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../CommandInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../GrayDetectorInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ImageInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ParcelDeviceInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../ParcelPositionInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../SorterCarrierInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../StickingParcelInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../VideoInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../VolumeInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/.../WeightInfoResponse.cs` | ✅ 通过 | 0/0/0 | — | — | — | 2026-04-09 / ef1389e |
| `Contracts/Zeye.Sorting.Hub.Contracts.csproj` | ⚠️ 有问题 | 0/0/1 | 空 Folder 占位冗余 | L12-14 | PR-FIX-C2 | 2026-04-09 / ef1389e |

---

## 三、问题清单

### P0 问题（0条）

本批次未发现 P0 级别问题。

---

### P1 问题（3条）

---

#### C-P1-01：`WriteWebRequestAuditLogCommandService.WriteAsync` 是纯单层调用转发，违反禁止转发方法规则

- **文件**：`Zeye.Sorting.Hub.Application/Services/AuditLogs/WriteWebRequestAuditLogCommandService.cs`
- **行号**：L29–33
- **证据**：
  ```csharp
  public Task<Domain.Repositories.Models.Results.RepositoryResult> WriteAsync(
      WebRequestAuditLog auditLog,
      CancellationToken cancellationToken) {
      return _webRequestAuditLogRepository.AddAsync(auditLog, cancellationToken);
  }
  ```
  方法体仅有一行，直接透传到仓储 `AddAsync`，无参数校验、无日志、无异常捕获、无业务语义增强，与同层的 `CreateParcelCommandService`（含 null 检查、枚举校验、错误日志、结果判断）结构天差地别。
- **违反规则**：「禁止新增只做一层调用转发的方法」
- **影响**：调用方传入 `null` 的 `auditLog` 或仓储层抛出异常时，上层无法获得任何日志线索，排查困难。
- **建议修复**：补充 `null` 检查（`ArgumentNullException`）、异常捕获与 NLog 日志记录，与其他 CommandService 对齐。
- **分级**：P1
- **建议修复 PR**：PR-FIX-C1

---

#### C-P1-02：`WriteWebRequestAuditLogCommandService` 缺少异常日志记录，违反"所有异常必须输出日志"规则

- **文件**：`Zeye.Sorting.Hub.Application/Services/AuditLogs/WriteWebRequestAuditLogCommandService.cs`
- **行号**：L9–34（整个类）
- **证据**：类中不存在 `private static readonly ILogger`，`WriteAsync` 内无任何 `try/catch` 块，与 `GetWebRequestAuditLogByIdQueryService`（L15 定义 Logger，L43-46 有 Error 日志）相比完全缺失日志防护。
- **违反规则**：「所有的异常都必须输出日志」
- **影响**：若仓储层（EF Core/DB 驱动）抛出异常，异常会静默向上冒泡，审计写入失败无任何落盘记录，无法在运维监控中发现问题。
- **建议修复**：类中增加 `NLogLogger` 静态字段，`WriteAsync` 中增加 `try/catch` 并记录 Error 日志后重新抛出，与 `CreateParcelCommandService` 等保持一致。
- **分级**：P1
- **建议修复 PR**：PR-FIX-C1

---

#### C-P1-03：`ParcelContractMapper.BuildFromReadModel` 与 `BuildFromAggregate` 是影分身代码

- **文件**：`Zeye.Sorting.Hub.Application/Services/Parcels/ParcelContractMapper.cs`
- **行号**：L154–187（`BuildFromReadModel`），L189–227（`BuildFromAggregate`）
- **证据**：两个私有静态方法结构完全一致，均将 25 个同名属性逐一赋值给 `ParcelResponseArgs`，差异仅在形参类型（`ParcelSummaryReadModel` vs `Parcel`）。任意新增/删除一个字段，必须同时修改两处，极易遗漏。
  ```csharp
  // BuildFromReadModel (L154-187) - 25 字段赋值
  private static ParcelResponseArgs BuildFromReadModel(ParcelSummaryReadModel readModel) {
      return new ParcelResponseArgs {
          Id = readModel.Id, CreatedTime = readModel.CreatedTime, ... // 25 项
      };
  }
  // BuildFromAggregate (L189-227) - 完全相同的 25 字段赋值
  private static ParcelResponseArgs BuildFromAggregate(Parcel parcel) {
      return new ParcelResponseArgs {
          Id = parcel.Id, CreatedTime = parcel.CreatedTime, ... // 25 项
      };
  }
  ```
- **违反规则**：「全局禁止代码重复（影分身代码/复制粘贴代码）」
- **影响**：后续字段变更（如新增 `Remark` 字段）必须同时维护两处，遗漏其一将导致列表页与详情页数据不一致，且编译器不会报错。
- **建议修复**：提取统一接口 `IParcelSummaryFields`（仅声明两者共同的 25 个属性），让 `ParcelSummaryReadModel` 和 `Parcel` 均实现该接口，合并为一个 `private static ParcelResponseArgs BuildArgs(IParcelSummaryFields source)` 方法。若引入接口有侵入性顾虑（Domain 不得引用 Application），可将接口定义在 `Domain/Projections/` 或通过 `Func<>` 委托传递属性提取器来消除重复。
- **分级**：P1
- **建议修复 PR**：PR-FIX-C1

---

### P2 问题（5条）

---

#### C-P2-01：`WriteWebRequestAuditLogCommandService` 命名空间使用花括号包裹风格，与全项目文件级语法不一致

- **文件**：`Zeye.Sorting.Hub.Application/Services/AuditLogs/WriteWebRequestAuditLogCommandService.cs`
- **行号**：L4–35
- **证据**：
  ```csharp
  namespace Zeye.Sorting.Hub.Application.Services.AuditLogs {
      // ...
  }
  ```
  项目所有其他 C# 文件（同目录的 `GetWebRequestAuditLogByIdQueryService.cs`、`GetWebRequestAuditLogPagedQueryService.cs` 等）均使用 C# 10 文件级 namespace（`namespace X.Y.Z;`），此文件单独使用块级语法，风格不统一。
- **违反规则**：代码风格一致性（非硬性规则，但影响可读性与合并冲突概率）
- **建议修复**：改为文件级 namespace 语法。
- **分级**：P2
- **建议修复 PR**：PR-FIX-C1

---

#### C-P2-02：`GetAdjacentParcelsQueryService.ExecuteAsync` catch 块将业务性 `KeyNotFoundException` 记录为 Error，日志级别误判

- **文件**：`Zeye.Sorting.Hub.Application/Services/Parcels/GetAdjacentParcelsQueryService.cs`
- **行号**：L55–57（抛出位置），L68–76（catch 位置）
- **证据**：
  ```csharp
  // L55-57
  if (!adjacentResult.IsSuccess) {
      throw new KeyNotFoundException(adjacentResult.ErrorMessage);
  }
  // L68-76
  catch (Exception ex) {
      Logger.Error(ex, "查询 Parcel 邻近记录失败，..."); // 将业务"锚点不存在"也记录为 Error
      throw;
  }
  ```
  "锚点包裹不存在"属于正常业务边界，不是系统错误，被记录为 `Error` 会干扰监控告警阈值，与其他服务（如 `GetParcelByIdQueryService` 返回 `null` 而非异常）的设计也不一致。
- **影响**：误报 Error 日志，监控大盘告警敏感度下降；同时若仓储通过 `IsSuccess=false` 表达"未找到"，日志条目会呈 Error 级，运维难以区分真实故障。
- **建议修复**：将"锚点不存在"的 `KeyNotFoundException` 排除于 catch 外（`when (ex is not KeyNotFoundException)`），或在内部判断后用 `Logger.Warn` 记录，再重新抛出；或参考 `GetParcelByIdQueryService` 模式改为返回空结果而非抛出异常。
- **分级**：P2
- **建议修复 PR**：PR-FIX-C2

---

#### C-P2-03：`GetAdjacentParcelsQueryService.ExecuteAsync` 内嵌全限定类型名，未使用 `using` 引入

- **文件**：`Zeye.Sorting.Hub.Application/Services/Parcels/GetAdjacentParcelsQueryService.cs`
- **行号**：L59
- **证据**：
  ```csharp
  var adjacent = adjacentResult.Value ?? Array.Empty<Domain.Repositories.Models.ReadModels.ParcelSummaryReadModel>();
  ```
  `Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels.ParcelSummaryReadModel` 全限定名直接写在业务逻辑代码中，而非通过文件顶部的 `using` 引入。同文件中其他类型（`IParcelRepository`、`ParcelAdjacentRequest` 等）均通过 `using` 引入。
- **违反规则**：代码可读性规范（全限定名嵌入业务逻辑降低可读性）
- **建议修复**：在文件顶部添加 `using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;`，将 L59 改为 `Array.Empty<ParcelSummaryReadModel>()`。
- **分级**：P2
- **建议修复 PR**：PR-FIX-C2

---

#### C-P2-04：`Application.csproj` 包含多个空 Folder 占位项，与实际目录结构不符

- **文件**：`Zeye.Sorting.Hub.Application/Zeye.Sorting.Hub.Application.csproj`
- **行号**：L12–16
- **证据**：
  ```xml
  <ItemGroup>
    <Folder Include="DTOs\Commands\" />
    <Folder Include="DTOs\Queries\" />
    <Folder Include="Validators\" />
    <Folder Include="Services\" />
    <Folder Include="Notifications\" />
  </ItemGroup>
  ```
  `DTOs\Commands/`、`DTOs\Queries/`、`Validators/`、`Notifications/` 均不存在于 git 版本控制中（`git ls-files` 无对应条目），这些是早期规划遗留的空占位，实际代码已直接放在 `Services/` 和 `Utilities/` 下。
- **影响**：IDE（Rider/VS）会在解决方案中显示不存在的虚拟目录，混淆目录结构认知；新成员误以为应将代码放入这些不存在的目录。
- **建议修复**：删除不存在目录的 `<Folder>` 条目，若有规划目录则先建立实际文件后再添加。
- **分级**：P2
- **建议修复 PR**：PR-FIX-C2

---

#### C-P2-05：`Contracts.csproj` 包含空 Folder 占位项，与实际目录结构不符

- **文件**：`Zeye.Sorting.Hub.Contracts/Zeye.Sorting.Hub.Contracts.csproj`
- **行号**：L12–14
- **证据**：
  ```xml
  <ItemGroup>
    <Folder Include="Enums\" />
    <Folder Include="Events\" />
    <Folder Include="Models\" />
  </ItemGroup>
  ```
  `Enums/` 和 `Events/` 目录不在 git 版本控制中，只有 `Models/` 有实际内容。`Enums` 和 `Events` 目录按规则应属于 `Domain` 层，Contracts 层中提前占位这两个目录存在层级混淆风险。
- **影响**：同 C-P2-04，IDE 中显示不存在目录；且若误将领域枚举放入 Contracts 层，会违反 Application/Domain 层级边界规则。
- **建议修复**：删除 `<Folder Include="Enums\" />` 和 `<Folder Include="Events\" />` 条目（枚举/事件应在 Domain 层）；若 Contracts 确有需要定义合同级枚举，需在 README 中明确说明并建立实际目录后再添加。
- **分级**：P2
- **建议修复 PR**：PR-FIX-C2

---

## 四、未覆盖文件清单

本批次计划 45 个文件已全部检查，无未覆盖文件。

---

## 五、下一批 PR 计划

| PR | 覆盖目录 | 预估文件数 |
|----|---------|----------|
| PR-D | `Zeye.Sorting.Hub.Infrastructure/` | 63 |
| PR-E | `Zeye.Sorting.Hub.Host/` | 43 |
| PR-F | `Zeye.Sorting.Hub.SharedKernel/` + `Zeye.Sorting.Hub.Host.Tests/` + `Zeye.Sorting.Hub.Analytics/` + `Zeye.Sorting.Hub.Realtime/` + `Zeye.Sorting.Hub.RuleEngine/` + 占位子域项目 | 约 58 |

---

## 六、对账结果

- **本PR计划检查文件数**：45
- **本PR实际已检查文件数**：45
- **对账差异**：0 ✅
- **累计已检查文件数**：133 / 287（21 [PR-A] + 67 [PR-B] + 45 [PR-C]；总数 287 来自基线版本 `ef1389e` 的 `git ls-files` 统计）
