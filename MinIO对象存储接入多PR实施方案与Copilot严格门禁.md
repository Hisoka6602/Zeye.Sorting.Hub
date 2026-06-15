# Zeye.Sorting.Hub MinIO 对象存储接入多 PR 实施方案与 Copilot 严格门禁

> 适用仓库：`Zeye.Sorting.Hub`  
> 目标：在不破坏当前 Parcel 主链路、数据库底座能力与长期运行边界的前提下，引入 MinIO 作为图片 / 文件对象存储底座，并为后续业务模块提供统一复用入口。  
> 特别要求：**支持 Copilot 断点续跑**、**支持资源上传断点续传（Multipart）**、**支持严格门禁**、**尽量降低后续 Copilot 多轮反复沟通成本**。  
> 当前阶段优先级：**图片对象存储 > 通用文件对象存储 > 大文件断点续传 > 历史数据回填 > 旧字段清理**。  

---

## 0. 结论先行

当前项目**适合接入 MinIO**，但不建议采用“把所有二进制流量都先打进当前 Host，再由 Host 代理上传”的方式。

推荐目标架构：

```text
调用方 / 业务模块
        ↓
Host API（只负责签发上传会话、校验元数据、绑定业务关系）
        ↓
MinIO（对象本体存储）
        ↓
Database（仅保存对象元数据、对象键、业务关联）
```

推荐分两阶段实施：

1. **第一阶段：MinIO 底座 + 图片元数据接入**
   - 先完成对象存储抽象、配置、图片元数据模型、预签名上传、Parcel 图片绑定。
   - 不让数据库存图片二进制。
   - 不先改视频链路。

2. **第二阶段：Multipart 断点续传 + 通用文件对象存储**
   - 在保持第一阶段能力不回退的前提下，为大文件引入 multipart upload session / 分片签名 / 完成 / 中止能力。
   - 将“断点续传”能力设计成底座能力，而不是单个业务模块私有实现。

---

## 1. 当前现状判断

## 1.1 已有资源模型

当前仓库中，图片与视频都不是“二进制正文入库”模式：

- `ImageInfo` 仅保存图片元数据与 `RelativePath`，不保存字节流。
- `VideoInfo` 仅保存 `Channel`、`NvrSerialNumber`、`NodeType`，更接近“外部视频系统定位信息”。
- 审计详情表会记录 `FileMetadataJson` / `ImageMetadataJson`，但这是元数据 JSON，不是对象正文。

这意味着当前项目**天然适合演进到对象存储模式**，因为数据库本来就只承担“索引与元数据”职责。

## 1.2 当前限制

当前项目还没有以下能力：

- 没有 `IObjectStorage` / `IFileStorage` 之类统一抽象。
- 没有 MinIO / S3 兼容 SDK 接入。
- 没有图片上传 API。
- 没有图片对象与 Parcel 绑定的显式命令接口。
- 没有 Multipart 断点续传会话模型。
- `WebRequestAuditLog` 当前默认支持请求/响应体采集；若后续直接接 Multipart 上传流量，必须先做上传路由的采集豁免，否则会把大文件审计采集问题引入系统。

## 1.3 当前不建议直接改造的视频链路

视频当前仅保存：

- `Channel`
- `NvrSerialNumber`
- `NodeType`

这类模型更像“录像系统索引”，不是对象文件元数据。因此当前阶段**不建议强行把视频改成 MinIO 文件模型**。除非后续明确需要：

- 存储短视频片段
- 存储可下载录像文件
- 做跨系统回放缓存

否则本次方案**只处理图片与通用文件，不处理现有视频模型**。

---

## 2. 总体架构决策

## 2.1 目标边界

本次 MinIO 接入只允许实现以下能力：

1. 对象存储抽象层
2. MinIO 基础设施实现
3. 图片对象元数据入库
4. 通用文件对象元数据约定
5. 预签名上传 / 下载能力
6. Multipart 断点续传会话能力
7. 与 Parcel 的图片关联能力
8. 严格门禁、测试、文档、运行说明

## 2.2 当前阶段禁止项

所有 PR 均不得顺手引入以下内容：

