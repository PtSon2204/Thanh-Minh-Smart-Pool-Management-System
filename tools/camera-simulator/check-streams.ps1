param([string]$ConfigPath = (Join-Path $PSScriptRoot 'cameras.json'), [switch]$ConsumerAddress)
. (Join-Path $PSScriptRoot 'common.ps1')
$settings = Read-SimulatorConfig $ConfigPath
$ffprobe = Find-SimulatorTool $settings.Config.ffprobePath $settings.Directory
$base = if ($ConsumerAddress) { $settings.Config.consumerBaseUrl } else { $settings.Config.publishBaseUrl }
$failed = $false
foreach ($camera in @($settings.Config.cameras | Where-Object enabled)) {
    $url = Get-StreamUrl $base $camera.streamName
    if (!(Test-SimulatorPort ([Uri]$url))) {
        $failed = $true
        Write-Warning "DISCONNECTED $url | Cong RTSP khong ket noi duoc."
        continue
    }
    $key = 'probe-' + [Guid]::NewGuid().ToString('N')
    # Khong in stderr tho: FFprobe co the lap lai URL tu server redirect.
    $process = Start-OwnedProcess $ffprobe @('-v', 'error', '-rtsp_transport', 'tcp', '-timeout', '5000000', '-i', $url,
        '-select_streams', 'v:0', '-show_entries', 'stream=codec_name,width,height,r_frame_rate', '-of', 'json') $key
    try {
        if (!$process.WaitForExit(10000)) { throw 'Het thoi gian cho stream.' }
        if ($process.ExitCode -ne 0) { throw 'Khong doc duoc stream.' }
        $data = Get-Content -LiteralPath (Join-Path $script:Runtime ($key + '.stdout.log')) -Raw | ConvertFrom-Json
        if (!$data.streams.Count) { throw 'Khong co video track.' }
        $stream = $data.streams[0]
        Write-Host "CONNECTED $url | $($stream.codec_name) | $($stream.width)x$($stream.height) | FPS $($stream.r_frame_rate)"
    } catch { $failed = $true; Write-Warning "DISCONNECTED $url | $($_.Exception.Message)" }
    finally { $lock = Enter-SimulatorLock; try { Stop-OwnedProcess $key } finally { $lock.Dispose() } }
}
if ($failed) { exit 1 }
