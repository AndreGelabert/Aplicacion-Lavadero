param(
    [int]$Port = 8190,
    [string]$JavaPath = '.local/firebase-emulator/jdk/jdk-21.0.13.0.2+1/bin/java.exe',
    [string]$EmulatorJar = '.local/firebase-emulator/home/.cache/firebase/emulators/cloud-firestore-emulator-v1.22.0.jar'
)

# Use a disposable emulator, never the application's imported snapshot.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ($Port -ne 8190) { throw 'The isolated suite requires Firestore port 8190.' }
$authPort = 9199
$node = Get-Command node.exe -ErrorAction Stop
$authHelper = Join-Path $PSScriptRoot 'Helpers/firebase-auth-emulator.cjs'
$authModule = Join-Path $root '.local/firebase-emulator/npm/node_modules/firebase-tools/lib/emulator/auth/index.js'
if (!(Test-Path $authModule)) { throw 'Local firebase-tools Auth emulator installation missing; no installation is performed.' }
$java = Join-Path $root $JavaPath
$jar = Join-Path $root $EmulatorJar
if (!(Test-Path $java) -or !(Test-Path $jar)) {
    throw 'Local Java/emulator installation missing. Supply -JavaPath and -EmulatorJar; no installation is performed.'
}
foreach ($checkPort in @($Port, $authPort)) {
    $probe = New-Object Net.Sockets.TcpClient
    try {
        try { $probe.Connect('127.0.0.1', $checkPort); $occupied = $true } catch { $occupied = $false }
    } finally { $probe.Dispose() }
    if ($occupied) { throw "Port $checkPort is occupied. Refusing to reuse or stop another emulator." }
}

$previousHost = $env:FIRESTORE_EMULATOR_HOST
$previousAuthHost = $env:FIREBASE_AUTH_EMULATOR_HOST
$previousOptIn = $env:ISOLATED_FIREBASE_TESTS
$emulator = $null
$authEmulator = $null
$exitCode = 1
try {
    $emulator = Start-Process -FilePath $java -ArgumentList @(
        '-jar', ('"' + $jar + '"'), '--host=127.0.0.1', "--port=$Port",
        '--project_id=demo-suite', '--single_project_mode=false'
    ) -PassThru -NoNewWindow
    $authEmulator = Start-Process -FilePath $node.Source -ArgumentList @(('"' + $authHelper + '"')) -PassThru -NoNewWindow
    $deadline = (Get-Date).AddSeconds(60)
    do {
        if ($emulator.HasExited) { throw "Emulator exited with code $($emulator.ExitCode)." }
        $probe = New-Object Net.Sockets.TcpClient
        try {
            try { $probe.Connect('127.0.0.1', $Port); $ready = $true } catch { $ready = $false }
        } finally { $probe.Dispose() }
        if (!$ready) { Start-Sleep -Milliseconds 250 }
    } until ($ready -or (Get-Date) -gt $deadline)
    if (!$ready) { throw 'Emulator did not become ready within 60 seconds.' }
    $deadline = (Get-Date).AddSeconds(60)
    do {
        if ($authEmulator.HasExited) { throw "Auth emulator exited with code $($authEmulator.ExitCode)." }
        $probe = New-Object Net.Sockets.TcpClient
        try {
            try { $probe.Connect('127.0.0.1', $authPort); $ready = $true } catch { $ready = $false }
        } finally { $probe.Dispose() }
        if (!$ready) { Start-Sleep -Milliseconds 250 }
    } until ($ready -or (Get-Date) -gt $deadline)
    if (!$ready) { throw 'Auth emulator did not become ready within 60 seconds.' }
    $env:FIRESTORE_EMULATOR_HOST = "127.0.0.1:$Port"
    $env:FIREBASE_AUTH_EMULATOR_HOST = "127.0.0.1:$authPort"
    $env:ISOLATED_FIREBASE_TESTS = '1'
    & dotnet test (Join-Path $PSScriptRoot 'Firebase.Tests.csproj') --verbosity minimal
    $exitCode = $LASTEXITCODE
} finally {
    $env:FIRESTORE_EMULATOR_HOST = $previousHost
    $env:FIREBASE_AUTH_EMULATOR_HOST = $previousAuthHost
    $env:ISOLATED_FIREBASE_TESTS = $previousOptIn
    if ($null -ne $authEmulator -and !$authEmulator.HasExited) {
        Stop-Process -Id $authEmulator.Id -ErrorAction Stop
    }
    if ($null -ne $emulator -and !$emulator.HasExited) {
        Stop-Process -Id $emulator.Id -ErrorAction Stop
    }
}
exit $exitCode
