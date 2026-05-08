using System.IO;
using System.Linq;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 仓库文件读取入口。
/// </summary>
public static class RepositoryFileReader {
    /// <summary>
    /// 按相对路径读取仓库文件内容。
    /// </summary>
    /// <param name="pathSegments">相对路径片段。</param>
    /// <returns>文件内容。</returns>
    public static string ReadAllText(params string[] pathSegments) {
        var repositoryRoot = LocateRepositoryRoot();
        var filePath = Path.Combine(new[] { repositoryRoot }.Concat(pathSegments).ToArray());
        return File.ReadAllText(filePath);
    }

    /// <summary>
    /// 定位仓库根目录。
    /// </summary>
    /// <returns>仓库根目录绝对路径。</returns>
    private static string LocateRepositoryRoot() {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null) {
            var readmePath = Path.Combine(current.FullName, "README.md");
            var solutionPath = Path.Combine(current.FullName, "Zeye.Sorting.Hub.sln");
            if (File.Exists(readmePath) && File.Exists(solutionPath)) {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("未找到仓库根目录。");
    }
}
