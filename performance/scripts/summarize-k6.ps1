param(
    [Parameter(Mandatory = $true)]
    [string[]]$InputPath,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$rows = [System.Collections.Generic.List[string]]::new()

foreach ($path in $InputPath) {
    if (-not (Test-Path -LiteralPath $path)) {
        continue
    }

    $summary = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    $duration = $summary.metrics.http_req_duration.values
    $requestRate = $summary.metrics.http_reqs.values.rate
    $failedRate = $summary.metrics.http_req_failed.values.rate
    $scenario = [System.IO.Path]::GetFileNameWithoutExtension($path)
    $rows.Add(
        "| $scenario | $([math]::Round($requestRate, 2)) | $([math]::Round($duration.med, 2)) | $([math]::Round($duration.'p(95)', 2)) | $([math]::Round($duration.'p(99)', 2)) | $([math]::Round($failedRate * 100, 4))% |")
}

$content = @(
    '# k6 性能回归摘要',
    '',
    "生成时间：$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')",
    '',
    '| 场景 | RPS | P50(ms) | P95(ms) | P99(ms) | 错误率 |',
    '|---|---:|---:|---:|---:|---:|'
) + $rows

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$content | Set-Content -LiteralPath $OutputPath -Encoding UTF8
