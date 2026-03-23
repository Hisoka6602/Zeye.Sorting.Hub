namespace Zeye.Sorting.Hub.Infrastructure.Persistence {

    /// <summary>
    /// Parcel 相关索引名称常量（供迁移说明、治理审计与测试复用，避免多处硬编码漂移）。
    /// </summary>
    public static class ParcelIndexNames {
        /// <summary>
        /// Parcel 按袋号 + 扫描时间复合索引名。
        /// </summary>
        public const string BagCodeScannedTime = "IX_Parcels_BagCode_ScannedTime";

        /// <summary>
        /// Parcel 按实际格口 + 扫描时间复合索引名。
        /// </summary>
        public const string ActualChuteIdScannedTime = "IX_Parcels_ActualChuteId_ScannedTime";

        /// <summary>
        /// Parcel 按目标格口 + 扫描时间复合索引名。
        /// </summary>
        public const string TargetChuteIdScannedTime = "IX_Parcels_TargetChuteId_ScannedTime";

        /// <summary>
        /// Parcel 条码全文索引名（MySQL）。
        /// </summary>
        public const string BarCodesFullText = "FTX_Parcels_BarCodes";
    }
}
