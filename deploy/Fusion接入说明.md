# Fusion 工作台接入 Hub

来源：`Zeye.SortingFusionService/docs/fusion-hub-protocol.md`、发送端 `HubProtocol.cs` 合同、本项目实现及跨平台运行验证。

本项目实现 Fusion 1.0 接收端，协议依据 `Zeye.SortingFusionService/docs/fusion-hub-protocol.md` 及发送端 `Zeye.SortingFusionService.Contracts/Hub/HubProtocol.cs`。设备配置尚未提供，因此默认来源目录为空，UDP 发现关闭；部署不会自动启用或修改 Fusion 发送端。

## 配置来源

每台工作台登记独立的 `SourceInstanceId`、`LineId` 和随机 `MachineApiKey`。密钥至少 32 字符，最多 256 字符，不得复用其他工作台或网页初始化密钥。机器注册必须与 Hub 中的 `HubId`、产线、站点、设备及 `TimeZoneId` 完全一致。当前业务模型的来源和归属编码上限为 96 个字符，接受字母、数字、点、短横线及下划线。

`TenantId`、`StoragePartitionId` 和展示名称由 Hub 登记，不接受来源报文覆盖。租户与分区目前记录在接收簿中，业务投影继续使用本项目现有的包裹路由；它们不会自动创建独立租户库或切换数据库。这些归属不参与包裹身份：包裹以来源、计数周期、设备包裹编号三元组区分。同条码、新发送数据库、设备计数重置均有独立明确语义。大整数仍使用十进制字符串。

Windows 一体化发布后，将 `fusion-ingestion.example.json` 中的 `FusionIngestion` 节合并进发布目录的 `appsettings.Production.json`，保留现有数据库和账号配置。图片目录默认为程序目录下的 `fusion-images`，运行账号必须有写入权限。启动程序即可同时提供页面、API 和两种 SignalR 通道。

Docker 使用可选叠加文件：

```powershell
Copy-Item deploy/fusion-ingestion.example.json deploy/fusion-ingestion.json
# 编辑登记身份、独立随机密钥及可达地址后执行。
docker compose --env-file deploy/.env -f deploy/compose.yaml -f deploy/compose.fusion.yaml up -d
```

`fusion-ingestion.json` 已被 Git 忽略，只读挂载为 `appsettings.LocalDocker.json`。基础 Compose 使用 `ZEYE_FUSION_HUB_ID` 配置稳定 Hub 编码，默认 `sorting-hub`；环境变量优先于 JSON。更改登记信息需重启 Host。完整图片和未完成分块使用 `host_fusion_images` 持久卷，与数据库一同备份；不能在升级时删除该卷。仅为本地受控 HTTP 联调设置 `AllowInsecureHttp=true`，正式接入使用 HTTPS。

## 通信与确认

机器路径是 `/hubs/fusion-ingestion`，支持 WebSocket 和长轮询。请求同时带 `Authorization: Bearer <独立机器密钥>` 及 `X-Fusion-SourceId`；网页 Cookie、URL 中的令牌和旧 HTTP 机器密钥不能进入这个 Hub。网页业务继续使用 `/hubs/sorting`，两个入口的权限及消息预算独立。

实现的方法为 `RegisterFusion`、`PublishFacts`、`Heartbeat`、`BeginImageUpload`、`UploadImageChunk` 和 `CompleteImageUpload`。默认每批最多 50 条、524288 UTF-8 字节，图片块为解码后 32768 字节，机器通道接收上限 1 MiB，网页通道保留 16 KiB。图片最大 128 MiB，每来源最多 100 个尚未完成上传，上传租约跟随当前机器连接。

事实原文 SHA-256 按原始字符串的 UTF-8 字节核验。来源、发送库、事实编号和序号使用独立唯一约束；相同内容返回 `duplicate`，变更内容返回 `conflict`，格式或不支持类型返回 `rejected`，未持久化返回 `retryable`。不存在根据最高序号隐式确认其他事实的行为。

