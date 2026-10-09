# Windows / Linux 部署

Fusion 1.0 的来源登记、专用 SignalR 机器认证、图片持久化、可选发现和追溯入口见 [Fusion接入说明.md](Fusion接入说明.md)。默认未登记工作台，来源不会自动启用。

来源：部署命令及参数依据本仓库 `publish-windows.ps1`、`compose.yaml` 和 Host 入口配置；实时协议及客户端行为参考[微软 SignalR 文档](https://learn.microsoft.com/aspnet/core/signalr/javascript-client)。

## Windows 前后端一体化发布

在仓库根目录执行一个命令即可构建并发布前后端：

```powershell
./deploy/publish-windows.ps1
```

构建机需要 .NET 10 SDK 和 Node.js 24/npm。默认输出为 `artifacts/windows-x64`，包含 Windows x64 自包含 Host、前端 `wwwroot`、配置和 `Start-Hub.cmd`。将整个目录一次复制到目标主机，配置数据库后运行 `Start-Hub.cmd` 或 `Zeye.Sorting.Hub.Host.exe`。目标机不需要 Node、Nginx、独立前端服务或另外安装 .NET；业务数据库支持 MySQL、SQL Server、Oracle、SQLite，配置及四库隔离 Docker/Fusion 验收步骤见 [四数据库部署与验收](../docs/四数据库部署与验收.md)。

页面、登录、API、包裹图片及健康探针由同一个进程和端口提供，默认入口为 `http://127.0.0.1:5078/`。深层页面刷新也可直接访问。程序从自身目录加载配置和 `wwwroot`，从其他工作目录启动同样有效。默认仅监听 HTTP，无需开发证书；配置服务器证书后可用 `Hosting:Urls` 或 `--urls` 启用 HTTPS。

登录后的包裹列表、概览、报表、规则和平台状态通过同源 `/hubs/sorting` 的 SignalR 通道查询与订阅。处理事实提交与包裹状态更新使用两个明确的实时方法，正文最多 4 KiB，较大请求继续走原 HTTP 入口。成功业务写入会唤醒快照更新；后台状态另有低频检查，前端不再定时发送 HTTP 查询。Windows 内置前端直接连接 Host；Docker Nginx 与 Vite 开发代理均已转发 WebSocket。额外反向代理需同时转发 `/hubs/`、HTTP Upgrade 与 Cookie，并允许长连接。

实时通道复用原接口认证、权限、限流及审计，每次调用重新验证账号；断线自动重连并恢复订阅，JSON 原文保证长编号不失真。写入只发送一次，断线或超时未收到结果时先检查业务记录，不能自动重放。账号、上传、下载、清理和其余管理写入继续使用现有 HTTP 安全流程。融合服务尚未上报的设备在线状态仍显示待接入，不能由浏览器实时连接推断设备在线。客户端参考：[微软 SignalR JavaScript 文档](https://learn.microsoft.com/aspnet/core/signalr/javascript-client)。

原生部署可使用环境变量配置数据库及认证，环境变量始终优先于页面保存值。首次启动时也会自动导入独立的 `appsettings.Production.json`；导入完成后，运行配置由超级管理员在“系统配置”页面维护，JSON 文件仅保留配置库路径和 Kestrel 等启动引导参数。例如在 PowerShell 中设置实际连接和不同的初始化、设备密钥后启动：

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

这些建表设置允许程序自动执行迁移及预建分表，数据库账号需有对应 DDL 权限，生产危险迁移阻断仍遵循已有配置。双击启动前，可将对应配置写入发布目录中的 `appsettings.Production.json` 作为首次导入值；已建立配置库后，修改旧 JSON 的运行参数不会覆盖配置库，应改用页面或明确的环境覆盖。

首次运行仍需通过页面创建首个管理员，完成后才启用内置超级用户。仅剩内置账号、没有普通成员时，登录页重新提示创建管理员，仍需部署初始化密钥；已有普通成员时不能重复初始化。内置账号不显示在成员列表中，也不计入角色成员数。接口权限和清理密码确认流程保持生效。配置、账号及数据库不放在公开的 `wwwroot` 中；升级时先发布到新的输出目录，再保留目标机部署配置、`data/configuration`、`data/business-history`、`logs/data-protection`、日志、备份及业务数据库。配置目录和历史目录不能用发布包中的初始文件覆盖。

配置默认保存在 `data/configuration/settings.db`，修改前后原值历史独立保存在 `data/business-history/configuration-history.db`，配置及历史原值接口仅超级管理员可访问；旧版已脱敏历史保留原记录。目录、文件、集合及历史表由程序自动建立。迁移旧配置时会保留已有版本和加密凭据，已有配置不会被旧 JSON 或关系库重复覆盖。备份和迁移部署时应在服务停止后整体复制上述目录。加密凭据依赖配置库目录下的 `data-protection` 密钥；启动会从旧 `logs/data-protection` 自动复制且保留原文件。热更新范围、旧版兼容和存储路径覆盖方式见根目录 [README.md](../README.md)。

脚本可用 `-OutputDirectory 'D:\Publish\SortingHub'` 改变输出目录。原生发布默认按内容增量处理：依赖声明、锁文件、npm 配置或 Node 平台变化时才恢复依赖；源码、静态资源、TypeScript/Vite 配置、构建脚本或 `VITE_*` 环境变化时才重建前端。输入与全部输出的 SHA-256 均匹配时复用构建，每次仍将完整前端纳入发布包，并再次完整复制到 `wwwroot`，修复与源文件大小和时间戳相同的内容损坏。构建目录缺失或损坏的资源自动重建，失败的构建不能被缓存。缓存元数据保存在 `artifacts/web-publish`，不会进入公开的 `wwwroot`；并发发布共用锁，避免同时重装依赖。

依赖恢复优先使用 `artifacts/npm-publish-cache` 的离线缓存；缓存不足时尝试官方源，失败后使用备用源（默认 `https://registry.npmmirror.com/`）。版本与完整性仍按原锁文件校验，不修改锁文件、系统 DNS 或全局 npm 配置，不关闭 HTTPS 证书校验。联网请求超时 15 秒、无重复请求，每轮安装最多 90 秒，构建最多 180 秒，每 15 秒输出阶段进度。

`-ForceWebUiBuild` 可强制构建；`-ForceWebDependencyRestore` 可强制恢复依赖并构建。`-WebNpmRegistry` / `-WebNpmFallbackRegistry` 可指定 HTTPS 主源和备用源，备用源设为 `none` 可关闭。`-SkipDependencyRestore` 禁止自动安装；需要重建时必须已安装与锁文件匹配的完整依赖，否则明确失败。Visual Studio 可选择 `Windows-x64` 文件夹发布配置，CLI 等价命令如下：

```powershell
dotnet publish Zeye.Sorting.Hub.Host/Zeye.Sorting.Hub.Host.csproj -p:PublishProfile=Windows-x64 -o artifacts/windows-x64
```

直接使用 `dotnet publish` 时，对应参数为 `-p:ForceWebUiBuild=true`、`-p:ForceWebDependencyRestore=true`、`-p:WebNpmRegistry=https://...`、`-p:WebNpmFallbackRegistry=https://...`（或 `none`），以及 `-p:RestoreWebDependencies=false`。

Linux 同样默认打包前端：`dotnet publish Zeye.Sorting.Hub.Host -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64`，部署目录完整复制后执行 `chmod +x Zeye.Sorting.Hub.Host`，再运行 `./Zeye.Sorting.Hub.Host`。目标机仍需操作系统要求的原生依赖和可用数据库；运行用户应对发布目录拥有写入权限，以保存日志、会话密钥和治理文件。普通 `dotnet build` / `dotnet test` 不触发 npm；仅发布时构建前端。显式 `-p:BundleWebUi=false` 可保留纯 API 发布能力。

## Windows / Linux 服务安装与卸载

服务脚本随完整发布包交付，在发布目录使用；源码目录不能直接安装服务。前端 `wwwroot` 与后端 Host 由同一个服务提供，不需要另行注册前端。数据库尚未配置或初始化失败时，服务先提供网页和本机数据库配置入口，业务请求及业务后台任务保持关闭。部署目录应置于目标账号能够访问的位置，Linux 建议 `/opt/zeye/sorting-hub`。

Windows 发布仍执行 `./deploy/publish-windows.ps1`，在目标机双击安装或卸载脚本，或在终端运行：

```bat
install.bat
uninstall.bat
```

`install.bat` 注册延迟自动启动服务，使用专用虚拟账号 `NT SERVICE\Zeye.Sorting.Hub.Host`，授权其读写本发布目录，启动后验证服务保持运行。重复安装先停止再更新并启动服务。PowerShell 当前进程中显式设置的 `*__*` 配置变量以及 `ASPNETCORE_*` / `DOTNET_*` 会保存到该服务的注册表 `Environment`，已有配置继续保留；变量值不输出到安装日志。运行配置通常从“系统配置”页面维护，`appsettings.Production.json` 仅用于首次导入或启动引导参数。不需要配置管理员密码或默认使用 LocalSystem。

Windows 脚本在权限不足时自动请求 UAC 管理员授权；选择“是”后继续安装或卸载。操作结束后保留窗口和真实退出码，按任意键关闭。安装及卸载的最近一次执行记录分别保存到发布目录的 `logs/service-install.log` 和 `logs/service-uninstall.log`；取消授权返回失败，不操作服务。`--dry-run` 不请求授权、不生成执行日志，也不等待按键。自动化实际安装可直接调用 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File service.ps1 -Action Install`，卸载使用 `-Action Uninstall`。

首次安装自动创建缺失的 `Environment` 注册表项（`REG_MULTI_SZ`），默认运行环境为 `Production`。重复安装保留已有配置，并用本次显式传入的环境变量覆盖对应项；配置值中的等号保持完整。

Windows 安装成功前会等待服务进程开始监听端口；显式设置 `ZEYE_SERVICE_HEALTH_URL` 时使用 HTTP 就绪探针，或确认已进入可用的数据库配置模式。配置模式显示“网页已启动，等待数据库配置”，不会声称业务就绪；进程退出、未开始监听或超过启动时限仍返回失败。

首次启动可先双击 `install.bat`：数据库未就绪时会自动打开带本机访问码的配置页面。入口沿用现有数据库配置表单，将数据库类型和连接字符串保存到 LiteDB，并记录独立的配置变更历史。保存后点击表单底部“重启 Host”，确认后等待页面自动恢复；已保存的配置无需重复修改，可以直接重启重试连接。存在未保存修改时不能重启，必须先保存或撤销。连接正常且初始化完成后，页面恢复登录和业务功能。启动参数不会在当前进程中提前切换。

正常业务模式下，“系统配置”中的重启操作仅超级管理员可用；本机数据库配置模式下，需要有效访问码和本机同源请求。接口核对当前保存版本，重复请求只安排一次重启，并在响应发送后正常关闭后台任务与配置存储。Windows 安装程序启用非零停止状态恢复，systemd 和容器使用各自的失败恢复策略，直接运行的程序使用原启动命令重新启动。前端必须观察到新的 Host 实例才刷新，等待超时会提示检查服务日志。主动重启后数据库仍未就绪时，当前已授权的本机配置页可以继续修正连接；访问码交接最多五分钟且只消费一次，普通重启仍生成新的访问码。

SQLite 首次启动时，由数据库初始化流程自动创建连接字符串指定的目录、文件与迁移表结构，连接探测本身不创建文件。若“仅预演数据库初始化”处于开启状态，表结构不会实际创建；确认初始化时关闭该项，保存后重启 Host。该选项也显示在本机数据库配置表单中，仍保留自动建库隔离器、迁移脚本归档和生产环境危险迁移检查。`Mode=ReadOnly` 或 `Mode=ReadWrite` 不允许创建缺失文件。

MySQL、SQL Server 和 Oracle 需要先有可连接的数据库服务器；Host 在已有授权范围内创建业务数据库或 schema。Oracle 首次创建业务用户或补齐锁包权限时，在同一本机表单填写指向目标 PDB 的“Oracle 管理连接字符串”，已有且完成授权的业务用户可以留空。SQL Server 已有业务库首先使用目标库验证连接，避免要求业务账号额外连接 `master`。

首次建表会检查目标库确实没有用户对象且没有已应用迁移。只有已启用自动建库、隔离器允许执行且关闭预演时，才执行空库的完整历史迁移链；执行前再次确认空库。已有对象的数据库继续执行原有生产危险迁移保护。配置页会区分连接失败、初始化预演、迁移保护和业务服务启动失败；详细异常仍保存在服务日志，页面不显示凭据。

配置入口只接受服务器本机回环地址、正确来源标识和本次启动访问码，不依赖业务账号数据库。访问码保存在配置库同目录的 `database-setup.key`，Windows 仅运行账号和管理员可读，Linux 仅文件所有者可读；不写入日志、不通过 URL 查询参数发送，业务恢复后立即失效。手动打开页面时可从该文件复制访问码；远程访问只能看到等待本机配置的提示。接口只允许数据库类型、连接字符串和初始化预演开关，不能修改账号权限、设备密钥、危险迁移保护或读取原值历史。`/health/live` 在配置模式返回正常，`/health/ready` 和业务接口返回 503，避免流量或设备将配置模式误认为业务可用。

业务数据库就绪后，在 `/access/login` 创建首个管理员。部署显式设置的 `Access__BootstrapKey` 优先使用；没有配置该值时，从服务器本机打开登录页会自动生成配置库同目录的 `administrator-bootstrap.key`，页面显示实际路径。以管理员身份读取文件内容，填入“管理员初始化密钥”，再自行填写姓名、账号和 12～128 位密码。该密钥与数据库配置访问码不同，不通过接口返回、不进入日志；文件权限限运行账号及管理员，重启后保留，账号保存成功后自动删除并关闭入口。远程请求不会生成或获取本机初始化凭据。

Linux 发布和安装：

```sh
dotnet publish Zeye.Sorting.Hub.Host -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64
# 将整个目录复制到目标主机；首次部署可用 appsettings.Production.json 提供导入配置。
cd /opt/zeye/sorting-hub
sudo bash install.sh
sudo bash uninstall.sh
```

`install.sh` 创建专用 `zeye-hub` 用户与组，发布目录及文件归该账号所有，安装 `/etc/systemd/system/Zeye.Sorting.Hub.Host.service` 并启用开机启动。服务使用 `Type=notify` 等待 Host 就绪，工作目录固定为发布目录，并在意外退出后自动重启。目标系统需要运行 systemd、具备 `useradd` / `groupadd` 等标准管理工具及 .NET 所需原生依赖。自定义到发布目录之外的备份、图片等存储路径，需要事先授权服务账号写入。

发布目录支持中文、空格、美元及百分号；systemd 不支持可执行文件路径中的引号和反斜线，安装预检会提前拒绝这类目录。运行 `bash deploy/test-systemd-unit.sh` 可使用真实 systemd 解析器复验生成的 unit，测试不注册服务。

Linux 的持久化环境变量文件默认 `/etc/default/Zeye.Sorting.Hub.Host`，首次安装自动创建，权限为 `600`；填入实际配置后重新执行安装或重启服务。Linux 不自动复制当前 shell 环境到服务，环境文件格式为 `配置名="值"`。`appsettings.Production.json` 可提供首次导入值，之后从页面维护运行配置；环境文件中的配置仍为优先覆盖。安装保留既有环境文件。正常服务管理与诊断示例：

```sh
sudo systemctl status Zeye.Sorting.Hub.Host
sudo systemctl restart Zeye.Sorting.Hub.Host
sudo journalctl -u Zeye.Sorting.Hub.Host -n 100 --no-pager
```

两个卸载脚本先等待服务停止，再移除服务注册或 unit；停止失败不会继续删除注册。卸载保留发布文件、环境配置、专用账号、日志、图片、备份和数据库，不执行业务数据清理。重复卸载返回成功。同名服务若属于不同发布目录，脚本拒绝覆盖或卸载；升级到新目录应先从旧目录卸载，再从新目录安装。

Linux 的自有 unit 即使配置损坏，只要确认处于未运行状态且没有宿主进程，也可停止卸载或修正后重新安装。

可用 `install.bat --dry-run`、`uninstall.bat --dry-run` 或 `bash install.sh --dry-run`、`bash uninstall.sh --dry-run` 无副作用预演，预演不注册账号或服务，也不启动程序。安装过程失败返回非零退出码，不显示成功；已创建的本项目注册保留，便于修正配置后重新安装。

| 环境变量 | 范围与默认值 |
| --- | --- |
| `ZEYE_SERVICE_NAME` | 1～80 个字母、数字、点、下划线或连字符，首字符为字母或数字；默认 `Zeye.Sorting.Hub.Host`，安装及卸载需一致 |
| `ZEYE_SERVICE_DISPLAY_NAME` | Windows 显示名称，默认 `Zeye Sorting Hub` |
| `ZEYE_SERVICE_TIMEOUT_SECONDS` | 30～900 的整数秒数，默认 180，用于等待启动、停止或就绪 |
| `ZEYE_SERVICE_DRY_RUN` | `1` / `true` / `yes` / `on` 启用预演，默认关闭 |
| `ZEYE_SERVICE_HEALTH_URL` | 可选 HTTP / HTTPS 就绪地址，如 `http://127.0.0.1:5078/health/ready`；填写后安装须等到响应 200，未填写则等待服务进程开始监听端口 |
| `ZEYE_SERVICE_USER` / `ZEYE_SERVICE_GROUP` | Linux 本地账号与组名，默认 `zeye-hub`；允许 1～32 个小写字母、数字、下划线或连字符，首字符为小写字母或下划线 |
| `ZEYE_SERVICE_ENV_FILE` | Linux 环境文件绝对路径，默认 `/etc/default/Zeye.Sorting.Hub.Host`，不能使用符号链接 |

多实例需要不同服务名、独立发布目录与监听端口；数据库及 Fusion 来源配置继续按业务部署规划设置。脚本不会更改已有账号初始化、密码确认、接口权限或数据库危险操作规则。

构建机可执行 `./deploy/test-service-scripts.ps1` 验证安装脚本语法、预演及错误输入，不操作真实服务。Windows 默认使用 Git Bash，可用 `-BashExecutable '完整 Bash 路径'` 指定环境。

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

Windows 本机入口 `4187` 默认跟随当前工作目录的代码：`start.ps1` 成功部署后会启动无窗口后台监听。前端源码、样式、依赖或 Nginx 配置改变时重建 Web；后端源码、项目依赖或应用配置改变时重建 Host。每个 Compose 项目保持一个监听进程；同一批修改命中多个服务时，由 Compose 一起构建，全部成功后更新对应容器。编译失败时保留已有可用版本。原有 MySQL、登录账号和配置数据卷继续保留。浏览器刷新即可加载当前构建；已经打开的页面不会被自动刷新。监听排除依赖目录、编译输出、测试、日志和业务数据，避免重复构建。该流程使用 [Docker Compose Watch](https://docs.docker.com/compose/how-tos/file-watch/)，需要支持 `develop.watch.include` 的新版 Compose（本机已验证 v5.1.4）。

```powershell
# 独立开启监听，先补构建当前代码；重复执行复用已有监听。
./deploy/watch.ps1
# 查看状态及日志位置。
./deploy/watch.ps1 -Action Status
# 停止自动更新，现有服务和数据继续保留。
./deploy/watch.ps1 -Action Stop
```

日志和进程状态保存在 Git 忽略的 `artifacts/local-watch/`。监听叠加 `compose.watch.yaml`，只更新 Host/Web，避免 Compose 连带重建数据库依赖；首次部署仍由 `start.ps1` 按原依赖顺序启动数据库。Docker Desktop 重启、监听异常退出或 Windows 文件事件出错导致监听停用时，后台进程自动重试并重新构建后恢复监听；Windows 重启后需重新执行 `./deploy/start.ps1 -NoOpenBrowser`。`-NoWatch` 可跳过启动监听，已运行的监听需要单独停止。修改 Compose 规则或 `.env` 后，应先停止监听，再重新执行部署入口以加载新的设置。`-SkipBuild` 只复用已有镜像，不能用于确认代码已更新。Linux 可在部署后运行 `docker compose --env-file deploy/.env -f deploy/compose.yaml -f deploy/compose.watch.yaml watch --no-up host web`，保持此进程运行以自动更新。

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
