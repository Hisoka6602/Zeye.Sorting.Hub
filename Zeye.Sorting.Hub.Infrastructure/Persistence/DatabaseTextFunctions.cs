using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence;

/// <summary>显式映射数据库文本运算，保留服务端排序规则，避免将查询改为客户端区域性比较。</summary>
internal static class DatabaseTextFunctions {
    /// <summary>数据库使用原有 LOWER 运算；内存查询使用固定区域性，结果不依赖进程语言。</summary>
    public static string? Lower(string? value) => value?.ToLowerInvariant();

    /// <summary>游标在数据库端按列的排序规则比较，与同一查询的 ORDER BY 保持一致。</summary>
    public static bool IsAfter(string value, string cursor) => string.CompareOrdinal(value, cursor) > 0;

    /// <summary>四种关系库共用标准 LOWER 和大于运算，无需安装额外数据库函数。</summary>
    internal static void Configure(ModelBuilder modelBuilder) {
        var lower = modelBuilder.HasDbFunction(typeof(DatabaseTextFunctions).GetMethod(nameof(Lower))!)
            .HasName("LOWER").IsBuiltIn();
        lower.HasParameter("value").PropagatesNullability();
        modelBuilder.HasDbFunction(typeof(DatabaseTextFunctions).GetMethod(nameof(IsAfter))!)
            .HasTranslation(arguments => new SqlBinaryExpression(ExpressionType.GreaterThan,
                arguments[0], arguments[1], typeof(bool), typeMapping: null));
    }
}
