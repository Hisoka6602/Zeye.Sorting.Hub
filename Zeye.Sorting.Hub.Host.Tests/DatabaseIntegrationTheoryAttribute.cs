namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>服务器数据库回归仅在显式启用的独立 Docker 验收环境运行。</summary>
public sealed class DatabaseIntegrationTheoryAttribute : TheoryAttribute {
    /// <summary>普通单元测试跳过外部数据库，启用时连接固定的验收端口。</summary>
    public DatabaseIntegrationTheoryAttribute() {
        if (Environment.GetEnvironmentVariable("ZEYE_DATABASE_VERIFICATION") != "1")
            Skip = "需启动 zeye-db-verification 数据库并设置 ZEYE_DATABASE_VERIFICATION=1。";
    }
}
