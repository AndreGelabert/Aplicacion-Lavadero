# RBAC + UI fixes (permissions, inline role form, navigation, toasts)

Objective: make the role/permission feature usable end to end, with the permission matrix matching the real actions of each module, permission-aware navigation and landing, and the role form embedded inline like the other CRUD views.
Branch: `main` (worktree changes uncommitted; no feature branch, push, or PR created).
Delivery: `ask-on-risk` (default). Test runner: `dotnet test Firebase.Tests`.

## Specs

- **S1 — Permission matrix per module.** `RolCatalogo` must expose actions per module instead of one uniform list. Exact matrix:
  - `Clientes`: `Consultar`, `Crear`, `Modificar`, `Eliminar`
  - `Vehiculos`: `Consultar`, `Crear`, `Modificar`, `Eliminar`
  - `Servicios`: `Consultar`, `Crear`, `Modificar`, `Eliminar`
  - `Paquetes`: `Consultar`, `Crear`, `Modificar`, `Eliminar`
  - `Lavados`: `Consultar`, `Crear`, `Modificar`
  - `Personal`: `Consultar`, `Modificar`
  - `Configuracion`: `Consultar`, `Modificar`
  - `Auditoria`: `Consultar`
  - `Roles`: `Consultar`, `Crear`, `Modificar`, `Eliminar`
  Total valid permissions: **28**. Invalid combinations such as `Auditoria.Crear`, `Configuracion.Eliminar`, `Lavados.Eliminar`, `Personal.Crear` must be rejected by `EsPermisoValido` and dropped by `NormalizarPermisos`. Persisted role documents are normalized when read and when written; the Administrator keeps all four `Roles.*` critical permissions.
- **S2 — Role form inline.** The Roles view must not use `#rol-modal`. Creating/editing a role loads `_RolForm` into `#rol-form-container` inside a Flowbite accordion (`data-accordion="collapse"`, `#accordion-flush-body-1`), matching the Cliente/Vehículo/Servicio pattern. The heading shows `Nuevo Rol` or `Modificar Rol`. `Cancelar` collapses the accordion. Successful create/update collapses it, clears the form, reloads the table and shows a toast. The permission matrix renders only the actions of each module (`Roles` keeps its critical checkboxes disabled for Administrator).
- **S3 — Toast position.** The roles toast container must clear the fixed top navigation: fixed at `top: 5rem`, right-aligned, above the sidebar/nav (`z-index: 60`), stacked vertically. Toast text remains Spanish and disappears after 4 seconds.
- **S4 — Permission claims and authorization.** On login (email/password and Google), load the active role's permissions from Firestore and add one `permiso` claim per valid permission, in addition to the existing name/email/role claims. Add a `[Permiso("Modulo.Accion")]` attribute plus a dynamic policy provider; support multiple keys as OR. Every module controller action is protected by its module action: list/detail/lookups require `Consultar`; creates require `Crear`; edits/reactivations require `Modificar`; logical deactivations and deletions require `Eliminar` (Personal deactivation/reactivation uses `Modificar`; Lavados cancellation/payment uses `Modificar`).
- **S5 — Unauthorized handling.** An authenticated user without permission must get a friendly Spanish 403 page at `/Home/AccessDenied` (HTTP 403), never a redirect to `/Login/Index`. Anonymous users still go to login. AJAX requests receive 401/403 status codes instead of HTML redirects. `AccessDeniedPath` points to the new page.
- **S6 — Permission-aware navigation.** The sidebar is a single permission-filtered list. Visible entries and required permissions:
  - `Lavados` → `Lavados.Consultar`
  - `Clientes` → `Clientes.Consultar`
  - `Vehículos` → `Vehiculos.Consultar`
  - `Servicios` → `Servicios.Consultar`
  - `Paquetes` → `Paquetes.Consultar`
  - `Personal` → `Personal.Consultar`
  - `Auditoría` → `Auditoria.Consultar`
  - `Configuración` → `Configuracion.Consultar`
  - `Roles y Permisos` → `Roles.Consultar`
  No `Administrador`-only block. The dead `/Estadisticas` link is removed. Leftover English chrome (`Open user menu`, `Search`, `Flowbite`) is replaced with Spanish/branding. The active controller is highlighted.
- **S7 — Landing by permission.** `/` and post-login redirect to the first module the user can open, in sidebar order. With only `Auditoria.Consultar` + `Configuracion.Consultar` + `Configuracion.Modificar`, the user lands on `/Auditoria`, sees only Auditoría and Configuración in the sidebar, and can open both. A user with no module permission lands on the 403 page with the message `Su rol no tiene acceso a ningún módulo del sistema. Contacte al administrador.`
- **S8 — In-view actions gated.** Create/edit/deactivate/delete buttons and create-edit accordions are hidden when the user lacks the matching permission. Read-only users keep filters, tables and details. Configuración hides the save/schedule edit controls without `Configuracion.Modificar`; Auditoría stays read-only. All visible UI text stays in Spanish.
- **S9 — Verification.** `RolCatalogoTests` asserts 28 unique permissions and the matrix; new tests assert per-module actions, invalid-action rejection, and the landing helper. `dotnet build` and `dotnet test Firebase.Tests` pass. Existing behavior of non-role modules is preserved (same routes, same CRUD flows, same messages).

