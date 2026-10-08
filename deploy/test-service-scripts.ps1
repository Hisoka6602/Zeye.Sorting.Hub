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
$savedBashEnvironment = $env:BASH_ENV
$checks = 0
if (-not $BashExecutable) {
    $gitBash = if ($env:OS -eq 'Windows_NT') { Join-Path ${env:ProgramFiles} 'Git/bin/bash.exe' } else { '' }
    $BashExecutable = if ($gitBash -and (Test-Path -LiteralPath $gitBash)) { $gitBash } else { (Get-Command bash -ErrorAction Stop).Source }
}

# 中文说明：运行子进程，验证预期退出码；不把预期错误转换成主脚本中断。
function Assert-ScriptExit {
    param([string]$Command, [string[]]$Arguments, [int]$ExpectedExitCode = 0, [string]$ExpectedOutput, [switch]$FeedPauseKey)
    Write-Verbose "检查 $Command（预期退出码 $ExpectedExitCode）"
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = if ($FeedPauseKey) { ' ' | & $Command @Arguments 2>&1 } else { & $Command @Arguments 2>&1 }
        $actualExitCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previousPreference }
    if ($actualExitCode -ne $ExpectedExitCode) { throw "预期退出码 $ExpectedExitCode，实际 $actualExitCode；输出：$output" }
    if ($ExpectedOutput -and ($output -join "`n") -notmatch $ExpectedOutput) { throw "输出缺少预期内容 $ExpectedOutput；实际输出：$output" }
    $script:checks++
}

