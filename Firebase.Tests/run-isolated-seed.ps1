$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$java = Join-Path $root '.local/firebase-emulator/jdk/jdk-21.0.13.0.2+1/bin/java.exe'
$jar = Join-Path $root '.local/firebase-emulator/home/.cache/firebase/emulators/cloud-firestore-emulator-v1.22.0.jar'
$module = Join-Path $root '.local/firebase-emulator/npm/node_modules/firebase-tools/lib/emulator/auth/index.js'
$node = (Get-Command node.exe -ErrorAction Stop).Source
if (!(Test-Path $java) -or !(Test-Path $jar) -or !(Test-Path $module)) { throw 'Local emulator dependencies missing; no installation performed.' }
foreach ($port in @(8189, 9099)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Port $port occupied; refusing to reuse or stop an emulator." }
}
$names = @('FIRESTORE_EMULATOR_HOST', 'FIREBASE_AUTH_EMULATOR_HOST', 'ISOLATED_FIREBASE_TESTS', 'LOCAL_EMULATOR_PASSWORD')
$previous = @{}; foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$firestore = $null; $auth = $null; $exitCode = 1
try {
    $firestore = Start-Process -FilePath $java -ArgumentList @('-jar', ('"' + $jar + '"'), '--host=127.0.0.1', '--port=8189', '--project_id=demo-local') -PassThru -NoNewWindow
    $js = "const {AuthEmulator}=require(process.argv[1]); new AuthEmulator({host:'127.0.0.1',port:9099,projectId:'demo-local'}).start().catch(e=>{console.error(e);process.exit(1)});"
    $auth = Start-Process -FilePath $node -ArgumentList @('-e', ('"' + $js + '"'), ('"' + $module + '"')) -PassThru -NoNewWindow
    foreach ($entry in @(@(8189,$firestore), @(9099,$auth))) {
        $port = $entry[0]; $process = $entry[1]; $deadline = (Get-Date).AddSeconds(60); $owned = $false
        do {
            $process.Refresh(); if ($process.HasExited) { throw "Emulator process $($process.Id) exited before readiness." }
            $listeners = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
            if ($listeners.Count -gt 0) {
                if (@($listeners | Where-Object { $_.OwningProcess -ne $process.Id }).Count -gt 0) { throw "Port $port is not exclusively owned by our emulator." }
                $owned = $true
            } else { Start-Sleep -Milliseconds 250 }
        } until ($owned -or (Get-Date) -gt $deadline)
        if (!$owned) { throw "Emulator on port $port did not become ready." }
    }
    $env:FIRESTORE_EMULATOR_HOST = '127.0.0.1:8189'
    $env:FIREBASE_AUTH_EMULATOR_HOST = '127.0.0.1:9099'
    $env:ISOLATED_FIREBASE_TESTS = '1'
    $env:LOCAL_EMULATOR_PASSWORD = 'disposable-test-password'
    & dotnet test (Join-Path $PSScriptRoot 'Firebase.Tests.csproj') --filter FullyQualifiedName~LocalEmulatorSeedIntegrationTests --verbosity minimal
    $exitCode = $LASTEXITCODE
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    foreach ($process in @($auth, $firestore)) { if ($null -ne $process) { $process.Refresh(); if (!$process.HasExited) { Stop-Process -InputObject $process -ErrorAction Stop; $process.WaitForExit(10000) | Out-Null } } }
}
exit $exitCode
