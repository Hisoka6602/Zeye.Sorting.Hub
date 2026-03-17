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

        public SortingHubDbContext(DbContextOptions<SortingHubDbContext> options) : base(options) {
        }

        /// <summary>
        /// 应用程序集内全部实体类型配置。
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            // 统一应用所有 IEntityTypeConfiguration
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(SortingHubDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}
