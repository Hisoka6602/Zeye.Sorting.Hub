param(
    [ValidateSet('MySql', 'SqlServer')][string]$Provider = 'MySql',
    [string]$MySqlExe,
    [string]$SqlCmdExe,
    [Parameter(Mandatory = $true)][string]$Server,
    [int]$Port,
    [Parameter(Mandatory = $true)][string]$Database,
    [Parameter(Mandatory = $true)][string]$ExpectedDatabase,
    [string]$User,
    [Parameter(Mandatory = $true)][string]$PlanPath,
    [Parameter(Mandatory = $true)][string]$RollbackPath,
    [string]$AuditPath,
    [switch]$TrustServerCertificate,
    [switch]$Apply,
    [switch]$AllowDangerousActionExecution
)

$ErrorActionPreference = 'Stop'
if ($Database -cne $ExpectedDatabase -or $Database -cnotmatch '^[A-Za-z][A-Za-z0-9_]*$') {
    throw '数据库名称必须与显式预期值完全一致，且只能包含安全标识符字符。'
}
if ([string]::IsNullOrWhiteSpace($Server)) {
    throw '数据库服务器不能为空。'
}
if ($Apply -and -not $AllowDangerousActionExecution) {
    throw '实际建索引需要同时指定 -Apply 和 -AllowDangerousActionExecution；默认仅生成计划。'
}
$databaseExe = if ($Provider -eq 'MySql') {
    if ($Port -lt 1 -or $Port -gt 65535 -or [string]::IsNullOrWhiteSpace($User) -or [string]::IsNullOrWhiteSpace($MySqlExe)) {
        throw 'MySQL程序、端口或用户无效。'
    }
    (Resolve-Path -LiteralPath $MySqlExe).Path
} else {
    if ([string]::IsNullOrWhiteSpace($SqlCmdExe)) { throw 'SQL Server必须提供SqlCmdExe。' }
    (Resolve-Path -LiteralPath $SqlCmdExe).Path
}
$argsBase = if ($Provider -eq 'MySql') {
    @('--no-defaults', '--protocol=tcp', "--host=$Server", "--port=$Port", "--user=$User", "--database=$Database", '--batch', '--skip-column-names')
} else {
    $sqlCmdArguments = @('-S', $Server, '-d', $Database, '-b', '-h', '-1', '-W', '-w', '65535')
    if ($TrustServerCertificate) { $sqlCmdArguments += '-C' }
    if ([string]::IsNullOrWhiteSpace($User)) {
        $sqlCmdArguments += '-E'
    } else {
        if ([string]::IsNullOrWhiteSpace($env:SQLCMDPASSWORD)) { throw 'SQL Server账号认证需通过SQLCMDPASSWORD环境变量提供密码。' }
        $sqlCmdArguments += @('-U', $User)
    }
    $sqlCmdArguments
}

# 使用进程参数数组执行数据库命令，避免通过shell拼接SQL或输出连接凭据。
function Invoke-IndexDatabase {
    param([string]$Sql)
    $result = if ($Provider -eq 'MySql') {
        & $databaseExe @argsBase "--execute=$Sql" 2>&1
    } else {
        & $databaseExe @argsBase '-Q' "SET NOCOUNT ON; $Sql" 2>&1
    }
    if ($LASTEXITCODE -ne 0) { throw "$Provider 执行失败：$($result -join ' ')" }
    return @($result | ForEach-Object { "$($_)".Trim() } | Where-Object { $_.Length -gt 0 })
}

