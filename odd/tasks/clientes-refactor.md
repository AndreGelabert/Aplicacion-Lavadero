# Clientes refactor
Objective: preserve observable Clientes behavior while consolidating duplicated listing/count filtering.
Delivery: local only; audit writer remains uncommitted and separate. No changes to keys, transactions, permissions, lifecycle mutations or audit emissions.

## Specs
S1. "Ignora esa prueba y segui con el proximo modulo planificado" — proceed with Clientes; audit independent execution remains unavailable, not passed.
S2. "Siempre valora mas el codigo, o no sigas super a raja tabla la documentacion" — preserve existing service filters, sorting, paging and failure behavior.
S3. "muchas se hicieron en fases tempranas de desarrollo y despues no se actualizaron mas" — characterize production service calls instead of relying on test-local logic.
S4. "Ramas encadenadas hacia la rama principal" — local work only, no publishing.

## Tasks
T1 | S1-S3 | inline | done | Public service characterization and isolated baseline passed: 851 passed, 0 failed/skipped (job-9, exit 0). Commit: pending.
T2 | S2-S3 | inline | done | Exact predicates/materialization retained; baseline and post-change isolated suites both 851 passed, 0 failed/skipped (job-9/job-10, exit 0). Commit: pending.
T3 | S1-S4 | inline | in_progress | Suite passed; native ASSESS unassessable due to unrelated untracked declaration, plan medium/self-verification, no independent verifier. Preparing separate local unit commit.

## Log
L1. User authorization verbatim: "Ignora esa prueba y segui con el proximo modulo planificado"
L2. Explorer mw46epny-5-wkbo identified duplicate filters Services/ClienteService.cs:22-108 and duplicate MVC metadata. First bounded unit is service filtering only. Controller consolidation is deferred to avoid mixing with unresolved audit writer edits. No new product behavior or architecture layers.
L3. Audit native review review-10210b6ea819cb83 approved and acknowledged. Independent audit executable verification timed out, remains unavailable and is not replaced by this work. Existing parent checks: 850 .NET and 10 browser fixtures passed. Audit delivery not declared fully verified.

L4. Post-change job-10 exit 0: 851 passed, 0 failed/skipped; same emulator Connection reset warning as baseline. No behavior change means no meaningful RED; characterization brackets exact extraction. No UI, CLI, help or public signature changed. Risk: none (both listing/count explicitly in this unit; private helper only; no security/persistence changes). Native assessment unavailable due to untracked declaration; no native approval claimed.
