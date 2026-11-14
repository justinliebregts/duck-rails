$ErrorActionPreference = 'Stop'
function Records($path) {
    $records = @{}
    $text = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
    foreach ($match in [regex]::Matches($text, '(?ms)^--- !u!(\d+) &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\z)')) {
        $records[$match.Groups[2].Value] = @{ Kind=$match.Groups[1].Value; Text=$match.Value }
    }
    return $records
}
$old = Records 'ArtSource/FarmSilosAtlas/pre-integration.prefab'
$new = Records 'Assets/DuckRails/Blocks/farm-silos-v1.prefab'
$missing = @($old.Keys | Where-Object { !$new.ContainsKey($_) })
$changed = @($old.Keys | Where-Object { $new.ContainsKey($_) -and $old[$_].Text -cne $new[$_].Text })
$enabledOnly = @($changed | Where-Object { $old[$_].Kind -eq '23' -and $old[$_].Text.Replace('m_Enabled: 1','m_Enabled: 0') -ceq $new[$_].Text })
$other = @($changed | Where-Object { $_ -notin $enabledOnly } | ForEach-Object { @{id=$_;kind=$old[$_].Kind} })
if ($missing.Count -ne 0 -or $enabledOnly.Count -ne 899 -or $other.Count -ne 2 -or @($other | Where-Object {$_.kind -notin @('4','114')}).Count -ne 0) { throw 'Unexpected original object record changes' }
$report = "PASS original object identity audit`nOriginal local object IDs: $($old.Count)`nPost-integration local object IDs: $($new.Count)`nMissing original IDs: $($missing.Count)`nOriginal renderer enabled-only changes: $($enabledOnly.Count)`nOther changed records (root children / lighting bindings): $($other | ConvertTo-Json -Compress)`n"
$report | Set-Content docs/validation/farm-silos-atlas/production-file-ids.txt
$report