1. 不新增 UI。
2. 不新增 JWT / RBAC / API-Key 鉴权体系。
3. 不把当前系统改成“文件网关”。
4. 不把图片二进制写入数据库。
5. 不把视频链路强制改造成对象存储。
6. 不在 Host 中直接堆叠大量 MinIO SDK 调用逻辑。
7. 不绕过 Application 层从 Route 直接操作 MinIO 客户端。
8. 不在 Domain 层出现 MinIO、S3、Bucket、SDK 细节。
9. 不把 Multipart 会话状态仅保存在内存中。
10. 不使用 UTC 语义。

---

## 3. 推荐目标架构

## 3.1 分层放置

### Domain 层

只允许放：

- 图片元数据值对象
- 对象存储提供器枚举
- 上传会话领域只读模型契约（如确有必要）
- Parcel 与图片绑定规则

禁止：

- MinIO SDK
- Bucket 创建逻辑
- 预签名 URL 生成
- HTTP 上传/下载协议细节

### Application 层

只允许放：

- 上传会话创建编排
- Multipart 分片签名编排
- 上传完成确认
- Parcel 图片绑定命令
- 读 URL 预签名编排
- 参数守卫、合同映射

### Infrastructure 层

只允许放：

- MinIO 客户端实现
- 对象键生成策略
- 上传会话仓储实现
- EF Core 映射与迁移
- Bucket 自检 / DryRun / 守卫

### Host 层

只允许放：

- `Routing/ObjectStorageApiRouteExtensions.cs`
- `Routing/ParcelImageApiRouteExtensions.cs`
- Options 注册与 Swagger 说明
- 针对上传路由的审计采集豁免接线

---

## 4. 数据模型设计

## 4.1 图片模型演进原则

### 原则

1. **先兼容、后收敛**。
2. 历史 `RelativePath` 数据先保留，不做首批大迁移。
3. 新写入数据统一走 MinIO 字段。
4. 读取时遵循“新字段优先，旧字段兜底”的兼容策略。

### `ImageInfo` 建议新增字段

在不立刻删除 `RelativePath` 的前提下，为 `ImageInfo` 增加以下字段：

```text
StorageProvider        对象存储提供器（LocalFileSystem / Minio）
BucketName             Bucket 名称
ObjectKey              对象键
ContentType            内容类型
ObjectSizeBytes        对象大小（字节）
ETag                   对象 ETag
Sha256                 可选内容摘要
UploadedAtLocal        上传完成时间（本地时间）
OriginalFileName       原始文件名（可选）
```

### 兼容读取规则

1. 若 `BucketName + ObjectKey` 非空，则视为 MinIO 对象。
2. 若 MinIO 字段为空而 `RelativePath` 非空，则视为历史本地/共享盘路径。
3. 首批 PR 不删除 `RelativePath`。

### 新写入规则

1. 新接入 MinIO 的图片记录必须写入：
   - `StorageProvider = Minio`
   - `BucketName`
   - `ObjectKey`
2. 新数据允许 `RelativePath` 为空字符串。
3. 严禁出现“同时把对象键和绝对磁盘路径都写进去”的双重真相。

## 4.2 视频模型处理原则

本次不修改 `VideoInfo` 的领域语义与数据库结构。

仅允许：

- 保持原样
- 在文档中明确“视频当前不是对象存储模型”

禁止：

- 在本次改造中给 `VideoInfo` 强行追加 `BucketName/ObjectKey`

## 4.3 通用文件元数据约定

当前仓库还没有正式的“通用文件聚合”。因此本次先统一**元数据形状**，不急于引入完整业务模块。

建议统一的文件元数据字段：

```text
StorageProvider
BucketName
ObjectKey
ContentType
ObjectSizeBytes
ETag
Sha256
OriginalFileName
UploadedAtLocal
BusinessTag
```

## 4.4 审计元数据 JSON 约定

当前审计中的 `FileMetadataJson` / `ImageMetadataJson` 建议统一输出如下结构：

```json
{
  "storageProvider": "Minio",
  "bucketName": "sorting-hub-parcel-images",
  "objectKey": "images/2026/06/15/123456/topcam/abc.jpg",
  "contentType": "image/jpeg",
  "objectSizeBytes": 284391,
  "etag": "e3b0c442...",
  "originalFileName": "topcam-001.jpg"
}
```

首批允许保留字符串 JSON，不要求立即拆成结构化表。

---

## 5. 对象键设计

## 5.1 图片对象键规范

推荐对象键格式：