## Tasks

| ID | Task | Specs | Route | Status |
| --- | --- | --- | --- | --- |
| T1 | Update catalog matrix, normalization and tests (RED then GREEN) | S1, S9 | inline | done — `895913b`; `RolCatalogoTests` GREEN (28 permissions) |
| T2 | Add permission claims, claim helper, policy provider/attribute and landing helper + tests | S4, S7, S9 | inline | done — `743c221`; `PermisoTests` + `NavegacionSistemaTests` GREEN |
| T3 | Add `HomeController`, 403 view, login redirects, cookie AJAX behavior and default route | S5, S7 | inline | done — `743c221`; build + tests GREEN |
| T4 | Protect controller actions with `[Permiso]` | S4 | inline | done — `07c7a41`; 104 attributes, build + tests GREEN |
| T5 | Inline role form + toast fix in Rol view/JS | S2, S3 | inline | done — `f80432e`; runtime checks G1–G15 |
| T6 | Permission-filtered sidebar and Spanish chrome | S6 | inline | done — `f80432e`; runtime checks A3–A5, C3, D7 |
| T7 | Gate in-view buttons/forms in module views | S8 | inline | done — `6b84144`; runtime checks A6–A9, D1–D7, E, F, H, Vehículo/Servicio/Paquete |
| T8 | Build, run tests, verify landing scenario and report | S9 | inline | done — `6b84144`; build 0 errors; 563/563 tests; independent verifier: S1–S9 MET |

## Log

- **L1 (authorized scope, reconstructed after context compaction).** The user reported UI/UX and RBAC integration bugs after testing the new roles and permissions feature: audit the module permission matrix (Auditoría only `Consultar`; Configuración only `Consultar` + `Modificar`; trim the rest to what applies); replace the role modal with an embedded inline form consistent with the Cliente/Vehículo/Servicio pattern; fix toast position/z-index (stuck at the very top-right edge, needs to align with the layout); make post-login landing redirect to the first view the role can access; filter the sidebar by the active role's permissions; allow authorized routes (Auditoría, Configuración) and reject unauthorized routes without bouncing to login; hide/disable in-view action buttons per permission. Acceptance scenario: a role with only Auditoría Ver + Configuración Ver/Editar lands on an allowed view, sees only Auditoría/Configuración in the nav, and can open both. All UI text in Spanish. Use the `ui-ux-pro-max` skill.
- **L2 (decision).** Logical deactivation maps to `Eliminar`; reactivation/edit maps to `Modificar`. Exceptions where the module has no `Eliminar`: Personal uses `Modificar` for deactivate/reactivate; Lavados uses `Modificar` for cancel/payment/finalize.
- **L3 (decision).** Permission claims are issued at login from the active Firestore role. Existing cookies keep their claims until the next login; this change does not add live claim refresh.
- **L4 (decision).** Administrator keeps `Roles.*` critical permissions enforced by `RolReglas`; the other modules remain editable for the Administrator.
- **L5 (risk).** This change touches authentication/authorization (high-risk item 2) and the permission contract persisted in Firestore (item 3). Verification includes build, unit tests, and an independent review of the diff.
- **L6 (implementation evidence, 2026-10-27).** `dotnet build`: 0 errors (245 pre-existing warnings). `dotnet test Firebase.Tests --no-build`: 539/539 passed, 0 failed, 0 skipped. `RolCatalogoTests`, `PermisoTests` and `NavegacionSistemaTests` are GREEN.
- **L7 (runtime evidence, 2026-10-27).** Local app boot on `https://localhost:7101` with a local-only verification cookie minted from the app's Data Protection key ring (read-only probing, no data writes). 49 checks passed:
  - Acceptance role (`Auditoria.Consultar`, `Configuracion.Consultar`, `Configuracion.Modificar`): `/` → `/Auditoria` (302); sidebar exactly `/Auditoria,/Configuracion` with active highlighting; `/Auditoria` and `/Configuracion` both 200; Configuración shows save and schedule editors with `Modificar`; no Lavados cancel link without `Lavados.Consultar`.
  - No-permission role: `/` → `/Home/AccessDenied`; page returns 403 with `Su rol no tiene acceso a ningún módulo del sistema. Contacte al administrador.`
  - Unauthorized route: `/Cliente` → 302 `/Home/AccessDenied?ReturnUrl=%2FCliente`; AJAX request → 403 with no HTML redirect; AccessDenied page returns 403 with `No tiene permiso para acceder a esta sección. Contacte al administrador.`
  - Landing order: `Lavados.Consultar` + `Clientes.Consultar` lands on `/Lavados` (sidebar order, not alphabetical).
  - Read-only gating: Configuración hides save/bulk/individual schedule controls, disables day checkboxes and keeps the read-only detail; Cliente, Lavados, Vehículo, Servicio, PaqueteServicio and Personal hide create/edit/deactivate/cancel/payment controls while keeping tables, filters and `Ver detalle`.
  - Roles: inline `#rol-form-container` + `#accordion-flush-body-1` present, no `#rol-modal`; `Nuevo Rol` requires `Roles.Crear`; edit requires `Roles.Modificar`; delete requires `Roles.Eliminar`; toast container uses inline `top: 5rem; right: 1rem; z-index: 60`.
  - Anonymous request to `/` still redirects to `/Login/Index`.
