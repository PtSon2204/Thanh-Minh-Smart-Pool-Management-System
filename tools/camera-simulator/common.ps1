# Dùng được với Windows PowerShell 5.1 và PowerShell 7 trên Windows.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:Runtime = Join-Path $PSScriptRoot '.runtime'

function Read-SimulatorConfig([string]$Path) {
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $config = Get-Content -LiteralPath $resolved -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($config.environment -cnotin @('Development', 'Testing')) { throw 'Simulator chi ho tro Development/Testing.' }
    if ($config.enabled -isnot [bool] -or !$config.enabled) { throw 'Dat enabled=true trong cau hinh de bat simulator.' }
    if ($config.loopPauseSeconds -isnot [ValueType] -or $config.loopPauseSeconds -is [bool] -or $config.loopPauseSeconds -lt 0 -or $config.loopPauseSeconds -gt 60) { throw 'loopPauseSeconds phai tu 0 den 60.' }
    foreach ($base in @($config.publishBaseUrl, $config.consumerBaseUrl)) {
        $uri = $null
        if (![Uri]::TryCreate($base, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne 'rtsp' -or
            $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $uri.AbsolutePath -ne '/' -or $uri.Port -eq 0 -or $base -match '[\s\x00-\x1f]') {
            throw 'Base URL phai la rtsp://host:port, khong co credentials/path/query.'
        }
    }
    $names = @{}
    foreach ($camera in $config.cameras) {
        if ($camera.streamName -cnotmatch '^[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}$') { throw 'streamName chi gom chu, so, _ va - (toi da 64 ky tu).' }
        if ($names.ContainsKey($camera.streamName)) { throw 'streamName bi trung (ke ca khac chu hoa/thuong).' }
        $names[$camera.streamName] = $true
        if ($camera.fps -isnot [ValueType] -or $camera.fps -lt 1 -or $camera.fps -gt 60 -or [int]$camera.fps -ne $camera.fps) { throw 'FPS phai la so nguyen tu 1 den 60.' }
        if ($camera.loop -isnot [bool] -or $camera.enabled -isnot [bool]) { throw 'loop/enabled phai la boolean.' }
    }
    [pscustomobject]@{ Path = $resolved; Directory = Split-Path -Parent $resolved; Config = $config }
}

function Resolve-SimulatorPath([string]$Path, [string]$Directory) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    [IO.Path]::GetFullPath((Join-Path $Directory $Path))
}

function Assert-SimulatorOriginalVideo([string]$Path) {
    $name = [IO.Path]::GetFileName($Path)
    if ($name -match '^([a-fA-F0-9]{32})-result\.mp4$') {
        $original = Join-Path (Split-Path -Parent $Path) ($Matches[1] + '.mp4')
        if (Test-Path -LiteralPath $original -PathType Leaf) {
            throw "Video ket qua SmartPool da ve box AI. Chon video goc de tranh box/ID chong len nhau: $original"
        }
    }
}

function Find-SimulatorTool([string]$Name, [string]$Directory) {
    $local = Resolve-SimulatorPath $Name $Directory
    if (Test-Path -LiteralPath $local -PathType Leaf) { return $local }
    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (!$command) { throw "Chua tim thay $Name. Cai tool hoac dat duong dan tuyet doi trong cameras.json." }
    $command.Source
}

function Get-StreamUrl([string]$Base, [string]$Name) { $Base.TrimEnd('/') + '/' + $Name }

function Test-SimulatorPort([Uri]$Uri) {
    $client = New-Object Net.Sockets.TcpClient
    try {
        $port = if ($Uri.Port -gt 0) { $Uri.Port } else { 554 }
        $task = $client.ConnectAsync($Uri.DnsSafeHost, $port)
        return $task.Wait(750) -and $client.Connected
    } catch { return $false } finally { $client.Dispose() }
}

