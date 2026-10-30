# Validación de dependencias de permisos

Branch: `feature/rbac-ui-fixes`; base: `bf0141b`; delivery: local feature branch.
Runner: `dotnet test Firebase.Tests`; UI text: Spanish.

## Specs

- **S1**: "si a una vista le pongo permisos de Consultar, Modificar y Eliminar pero no de Crear, la vista no me sale en el navbar lateral"; "deberia entrar a la vista pero deberia tener deshabilitado/oculto el formulario de registro".
- **S2**: "si se selecciona un permiso de modificar y/o eliminar, si o si debe tener el permiso de consultar".
- **S3**: "en caso de seleccionar un registro a editar, el acordeon del formulario deberia ser visible e interactuable, pero despues de la edicion volver a inhabilitarse o desaparecer por no tener permiso de creacion".
- **S4**: "si se selecciona la creacion de cliente, si o si debe permitirse la creacion de vehiculo"; confirmed: Crear clientes requires Crear vehículos.
- **S5**: "la eliminacion de un cliente y su vehiculo"; confirmed: Eliminar clientes requires Eliminar vehículos and Eliminar vehículos requires Modificar clientes.
- **S6**: "actualizar la rama de esta feature ... respecto a la main"; local branch fast-forwarded to `bf0141b`.

## Tasks

- T1 (S2, S4, S5): inline — permission dependency validation and tests; complete, GREEN.
- T2 (S1, S3): inline — navigation behavior pinned; edit-only form lifecycle implemented; S3 browser interaction blocked by unavailable browser runtime.
- T3 (S1–S5): inline + independent verifier for item 2 — isolated tests/build passed; S1, S2, S4, S5 met; S3 remains pending.

## Log

- L1: Algunos detalles mas que me di cuenta que faltaron en la validaciones de roles y permisos. Cambios principalmente cetrado y que se refleja en los casos de usos extendidos de @Documentacion/1-Requisitos.md .Que es que basicamente me di cuenta que acutalmente, si a una vista le pongo permisos de Consultar, Modificar y Eliminar pero no de Crear, la vista no me sale en el navbar lateral, y eso no deberia ser asi, se supone que tengo los otros permisos. Asi que habria que corregir eso, deberia entrar a la vista pero deberia tener deshabilitado/oculto el formulario de registro nomas que esta en la parte superior de la tabla. Por otra parte, por como esta diseñado el sistema, si se selecciona un permiso de modificar y/o eliminar, si o si debe tener el permiso de consultar, sino como puede acceder a la tabla para modificar y/o eliminar el registro. Y en caso de supongamos tener en la vista todos los permisos menos el de crear, en caso de seleccionar un registro a editar, el acordeon del formulario deberia ser visible e interactuable, pero despues de la edicion volver a inhabilitarse o desaparecer por no tener permiso de creacion. Otro caso de permiso que debe ser condicional es el caso de Creacion de vehiculo, que en caso de establecer la creacion de cliente, si o si debe permitirse la creacion de vehiculo porque es esencial para el flujo del caso de uso (misma validacion de vehiculo a cliente, si selecciona crear vehiculo si o si debe tener creacion de vehiculo). Tambien caso similar con la eliminacion de un cliente y su vehiculo, ya que en el sistema, la unica forma de eliminar un vehiculo (con eliminar recordemos que es ponerlo inactivo, baja logica) es editar un cliente y eliminarle el vehiculo asociado. O sino si directamente se da de baja al cliente, y este es unico propietario de su vehiculo, el mismo se pone inactivo automaticamente junto con el cliente por lo que tambien termina siendo un permiso "especial" condicionado
- L2: Si entendiste bien, igual antes de ponerte a trabajar deberias actualizar la rama de esta feature si es que piensas trabajar en la misma porque quedo desactualizada respecto a la main
- L3: Pues implementala
- L4: Branch fast-forwarded to local main `bf0141b`; explorer identified navigation based on Consultar, OR semantics of permission attributes, and permission normalization shared with authentication. No silent privilege expansion assumed. Engram mirror pending.
- L5: Added server-side dependency validation and focused tests. RED: 5 failures, 2 passes; GREEN: 7 passes. Full suite after preserving administrator critical-permission error: 646 passed, 23 skipped, 0 failed. T1 remains pending until UI and independent security verification; S1 and S3 remain unimplemented and the navigation symptom has not been reproduced.
- L6: Added a navigation regression asserting `Clientes.Consultar` + `Modificar` + `Eliminar` without `Crear` remains visible. Inspection shows existing sidebar behavior already filters only by `Consultar`; create permission does not gate it. Customer, Service and Package edit success now hides the accordion and replaces the reset form with Spanish feedback when Create is absent; Vehicle edit already hides the accordion on success. Role dependency validation runs server-side and preserves Administrator critical-permission error priority.
- L7: RDD is disabled by clone-local user choice; no native review is started. Independent verification remains required because permission validation touches authorization (high-risk item 2).
- L8: Full local suite passed: 650 passed, 23 skipped, 0 failed; build passed with 0 warnings and 0 errors. The independent verifier could not prepare its fresh isolated baseline copy and returned no S1–S5 verdicts; required isolated probes remain pending. Its scratch directory was removed. No commits or push made.
- L9: User requested a retry after installing Firebase CLI. The second independent attempt successfully prepared isolated Git worktrees at `bf0141b`, applied the candidate patch, and ran focused tests (23/23), full tests (650 passed, 23 skipped), and build (0 warnings/errors). Verifier marked S1, S2, S4, S5 met; S3 remains unverified because it did not run browser interaction for edit-only save. The CLI command was not found in the current shell; no emulator was run. Temporary worktrees were removed; no commits or push made.
- L10: Retried S3 validation in the current environment. `node --check` passed for the Cliente, Servicio and Paquete scripts. No browser executable, Playwright/Puppeteer/jsdom package, Firebase CLI command, `firebase.json`, or emulator configuration was available in this shell, so an actual edit-submit interaction could not be executed. S3 remains unverified; no product code changed.
