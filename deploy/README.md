# Windows / Linux 部署

Fusion 1.0 的来源登记、专用 SignalR 机器认证、图片持久化、可选发现和追溯入口见 [Fusion接入说明.md](Fusion接入说明.md)。默认未登记工作台，来源不会自动启用。

来源：部署命令及参数依据本仓库 `publish-windows.ps1`、`compose.yaml` 和 Host 入口配置；实时协议及客户端行为参考[微软 SignalR 文档](https://learn.microsoft.com/aspnet/core/signalr/javascript-client)。

## Windows 前后端一体化发布

在仓库根目录执行一个命令即可构建并发布前后端：

```powershell
./deploy/publish-windows.ps1
```

构建机需要 .NET 10 SDK 和 Node.js 24/npm。默认输出为 `artifacts/windows-x64`，包含 Windows x64 自包含 Host、前端 `wwwroot`、配置和 `Start-Hub.cmd`。将整个目录一次复制到目标主机，配置数据库后运行 `Start-Hub.cmd` 或 `Zeye.Sorting.Hub.Host.exe`。目标机不需要 Node、Nginx、独立前端服务或另外安装 .NET；数据库继续使用现有 MySQL / SQL Server 连接。

页面、登录、API、包裹图片及健康探针由同一个进程和端口提供，默认入口为 `http://127.0.0.1:5078/`。深层页面刷新也可直接访问。程序从自身目录加载配置和 `wwwroot`，从其他工作目录启动同样有效。默认仅监听 HTTP，无需开发证书；配置服务器证书后可用 `Hosting:Urls` 或 `--urls` 启用 HTTPS。

登录后的包裹列表、概览、报表、规则和平台状态通过同源 `/hubs/sorting` 的 SignalR 通道查询与订阅。处理事实提交与包裹状态更新使用两个明确的实时方法，正文最多 4 KiB，较大请求继续走原 HTTP 入口。成功业务写入会唤醒快照更新；后台状态另有低频检查，前端不再定时发送 HTTP 查询。Windows 内置前端直接连接 Host；Docker Nginx 与 Vite 开发代理均已转发 WebSocket。额外反向代理需同时转发 `/hubs/`、HTTP Upgrade 与 Cookie，并允许长连接。

实时通道复用原接口认证、权限、限流及审计，每次调用重新验证账号；断线自动重连并恢复订阅，JSON 原文保证长编号不失真。写入只发送一次，断线或超时未收到结果时先检查业务记录，不能自动重放。账号、上传、下载、清理和其余管理写入继续使用现有 HTTP 安全流程。融合服务尚未上报的设备在线状态仍显示待接入，不能由浏览器实时连接推断设备在线。客户端参考：[微软 SignalR JavaScript 文档](https://learn.microsoft.com/aspnet/core/signalr/javascript-client)。

原生部署使用已有环境变量或独立的 `appsettings.Production.json` 配置数据库及认证。例如在 PowerShell 中设置实际连接和不同的初始化、设备密钥后启动：

```powershell
$env:ConnectionStrings__MySql = '填写目标数据库的实际连接字符串'
$env:Access__EnforceAuthorization = 'true'
$env:Access__BootstrapKey = '填写本次部署的初始化密钥'
$env:Access__MachineApiKey = '填写独立设备接口密钥'
$env:Persistence__MigrationGovernance__DryRun = 'false'
$env:Persistence__Sharding__WriteRouting__AllowTableCreation = 'true'
$env:Persistence__Sharding__WriteRouting__DryRun = 'false'
$env:Persistence__Sharding__Prebuild__DryRun = 'false'
& ./artifacts/windows-x64/Start-Hub.cmd
```

这些建表设置允许程序自动执行迁移及预建分表，数据库账号需有对应 DDL 权限，生产危险迁移阻断仍遵循已有配置。双击启动时，将上述对应配置写入发布目录中的 `appsettings.Production.json`，无需每次设置环境变量。

首次运行仍需通过页面创建首个管理员，完成后才启用内置超级用户。仅剩内置账号、没有普通成员时，登录页重新提示创建管理员，仍需部署初始化密钥；已有普通成员时不能重复初始化。内置账号不显示在成员列表中，也不计入角色成员数。接口权限和清理密码确认流程保持生效。配置、账号及数据库不放在公开的 `wwwroot` 中；升级时先发布到新的输出目录，再保留目标机部署配置、日志、备份及数据库。

脚本可用 `-OutputDirectory 'D:\Publish\SortingHub'` 改变输出目录；已安装正确前端依赖时可用 `-SkipDependencyRestore` 跳过 `npm ci`，前端构建仍会执行。Visual Studio 可选择 `Windows-x64` 文件夹发布配置，CLI 等价命令如下：

```powershell
dotnet publish Zeye.Sorting.Hub.Host/Zeye.Sorting.Hub.Host.csproj -p:PublishProfile=Windows-x64 -o artifacts/windows-x64
```

Linux 同样默认打包前端：`dotnet publish Zeye.Sorting.Hub.Host -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64`，部署目录完整复制后执行 `chmod +x Zeye.Sorting.Hub.Host`，再运行 `./Zeye.Sorting.Hub.Host`。目标机仍需操作系统要求的原生依赖和可用数据库；运行用户应对发布目录拥有写入权限，以保存日志、会话密钥和治理文件。普通 `dotnet build` / `dotnet test` 不触发 npm；仅发布时构建前端。显式 `-p:BundleWebUi=false` 可保留纯 API 发布能力。

## Docker 部署

Host 镜像同样内置前端，可直接从 Host 端口访问页面；已有 Compose 的 Web 入口继续兼容。

此 Compose 项目使用独立的 MySQL 数据卷，构建 `zeye-sorting-hub-host:local` 和 `zeye-sorting-hub-web:local`，并把 Web 与 API 仅绑定到本机回环地址。运营页面连接真实 API，接口失败时显示错误；备份、分区、系统配置页面展示服务器实际状态及能力边界。MinIO 功能在此本机部署中关闭。

MySQL 默认使用 1 GiB InnoDB 数据页缓存和 3 GiB 容器内存上限，避免默认 128 MiB 缓存在持续写入、大范围事实统计时反复读盘。`ZEYE_MYSQL_BUFFER_POOL_SIZE` 接受 MySQL 大小单位，例如 `256M`、`512M`、`1G`；建议使用 128 MiB 的整数倍，不超过本服务内存预算的一半。`ZEYE_MYSQL_MEMORY_LIMIT` 接受 Compose 内存单位，例如 `1g`、`2g`、`3g`，至少为缓存大小的两倍，并为连接、排序、Host 和其他容器留出空间。低内存机器可使用 `512M` / `2g`。缓存只复用数据库数据页，报表仍由 EF Core 查询实际已提交记录，不缓存业务统计结果；Windows Docker Desktop 和 Linux Docker Engine 使用同一配置。原生 Windows 或 Linux 服务连接外部数据库时，由数据库部署配置同等资源预算。

本机 Host 允许首次写入时创建当前包裹分表；此设置仅在 `deploy/compose.yaml` 的隔离数据库中生效，仓库默认配置仍保持 DDL 预演保护。

本机 Compose 同时开启实际自动备份与滚动预建：未保存自定义策略时默认每 60 分钟创建一次真实备份；后台每分钟按已保存窗口复核当前和下一周期的包裹表及审计日表。审计热表、详情表成对建立，索引同步补齐，已有不兼容结构会明确报错。实际预建必须同时开启物理建表授权并关闭建表预演；普通部署默认仍只输出预演计划。自动备份或窗口可在系统配置中修改，页面保存的策略优先于部署默认值。

运维人员可在部署完成后核验最新实际备份：`docker compose --env-file deploy/.env -f deploy/compose.yaml exec -T host dotnet Zeye.Sorting.Hub.Host.dll --verify-latest-backup`。此命令检查文件长度与 SHA-256，仅恢复到服务端新生成的 `zeye_restore_` 隔离库，并逐表核对备份行数；成功后保留隔离库供复核，不覆盖或切换业务库。没有实际备份或核验失败时命令返回非零。该命令不启动第二个 HTTP 服务，也不改变登录保护。

1. 复制 `.env.example` 为 `.env`，分别设置随机数据库密码、`ZEYE_BOOTSTRAP_KEY`（初始化密钥）和 `ZEYE_MACHINE_API_KEY`（设备写入密钥），各项使用不同的随机值。`.env` 已被 Git 忽略。`ZEYE_AUTH_ENABLED` 接受 `true` / `false`，默认 `true`。
2. 在仓库根目录执行：

   Windows（PowerShell）：

   ```powershell
   ./deploy/start.ps1
   ```

   Linux（安装 Docker Engine、Compose 插件和 curl 后）：

   ```bash
   bash deploy/start.sh
   ```

3. 脚本等待容器、前端和 API 就绪后，使用系统默认浏览器打开数据概览。默认地址为 `http://127.0.0.1:4187/data-overview`；首次访问会转到登录页。输入 `.env` 中的 `ZEYE_BOOTSTRAP_KEY`，自行设置首个管理员账号、姓名及 12~128 位密码。初始化成功后入口自动关闭，后续使用内置账号登录。修改 `.env` 中的 `ZEYE_WEB_PORT` 后，脚本会使用实际发布的端口。工作台位于 `/overview`。API 存活检查为 `http://127.0.0.1:5087/health/live`，数据库就绪检查为 `/health/ready`。

账号、角色和规则保存在 MySQL 的 `ManagedDocuments` 中。Cookie 使用 HttpOnly，登录有频率限制，密码使用随机盐散列；密码重置、角色变更和停用账号会撤销会话。密钥文件保存在原有 `host_logs` 卷中，容器重建后会话仍可验证。前端通过同源 Nginx 转发 API。生产对外发布时由入口代理提供 HTTPS。

设备或 Fusion 客户端提交 `/api/admin/parcels/processing-records` 时，需在 `X-Sorting-Api-Key` 请求头中提供 `ZEYE_MACHINE_API_KEY`。该密钥仅授权这一处理事实接口，不能读取或维护三个敏感版块、账号或规则。未配置此请求头的匿名客户端返回 401；部署切换前应完成客户端配置。

包裹清理历史仅永久保存操作人、清理条件、开始与结束时间、实际删除数和结果；每批保存少量事务提交凭据，不复制逐票编号、条码、图片或业务报文。旧版逐票清单在 Host 启动时自动转换，执行中的操作和数量校验不一致的记录保持原样并记录日志。历史转换复用 `Persistence:RepositoryDangerousActions:ParcelRemoveExpired:Isolator` 的守卫、允许执行和演练开关，阻断或演练均不修改记录。转换前生成 `.rollback.sql.gz`；默认目录为 `governance-artifacts/cleanup-audit-rollback`，Docker 位于 `host_governance` 卷，Windows 位于运行目录。可通过 `Persistence:RepositoryDangerousActions:ParcelRemoveExpired:AuditCompaction:RollbackDirectory` 指定可写目录。历史恢复时先关闭执行或开启演练，再在停服状态解压并执行对应数据库方言的 SQL，避免重新转换或覆盖后续修改。

独立来源身份与处理事实仍按原策略保留，清理汇总不是包裹备份。删除和载荷精简释放的数据库空间可供后续写入复用；本升级不执行需要重建业务表的磁盘压缩操作。

测试数据、数据治理及可观测性必须由固定超级管理员角色或内置超级用户访问。普通自定义角色即使拥有全部单项权限也不能进入这些版块；关闭 `ZEYE_AUTH_ENABLED` 仍保留此限制和超级管理员账号维护保护。`/health/live`、`/health/ready` 保持公开供容器探针使用，`/health/deep` 需要超级管理员身份。HTTP 与 SignalR 共用权限判断，已有登录会话在下一次请求重新验证，无需重建账号。

系统配置中的自动备份、备份间隔和包裹预建窗口可在线保存，重启后保留，后台在一分钟内加载；修改需要账号管理权限。其余部署参数通过服务器配置维护。备份页支持 MySQL 事务表结构及数据快照、受认证下载和隔离恢复核验，文件持久化在 `host_backups` 卷中；恢复只新建 `zeye_restore_` 前缀的数据库，逐表核对行数，不覆盖或切换当前业务库。分区页可实际预建当前及窗口内的包裹主表、关联表并登记目录，重复执行自动跳过。审计日表规划不由包裹预建执行。包裹规则和异常规则均可保存、发布并参与实际记录处理；包裹规则只标记包裹类型，物理格口控制由 Fusion 链路执行。

Windows 部署完成后会提示按 `Ctrl+D` 收藏前端地址。选择“是”表示用户确认已经收藏，此地址之后不再提醒；选择“否”则下次部署继续提醒。确认记录按本机用户与完整前端地址保存至 `%LOCALAPPDATA%/Zeye.Sorting.Hub/bookmark-confirmations.json`，地址或端口变化会重新提醒。浏览器的真实收藏状态无法由普通部署脚本通用检测，因此此记录是用户确认，不代表脚本读取或写入了浏览器书签。

可用参数：`-SkipBuild` 使用已有镜像，`-NoOpenBrowser` 跳过打开浏览器与收藏提醒（适用于自动化运行），`-RemindBookmark` 强制重新询问收藏状态，`-StartupTimeout 300` 调整就绪等待时间（默认 180 秒）。未就绪或部署失败时不会自动打开前端。需要手动部署时仍可使用 `docker compose --env-file deploy/.env -f deploy/compose.yaml up -d --build`。

Linux 对应参数为 `--skip-build`、`--no-open-browser`、`--startup-timeout 300`。无桌面环境时脚本输出访问地址；有桌面环境且安装了 `xdg-open` 时自动打开浏览器。

所有容器默认使用 `Asia/Shanghai`，可通过 `.env` 的 `ZEYE_TIME_ZONE` 调整。此项目的数据库与接口使用本地时间，因此 Windows 运行主机的时区应与容器保持一致，避免按日统计、分表日期和日志时间出现偏差。

Windows 原生验证可执行 `dotnet test Zeye.Sorting.Hub.sln -c Release`、`./deploy/publish-windows.ps1`，以及在 Web 目录运行 `npm test`。原生 Host 必须通过环境变量或独立环境配置提供真实数据库连接，首次部署时配置迁移策略；前端开发与预览仍可用 `ZEYE_API_PROXY` 指向 Host。CI 为 Windows / Linux 配置后端测试、前端测试及一体化发布检查。

查看状态：`docker compose --env-file deploy/.env -f deploy/compose.yaml ps`。查看日志：`docker compose --env-file deploy/.env -f deploy/compose.yaml logs --tail=100`。停止服务：`docker compose --env-file deploy/.env -f deploy/compose.yaml down`；保留数据卷，后续可再次启动。只有明确要清空本项目数据时才使用 `down -v`。

需要给包裹台账填充可重复核验的 50 条本地测试记录时，运行 `./deploy/seed-test-parcels.ps1 -Date '2026-10-01'`。条码以 `TEST-20261001-` 开头，脚本通过 API 写入并核对 35 条已完成、10 条分拣异常、5 条待分拣；重复运行不会新增重复包裹。

需要给「数据概览」填充图表样本时，运行 `./deploy/seed-workbench-data.ps1 -AnchorDate '2026-10-01'`。脚本只连接本 Compose 项目的 MySQL，在结束日期之前的 7 天写入 80 条带 `WB-DEMO-` 条码的测试快照，包括 60 条已完成、13 条异常和 7 条待处理，最后核对数据库及报表 API。相同日期重复运行不会增加记录；不传 `-AnchorDate` 时以运行当天为结束日期。这些快照用于验证统计页面，不包含设备处理流水。

需要覆盖业务表和完整分拣链路的模拟数据时，运行 `./deploy/seed-business-data.ps1`；Linux 使用 `sh deploy/seed-business-data.sh`。默认补充最近30天共1万票，保留已有记录，重跑不会重复，包含四个模拟来源、称重/体积/条码/设备/格口/指令/接口/图片等明细、处理事实及幂等定位、审计、集包、规则草稿和归档演练。使用 `-Preview` 只生成校验，`-VerifyOnly` 只复核已落库批次。详见 [业务模拟工具说明](../tools/BusinessDataSimulator/README.md)。

开启权限保护时，两个样本脚本均需传入已登录的 PowerShell 会话 `-Session $session`。可通过 `Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:4187/api/access/login' -ContentType 'application/json' -Body $loginJson -SessionVariable session` 建立会话，其中 `$loginJson` 在内存中包含账号和密码，勿将凭据写入脚本或日志。脚本会先校验登录状态，避免写完样本后才发现报表查询被拒绝。

归档任务仍只执行 dry-run，不执行历史数据迁移或删除。

Windows 原生运行同样支持上述备份和恢复，不依赖额外安装 MySQL 命令行工具。应用账号须具备源库读取权限及隔离库前缀建库权限；Docker 部署入口会执行 `initialize-restore.sql` 的受限授权。非 Docker 环境由数据库管理员在目标服务器执行该 SQL。含视图、存储程序、非 InnoDB 表或超过配置导出预算的数据库请使用原生备份工具；`Persistence:Backup:MaxExportGiB` 默认 32 GiB，单次自动操作默认 30 分钟。SQL Server 使用既有备份恢复 Runbook。上线恢复应先验证隔离库，再按 Runbook 切换，页面不会执行业务库覆盖操作。

消息投递模块已移除。升级迁移仅清理为空的遗留消息表；非空表保留历史记录，不自动销毁数据。包裹接收、持久化与分析流程无需外部消息接收端。

长期运行配置将 MySQL、Host、Web 标准输出日志限制为每文件 10 MiB、最多 3 份；已有容器需重建才应用日志驱动设置。Host 停止宽限 45 秒，应用停止预算 30 秒，管理员测试缓冲排空预算 15 秒。Host 默认内存上限为 2g，可用 `ZEYE_HOST_MEMORY_LIMIT` 调整；进程数上限 512。`/health/deep` 额外报告实际内存、句柄和运行/备份/日志所在文件系统剩余空间。

新增 `host_governance` 卷持久化恢复手册、演练记录、迁移脚本、月报及年度报告。升级已有容器时先复制原 `/app/backup-runbooks`、`/app/drill-records`、`/app/migration-scripts`、`/app/monthly-reports` 和存在的年度报告目录，再迁入 `/app/governance-artifacts` 下对应目录；不能仅重建容器后认为旧文件自动转入新卷。

本机已关闭备份预演，因此完整快照按默认 30 天、744 份、32 GiB 目标轮转；至少保护 3 份摘要核验通过的完整备份。无法确认的文件不删除，低于安全份数或安全副本已超过容量目标时保留并告警。备份空间和执行预算须随真实数据增长调整。后台每小时维护一次目录。源数据库、备份同处本机不能抵御整机故障，生产需离机备份和隔离恢复演练。

Host 镜像每 30 秒执行 `/health/live`，Docker 只据此标记健康状态；`restart: unless-stopped` 在进程退出后重启，不会因为仍运行的进程被标记 unhealthy 而自动重启。长时间暂停的外部监督、主机开机自启和远程 MySQL/MinIO 容量监测应由运行环境提供。已修复项目及验证边界见仓库根目录 `超长时间无人值守补强审查报告.md`。
