# Wash modal layering

Wash helper fallback backdrops use layer 51 and panels layer 52; detail fallback also updates a reused backdrop.
Existing Flowbite and fallback lifecycles are retained; no new Escape or outside dismissal is introduced. Auditoría's native modal remains distinct.
Focused check: `node --test Firebase.Tests/WashModalLayering.test.cjs`; browser fixture: `tests/browser/wash-modal-layering.spec.cjs`.
