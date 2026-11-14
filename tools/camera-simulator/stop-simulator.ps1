param([ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}$')][string]$StreamName)
. (Join-Path $PSScriptRoot 'common.ps1')
if (!(Test-Path -LiteralPath $script:Runtime)) { Write-Host 'Simulator chua chay.'; return }
$lock = Enter-SimulatorLock
try {
    if ($StreamName) {
        $names = @($StreamName)
        [IO.File]::WriteAllText((Join-Path $script:Runtime ($StreamName + '.stop')), 'stop')
    } else {
        [IO.File]::WriteAllText((Join-Path $script:Runtime 'stop'), 'stop')
        $names = @(Get-ChildItem -LiteralPath $script:Runtime -Filter 'publisher-*.json' | ForEach-Object { $_.BaseName.Substring(10) })
    }
} finally { $lock.Dispose() }
# Cho supervisor nhan stop va tu don FFmpeg; khong giu khoa trong luc doi.
Start-Sleep -Milliseconds 500
$lock = Enter-SimulatorLock
try {
    foreach ($name in $names) {
        Stop-OwnedProcess ('ffmpeg-' + $name)
        Stop-OwnedProcess ('publisher-' + $name)
    }
    if (!$StreamName) {
        # Thu gom FFmpeg neu supervisor da crash truoc khi ghi/xoa publisher record.
        foreach ($record in Get-ChildItem -LiteralPath $script:Runtime -Filter 'ffmpeg-*.json') { Stop-OwnedProcess $record.BaseName }
        foreach ($record in Get-ChildItem -LiteralPath $script:Runtime -Filter 'probe-*.json') { Stop-OwnedProcess $record.BaseName }
        Stop-OwnedProcess 'mediamtx'
    }
} finally { $lock.Dispose() }
Write-Host 'Da dung cac process thuoc simulator; giu log de chan doan.'
