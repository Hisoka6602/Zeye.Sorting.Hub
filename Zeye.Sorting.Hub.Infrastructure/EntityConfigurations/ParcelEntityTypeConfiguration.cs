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

        /// <summary>
        /// 方法：Configure。
        /// </summary>
        public void Configure(EntityTypeBuilder<Parcel> builder) {
            builder.ToTable("Parcels", SchemaDbo);

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

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
                b.HasIndex("NvrSerialNumber");
            });
        }
    }
}