```text
images/{yyyy}/{MM}/{dd}/{parcelId}/{cameraSerialNumber}/{guid}-{imageType}.{ext}
```

示例：

```text
images/2026/06/15/123456/CAM-A001/3f4d0f2c-topscan.jpg
```

## 5.2 通用文件对象键规范

```text
files/{yyyy}/{MM}/{dd}/{businessTag}/{guid}-{safeFileName}
```

## 5.3 强制规则

1. 不允许把 AccessKey、Secret、租户敏感信息写进对象键。
2. 不允许直接使用用户原始文件名作为完整对象键。
3. 不允许出现空格、反斜杠、双点路径穿越。
4. 对象键一律使用 `/`。
5. 对象键必须可由纯函数生成，避免同一输入产生不稳定结果。

---

## 6. 配置设计

建议新增配置节：

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
      "Region": "",
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

## 6.1 强制规则

1. `AccessKey` / `SecretKey` 仅允许占位符，不允许真实值入库。
2. `EnsureBucketsExist=true` 时必须带守卫 / dry-run / 审计。
3. 不允许在 `appsettings*.json` 写死生产 Endpoint、真实密钥、真实 Bucket 策略凭据。
4. 配置项必须支持 `ValidateOnStart()`。

---

## 7. API 设计

## 7.1 第一阶段：单对象预签名上传

### 接口 1：创建图片上传会话

```text
POST /api/object-storage/images/upload-sessions
```

用途：

- 由业务方申请一个图片上传会话
- 系统生成 `bucket + objectKey + presignedPutUrl`
- 上传动作由调用方直传 MinIO，不经过 Host 持有文件流

建议请求合同：

```json
{
  "parcelId": 123456,
  "cameraSerialNumber": "CAM-A001",
  "imageType": 1,
  "contentType": "image/jpeg",
  "fileName": "topcam-001.jpg",
  "fileSizeBytes": 284391,
  "customName": "TopCam"
}
```

建议响应合同：

```json
{
  "uploadSessionId": "guid",
  "bucketName": "sorting-hub-parcel-images",
  "objectKey": "images/2026/06/15/123456/CAM-A001/3f4d0f2c-topscan.jpg",
  "presignedPutUrl": "https://...",
  "expireAtLocal": "2026-06-15 10:15:00",
  "headers": {
    "Content-Type": "image/jpeg"
  }
}
```

### 接口 2：确认图片上传完成并绑定到 Parcel

```text
POST /api/admin/parcels/{id}/images
```

用途：

- 调用方完成 MinIO 上传后，通知业务系统写入图片元数据
- 应用层校验会话、对象存在性、大小、ETag，再绑定到 Parcel

建议请求合同：

```json
{
  "uploadSessionId": "guid",
  "cameraName": "Basler A2A",
  "customName": "TopCam",
  "cameraSerialNumber": "CAM-A001",
  "imageType": 1,
  "captureType": 1
}
```

### 接口 3：获取图片读取签名

```text
POST /api/object-storage/read-sessions
```

用途：

- 根据 `bucket + objectKey` 生成短期读 URL
- 避免把对象存储内部地址长期暴露给前端

---

## 7.2 第二阶段：Multipart 断点续传

当对象可能超过单次 PUT 合理阈值时，引入 multipart 会话。

### 接口 4：创建 Multipart 上传会话

```text
POST /api/object-storage/multipart-sessions
```

响应需返回：

- `uploadSessionId`
- `storageProvider`
- `bucketName`
- `objectKey`
- `uploadId`
- `partSizeBytes`
- `expireAtLocal`

### 接口 5：获取分片上传签名

```text
POST /api/object-storage/multipart-sessions/{uploadSessionId}/parts/{partNumber}:presign
```

### 接口 6：完成 Multipart 上传

```text
POST /api/object-storage/multipart-sessions/{uploadSessionId}:complete
```

### 接口 7：中止 Multipart 上传

```text
POST /api/object-storage/multipart-sessions/{uploadSessionId}:abort
```

## 7.3 关于“断点续传”的明确说明

本方案中的“支持断点续传”指两层含义：

1. **实现计划可断点续跑**：每个 PR 都有明确断点摘要与下一入口。
2. **资源上传可断点续传**：通过 Multipart Upload Session 持久化状态，支持中断后继续上传剩余分片，而不是从头重传。

---

## 8. 审计与大流量保护

