# Fusion 在线接入管理

来源：本仓库的 FusionConfigurationService、FusionIngestionHub、FusionIngestionOptions 及 `deploy/compose.yaml` 的实际实现和配置约定。

Hub 前端的“系统管理 → Fusion 接入”支持接入开关、对外 SignalR 地址、开发 HTTP、UDP 发现、传输限额及多个工作台的在线登记、停用和密钥轮换。管理员账号需拥有 access.manage 权限。目录保存到 ManagedDocuments，不需手工维护部署 JSON；启动时首次导入静态目录，后续数据库为准。

1. 本机 Docker 对外地址填写 http://host.docker.internal:5087/hubs/fusion-ingestion，明确允许开发 HTTP。现场部署填写 Fusion 实际可达的 HTTPS 域名或 IP。
2. 新增工作台，来源标识每台独立，产线/时区与 Fusion 保持一致。站点/设备可留空。复制一次性配对 JSON。
3. 在 Fusion → 参数配置 → Hub 导入配对信息，校验预览后保存。首次身份设置需重启，之后启用接入。密钥、地址、发现设置和传输限额支持热更新。
4. “测试连接”检查当前草稿的认证和身份，不创建租约；实际在线状态依据正式连接心跳。一个 Hub 可登记多个独立 Fusion。

误建且显示“尚未接入”的工作台可在列表操作栏点击“删除”，确认后在线移除登记和配对凭据，工作台概览同步更新。已接入或存在心跳、处理事实、图片记录的来源不能删除，可使用“编辑 / 停用”；历史数据始终保留。删除请求使用 `DELETE /api/operations/configuration/fusion/sources/{id}?revision={当前目录版本}`，需要接入管理权限，版本冲突不会覆盖其他修改。

固定 Hub 身份及图片目录由部署控制，已接入工作台的业务归属固定。停用/轮换仅撤销目标工作台的旧租约，其他来源不受影响。机器密钥只在登记/轮换的本次响应中显示，普通读取和实时快照不包含密钥，管理请求/响应正文不进入审计。密钥加密保存到数据库，必须同时备份数据库和 /app/logs/data-protection 密钥环；多个 Hub 实例共用目录时必须共享密钥环。

主 Compose 默认仅将 UDP 47651 发布到本机，UDP 服务开启后才响应已登记来源。局域网部署通过 ZEYE_FUSION_UDP_BIND 和 ZEYE_FUSION_UDP_PORT 发布实际地址及端口，修改监听端口时需同步 Docker 映射。5089 保留给 NarrowBeltSorter，不能用于此接入。跨子网/Docker 桥接不保证广播可达，可在 Fusion 填写单播目标或固定 SignalR 地址。

管理 HTTP 接口为 /api/operations/configuration/fusion，写入必须携带 revision，版本冲突返回 409。SignalR 的只读 CheckFusionConfiguration 接收来源、Hub、产线、时区和可选站点/设备编码，返回 matched、hubId、sourceInstanceId、mismatches；它与原有六个业务方法兼容。包裹唯一身份仍为来源、计数周期、分拣机包裹 Id 的三元组，条码不参与唯一约束。
