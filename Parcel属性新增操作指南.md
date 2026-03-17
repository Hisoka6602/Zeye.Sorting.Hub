# Parcel 新增属性操作指南

> 本文档回答一个问题：**当 Parcel 聚合需要新增一个属性时，需要修改哪些文件、如何修改？**
>
> 本项目采用 DDD 分层架构，Parcel 相关代码跨越 Domain 层与 Infrastructure 层（当 Application/Contracts 层完成占位后还需同步）。根据属性的性质不同，分三种情形分别说明。

---

## 情形一：在 Parcel 聚合根主表新增普通标量属性

**适用场景**：新增一个直接挂在 Parcel 实体上、持久化到 `dbo.Parcels` 主表的字段，例如新增 `string? Remark`（备注）或 `int Priority`（优先级）。

### 需要修改的文件（共 2 个 + 1 条迁移命令）

| 序号 | 文件路径 | 操作类型 |
|------|---------|---------|
| 1 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/Parcel.cs` | 修改 |
| 2 | `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/ParcelEntityTypeConfiguration.cs` | 修改 |
| — | `dotnet ef migrations add` 命令 | 执行 |

---

### 第 1 步：`Parcel.cs` — 添加属性声明

在 `Parcel` 类的属性区域（其他标量属性旁）添加新属性，访问修饰符用 `{ get; private set; }`：

```csharp
/// <summary>
/// 备注信息（可选）
/// </summary>
public string? Remark { get; private set; }
```

**若该属性需要在创建时传入**，同步修改 `Create()` 工厂方法：

```csharp
// 1. 在 Create() 参数列表中添加（可选参数放在末尾）
public static Parcel Create(
    /* 已有参数... */
    string? remark = null) {

    // 2. 在对象初始化器中赋值
    var entity = new Parcel {
        /* 已有字段赋值... */
        Remark = remark,
        CreatedTime = DateTime.Now,
    };
    return entity;
}
```

**若该属性在创建后由业务行为设置**，增加对应的行为方法：

```csharp
/// <summary>
/// 设置备注信息
/// </summary>
public void SetRemark(string? remark) {
    Remark = remark;
}
```

---

### 第 2 步：`ParcelEntityTypeConfiguration.cs` — 添加 EF Core 映射

在 `Configure()` 方法的"Parcel 主体字段"注释块下方，参照已有字段格式添加：

```csharp
builder.Property(x => x.Remark)
    .HasColumnName("Remark")
    .HasMaxLength(512);   // 可选字段不加 .IsRequired()
```

**常用映射配置速查**：

| 场景 | 配置片段示例 |
|------|------------|
| 非空字符串（最大 128 字符） | `.HasColumnName("X").HasMaxLength(128).IsRequired()` |
| 可空字符串（最大 1024 字符）| `.HasColumnName("X").HasMaxLength(1024)` |
| 非空整数/枚举 | `.HasColumnName("X").IsRequired()` |
| 可空整数/枚举 | `.HasColumnName("X")` |
| 非空 decimal（精度 18,3） | `.HasColumnName("X").HasPrecision(18, 3).IsRequired()` |
| 可空 decimal | `.HasColumnName("X").HasPrecision(18, 3)` |
| 非空 DateTime | `.HasColumnName("X").IsRequired()` |
| 可空 DateTime | `.HasColumnName("X")` |
| 布尔值 | `.HasColumnName("X").IsRequired()` |

**若新属性需要索引**，在 `Configure()` 结尾的索引区域追加：

```csharp
builder.HasIndex(x => x.Remark);
```

---

### 第 3 步：执行 EF Core 迁移

> 前提：`Zeye.Sorting.Hub.Infrastructure/Persistence/DesignTime/MySqlContextFactory.cs` 已实现设计时工厂（当前为占位存根，需先补全）。

```bash
# 在解决方案根目录执行
dotnet ef migrations add Add_Parcel_Remark \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Host