## 8.1 为什么不能直接把上传路由照搬进现有审计模型

当前 `WebRequestAuditLog` 默认允许采集请求/响应体。若直接把大文件 `multipart/form-data` 上传接到现有 Host：

1. 审计体积会迅速膨胀
2. 内存与磁盘压力会放大
3. 性能基线会失真
4. 大文件失败重试成本过高

## 8.2 强制改造要求

在引入任何上传路由前，必须先完成以下之一：

1. 为对象上传路由增加“请求/响应体采集豁免”
2. 或将上传流量设计为“只签发预签名 URL，不让 Host 代理二进制流”

本方案推荐 **第 2 条** 作为默认路径。

---

## 9. 数据库与迁移策略

## 9.1 首批迁移原则

首批只允许做**增量迁移**：

1. 给 `Parcel_ImageInfos` 增加 MinIO 元数据列
2. 不删除 `RelativePath`
3. 不批量改写历史数据
4. 不触碰 `Parcel_VideoInfos`

## 9.2 历史数据迁移策略

建议单独作为后续 PR：

1. 扫描历史 `RelativePath`
2. 判断对应文件是否仍存在
3. 若文件仍存在，则异步迁移到 MinIO
4. 回填 `BucketName/ObjectKey/...`
5. 迁移完成后再评估清理旧路径字段

## 9.3 首批禁止事项

1. 不在首批 PR 就删除 `RelativePath`
2. 不做“迁移脚本 + 上传对象 + 回填数据库”大一锅实现
3. 不把对象迁移工具放到 Host 主进程自动执行

---

## 10. 多 PR 实施拆分

以下拆分目标：**每个 PR 单一职责、可验证、可回滚、可中断续跑**。

## PR-A：对象存储领域模型与兼容元数据骨架

### 目标

建立图片对象存储元数据模型，但不接 MinIO SDK。

### 允许修改

- `Zeye.Sorting.Hub.Domain/Enums/`
- `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/ImageInfo.cs`
- `Zeye.Sorting.Hub.Contracts/Models/Parcels/ValueObjects/ImageInfoResponse.cs`
- `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/ParcelEntityTypeConfiguration.cs`
- `Zeye.Sorting.Hub.Infrastructure/Persistence/Migrations/`
- `Zeye.Sorting.Hub.Host.Tests/`

### 必做项

1. 新增 `ObjectStorageProvider` 枚举。
2. 为 `ImageInfo` 增加 MinIO 元数据字段。
3. 为 `ImageInfoResponse` 增加对应响应字段。
4. EF 映射与迁移同步补齐。
5. 增加回归测试，验证：
   - 历史 `RelativePath` 仍可读
   - 新字段可持久化
   - 不影响 `ParcelDetailResponse`

### 禁止项

1. 不引入 MinIO 包。
2. 不新增上传 API。
3. 不改视频模型。

### 完成标准

- `dotnet build` 通过
- 新增迁移通过
- 现有 Parcel 详情路径不回退

### Copilot 执行提示词

```text
当前仅实现 PR-A：
1. 只做图片对象存储元数据骨架，不接 MinIO SDK。
2. 不改 VideoInfo，不新增上传接口。
3. 允许修改 Domain/Contracts/Infrastructure/Migrations/Tests 中与 ImageInfo 直接相关文件。
4. 保持 RelativePath 向后兼容，新字段优先为未来 MinIO 做准备。
5. 完成后同步 README.md、更新记录.md、检查台账/文件清单基线.txt。
```

---

## PR-B：Application 抽象与配置骨架

### 目标

建立对象存储抽象、Options、校验规则，不接具体 MinIO 实现。

### 允许新增

- `Zeye.Sorting.Hub.Application/Abstractions/ObjectStorage/`
- `Zeye.Sorting.Hub.Application/Services/ObjectStorage/`
- `Zeye.Sorting.Hub.Host/Options/`
- `Zeye.Sorting.Hub.Host.Tests/`

### 必做项

1. 新增 `IObjectStorageService` 抽象。
2. 新增会话模型：
   - 单对象上传会话
   - Multipart 上传会话
3. 新增 `ObjectStorageOptions` / `MinioOptions`。
4. 使用 `ValidateOnStart()` 做配置校验。
5. 约束密钥只能以占位符方式出现在 `appsettings*.json`。

### 禁止项

