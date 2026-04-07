using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence {

    /// <summary>
    /// 分拣中心 DbContext（仅负责映射与 DbSet，不执行运维动作）
    /// </summary>
    public sealed class SortingHubDbContext : DbContext {
        /// <summary>
        /// SQL Server 默认 schema。
        /// </summary>
        private const string SqlServerDefaultSchema = "dbo";

        /// <summary>
        /// 初始化 <see cref="SortingHubDbContext"/>。
        /// </summary>
        /// <param name="options">DbContext 配置选项。</param>
        public SortingHubDbContext(DbContextOptions<SortingHubDbContext> options) : base(options) {
        }

        /// <summary>
        /// 应用程序集内全部实体类型配置。
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            if (Database.ProviderName == DbProviderNames.SqlServer) {
                modelBuilder.HasDefaultSchema(SqlServerDefaultSchema);
            }

            // 统一应用所有 IEntityTypeConfiguration
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(SortingHubDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}
