# Copilot 执行指令：Zeye.Sorting.Hub MinIO 接入

> 用途：本文件是 **给 Copilot 直接阅读并执行** 的指令文档。  
> 目标：让 Copilot 在尽量少的上下文下，按固定断点完成 MinIO 接入，不跑偏、不偷跑、不重复造轮子。  
> 配套文档：如需完整人类说明，参考 `MinIO对象存储接入多PR实施方案与Copilot严格门禁.md`。  

---

## 0. 执行模式

每次只执行 **一个 PR 阶段**。

Copilot 在开始编码前，必须先确认：

1. 当前要做的是哪个 PR 阶段。
2. 只允许修改哪些目录/文件。
3. 哪些内容明确禁止实现。
4. 完成后必须同步哪些文档与基线文件。

如果没有明确当前 PR 阶段，默认停止，不得自行跨阶段实现。

---

## 1. 总目标

为当前项目增加 **MinIO 对象存储能力**，用于：

- 图片对象存储
- 通用文件对象存储预留
- Multipart 断点续传
- Parcel 图片绑定

同时保持以下前提：

- 不破坏当前 Parcel 主链路
- 不破坏数据库底座能力
- 不把图片/文件二进制写入数据库
- 不改现有视频模型语义
- 不引入 UI / JWT / RBAC / 硬件控制

---

## 2. 当前事实

Copilot 必须基于以下事实工作：

1. `ImageInfo` 当前保存的是元数据 + `RelativePath`，不是图片字节流。
2. `VideoInfo` 当前保存的是 `Channel + NvrSerialNumber + NodeType`，更接近外部录像系统索引。
3. 当前仓库没有 MinIO / S3 SDK 接入。
4. 当前仓库没有统一 `IObjectStorage` 抽象。
5. 当前 `WebRequestAuditLog` 支持请求/响应体采集，大文件上传不能直接沿用当前大体量审计路径。
6. 当前项目所有新增时间字段必须使用 **本地时间语义**，禁止 UTC。

---

## 3. 全局硬约束

## 3.1 禁止实现

以下内容在所有阶段都禁止：

1. 不新增 UI。
2. 不新增 JWT / RBAC / API-Key。
3. 不新增硬件控制、PLC、IO、扫码枪、相机驱动逻辑。
4. 不把当前系统改成文件网关。
5. 不把图片或文件二进制保存到数据库。
6. 不修改当前 `VideoInfo` 语义为对象存储模型。
7. 不在 Domain 层出现 MinIO、S3、Bucket、SDK 类型。
8. 不在 Host 路由里直接调用 MinIO SDK。
9. 不把 Multipart 会话状态只保存在内存。
10. 不使用 UTC/offset 时间语义。

## 3.2 强制规则

1. 新增 C# 代码优先用 `var`。
2. 所有新枚举项都带 `[Description("中文说明")]`。
3. 注释、日志、异常、ProblemDetails 一律中文。
4. 所有新增时间字段使用本地时间语义，命名应体现 `Local`。
5. Route 只负责接线，不做业务编排。
6. MinIO SDK 只能放在 `Infrastructure` 和 `Host.Tests`。

---

## 4. 总体架构

目标架构固定为：

```text
调用方
  ↓
Host API（创建上传会话 / 确认完成 / 绑定业务）
  ↓
MinIO（对象本体）
  ↓
Database（只存对象元数据与业务关联）
```

默认采用 **预签名直传**：

- Host 不代理大文件流
- 调用方直传 MinIO
- Host 只负责签发上传参数和校验完成状态

Multipart 断点续传必须采用 **持久化上传会话**：

- 会话状态入库
- 服务重启后可继续
- 分片完成状态可查询
- 支持完成/中止

---

## 5. 图片模型固定演进规则

对 `ImageInfo` 的演进策略固定如下：

1. 保留历史字段 `RelativePath`
2. 新增 MinIO 元数据字段
3. 读取时“新字段优先，旧字段兜底”
4. 首批 PR 不删除 `RelativePath`

建议字段：

```text
StorageProvider
BucketName
ObjectKey
ContentType
ObjectSizeBytes
ETag
Sha256
UploadedAtLocal
OriginalFileName
```

新写入要求：

1. 走 MinIO 的新图片必须写 `StorageProvider/BucketName/ObjectKey`
2. 可以允许 `RelativePath` 为空
3. 不允许同时保存“绝对磁盘路径 + 对象键”作为两个真相

---

## 6. 视频模型固定规则

