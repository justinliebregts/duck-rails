param(
    [ValidateSet('List','Download','Resolve','Reopen')][string]$Action = 'List',
    [string]$Id,
    [string]$Resolution,
    [string]$CredentialFile = (Join-Path $PSScriptRoot '../.env.local'),
    [int]$Offset = 0
)
$ErrorActionPreference = 'Stop'
$reportToken = $env:DUCK_RAILS_ADMIN_TOKEN
if (!$reportToken -and (Test-Path -LiteralPath $CredentialFile)) {
    foreach ($line in [IO.File]::ReadAllLines((Resolve-Path -LiteralPath $CredentialFile))) {
        if ($line -match '^\s*ADMIN_API_TOKEN\s*=\s*(.+)$') { $reportToken = $Matches[1].Trim().Trim('"').Trim("'") }
    }
}
if (!$reportToken) { throw 'Set DUCK_RAILS_ADMIN_TOKEN or configure ADMIN_API_TOKEN in the ignored Duck Rails root .env.local.' }
$reportBase = 'https://duckrails.gunraidersapi.com/api/v1/diagnostics/admin/reports'
$headers = @{ Authorization = "Bearer $reportToken" }
if ($Action -ne 'List' -and $Id -notmatch '^[a-f0-9]{32}$') { throw 'A 32-character report ID is required.' }
if ($Action -eq 'List') {
    $result = Invoke-RestMethod -Uri "$reportBase`?status=open&offset=$Offset" -Headers $headers -TimeoutSec 20
    $result.data | ConvertTo-Json -Depth 10
} elseif ($Action -eq 'Download') {
    $result = Invoke-RestMethod -Uri "$reportBase/$Id" -Headers $headers -TimeoutSec 20
    $folder = Join-Path $PSScriptRoot '../Logs/Diagnostics'
    [IO.Directory]::CreateDirectory($folder) | Out-Null
    $path = Join-Path $folder "$Id.backend.txt"
    [IO.File]::WriteAllText($path, $result.data.payload)
    Write-Output ([IO.Path]::GetFullPath($path))
} else {
    if ($Action -eq 'Resolve' -and [string]::IsNullOrWhiteSpace($Resolution)) { throw 'Provide resolution and verification evidence.' }
    $status = if ($Action -eq 'Resolve') { 'resolved' } else { 'open' }
    $body = @{ status = $status; resolution = $Resolution } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri "$reportBase/$Id/status" -Headers $headers -ContentType 'application/json' -Body $body -TimeoutSec 20 | ConvertTo-Json
}
