param(
    [string]$Date = (Get-Date -Format 'yyyy-MM-dd'),
    [string]$BaseUrl = 'http://127.0.0.1:4187',
    [Microsoft.PowerShell.Commands.WebRequestSession]$Session
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Seed through the same API used by the UI, so domain validation and status transitions apply.
$day = [datetime]::ParseExact($Date, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
$dateKey = $day.ToString('yyyyMMdd')
$barcodePrefix = "TEST-$dateKey-"
$api = $BaseUrl.TrimEnd('/')
$requestSession = if ($null -ne $Session) { $Session } else { [Microsoft.PowerShell.Commands.WebRequestSession]::new() }
$requestSession.Headers['X-Zeye-Client'] = 'web'
$access = Invoke-RestMethod -Uri "$api/api/access/session" -WebSession $requestSession
if ($access.enforceAuthorization -and -not $access.authenticated) { throw 'Login first and pass the authenticated WebRequestSession with -Session.' }
$start = $day.ToString('yyyy-MM-ddT00:00:00')
$end = $day.ToString('yyyy-MM-ddT23:59:59')
$query = "barCodeKeyword=$([uri]::EscapeDataString($barcodePrefix))&scannedTimeStart=$start&scannedTimeEnd=$end&pageNumber=1&pageSize=200&includeTotalCount=true"
$existing = Invoke-RestMethod -Uri "$api/api/parcels?$query" -Method Get -WebSession $requestSession
$byId = @{}
foreach ($parcel in $existing.items) {
    $byId[[string]$parcel.id] = $parcel
}

$created = 0
$updated = 0
for ($number = 1; $number -le 50; $number++) {
    $suffix = $number.ToString('0000')
    $id = [long]::Parse("9$dateKey$suffix")
    $barcode = "$barcodePrefix$suffix"
    $scan = $day.AddMinutes(6 + ($number - 1) * 5)
    $discharge = $scan.AddSeconds(12 + (($number * 7) % 36))
    $targetChute = 101 + (($number - 1) % 8)
    $expectedStatus = if ($number -le 35) { 1 } elseif ($number -le 45) { 2 } else { 0 }
    $actualChute = if ($expectedStatus -eq 2) { 101 + ($number % 8) } else { $targetChute }
    $weight = [decimal]([math]::Round(0.25 + (($number * 37) % 400) / 100, 2))
    $length = 200 + $number * 3
    $width = 120 + ($number % 10) * 5
    $height = 70 + ($number % 7) * 4

    $record = $byId[[string]$id]
    if ($null -ne $record -and $record.barCodes -ne $barcode) {
        throw "Parcel ID $id is already used by a different barcode."
    }
    if ($null -eq $record) {
        $body = @{
            id = $id
            parcelTimestamp = $scan.Ticks
            type = @(0, 1, 3, 4)[($number - 1) % 4]
            barCodes = $barcode
            weight = $weight
            workstationName = "工作台 $((($number - 1) % 4) + 1)"
            scannedTime = $scan.ToString('yyyy-MM-ddTHH:mm:ss')
            dischargeTime = $discharge.ToString('yyyy-MM-ddTHH:mm:ss')
            targetChuteId = $targetChute
            actualChuteId = $actualChute
            requestStatus = if ($expectedStatus -eq 0) { 0 } elseif ($expectedStatus -eq 2) { 2 } else { 1 }
            bagCode = "TB-$dateKey-$([math]::Ceiling($number / 10).ToString('00'))"
            isSticking = ($number % 17 -eq 0)
            length = $length
            width = $width
            height = $height
            volume = $length * $width * $height
            hasImages = ($number % 3 -eq 0)
            hasVideos = $false
            coordinate = "x:$($number % 12),y:$([math]::Floor($number / 12))"
            lifecycleMilliseconds = [long]($discharge - $scan).TotalMilliseconds
        }
        $record = Invoke-RestMethod -Uri "$api/api/admin/parcels" -Method Post -WebSession $requestSession -ContentType 'application/json; charset=utf-8' -Body ($body | ConvertTo-Json -Compress)
        $created++
    }

    if ([int]$record.status -ne $expectedStatus) {
        if ($expectedStatus -eq 0) {
            throw "Existing parcel $id is no longer pending; refusing to reset its status."
        }
        $change = if ($expectedStatus -eq 1) {
            @{ operation = 1; completedTime = $discharge.ToString('yyyy-MM-ddTHH:mm:ss') }
        } else {
            @{ operation = 2; exceptionType = 1 + (($number - 36) % 5) }
        }
        $null = Invoke-RestMethod -Uri "$api/api/admin/parcels/$id" -Method Put -WebSession $requestSession -ContentType 'application/json; charset=utf-8' -Body ($change | ConvertTo-Json -Compress)
        $updated++
    }
}

$verified = Invoke-RestMethod -Uri "$api/api/parcels?$query" -Method Get -WebSession $requestSession
if ([int]$verified.totalCount -ne 50 -or @($verified.items).Count -ne 50) {
    throw "Verification failed: expected 50 test parcels, found $($verified.totalCount)."
}
$counts = @{ pending = 0; completed = 0; exception = 0 }
foreach ($parcel in $verified.items) {
    switch ([int]$parcel.status) {
        0 { $counts.pending++ }
        1 { $counts.completed++ }
        2 { $counts.exception++ }
        default { throw "Unexpected status on parcel $($parcel.id)." }
    }
}
if ($counts.completed -ne 35 -or $counts.exception -ne 10 -or $counts.pending -ne 5) {
    throw "Verification failed: unexpected status distribution."
}
Write-Output "Seed verified: date=$Date total=50 created=$created statusUpdated=$updated completed=35 exception=10 pending=5 barcodePrefix=$barcodePrefix"
