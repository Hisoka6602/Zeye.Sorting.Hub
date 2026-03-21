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
        /// <summary>
        /// 执行逻辑：Configure。
        /// </summary>
        public void Configure(EntityTypeBuilder<Parcel> builder) {
            builder.ToTable("Parcels");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.HasIndex(x => x.ParcelTimestamp);
            builder.HasIndex(x => x.ScannedTime);
            builder.HasIndex(x => x.CreatedTime);
            builder.HasIndex(x => x.BagCode);
            builder.HasIndex(x => new { x.Status, x.ScannedTime });
            builder.HasIndex(x => new { x.NoReadType, x.ScannedTime });
            builder.HasIndex(x => new { x.RequestStatus, x.ScannedTime });
            builder.HasIndex(x => new { x.Status, x.ExceptionType, x.ScannedTime });
            builder.HasIndex(x => new { x.ActualChuteId, x.DischargeTime });
            builder.HasIndex(x => new { x.TargetChuteId, x.ScannedTime });
            builder.HasIndex(x => new { x.WorkstationName, x.ScannedTime });

            // BagInfo：多 Parcel -> 1 BagInfo（独立表实体 + 影子外键）
            builder.Property<long?>("BagId").HasColumnName("BagId");
            builder.HasIndex("BagId");
            builder.HasOne(x => x.BagInfo)
                .WithMany()
                .HasForeignKey("BagId")
                .OnDelete(DeleteBehavior.Restrict);

            // 值对象：一对一（独立表）
            builder.OwnsOne(x => x.VolumeInfo, b => {
                b.ToTable("Parcel_VolumeInfos");
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
            });

            builder.OwnsOne(x => x.ChuteInfo, b => {
                b.ToTable("Parcel_ChuteInfos");
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("TargetChuteId");
                b.HasIndex("ActualChuteId");
            });

            builder.OwnsOne(x => x.SorterCarrierInfo, b => {
                b.ToTable("Parcel_SorterCarrierInfos");
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("SorterCarrierId");
            });

            builder.OwnsOne(x => x.DeviceInfo, b => {
                b.ToTable("Parcel_DeviceInfos");
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property(x => x.ParcelId).HasColumnName("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("MachineCode");
            });

            builder.OwnsOne(x => x.GrayDetectorInfo, b => {
                b.ToTable("Parcel_GrayDetectorInfos");
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("CarrierNumber");
            });

            builder.OwnsOne(x => x.StickingParcelInfo, b => {
                b.ToTable("Parcel_StickingParcelInfos");
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property(x => x.ParcelId).HasColumnName("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
            });

            builder.OwnsOne(x => x.ParcelPositionInfo, b => {
                b.ToTable("Parcel_PositionInfos");
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property(x => x.ParcelId).HasColumnName("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
            });

            // 值对象：一对多（独立表）
            builder.OwnsMany(x => x.BarCodeInfos, b => {
                b.ToTable("Parcel_BarCodeInfos");
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property(x => x.ParcelId).HasColumnName("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("BarCode", "ParcelId");
                b.HasIndex("CapturedTime");
            });

            builder.OwnsMany(x => x.WeightInfos, b => {
                b.ToTable("Parcel_WeightInfos");
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("WeighingTime");
            });

            builder.OwnsMany(x => x.ApiRequests, b => {
                b.ToTable("Parcel_ApiRequests");
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("RequestTime");
                b.HasIndex("ApiType");
            });

            builder.OwnsMany(x => x.CommandInfos, b => {
                b.ToTable("Parcel_CommandInfos");
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("GeneratedTime");
                b.HasIndex("ActionType");
            });

            builder.OwnsMany(x => x.ImageInfos, b => {
                b.ToTable("Parcel_ImageInfos");
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property(x => x.ParcelId).HasColumnName("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("ImageType");
            });

            builder.OwnsMany(x => x.VideoInfos, b => {
                b.ToTable("Parcel_VideoInfos");
                b.WithOwner().HasForeignKey("ParcelId");
                b.Property(x => x.ParcelId).HasColumnName("ParcelId");
                b.Property<long>("Id").ValueGeneratedOnAdd();
                b.HasKey("Id");

                b.HasIndex("ParcelId");
                b.HasIndex("NodeType");
                b.HasIndex("NvrSerialNumber");
            });
        }
    }
}
