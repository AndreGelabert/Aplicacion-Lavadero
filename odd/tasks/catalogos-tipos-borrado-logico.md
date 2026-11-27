# Catalogos de tipos - borrado logico y ABM unificado
Objective: Unify TipoServicio, TipoVehiculo, TipoDocumento and TipoInsumo under TipoInsumo's soft-delete, modal ABM, inline extra capability, diacritic-insensitive uniqueness normalization, and active-reference deactivation guard.
Branch: feat/catalogos-tipos-borrado-logico. Delivery: local work-unit commits only; no push/PR. Runner: Firebase.Tests/run-isolated.ps1.

## Specs
S1: «Quiero que todos los tipo funcionen si de la misma manera, pero mejor hagamoslo de la manera mas prolija posible, que hoy ya esta hecha, que copiemos nomas el funcionamiento de TipoInsumo literalmente, nada de agregar cosas locas, simplemente desactivacion logica, y que dentro de los respectivos modales de tipo, un pequeño abm igual que como se hizo para tipo insumo, nada de reactivar escribiendo nombres repetidos ni nada eso, si se quiere reactivar, ahi desde la tabla, manteniendo si la capacidad inline "extra".»
S2: «Normalizar la regla si, ignorando tildes, pero para impedir duplicados.»
S3: «No se si aplica con esta nueva forma propuesta pero si esta en uso por un servicio/paquete/vehiculo (que esten activos) (ya sea para este tipo como para cualquier otro) prohibir desactivar tipos en uso.»
S4: «No hay duplicados existentes, puedo dar certeza de eso.» (No data migration or duplicate-merge scripts; legacy docs without Estado are read as "Activo".)

## Tasks
T1 | S1,S2,S4 | inline | done | Shared diacritic-insensitive name normalizer (`NombreCatalogoNormalizer`), `Estado` field on `TipoServicio`/`TipoVehiculo`/`TipoDocumento`, `AuditCampos` helpers, and `TipoInsumo` normalization alignment.
T2 | S1,S2,S3,S4 | inline | done | Transactional `GuardarConAuditoria`/`CambiarEstadoConAuditoria` in `TipoServicioService`, `TipoVehiculoService`, `TipoDocumentoService`, active-only usage guard in `TipoInsumoService`, and active-only vs historical query separation.
T3 | S1,S3 | inline | done | Controllers (`ModalDatos`, `ModalGuardar`, `ModalEstado`, inline `DesactivarTipo*` replacing physical delete) and `ServicioTypeService` updated.
T4 | S1 | inline | done | Shared ABM modals (`_TipoServicioModal`, `_TipoVehiculoModal`, `_TipoDocumentoModal`), `tipos-catalogo-modal.js`, and inline deactivation UI/JS.
T5 | S1-S4 | inline + independent verifier (item 4 concurrency) | done | 122/122 focused Firestore integration tests passed (`CatalogosTiposBorradoLogicoTests`, `AuditCatalogueCaptureTests`, `AuditServiceCaptureTests`, `ServicioMutationRefactorTests`, `TipoInsumoIntegrationTests`, `TiposInsumoModalHttpIntegrationTests`, `ServicioServiceTests`); 6/6 UI modal regression tests passed; independent verifier `jd-judge-a` found 0 blockers (1 warning in `Views/Cliente/Index.cshtml` fixed and re-verified). Risk: item 4 (concurrency).

## Log
L1. Original request (verbatim):
Auditá los catálogos de tipos del sistema por ejemplo, tipos de servicio, de vehículo y de insumos para evaluar una mejora de borrado lógico.
La idea deseada es que se puedan desactivar en vez de borrar físicamente y que, si se registra nuevamente un nombre perteneciente a un tipo desactivado, se reactive silenciosamente en lugar de crear un duplicado. La desactivación debería seguir siendo inline.
En esta etapa no implementes nada. Identificá para cada catálogo: comportamiento actual, referencias históricas, validaciones de unicidad, normalización de nombres, consultas que incluyen o excluyen desactivados, riesgos de concurrencia y pruebas existentes.
Proponé una implementación por etapas y señalá decisiones pendientes, incluidos posibles registros duplicados existentes y cómo tratarlos sin perder datos. No realices migraciones ni modifiques datos.
L2. User refinement (verbatim):
Te respondere las dudas primero: 1. Quiero que todos los tipo funcionen si de la misma manera, pero mejor hagamoslo de la manera mas prolija posible, que hoy ya esta hecha, que copiemos nomas el funcionamiento de TipoInsumo literalmente, nada de agregar cosas locas, simplemente desactivacion logica, y que dentro de los respectivos modales de tipo, un pequeño abm igual que como se hizo para tipo insumo, nada de reactivar escribiendo nombres repetidos ni nada eso, si se quiere reactivar, ahi desde la tabla, manteniendo si la capacidad inline "extra".
2. Normalizar la regla si, ignorando tildes, pero para impedir duplicados.
3. No se si aplica con esta nueva forma propuesta pero si esta en uso por un servicio/paquete/vehiculo (que esten activos) (ya sea para este tipo como para cualquier otro) prohibir desactivar tipos en uso.
4. Respondido arriba creo.
5. No hay duplicados existentes, puedo dar certeza de eso.
Confirmame que entendiste antes de empezar con el plan de trabajo
L3. User authorization (verbatim):
Si, arranca
Pone esto en un plan de trabajo siguiendo el estilo/formato de los presentes en @[odd/tasks] y arranca a desarrollar hasta terminar todas las etapas
L4. Classification: large (multi-stage across 4 catalogs, backend transactions, controllers, UI modals, and integration/browser tests; exceeds single-diff resume test). Route: inline task-by-task with ~400-line work-unit commits on feat/catalogos-tipos-borrado-logico; independent verification at T5 for concurrency (item 4). Pre-existing unstaged changes in Services/InsumosService.cs and Firebase.Tests/Integration/InsumoCatalogIntegrationTests.cs must remain untouched.