1. 不直接 new MinIO Client。
2. 不在 Host Route 中写业务编排。

### 完成标准

- 抽象接口与配置模型稳定
- 测试覆盖非法配置与默认值

### Copilot 执行提示词

```text
当前仅实现 PR-B：
1. 只建立 Application 抽象和 Options 骨架，不接真实 MinIO SDK。
2. Multipart 会话模型必须先定义好，避免后续返工。
3. 不新增数据库表，不新增路由。
4. 完成后同步 README.md、更新记录.md、检查台账/文件清单基线.txt。
```

---

## PR-C：Infrastructure MinIO 实现与 DI 接线

### 目标

将 MinIO SDK 接入 Infrastructure，并通过 DI 暴露 `IObjectStorageService` 实现。

### 允许修改

- `Zeye.Sorting.Hub.Infrastructure/*.csproj`
- `Zeye.Sorting.Hub.Infrastructure/DependencyInjection/`
- `Zeye.Sorting.Hub.Infrastructure/ObjectStorage/`（新目录）
- `Zeye.Sorting.Hub.Host/Program.cs`
- `Zeye.Sorting.Hub.Host/appsettings*.json`
- `Zeye.Sorting.Hub.Host.Tests/`

### 必做项

1. 仅在 Infrastructure 引入 MinIO 包。
2. 新增 `MinioObjectStorageService`。
3. 注册 DI。
4. 实现：
   - 生成单对象上传预签名
   - 生成读预签名
   - 创建 multipart upload
   - 生成 part 上传签名
   - complete / abort multipart
   - 对象存在性探测
5. 对 MinIO endpoint / bucket / 密钥配置做启动期校验。

### 禁止项

1. 不在 Domain / Contracts / Host 直接引用 MinIO SDK 命名空间。
2. 不把 AccessKey / SecretKey 记录到日志。

### 完成标准

- Fake/Stub 测试通过
- DI 接线稳定
- 不影响现有数据库能力

### Copilot 执行提示词

```text
当前仅实现 PR-C：
1. MinIO 包只能放在 Infrastructure 项目。
2. 需要完整实现预签名和 multipart 基础能力。
3. 不新增 API 路由，不做 Parcel 绑定。
4. 所有异常、日志必须使用中文，且不得打印密钥。
```

---

## PR-D：上传会话表与断点续传状态持久化

### 目标

让 Multipart 会话可持久化，支持服务重启后续跑。

### 建议新增模块

```text
Domain
└── Aggregates/ObjectStorageUploadSessions

Application
└── Services/ObjectStorage

Infrastructure
├── EntityConfigurations/ObjectStorageUploadSessionEntityTypeConfiguration.cs
└── Repositories/ObjectStorageUploadSessionRepository.cs
```

### 必做项

1. 新增上传会话聚合 / 实体。
2. 保存：
   - UploadSessionId
   - BucketName
   - ObjectKey
   - UploadId
   - 状态
   - 已签发分片
   - 已确认分片
   - 过期时间
   - 创建时间 / 完成时间（本地时间）
3. 新增仓储、映射、迁移、测试。

### 为什么必须单独一个 PR

因为“断点续传”的真正关键不是 SDK，而是**会话状态持久化**。如果只靠内存，服务重启后就无法续跑。

### Copilot 执行提示词

```text
当前仅实现 PR-D：
1. 目标是让 multipart 上传会话具备数据库持久化能力。
2. 会话状态必须可在进程重启后恢复。
3. 不做前端，不做真实上传接口。
4. 所有新增时间字段必须使用本地时间语义并体现 Local 命名。
```

---

## PR-E：对象存储 API 路由（上传会话 / 分片签名 / 完成 / 中止 / 读签名）

### 目标

对外暴露对象存储能力 API，但仍不改 Parcel 业务写接口。

### 建议新增文件

- `Zeye.Sorting.Hub.Contracts/Models/ObjectStorage/`
- `Zeye.Sorting.Hub.Application/Services/ObjectStorage/`
- `Zeye.Sorting.Hub.Host/Routing/ObjectStorageApiRouteExtensions.cs`
- `Zeye.Sorting.Hub.Host.Tests/ObjectStorageApiTests.cs`

### 必做项

1. 创建单对象上传会话 API
2. 创建 multipart 上传会话 API
3. 分片签名 API
4. 完成上传 API
5. 中止上传 API
6. 读取签名 API
7. 完整 Swagger 摘要 / 描述 / 错误响应

