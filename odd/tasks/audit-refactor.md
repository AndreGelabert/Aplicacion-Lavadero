# Audit refactor
Objective: preserve current behavior while improving audit queries/presentation and transversal emission. Baseline: 901502d7959cc84632cc01d87a21df5b0cb55bba; default branch verified: origin/main.
Delivery: stacked-to-main, local branches only; no publishing. Runners: dotnet test; Firebase.Tests/run-isolated.ps1; node scripts/playwright.cjs test tests/browser/auditoria.spec.cjs.

## Specs
S1. "Bueno empeza con el mega plan recomendado por apartados empezando con audtioria y sus dos partes" — begin with audit query/presentation and transversal recording, preserving the current code's externally observable behavior.
S2. "Siempre valora mas el codigo, o no sigas super a raja tabla la documentacion" — existing implementation is authoritative; historical documents do not authorize behavior changes.
S3. "muchas se hicieron en fases tempranas de desarrollo y despues no se actualizaron mas" — inspect existing test assertions before relying on them; characterize actual production paths.
S4. Delivery selection: "Ramas encadenadas hacia la rama principal" (stacked-to-main). No push, PR creation or merge authorized.

## Tasks
T1 | S1-S3 | inline | done | Baseline 844 passed, 0 skipped; browser fixture 10 passed. New behavioral characterization passed; typed-model contract RED: expected AuditoriaViewModel, actual List`1 (845 passed, 1 failed). Commit: pending.
T2 | S1-S3 | inline | done | Typed audit filter/view model; screen ViewBag removed; shared TargetName pipeline. GREEN: 846 passed, 0 skipped; compiled Razor rendering 1 passed. Commit: pending.
T3 | S1-S3 | inline | in_progress | Introduce purpose-built audit writer contract, preserve persistence and switch actual emission consumers with tests. Commit: pending.
T4 | S1-S4 | inline + independent verify (Risk item 3: shared audit emission contract and consumers) | pending | Functional checks, differential audit checks, independent verification, risk-gated native assessment/review, local work-unit commits. Commit: pending.

## Log
L1. User authorization verbatim: "Bueno empeza con el mega plan recomendado por apartados empezando con audtioria y sus dos partes"
L2. User constraint verbatim: "Si, pero cuidado que tanto tomas de la documentacion, o sea, se que esta bien, pero no es exactamente como funciona el sistema ya que la docu la hize despues, asi que puede no coincidir comportamientos, nombres de variables, flujos de accion, etc. Siempre valora mas el codigo, o no sigas super a raja tabla la documentacion"
L3. Test context verbatim: "Buena identificacion de necesidades por vista. El tema de las pruebas que mencionas debes tener en cuenta que muchas se hicieron en fases tempranas de desarrollo y despues no se actualizaron mas, asi que esa podria ser una causa de las inconsistencias en pruebas. Por otra parte, por que no tocaría la escritura transversal de auditoría ni introduciría una interfaz nueva en todos sus consumidores?"
L4. Scope: audit only. No Blazor, generic repositories, MediatR, global namespace or credential changes; no unrelated Logout changes. Detail JSON, query names, DOM IDs, permissions, historical names, event payloads and failure behavior must remain compatible. Existing untracked .impeccable/, .pi/, DESIGN.md, PRODUCT.md and nul are unrelated and must remain untouched.
L5. Forecast: initial explorer estimate 400–560 authored changed lines, likely higher with meaningful integration tests and typed Razor substitutions; split by cohesive unit, never shrink code to hit a line cap. Query and writer units are dependent, so implementation remains inline.
L7. Query checks: baseline 844/844; RED 845 passed + 1 intentional typed-model failure; GREEN 846/846; compiled Razor partial render 1/1; browser fixture baseline 10/10. Compiler emitted pre-existing nullable/analyzer warnings. Initial assess could not assess ambient untracked scope; assess committed-only slice after commit. No review closure claimed.
L6. RDD on. New review entry follows current mirrored contract: assess completed candidate; inspect/start only if reviewDue or explicitly requested. Default branch is main (refs/remotes/origin/HEAD). Delivery selection grants shape only, not publishing.
