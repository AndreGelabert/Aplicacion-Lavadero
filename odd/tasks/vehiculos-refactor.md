# Vehiculos query refactor
Objective: consolidate duplicated vehicle listing/count filters while preserving observable behavior.
Work line: refactor/clientes-queries, base 39cc04c. Local only; no publishing, new branches or ownership/key/persistence changes.

## Specs
S1. "Si, continua con el proximo modulo" — proceed with the next bounded module consolidation; no strict post-Clientes order was recorded, Vehiculos selected from live-code duplication.
S2. "Siempre valora mas el codigo, o no sigas super a raja tabla la documentacion" — preserve state/text/type/brand/color predicates, live owner-name hydration, ordering, pagination and failure behavior.
S3. "muchas se hicieron en fases tempranas de desarrollo y despues no se actualizaron mas" — characterize public service calls against disposable Firestore, not test-local filtering.

S4. "Bien, segui con lo que falte y/o el siguiente modulo a refactorizar" — finish Vehiculos MVC listing preparation while preserving routes, permission early return and ViewBag differences.

## Tasks
T1 | S1-S3 | inline | done | Baseline isolated job-1 exit 0: 853 passed, zero failed/skipped. Commit: 38ad981.
T2 | S2-S3 | worker + parent checks | done | Private FiltrarVehiculos extracted; parent diff inspected. Baseline/post-change isolated suites: 853 passed, zero failed/skipped, exit 0. Commit: 38ad981.
T3 | S1-S3 | inline | done | Post-change job-2 passed; ASSESS unavailable due to untracked declaration, returned self-verification/no independent verifier. Local slice saved as 38ad981. Commit: 38ad981.

T4 | S2-S4 | worker + parent checks | done | Public MVC characterization baseline job-3 exit 0: 854 passed, zero failed/skipped. Commit: pending.
T5 | S2-S4 | worker + parent checks | in_progress | Private PrepararListado extracted, parent inspected diff, diff-check passed. Post-change job-4 exit 0: 854 passed, zero failed/skipped. Native assessment medium, reviewDue false (under_budget), writer self-verification only. Commit: pending.

## Log
L1. User authorization verbatim: "Si, continua con el proximo modulo"
L2. Explorer mw4920fp-2-xhd1 found no strict next-module roadmap. VehiculoService listing/count duplicates predicates; narrow first unit Services/VehiculoService.cs and Firebase.Tests/Integration/VehiculoQueryRefactorTests.cs. MVC preparation remains outside this unit. No key/ownership/auth/transaction changes.

L3. Worker mw496w0h-3-ofyh edited only Services/VehiculoService.cs; parent inspected full diff. Hydration remains before filtering in both callers. No meaningful RED for pure extraction; public characterization brackets change. Baseline 853 passed. Post-change pending.
L4. Post-change job-2 exit 0: 853 passed, zero failed/skipped. Connection reset emulator warning also present before extraction; suite successful. No UI or CLI contract changes. Risk: none (private exact filter extraction; listing/count both in scope; owner hydration, keys, transactions and persistence unchanged). Slice base 39cc04c; do not review accumulated branch as this unit.
L5. Implementation slice 38ad981 contains exactly service extraction, characterization and tracking (149 authored diff lines). Local branch refactor/clientes-queries preserved; no publishing or branch changes. Native approval not claimed.

L6. User verbatim: "Bien, segui con lo que falte y/o el siguiente modulo a refactorizar". Explorer mw49np6l-4-jqkp confirms Index early return without listing metadata when consultation permission absent; do not copy Clientes behavior. Native accumulated review review-276410af4a8958d3 approved and acknowledged for prior candidate through d25b052; this new unit not included.
L7. Characterization worker mw49qa9j-5-0bnd edited only test file; extraction worker mw49u0jd-6-nqz7 edited only controller. Baseline 854/854; no meaningful RED for exact behavior-preserving extraction. No permission check moved or changed; Index-only values remain outside helper.
L8. MVC post-change job-4 exit 0: 854 passed, zero failed/skipped, matching baseline. Native ASSESS succeeded: medium, 3 paths/144 changed lines, reviewDue false under_budget; no independent verifier required. No native approval claimed for this unit. Slice base d25b052; commit only controller/test/tracking, not accumulated branch. Risk: none (private extraction of scoped Index/SearchPartial; existing guard, routes and signatures unchanged, no persistence/auth logic modified). No UI markup or CLI/help changes.
