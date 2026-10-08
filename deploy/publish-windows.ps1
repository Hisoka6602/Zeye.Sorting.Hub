<# 中文说明：一次发布 Windows x64 自包含程序及前端，构建机需要 .NET 10 SDK 和 Node/npm。 #>
[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/windows-x64'),
    [switch]$SkipDependencyRestore,
    [switch]$ForceWebUiBuild,
    [switch]$ForceWebDependencyRestore,
    [string]$WebNpmRegistry,
    [string]$WebNpmFallbackRegistry
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '../Zeye.Sorting.Hub.Host/Zeye.Sorting.Hub.Host.csproj'
$publishPath = [System.IO.Path]::GetFullPath($OutputDirectory)
foreach ($commandName in @('dotnet', 'node', 'npm')) {
    if (-not (Get-Command $commandName -ErrorAction SilentlyContinue)) {
        throw "构建机缺少 $commandName；请安装 .NET 10 SDK 和 Node.js 24 LTS 后发布。"
    }
}

$publishArguments = @('publish', $projectPath, '-p:PublishProfile=Windows-x64', '-o', $publishPath)
if ($SkipDependencyRestore -and $ForceWebDependencyRestore) { throw 'SkipDependencyRestore 与 ForceWebDependencyRestore 不能同时使用。' }
if ($SkipDependencyRestore) { $publishArguments += '-p:RestoreWebDependencies=false' }
if ($ForceWebUiBuild) { $publishArguments += '-p:ForceWebUiBuild=true' }
if ($ForceWebDependencyRestore) { $publishArguments += '-p:ForceWebDependencyRestore=true' }
if ($WebNpmRegistry) { $publishArguments += "-p:WebNpmRegistry=$WebNpmRegistry" }
if ($WebNpmFallbackRegistry) { $publishArguments += "-p:WebNpmFallbackRegistry=$WebNpmFallbackRegistry" }
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw "前后端发布失败，退出码：$LASTEXITCODE。" }
foreach ($relativePath in @('Zeye.Sorting.Hub.Host.exe', 'wwwroot/index.html', 'Start-Hub.cmd', 'install.bat', 'uninstall.bat', 'install.sh', 'uninstall.sh', 'service.ps1', 'service.sh')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishPath $relativePath) -PathType Leaf)) {
        throw "发布包缺少 $relativePath，不能部署。"
    }
}
Write-Host "前后端已发布到：$publishPath"
Write-Host '将整个发布目录复制到目标 Windows 主机，配置数据库连接和初始化密钥，再以管理员身份运行 install.bat 安装服务；也可使用 Start-Hub.cmd 直接启动。'
Write-Host '页面与 API 共用 Host 监听地址，默认 http://127.0.0.1:5078/；目标机无需 Node、Nginx 或 .NET SDK。'
