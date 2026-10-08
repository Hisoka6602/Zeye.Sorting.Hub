using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Zeye.Sorting.Hub.Infrastructure.Configuration;

/// <summary>使用 EF Core 读写独立的配置历史库，不参与业务库的分表和迁移。</summary>
internal sealed class ConfigurationHistoryDbContext(DbContextOptions<ConfigurationHistoryDbContext> options) : DbContext(options) {
    /// <summary>映射版本一的既有列和 SQLite 隐式行号，保证同毫秒记录仍按插入顺序排序。</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        var history = modelBuilder.Entity<ConfigurationHistoryRecord>();
        history.ToTable("ConfigurationChanges", table => table.ExcludeFromMigrations());
        history.HasKey(row => row.ChangeKey);
        history.Property(row => row.ChangeKey).HasColumnName("Id");
        history.Property(row => row.ChangedKeysJson).HasColumnName("ChangedKeys");
        history.Property<long>("Sequence").HasColumnName("rowid").ValueGeneratedOnAdd()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        history.HasIndex(row => new { row.DocumentKey, row.RecordedAtLocal }).HasDatabaseName("IX_ConfigurationChanges_Key_Time");
    }
}
