# Catálogo híbrido de vehículos
Branch: feature/catalogo-vehiculos-hibrido. Delivery: ask-on-risk, stacked-to-main selected; forecast exceeds 400 authored lines. Runner: dotnet test Firebase.Tests/Firebase.Tests.csproj.

## Specs
- S1: «implementar una tabla local de Marca y Modelo en vehículos para casos donde la API falle»; conservar datos válidos y permitir catálogo complementario aprobado.
- S2: «enfoque Hibrido o que sea escrito pero el sistema por detras haga un match entre la marca que escribio el cliente y las disponibles y lo mismo con el modelo»; «si no encuentre que sugiera la mas similar con el tipico quisiste decir X?»; coincidencias sugeridas requieren confirmación y no confundir modelos numéricos.
- S3: «el filtrado de marcas mediante el tipo de vehiculo»; «siquiera se estan contemplando los camiones»; no tratar tipos desconocidos silenciosamente como automóviles.
- S4: «los casos que se deberia escribir seran muy pocos»; «que no se deben popagar los errores de escritura»; «que se guarde automaticamente estaria mal»; el cliente puede continuar con texto manual y solo se agrega al catálogo compartido tras revisión.
- S5: «En que apartado del sistema el Empleado o Administrador gestionaria eso? Lo de autorizar»; «un apartado en la vista de vehiculos»; vincular existente, corregir y aprobar, dejar pendiente o descartar sin borrar el vehículo.
- S6: Preservar vehículos existentes y ambos flujos de registro; utilizar permisos y auditoría apropiados y verificar comportamiento y fallos.

## Tasks
- T1 [S1,S3] inline: catálogo persistente y consulta híbrida; pendiente; commit: pendiente.
- T2 [S2,S4,S6] inline: WhatsApp, coincidencias, confirmación y registro de pendientes; pendiente; commit: pendiente.
- T3 [S4,S5,S6] inline: revisión autorizada desde vehículos; pendiente; commit: pendiente.
- T4 [S1-S6] inline: integración y verificación independiente de datos y permisos; pendiente; commit: pendiente.

## Log
- L1: «Bueno, empeza con el desarrollo de todo lo hablado»
- L2: Alcance acordado en conversación previa: catálogo persistente respaldado por API, búsqueda y sugerencias, ingreso manual sin bloquear al cliente, revisión humana antes de publicar, camiones y tipos desconocidos. Mapeo inicial: ambos flujos guardan marca/modelo como texto; sesiones hacen sobrescritura completa; API no distingue fallo y resultados vacíos. Riesgos: datos persistidos, permisos, concurrencia y contratos compartidos. Ruta inline: no hay unidades de escritura independientes identificadas.
- L3: Started S3: unknown vehicle types now request the general brand catalogue rather than passenger cars. Focused RED: 1 failed/2 passed; GREEN: 3 passed. Full suite, independent verification and remaining S1-S6 pending. No commit yet. Delivery forecast exceeds 400 authored lines; chain strategy decision required before the next work-unit commit.
- L4: User selected «Cadena sobre main (Recomendado)»; no publishing or PR creation is authorized.
- L5: Full suite after S3 initial change: 606 passed. Independent item-3 verification running. Added isolated matcher with normalized exact matching, bounded edit-distance suggestions and strict digit preservation; focused RED compilation failure followed by GREEN 7 tests. Not integrated into WhatsApp yet. No commits.
- L6: Full suite with matcher: 613 passed. Independent S3 verifier found no regression in truck, motorcycle, unknown type, or empty web input; remaining specs unverified. Added controller-level regression tests; 7 focused cases passed. Commit hook blocked on pre-existing raw NHTSA response logging, so removed that debug log; focused tests passed again and full suite is running. Commit and feature remain pending.
