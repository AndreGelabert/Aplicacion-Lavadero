# Clientes refactor
Objective: preserve observable Clientes behavior while consolidating duplicated listing/count filtering.
Delivery: local only; audit writer saved separately in commit 88f523b on the current integration line. No changes to keys, transactions, permissions, lifecycle mutations or audit emissions.

## Specs
S1. "Ignora esa prueba y segui con el proximo modulo planificado" — proceed with Clientes; audit independent execution remains unavailable, not passed.
S2. "Siempre valora mas el codigo, o no sigas super a raja tabla la documentacion" — preserve existing service filters, sorting, paging and failure behavior.
S3. "muchas se hicieron en fases tempranas de desarrollo y despues no se actualizaron mas" — characterize production service calls instead of relying on test-local logic.
S4. "Ramas encadenadas hacia la rama principal" — local work only, no publishing.

S5. "Y segui si" — continue with the identified Index/SearchPartial duplication, preserving existing routes, permission guards, models and ViewBag metadata.

S6. "Bueno, hace todo eso mas la eliminacion de codigo duplicado, no es uno u otro." — separate business responsibilities and remove duplication, not only listing preparation.
S7. "Si, asi que avanza con eso en ese orden por favor" — proceed Audit, Clientes, Vehiculos, Lavados.

## Tasks
T1 | S1-S3 | inline | done | Public service characterization and isolated baseline passed: 851 passed, 0 failed/skipped (job-9, exit 0). Commit: 10219c4.
T2 | S2-S3 | inline | done | Exact predicates/materialization retained; baseline and post-change isolated suites both 851 passed, 0 failed/skipped (job-9/job-10, exit 0). Commit: 10219c4.
T3 | S1-S4 | inline | done | Both suites 851/851; diff check clean; native assessment unavailable, no independent verifier required by returned plan. Local implementation commit: 10219c4.

T4 | S2-S3,S5 | inline | done | Controller extraction preserves signatures and ViewBag differences. Baseline/post-change 852 passed, zero failed/skipped (job-11/job-12). Commit: f982100.
T5 | S2-S5 | inline | done | 852/852 before and after; local MVC slice f982100 excludes audit constructor/field edits.

T6 | S2-S3,S6-S7 | worker + parent checks | done | Characterize lifecycle cascades then extract concrete lifecycle coordination preserving audit order, state/ownership semantics and HTTP guards. Base a283cb8. Commit: pending.
T7 | S2-S3,S6-S7 | worker + independent verification if risk triggered | pending | Evaluate/create-update association orchestration and duplication, preserve keys/transaction/guards, verify responsibility outcome before closure. Commit: pending.

## Log
L1. User authorization verbatim: "Ignora esa prueba y segui con el proximo modulo planificado"
L2. Explorer mw46epny-5-wkbo identified duplicate filters Services/ClienteService.cs:22-108 and duplicate MVC metadata. First bounded unit is service filtering only. Controller consolidation is deferred to avoid mixing with unresolved audit writer edits. No new product behavior or architecture layers.
L3. Audit native review review-10210b6ea819cb83 approved and acknowledged. Independent audit executable verification timed out, remains unavailable and is not replaced by this work. Existing parent checks: 850 .NET and 10 browser fixtures passed. Audit delivery not declared fully verified.

L4. Post-change job-10 exit 0: 851 passed, 0 failed/skipped; same emulator Connection reset warning as baseline. No behavior change means no meaningful RED; characterization brackets exact extraction. No UI, CLI, help or public signature changed. Risk: none (both listing/count explicitly in this unit; private helper only; no security/persistence changes). Native assessment unavailable due to untracked declaration; no native approval claimed.
L5. Local slice 10219c4 on refactor/clientes-queries includes exactly service/test/task document (123 authored changed lines). Branch ancestry contains earlier query slice f302ec6; review this unit relative to f302ec6, not accumulated origin/main range. Audit writer changes remain uncommitted, including its two staged new files; no publishing.
L6. User authorization verbatim: "Y segui si". Controller already contains uncommitted audit dependency change; preserve it and exclude it from Clientes delivery.
L7. MVC baseline job-11 exit 0: 852 passed, 0 failed/skipped. Private PrepararListado extraction implemented; retains query order, permission-dependent Index reads, total-page math and ViewBag keys; PageSize remains Index-only. Post-change checks pending.
L8. Post-change job-12 exit 0: 852 passed, zero failed/skipped. ASSESS unavailable: untracked declaration error; returned plan self-verification/no independent verifier. No native approval claimed. Risk: none, private extraction of explicitly scoped actions with unchanged permission checks. No CLI/UI contract or help change.
L9. Commit f982100 contains only MVC extraction, its public-action characterization and tracking. Temporary index used to exclude the two pre-existing IAuditWriter lines; normalized candidate line endings before commit after diff-check rejected initial CRLF blob. Worktree bytes preserved. Remaining controller diff verified to be exactly the two audit dependency lines; staged audit new files preserved. No publishing.
L10. User authorized organizing pending local commits and personal files. Audit writer saved separately as 88f523b after client slices 10219c4/f982100; no history rewrite. Current authoritative work line: refactor/clientes-queries. Earlier audit branches are historical pointers only. Slice boundaries: f302ec6 audit queries; 10219c4 client filters; f982100 MVC listing; 88f523b audit writer. Review individual slices, not accumulated main range.

L11. User correction: "Bueno, hace todo eso mas la eliminacion de codigo duplicado, no es uno u otro." Authorization: "Si, asi que avanza con eso en ese orden por favor". Earlier completed query units not full responsibility closure. Explorer mw50hl7g-c-3379 identified lifecycle cascades and create/update preparation. Start cascades; preserve exact reads, per-vehicle write/audit order then client write/audit, partial failure behavior; do not introduce transactions or security/key changes. Audit read unit d64b7c2/a283cb8 passed857tests; historical audit writer verifier unavailable unchanged.

L12. Baseline job-5 exit0:858passed0failed0skipped. Worker mw50nidz-e-5x4i returned partial without changes; parent mechanically extracted lifecycle loops to ClienteLifecycleService, injected scoped dependency and adapted direct integration-test constructors. HTTP try/catch/attributes retained; actor values explicitly passed; per-vehicle state+audit then client state+audit order unchanged. No owner/key/transaction logic modified; post-change job-6 running. Creation/update orchestration and duplicated active-owner lookup evaluation remain pending.

L13. Post-extraction job-6 exit1 compilation: missing service imports and target-typed controller factories not adapted. No tests executed. Wiring repaired; job-7 exit0:858 passed/0failed/0skipped. Shared TieneOtrosClientesActivos extracted, preserving ordered reads/principal fallback and reactivation discarded result (no new eligibility rule). Same suite job-8 running. No commit/closure yet.

L14. Final lifecycle shared-lookup job-8 exit0:858passed0failed0skipped. Native ASSESS unavailable due untracked scope, returned writer self-verification/no independent verifier; no approval claimed. Risk: none for mechanical lifecycle move and same owner lookup without guard/eligibility/key/transaction changes. Existing state writes and ordered audits retained; no persistence format or irreversible rewrite. Save this cohesive lifecycle slice before association work; base a283cb8. Creation/update and related selection query coordination still pending under T7.
