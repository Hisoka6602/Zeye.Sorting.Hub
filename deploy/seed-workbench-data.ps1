param(
    [string]$AnchorDate = (Get-Date -Format 'yyyy-MM-dd'),
    [string]$BaseUrl = 'http://127.0.0.1:4187',
    [Microsoft.PowerShell.Commands.WebRequestSession]$Session
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

# Only the isolated Compose database is targeted. These are synthetic source
# snapshots for the workbench overview, not device processing histories.
$uri = [uri]$BaseUrl
if (-not $uri.IsLoopback) { throw 'BaseUrl must point to this machine.' }
$requestSession = if ($null -ne $Session) { $Session } else { [Microsoft.PowerShell.Commands.WebRequestSession]::new() }
$access = Invoke-RestMethod -Uri "$($BaseUrl.TrimEnd('/'))/api/access/session" -WebSession $requestSession
if ($access.enforceAuthorization -and -not $access.authenticated) { throw 'Login first and pass the authenticated WebRequestSession with -Session.' }
$anchor = [datetime]::ParseExact($AnchorDate, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
$anchorKey = $anchor.ToString('yyyyMMdd')
$sourceInstance = 'workbench-demo-v1'
$sourceRun = "seed-$anchorKey"
$composeFile = Join-Path $PSScriptRoot 'compose.yaml'
$envFile = Join-Path $PSScriptRoot '.env'
if (-not (Test-Path -LiteralPath $envFile)) { throw 'deploy/.env is missing.' }

$dockerArgs = @(
    'compose', '--env-file', $envFile, '-f', $composeFile,
    'exec', '-T', 'mysql', 'sh', '-lc',
    'MYSQL_PWD="$MYSQL_PASSWORD" mysql --default-character-set=utf8mb4 -u sorting_hub -N -B zeye_sorting_hub'
)
function Invoke-DockerSql([string]$statement) {
    $output = $statement | & docker @script:dockerArgs
    if ($LASTEXITCODE -ne 0) { throw "Docker MySQL command failed with exit code $LASTEXITCODE." }
    return $output
}
function SqlText([string]$value) { return "'" + $value.Replace("'", "''") + "'" }
function SqlDate([datetime]$value) { return SqlText ($value.ToString('yyyy-MM-dd HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture)) }

$days = @(
    @{ count = 8;  completed = 6;  exception = 1 },
    @{ count = 10; completed = 7;  exception = 2 },
    @{ count = 9;  completed = 7;  exception = 1 },
    @{ count = 12; completed = 9;  exception = 2 },
    @{ count = 11; completed = 8;  exception = 2 },
    @{ count = 14; completed = 11; exception = 2 },
    @{ count = 16; completed = 12; exception = 3 }
)
$exceptionCodes = @(1, 3, 13, 14, 15)
$rows = [System.Collections.Generic.List[string]]::new()
$ids = [System.Collections.Generic.List[string]]::new()
$expectedCompleted = 0
$expectedException = 0
$expectedNoRead = 0
$invariant = [Globalization.CultureInfo]::InvariantCulture

for ($dayIndex = 0; $dayIndex -lt $days.Count; $dayIndex++) {
    $day = $anchor.Date.AddDays($dayIndex - 6)
    $dateKey = $day.ToString('yyyyMMdd')
    $plan = $days[$dayIndex]
    $expectedCompleted += $plan.completed
    $expectedException += $plan.exception
    for ($number = 1; $number -le $plan.count; $number++) {
        $suffix = $number.ToString('0000')
        $id = [long]::Parse("8$dateKey$suffix")
        $ids.Add([string]$id)
        $scan = $day.AddHours(7).AddMinutes(19 * $number + $dayIndex)
        $status = if ($number -le $plan.completed) { 1 } elseif ($number -le $plan.completed + $plan.exception) { 2 } else { 0 }
        $exceptionType = if ($status -eq 2) { [string]$exceptionCodes[($number + $dayIndex) % $exceptionCodes.Count] } else { 'NULL' }
        $isNoRead = (($number + 3 * $dayIndex) % 13 -eq 0)
        if ($isNoRead) { $expectedNoRead++ }
        $noReadType = if ($isNoRead) { 3 } else { 0 }
        $lifecycle = if ($status -eq 1) { 18000 + (($number * 7 + $dayIndex * 11) % 30) * 1000 } else { $null }
        $completedAt = if ($status -eq 1) { $scan.AddMilliseconds($lifecycle) } else { $null }
        $targetId = 101 + (($number + $dayIndex) % 8)
        $actualId = if ($status -eq 2) { 201 + (($number + $dayIndex) % 8) } elseif ($status -eq 1) { $targetId } else { $null }
        $weight = (0.35 + (($number * 19 + $dayIndex * 7) % 320) / 100).ToString('0.00', $invariant)
        $length = 190 + $number * 4
        $width = 120 + ($number % 7) * 7
        $height = 80 + ($number % 5) * 6
        $workstation = "工作台 $((($number + $dayIndex) % 4) + 1)"
        $barcode = "WB-DEMO-$dateKey-$suffix"
        $values = @(
            [string]$id, [string]$scan.Ticks, '0', [string]$status, $exceptionType, [string]$noReadType,
            $(if ($null -eq $lifecycle) { 'NULL' } else { [string]$lifecycle }),
            [string]$targetId, $(if ($null -eq $actualId) { 'NULL' } else { [string]$actualId }),
            (SqlText $barcode), $weight, [string]$(if ($status -eq 0) { 0 } elseif ($status -eq 1) { 1 } else { 2 }),
            (SqlText "WB-DEMO-$dateKey"), (SqlText $workstation), '0', [string]$length, [string]$width, [string]$height,
            [string]($length * $width * $height), (SqlDate $scan),
            $(if ($null -eq $completedAt) { 'NULL' } else { SqlDate $completedAt }),
            $(if ($null -eq $completedAt) { 'NULL' } else { SqlDate $completedAt }),
            '0', '0', (SqlText 'demo'), (SqlDate $scan),
            (SqlDate $(if ($null -eq $completedAt) { $scan.AddMinutes(1) } else { $completedAt })),
            (SqlText '127.0.0.1'),
            $(if ($null -eq $actualId) { 'NULL' } else { SqlText ([string]$actualId) }),
            (SqlDate $scan), (SqlDate $scan.AddSeconds(1)),
            $(if ($status -eq 2) { SqlText 'DemoSorterException' } else { 'NULL' }),
            (SqlText $sourceInstance), [string]$id, (SqlText $sourceRun), (SqlText ([string]$targetId))
        )
        $rows.Add('(' + ($values -join ',') + ')')
    }
}

$idList = $ids -join ','
$conflictSql = "SELECT COUNT(*) FROM Parcels WHERE Id IN ($idList) AND (SourceInstanceId IS NULL OR SourceInstanceId <> '$sourceInstance' OR SourceRunId <> '$sourceRun' OR BarCodes NOT LIKE 'WB-DEMO-%');"
$conflicts = [int]([string](Invoke-DockerSql $conflictSql | Select-Object -Last 1)).Trim()
if ($conflicts -ne 0) { throw "Reserved test IDs conflict with $conflicts existing parcel(s); no rows were changed." }

$columns = @(
    'Id','ParcelTimestamp','Type','Status','ExceptionType','NoReadType','LifecycleMilliseconds',
    'TargetChuteId','ActualChuteId','BarCodes','Weight','RequestStatus','BagCode','WorkstationName',
    'IsSticking','Length','Width','Height','Volume','ScannedTime','DischargeTime','CompletedTime',
    'HasImages','HasVideos','Coordinate','CreatedTime','ModifyTime','ModifyIp','ActualChuteCode',
    'DetectedTime','MeasurementTime','SourceExceptionCode','SourceInstanceId','SourceParcelId',
    'SourceRunId','TargetChuteCode'
) -join ','
$sql = "SET NAMES utf8mb4; START TRANSACTION; INSERT INTO Parcels ($columns) VALUES " + ($rows -join ",`n") + ' ON DUPLICATE KEY UPDATE Id=Id; COMMIT;'
$null = Invoke-DockerSql $sql

$databaseCheck = [string](Invoke-DockerSql "SELECT COUNT(*), SUM(Status=1), SUM(Status=2), SUM(Status=0) FROM Parcels WHERE Id IN ($idList) AND SourceInstanceId='$sourceInstance' AND SourceRunId='$sourceRun';" | Select-Object -Last 1)
$actual = $databaseCheck.Trim() -split "`t"
$expectedTotal = $rows.Count
if ($actual.Count -ne 4 -or [int]$actual[0] -ne $expectedTotal -or [int]$actual[1] -ne $expectedCompleted -or [int]$actual[2] -ne $expectedException) {
    throw "Database verification failed: $databaseCheck"
}

$from = $anchor.AddDays(-6).ToString('yyyy-MM-dd')
$to = $anchor.ToString('yyyy-MM-dd')
$report = Invoke-RestMethod -Uri "$($BaseUrl.TrimEnd('/'))/api/parcels/analytics?fromDate=$from&toDate=$to" -Method Get -WebSession $requestSession
if ($report.detectedCount -lt $expectedTotal -or $report.completedCount -lt $expectedCompleted -or
    $report.exceptionCount -lt $expectedException -or $report.noReadCount -lt $expectedNoRead -or
    $null -eq $report.averageLifecycleSeconds -or @($report.daily).Count -lt 7) {
    throw 'Analytics API verification failed after seeding.'
}

Write-Output "Workbench seed verified: $from..$to, parcels=$expectedTotal, completed=$expectedCompleted, exception=$expectedException, pending=$([int]$actual[3]), NoRead=$expectedNoRead."