# 确认迁移文件无误后，应用到数据库
dotnet ef database update \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Host
```

---

## 情形二：在现有值对象中新增属性

**适用场景**：要扩展已有子对象，例如在 `WeightInfo` 中新增 `string? SensorId`（传感器编号）。

当前 Parcel 的值对象清单（文件路径均位于 `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/`）：

| 值对象类名 | 对应子表 | 与 Parcel 的关系 |
|-----------|---------|----------------|
| `WeightInfo` | `Parcel_WeightInfos` | 一对多（`OwnsMany`） |
| `BarCodeInfo` | `Parcel_BarCodeInfos` | 一对多（`OwnsMany`） |
| `ApiRequestInfo` | `Parcel_ApiRequests` | 一对多（`OwnsMany`） |
| `CommandInfo` | `Parcel_CommandInfos` | 一对多（`OwnsMany`） |
| `ImageInfo` | `Parcel_ImageInfos` | 一对多（`OwnsMany`） |
| `VideoInfo` | `Parcel_VideoInfos` | 一对多（`OwnsMany`） |
| `VolumeInfo` | `Parcel_VolumeInfos` | 一对一（`OwnsOne`） |
| `ChuteInfo` | `Parcel_ChuteInfos` | 一对一（`OwnsOne`） |
| `SorterCarrierInfo` | `Parcel_SorterCarrierInfos` | 一对一（`OwnsOne`） |
| `BagInfo` | `Parcel_BagInfos` | 一对一（独立表，另有 `BagInfoEntityTypeConfiguration`） |
| `ParcelDeviceInfo` | `Parcel_DeviceInfos` | 一对一（`OwnsOne`） |
| `GrayDetectorInfo` | `Parcel_GrayDetectorInfos` | 一对一（`OwnsOne`） |
| `StickingParcelInfo` | `Parcel_StickingParcelInfos` | 一对一（`OwnsOne`） |
| `ParcelPositionInfo` | `Parcel_ParcelPositionInfos` | 一对一（`OwnsOne`） |

### 需要修改的文件（共 2 个 + 1 条迁移命令）

| 序号 | 文件路径 | 操作类型 |
|------|---------|---------|
| 1 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/WeightInfo.cs`（以此为例） | 修改 |
| 2 | `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/ParcelEntityTypeConfiguration.cs` | 修改 |
| — | `dotnet ef migrations add` 命令 | 执行 |

---

### 第 1 步：值对象文件 — 添加属性声明

值对象使用 `sealed record class`，属性用 `{ get; init; }`（不可变）。
- 必填属性加 `required`；可选属性直接声明（含默认值）。

```csharp
// WeightInfo.cs 示例：新增可选的传感器编号
/// <summary>
/// 传感器编号（可选，设备传入）
/// </summary>
public string? SensorId { get; init; }
```

> **注意**：值对象是不可变的 `record`，只有 `init` 访问器，无需添加 `Set` 方法。调用方在构造时直接通过对象初始化器赋值即可：
> ```csharp
> var info = new WeightInfo {
>     FormattedWeight = 1.5m,
>     WeighingTime = DateTime.Now,
>     SensorId = "SN-001",   // 新属性
> };
> ```

---

### 第 2 步：`ParcelEntityTypeConfiguration.cs` — 在对应 `OwnsMany`/`OwnsOne` 块中添加映射

找到对应值对象的配置块（以 `WeightInfo` 为例，关键词 `Parcel_WeightInfos`），在块内追加：

```csharp
builder.OwnsMany(x => x.WeightInfos, b => {
    // ...已有映射...
    b.Property(x => x.FormattedWeight).HasColumnName("FormattedWeight").HasPrecision(18, 3).IsRequired();
    b.Property(x => x.WeighingTime).HasColumnName("WeighingTime").IsRequired();
    b.Property(x => x.AdjustedWeight).HasColumnName("AdjustedWeight").HasPrecision(18, 3);

    // ✅ 新增以下一行
    b.Property(x => x.SensorId).HasColumnName("SensorId").HasMaxLength(128);

    b.HasIndex("ParcelId");
    b.HasIndex("WeighingTime");
});
```

---

### 第 3 步：执行 EF Core 迁移（同情形一第 3 步）

```bash
dotnet ef migrations add Add_WeightInfo_SensorId \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Host
```

---

## 情形三：新增一个全新的值对象（子信息对象）

**适用场景**：Parcel 需要挂载一个全新的子信息对象，例如新增 `ScannerInfo`（扫描仪信息）。

### 需要修改/新增的文件（共 3 个 + README + 1 条迁移命令）