### 禁止项

1. 不在这一 PR 把图片直接绑定到 Parcel。
2. 不引入 `IFormFile` 上传代理。

### Copilot 执行提示词

```text
当前仅实现 PR-E：
1. 只做对象存储 API，不做 Parcel 绑定。
2. 路由必须独立扩展类，不允许把实现堆到 Program.cs。
3. 不接收大文件流，只返回预签名信息。
4. API 文档必须明确本地时间语义、过期时间、错误码。
```

---

## PR-F：Parcel 图片绑定接口

### 目标

在对象上传完成后，将图片对象元数据与 Parcel 业务记录绑定。

### 必做项

1. 新增 `POST /api/admin/parcels/{id}/images`
2. 新增图片绑定请求合同
3. 新增应用服务：
   - 校验 Parcel 存在
   - 校验上传会话存在且已完成
   - 校验对象存在性与元数据一致性
   - `Parcel.AddImageInfo(...)`
4. 返回新的图片明细响应
5. 测试覆盖：
   - Parcel 不存在
   - 上传会话不存在
   - 上传未完成
   - 对象不存在
   - 成功绑定

### 特别说明

当前 `ParcelCreateRequest` 不包含图片元数据列表，因此**不建议把图片绑定逻辑强塞回 `CreateParcel` 主接口**。应作为独立命令接口落地，降低主链路复杂度。

### Copilot 执行提示词

```text
当前仅实现 PR-F：
1. 只做 Parcel 图片绑定接口，不回写 CreateParcel 主接口。
2. 绑定前必须校验上传会话状态和对象存在性。
3. 所有失败路径返回稳定错误码和中文 ProblemDetails。
```

---

## PR-G：上传审计豁免与严格门禁

### 目标

避免大文件流量破坏当前审计与门禁基线。

### 必做项

1. 为对象上传相关路由增加审计体采集豁免机制。
2. 新增规则测试：
   - MinIO 包只能出现在 Infrastructure / Host.Tests
   - `ImageInfo` 新字段完整存在
   - 不允许新增图片二进制列 / `byte[]` 媒体正文列
   - `appsettings*.json` 中 MinIO 密钥只能是占位符
   - README 已登记 MinIO 方案文档与新增代码文件
3. 扩展脚本：
   - `validate-sensitive-config.sh`
   - `validate-database-foundation-rules.sh`
4. 在 `stability-gates.yml` 中增加对象存储相关上下文校验

### 推荐新增测试文件

- `Zeye.Sorting.Hub.Host.Tests/MinioIntegrationRulesTests.cs`

### Copilot 执行提示词

```text
当前仅实现 PR-G：
1. 重点是门禁和审计保护，不做业务新功能。
2. 必须阻断明文 MinIO 密钥、错误分层放置、媒体二进制入库回归。
3. 所有规则测试应以读取仓库文件方式实现，避免脆弱字符串匹配散落多处。
```

---

## PR-H：运行文档、压测补充、历史数据回填预案

### 目标

把 MinIO 接入收口为可运行、可演练、可续跑的资产。

### 必做项

1. 新增或补充运行文档：
   - MinIO 部署前置条件
   - Bucket 准备规范
   - 预签名上传联调步骤
   - Multipart 断点续传演练步骤
   - 故障排查
2. 增加压测建议：
   - 预签名会话创建 QPS
   - Multipart 分片签名并发
   - Parcel 图片绑定延迟
3. 提供历史 `RelativePath` 回填 Runbook

### 说明

若历史回填实际执行风险较高，可把“真实回填脚本”继续延后，但 Runbook 和预案必须先落地。

---

## 11. 严格门禁要求

## 11.1 CI 门禁新增要求

本方案实施期间，每个 PR 都必须继续通过：

- `database-foundation-gates.yml`
- `stability-gates.yml`
- `copilot-instructions-validation.yml`

并建议新增以下最小门禁：

1. **MinIO 分层门禁**
   - `Minio` SDK 只能出现在 `Infrastructure/` 与 `Host.Tests/`

2. **配置安全门禁**
   - `appsettings*.json` 中 `AccessKey/SecretKey` 只能是占位符

3. **媒体二进制门禁**
   - 禁止向 EF 实体新增图片/文件二进制正文列