$databaseNameSql = if ($Provider -eq 'MySql') { 'SELECT DATABASE()' } else { 'SELECT DB_NAME()' }
$actualDatabase = Invoke-IndexDatabase $databaseNameSql | Select-Object -First 1
if ($actualDatabase -cne $ExpectedDatabase) { throw '实际数据库与显式预期值不一致。' }
$tableQuery = if ($Provider -eq 'MySql') {
    "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME LIKE 'Parcel_ProcessingRecords_%' ORDER BY TABLE_NAME"
} else {
    "SELECT t.name FROM sys.tables AS t JOIN sys.schemas AS s ON s.schema_id=t.schema_id WHERE s.name=N'dbo' AND t.name LIKE N'Parcel[_]ProcessingRecords[_]%' ORDER BY t.name"
}
$tableNames = @(Invoke-IndexDatabase $tableQuery)
$commands = [System.Collections.Generic.List[string]]::new()
$rollback = [System.Collections.Generic.List[string]]::new()
foreach ($tableName in $tableNames) {
    $nameOptions = if ($Provider -eq 'MySql') { [System.Text.RegularExpressions.RegexOptions]::IgnoreCase } else { [System.Text.RegularExpressions.RegexOptions]::None }
    $tableMatch = [regex]::Match($tableName, '^Parcel_ProcessingRecords_(\d{6}|\d{8}|\d{4}W\d{2})$', $nameOptions)
    if (-not $tableMatch.Success) {
        throw "遇到非预期物理表名：$tableName"
    }
    $canonicalTableName = 'Parcel_ProcessingRecords_' + $tableMatch.Groups[1].Value.ToUpperInvariant()
    $indexName = "IX_${canonicalTableName}_OccurredAt"
    $indexQuery = if ($Provider -eq 'MySql') {
        "SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '$tableName' AND INDEX_NAME = '$indexName' LIMIT 1"
    } else {
        "SELECT TOP (1) 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[dbo].[$canonicalTableName]') AND name=N'$indexName'"
    }
    $exists = @(Invoke-IndexDatabase $indexQuery)
    if ($exists.Count -eq 0) {
        if ($Provider -eq 'MySql') {
            $commands.Add("CREATE INDEX ``$indexName`` ON ``$canonicalTableName`` (``OccurredAt``);")
            $rollback.Add("DROP INDEX ``$indexName`` ON ``$canonicalTableName``;")
        } else {
            $commands.Add("CREATE INDEX [$indexName] ON [dbo].[$canonicalTableName] ([OccurredAt]);")
            $rollback.Add("DROP INDEX [$indexName] ON [dbo].[$canonicalTableName];")
        }
    }
}

$planFile = [System.IO.Path]::GetFullPath($PlanPath)
$rollbackFile = [System.IO.Path]::GetFullPath($RollbackPath)
$auditFile = [System.IO.Path]::GetFullPath($(if ($AuditPath) { $AuditPath } else { "$PlanPath.audit.log" }))
$distinctOutputPaths = @(@($planFile, $rollbackFile, $auditFile) | Sort-Object -Unique)
if ($distinctOutputPaths.Count -ne 3) {
    throw '计划、回滚与审计输出路径必须互不相同。'
}
foreach ($path in @($planFile, $rollbackFile, $auditFile)) {
    $directory = [System.IO.Path]::GetDirectoryName($path)
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
}
@("-- Provider=$Provider; Database=$actualDatabase; Mode=$(if ($Apply) {'apply'} else {'dry-run'}); GeneratedAtLocal=$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')", '-- 由已校验物理表生成，执行前复核每张表及维护窗口。') + $commands |
    Set-Content -LiteralPath $planFile -Encoding UTF8
@("-- Database=$actualDatabase; 仅在确认可回退时使用；删除索引可能影响查询性能。") + $rollback |
    Set-Content -LiteralPath $rollbackFile -Encoding UTF8
Write-Output "INDEX_BACKFILL_PLAN provider=$Provider database=$actualDatabase missing=$($commands.Count) plan=$planFile rollback=$rollbackFile apply=$Apply"
Add-Content -LiteralPath $auditFile -Encoding UTF8 -Value "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ss') provider=$Provider database=$actualDatabase mode=$(if ($Apply) {'apply'} else {'dry-run'}) missing=$($commands.Count) plan=$planFile rollback=$rollbackFile"

if ($Apply) {
    foreach ($sql in $commands) {
        try {
            Invoke-IndexDatabase $sql | Out-Null
            Add-Content -LiteralPath $auditFile -Encoding UTF8 -Value "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ss') APPLIED $sql"
            Write-Output "INDEX_BACKFILL_APPLIED $sql"
        }
        catch {
            Add-Content -LiteralPath $auditFile -Encoding UTF8 -Value "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ss') FAILED $sql $($_.Exception.Message)"
            throw
        }
    }
}