| 序号 | 文件路径 | 操作类型 |
|------|---------|---------|
| 1 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/ScannerInfo.cs` | **新建** |
| 2 | `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/Parcel.cs` | 修改 |
| 3 | `Zeye.Sorting.Hub.Infrastructure/EntityConfigurations/ParcelEntityTypeConfiguration.cs` | 修改 |
| 4 | `README.md` | 修改（新增文件后必须同步） |
| — | `dotnet ef migrations add` 命令 | 执行 |

---

### 第 1 步：新建值对象文件

在 `Zeye.Sorting.Hub.Domain/Aggregates/Parcels/ValueObjects/` 下新建 `ScannerInfo.cs`，参照同目录的 `WeightInfo.cs` 结构：

```csharp
using System;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 扫描仪信息（值对象）
    /// 说明：仅表达领域语义，不包含 ORM 映射与序列化特性
    /// </summary>
    public sealed record class ScannerInfo {
        /// <summary>
        /// 扫描仪编号
        /// </summary>
        public required string ScannerId { get; init; }

        /// <summary>
        /// 扫描时间
        /// </summary>
        public required DateTime ScanTime { get; init; }

        /// <summary>
        /// 扫描结果原始字符串
        /// </summary>
        public string RawResult { get; init; } = string.Empty;
    }
}
```

**规则说明**：
- 必须使用 `sealed record class`（不可变值对象约定）。
- 所有属性用 `{ get; init; }`，必填属性加 `required`。
- 不得引入任何 ORM 特性（如 `[Column]`、`[Table]`）。
- 时间属性使用 `DateTime`（本地时间，禁止 `DateTimeOffset` / UTC）。

---

### 第 2 步：`Parcel.cs` — 添加导航属性与行为方法

**一对多子集合**（例如一个包裹可以有多次扫描记录）：

```csharp
// 在属性区域，与其他集合属性放在一起
private readonly List<ScannerInfo> _scannerInfos = new();
public IReadOnlyList<ScannerInfo> ScannerInfos => _scannerInfos;
```

```csharp
// 在行为方法区域，与其他 Add 方法放在一起
/// <summary>
/// 追加扫描仪信息
/// </summary>
public void AddScannerInfo(ScannerInfo info) {
    if (info is null) {
        throw new ArgumentNullException(nameof(info), "扫描仪信息不能为空");
    }
    _scannerInfos.Add(info);
}
```

**一对一单例对象**（例如一个包裹只关联一个扫描仪信息）：

```csharp
// 在属性区域，与其他单例属性放在一起
public ScannerInfo? ScannerInfo { get; private set; }
```

```csharp
// 在行为方法区域，与其他 Set 方法放在一起
/// <summary>
/// 设置扫描仪信息
/// </summary>
public void SetScannerInfo(ScannerInfo info) {
    ScannerInfo = info ?? throw new ArgumentNullException(nameof(info), "扫描仪信息不能为空");
}
```

---

### 第 3 步：`ParcelEntityTypeConfiguration.cs` — 添加 EF Core 配置块

在 `Configure()` 方法末尾，紧跟其他同类配置块之后追加：

**一对多（`OwnsMany`）示例**：

```csharp
builder.OwnsMany(x => x.ScannerInfos, b => {
    b.ToTable("Parcel_ScannerInfos", SchemaDbo);
    b.WithOwner().HasForeignKey("ParcelId");

    b.Property<long>("Id").ValueGeneratedOnAdd();
    b.HasKey("Id");

    b.Property(x => x.ScannerId).HasColumnName("ScannerId").HasMaxLength(MaxCode128).IsRequired();
    b.Property(x => x.ScanTime).HasColumnName("ScanTime").IsRequired();
    b.Property(x => x.RawResult).HasColumnName("RawResult").HasMaxLength(MaxText1024);

    b.HasIndex("ParcelId");
    b.HasIndex("ScanTime");
});
```

**一对一（`OwnsOne`）示例**：

```csharp
builder.OwnsOne(x => x.ScannerInfo, b => {
    b.ToTable("Parcel_ScannerInfos", SchemaDbo);
    b.WithOwner().HasForeignKey("ParcelId");

    b.Property<long>("Id").ValueGeneratedOnAdd();
    b.HasKey("Id");

    b.Property(x => x.ScannerId).HasColumnName("ScannerId").HasMaxLength(MaxCode128).IsRequired();
    b.Property(x => x.ScanTime).HasColumnName("ScanTime").IsRequired();
    b.Property(x => x.RawResult).HasColumnName("RawResult").HasMaxLength(MaxText1024);

    b.HasIndex("ParcelId");
});
```

**字段长度常量速查**（已在 `ParcelEntityTypeConfiguration` 中定义）：

| 常量名 | 值 | 典型用途 |
|--------|----|---------|
| `MaxCode128` | 128 | 编码类字段（编号、名称等） |
| `MaxText512` | 512 | 中等文本（URL、路径等） |
| `MaxText1024` | 1024 | 较长文本（序列化字符串等） |
| `MaxText2048` | 2048 | 长文本（Header、Body、异常信息等） |

---

### 第 4 步：更新 `README.md`

新增文件后，必须同步更新 `README.md` 中以下两个章节：

1. **"仓库文件结构（当前）"** 的文件树 — 在对应路径下添加新文件行：
   ```
   │           └── ScannerInfo.cs（扫描仪信息值对象）
   ```

2. **"各层级与各文件作用说明（逐项）"** 的 `ValueObjects` 小节 — 补充一行：
   ```
   - `ScannerInfo.cs`：扫描仪信息值对象。
   ```

3. **"本次更新内容"** — 说明本次新增的属性/值对象的用途与背景。

---

### 第 5 步：执行 EF Core 迁移

```bash
dotnet ef migrations add Add_Parcel_ScannerInfo \
  --project Zeye.Sorting.Hub.Infrastructure \
  --startup-project Zeye.Sorting.Hub.Host
