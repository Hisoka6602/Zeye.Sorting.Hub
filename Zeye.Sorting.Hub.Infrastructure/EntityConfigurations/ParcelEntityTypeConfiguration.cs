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

        private const string SchemaDbo = "dbo";

        public void Configure(EntityTypeBuilder<Parcel> builder) {
            builder.ToTable("Parcels", SchemaDbo);

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            // decimal 精度：无法用 BCL 特征标记，仍在此配置
            builder.Property(x => x.Weight).HasPrecision(18, 3);
            builder.Property(x => x.Length).HasPrecision(18, 3);
            builder.Property(x => x.Width).HasPrecision(18, 3);
            builder.Property(x => x.Height).HasPrecision(18, 3);
            builder.Property(x => x.Volume).HasPrecision(18, 3);

            // 索引（按常用查询条件/时间维度加速）
            builder.HasIndex(x => x.ParcelTimestamp);
            builder.HasIndex(x => x.BagCode);
            builder.HasIndex(x => x.WorkstationName);
            builder.HasIndex(x => x.ScannedTime);

            // BagInfo：多 Parcel -> 1 BagInfo（独立表实体 + 影子外键）
            builder.Property<long?>("BagId").HasColumnName("BagId");
            builder.HasIndex("BagId");
            builder.HasOne(x => x.BagInfo)
                .WithMany()
                .HasForeignKey("BagId")
                .OnDelete(DeleteBehavior.Restrict);

            // 值对象：一对一（独立表）
            builder.OwnsOne(x => x.VolumeInfo, b => {
                b.ToTable("Parcel_VolumeInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.FormattedLength).HasPrecision(18, 3);
                b.Property(x => x.FormattedWidth).HasPrecision(18, 3);
                b.Property(x => x.FormattedHeight).HasPrecision(18, 3);
                b.Property(x => x.FormattedVolume).HasPrecision(18, 3);
                b.Property(x => x.AdjustedLength).HasPrecision(18, 3);
                b.Property(x => x.AdjustedWidth).HasPrecision(18, 3);
                b.Property(x => x.AdjustedHeight).HasPrecision(18, 3);
                b.Property(x => x.AdjustedVolume).HasPrecision(18, 3);

                b.HasIndex("ParcelId");
            });

            builder.OwnsOne(x => x.ChuteInfo, b => {
                b.ToTable("Parcel_ChuteInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("TargetChuteId");
                b.HasIndex("ActualChuteId");
            });

            builder.OwnsOne(x => x.SorterCarrierInfo, b => {
                b.ToTable("Parcel_SorterCarrierInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.ConveyorSpeedWhenLoaded).HasPrecision(18, 3);

                b.HasIndex("ParcelId");
                b.HasIndex("SorterCarrierId");
            });

            builder.OwnsOne(x => x.DeviceInfo, b => {
                b.ToTable("Parcel_DeviceInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("MachineCode");
            });

            builder.OwnsOne(x => x.GrayDetectorInfo, b => {
                b.ToTable("Parcel_GrayDetectorInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("CarrierNumber");
            });

            builder.OwnsOne(x => x.StickingParcelInfo, b => {
                b.ToTable("Parcel_StickingParcelInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
            });

            builder.OwnsOne(x => x.ParcelPositionInfo, b => {
                b.ToTable("Parcel_PositionInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.X1).HasPrecision(18, 3);
                b.Property(x => x.X2).HasPrecision(18, 3);
                b.Property(x => x.Y1).HasPrecision(18, 3);
                b.Property(x => x.Y2).HasPrecision(18, 3);
                b.Property(x => x.BackgroundX1).HasPrecision(18, 3);
                b.Property(x => x.BackgroundX2).HasPrecision(18, 3);
                b.Property(x => x.BackgroundY1).HasPrecision(18, 3);
                b.Property(x => x.BackgroundY2).HasPrecision(18, 3);

                b.HasIndex("ParcelId");
            });

            // 值对象：一对多（独立表）
            builder.OwnsMany(x => x.BarCodeInfos, b => {
                b.ToTable("Parcel_BarCodeInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("BarCode");
                b.HasIndex("CapturedTime");
            });

            builder.OwnsMany(x => x.WeightInfos, b => {
                b.ToTable("Parcel_WeightInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.Property(x => x.FormattedWeight).HasPrecision(18, 3);
                b.Property(x => x.AdjustedWeight).HasPrecision(18, 3);

                b.HasIndex("ParcelId");
                b.HasIndex("WeighingTime");
            });

            builder.OwnsMany(x => x.ApiRequests, b => {
                b.ToTable("Parcel_ApiRequests", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("RequestTime");
                b.HasIndex("ApiType");
            });

            builder.OwnsMany(x => x.CommandInfos, b => {
                b.ToTable("Parcel_CommandInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("GeneratedTime");
                b.HasIndex("ActionType");
            });

            builder.OwnsMany(x => x.ImageInfos, b => {
                b.ToTable("Parcel_ImageInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("ImageType");
            });

            builder.OwnsMany(x => x.VideoInfos, b => {
                b.ToTable("Parcel_VideoInfos", SchemaDbo);
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("NodeType");
            });
        }
    }
}
