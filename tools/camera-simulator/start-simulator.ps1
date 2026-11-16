param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'cameras.json'),
    [Parameter(Mandatory=$true)][ValidateSet('Development', 'Testing')][string]$Environment,
    [string]$StreamName,
    [switch]$UseExistingServer
)
. (Join-Path $PSScriptRoot 'common.ps1')
$settings = Read-SimulatorConfig $ConfigPath
if ($Environment -cne $settings.Config.environment) { throw 'Environment khong khop cameras.json.' }
foreach ($variable in @('ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT')) {
    $value = [Environment]::GetEnvironmentVariable($variable)
    if ($value -and $value -cnotin @('Development', 'Testing')) { throw "$variable khong phai Development/Testing." }
}
$selected = @($settings.Config.cameras | Where-Object { $_.enabled -and (!$StreamName -or $_.streamName -ceq $StreamName) })
if (!$selected.Count) { throw 'Khong co stream enabled phu hop.' }
$ffmpeg = Find-SimulatorTool $settings.Config.ffmpegPath $settings.Directory
$ffprobe = Find-SimulatorTool $settings.Config.ffprobePath $settings.Directory
foreach ($camera in $selected) {
    $video = Resolve-SimulatorPath $camera.videoPath $settings.Directory
    if (!(Test-Path -LiteralPath $video -PathType Leaf)) { throw "Khong tim thay video cho stream $($camera.streamName): $video" }
    Assert-SimulatorOriginalVideo $video
    $metadata = & $ffprobe -v error -select_streams v:0 -show_entries stream=codec_name -of json $video 2>$null
    if ($LASTEXITCODE -ne 0 -or !(($metadata -join "`n" | ConvertFrom-Json).streams.Count)) { throw "Video $($camera.streamName) khong doc duoc video track." }
}
$serverConfig = Resolve-SimulatorPath $settings.Config.mediaMtxConfig $settings.Directory
$mediaMtx = $null
if (!$UseExistingServer) {
    $mediaMtx = Find-SimulatorTool $settings.Config.mediaMtxPath $settings.Directory
    if (!(Test-Path -LiteralPath $serverConfig -PathType Leaf)) { throw 'Khong tim thay mediamtx.yml.' }
    $publishHost = ([Uri]$settings.Config.publishBaseUrl).DnsSafeHost
    if ($publishHost -notin @('127.0.0.1', 'localhost', '::1')) { throw 'Server do script quan ly phai publish qua loopback. Server ngoai: dung -UseExistingServer.' }
}
[IO.Directory]::CreateDirectory($script:Runtime) | Out-Null
$lock = Enter-SimulatorLock
try {
    foreach ($camera in $selected) {
        $path = Join-Path $script:Runtime ('publisher-' + $camera.streamName + '.json')
        if ((Test-Path -LiteralPath $path) -and (Get-OwnedProcess (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json))) { throw "Stream $($camera.streamName) dang chay; dung Stop truoc." }
    }
    $stop = Join-Path $script:Runtime 'stop'
    if (Test-Path -LiteralPath $stop) { Remove-Item -LiteralPath $stop }
    foreach ($camera in $selected) {
        $stop = Join-Path $script:Runtime ($camera.streamName + '.stop')
        if (Test-Path -LiteralPath $stop) { Remove-Item -LiteralPath $stop }
    }
} finally { $lock.Dispose() }
$started = @()
$serverStarted = $false
try {
    if (!(Test-SimulatorPort ([Uri]$settings.Config.publishBaseUrl))) {
        if ($UseExistingServer) { throw 'RTSP server ben ngoai chua san sang.' }
        $server = Start-OwnedProcess $mediaMtx @($serverConfig) 'mediamtx'
        $serverStarted = $true
        $until = [DateTime]::UtcNow.AddSeconds(10)
        while (!(Test-SimulatorPort ([Uri]$settings.Config.publishBaseUrl))) {
            if ($server.HasExited -or [DateTime]::UtcNow -ge $until) { throw 'MediaMTX khong khoi dong duoc. Xem .runtime/mediamtx.stderr.log.' }
            Start-Sleep -Milliseconds 200
        }
    } elseif (!$UseExistingServer) {
        $record = Join-Path $script:Runtime 'mediamtx.json'
        if (!(Test-Path -LiteralPath $record) -or !(Get-OwnedProcess (Get-Content -LiteralPath $record -Raw | ConvertFrom-Json))) {
            throw 'Cong RTSP dang do service khac su dung. Dung -UseExistingServer neu day la server test cua ban.'
        }
    }
    $powershell = (Get-Process -Id $PID).Path
    foreach ($camera in $selected) {
        $key = 'publisher-' + $camera.streamName
        $process = Start-OwnedProcess $powershell @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'publish-stream.ps1'), '-ConfigPath', $settings.Path, '-StreamName', $camera.streamName) $key
        $started += $camera.streamName
        Start-Sleep -Milliseconds 700
        if ($process.HasExited) { throw "Publisher $($camera.streamName) da dung. Xem .runtime/$key.stderr.log." }
        Write-Host "$($camera.name): $(Get-StreamUrl $settings.Config.consumerBaseUrl $camera.streamName)"
    }
    Write-Host 'Da khoi dong publisher. Chay check-streams.ps1 de xac minh media; dung stop-simulator.ps1 de dung.'
} catch {
    foreach ($name in $started) { & (Join-Path $PSScriptRoot 'stop-simulator.ps1') -StreamName $name }
    if ($serverStarted) { $lock = Enter-SimulatorLock; try { Stop-OwnedProcess 'mediamtx' } finally { $lock.Dispose() } }
    throw
}