# Windows CreateProcess: quote argument, không qua cmd.exe/Invoke-Expression.
function Join-NativeArguments([string[]]$Values) {
    ($Values | ForEach-Object {
        if ($_ -match '[\x00\r\n]') { throw 'Argument khong hop le.' }
        '"' + ([regex]::Replace([regex]::Replace($_, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1')) + '"'
    }) -join ' '
}

function Get-OwnedProcess($Record) {
    $process = Get-Process -Id $Record.id -ErrorAction SilentlyContinue
    if (!$process -or $process.HasExited) { return $null }
    # Không xóa record nếu thiếu quyền đọc thông tin của process còn sống.
    if ($process.StartTime.ToUniversalTime().Ticks.ToString() -ne $Record.startedTicks) { return $null }
    $actualPath = $process.Path
    if (!$actualPath) {
        if ($process.HasExited) { return $null }
        throw 'Khong doc duoc executable cua process; giu PID record de thu lai voi quyen phu hop.'
    }
    if ($actualPath -ne $Record.executable) { return $null }
    $process
}

function Start-OwnedProcess([string]$Executable, [string[]]$Arguments, [string]$Key) {
    # Khóa được giữ xuyên suốt check-stop/start/ghi PID, tránh Stop đua với publisher.
    $lock = Enter-SimulatorLock
    try {
        if ($Key -notlike 'probe-*' -and (Test-Path -LiteralPath (Join-Path $script:Runtime 'stop'))) { throw 'Simulator dang dung.' }
        if ($Key -match '^ffmpeg-(.+)$' -and (Test-Path -LiteralPath (Join-Path $script:Runtime ($Matches[1] + '.stop')))) { throw 'Stream dang dung.' }
        $recordPath = Join-Path $script:Runtime ($Key + '.json')
        if (Test-Path -LiteralPath $recordPath) {
            $old = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
            if (Get-OwnedProcess $old) { throw "Process $Key van dang chay." }
        }
        $process = Start-Process -FilePath $Executable -ArgumentList (Join-NativeArguments $Arguments) -PassThru -WindowStyle Hidden `
            -RedirectStandardOutput (Join-Path $script:Runtime ($Key + '.stdout.log')) `
            -RedirectStandardError (Join-Path $script:Runtime ($Key + '.stderr.log'))
        # Giữ native handle để ExitCode không thành null trong Windows PowerShell 5.1.
        $null = $process.Handle
        try {
            $record = [pscustomobject]@{ id = $process.Id; startedTicks = $process.StartTime.ToUniversalTime().Ticks.ToString(); executable = $Executable }
            $record | ConvertTo-Json | Set-Content -LiteralPath $recordPath -Encoding UTF8
        } catch { if (!$process.HasExited) { $process.Kill() }; throw }
        $process
    } finally { $lock.Dispose() }
}

function Enter-SimulatorLock {
    [IO.Directory]::CreateDirectory($script:Runtime) | Out-Null
    $until = [DateTime]::UtcNow.AddSeconds(10)
    do {
        try { return [IO.File]::Open((Join-Path $script:Runtime 'control.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
        catch [IO.IOException] { Start-Sleep -Milliseconds 100 }
    } while ([DateTime]::UtcNow -lt $until)
    throw 'Simulator dang duoc lenh khac dieu khien. Thu lai sau.'
}

function Stop-OwnedProcess([string]$Key) {
    $path = Join-Path $script:Runtime ($Key + '.json')
    if (!(Test-Path -LiteralPath $path)) { return }
    $record = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $process = Get-OwnedProcess $record
    if ($process) {
        # /T gồm tiến trình con (ví dụ chocolatey shim -> ffmpeg.exe), chỉ theo PID đã xác minh.
        try { & "$env:SystemRoot\System32\taskkill.exe" /PID $process.Id /T /F 2>$null | Out-Null }
        catch { if (!$process.HasExited) { throw } }
        if ($LASTEXITCODE -ne 0 -and !$process.HasExited) { throw "Khong dung duoc process tree $Key." }
        if (!$process.WaitForExit(5000)) { throw "Khong dung duoc $Key; giu lai PID de thu lai." }
    }
    Remove-Item -LiteralPath $path
}
