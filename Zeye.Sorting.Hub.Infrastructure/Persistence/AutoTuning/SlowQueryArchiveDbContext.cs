using Microsoft.EntityFrameworkCore;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>后台专用 EF Core SQLite 归档上下文，不注册业务诊断拦截器，防止递归采样。</summary>
internal sealed class SlowQueryArchiveDbContext : DbContext {
    /// <summary>独立归档数据库绝对路径。</summary>
    private readonly string _path;
    /// <summary>关联后台专用数据库文件。</summary>
    internal SlowQueryArchiveDbContext(string path) => _path = path;
    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = _path, Pooling = false, DefaultTimeout = 5 }.ConnectionString);
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<SlowQueryArchiveRow>(row => {
            row.HasKey(item => item.Id); row.Property(item => item.Id).ValueGeneratedOnAdd();
            row.Property(item => item.Fingerprint).HasMaxLength(16); row.HasIndex(item => item.OccurredAt);
        });
    }
}