`VideoInfo` 当前 **不改**。

仅允许：

- 保持现有字段和映射
- 在文档或测试中明确“视频当前不是 MinIO 对象模型”

禁止：

- 给 `VideoInfo` 增加 `BucketName` / `ObjectKey`
- 借 MinIO 改造顺手推动视频对象化

---

## 7. 上传审计固定规则

上传相关路由必须满足以下至少一条：

1. 不采集请求/响应体
2. 或根本不让 Host 持有大文件流，只做预签名直传

推荐默认做法：

- **只做预签名直传**
- **不接 `IFormFile` 大文件代理上传**

如果某阶段出现 `IFormFile` 上传实现，视为偏离目标，必须回退。

---

## 8. 配置固定规则

建议配置节固定为：

```json
{
  "ObjectStorage": {
    "Provider": "Minio",
    "Minio": {
      "IsEnabled": true,
      "Endpoint": "minio.internal.local:9000",
      "UseSsl": false,
      "AccessKey": "${MINIO_ACCESS_KEY}",
      "SecretKey": "${MINIO_SECRET_KEY}",
      "ParcelImagesBucket": "sorting-hub-parcel-images",
      "GenericFilesBucket": "sorting-hub-files",
      "PresignedUploadExpireSeconds": 900,
      "PresignedReadExpireSeconds": 300,
      "MultipartPartExpireSeconds": 900,
      "Bootstrap": {
        "EnsureBucketsExist": false,
        "DryRun": true,
        "EnableGuard": true,
        "AllowDangerousActionExecution": false
      }
    }
  }
}
```

配置硬约束：

1. `AccessKey` / `SecretKey` 只能是占位符。
2. 必须使用 `ValidateOnStart()`。
3. 不允许真实生产密钥出现在仓库文本文件中。
4. `EnsureBucketsExist` 只能在守卫 + dry-run 机制下接入。

---

## 9. PR 拆分

以下 PR 顺序固定，不允许跳过依赖阶段偷跑。

## PR-A：图片对象存储元数据骨架

### 目标

只完成图片对象存储字段骨架，不接 MinIO SDK。

### 允许修改

- `Domain`
- `Contracts`
- `Infrastructure/EntityConfigurations`
- `Infrastructure/Persistence/Migrations`
- `Host.Tests`

### 必做

1. 新增 `ObjectStorageProvider` 枚举。
2. 扩展 `ImageInfo`。
3. 扩展 `ImageInfoResponse`。
4. 补 EF 映射与迁移。
5. 补回归测试。

### 禁止

1. 不接 MinIO 包。
2. 不加上传 API。
3. 不改 `VideoInfo`。

### 完成后输出

```text
PR-A 完成
已完成：
- ...
未开始：
- PR-B ...
关键文件：
- ...
```

---

## PR-B：对象存储抽象与 Options

### 目标

只完成抽象层和配置模型，不接真实 MinIO 实现。

### 允许修改

- `Application/Abstractions`
- `Application/Services/ObjectStorage`
- `Host/Options`
- `Host.Tests`

### 必做

1. 新增 `IObjectStorageService`
2. 新增单对象上传会话模型
3. 新增 Multipart 上传会话模型
4. 新增 `ObjectStorageOptions` / `MinioOptions`
5. 配置校验测试

### 禁止

1. 不创建 MinIO Client
2. 不新增数据库表
3. 不新增路由

---

## PR-C：Infrastructure MinIO 实现

### 目标

接入 MinIO SDK 并完成 Infrastructure 实现。

### 允许修改

- `Infrastructure/*.csproj`
- `Infrastructure/ObjectStorage`
- `Infrastructure/DependencyInjection`
- `Host/Program.cs`
- `Host/appsettings*.json`
- `Host.Tests`

### 必做

1. MinIO 包只加到 Infrastructure
2. 新增 `MinioObjectStorageService`
3. 实现：
   - 单对象上传预签名
   - 读预签名
   - Multipart 创建
   - Part 预签名
   - Complete
   - Abort
   - 对象存在性探测
4. DI 注册
5. 配置与异常处理

### 禁止

1. 不新增 API
2. 不改 Domain
3. 不记录密钥到日志

---

## PR-D：上传会话持久化

### 目标

让 Multipart 断点续传会话可入库、可重启恢复。

### 允许修改

- `Domain/Aggregates`
- `Domain/Repositories`
- `Application/Services/ObjectStorage`
- `Infrastructure/Repositories`
- `Infrastructure/EntityConfigurations`
- `Infrastructure/Persistence/Migrations`
- `Host.Tests`