4. **兼容读取门禁**
   - 读取图片时必须遵循“新字段优先、旧字段兜底”

5. **上传审计保护门禁**
   - 对象上传路由不得开启大请求体采集

## 11.2 代码级强制规则

1. 所有新增时间字段使用本地时间语义。
2. 所有新枚举带 `[Description("中文说明")]`。
3. 所有新异常、日志、ProblemDetails 使用中文。
4. 不允许在 Host 直接调用 MinIO SDK。
5. 不允许跳过 Application 层进行对象绑定。
6. 不允许直接在 API 中接 `byte[]` 作为图片上传主体。

---

## 12. 断点续跑机制

## 12.1 Copilot 实施断点续跑

每个实施 PR 完成后，必须在 `更新记录.md` 追加以下结构：

```text
## PR-X 断点摘要

### 已完成
- ...

### 保留能力
- ...

### 未开始
- ...

### 下一 PR 入口
- ...

### 关键文件
- ...
```

并建议同步新增：

```text
检查台账/PR-MinIO接入A-检查台账.md
检查台账/PR-MinIO接入B-检查台账.md
...
```

## 12.2 资源上传断点续传

Multipart 会话必须满足：

1. 分片上传状态入库
2. 可查询哪些分片已完成
3. 服务重启后可继续签发剩余分片
4. 支持超时后中止
5. 支持完成后禁止重复提交

---

## 13. 推荐的最小落地顺序

若希望先尽快拿到可用能力，建议按以下顺序执行：

1. PR-A 图片对象元数据骨架
2. PR-B 抽象与配置骨架
3. PR-C MinIO Infrastructure 实现
4. PR-D Multipart 会话持久化
5. PR-E 对象存储 API
6. PR-F Parcel 图片绑定
7. PR-G 严格门禁
8. PR-H 文档、演练、回填预案

---

## 14. 当前阶段最重要的取舍

1. **先做图片，不动视频**
   - 避免把 NVR 引用模型和对象存储模型混在一起。

2. **先做预签名直传，不做 Host 代理文件流**
   - 避免当前审计、内存、网络路径被大对象拖垮。

3. **先兼容 `RelativePath`，后清理**
   - 降低一次性数据迁移与回归风险。

4. **先做上传会话持久化，再谈断点续传**
   - 断点续传本质是“状态机能力”，不是“多几个 API”。

---

## 15. 最终验收标准

当以下条件全部满足时，才可认为 MinIO 接入一期完成：

1. Parcel 图片支持 MinIO 元数据持久化。
2. 支持创建预签名上传会话。
3. 支持 Multipart 分片上传、完成、中止与续跑。
4. 支持将已上传对象绑定到 Parcel。
5. 不把图片/文件二进制写入数据库。
6. 上传相关路由不会触发大请求体审计采集。
7. 所有新增配置安全、门禁、测试、README、更新记录、文件清单基线同步完成。

---

## 16. 后续明确可延后事项

以下能力可以在一期完成后再做：

1. 历史 `RelativePath` 真正回填 MinIO
2. 图片对象生命周期自动清理
3. 通用文件独立业务模块
4. 视频片段对象化
5. 基于对象事件的异步后处理（缩略图、AI 识别、OCR）

---

## 17. 给 Copilot 的总提示词（推荐直接复制）

```text
当前正在按《MinIO对象存储接入多PR实施方案与Copilot严格门禁.md》实施 Zeye.Sorting.Hub 的 MinIO 接入。

强制要求：
1. 严格按当前 PR 目标执行，不得跨 PR 偷跑。
2. 不得引入 UI、JWT、RBAC、硬件控制、视频对象化改造。
3. MinIO SDK 只能出现在 Infrastructure 和 Host.Tests。
4. 数据库不得存图片/文件二进制。
5. 先兼容 RelativePath，读取时新字段优先、旧字段兜底。
6. 上传必须优先走预签名直传，不能默认做 Host 代理文件流。
7. Multipart 断点续传必须有数据库会话持久化，不能只存在内存。
8. 每次新增/删除文件后，必须同步 README.md、更新记录.md、检查台账/文件清单基线.txt。
9. 所有异常、日志、ProblemDetails 使用中文；所有时间使用本地时间语义。
10. 完成当前 PR 后，输出“断点摘要”和“下一 PR 入口”。
```
