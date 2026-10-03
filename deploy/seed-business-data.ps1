param(
    [int]$Count = 10000,
    [int]$Days = 30,
    [string]$PublicBaseUrl = 'http://127.0.0.1:4187',
    [switch]$Preview,
    [switch]$VerifyOnly
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'tools/BusinessDataSimulator/BusinessDataSimulator.csproj'
$publishDirectory = Join-Path ([IO.Path]::GetTempPath()) ('zeye-business-simulator-' + [guid]::NewGuid().ToString('N'))
& dotnet publish $projectPath -c Release --nologo -v quiet -clp:ErrorsOnly -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw '模拟数据工具发布失败。' }
$arguments = @('--count', $Count, '--days', $Days, '--public-base-url', $PublicBaseUrl)
if ($Preview) {
    & dotnet (Join-Path $publishDirectory 'BusinessDataSimulator.dll') @arguments
} else {
    # 凭据始终留在正在运行的本机Host容器中，不读取.env，也不导出环境变量。
    $composeArguments = @('compose', '--env-file', (Join-Path $PSScriptRoot '.env'), '-f', (Join-Path $PSScriptRoot 'compose.yaml'))
    $containerId = (& docker @composeArguments ps -q host).Trim()
    if ($LASTEXITCODE -ne 0 -or $containerId -notmatch '^[a-f0-9]{12,64}$') { throw '未发现本项目运行中的Host容器。' }
    & docker cp (Join-Path $publishDirectory '.') ($containerId + ':/tmp/zeye-business-simulator')
    if ($LASTEXITCODE -ne 0) { throw '模拟工具复制失败。' }
    $mode = if ($VerifyOnly) { '--verify-only' } else { '--write' }
    & docker @composeArguments exec -T host dotnet /tmp/zeye-business-simulator/BusinessDataSimulator.dll --local-docker $mode @arguments
}
if ($LASTEXITCODE -ne 0) { throw '模拟业务数据生成或复核失败。' }
Write-Host "工具发布目录：$publishDirectory"
