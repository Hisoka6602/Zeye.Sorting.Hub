using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations {

    /// <summary>
    /// Parcel 聚合 EF Core 映射（Infrastructure 层）
    /// </summary>
    public sealed class ParcelEntityTypeConfiguration : IEntityTypeConfiguration<Parcel> {

        // 默认数据库架构
        private const string SchemaDbo = "dbo";

        // 常用字段长度约束（统一管理，避免魔法数字散落在配置中）
        private const int MaxCode128 = 128;

        private const int MaxText512 = 512;
        private const int MaxText1024 = 1024;
        private const int MaxText2048 = 2048;

        public void Configure(EntityTypeBuilder<Parcel> builder) {
            // -----------------------------
            // 主表：Parcel（聚合根表）
            // -----------------------------
            builder.ToTable("Parcels", SchemaDbo);

            // 主键
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .HasColumnName("Id")
                // 主键自增（由数据库生成）
                .ValueGeneratedOnAdd();

            // -----------------------------
            // 审计字段（来自 AuditableEntity）
            // -----------------------------
            builder.Property(x => x.CreatedTime)
                .HasColumnName("CreatedTime")
                .IsRequired();

            builder.Property(x => x.ModifyTime)
                .HasColumnName("ModifyTime")
                .IsRequired();

            builder.Property(x => x.ModifyIp)
                .HasColumnName("ModifyIp")
                .HasMaxLength(64)
                .IsRequired();

            // -----------------------------
            // Parcel 主体字段（聚合根的普通标量字段）
            // -----------------------------
            builder.Property(x => x.ParcelTimestamp)
                .HasColumnName("ParcelTimestamp")
                .IsRequired();

            builder.Property(x => x.Type)
                .HasColumnName("Type")
                .IsRequired();

            builder.Property(x => x.Status)
                .HasColumnName("Status")
                .IsRequired();

            builder.Property(x => x.NoReadType)
                .HasColumnName("NoReadType")
                .IsRequired();

            builder.Property(x => x.SorterCarrierId)
                .HasColumnName("SorterCarrierId");

            builder.Property(x => x.SegmentCodes)
                .HasColumnName("SegmentCodes")
                // 多段编码整体落库为字符串（长度受限）
                .HasMaxLength(MaxText512);

            builder.Property(x => x.LifecycleMilliseconds)
                .HasColumnName("LifecycleMilliseconds");

            builder.Property(x => x.TargetChuteId)
                .HasColumnName("TargetChuteId")
                .IsRequired();

            builder.Property(x => x.ActualChuteId)
                .HasColumnName("ActualChuteId")
                .IsRequired();

            builder.Property(x => x.BarCodes)
                .HasColumnName("BarCodes")
                // 所有条码拼接后的文本（仍然保留一对多明细表 BarCodeInfos）
                .HasMaxLength(MaxText1024)
                .IsRequired();

            builder.Property(x => x.Weight)
                .HasColumnName("Weight")
                // 十进制精度：18,3
                .HasPrecision(18, 3)
                .IsRequired();

            builder.Property(x => x.RequestStatus)
                .HasColumnName("RequestStatus")
                .IsRequired();

            builder.Property(x => x.BagCode)
                .HasColumnName("BagCode")
                .HasMaxLength(MaxCode128)
                .IsRequired();

            builder.Property(x => x.WorkstationName)
                .HasColumnName("WorkstationName")
                .HasMaxLength(MaxCode128)
                .IsRequired();

            builder.Property(x => x.IsStacked)
                .HasColumnName("IsStacked")
                .IsRequired();

            builder.Property(x => x.Length)
                .HasColumnName("Length")
                .HasPrecision(18, 3)
                .IsRequired();

            builder.Property(x => x.Width)
                .HasColumnName("Width")
                .HasPrecision(18, 3)
                .IsRequired();

            builder.Property(x => x.Height)
                .HasColumnName("Height")
                .HasPrecision(18, 3)
                .IsRequired();

            builder.Property(x => x.Volume)
                .HasColumnName("Volume")
                .HasPrecision(18, 3)
                .IsRequired();

            builder.Property(x => x.ScannedTime)
                .HasColumnName("ScannedTime")
                .IsRequired();

            builder.Property(x => x.DischargeTime)
                .HasColumnName("DischargeTime")
                .IsRequired();

            builder.Property(x => x.CompletedTime)
                .HasColumnName("CompletedTime");

            builder.Property(x => x.HasImages)
                .HasColumnName("HasImages")
                .IsRequired();

            builder.Property(x => x.HasVideos)
                .HasColumnName("HasVideos")
                .IsRequired();

            builder.Property(x => x.Coordinate)
                .HasColumnName("Coordinate")
                // 坐标数据序列化后的文本
                .HasMaxLength(MaxText1024)
                .IsRequired();

            // -----------------------------
            // 索引（按常用查询条件/时间维度加速）
            // -----------------------------
            builder.HasIndex(x => x.ParcelTimestamp);
            builder.HasIndex(x => x.BagCode);
            builder.HasIndex(x => x.WorkstationName);
            builder.HasIndex(x => x.ScannedTime);

            // -----------------------------
            // BagInfo：多 Parcel -> 1 BagInfo（独立表实体 + 影子外键）
            // 说明：
            // - Parcel 聚合根上暴露 BagInfo 导航属性
            // - 但外键字段不一定在领域模型中出现，使用影子属性 BagId
            // -----------------------------
            builder.Property<long?>("BagId")
                .HasColumnName("BagId");

            builder.HasIndex("BagId");

            builder.HasOne(x => x.BagInfo)
                .WithMany()
                .HasForeignKey("BagId")
                // 限制级联删除，避免删除 Bag 误删 Parcel
                .OnDelete(DeleteBehavior.Restrict);

            // -----------------------------
            // 值对象：一对一（独立表）
            // 说明：
            // - OwnsOne：值对象随聚合根生命周期
            // - 但这里落库到独立表，便于扩展字段/减少主表宽度
            // - 每个 owned 表都通过 ParcelId 外键回指主表
            // -----------------------------
            builder.OwnsOne(x => x.VolumeInfo, b => {
                b.ToTable("Parcel_VolumeInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");

                // 独立表主键（自增），避免以 ParcelId 作为主键带来的扩展限制
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.SourceType).HasColumnName("SourceType").IsRequired();
                b.Property(x => x.RawVolume).HasColumnName("RawVolume").HasMaxLength(MaxText512);
                b.Property(x => x.EvidenceCode).HasColumnName("EvidenceCode").HasMaxLength(MaxCode128);

                b.Property(x => x.FormattedLength).HasColumnName("FormattedLength").HasPrecision(18, 3).IsRequired();
                b.Property(x => x.FormattedWidth).HasColumnName("FormattedWidth").HasPrecision(18, 3).IsRequired();
                b.Property(x => x.FormattedHeight).HasColumnName("FormattedHeight").HasPrecision(18, 3).IsRequired();
                b.Property(x => x.FormattedVolume).HasColumnName("FormattedVolume").HasPrecision(18, 3).IsRequired();

                b.Property(x => x.AdjustedLength).HasColumnName("AdjustedLength").HasPrecision(18, 3);
                b.Property(x => x.AdjustedWidth).HasColumnName("AdjustedWidth").HasPrecision(18, 3);
                b.Property(x => x.AdjustedHeight).HasColumnName("AdjustedHeight").HasPrecision(18, 3);
                b.Property(x => x.AdjustedVolume).HasColumnName("AdjustedVolume").HasPrecision(18, 3);

                b.Property(x => x.MeasurementTime).HasColumnName("MeasurementTime").IsRequired();
                b.Property(x => x.BindTime).HasColumnName("BindTime");

                b.HasIndex("ParcelId");
            });

            builder.OwnsOne(x => x.ChuteInfo, b => {
                b.ToTable("Parcel_ChuteInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");

                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.TargetChuteId).HasColumnName("TargetChuteId");
                b.Property(x => x.ActualChuteId).HasColumnName("ActualChuteId");
                b.Property(x => x.BackupChuteId).HasColumnName("BackupChuteId");
                b.Property(x => x.LandedTime).HasColumnName("LandedTime").IsRequired();

                b.HasIndex("ParcelId");
                b.HasIndex("TargetChuteId");
                b.HasIndex("ActualChuteId");
            });

            builder.OwnsOne(x => x.SorterCarrierInfo, b => {
                b.ToTable("Parcel_SorterCarrierInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");

                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.SorterCarrierId).HasColumnName("SorterCarrierId").IsRequired();
                b.Property(x => x.LoadedTime).HasColumnName("LoadedTime").IsRequired();
                b.Property(x => x.ConveyorSpeedWhenLoaded).HasColumnName("ConveyorSpeedWhenLoaded").HasPrecision(18, 3).IsRequired();
                b.Property(x => x.LinkedCarrierCount).HasColumnName("LinkedCarrierCount").IsRequired();

                b.HasIndex("ParcelId");
                b.HasIndex("SorterCarrierId");
            });

            builder.OwnsOne(x => x.DeviceInfo, b => {
                b.ToTable("Parcel_DeviceInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");

                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.WorkstationName).HasColumnName("WorkstationName").HasMaxLength(MaxCode128).IsRequired();
                b.Property(x => x.MachineCode).HasColumnName("MachineCode").HasMaxLength(MaxCode128).IsRequired();
                b.Property(x => x.CustomName).HasColumnName("CustomName").HasMaxLength(MaxCode128).IsRequired();

                b.HasIndex("ParcelId");
                b.HasIndex("MachineCode");
            });

            builder.OwnsOne(x => x.GrayDetectorInfo, b => {
                b.ToTable("Parcel_GrayDetectorInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");

                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.CarrierNumber).HasColumnName("CarrierNumber").HasMaxLength(64).IsRequired();
                b.Property(x => x.AttachBoxInfo).HasColumnName("AttachBoxInfo").HasMaxLength(MaxText2048);
                b.Property(x => x.MainBoxInfo).HasColumnName("MainBoxInfo").HasMaxLength(MaxText2048);
                b.Property(x => x.LinkedCarrierCount).HasColumnName("LinkedCarrierCount").IsRequired();
                b.Property(x => x.CenterPosition).HasColumnName("CenterPosition").HasMaxLength(MaxText512);
                b.Property(x => x.ResultTime).HasColumnName("ResultTime").IsRequired();
                b.Property(x => x.RawResult).HasColumnName("RawResult").HasMaxLength(MaxText2048).IsRequired();

                b.HasIndex("ParcelId");
                b.HasIndex("CarrierNumber");
            });

            builder.OwnsOne(x => x.StackedParcelInfo, b => {
                b.ToTable("Parcel_StackedParcelInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");

                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.IsStacked).HasColumnName("IsStacked").IsRequired();
                b.Property(x => x.ReceiveTime).HasColumnName("ReceiveTime");
                b.Property(x => x.RawData).HasColumnName("RawData").HasMaxLength(MaxText2048);
                b.Property(x => x.ElapsedMilliseconds).HasColumnName("ElapsedMilliseconds");

                b.HasIndex("ParcelId");
            });

            builder.OwnsOne(x => x.ParcelPositionInfo, b => {
                b.ToTable("Parcel_PositionInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");

                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.X1).HasColumnName("X1").HasPrecision(18, 3).IsRequired();
                b.Property(x => x.X2).HasColumnName("X2").HasPrecision(18, 3).IsRequired();
                b.Property(x => x.Y1).HasColumnName("Y1").HasPrecision(18, 3).IsRequired();
                b.Property(x => x.Y2).HasColumnName("Y2").HasPrecision(18, 3).IsRequired();

                b.Property(x => x.BackgroundX1).HasColumnName("BackgroundX1").HasPrecision(18, 3).IsRequired();
                b.Property(x => x.BackgroundX2).HasColumnName("BackgroundX2").HasPrecision(18, 3).IsRequired();
                b.Property(x => x.BackgroundY1).HasColumnName("BackgroundY1").HasPrecision(18, 3).IsRequired();
                b.Property(x => x.BackgroundY2).HasColumnName("BackgroundY2").HasPrecision(18, 3).IsRequired();

                b.HasIndex("ParcelId");
            });

            // -----------------------------
            // 值对象：一对多（独立表）
            // 说明：
            // - OwnsMany：值对象集合随聚合根生命周期
            // - 每条明细拥有独立 Id（自增），便于追踪/扩展
            // -----------------------------
            builder.OwnsMany(x => x.BarCodeInfos, b => {
                b.ToTable("Parcel_BarCodeInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");

                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.BarCode).HasColumnName("BarCode").HasMaxLength(MaxCode128).IsRequired();
                b.Property(x => x.BarCodeType).HasColumnName("BarCodeType").IsRequired();
                b.Property(x => x.CapturedTime).HasColumnName("CapturedTime");

                b.HasIndex("ParcelId");
                b.HasIndex("BarCode");
                b.HasIndex("CapturedTime");
            });

            builder.OwnsMany(x => x.WeightInfos, b => {
                b.ToTable("Parcel_WeightInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");

                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.RawWeight).HasColumnName("RawWeight").HasMaxLength(MaxText512);
                b.Property(x => x.EvidenceCode).HasColumnName("EvidenceCode").HasMaxLength(MaxCode128);
                b.Property(x => x.FormattedWeight).HasColumnName("FormattedWeight").HasPrecision(18, 3).IsRequired();
                b.Property(x => x.WeighingTime).HasColumnName("WeighingTime").IsRequired();
                b.Property(x => x.AdjustedWeight).HasColumnName("AdjustedWeight").HasPrecision(18, 3);

                b.HasIndex("ParcelId");
                b.HasIndex("WeighingTime");
            });

            builder.OwnsMany(x => x.ApiRequests, b => {
                b.ToTable("Parcel_ApiRequests", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");

                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.ApiType).HasColumnName("ApiType").IsRequired();
                b.Property(x => x.RequestStatus).HasColumnName("RequestStatus").IsRequired();
                b.Property(x => x.RequestUrl).HasColumnName("RequestUrl").HasMaxLength(MaxText512).IsRequired();
                b.Property(x => x.QueryParams).HasColumnName("QueryParams").HasMaxLength(MaxText1024);
                b.Property(x => x.Headers).HasColumnName("Headers").HasMaxLength(MaxText2048);

                b.Property(x => x.RequestBody).HasColumnName("RequestBody");
                b.Property(x => x.ResponseBody).HasColumnName("ResponseBody");

                b.Property(x => x.RequestTime).HasColumnName("RequestTime").IsRequired();
                b.Property(x => x.ResponseTime).HasColumnName("ResponseTime");
                b.Property(x => x.ElapsedMilliseconds).HasColumnName("ElapsedMilliseconds").IsRequired();
                b.Property(x => x.Exception).HasColumnName("Exception").HasMaxLength(MaxText2048);
                b.Property(x => x.RawData).HasColumnName("RawData");
                b.Property(x => x.FormattedMessage).HasColumnName("FormattedMessage").HasMaxLength(MaxText1024);

                b.HasIndex("ParcelId");
                b.HasIndex("RequestTime");
                b.HasIndex("ApiType");
            });

            builder.OwnsMany(x => x.CommandInfos, b => {
                b.ToTable("Parcel_CommandInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");

                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.ProtocolType).HasColumnName("ProtocolType").IsRequired();
                b.Property(x => x.ProtocolName).HasColumnName("ProtocolName").HasMaxLength(MaxCode128).IsRequired();
                b.Property(x => x.ConnectionName).HasColumnName("ConnectionName").HasMaxLength(MaxCode128);
                b.Property(x => x.CommandPayload).HasColumnName("CommandPayload");
                b.Property(x => x.GeneratedTime).HasColumnName("GeneratedTime").IsRequired();
                b.Property(x => x.ActionType).HasColumnName("ActionType").IsRequired();
                b.Property(x => x.FormattedMessage).HasColumnName("FormattedMessage").HasMaxLength(MaxText1024);
                b.Property(x => x.Direction).HasColumnName("Direction").IsRequired();

                b.HasIndex("ParcelId");
                b.HasIndex("GeneratedTime");
                b.HasIndex("ActionType");
            });

            builder.OwnsMany(x => x.ImageInfos, b => {
                b.ToTable("Parcel_ImageInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");

                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.CameraName).HasColumnName("CameraName").HasMaxLength(MaxCode128);
                b.Property(x => x.CustomName).HasColumnName("CustomName").HasMaxLength(MaxCode128);
                b.Property(x => x.CameraSerialNumber).HasColumnName("CameraSerialNumber").HasMaxLength(MaxCode128);
                b.Property(x => x.ImageType).HasColumnName("ImageType").IsRequired();
                b.Property(x => x.RelativePath).HasColumnName("RelativePath").HasMaxLength(MaxText1024).IsRequired();
                b.Property(x => x.CaptureType).HasColumnName("CaptureType").IsRequired();

                b.HasIndex("ParcelId");
                b.HasIndex("ImageType");
            });

            builder.OwnsMany(x => x.VideoInfos, b => {
                b.ToTable("Parcel_VideoInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");

                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.Channel).HasColumnName("Channel").IsRequired();
                b.Property(x => x.NvrSerialNumber).HasColumnName("NvrSerialNumber").HasMaxLength(MaxCode128).IsRequired();
                b.Property(x => x.NodeType).HasColumnName("NodeType").IsRequired();

                b.HasIndex("ParcelId");
                b.HasIndex("NodeType");
                b.HasIndex("NvrSerialNumber");
            });

            // -----------------------------
            // 集合字段访问模式
            // 说明：
            // - 通过 Field 访问集合，支持领域模型使用私有字段保存集合
            // - 保持封装性，同时避免不必要的 setter 暴露
            // -----------------------------
            builder.Navigation(x => x.BarCodeInfos).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(x => x.WeightInfos).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(x => x.ApiRequests).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(x => x.CommandInfos).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(x => x.ImageInfos).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(x => x.VideoInfos).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