```

---

## 完整检查清单

新增属性完成后，逐条确认：

| 检查项 | 情形一 | 情形二 | 情形三 |
|--------|--------|--------|--------|
| `Parcel.cs` 已添加属性（`{ get; private set; }`） | ✅ | — | ✅ |
| 值对象文件已添加/新建（`{ get; init; }`） | — | ✅ | ✅ |
| `Parcel.cs` 已添加 `Set/Add` 行为方法（情形三） | — | — | ✅ |
| `Create()` 工厂方法已同步（若属性在创建时确定） | 可选 | — | — |
| `ParcelEntityTypeConfiguration.cs` 已添加映射 | ✅ | ✅ | ✅ |
| 迁移文件已生成（`dotnet ef migrations add`） | ✅ | ✅ | ✅ |
| `README.md` 文件树与逐项说明已更新（新增文件时） | — | — | ✅ |
| 时间属性使用 `DateTime.Now`（禁止 UTC） | ✅ | ✅ | ✅ |
| 无 ORM 特性混入 Domain 层（无 `[Column]`、`[Table]`） | ✅ | ✅ | ✅ |
| 若将来 Application/Contracts 层有对应 DTO，需同步更新 | 待定 | 待定 | 待定 |

---

## 附录：当前 Parcel 聚合根主表字段一览

> 位于 `dbo.Parcels`，对应 `Parcel` 类中的直接标量属性。

| 属性名 | 类型 | 可空 | 说明 |
|--------|------|------|------|
| `Id` | `long` | 否 | 主键，自增 |
| `CreatedTime` | `DateTime` | 否 | 创建时间（审计字段） |
| `ModifyTime` | `DateTime` | 否 | 修改时间（审计字段） |
| `ModifyIp` | `string` | 否 | 修改来源 IP（审计字段） |
| `ParcelTimestamp` | `long` | 否 | 包裹时间戳 |
| `Type` | `ParcelType` | 否 | 包裹类型 |
| `Status` | `ParcelStatus` | 否 | 包裹状态（待操作/已完成/分拣异常） |
| `ExceptionType` | `ParcelExceptionType?` | 是 | 分拣异常类型（仅 `Status=SortingException` 时有值） |
| `NoReadType` | `NoReadType` | 否 | 无码/难码类型 |
| `SorterCarrierId` | `int?` | 是 | 小车编号 |
| `SegmentCodes` | `string?` | 是 | 三段码 |
| `LifecycleMilliseconds` | `long?` | 是 | 包裹生命周期（毫秒） |
| `TargetChuteId` | `long` | 否 | 目标格口 Id |
| `ActualChuteId` | `long` | 否 | 实际落格 Id |
| `BarCodes` | `string` | 否 | 主条码 |
| `Weight` | `decimal` | 否 | 重量 |
| `RequestStatus` | `ApiRequestStatus` | 否 | 外部接口访问状态 |
| `BagCode` | `string` | 否 | 集包号 |
| `WorkstationName` | `string` | 否 | 工作台 |
| `IsSticking` | `bool` | 否 | 是否叠包 |
| `Length` | `decimal` | 否 | 长度 |
| `Width` | `decimal` | 否 | 宽度 |
| `Height` | `decimal` | 否 | 高度 |
| `Volume` | `decimal` | 否 | 体积 |
| `ScannedTime` | `DateTime` | 否 | 扫码时间 |
| `DischargeTime` | `DateTime` | 否 | 落格时间 |
| `CompletedTime` | `DateTime?` | 是 | 包裹完结时间 |
| `HasImages` | `bool` | 否 | 是否有图片 |
| `HasVideos` | `bool` | 否 | 是否有视频 |
| `Coordinate` | `string` | 否 | 包裹坐标位置 |
