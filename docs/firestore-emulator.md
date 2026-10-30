# Run isolated Firestore integration tests

The project-local Firestore emulator is installed under the ignored directory
`.local/firebase-emulator/`. It uses a demo project and does not need a Firebase
account. It listens on `127.0.0.1:8189` when started by the supplied launcher.

## Start and test on Windows

1. Run `.local\firebase-emulator\start-firestore.cmd` in a terminal and wait for
   the emulator to report that it is ready. Leave that terminal open.
2. In another PowerShell terminal, run:

   ```powershell
   $env:FIRESTORE_EMULATOR_HOST = '127.0.0.1:8189'
   dotnet test Firebase.Tests/Firebase.Tests.csproj
   ```

The integration tests create unique `demo-` projects and use `EmulatorOnly`;
without a loopback emulator address they are skipped. WhatsApp requests in these
tests are intercepted in memory and are not sent to Meta. Stop the emulator
with Ctrl+C in its terminal. Emulator data is temporary unless explicitly
exported.

The emulator's default rules allow local reads and writes; it must not be exposed
to a network. Setting `FIRESTORE_EMULATOR_HOST` for the test command does **not**
guarantee that the application itself uses the emulator: do not run the normal
application against production credentials as part of these tests.
