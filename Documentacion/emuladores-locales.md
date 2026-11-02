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

Run the suite from the repository root with an isolated, disposable Firestore emulator:

```powershell
powershell.exe -NoProfile -File .\Firebase.Tests\run-isolated.ps1
```

The runner requires Node on PATH, the existing project-local Java and Firestore jar, and `.local/firebase-emulator/npm/node_modules/firebase-tools` including its Auth emulator. It starts disposable Firestore on `127.0.0.1:8190` and Auth on `127.0.0.1:9199`, without import/export, runs the full `dotnet test` suite, restores environment variables and stops only its own processes. It refuses occupied ports. Optional `-JavaPath` and `-EmulatorJar` arguments select an existing installation relative to the repository root. It never installs dependencies or deletes snapshots.

Do not run empty-database integration tests against the application's persistent emulator on port 8189: imported seed data can appear even under random demo project IDs. Random IDs alone do not isolate tests from imported snapshots. The application's emulator and data remain untouched by the runner.

The runner opts into the disposable seed and email/password login smoke test. It creates a random password in test memory, seeds twice, checks preservation, and exercises successful and incorrect-password authentication. The test temporarily sets the application's guard values while its Firestore client is already bound to 8190 and its fail-closed Auth transport routes only 9099 requests to 9199. Outside the runner it is skipped; an opted-in run with incorrect endpoints fails. Never use production credentials or point tests at production Firestore.

For future authentication features, reuse the runner, isolated xUnit collection and test-only transport; add tests against the disposable services and keep credentials transient. Do not route requests to the application's persistent emulators. This exercises emulated email authentication, not a real Google OAuth flow; Google sign-in remains disabled in local mode.
