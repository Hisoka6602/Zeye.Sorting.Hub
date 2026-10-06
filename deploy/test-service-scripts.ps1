<# 中文说明：验证服务安装脚本语法、预演及错误输入，不注册或停止真实服务。 #>
[CmdletBinding()]
param([string]$BashExecutable)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$hostDirectory = Join-Path $repositoryDirectory 'Zeye.Sorting.Hub.Host'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('Zeye Hub 服务脚本检查 ' + [Guid]::NewGuid().ToString('N'))
$scriptNames = @('install.bat', 'uninstall.bat', 'install.sh', 'uninstall.sh', 'service.ps1', 'service.sh')
$savedName = $env:ZEYE_SERVICE_NAME
$savedTimeout = $env:ZEYE_SERVICE_TIMEOUT_SECONDS
$savedDryRun = $env:ZEYE_SERVICE_DRY_RUN
$checks = 0
if (-not $BashExecutable) {
    $gitBash = if ($env:OS -eq 'Windows_NT') { Join-Path ${env:ProgramFiles} 'Git/bin/bash.exe' } else { '' }
    $BashExecutable = if ($gitBash -and (Test-Path -LiteralPath $gitBash)) { $gitBash } else { (Get-Command bash -ErrorAction Stop).Source }
}

# 中文说明：运行子进程，验证预期退出码；不把预期错误转换成主脚本中断。
function Assert-ScriptExit {
    param([string]$Command, [string[]]$Arguments, [int]$ExpectedExitCode = 0)
    Write-Verbose "检查 $Command（预期退出码 $ExpectedExitCode）"
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $output = & $Command @Arguments 2>&1; $actualExitCode = $LASTEXITCODE }
    finally { $ErrorActionPreference = $previousPreference }
    if ($actualExitCode -ne $ExpectedExitCode) { throw "预期退出码 $ExpectedExitCode，实际 $actualExitCode；输出：$output" }
    $script:checks++
}

try {
    $env:ZEYE_SERVICE_NAME = 'Zeye.Hub.ScriptTest'
    $env:ZEYE_SERVICE_TIMEOUT_SECONDS = '30'
    $env:ZEYE_SERVICE_DRY_RUN = 'false'
    New-Item -ItemType Directory -Path (Join-Path $testDirectory 'wwwroot') -Force | Out-Null
    foreach ($name in $scriptNames) { Copy-Item -LiteralPath (Join-Path $hostDirectory $name) -Destination $testDirectory }
    foreach ($name in @('Zeye.Sorting.Hub.Host.exe', 'Zeye.Sorting.Hub.Host', 'appsettings.json', 'wwwroot/index.html')) {
        [IO.File]::WriteAllText((Join-Path $testDirectory $name), '{}')
    }
    $tokens = $null; $errors = $null
    [Management.Automation.Language.Parser]::ParseFile((Join-Path $testDirectory 'service.ps1'), [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count) { throw "PowerShell 语法失败：$errors" }
    $checks++
    foreach ($name in @('install.sh', 'uninstall.sh', 'service.sh')) {
        Assert-ScriptExit $BashExecutable @('--noprofile', '--norc', '-n', (Join-Path $testDirectory $name).Replace('\', '/'))
    }
    foreach ($name in @('install.sh', 'uninstall.sh')) {
        Assert-ScriptExit $BashExecutable @('--noprofile', '--norc', (Join-Path $testDirectory $name).Replace('\', '/'), '--dry-run')
        Assert-ScriptExit $BashExecutable @('--noprofile', '--norc', (Join-Path $testDirectory $name).Replace('\', '/'), '--invalid') 1
    }
    if ($env:OS -eq 'Windows_NT') {
        foreach ($name in @('install.bat', 'uninstall.bat')) {
            Assert-ScriptExit (Join-Path $testDirectory $name) @('--dry-run')
            Assert-ScriptExit (Join-Path $testDirectory $name) @('--invalid') 2
        }
    }
    $env:ZEYE_SERVICE_NAME = '../ForeignService'
    Assert-ScriptExit $BashExecutable @('--noprofile', '--norc', (Join-Path $testDirectory 'install.sh').Replace('\', '/'), '--dry-run') 1
    if ($env:OS -eq 'Windows_NT') { Assert-ScriptExit (Join-Path $testDirectory 'install.bat') @('--dry-run') 1 }
    $env:ZEYE_SERVICE_NAME = 'Zeye.Hub.ScriptTest'
    $env:ZEYE_SERVICE_TIMEOUT_SECONDS = 'invalid'
    Assert-ScriptExit $BashExecutable @('--noprofile', '--norc', (Join-Path $testDirectory 'uninstall.sh').Replace('\', '/'), '--dry-run') 1
    if ($env:OS -eq 'Windows_NT') { Assert-ScriptExit (Join-Path $testDirectory 'uninstall.bat') @('--dry-run') 1 }
    $env:ZEYE_SERVICE_TIMEOUT_SECONDS = '30'
    Remove-Item -LiteralPath (Join-Path $testDirectory 'wwwroot/index.html')
    Assert-ScriptExit $BashExecutable @('--noprofile', '--norc', (Join-Path $testDirectory 'install.sh').Replace('\', '/'), '--dry-run') 1
    if ($env:OS -eq 'Windows_NT') { Assert-ScriptExit (Join-Path $testDirectory 'install.bat') @('--dry-run') 1 }
    Write-Host "$checks 项服务脚本检查通过；未操作系统服务。"
}
finally {
    $env:ZEYE_SERVICE_NAME = $savedName
    $env:ZEYE_SERVICE_TIMEOUT_SECONDS = $savedTimeout
    $env:ZEYE_SERVICE_DRY_RUN = $savedDryRun
    # 递归清理只作用于本次创建且位于系统临时目录内的精确路径。
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not [IO.Path]::GetFullPath($testDirectory).StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) { throw '测试清理路径超出临时目录。' }
    if (Test-Path -LiteralPath $testDirectory) { Remove-Item -LiteralPath $testDirectory -Recurse -Force }
}
