# Local Firebase emulators

The project-local Windows launcher runs Firestore and Authentication against the isolated `demo-local` project. It does not import a remote Firebase database.

## Start and seed

1. Start `.local\firebase-emulator\start-firestore.cmd` and leave it running.
2. In another PowerShell terminal, set a local-only password in `LOCAL_EMULATOR_PASSWORD` before the first seed, then run `.local\firebase-emulator\seed.cmd`. The seed does not print or store the password in project files, and rerunning it does not reset an existing user. Remove the environment variable afterward.
3. Run `.local\firebase-emulator\start-app.cmd` and open `http://127.0.0.1:5200`.

For the first seed, PowerShell can read the password without echoing it:

```powershell
$secret = Read-Host 'Local-only demo password' -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try { $env:LOCAL_EMULATOR_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
.\.local\firebase-emulator\seed.cmd
Remove-Item Env:LOCAL_EMULATOR_PASSWORD
```

These launchers and the emulator installation reside in `.local`, which Git ignores. They are specific to this workstation and are not included in a clone.

## Isolation and persistence

- Firestore listens on `127.0.0.1:8189`, Auth on `127.0.0.1:9099`. The application requires both settings together in Development; partial, non-loopback, and production configurations fail closed.
- The local launcher imports `seed-data` only when Firebase's export manifest is present and exports on a clean shutdown. A pre-existing directory without a manifest is not overwritten. Keep separate backups before changing or deleting snapshots. An abrupt termination may not export the latest changes.
- The seed uses create-if-absent writes and preserves existing records and users. It contains fictional example data and does not import a remote database.
- Google sign-in is disabled in local emulator mode. The synthetic password is only for the local Auth emulator; never reuse it elsewhere.
- The application has other integrations and browser resources beyond Firebase. Emulator mode alone is not a general-purpose network sandbox.

## Tests

For Firestore integration tests, set `FIRESTORE_EMULATOR_HOST=127.0.0.1:8189` in the test process and run `dotnet test Firebase.Tests`. Existing integration tests use their own random demo project IDs. Do not point tests at a production Firestore instance.
