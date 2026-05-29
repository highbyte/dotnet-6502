$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptDir '..\..')

$HeadlessProject = Join-Path $RepoRoot 'src/apps/Highbyte.DotNet6502.App.Headless/Highbyte.DotNet6502.App.Headless.csproj'
$RemoteProject = Join-Path $RepoRoot 'src/apps/Highbyte.DotNet6502.App.RemoteClient/Highbyte.DotNet6502.App.RemoteClient.csproj'
$HeadlessOut = Join-Path $RepoRoot 'src/apps/Highbyte.DotNet6502.App.Headless/bin/Debug/net10.0'
$RemoteOut = Join-Path $RepoRoot 'src/apps/Highbyte.DotNet6502.App.RemoteClient/bin/Debug/net10.0'
$HeadlessDll = Join-Path $HeadlessOut 'Highbyte.DotNet6502.App.Headless.dll'
$RemoteDll = Join-Path $RemoteOut 'Highbyte.DotNet6502.App.RemoteClient.dll'
$AppSettingsPath = Join-Path $HeadlessOut 'appsettings.Development.json'

function Get-PythonCommand {
    if (Get-Command py -ErrorAction SilentlyContinue) {
        return @('py', '-3')
    }
    if (Get-Command python -ErrorAction SilentlyContinue) {
        return @('python')
    }
    throw 'Python 3 is required.'
}

function Invoke-Python {
    param([string[]]$Arguments)
    $prefix = @()
    if ($PythonCommand.Count -gt 1) {
        $prefix = $PythonCommand[1..($PythonCommand.Count - 1)]
    }
    & $PythonCommand[0] @($prefix + $Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw 'Python command failed.'
    }
}

function Get-FreePort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try {
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Invoke-RemoteCommand {
    param([string[]]$Arguments)
    $output = & dotnet $RemoteDll --port $RemotePort @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ($output | Out-String)
    }
    return ($output | Out-String)
}

$PythonCommand = Get-PythonCommand
$RemotePort = Get-FreePort
$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("swiftlink-smoke-" + [System.Guid]::NewGuid().ToString('N'))
$PrgPath = Join-Path $TempDir 'swiftlink-smoke.prg'
$EchoPortFile = Join-Path $TempDir 'echo.port'
$EchoLog = Join-Path $TempDir 'echo.log'
$EchoStdout = Join-Path $TempDir 'echo.stdout.log'
$EchoStderr = Join-Path $TempDir 'echo.stderr.log'
$HeadlessStdout = Join-Path $TempDir 'headless.stdout.log'
$HeadlessStderr = Join-Path $TempDir 'headless.stderr.log'
$AppSettingsBackup = Join-Path $TempDir 'appsettings.Development.backup.json'

$EchoProcess = $null
$HeadlessProcess = $null
$HadAppSettings = Test-Path $AppSettingsPath
$Success = $false

New-Item -ItemType Directory -Path $TempDir | Out-Null

