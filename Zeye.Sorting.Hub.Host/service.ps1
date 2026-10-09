<# 中文说明：Windows 服务安装与停止卸载的共用实现，兼容 Windows PowerShell 5.1。 #>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Install', 'Uninstall')][string]$Action,
    [switch]$DryRun,
    [switch]$Elevated,
    [switch]$OpenBrowser
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$serviceName = if ($env:ZEYE_SERVICE_NAME) { $env:ZEYE_SERVICE_NAME } else { 'Zeye.Sorting.Hub.Host' }
$displayName = if ($env:ZEYE_SERVICE_DISPLAY_NAME) { $env:ZEYE_SERVICE_DISPLAY_NAME } else { 'Zeye Sorting Hub' }
$installDirectory = [IO.Path]::GetFullPath($PSScriptRoot)
$serviceScriptPath = $PSCommandPath
$installerLogPath = Join-Path $installDirectory ('logs/service-{0}.log' -f $Action.ToLowerInvariant())
$transcriptStarted = $false
$executable = Join-Path $installDirectory 'Zeye.Sorting.Hub.Host.exe'
$binaryCommand = '"{0}" --ServiceName "{1}"' -f $executable, $serviceName
$serviceRegistryPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\' + $serviceName
$timeoutSeconds = 180
if ($env:ZEYE_SERVICE_TIMEOUT_SECONDS) {
    if (-not [int]::TryParse($env:ZEYE_SERVICE_TIMEOUT_SECONDS, [ref]$timeoutSeconds) -or $timeoutSeconds -lt 30 -or $timeoutSeconds -gt 900) {
        throw 'ZEYE_SERVICE_TIMEOUT_SECONDS 必须为 30～900 的整数秒数。'
    }
}
$DryRun = $DryRun -or ($env:ZEYE_SERVICE_DRY_RUN -match '^(1|true|yes|on)$')

# 中文说明：经 UAC 授权重启同一脚本，回显提权进程的日志并保留真实退出码。
function Invoke-ElevatedServiceAction {
    Write-Host '安装或卸载 Windows 服务需要管理员权限，请在 Windows 授权提示中选择“是”。'
    $powershellPath = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
    $elevationArguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "{0}" -Action {1} -Elevated' -f $serviceScriptPath, $Action
    if ($OpenBrowser) { $elevationArguments += ' -OpenBrowser' }
    $startedAt = Get-Date
    try {
        $process = Start-Process -FilePath $powershellPath -ArgumentList $elevationArguments -Verb RunAs -WindowStyle Hidden -Wait -PassThru
    }
    catch [ComponentModel.Win32Exception] {
        if ($_.Exception.NativeErrorCode -eq 1223) { throw '已取消管理员授权，服务操作未执行。' }
        throw
    }
    try { $exitCode = $process.ExitCode }
    finally { $process.Dispose() }
    if ((Test-Path -LiteralPath $installerLogPath -PathType Leaf) -and (Get-Item -LiteralPath $installerLogPath).LastWriteTime -ge $startedAt) {
        Get-Content -LiteralPath $installerLogPath -Encoding UTF8 | ForEach-Object { Write-Host $_ }
    }
    if ($exitCode -ne 0) { Write-Host "服务操作未完成，退出码：$exitCode；详细记录：$installerLogPath" }
    return $exitCode
}

# 中文说明：执行系统服务控制命令，失败时不能显示成功或继续卸载。
function Invoke-ServiceControl {
    param([string[]]$Arguments)
    & "$env:SystemRoot\System32\sc.exe" @Arguments
    if ($LASTEXITCODE -ne 0) { throw "服务控制失败，退出码：$LASTEXITCODE。" }
}

