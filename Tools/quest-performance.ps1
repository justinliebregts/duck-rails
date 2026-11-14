[CmdletBinding()]
param(
    [ValidateSet('Preflight', 'Install', 'Run', 'Status', 'Cancel', 'Collect', 'CollectMetrics')][string]$Action = 'Preflight',
    [string]$Serial,
    [string]$Adb = 'adb',
    [string]$OutputRoot = (Join-Path $PSScriptRoot '../output/quest-performance'),
    [string]$Apk = (Join-Path $PSScriptRoot '../output/quest-performance/duckrails-quest-profile.apk'),
    [string]$RunId,
    [int]$Seed = 12345,
    [ValidateRange(0,1200)][int]$DurationSeconds = 180,
    [ValidateRange(5,120)][int]$WarmupSeconds = 20,
    [ValidateRange(5,120)][int]$CompoundSeconds = 20,
    [ValidateRange(2,60)][int]$ViewSeconds = 8,
    [ValidateRange(300,1800)][int]$TimeoutSeconds = 1200,
    [ValidateRange(0,2000)][float]$FarClipMeters = 0,
    [ValidateSet('', 'control', 'atlas')][string]$MotelWingVariant = '',
    [ValidateSet('', 'control', 'atlas')][string]$MotelFullMapVariant = '',
    [string[]]$Blocks
)
$ErrorActionPreference = 'Stop'
function Invoke-Adb([string[]]$Arguments) {
    $result = & $Adb @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "ADB failed ($LASTEXITCODE): $($result -join [Environment]::NewLine)" }
    return ($result -join [Environment]::NewLine)
}
$devices = Invoke-Adb @('devices', '-l')
$ready = @($devices -split '\r?\n' | Where-Object { $_ -match '^\S+\s+device\s' })
if (!$Serial) {
    if ($ready.Count -ne 1) { throw 'Connect and authorize exactly one Quest, or specify -Serial. No device was changed.' }
    $Serial = ($ready[0] -split '\s+')[0]
}
if ($Serial -notmatch '^[A-Za-z0-9.:_-]+$') { throw 'Invalid device serial.' }
if (!($ready | Where-Object { ($_ -split '\s+')[0] -eq $Serial })) { throw 'Selected device is not connected and authorized.' }
$model = Invoke-Adb @('-s', $Serial, 'shell', 'getprop', 'ro.product.model')
if ($model.Trim() -ne 'Quest 3') { throw "Expected Quest 3; found '$model'." }
$package = 'com.gunraiders.duckrails'
$remoteRoot = "/sdcard/Android/data/$package/files"
if ($Action -eq 'Install') {
    if (!(Test-Path -LiteralPath $Apk -PathType Leaf)) { throw "APK missing: $Apk" }
    Write-Output (Invoke-Adb @('-s', $Serial, 'install', '-r', (Resolve-Path -LiteralPath $Apk).Path))
    exit
}
if ($Action -in @('Status', 'Cancel', 'Collect')) {
    if ($RunId -notmatch '^[a-f0-9]{32}$') { throw 'Provide the 32-character -RunId from Run.' }
    if ($Action -eq 'Status') { Write-Output (Invoke-Adb @('-s', $Serial, 'shell', 'cat', "$remoteRoot/QuestPerformance/$RunId/status.json")); exit }
    if ($Action -eq 'Cancel') { $null = Invoke-Adb @('-s', $Serial, 'shell', 'touch', "$remoteRoot/quest-performance-cancel-$RunId"); Write-Output 'Cancellation requested.'; exit }
    $destination = Join-Path $OutputRoot $RunId
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Write-Output (Invoke-Adb @('-s', $Serial, 'pull', "$remoteRoot/QuestPerformance/$RunId/.", $destination))
    exit
}
$installed = Invoke-Adb @('-s', $Serial, 'shell', 'pm', 'path', $package)
if ($installed -notmatch 'package:') { throw 'Duck Rails is not installed.' }
$metrics = Invoke-Adb @('-s', $Serial, 'shell', 'pm', 'path', 'com.oculus.ovrmonitormetricsservice')
$build = Invoke-Adb @('-s', $Serial, 'shell', 'dumpsys', 'package', $package)
$battery = Invoke-Adb @('-s', $Serial, 'shell', 'dumpsys', 'battery')
$report = [ordered]@{
    capturedUtc = [DateTime]::UtcNow.ToString('o')
    serial = $Serial
    model = $model.Trim()
    package = $package
    build = @($build -split '\r?\n' | Where-Object { $_ -match 'versionCode=|versionName=|flags=.*DEBUGGABLE' } | ForEach-Object { $_.Trim() })
    metricsServiceInstalled = ($metrics -match 'package:')
    battery = @($battery -split '\r?\n' | Where-Object { $_ -match 'powered:|level:|temperature:|status:' } | ForEach-Object { $_.Trim() })
    note = 'Preflight only. No gameplay, focus, refresh rate, clocks, proximity or recording settings changed. Not performance evidence.'
}
$folder = Join-Path $OutputRoot ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $folder 'preflight.json') -Encoding UTF8
if ($Action -eq 'CollectMetrics') {
    # Pull only the dedicated profiler exports, never the game's diagnostic reports.
    $remote = '/sdcard/Android/data/com.oculus.ovrmonitormetricsservice/files/CapturedMetrics'
    try { $null = Invoke-Adb @('-s', $Serial, 'shell', 'ls', $remote) }
    catch { throw "No readable metrics export directory. Enable OVR Metrics CSV recording and complete a capture first. Preflight saved in $folder. $($_.Exception.Message)" }
    $null = Invoke-Adb @('-s', $Serial, 'pull', $remote, (Join-Path $folder 'CapturedMetrics'))
    Write-Output 'Collected existing exports; filenames/timestamps must be matched to the intended run. This does not start a benchmark.'
}
Write-Output "Quest 3 preflight passed. Output: $folder"
if ($Action -eq 'Run') {
    if ($MotelWingVariant -and $MotelFullMapVariant) { throw 'Select only one Motel comparison scope.' }
    if ($FarClipMeters -ne 0 -and $FarClipMeters -lt 10) { throw 'FarClipMeters must be zero (authored) or 10–2000 metres.' }
    if ($build -notmatch 'DEBUGGABLE') { throw 'Install the development runner APK first; current app is not debuggable.' }
    if ($Blocks -and ($Blocks.Count -ne 4 -or @($Blocks | Select-Object -Unique).Count -ne 4 -or
        @($Blocks | Where-Object { $_ -notmatch '^[a-z0-9-]+$' }).Count -gt 0)) { throw 'Specify exactly four distinct block IDs.' }
    $RunId = [Guid]::NewGuid().ToString('N')
    $runFolder = Join-Path $OutputRoot $RunId
    New-Item -ItemType Directory -Path $runFolder | Out-Null
    Copy-Item -LiteralPath (Join-Path $folder 'preflight.json') -Destination $runFolder
    $request = [ordered]@{ version = 1; id = $RunId; roomCode = (Get-Random -Minimum 10000 -Maximum 100000).ToString();
        seed = $Seed; warmupSeconds = $WarmupSeconds; compoundSeconds = $CompoundSeconds;
        viewSeconds = $ViewSeconds; timeoutSeconds = $TimeoutSeconds; farClipMeters = $FarClipMeters; motelWingVariant = $MotelWingVariant; motelFullMapVariant = $MotelFullMapVariant; blocks = @($Blocks | Where-Object { $_ }) }
    $requestPath = Join-Path $runFolder 'request.json'
    [IO.File]::WriteAllText($requestPath, ($request | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    $properties = Invoke-Adb @('-s', $Serial, 'shell', 'content', 'call', '--uri', 'content://com.oculus.rc', '--method', 'GET_PROPERTY')
    $properties | Set-Content -LiteralPath (Join-Path $runFolder 'device-properties-before.txt')
    if ($properties -notmatch 'set_proximity_close=(true|false)') { throw 'Cannot determine original proximity state; device not changed.' }
    $wasClose = $Matches[1] -eq 'true'
    $restoreProximity = $false
    $restoreMetrics = $false
    $metricsLogger = $null
    $metricsRoot = '/sdcard/Android/data/com.oculus.ovrmonitormetricsservice/files/CapturedMetrics'
    $metricsBefore = @()
    $launched = $false
    try {
        if ($report.metricsServiceInstalled) {
            $null = Invoke-Adb @('-s', $Serial, 'shell', 'am', 'broadcast', '-n', 'com.oculus.ovrmonitormetricsservice/.SettingsBroadcastReceiver', '-a', 'com.oculus.ovrmonitormetricsservice.LOG_STATE')
            $recorderLog = Invoke-Adb @('-s', $Serial, 'logcat', '-d', '-t', '500')
            $recorderLines = @($recorderLog -split '\r?\n' | Where-Object { $_ -match 'SettingsBroadcastReceiver.Info.*"recordingMetricsEnabled":(true|false)' })
            if (!$recorderLines.Count) { throw 'Could not read original Meta CSV recording state; no recording settings changed.' }
            $null = $recorderLines[-1] -match '"recordingMetricsEnabled":(true|false)'
            $wasRecording = $Matches[1] -eq 'true'
            $recorderLines[-1] | Set-Content -LiteralPath (Join-Path $runFolder 'recorder-before.txt')
            try { $metricsBefore = (Invoke-Adb @('-s', $Serial, 'shell', 'ls', $metricsRoot)) -split '\r?\n' } catch { $metricsBefore = @() }
            if (!$wasRecording) {
                $restoreMetrics = $true
                $null = Invoke-Adb @('-s', $Serial, 'shell', 'am', 'broadcast', '-n', 'com.oculus.ovrmonitormetricsservice/.SettingsBroadcastReceiver', '-a', 'com.oculus.ovrmonitormetricsservice.ENABLE_CSV')
            }
        }
        $null = Invoke-Adb @('-s', $Serial, 'shell', 'am', 'force-stop', $package)
        $null = Invoke-Adb @('-s', $Serial, 'shell', 'mkdir', '-p', $remoteRoot)
        $null = Invoke-Adb @('-s', $Serial, 'push', $requestPath, "$remoteRoot/quest-performance-request.json")
        if (!$wasClose) {
            $restoreProximity = $true
            $null = Invoke-Adb @('-s', $Serial, 'shell', 'am', 'broadcast', '-a', 'com.oculus.vrpowermanager.prox_close')
        }
        $component = ((Invoke-Adb @('-s', $Serial, 'shell', 'cmd', 'package', 'resolve-activity', '--brief', $package)) -split '\r?\n' | Where-Object { $_ -match '^com\.gunraiders\.duckrails/' } | Select-Object -Last 1)
        if (!$component) { throw 'No launchable Duck Rails activity.' }
        $null = Invoke-Adb @('-s', $Serial, 'shell', 'am', 'start', '-n', $component.Trim())
        $launched = $true
        $appProcessId = ''
        for ($attempt = 0; $attempt -lt 20 -and !$appProcessId; $attempt++) {
            try { $appProcessId = (Invoke-Adb @('-s', $Serial, 'shell', 'pidof', $package)).Trim() }
            catch { Start-Sleep -Milliseconds 500 }
        }
        if ($appProcessId -notmatch '^\d+$') { throw 'Could not identify one running game process.' }
        # Record only the native performance tag for this game's PID. No game reports or account logs.
        $metricsLogger = Start-Process -FilePath (Get-Command $Adb).Source -ArgumentList @('-s', $Serial, 'logcat', '-v', 'epoch', '-T', '1', "--pid=$appProcessId", '-s', 'VrApi:I', '*:S') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runFolder 'vrapi-metrics.txt') -RedirectStandardError (Join-Path $runFolder 'vrapi-capture-errors.txt')
        Write-Output "RUN_ID=$RunId"
        Write-Output "Output: $runFolder"
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds + 60)
        $lastSegment = ''
        $lastStatus = $null
        $expeditionStarted = $null
        $timedStop = $false
        while ([DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Seconds 5
            try {
                $json = Invoke-Adb @('-s', $Serial, 'shell', 'cat', "$remoteRoot/QuestPerformance/$RunId/status.json")
                $lastStatus = $json | ConvertFrom-Json
                if ($null -eq $expeditionStarted -and $lastStatus.sessionPhase -eq 'Running') { $expeditionStarted = [double]$lastStatus.elapsed }
                if ($DurationSeconds -gt 0 -and !$timedStop -and $lastStatus.state -eq 'running' -and
                    $null -ne $expeditionStarted -and [double]$lastStatus.elapsed - $expeditionStarted -ge $DurationSeconds) {
                    $null = Invoke-Adb @('-s', $Serial, 'shell', 'touch', "$remoteRoot/quest-performance-cancel-$RunId")
                    $timedStop = $true
                    @{ reason = 'Requested short capture duration reached'; durationSeconds = $DurationSeconds } |
                        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runFolder 'capture-stop.json')
                    Write-Output 'Short capture duration reached; saving measurements.'
                }
                if ($lastStatus.segment -ne $lastSegment -or $lastStatus.state -ne 'running') {
                    Write-Output "$($lastStatus.state): $($lastStatus.segment) ($([Math]::Round($lastStatus.elapsed))s, $($lastStatus.validFrames)/$($lastStatus.frames) focused/tracked frames) $($lastStatus.message)"
                    $lastSegment = $lastStatus.segment
                }
                if ($lastStatus.state -in @('complete', 'failed')) { break }
            } catch {
                if ([DateTime]::UtcNow -gt $deadline.AddSeconds(-$TimeoutSeconds + 30)) {
                    # Startup can take time, but show read failures rather than inventing progress.
                    Write-Verbose $_.Exception.Message
                }
            }
        }
        if (!$lastStatus -or $lastStatus.state -notin @('complete', 'failed')) { throw 'Device runner deadline exceeded; no completed capture. Check headset focus/login and runner installation.' }
        $null = Invoke-Adb @('-s', $Serial, 'pull', "$remoteRoot/QuestPerformance/$RunId/.", $runFolder)
        $null = Invoke-Adb @('-s', $Serial, 'shell', 'am', 'force-stop', $package)
        if ($report.metricsServiceInstalled) {
            $metricsAfter = (Invoke-Adb @('-s', $Serial, 'shell', 'ls', $metricsRoot)) -split '\r?\n'
            $newFiles = @($metricsAfter | Where-Object { $_ -notin $metricsBefore -and $_ -match '^com\.gunraiders\.duckrails#[A-Za-z0-9_.-]+\.csv$' })
            if ($newFiles.Count) {
                $metricsOutput = Join-Path $runFolder 'ovr-metrics'
                New-Item -ItemType Directory -Path $metricsOutput -Force | Out-Null
                foreach ($file in $newFiles) { $null = Invoke-Adb @('-s', $Serial, 'pull', "$metricsRoot/$file", $metricsOutput) }
            } else { Write-Warning 'Meta did not create a new app CSV; use the PID-filtered VrApi metrics and do not reuse an older capture.' }
        }
        Invoke-Adb @('-s', $Serial, 'shell', 'dumpsys', 'battery') | Set-Content -LiteralPath (Join-Path $runFolder 'battery-after.txt')
        Write-Output "Capture retrieved: $runFolder"
        if ($lastStatus.state -eq 'failed' -and !($timedStop -and $lastStatus.message -eq 'Cancelled by PC')) { throw "Device test failed: $($lastStatus.message)" }
    } finally {
        if ($metricsLogger -and !$metricsLogger.HasExited) { Stop-Process -Id $metricsLogger.Id }
        if ($restoreMetrics) {
            try { $null = Invoke-Adb @('-s', $Serial, 'shell', 'am', 'broadcast', '-n', 'com.oculus.ovrmonitormetricsservice/.SettingsBroadcastReceiver', '-a', 'com.oculus.ovrmonitormetricsservice.DISABLE_CSV') } catch { Write-Warning "Restore Meta recording manually: $($_.Exception.Message)" }
        }
        # Also disarm a request that was never consumed (e.g. login activity failed).
        try { $null = Invoke-Adb @('-s', $Serial, 'shell', 'rm', '-f', "$remoteRoot/quest-performance-request.json") } catch { Write-Warning $_.Exception.Message }
        if ($launched) {
            try { $null = Invoke-Adb @('-s', $Serial, 'shell', 'am', 'force-stop', $package) } catch { Write-Warning $_.Exception.Message }
        }
        if ($restoreProximity) {
            try { $null = Invoke-Adb @('-s', $Serial, 'shell', 'am', 'broadcast', '-a', 'com.oculus.vrpowermanager.prox_far') } catch { Write-Warning "Restore proximity manually: $($_.Exception.Message)" }
        }
    }
}