try {
    if ($HadAppSettings) {
        Copy-Item $AppSettingsPath $AppSettingsBackup
    }

    Write-Host '==> Building Headless app and remote client'
    dotnet build $HeadlessProject | Out-Null
    dotnet build $RemoteProject | Out-Null

    Write-Host '==> Starting local TCP echo server'
    $echoArgs = @()
    if ($PythonCommand.Count -gt 1) {
        $echoArgs += $PythonCommand[1..($PythonCommand.Count - 1)]
    }
    $echoArgs += @(
        (Join-Path $ScriptDir 'tcp_echo_server.py'),
        '--host', '127.0.0.1',
        '--port', '0',
        '--port-file', $EchoPortFile,
        '--log-file', $EchoLog
    )
    $EchoProcess = Start-Process -FilePath $PythonCommand[0] -ArgumentList $echoArgs -PassThru -RedirectStandardOutput $EchoStdout -RedirectStandardError $EchoStderr -NoNewWindow

    for ($i = 0; $i -lt 40 -and -not (Test-Path $EchoPortFile); $i++) {
        Start-Sleep -Milliseconds 250
    }
    if (-not (Test-Path $EchoPortFile)) {
        throw 'Echo server did not publish a port.'
    }
    $EchoPort = (Get-Content $EchoPortFile -Raw).Trim()

    Write-Host '==> Generating SwiftLink smoke PRG'
    Invoke-Python @((Join-Path $ScriptDir 'write_smoke_prg.py'), $PrgPath)

    @"
{
  "Highbyte.DotNet6502.C64.Headless": {
    "SwiftLinkHost": {
      "TcpHost": "127.0.0.1",
      "TcpPort": $EchoPort,
      "ConnectOnBoot": true
    },
    "SystemConfig": {
      "SwiftLink": {
        "Enabled": true,
        "CartridgeIOAddress": "DE00",
        "ReceiveMode": "FastBuffered"
      }
    }
  }
}
"@ | Set-Content -Path $AppSettingsPath -Encoding utf8

    Write-Host '==> Starting Headless app'
    $headlessArgs = @(
        $HeadlessDll,
        '--system', 'C64',
        '--start',
        '--waitForSystemReady',
        '--remote-port', $RemotePort,
        '--allow-remote-quit',
        '-l', 'Warning'
    )
    $HeadlessProcess = Start-Process -FilePath 'dotnet' -ArgumentList $headlessArgs -PassThru -RedirectStandardOutput $HeadlessStdout -RedirectStandardError $HeadlessStderr -NoNewWindow

    Write-Host '==> Waiting for remote control endpoint'
    $ready = $false
    for ($i = 0; $i -lt 80; $i++) {
        try {
            Invoke-RemoteCommand @('emu.state') | Out-Null
            $ready = $true
            break
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }
    if (-not $ready) {
        throw "Remote control endpoint did not become ready. Headless stdout: $HeadlessStdout"
    }

    Write-Host '==> Waiting for SwiftLink TCP connection'
    $connected = $false
    for ($i = 0; $i -lt 80; $i++) {
        if ((Test-Path $EchoStdout) -and (Select-String -Path $EchoStdout -Pattern 'connected ' -Quiet)) {
            $connected = $true
            break
        }
        Start-Sleep -Milliseconds 250
    }
    if (-not $connected) {
        throw "SwiftLink transport did not connect to the local echo server. Headless stdout: $HeadlessStdout"
    }

    Write-Host '==> Loading PRG and starting it through remote control'
    Invoke-RemoteCommand @('c64.loadprg', '--file', $PrgPath) | Out-Null
    Invoke-RemoteCommand @('cpu.set', '--pc', 'C000') | Out-Null

    Write-Host '==> Waiting for echoed byte to reach C64 memory'
    $value = $null
    for ($i = 0; $i -lt 80; $i++) {
        try {
            $memJson = Invoke-RemoteCommand @('mem.read', '--addr', 'C100', '--len', '1')
            $value = (($memJson | ConvertFrom-Json).data | Select-Object -First 1)
            if ($value -eq 65) {
                break
            }
        }
        catch {
        }
        Start-Sleep -Milliseconds 250
    }

    if ($value -ne 65) {
        throw "Expected C64 memory `$C100 to contain 65, got '$value'. Headless stdout: $HeadlessStdout"
    }

    if (-not (Select-String -Path $EchoLog -Pattern '\b41\b' -Quiet)) {
        throw "Echo server did not record the transmitted 41 byte. Echo log: $EchoLog"
    }

    $Success = $true
    Write-Host '==> SwiftLink smoke test passed'
}
finally {
    if (Test-Path $RemoteDll) {
        try {
            Invoke-RemoteCommand @('emu.quit') | Out-Null
        }
        catch {
        }
    }
    if ($HeadlessProcess -and -not $HeadlessProcess.HasExited) {
        Stop-Process -Id $HeadlessProcess.Id -Force
    }
    if ($EchoProcess -and -not $EchoProcess.HasExited) {
        Stop-Process -Id $EchoProcess.Id -Force
    }
    if ($HadAppSettings) {
        Copy-Item $AppSettingsBackup $AppSettingsPath -Force
    }
    else {
        Remove-Item $AppSettingsPath -ErrorAction SilentlyContinue
    }
    if ($Success) {
        Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    else {
        Write-Warning "Smoke artifacts kept at $TempDir"
    }
}
