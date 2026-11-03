# Disposable emulators: never import/export or reuse the developer snapshot.
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
foreach ($port in @(8189,9099,5200)) {
    $probe = New-Object Net.Sockets.TcpClient
    try { try { $probe.Connect('127.0.0.1',$port); $busy=$true } catch { $busy=$false } } finally { $probe.Dispose() }
    if ($busy) { throw "Port $port occupied; refusing to reuse an existing service." }
}
$jobs = @()
$names = @('ASPNETCORE_ENVIRONMENT','FIRESTORE_EMULATOR_HOST','FIREBASE_AUTH_EMULATOR_HOST','LOCAL_EMULATOR_PASSWORD','ISOLATED_NOTIFICATION_TESTS')
$before = @{}
foreach ($name in $names) { $before[$name] = [Environment]::GetEnvironmentVariable($name) }
function Wait-Port($port) {
    $deadline = (Get-Date).AddSeconds(60)
    do {
        foreach ($job in $jobs) { if ($job.HasExited) { throw "Service process $($job.Id) exited with code $($job.ExitCode). See .local/notification-proof logs." } }
        $probe = New-Object Net.Sockets.TcpClient
        try { try { $probe.Connect('127.0.0.1',$port); return } catch {} } finally { $probe.Dispose() }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    throw "Service on port $port not ready within 60 seconds."
}
try {
    New-Item -ItemType Directory -Force '.local/notification-proof' | Out-Null
    if (!(Test-Path '.local/notification-proof/flowbite.min.js')) {
        Invoke-WebRequest 'https://cdn.jsdelivr.net/npm/flowbite@2.5.2/dist/flowbite.min.js' -OutFile '.local/notification-proof/flowbite.min.js' -UseBasicParsing -TimeoutSec 60
    }
    $java = (Resolve-Path '.local/firebase-emulator/jdk/jdk-21.0.13.0.2+1/bin/java.exe').Path
    $jar = (Resolve-Path '.local/firebase-emulator/home/.cache/firebase/emulators/cloud-firestore-emulator-v1.22.0.jar').Path
    $jobs += Start-Process $java -ArgumentList @('-jar',('"'+$jar+'"'),'--host=127.0.0.1','--port=8189','--project_id=demo-local') -PassThru -RedirectStandardOutput '.local/notification-proof/firestore.log' -RedirectStandardError '.local/notification-proof/firestore-error.log'
    $auth = "const {AuthEmulator}=require('./.local/firebase-emulator/npm/node_modules/firebase-tools/lib/emulator/auth/index.js');new AuthEmulator({host:'127.0.0.1',port:9099,projectId:'demo-local'}).start().catch(e=>{console.error(e);process.exit(1)});"
    [IO.File]::WriteAllText((Join-Path (Get-Location) '.local/notification-proof/auth.cjs'),$auth.Replace("require('./.local/", "require('../"))
    $jobs += Start-Process node.exe -ArgumentList '.local/notification-proof/auth.cjs' -PassThru -RedirectStandardOutput '.local/notification-proof/auth.log' -RedirectStandardError '.local/notification-proof/auth-error.log'
    Wait-Port 8189
    Wait-Port 9099
    $env:ASPNETCORE_ENVIRONMENT='Development'
    $env:FIRESTORE_EMULATOR_HOST='127.0.0.1:8189'
    $env:FIREBASE_AUTH_EMULATOR_HOST='127.0.0.1:9099'
    $env:LOCAL_EMULATOR_PASSWORD=[Guid]::NewGuid().ToString('N')
    $env:ISOLATED_NOTIFICATION_TESTS='1'
    & dotnet run --no-build --no-launch-profile --project Firebase.csproj -- --seed-local-emulators
    if ($LASTEXITCODE -ne 0) { throw 'Seed failed' }
    $jobs += Start-Process dotnet.exe -ArgumentList @('run','--no-build','--no-launch-profile','--project','Firebase.csproj','--urls','http://127.0.0.1:5200') -PassThru -RedirectStandardOutput '.local/notification-proof/app.log' -RedirectStandardError '.local/notification-proof/app-error.log'
    Wait-Port 5200
    & node scripts/authenticated-notifications.cjs
    $result=$LASTEXITCODE
} finally {
    foreach ($job in $jobs) { if (!$job.HasExited) { & taskkill.exe /PID $job.Id /T /F | Out-Null } }
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name,$before[$name]) }
}
exit $result
