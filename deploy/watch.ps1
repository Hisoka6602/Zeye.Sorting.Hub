<# 本机源码监听：成功构建后替换 Host/Web，保留原有数据库和持久化卷。 #>
[CmdletBinding()]
param(
    [ValidateSet('Start', 'Stop', 'Status', 'Run')]
    [string]$Action = 'Start',
    [switch]$SkipInitialBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$repositoryDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stateDirectory = Join-Path $repositoryDirectory 'artifacts/local-watch'
$statePath = Join-Path $stateDirectory 'state.json'
$stopPath = Join-Path $stateDirectory 'stop.request'
$composeArguments = @('compose', '--env-file', (Join-Path $PSScriptRoot '.env'), '--file', (Join-Path $PSScriptRoot 'compose.yaml'), '--file', (Join-Path $PSScriptRoot 'compose.watch.yaml'))

function Get-WatcherState {
    if (-not (Test-Path -LiteralPath $statePath)) { return $null }
    try {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        $process = Get-Process -Id $state.ProcessId -ErrorAction Stop
        if ($process.StartTime.ToUniversalTime().Ticks.ToString() -ne $state.StartTicks) { return $null }
        return $state
    } catch { return $null }
}

function Write-WatcherState {
    param([string]$Phase)
    [pscustomobject]@{
        ProcessId = $PID
        StartTicks = (Get-Process -Id $PID).StartTime.ToUniversalTime().Ticks.ToString()
        Phase = $Phase
        UpdatedAt = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8
}

function Stop-OwnedChild {
    param([Diagnostics.Process]$Child)
    # 仅终止本次启动、仍存活的 Docker CLI；不停止容器或操作数据卷。
    if ($Child -and -not $Child.HasExited) {
        # Compose 是 Docker CLI 的子进程，必须一并退出，避免留下重复监听。
        if ($Child.GetType().GetMethod('Kill', [type[]]@([bool]))) { $Child.Kill($true) }
        else { & taskkill.exe /PID $Child.Id /T /F | Out-Null }
        $Child.WaitForExit()
    }
}

function Wait-OwnedChild {
    param([Diagnostics.Process]$Child)
    while (-not $Child.WaitForExit(1000)) {
        if (Test-Path -LiteralPath $stopPath) {
            Stop-OwnedChild -Child $Child
            return $false
        }
    }
    return $true
}

function Start-ComposeChild {
    param([string[]]$Arguments, [string]$LogName)
    # Start-Process 在 Windows 按字符串传递参数；文件路径必须保留引号。
    $quotedArguments = @($Arguments | ForEach-Object { '"' + $_ + '"' })
    return Start-Process -FilePath (Get-Command docker -ErrorAction Stop).Source -ArgumentList $quotedArguments `
        -WorkingDirectory $repositoryDirectory -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $stateDirectory "$LogName.log") `
        -RedirectStandardError (Join-Path $stateDirectory "$LogName.error.log")
}

function Test-WatchDisabled {
    $errorLog = Join-Path $stateDirectory 'watch.error.log'
    if (Test-Path -LiteralPath $errorLog) {
        if (Get-Content -LiteralPath $errorLog -Tail 64 | Select-String -Pattern 'Watch disabled with errors' -Quiet) { return $true }
    }
    return $false
}

function Invoke-Watcher {
    $hashAlgorithm = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($hashAlgorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($repositoryDirectory.ToLowerInvariant()))).Replace('-', '') }
    finally { $hashAlgorithm.Dispose() }
    $mutex = [Threading.Mutex]::new($false, "Local\Zeye.Sorting.Hub.Watch.$hash")
    $ownsMutex = $false
    $child = $null
    $watchChildren = @()
    try {
        try { $ownsMutex = $mutex.WaitOne(0) }
        catch [Threading.AbandonedMutexException] { $ownsMutex = $true }
        if (-not $ownsMutex) { throw '此工作目录已有源码监听进程。' }
        $needsBuild = -not $SkipInitialBuild
        while (-not (Test-Path -LiteralPath $stopPath)) {
            if ($needsBuild) {
                foreach ($service in @('host', 'web')) {
                    Write-WatcherState -Phase "Building-$service"
                    $child = Start-ComposeChild -Arguments ($composeArguments + @('up', '--detach', '--build', '--no-deps', '--wait', '--wait-timeout', '180', $service)) -LogName "build-$service"
                    if (-not (Wait-OwnedChild -Child $child)) { break }
                    if ($child.ExitCode -ne 0) {
                        Write-Warning "$service 构建或就绪检查失败，保留可用版本并继续监听；详情见 build-$service.error.log。"
                    }
                }
            }
            if (Test-Path -LiteralPath $stopPath) { break }
            # Compose 每个项目只允许一个监听进程，由规则选择需更新的服务。
            # 重建后回收无引用的旧构建镜像，避免持续监听造成磁盘累积。
            $watchChildren = @(Start-ComposeChild -Arguments ($composeArguments + @('watch', '--no-up', '--prune=true', 'host', 'web')) -LogName 'watch')
            Write-WatcherState -Phase 'Watching'
            while (-not (Test-Path -LiteralPath $stopPath) -and -not @($watchChildren | Where-Object { $_.HasExited }).Count) {
                # Windows 的文件事件缓冲出错可能只停用监听而不退出 CLI，需主动补构建。
                if (Test-WatchDisabled) { break }
                Start-Sleep -Seconds 1
            }
            foreach ($watchChild in $watchChildren) { Stop-OwnedChild -Child $watchChild }
            $watchChildren = @()
            if (Test-Path -LiteralPath $stopPath) { break }
            # Docker Desktop 重启或监听意外退出后，先补构建期间遗漏的修改再恢复监听。
            Write-WatcherState -Phase 'Retrying'
            Write-Warning 'Docker 源码监听已退出，10 秒后重新检查并启动。'
            for ($attempt = 0; $attempt -lt 10 -and -not (Test-Path -LiteralPath $stopPath); $attempt++) { Start-Sleep -Seconds 1 }
            $needsBuild = $true
        }
    } finally {
        Stop-OwnedChild -Child $child
        foreach ($watchChild in $watchChildren) { Stop-OwnedChild -Child $watchChild }
        if ($ownsMutex) {
            Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $stopPath -Force -ErrorAction SilentlyContinue
            $mutex.ReleaseMutex()
        }
        $mutex.Dispose()
    }
}

