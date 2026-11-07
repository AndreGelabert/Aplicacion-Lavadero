# Servicio MVC refactor — first bounded unit
Branch: `refactor/clientes-queries`; scope: partial-table presentation metadata only. Preserve current observable behavior.
Delivery: isolated local work-unit commit based on `298a8b16add7dc8fb581f38bea155f4a6a456ec6`; forecast ~180 authored changed lines, no push/PR. Runner: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Firebase.Tests/run-isolated.ps1`.

## Specs
S1. User priority: "Prefiero terminar primero con las refactorizaciones"; continuation: "Y continua si por favor". First bounded Servicio unit removes duplicated partial-table presentation assignments; remaining module boundaries require subsequent units, not speculative extraction.
S2. Preservation constraints carried from the agreed refactor: keep routes, public signatures, permissions, query ordering, persistence and audit unchanged. Preserve the eight shared metadata keys, SearchPartial-only SearchTerm, and leave Index unchanged. These are implementation preservation boundaries, not new product behavior.
S3. User disposition: "Agrega esa correccion propuesta al @TODO.md ,ya que ahora no me es una super prioridad." Keep that pending documentation separate; no credential changes.

## Tasks
T1 | S1-S2 | inline | done | Characterized both public partial actions on existing code; job-17 exit 0, 879 passed, 0 failed/skipped. No artificial RED. Commit: pending.
T2 | S1-S2 | inline | done | Extracted ConfigurarMetadataTabla, eight assignments only. Job-18 exit 0: 879 passed, 0 failed/skipped, matching job-17. Production diff inspected: 11 additions/11 deletions. Commit: pending.
T3 | S1-S3 | inline | in_progress | Commit only this verified unit and its tracking, record exact boundary and verification; report remaining Servicio scope. Commit: pending.

## Log
L1. User original continuation: "Y continua si por favor".
L2. Prior user priority: "Agrega esa correccion propuesta al @TODO.md ,ya que ahora no me es una super prioridad. Prefiero terminar primero con las refactorizaciones".
L3. Read-only explorer mw6r8suk-m-46i7 mapped Servicio. Parent narrowed its broad recommendation to shared partial metadata, avoiding premature query abstractions and Index permission/form behavior. SearchPartial retains distinct search/count calls and SearchTerm; TablePartial retains its distinct read/count. No security guards move. Risk: none identified for this bounded extraction. `TODO.md` is already modified and excluded from the unit commit.
L5. Baseline job-16 exited 1: 878 passed, 1 failed, none skipped. New characterization incorrectly expected the supplied ID; CrearServicio generates a document ID and mutates Servicio.Id. Corrected fixture to retain the generated IDs, without changing production code or lowering assertions. A new baseline is required.
L6. Post-refactor job-18 exited 0: 879 passed, 0 failed/skipped. The existing Netty shutdown Connection reset warning did not fail either successful suite. No view/JS/HTTP changes, so no new browser check applies; public controller characterization covers the extracted presentation metadata. Only SearchPartial and TablePartial call the new helper; no pre-existing shared helper changed, so no unrelated-command differential check applies. Risk: none; the requested pair is fully covered, no guards/contracts or dependencies changed.
L4. Delivery boundary: prior commit 298a8b1 has scoped approved/acknowledged review-c7801179030b950a. The accumulated branch is not a review candidate for this unit; no accumulated approval is inferred. No publication or history rewriting.
