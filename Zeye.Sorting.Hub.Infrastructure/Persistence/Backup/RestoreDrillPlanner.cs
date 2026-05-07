using System.Text;
using Microsoft.Extensions.Hosting;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 恢复演练与 Runbook 规划器。
/// </summary>
public sealed class RestoreDrillPlanner {
    /// <summary>
    /// 内容根环境信息。
    /// </summary>
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// 初始化恢复演练与 Runbook 规划器。
    /// </summary>
    /// <param name="hostEnvironment">内容根环境信息。</param>
    public RestoreDrillPlanner(IHostEnvironment hostEnvironment) {
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
    }

    /// <summary>
    /// 写入恢复 Runbook 与演练记录。
    /// </summary>
    /// <param name="plan">备份计划。</param>
    /// <param name="options">备份配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>资产路径。</returns>
    public async Task<(string RestoreRunbookPath, string DrillRecordPath)> WriteArtifactsAsync(
        BackupPlan plan,
        BackupOptions options,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(options);

        // 步骤 1：解析目录并确保目录存在，避免运行期因相对路径漂移导致资产丢失。
        var runbookDirectory = ResolveDirectory(options.RestoreRunbookDirectory);
        var drillRecordDirectory = ResolveDirectory(options.DrillRecordDirectory);
        Directory.CreateDirectory(runbookDirectory);
        Directory.CreateDirectory(drillRecordDirectory);

        // 步骤 2：生成稳定文件名，保证同一 provider 重复执行时覆盖最新指导资产，而不是无限膨胀。
        var providerSegment = BackupFileNamePolicy.SanitizeSegment(plan.ConfiguredProviderName);
        var runbookPath = Path.Combine(runbookDirectory, $"数据库恢复Runbook-{providerSegment}.md");
        var drillRecordPath = Path.Combine(drillRecordDirectory, $"备份恢复演练记录-{providerSegment}.md");

        // 步骤 3：分别使用临时文件 + 原子替换写入两份资产，避免异常时丢失历史可用文档。
        await WriteTextAtomicallyAsync(runbookPath, BuildRunbookContent(plan), cancellationToken);
        await WriteTextAtomicallyAsync(drillRecordPath, BuildDrillRecordContent(plan), cancellationToken);

        return (runbookPath, drillRecordPath);
    }

    /// <summary>
    /// 解析目录绝对路径。
    /// </summary>
    /// <param name="directory">配置目录。</param>
    /// <returns>绝对路径。</returns>
    internal string ResolveDirectory(string directory) {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (Path.IsPathRooted(directory)) {
            return directory;
        }

        return Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, directory));
    }

    /// <summary>
    /// 以临时文件 + 原子替换方式写入文本。
    /// </summary>
    /// <param name="targetPath">目标路径。</param>
    /// <param name="content">文本内容。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    private static async Task WriteTextAtomicallyAsync(string targetPath, string content, CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException($"目标路径缺少目录：{targetPath}");
        var tempFilePath = Path.Combine(targetDirectory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try {
            await File.WriteAllTextAsync(tempFilePath, content, Encoding.UTF8, cancellationToken);
            File.Move(tempFilePath, targetPath, overwrite: true);
        }
        finally {
            if (File.Exists(tempFilePath)) {
                File.Delete(tempFilePath);
            }
        }
    }

    /// <summary>
    /// 构建恢复 Runbook 内容。
    /// </summary>
    /// <param name="plan">备份计划。</param>
    /// <returns>Markdown 文本。</returns>
    private static string BuildRunbookContent(BackupPlan plan) {
        var builder = new StringBuilder();
        builder.AppendLine("# 数据库恢复 Runbook");
        builder.AppendLine();
        builder.AppendLine($"> 生成时间：{plan.GeneratedAtLocal:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"> Provider：{plan.ProviderName}");
        builder.AppendLine($"> 数据库：{plan.DatabaseName}");
        builder.AppendLine($"> 备份文件：`{plan.PlannedBackupFilePath}`");
        builder.AppendLine();
        builder.AppendLine("## 恢复边界");
        builder.AppendLine();
        builder.AppendLine("- 当前阶段仅生成 Runbook，不自动执行生产恢复。");
        builder.AppendLine("- 恢复前必须确认备份文件存在、文件时间满足要求，并完成人工审批。");
        builder.AppendLine("- 恢复后必须执行应用只读验证、关键表抽样校验与健康探针复查。");
        builder.AppendLine();
        builder.AppendLine("## 建议步骤");
        builder.AppendLine();
        builder.AppendLine("1. 停止写入流量并冻结危险治理动作。");
        builder.AppendLine($"2. 校验目标备份文件：`{plan.PlannedBackupFilePath}`。");
        builder.AppendLine($"3. 参考以下命令执行受控备份/恢复准备：`{plan.CommandText}`。");
        builder.AppendLine("4. 在隔离环境先完成恢复演练，确认表结构、行数与关键索引正常。");
        builder.AppendLine("5. 人工执行生产恢复后，重新启动服务并核对 `/health/ready`。");
        return builder.ToString();
    }

    /// <summary>
    /// 构建恢复演练记录内容。
    /// </summary>
    /// <param name="plan">备份计划。</param>
    /// <returns>Markdown 文本。</returns>
    private static string BuildDrillRecordContent(BackupPlan plan) {
        var builder = new StringBuilder();
        builder.AppendLine("# 备份恢复演练记录");
        builder.AppendLine();
        builder.AppendLine($"> 演练生成时间：{plan.GeneratedAtLocal:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"> Provider：{plan.ProviderName}");
        builder.AppendLine($"> 数据库：{plan.DatabaseName}");
        builder.AppendLine();
        builder.AppendLine("## 演练目标");
        builder.AppendLine();
        builder.AppendLine("- 验证当前备份命令、备份目录与恢复 Runbook 已准备完成。");
        builder.AppendLine("- 验证生产恢复仍保持人工执行边界，不引入自动危险恢复。");
        builder.AppendLine();
        builder.AppendLine("## 演练输入");
        builder.AppendLine();
        builder.AppendLine($"- 计划备份文件：`{plan.PlannedBackupFilePath}`");
        builder.AppendLine($"- 计划命令：`{plan.CommandText}`");
        builder.AppendLine();
        builder.AppendLine("## 演练结论");
        builder.AppendLine();
        builder.AppendLine("- 当前阶段已具备备份命令生成、备份文件校验与恢复文档输出能力。");
        builder.AppendLine("- 真正恢复动作仍需人工审批与隔离环境先行演练。");
        return builder.ToString();
    }
}
