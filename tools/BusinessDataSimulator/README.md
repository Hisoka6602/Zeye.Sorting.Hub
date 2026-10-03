# 业务模拟数据

显式运行的本地工具，默认给当前 Docker 数据库补充最近 30 天共 10,000 票。不会在生产服务启动时自动造数。

Windows（仓库根目录，需 .NET 10 SDK 与运行中的本项目 Docker）：

```powershell
./deploy/seed-business-data.ps1
./deploy/seed-business-data.ps1 -Preview
./deploy/seed-business-data.ps1 -VerifyOnly
```

Linux：

```sh
sh deploy/seed-business-data.sh 10000 30 --write
sh deploy/seed-business-data.sh 10000 30 --preview
sh deploy/seed-business-data.sh 10000 30 --verify-only
```

`-Count` / `-Days`（Linux前两个参数）调整规模，最多 10 万票、90 天。同一日期窗口的批次参数必须一致。首次写入保存批次时间，重跑复用时间与身份，跳过已有记录且复核完整性；失败后可用相同参数继续。已有业务数据、账号、权限、配置及已发布规则均保留。

数据包含四个 `sim-fusion-01` 至 `sim-fusion-04` 来源实例，条码带 `SIM-` 前缀；未读条码是 `NoRead`，可按模拟工作台或来源区分。件量按工作日/周末变化，一天多次到件批次；连续创建间隔约 650–1,250 毫秒，落格耗时约 8–22 秒。重量、长宽高、体积、DWS绑定、扫描上传、格口决策、指令和落格事实互相对应，包含临时接口失败后重试成功、分拣机协议异常和未知异常。历史包裹已完成或异常，只有最近时间保留少量待处理。

包裹、十三类附属明细和处理事实按当前配置进入物理分表，并同时保存 `ParcelLocations` 和 `ParcelProcessingReceipts`。每票都有模拟幂等记录；部分包裹有 Inbox 接收记录与审计主/详情。Bags 当前只支持每格口一个集包快照，因此仅给最近一天各格口末批最多 25 票关联当前模拟集包，不虚构持续一个月的大集包。已占用的真实格口集包不修改。

新增包裹/异常分类示例均为**草稿**，限模拟来源；系统未知异常和既有规则保留。六条归档记录明确标记为**模拟演练**，没有执行数据移动或删除。图片引用前端 `/demo/parcel-sample.svg`，图内明确标注非现场照片；需部署包含该静态资源的前端。视频表仅为模拟 NVR 节点元数据，没有伪造可播放录像。外部 Provider 地址使用保留的 `.invalid` 域名，工具不对外发送请求。来源设备在线情况仍等待真实 SignalR 接入。

工具仅允许显式 `--local-docker`、`LocalDocker` 环境及本机 `zeye_sorting_hub` 库。连接凭据直接使用已有 Host 容器环境，不输出秘密。不造假账号、备份文件、迁移历史、系统健康或未来预建分表数据。

验证命令：

```sh
dotnet test tools/BusinessDataSimulator.Tests/BusinessDataSimulator.Tests.csproj -c Release
docker build -f tools/BusinessDataSimulator/Dockerfile -t zeye-business-data-simulator:verify .
docker run --rm zeye-business-data-simulator:verify --count 10000 --days 30
```

生成阶段检查数量、唯一身份、时间边界、量测单位和状态；写入后重新读取数据库，核对快照/明细、来源定位、事实/去重凭据、集包和审计关系。补充后的统计是明确标识的模拟数据，不代表实际分拣机产能或故障率。
