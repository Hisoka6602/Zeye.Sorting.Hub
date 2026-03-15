# Zeye.Sorting.Hub

## 仓库文件结构（当前）

> 说明：以下结构已包含仓库内的全部受版本控制文件（不含 `.git`、`bin/`、`obj/` 等构建产物目录）。

```text
.
├── .gitattributes
├── .gitignore
├── README.md
├── Zeye.Sorting.Hub.Analytics
│   ├── Class1.cs
│   └── Zeye.Sorting.Hub.Analytics.csproj
├── Zeye.Sorting.Hub.Application
│   ├── Class1.cs
│   └── Zeye.Sorting.Hub.Application.csproj
├── Zeye.Sorting.Hub.Contracts
│   ├── Class1.cs
│   └── Zeye.Sorting.Hub.Contracts.csproj
├── Zeye.Sorting.Hub.Domain
│   ├── Abstractions
│   │   └── IEntity.cs
│   ├── Aggregates
│   │   └── Parcels
│   │       ├── Parcel.cs
│   │       └── ValueObjects
│   │           ├── ApiRequestInfo.cs
│   │           ├── BagInfo.cs
│   │           ├── BarCodeInfo.cs
│   │           ├── ChuteInfo.cs
│   │           ├── CommandInfo.cs
│   │           ├── GrayDetectorInfo.cs
│   │           ├── ImageInfo.cs
│   │           ├── ParcelDeviceInfo.cs
│   │           ├── ParcelPositionInfo.cs
│   │           ├── SorterCarrierInfo.cs
│   │           ├── StickingParcelInfo.cs
│   │           ├── VideoInfo.cs
│   │           ├── VolumeInfo.cs
│   │           └── WeightInfo.cs
│   ├── DomainEvents
│   │   └── Parcels
│   │       ├── ParcelChuteAssignedEventArgs.cs
│   │       └── ParcelScannedEventArgs.cs
│   ├── Enums
│   │   ├── ActionType.cs
│   │   ├── ApiRequestStatus.cs
│   │   ├── ApiRequestType.cs
│   │   ├── BarCodeType.cs
│   │   ├── CommandDirection.cs
│   │   ├── ImageCaptureType.cs
│   │   ├── ImageType.cs
│   │   ├── NoReadType.cs
│   │   ├── ParcelStatus.cs
│   │   ├── ParcelType.cs
│   │   ├── VideoNodeType.cs
│   │   └── VolumeSourceType.cs
│   ├── Primitives
│   │   └── AuditableEntity.cs
│   ├── Repositories
│   │   └── IParcelRepository.cs
│   └── Zeye.Sorting.Hub.Domain.csproj
├── Zeye.Sorting.Hub.Host
│   ├── HostedServices
│   │   └── DatabaseInitializerHostedService.cs
│   ├── Program.cs
│   ├── Properties
│   │   └── launchSettings.json
│   ├── Worker.cs
│   ├── Zeye.Sorting.Hub.Host.csproj
│   ├── appsettings.Development.json
│   └── appsettings.json
├── Zeye.Sorting.Hub.Infrastructure
│   ├── DependencyInjection
│   │   └── PersistenceServiceCollectionExtensions.cs
│   ├── EntityConfigurations
│   │   ├── BagInfoEntityTypeConfiguration.cs
│   │   └── ParcelEntityTypeConfiguration.cs
│   ├── Persistence
│   │   ├── DatabaseDialects
│   │   │   ├── IDatabaseDialect.cs
│   │   │   ├── MySqlDialect.cs
│   │   │   └── SqlServerDialect.cs
│   │   ├── DesignTime
│   │   │   └── MySqlContextFactory.cs
│   │   └── SortingHubDbContext.cs
│   ├── Repositories
│   │   ├── MemoryCacheRepositoryBase.cs
│   │   ├── RepositoryBase.cs
│   │   └── RepositoryResult.cs
│   └── Zeye.Sorting.Hub.Infrastructure.csproj
├── Zeye.Sorting.Hub.Realtime
│   ├── Class1.cs
│   └── Zeye.Sorting.Hub.Realtime.csproj
├── Zeye.Sorting.Hub.RuleEngine
│   ├── Class1.cs
│   └── Zeye.Sorting.Hub.RuleEngine.csproj
├── Zeye.Sorting.Hub.SharedKernel
│   ├── Class1.cs
│   └── Zeye.Sorting.Hub.SharedKernel.csproj
├── Zeye.Sorting.Hub.sln
└── 项目完成度与推进计划.md
```

## Copilot 维护规定

- 每次新增文件或删除文件后，必须同步更新本 README 的“仓库文件结构（当前）”章节，保证结构清单与仓库实际内容一致。
