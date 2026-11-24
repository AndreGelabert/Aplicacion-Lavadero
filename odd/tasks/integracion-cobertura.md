# Integración y cobertura global
Objective: add the missing integration proof for wash payments/pickup and calculate a repository-wide measured coverage percentage without touching real services or external providers.
Work line: feat/turnos

## Specs
S1. "te autorizo a todas esas pruebas" - Add integration coverage for partial/multiple payments, pickup eligibility, idempotence, and the existing client/vehicle/wash/stock/providence boundaries named in the prior audit.
S2. "me gustaria que modificaras la documentacion de testing para definir un porcentaje global de cobertura, es decir que calcules ese porcentaje" - Execute isolated test coverage with emulators when the environment permits and report an observed, reproducible global percentage; do not invent a percentage or claim production coverage.
S3. "No modifiques archivos" from the prior audit is superseded by this authorization only for tests and testing documentation; preserve unrelated dirty work, especially Turnos.
S4. The result must distinguish checked, inferred, skipped, and unavailable evidence. External email, WhatsApp, and browser fixture transports must remain mocked or locally disposable.

## Tasks
T1 | S1,S3,S4 | inline + isolated emulator | done | Partial/multiple payment and full-payment pickup integration characterized and verified 4/4 with AuditWashCaptureTests.
T2 | S1,S3,S4 | inline + focused suite | partial | Added HTTP payment/pickup coverage to LavadoInsumosHttpWorkflowIntegrationTests; wash HTTP and client/vehicle focal suites pass, while quotation HTTP still has an existing UI expectation failure.
T3 | S2,S3,S4 | inline + coverage runner | partial | XPlat coverage measured at 69.27% lines and 57.49% branches; latest full suite had 1465 passed and 34 failed, so coverage is observed but not an approval gate.

## Log
L1. User verbatim: "Bueno, te autorizo a todas esas pruebas y me gustaria que modificaras la documentacion de testing para definir un porcentaje global de cobertura, es decir que calcules ese porcentaje"
L2. Added PartialAndMultiplePaymentsPersistStateAndAllowPickupAfterFullPayment to AuditWashCaptureTests.cs. Isolated focal run 2026-11-19: 4/4 passed, 0 failed, 0 skipped.
L3. Extended run-isolated.ps1 with TestFilter and AdditionalArguments for focused and coverage runs. No emulator/configuration change outside the existing disposable ports.
L4. Full isolated .NET coverage run 2026-11-19 generated 68.95% line coverage and 56.76% branch coverage from coverage.cobertura.xml. Suite result was partial: 1427 passed, 54 failed, 0 skipped, 1481 total. Failures include disposable-emulator fixture mismatches, emulator project-mode warnings, and transaction lock timeout evidence; no global pass claim.
L5. Corrected WhatsApp operation HTTP fixtures, official emulator ports, LocalEmulatorSettings, RBAC/navigation counts, and audit configuration fields. Later full coverage run: 1465 passed, 34 failed, 0 skipped, 1499 total; coverage 69.27% lines and 57.49% branches.
L6. Seed and wash HTTP focal verification passed 5/5. Remaining 34 failures are not yet declared resolved.
L7. Reabastecimiento integration class verified 17/17 against disposable Firestore Emulator after provider fixtures were completed with valid contact and email fields.
L8. Continued integration emphasis: insumo catalog/HTTP rerun is blocked during project compilation by unintegrated Turnos changes in Firebase.Tests/Services/TurnoSnapshotBuilderTests.cs (missing Turno/Entrada properties); no edits were made to that other-agent work.
L9. User authorization: "bueno mi foco es seguir metiendo pruebas de integracion, asi que te autorizo el plan, cuidado nomas con el trabajo pendiente de turnos". Continue only with integration-test files and isolated disposable emulators; preserve all unrelated Turnos changes and avoid shared files unless necessary.
L10. Added PagoYRetiroHttpPersistenEstadoYRechazanRetiroSinSaldoCancelado to LavadoInsumosHttpWorkflowIntegrationTests.cs. Isolated run passed 1/1; the complete class passed 5/5. ClienteAssociationRefactorTests passed 3/3. CotizacionesHttpIntegrationTests remains partial: 1/2 passed because the page no longer contains the test's expected "Crear solicitud" text.
