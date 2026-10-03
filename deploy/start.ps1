[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$NoOpenBrowser,
    [switch]$RemindBookmark,
    [ValidateRange(10, 3600)]
    [int]$StartupTimeout = 180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-BookmarkConfirmations {
    param([string]$StatePath)

    if (-not (Test-Path -LiteralPath $StatePath)) { return @() }
    try {
        return @(Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json | Where-Object { $_ -is [string] })
    } catch {
        Write-Warning '无法读取收藏提醒记录，本次会重新提醒。'
        return @()
    }
}

function Request-BookmarkConfirmation {
    param([string]$FrontendUrl)

    $text = "前端已就绪：$FrontendUrl`n`n请在浏览器中按 Ctrl+D 将此地址加入收藏。`n`n这个地址已经收藏了吗？`n选择「是」后不再提醒；选择「否」则下次部署时继续提醒。"
    if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT -and [Environment]::UserInteractive) {
        try {
            Add-Type -AssemblyName System.Windows.Forms
            $answer = [System.Windows.Forms.MessageBox]::Show(
                $text, 'Zeye Sorting Hub - 收藏前端入口',
                [System.Windows.Forms.MessageBoxButtons]::YesNo,
                [System.Windows.Forms.MessageBoxIcon]::Information,
                [System.Windows.Forms.MessageBoxDefaultButton]::Button2
            )
            return $answer -eq [System.Windows.Forms.DialogResult]::Yes
        } catch {
            Write-Warning '无法显示收藏提示窗口。'
        }
    }
    Write-Host "请在默认浏览器中按 Ctrl+D 收藏：$FrontendUrl"
    return $null
}

function Show-BookmarkReminder {
    param(
        [string]$FrontendUrl,
        [switch]$Force,
        [string]$StatePath = (Join-Path (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Zeye.Sorting.Hub') 'bookmark-confirmations.json')
    )

    $confirmedUrls = @(Get-BookmarkConfirmations -StatePath $StatePath)
    if (-not $Force -and $confirmedUrls -contains $FrontendUrl) { return }
    $confirmed = Request-BookmarkConfirmation -FrontendUrl $FrontendUrl
    if ($null -eq $confirmed) { return }

    # Store the user's confirmation; never inspect or modify browser bookmarks.
    $updated = @($confirmedUrls | Where-Object { $_ -cne $FrontendUrl })
    if ($confirmed) { $updated += $FrontendUrl }
    try {
        $stateDirectory = Split-Path -Parent $StatePath
        New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
        ConvertTo-Json -InputObject $updated | Set-Content -LiteralPath $StatePath -Encoding UTF8
    } catch {
        Write-Warning '无法保存收藏确认记录，下次部署可能再次提醒。'
    }
}

function Wait-FrontendReady {
    param([string]$FrontendUrl, [int]$TimeoutSeconds)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            $page = Invoke-WebRequest -Uri $FrontendUrl -UseBasicParsing -TimeoutSec 5
            $healthUrl = ([uri]::new([uri]$FrontendUrl, '/health/ready')).AbsoluteUri
            $health = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 5
            if ($page.StatusCode -eq 200 -and $health.status -eq 'Healthy') { return }
        } catch {
            # Host migrations can finish after the Web container becomes healthy.
        }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    throw "前端或 API 未在 $TimeoutSeconds 秒内就绪，请检查 Compose 服务日志。"
}

function Invoke-LocalDeployment {
    param(
        [string]$ComposeFile,
        [string]$EnvFile,
        [switch]$SkipBuild,
        [switch]$NoOpenBrowser,
        [switch]$RemindBookmark,
        [int]$StartupTimeout = 180
    )

    Get-Command docker -ErrorAction Stop | Out-Null
    if (-not (Test-Path -LiteralPath $ComposeFile)) { throw "找不到 Compose 配置：$ComposeFile" }
    if (-not (Test-Path -LiteralPath $EnvFile)) { throw '请先复制 deploy/.env.example 为 deploy/.env，并设置数据库密码。' }
    $composeArguments = @('compose', '--env-file', $EnvFile, '--file', $ComposeFile)
    $upArguments = @('up', '--detach', '--wait', '--wait-timeout', [string]$StartupTimeout)
    $upArguments += if ($SkipBuild) { '--no-build' } else { '--build' }
    & docker @composeArguments @upArguments
    if ($LASTEXITCODE -ne 0) { throw "Docker 部署失败（退出码 $LASTEXITCODE）。" }
    Get-Content -LiteralPath (Join-Path $PSScriptRoot 'initialize-restore.sql') -Raw | & docker @composeArguments exec -T mysql sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" mysql -uroot'
    if ($LASTEXITCODE -ne 0) { throw '隔离恢复数据库授权初始化失败。' }

    $bindings = @(& docker @composeArguments port web 8080)
    if ($LASTEXITCODE -ne 0 -or $bindings.Count -ne 1) { throw '无法确定前端容器的本机端口。' }
    $binding = ([string]$bindings[0]).Trim()
    if ($binding -notmatch '^127\.0\.0\.1:(?<WebPort>\d{1,5})$') { throw "前端端口绑定不符合本机部署配置：$binding" }
    $webPort = [int]$Matches.WebPort
    if ($webPort -lt 1 -or $webPort -gt 65535) { throw '前端端口无效。' }
    $frontendUrl = "http://127.0.0.1:$webPort/data-overview"
    Wait-FrontendReady -FrontendUrl $frontendUrl -TimeoutSeconds $StartupTimeout
    Write-Host "部署完成，前端地址：$frontendUrl"

    if ($NoOpenBrowser) { return }
    try {
        # Opening a URI uses the user's system default browser.
        Start-Process -FilePath $frontendUrl
    } catch {
        Write-Warning "无法自动打开默认浏览器，请手动访问：$frontendUrl"
    }
    Show-BookmarkReminder -FrontendUrl $frontendUrl -Force:$RemindBookmark
}

Invoke-LocalDeployment -ComposeFile (Join-Path $PSScriptRoot 'compose.yaml') -EnvFile (Join-Path $PSScriptRoot '.env') -SkipBuild:$SkipBuild -NoOpenBrowser:$NoOpenBrowser -RemindBookmark:$RemindBookmark -StartupTimeout $StartupTimeout