- **L8 (T7 completion, 2026-10-27).** Finished the last view: Configuración gates the bulk and per-day schedule editors, disables the day checkboxes, keeps the read-only summary, and only renders the `Cancelar` link when the user has `Lavados.Consultar`. Build and tests re-run after the change: 0 errors, 539/539 passed.
- **L9 (risk line).** `Risk: items 2 and 3` — authentication/authorization and the persisted permission contract changed; independent verification required before completion.
- **L10 (RDD assessment).** `gentle-ai review assess` over the worktree (untracked support files excluded) reported `risk: medium`, `review_due: false`, `review_due_reason: under_budget`, 41 paths / 1926 changed lines. The change is raised to high risk by items 2 and 3 for verification purposes; native review decision is pending the independent verifier.

- **L11 (encoding blocker correction, 2026-10-26).** Independent verification found mojibake in 7 modified controllers (458 `Ã.`/`Â.` sequences) and lossy `U+FFFD` in `LoginController.cs` (93) and `Views/Personal/_PersonalTable.cshtml` (11), plus 4 corrupted emoji in `VehiculoController.cs`. Repair: reverse-mojibake to valid UTF-8, restore the emoji, restore every `U+FFFD` from the `1abe9de` cp1252 text, drop the BOMs added versus baseline (8 controllers), and normalize `TipoVehiculoController.cs` (baseline cp1252) to valid UTF-8. No logic, route, permission string, or UI text changed; 104 `[Permiso]` attributes and the 28-key catalog remain intact.
- **L12 (probe + evidence, 2026-10-26).** Added `Firebase.Tests/Security/RbacSpecProbes.cs` (21 probes from the independent verifier) and `Firebase.Tests/Security/TextoEncodingProbes.cs` (mojibake `Ã.`/`Â.`/`ðŸ` guard, `U+FFFD` scan over `Controllers/` + `Views/`, and visible-string assertions). `dotnet build`: 0 errors (245 pre-existing warnings). `dotnet test Firebase.Tests --no-build`: 563/563 passed (539 existing + 24 probes). `bin/Debug/net9.0/Firebase.dll` contains `Cerrar Sesión`, `Correo electrónico`, `Mostrando página`, `ID de cliente no válido`, `Debe agregar al menos un vehículo.` and `Editar Vehículo` as UTF-16; the corrupted variants are absent.
- **L13 (independent verification closure, 2026-10-26).** Bounded rechecks by the independent verifier: both encoding blockers resolved, **S1–S9 MET**, no remaining blockers. The verifier confirmed the decoded diff versus `1abe9de` preserves every baseline Spanish line and adds only the intended RBAC/login changes.
- **L14 (delivery state, 2026-10-26).** Worktree remains uncommitted on `main` (HEAD `1abe9de`); no commits, push, or PR. Feature files are untracked (`Security/`, `Controllers/HomeController.cs`, `Views/Home/`, `Firebase.Tests/Security/`) plus support (`odd/`, `.atl/`).
- **L15 (chain strategy, 2026-10-27).** Native review START refused the 51-file / 2146-line workspace candidate with `lens_context_budget_exceeded`; no authority was created. The user chose **feature-branch-chain**. Created `feature/rbac-ui-fixes` from `1abe9de`; changes will be committed as cohesive work units and reviewed slice-by-slice. No push or PR is authorized.
- **L16 (work-unit commits, 2026-10-27).** Local feature-branch chain: `895913b` permission catalog/normalization (133 lines); `743c221` claims, policies, landing and 403 handling with tests (952 lines, cohesive high-risk auth unit; size exception); `07c7a41` controller enforcement (156 lines); `f80432e` sidebar + inline role form/toast (537 lines, cohesive UI unit; size exception); `6b84144` in-view action gating + encoding guards (368 lines). No push or PR.
- **L17 (post-split verification, 2026-10-27).** On `feature/rbac-ui-fixes` after all source commits: `dotnet build` completed with 0 errors; `dotnet test Firebase.Tests --no-build` passed 563/563, 0 failed, 0 skipped. Native review proceeds commit-by-commit because the atomic workspace candidate exceeded its context budget.
