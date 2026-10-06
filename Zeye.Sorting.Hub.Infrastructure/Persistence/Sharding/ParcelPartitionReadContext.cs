using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>复用生产提供器和标量映射，以 EF Core 只读实体及 LINQ 合并实际物理分表。</summary>
internal sealed class ParcelPartitionReadContext<TReadModel> : DbContext, IParcelPartitionReadModelContext where TReadModel : class {
    /// <summary>每种读模型只构造一次标量投影表达式，查询日期继续独立参数化。</summary>
    private static readonly Expression<Func<TReadModel, TReadModel>> Projection = BuildProjection();
    /// <summary>来源持久化实体映射，不重新定义数据库列类型。</summary>
    public IEntityType SourceEntity { get; }
    /// <summary>仅包含所需标量字段的读模型类型。</summary>
    public Type ReadModelType => typeof(TReadModel);
    /// <summary>排序去重后的不可变物理后缀。</summary>
    private readonly string[] _suffixes;
    /// <summary>模型缓存按分表集合隔离，日期查询参数不参与模型缓存键。</summary>
    public string PartitionModelKey { get; }

    /// <summary>配置同一只读上下文中的共享实体，所有查询根使用同一连接及提供器。</summary>
    private ParcelPartitionReadContext(DbContextOptions<ParcelPartitionReadContext<TReadModel>> options, IEntityType sourceEntity,
        string[] suffixes) : base(options) {
        SourceEntity = sourceEntity; _suffixes = suffixes;
        PartitionModelKey = string.Join('|', suffixes);
    }

    /// <summary>复制已有连接、超时和诊断拦截器配置，不建立第二套提供器配置或修改写入模型。</summary>
    internal static ParcelPartitionReadContext<TReadModel> Create<TEntity>(SortingHubDbContext template,
        IReadOnlyList<string> suffixes) where TEntity : class {
        if (suffixes.Count == 0) throw new ArgumentException("至少提供一个物理分表。", nameof(suffixes));
        var validated = suffixes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        foreach (var suffix in validated) ParcelPartitionStore.ValidateSuffix(suffix);
        var entity = template.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"未配置 {typeof(TEntity).Name} 的持久化实体。");
        var options = new DbContextOptionsBuilder<ParcelPartitionReadContext<TReadModel>>();
        foreach (var extension in template.GetService<IDbContextOptions>().Extensions)
            ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(extension);
        options.ReplaceService<IModelCacheKeyFactory, ParcelPartitionReadModelCacheKeyFactory>()
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        return new(options.Options, entity, validated);
    }

    /// <summary>每个分表投影为同一 DTO 后使用 LINQ Concat，避免依赖不同实体根的集合运算。</summary>
    internal IQueryable<TReadModel> Query(IReadOnlyList<string> suffixes, string timeProperty,
        DateTime fromLocal, DateTime toLocal, bool includeEnd) {
        if (suffixes.Count == 0) throw new ArgumentException("至少提供一个物理分表。", nameof(suffixes));
        if (toLocal <= fromLocal || fromLocal.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified)
            || toLocal.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified))
            throw new ArgumentException("统计时间范围必须是有效的本地区间。");
        var time = ReadModelType.GetProperty(timeProperty)
            ?? throw new ArgumentException("时间字段必须包含在查询读模型中。", nameof(timeProperty));
        if (time.PropertyType != typeof(DateTime) && time.PropertyType != typeof(DateTime?))
            throw new ArgumentException("查询时间字段必须为本地时间。", nameof(timeProperty));
        var queries = suffixes.Distinct(StringComparer.Ordinal).Select(suffix => {
            if (Array.BinarySearch(_suffixes, suffix, StringComparer.Ordinal) < 0)
                throw new ArgumentException("物理分表不属于当前只读模型。", nameof(suffixes));
            var source = Set<TReadModel>(EntityName(suffix)).AsNoTracking();
            var filtered = time.PropertyType == typeof(DateTime?)
                ? source.Where(item => EF.Property<DateTime?>(item, timeProperty) >= fromLocal
                    && (includeEnd ? EF.Property<DateTime?>(item, timeProperty) <= toLocal : EF.Property<DateTime?>(item, timeProperty) < toLocal))
                : source.Where(item => EF.Property<DateTime>(item, timeProperty) >= fromLocal
                    && (includeEnd ? EF.Property<DateTime>(item, timeProperty) <= toLocal : EF.Property<DateTime>(item, timeProperty) < toLocal));
            return filtered.Select(Projection);
        });
        return queries.Aggregate((left, right) => left.Concat(right));
    }

    /// <summary>仅注册读模型需要的列；列名、精度、转换及可空性沿用写入模型。</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        var table = SourceEntity.GetTableName() ?? throw new InvalidOperationException("未配置来源实体的物理表名。");
        var schema = SourceEntity.GetSchema();
        var storeObject = StoreObjectIdentifier.Table(table, schema);
        foreach (var suffix in _suffixes) {
            modelBuilder.SharedTypeEntity(EntityName(suffix), ReadModelType, builder => {
                builder.HasNoKey();
                builder.ToTable(table + (suffix.Length == 0 ? "" : "_" + suffix), schema, mapped => mapped.ExcludeFromMigrations());
                foreach (var member in ReadModelType.GetProperties()) {
                    var source = SourceEntity.FindProperty(member.Name)
                        ?? throw new InvalidOperationException($"{member.Name} 不是来源实体的标量属性。");
                    if (member.PropertyType != source.ClrType) throw new InvalidOperationException($"{member.Name} 的读写类型不一致。");
                    var property = builder.Property(member.PropertyType, member.Name)
                        .HasColumnName(source.GetColumnName(storeObject)).HasColumnType(source.GetColumnType())
                        .IsRequired(!source.IsNullable);
                    if (source.GetMaxLength() is { } length) property.HasMaxLength(length);
                    if (source.GetPrecision() is { } precision) property.HasPrecision(precision, source.GetScale() ?? 0);
                    if (source.IsUnicode() is { } unicode) property.IsUnicode(unicode);
                    if (source.GetValueConverter() is { } converter) property.HasConversion(converter);
                }
            });
        }
    }

    /// <summary>共享实体名称只由本地读模型类型和已校验的后缀组成。</summary>
    private string EntityName(string suffix) => ReadModelType.FullName + ":" + suffix;

    /// <summary>共享实体投影到普通标量 DTO，使不同物理表可以由 EF 翻译为同一集合查询。</summary>
    private static Expression<Func<TReadModel, TReadModel>> BuildProjection() {
        var row = Expression.Parameter(typeof(TReadModel), "row");
        return Expression.Lambda<Func<TReadModel, TReadModel>>(Expression.MemberInit(
            Expression.New(typeof(TReadModel)), typeof(TReadModel).GetProperties().Select(property =>
                Expression.Bind(property, Expression.Property(row, property)))), row);
    }
}