try {
    $env:ZEYE_SERVICE_NAME = 'Zeye.Hub.ScriptTest'
    $env:ZEYE_SERVICE_TIMEOUT_SECONDS = '30'
    $env:ZEYE_SERVICE_DRY_RUN = 'false'
    New-Item -ItemType Directory -Path (Join-Path $testDirectory 'wwwroot') -Force | Out-Null
    # 中文说明：Git Bash 的精简环境也需要标准工具路径；仅影响测试子进程，结束后恢复原值。
    $bashEnvironmentPath = Join-Path $testDirectory 'bash-test-environment.sh'
    [IO.File]::WriteAllText($bashEnvironmentPath, "export PATH=/usr/bin:/bin:`$PATH`n", [Text.UTF8Encoding]::new($false))
    $env:BASH_ENV = $bashEnvironmentPath.Replace('\', '/')
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
        # 中文说明：隔离执行真实提权函数，仅模拟进程启动，不触发 UAC 或操作服务。
        $elevationTestPath = Join-Path $testDirectory 'test-service-elevation.ps1'
        $elevationTestSource = @'
param([string]$ServiceScriptPath, [ValidateSet('success', 'failure', 'cancelled')][string]$Scenario)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Action = 'Install'
$OpenBrowser = $false
$installerLogPath = Join-Path $PSScriptRoot 'logs/service-install.log'
$processDisposed = $false
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($ServiceScriptPath, [ref]$tokens, [ref]$errors)
$definition = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-ElevatedServiceAction' }, $true)
if (-not $definition) { throw '缺少管理员提权函数。' }
Invoke-Expression $definition.Extent.Text
# 中文说明：验证提权命令及带中文和空格的路径，返回模拟退出码和安装日志。
function Start-Process {
    param([string]$FilePath, [string]$ArgumentList, [string]$Verb, [string]$WindowStyle, [switch]$Wait, [switch]$PassThru)
    $expectedArguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "{0}" -Action Install -Elevated' -f $ServiceScriptPath
    if (-not $FilePath.EndsWith('powershell.exe') -or $ArgumentList -ne $expectedArguments -or $Verb -ne 'RunAs' -or $WindowStyle -ne 'Hidden' -or -not $Wait -or -not $PassThru) {
        throw '管理员提权命令或等待设置不正确。'
    }
    if ($Scenario -eq 'cancelled') { throw [ComponentModel.Win32Exception]::new(1223) }
    New-Item -ItemType Directory -Path (Split-Path -Parent $installerLogPath) -Force | Out-Null
    [IO.File]::WriteAllText($installerLogPath, '模拟安装进程执行记录', [Text.UTF8Encoding]::new($true))
    $process = [PSCustomObject]@{ ExitCode = $(if ($Scenario -eq 'success') { 0 } else { 37 }) }
    $process | Add-Member -MemberType ScriptMethod -Name Dispose -Value { $script:processDisposed = $true }
    return $process
}
try {
    $exitCode = Invoke-ElevatedServiceAction
    if (-not $processDisposed) { throw '提权进程未释放。' }
    exit $exitCode
}
catch { Write-Error $_ -ErrorAction Continue; exit 1 }
'@
        [IO.File]::WriteAllText($elevationTestPath, $elevationTestSource, [Text.UTF8Encoding]::new($true))
        foreach ($scenario in @('success', 'failure', 'cancelled')) {
            $expectedExitCode = if ($scenario -eq 'success') { 0 } elseif ($scenario -eq 'failure') { 37 } else { 1 }
            $expectedOutput = if ($scenario -eq 'cancelled') { '已取消管理员授\s*权' } else { '模拟安装进程执行记录' }
            Assert-ScriptExit 'powershell.exe' @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $elevationTestPath, (Join-Path $testDirectory 'service.ps1'), $scenario) $expectedExitCode $expectedOutput
        }
        # 中文说明：在独立的当前用户注册表键中运行真实配置函数，覆盖首次安装及重复安装。
        $registryTestPath = Join-Path $testDirectory 'test-service-registry.ps1'
        $registryTestSource = @'
param([string]$ServiceScriptPath, [ValidateSet('fresh', 'existing', 'override', 'fallback', 'missing-marker', 'missing-key')][string]$Scenario)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$serviceRegistryPath = 'HKCU:\Software\Zeye.Hub.InstallerTest.' + [Guid]::NewGuid().ToString('N')
$serviceName = 'Zeye.Hub.ScriptTest'
$installDirectory = $PSScriptRoot
$binaryCommand = 'test-service.exe'
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($ServiceScriptPath, [ref]$tokens, [ref]$errors)
foreach ($name in @('Get-ServiceRegistryValue', 'Save-ServiceEnvironment', 'Assert-ServiceOwnership')) {
    $definition = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $true)
    if (-not $definition) { throw "缺少注册表配置函数：$name。" }
    Invoke-Expression $definition.Extent.Text
}
Remove-Item Env:DOTNET_ENVIRONMENT, Env:ASPNETCORE_ENVIRONMENT, Env:ZEYE__InstallerProbe -ErrorAction SilentlyContinue
try {
    if ($Scenario -eq 'missing-key') {
        try { $null = Get-ServiceRegistryValue -Name 'Environment' }
        catch {
            if ($_.CategoryInfo.Category -ne [Management.Automation.ErrorCategory]::ObjectNotFound) { throw }
            Write-Host 'PASS: missing-key'
            exit 0
        }
        throw '不存在的注册表键未产生真实错误。'
    }
    New-Item -Path $serviceRegistryPath -Force | Out-Null
    if ($Scenario -eq 'missing-marker') {
        try { Assert-ServiceOwnership ([PSCustomObject]@{ PathName = $binaryCommand }) }
        catch {
            if ($_.Exception.Message -notmatch '不属于当前发布目录') { throw }
            Write-Host 'PASS: missing-marker'
            exit 0
        }
        throw '缺少归属标记的服务未被拒绝。'
    }
    if ($Scenario -in @('existing', 'override')) {
        New-ItemProperty -LiteralPath $serviceRegistryPath -Name 'Environment' -PropertyType MultiString -Value @('SavedOnly=one=two', 'DOTNET_ENVIRONMENT=Staging', 'ZEYE__InstallerProbe=old') | Out-Null
    }
    if ($Scenario -eq 'override') { $env:ZEYE__InstallerProbe = 'new=value' }
    if ($Scenario -eq 'fallback') { $env:ASPNETCORE_ENVIRONMENT = 'Development' }
    Save-ServiceEnvironment
    $entries = @(Get-ServiceRegistryValue -Name 'Environment')
    $expectedEnvironment = if ($Scenario -in @('existing', 'override')) { 'Staging' } elseif ($Scenario -eq 'fallback') { 'Development' } else { 'Production' }
    if ($entries -notcontains "DOTNET_ENVIRONMENT=$expectedEnvironment") { throw '运行环境默认值或已有值未正确保留。' }
    if ($Scenario -in @('existing', 'override') -and $entries -notcontains 'SavedOnly=one=two') { throw '已有配置或包含等号的值丢失。' }
    if ($Scenario -eq 'override' -and $entries -notcontains 'ZEYE__InstallerProbe=new=value') { throw '本次显式配置未覆盖已有值。' }
    $key = Get-Item -LiteralPath $serviceRegistryPath
    try { if ($key.GetValueKind('Environment') -ne [Microsoft.Win32.RegistryValueKind]::MultiString) { throw '服务环境变量未保存为 REG_MULTI_SZ。' } }
    finally { $key.Dispose() }
    Save-ServiceEnvironment
    if ($entries.Count -ne @(Get-ServiceRegistryValue -Name 'Environment').Count) { throw '重复安装改变了环境变量数量。' }
    Write-Host "PASS: $Scenario"
}
catch { Write-Error $_ -ErrorAction Continue; exit 1 }
finally {
    if (Test-Path -LiteralPath $serviceRegistryPath) { Remove-Item -LiteralPath $serviceRegistryPath -Force }
}
'@
        [IO.File]::WriteAllText($registryTestPath, $registryTestSource, [Text.UTF8Encoding]::new($true))
        foreach ($scenario in @('fresh', 'existing', 'override', 'fallback', 'missing-marker', 'missing-key')) {
            Assert-ScriptExit 'powershell.exe' @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $registryTestPath, (Join-Path $testDirectory 'service.ps1'), $scenario) 0 "PASS: $scenario"
        }
        # 中文说明：模拟服务与监听端口，验证未完成启动或中途退出时不能显示安装成功。
        $readinessTestPath = Join-Path $testDirectory 'test-service-readiness.ps1'
        $readinessTestSource = @'