# 中文说明：停止服务并限时等待，确保释放文件、数据库和上传队列后再继续。
function Stop-HubService {
    $controller = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if (-not $controller) { return }
    try {
        if ($controller.Status -ne [ServiceProcess.ServiceControllerStatus]::Stopped) {
            if ($controller.Status -ne [ServiceProcess.ServiceControllerStatus]::StopPending) { $controller.Stop() }
            $controller.WaitForStatus([ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds($timeoutSeconds))
        }
    }
    finally { $controller.Dispose() }
}

# 中文说明：缺失的可选注册表项返回空值，注册表键不存在或读取权限不足时保留真实错误。
function Get-ServiceRegistryValue {
    param([string]$Name)
    $key = Get-Item -LiteralPath $serviceRegistryPath
    try { return $key.GetValue($Name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) }
    finally { $key.Dispose() }
}

# 中文说明：验证服务归属于当前发布目录，防止名称冲突时修改或删除其他项目。
function Assert-ServiceOwnership {
    param($ExistingService)
    if (-not $ExistingService) { return }
    $marker = Get-ServiceRegistryValue -Name 'ZeyeHubInstallDirectory'
    if (-not [string]::Equals([string]$marker, $installDirectory, [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals([string]$ExistingService.PathName, $binaryCommand, [StringComparison]::OrdinalIgnoreCase)) {
        throw "服务 $serviceName 已存在，但不属于当前发布目录；请使用原目录卸载，或配置另一个 ZEYE_SERVICE_NAME。"
    }
}

# 中文说明：保留服务已有环境配置，并持久化本次显式传入的应用环境变量；不输出变量值。
function Save-ServiceEnvironment {
    $variables = @{}
    $saved = Get-ServiceRegistryValue -Name 'Environment'
    foreach ($entry in @($saved)) {
        if ($entry -and $entry.Contains('=')) {
            $parts = $entry.Split([char[]]'=', 2, [StringSplitOptions]::None)
            $variables[$parts[0]] = $parts[1]
        }
    }
    foreach ($entry in Get-ChildItem Env:) {
        if ($entry.Name.Contains('__') -or $entry.Name -match '^(ASPNETCORE_|DOTNET_)') {
            $variables[$entry.Name] = $entry.Value
        }
    }
    if (-not $variables.ContainsKey('DOTNET_ENVIRONMENT')) {
        $variables['DOTNET_ENVIRONMENT'] = if ($variables.ContainsKey('ASPNETCORE_ENVIRONMENT')) { $variables['ASPNETCORE_ENVIRONMENT'] } else { 'Production' }
    }
    $entries = [string[]]@($variables.Keys | Sort-Object | ForEach-Object { "$_=$($variables[$_])" })
    New-ItemProperty -LiteralPath $serviceRegistryPath -Name 'Environment' -PropertyType MultiString -Value $entries -Force | Out-Null
}

# 中文说明：只查询当前服务进程的本机端口，直接读取启动状态，避免系统代理影响回环请求。
function Get-DatabaseSetupStatus {
    $service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
    if (-not $service -or $service.ProcessId -le 0) { return $null }
    $ports = @(Get-NetTCPConnection -State Listen -ErrorAction Stop | Where-Object { $_.OwningProcess -eq $service.ProcessId } | Select-Object -ExpandProperty LocalPort -Unique)
    Add-Type -AssemblyName System.Net.Http
    foreach ($port in $ports) {
        $handler = [Net.Http.HttpClientHandler]::new()
        $handler.UseProxy = $false
        $client = [Net.Http.HttpClient]::new($handler)
        $client.Timeout = [TimeSpan]::FromSeconds(2)
        $response = $null
        try {
            $url = "http://127.0.0.1:$port"
            $response = $client.GetAsync("$url/api/setup/status").GetAwaiter().GetResult()
            if (-not $response.IsSuccessStatusCode) { continue }
            $status = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
            if ($null -ne $status.requiresConfiguration) {
                $status | Add-Member -NotePropertyName WebUrl -NotePropertyValue $url
                return $status
            }
        }
        catch { Write-Verbose '本机端口未提供启动状态，继续核对其他监听端口。' }
        finally {
            if ($response) { $response.Dispose() }
            $client.Dispose()
        }
    }
    return $null
}

# 中文说明：等待网页可用或业务就绪，数据库配置模式也允许先完成服务安装。
function Wait-ServiceReady {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    do {
        $controller = Get-Service -Name $serviceName
        try {
            $controller.Refresh()
            if ($controller.Status -ne [ServiceProcess.ServiceControllerStatus]::Running) {
                throw '服务启动后已停止；请检查发布目录 logs 中的异常和数据库配置。'
            }
        }
        finally { $controller.Dispose() }
        if (-not $env:ZEYE_SERVICE_HEALTH_URL) {
            $service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
            if ($service.ProcessId -gt 0) {
                $listeners = @(Get-NetTCPConnection -State Listen -ErrorAction Stop | Where-Object { $_.OwningProcess -eq $service.ProcessId })
                if ($listeners.Count -gt 0 -and $timer.Elapsed.TotalSeconds -ge 3) { return }
            }
        }
        else {
            try {
                $response = Invoke-WebRequest -UseBasicParsing -Uri $env:ZEYE_SERVICE_HEALTH_URL -TimeoutSec 5
                if ($response.StatusCode -eq 200) { return }
            }
            catch { Write-Verbose '就绪探针尚未通过，继续等待；不输出请求凭据。' }
            $setup = Get-DatabaseSetupStatus
            if ($setup -and $setup.requiresConfiguration) { return }
        }
        Start-Sleep -Milliseconds 500
    } while ($timer.Elapsed.TotalSeconds -lt $timeoutSeconds)
    throw '服务就绪检查超时；请核对 ZEYE_SERVICE_HEALTH_URL、监听端口和 logs 中的启动日志。'
}

try {
    if ($serviceName -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$') { throw '服务名只允许字母、数字、点、下划线和连字符，长度为 1～80。' }
    if ($installDirectory.TrimEnd('\') -eq [IO.Path]::GetPathRoot($installDirectory).TrimEnd('\')) { throw '发布包不能位于磁盘根目录。' }
    if ($env:ZEYE_SERVICE_HEALTH_URL) {
        $healthUri = $null
        if (-not [Uri]::TryCreate($env:ZEYE_SERVICE_HEALTH_URL, [UriKind]::Absolute, [ref]$healthUri) -or $healthUri.Scheme -notin @('http', 'https')) {
            throw 'ZEYE_SERVICE_HEALTH_URL 必须是 HTTP 或 HTTPS 的完整就绪地址。'
        }
    }
    if ($Action -eq 'Install') {
        foreach ($relativePath in @('Zeye.Sorting.Hub.Host.exe', 'wwwroot/index.html', 'appsettings.json')) {
            if (-not (Test-Path -LiteralPath (Join-Path $installDirectory $relativePath) -PathType Leaf)) {
                throw "缺少 $relativePath；请在完整的 Windows 发布包中运行安装脚本。"
            }
        }
    }
    if ($DryRun) {
        Write-Host "[预演] $Action 服务：$serviceName"
        Write-Host "[预演] 发布目录：$installDirectory"
        Write-Host '[预演] 安装将注册自动启动的独立服务账号并启动；卸载将等待停止后移除服务，保留全部文件与数据。'
        exit 0
    }
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try { $administrator = ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
    finally { $identity.Dispose() }
    if (-not $administrator) {
        if ($Elevated) { throw '管理员授权后仍未获得所需权限；请使用具有本机管理员权限的账号安装。' }
        exit (Invoke-ElevatedServiceAction)
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $installerLogPath) -Force | Out-Null
    Start-Transcript -LiteralPath $installerLogPath -Force | Out-Null
    $transcriptStarted = $true
    $existingService = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
    Assert-ServiceOwnership $existingService

    if ($Action -eq 'Uninstall') {
        if (-not $existingService) {
            Write-Host "服务 $serviceName 未安装；配置、日志和数据保持保留。"
            exit 0
        }
        Stop-HubService
        Invoke-ServiceControl @('delete', $serviceName)
        $timer = [Diagnostics.Stopwatch]::StartNew()
        do {
            $remaining = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
            if (-not $remaining) { break }
            $remaining.Dispose()
            if ($timer.Elapsed.TotalSeconds -ge $timeoutSeconds) { throw '服务仍被其他程序占用；请关闭服务管理窗口后重试卸载。' }
            Start-Sleep -Milliseconds 500
        } while ($true)
        Write-Host "服务 $serviceName 已停止并卸载；发布文件、配置、日志、图片和数据库均保留。"
        exit 0
    }

    if ($existingService) { Stop-HubService }
    else {
        # 账号和权限配置完成前保持手动启动，避免未完成安装的服务在下次开机以默认账号运行。
        New-Service -Name $serviceName -DisplayName $displayName -BinaryPathName $binaryCommand -StartupType Manual -Description 'Zeye Sorting Hub 前后端一体化管理服务' | Out-Null
        New-ItemProperty -LiteralPath $serviceRegistryPath -Name 'ZeyeHubInstallDirectory' -PropertyType String -Value $installDirectory -Force | Out-Null
    }
    Set-Service -Name $serviceName -DisplayName $displayName -Description 'Zeye Sorting Hub 前后端一体化管理服务'
    # 使用本服务的虚拟账号，不能默认授予 LocalSystem 权限。
    Invoke-ServiceControl @('config', $serviceName, 'start=', 'delayed-auto', 'obj=', "NT SERVICE\$serviceName")
    Invoke-ServiceControl @('sidtype', $serviceName, 'unrestricted')
    Invoke-ServiceControl @('failure', $serviceName, 'reset=', '86400', 'actions=', 'restart/10000/restart/30000/restart/60000')
    # 前端主动重启使用非零服务状态正常关闭，允许 SCM 在关闭完成后重新启动本服务。
    Invoke-ServiceControl @('failureflag', $serviceName, '1')
    & "$env:SystemRoot\System32\icacls.exe" $installDirectory '/grant' "NT SERVICE\${serviceName}:(OI)(CI)M" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw '无法授权服务读取发布文件并写入运行数据；安装未完成。' }
    Save-ServiceEnvironment
    Start-Service -Name $serviceName
    Wait-ServiceReady
    $setup = Get-DatabaseSetupStatus
    if ($setup -and $setup.requiresConfiguration) {
        Write-Host "服务 $serviceName 已安装，网页已启动；当前等待数据库配置，已启用开机自动启动。"
        Write-Host ('本机配置入口：{0}/；保存数据库配置后点击网页中的“重启 Host”。' -f $setup.WebUrl)
        Write-Host "本机配置访问码文件：$($setup.setupKeyPath)；访问码不记录到安装日志。"
        if ($OpenBrowser) {
            try {
                $setupKey = (Get-Content -LiteralPath $setup.setupKeyPath -Raw -Encoding UTF8).Trim()
                if ($setupKey -notmatch '^[A-F0-9]{64}$') { throw '配置访问码格式无效。' }
                # 浏览器用于本机配置交互；访问码置于片段，不作为请求地址或日志内容发送。
                Start-Process -FilePath ("{0}/#setup={1}" -f $setup.WebUrl, $setupKey)
            }
            catch { Write-Warning '配置页面未能自动打开；请从本机入口进入，并以管理员权限读取访问码文件。' }
        }
    }
    else {
        Write-Host "服务 $serviceName 已安装并运行，已启用开机自动启动。"
        Write-Host '前端与 API 使用同一地址，默认 http://127.0.0.1:5078/；运行日志位于发布目录 logs。'
    }
}
catch {
    Write-Error -Message ("服务{0}失败：{1}" -f $Action, $_.Exception.Message) -ErrorAction Continue
    exit 1
}
finally {
    if ($transcriptStarted) { Stop-Transcript | Out-Null }
}