switch ($Action) {
    'Status' {
        $state = Get-WatcherState
        if ($state) { Write-Host "源码监听运行中：$($state.Phase)，PID $($state.ProcessId)。日志：$stateDirectory" }
        else { Write-Host '源码监听未运行。' }
    }
    'Stop' {
        $state = Get-WatcherState
        if (-not $state) { Write-Host '源码监听未运行。'; return }
        New-Item -ItemType File -Path $stopPath -Force | Out-Null
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while ((Get-WatcherState) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
        if (Get-WatcherState) { throw "监听尚未退出，请检查 $stateDirectory 中的日志。" }
        Write-Host '源码监听已停止，现有容器继续运行。'
    }
    'Start' {
        $state = Get-WatcherState
        if ($state) { Write-Host "源码监听已运行：$($state.Phase)，PID $($state.ProcessId)。"; return }
        Get-Command docker -ErrorAction Stop | Out-Null
        & docker @composeArguments config --quiet
        if ($LASTEXITCODE -ne 0) { throw 'Compose 配置无效，源码监听未启动。' }
        New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
        Remove-Item -LiteralPath $stopPath -Force -ErrorAction SilentlyContinue
        $shellPath = (Get-Process -Id $PID).Path
        $arguments = @('-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $PSCommandPath + '"'), '-Action', 'Run')
        if ($SkipInitialBuild) { $arguments += '-SkipInitialBuild' }
        $runner = Start-Process -FilePath $shellPath -ArgumentList $arguments -WorkingDirectory $repositoryDirectory `
            -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $stateDirectory 'supervisor.log') `
            -RedirectStandardError (Join-Path $stateDirectory 'supervisor.error.log')
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        do {
            if ($runner.HasExited) { throw "监听启动失败，请检查 $stateDirectory/supervisor.error.log。" }
            $state = Get-WatcherState
            if ($state) { Write-Host "源码监听已启动：$($state.Phase)，PID $($state.ProcessId)。日志：$stateDirectory"; return }
            Start-Sleep -Milliseconds 200
        } while ([DateTime]::UtcNow -lt $deadline)
        throw "监听启动超时，请检查 $stateDirectory 中的日志。"
    }
    'Run' {
        New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
        Invoke-Watcher
    }
}
