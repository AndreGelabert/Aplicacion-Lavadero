# Roles and Personal table controls

Roles reuses sort-state reads and guarded confirmation-label updates. Personal reuses guarded filter initialization and dropdown setup in the existing refresh order.
Optional shared helpers retain standalone fallbacks; filtering and dropdown closing remain separate.
Focused check: `node --test Firebase.Tests/TableControlsConsistency.test.cjs`; browser fixture: `tests/browser/table-controls-consistency.spec.cjs`.