param([string]$ServiceScriptPath, [ValidateSet('listener', 'no-listener', 'stopped', 'custom-health', 'setup')][string]$Scenario)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.ServiceProcess
$serviceName = 'Zeye.Hub.ScriptTest'
$timeoutSeconds = 4
$listenerChecks = 0
Remove-Item Env:ZEYE_SERVICE_HEALTH_URL -ErrorAction SilentlyContinue
if ($Scenario -in @('custom-health', 'setup')) { $env:ZEYE_SERVICE_HEALTH_URL = 'http://127.0.0.1:5078/health/ready' }
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($ServiceScriptPath, [ref]$tokens, [ref]$errors)
$definition = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Wait-ServiceReady' }, $true)
if (-not $definition) { throw '缺少服务就绪检查。' }
Invoke-Expression $definition.Extent.Text
# 中文说明：以模拟的服务控制器覆盖运行和停止状态，不读取真实服务。
function Get-Service {
    param([string]$Name)
    $status = if ($Scenario -eq 'stopped') { [ServiceProcess.ServiceControllerStatus]::Stopped } else { [ServiceProcess.ServiceControllerStatus]::Running }
    $controller = [PSCustomObject]@{ Status = $status }
    $controller | Add-Member ScriptMethod Refresh {}
    $controller | Add-Member ScriptMethod Dispose {}
    return $controller
}
# 中文说明：服务进程编号仅用于匹配模拟监听端口。
function Get-CimInstance {
    param([string]$ClassName, [string]$Filter)
    return [PSCustomObject]@{ ProcessId = 424242 }
}
# 中文说明：验证端口必须由当前服务进程拥有，其他进程的端口不能表示启动完成。
function Get-NetTCPConnection {
    param([string]$State, [string]$ErrorAction)
    $script:listenerChecks++
    return [PSCustomObject]@{ OwningProcess = $(if ($Scenario -eq 'listener') { 424242 } else { 1 }) }
}
# 中文说明：显式就绪探针返回成功，不发送真实 HTTP 请求。
function Invoke-WebRequest {
    param([switch]$UseBasicParsing, [string]$Uri, [int]$TimeoutSec)
    if ($Scenario -eq 'setup') { throw '业务数据库尚未就绪。' }
    return [PSCustomObject]@{ StatusCode = 200 }
}
# 中文说明：配置模式提供网页而不把业务健康检查伪造为成功。
function Get-DatabaseSetupStatus { return [PSCustomObject]@{ requiresConfiguration = ($Scenario -eq 'setup') } }
try {
    Wait-ServiceReady
    if ($Scenario -eq 'listener' -and $listenerChecks -eq 0) { throw '未查询本服务的监听端口。' }
    if ($Scenario -eq 'custom-health' -and $listenerChecks -ne 0) { throw '显式就绪探针不应依赖默认端口检查。' }
    Write-Host "PASS: $Scenario"
}
catch { Write-Error $_ -ErrorAction Continue; Write-Host 'READINESS_FAILED'; exit 1 }
'@
        [IO.File]::WriteAllText($readinessTestPath, $readinessTestSource, [Text.UTF8Encoding]::new($true))
        foreach ($scenario in @('listener', 'no-listener', 'stopped', 'custom-health', 'setup')) {
            $expectedExitCode = if ($scenario -in @('no-listener', 'stopped')) { 1 } else { 0 }
            $expectedOutput = if ($expectedExitCode -eq 1) { 'READINESS_FAILED' } else { "PASS: $scenario" }
            Assert-ScriptExit 'powershell.exe' @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $readinessTestPath, (Join-Path $testDirectory 'service.ps1'), $scenario) $expectedExitCode $expectedOutput
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
    if ($env:OS -eq 'Windows_NT') {
        # 中文说明：用临时替身验证交互窗口等待按键后仍返回原始失败码，禁止调用真实服务实现。
        [IO.File]::WriteAllText((Join-Path $testDirectory 'service.ps1'), "Write-Host '模拟服务操作失败'; exit 37", [Text.UTF8Encoding]::new($true))
        foreach ($name in @('install.bat', 'uninstall.bat')) {
            Assert-ScriptExit (Join-Path $testDirectory $name) @() 37 '模拟服务操作失败' -FeedPauseKey
        }
    }
    Write-Host "$checks 项服务脚本检查通过；未操作系统服务。"
}
finally {
    $env:ZEYE_SERVICE_NAME = $savedName
    $env:ZEYE_SERVICE_TIMEOUT_SECONDS = $savedTimeout
    $env:ZEYE_SERVICE_DRY_RUN = $savedDryRun
    $env:BASH_ENV = $savedBashEnvironment
    # 递归清理只作用于本次创建且位于系统临时目录内的精确路径。
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not [IO.Path]::GetFullPath($testDirectory).StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) { throw '测试清理路径超出临时目录。' }
    if (Test-Path -LiteralPath $testDirectory) { Remove-Item -LiteralPath $testDirectory -Recurse -Force }
}
