param([Parameter(Mandatory=$true)][string]$ConfigPath, [Parameter(Mandatory=$true)][string]$StreamName)
. (Join-Path $PSScriptRoot 'common.ps1')
$settings = Read-SimulatorConfig $ConfigPath
$camera = @($settings.Config.cameras | Where-Object { $_.enabled -and $_.streamName -ceq $StreamName })
if ($camera.Count -ne 1) { throw 'Stream khong ton tai hoac chua bat.' }
$camera = $camera[0]
$ffmpeg = Find-SimulatorTool $settings.Config.ffmpegPath $settings.Directory
$video = Resolve-SimulatorPath $camera.videoPath $settings.Directory
Assert-SimulatorOriginalVideo $video
$url = Get-StreamUrl $settings.Config.publishBaseUrl $StreamName
$stopFile = Join-Path $script:Runtime ($StreamName + '.stop')
$key = 'ffmpeg-' + $StreamName
$keyframeFrames = [Math]::Max(1, [int][Math]::Ceiling($camera.fps / 2))
try {
    do {
        if ((Test-Path -LiteralPath $stopFile) -or (Test-Path -LiteralPath (Join-Path $script:Runtime 'stop'))) { break }
        # Một lượt = một RTSP session. Ngắt tại seam cho backend reset tracking simulator.
        # PTS liên tục trong lượt, LIVE dùng thời gian capture UTC, không dùng PTS file làm ngày giờ.
        $arguments = @('-hide_banner', '-loglevel', 'warning', '-nostdin', '-threads', '1', '-filter_threads', '1', '-re', '-fflags', '+genpts', '-i', $video,
            '-map', '0:v:0', '-an', '-c:v', 'libx264', '-preset', 'veryfast', '-tune', 'zerolatency',
            '-threads', '2', '-profile:v', 'baseline', '-pix_fmt', 'yuv420p',
            '-vf', "scale=w='min(1280,iw)':h='min(720,ih)':force_original_aspect_ratio=decrease:force_divisible_by=2",
            '-r', [string]$camera.fps, '-g', [string]$keyframeFrames, '-keyint_min', [string]$keyframeFrames,
            '-sc_threshold', '0', '-bf', '0', '-f', 'rtsp', '-rtsp_transport', 'tcp', $url)
        $process = Start-OwnedProcess $ffmpeg $arguments $key
        while (!$process.WaitForExit(200)) {
            if ((Test-Path -LiteralPath $stopFile) -or (Test-Path -LiteralPath (Join-Path $script:Runtime 'stop'))) { break }
        }
        if (!$process.HasExited) { break }
        if ($process.ExitCode -ne 0) { Write-Error "FFmpeg $StreamName da dung do loi; xem .runtime/$key.stderr.log." }
        if (!$camera.loop) { break }
        $until = [DateTime]::UtcNow.AddSeconds($settings.Config.loopPauseSeconds)
        while ([DateTime]::UtcNow -lt $until -and !(Test-Path -LiteralPath $stopFile) -and !(Test-Path -LiteralPath (Join-Path $script:Runtime 'stop'))) {
            Start-Sleep -Milliseconds 200
        }
    } while ($camera.loop)
} finally {
    $lock = Enter-SimulatorLock
    try { Stop-OwnedProcess $key } finally { $lock.Dispose() }
}