原文和可恢复投影任务在同一次数据库保存中提交后才返回 `stored`。后台通过已有业务用例幂等更新包裹及处理记录；异常、重启或乱序不会删除已确认原文。检测时间只来自真实检测事实，实际落格才标记完成，HTTP 200 或 Provider 操作完成不能推断业务成功。未绑定 DWS 保持未绑定；无法安全关联的图片及设备事实保留原文，不能凭条码猜测归属。量测使用克和毫米，可空值保留未知。

图片先确认描述，再按持久偏移上传。分块刷盘且数据库偏移保存后才确认；重放块需要逐字节相同。断线重新注册并调用 Begin 后可续传。完整大小、SHA-256、对象存在和元数据提交均成功才返回 `LocalDurable` 存储凭据；这与外部 Provider 的图片上传结果分开。图片关联可早于或晚于上传。同一包裹的多个图片在现有图库中展示，图片读取需要包裹查看权限。

## 工作台状态与追溯

工作台页面显示登记来源，包括还没有包裹的工作台。在线状态来自有效连接及心跳租约，默认 120 秒到期；产线、设备编码、待确认事实、待传图片和累计舍弃计数来自登记及真实心跳。设备连接健康没有单独事实时仍保持未知，不能以服务在线代替设备在线。

- `GET /api/parcels/fusion/sources`：工作台快照，可通过网页 SignalR 读取和订阅。
- `GET /api/diagnostics/fusion/facts?sourceInstanceId=fusion-line-01&limit=50`：原始事实、摘要、投影状态、错误和中心包裹编号，可追加 `journalId`；仅超级管理员或内置超级用户可读，最多 200 条。
- `GET /api/parcels/fusion/images/{安全对象键}/content`：完整图片流，需要包裹查看权限，不暴露磁盘路径。

启动自动创建/升级 `FusionSourceLeases`、`FusionJournalHeartbeats`、`FusionFactReceipts`、`FusionImageUploads`，包裹继续复用已有自动物理分表与索引治理。MySQL 和 SQL Server 均有对应模型迁移，正常部署无需手动执行迁移。不能通过回滚这次结构迁移恢复业务数据；回滚前必须先导出新增事实及图片元数据。

图片临时分块默认 24 小时过期，完整对象和明确关联不会由临时清理删除。原始接收簿独立于包裹清理，保留已确认事实供追溯。当前默认单 Host 部署；多 Host 必须使用同一耐久数据库、共享图片存储和一致时区，数据库唯一键及租约版本保护跨进程竞争，网页推送的共享消息总线仍需额外部署。

## 可选发现

登记来源后才可开启 `DiscoveryEnabled`，设置 Fusion 实际可达的 `/hubs/fusion-ingestion` 地址。默认 IPv4 UDP 端口 47651，与已有 5089 服务分开；可选 Compose 默认只绑定回环地址，局域网发现需把 `ZEYE_FUSION_UDP_BIND` 设置为指定网卡地址并配置防火墙。

Docker 的发现地址应填写宿主机或代理对工作台可达的地址。即使宿主机以回环地址发送单播，容器也可能通过 NAT 看到非回环来源；此时不能配置 `localhost` 或 `127.0.0.1` 作为应答地址。跨网络广播不通时使用单播或固定 Endpoint。

发现只处理不超过 4096 字节的有效协议包，使用来源独立密钥按八字段和换行计算 HMAC-SHA256。应答回显 nonce、来源、Hub 及过期时间；显式错误协议、超期、错误签名、未登记来源和外部请求的回环地址均不响应。全局每秒最多处理 100 个包，每来源每分钟最多返回 20 个应答，UDP 不携带业务事实或机器密钥。

发现端口暂时被占用时记录日志并每 30 秒重试，网页和已知地址的机器接收入口继续提供服务。无效的发现配置仍会明确报错。