### 必做

1. 新增上传会话聚合/实体
2. 保存：
   - UploadSessionId
   - BucketName
   - ObjectKey
   - UploadId
   - 状态
   - 已确认分片
   - 过期时间
   - CreatedAtLocal / CompletedAtLocal
3. 仓储实现
4. 迁移
5. 测试

### 禁止

1. 不只用内存保存会话
2. 不新增前端逻辑

---

## PR-E：对象存储 API

### 目标

对外暴露对象存储会话能力 API。

### 允许修改

- `Contracts/Models/ObjectStorage`
- `Application/Services/ObjectStorage`
- `Host/Routing/ObjectStorageApiRouteExtensions.cs`
- `Host.Tests`

### 必做

1. 创建单对象上传会话 API
2. 创建 Multipart 会话 API
3. Part 预签名 API
4. Complete API
5. Abort API
6. Read URL API
7. Swagger 文档

### 禁止

1. 不接 `IFormFile`
2. 不做 Parcel 图片绑定

---

## PR-F：Parcel 图片绑定

### 目标

把已完成上传的图片对象绑定到 Parcel。

### 允许修改

- `Contracts/Models/Parcels`
- `Application/Services/Parcels`
- `Host/Routing/ParcelImageApiRouteExtensions.cs` 或 `ParcelAdminApiRouteExtensions.cs`
- `Host.Tests`

### 必做

1. 新增 `POST /api/admin/parcels/{id}/images`
2. 校验 Parcel 存在
3. 校验上传会话存在且已完成
4. 校验对象存在性
5. `Parcel.AddImageInfo(...)`
6. 测试成功/失败路径

### 禁止

1. 不回写 `CreateParcel` 主接口
2. 不跳过上传会话直接绑对象

---

## PR-G：审计豁免与严格门禁

### 目标

防止 MinIO 接入破坏当前审计、门禁和配置安全。

### 允许修改

- `.github/scripts`
- `.github/workflows`
- `Host/Middleware`
- `Host.Tests`

### 必做

1. 上传路由请求体审计豁免或等效保护
2. 新增规则测试：
   - MinIO SDK 分层限制
   - 不允许媒体二进制入库
   - appsettings 只允许占位密钥
   - README 与基线同步
3. 扩展门禁脚本

### 禁止

1. 不新增与 MinIO 无关的门禁膨胀
2. 不把规则散落成多个影分身脚本

---

## PR-H：运行资料与历史回填预案

### 目标

把方案收口为可运行、可演练、可续跑资产。

### 允许修改

- `*.md`
- `performance/`
- `Host.Tests`

### 必做

1. 补 MinIO 运维与联调说明
2. 补 Multipart 演练步骤
3. 补历史 `RelativePath` 回填 Runbook
4. 补压测建议

### 禁止

1. 不在这一阶段顺手做真实大规模历史回填
2. 不新增无验证价值的空文档

---

## 10. 文件同步要求

每次新增/删除文件后，必须同步更新：

1. `README.md`
2. `更新记录.md`
3. `检查台账/文件清单基线.txt`

如果当前阶段新增了规则测试或门禁脚本，也必须同步更新对应文档说明。

---

## 11. 测试要求

每个阶段至少保证：

1. `dotnet build Zeye.Sorting.Hub.sln`
2. 与本阶段直接相关的 `Host.Tests` 测试
3. 若涉及迁移，则验证迁移文件生成和模型快照一致

如果测试未运行，必须明确写出“未运行”的原因，不得假设通过。

---

## 12. 输出格式

Copilot 每次完成一个阶段后，必须输出固定摘要：

```text
当前阶段：PR-X

已完成：
- ...

未完成：
- ...

未做原因：
- ...

关键变更文件：
- ...

验证：
- 已运行 ...
- 未运行 ...

下一阶段入口：
- PR-Y ...
```

---

## 13. 一句话总提示词

如果只能给 Copilot 一段最短指令，使用下面这段：

```text
按《Copilot-MinIO接入执行指令.md》执行当前指定 PR 阶段，只允许做该阶段任务，不得跨阶段偷跑；MinIO SDK 只能放 Infrastructure；数据库不得存图片/文件二进制；图片先兼容 RelativePath 再演进；视频模型不改；上传默认预签名直传；Multipart 会话必须持久化；所有时间使用本地时间；完成后同步 README.md、更新记录.md、检查台账/文件清单基线.txt，并输出固定断点摘要。
```
