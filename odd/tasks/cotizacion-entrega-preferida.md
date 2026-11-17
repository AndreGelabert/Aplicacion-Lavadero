# Cotización y entrega preferida

## Specs

### S1 — Solicitud y preferencia

> Estoy de acuerdo con tu recomendacion con el añadido de agregar a la solicitud de cotizacion los campos fecha y hora preferida para la entrega (obvio no necesariamente el proveedor podra cumplirla, obviamente no deberia seleccionar una fecha y hora anterior a la del dia). El nombre del lavadero lo tiene que obtener automaticamente a partir de la Configuracion del sistema que ahi se define eso. Te autorizo a editar

Fecha y hora son opcionales juntas, o ambas informadas; la comparación es contra el instante actual en America/Argentina/Buenos_Aires, validada en servidor antes de persistencia, auditoría o envío. Persistir el instante UTC, sin migrar solicitudes históricas; mostrar hora argentina y aclarar que es preferencia, no compromiso. El nombre proviene de Configuracion.NombreLavadero, no del formulario.

### S2 — Contenido y plantillas

> Solicitud: pedir explícitamente al proveedor precio, disponibilidad y plazo de entrega, en lugar de presentar esos datos como si ya estuvieran definidos. Orden: mostrar el plazo cotizado y la ubicación del lavadero; quitar fecha y hora del formato recomendado mientras no exista un campo real para acordarlas. Ayuda: distinguir los marcadores comunes de los que solo tienen sentido en una orden.

Los marcadores de fecha y hora expresan preferencia en solicitud, no una entrega confirmada. Mantener plantillas personalizadas y sustitución de ausentes como No informado; no sobrescribir configuración. Preservar escape HTML y protección CRLF y validación de correo antes de escritura. No inventar plazo, total ni fecha acordada.

### S3 — Seguridad y compatibilidad

Aplicar la validación también al servicio y entradas directas; invalidaciones no generan solicitudes, auditorías ni correos, incluso con múltiples proveedores. Mantener compatibilidad de documentos y llamadas anteriores. Pruebas aisladas con proveedor y correo simulados; sin correo real, cambios DEV ni migración.

## Tasks

- [x] T1 — S1–S3: implementación funcional y auditoría manual corregida; evaluación independiente MET.
- [x] T2 — S1–S3: verificación funcional informada por el parent: .NET 19/19, navegador 1/1 y Node de preservación 25/25, con los límites indicados en L5.
- [ ] T3 — BLOQUEADA: evaluación nativa UNASSESSABLE por alcance de archivos candidatos sin seguimiento; pendiente elegir cómo resolver ese alcance y obtener evaluación del proveedor. Sin aprobación ni entrega. Cuarentena histórica y Kardex ajenos permanecen sin cambios; sin commits.

## Log

### L1 — Solicitud literal

Estoy de acuerdo con tu recomendacion con el añadido de agregar a la solicitud de cotizacion los campos fecha y hora preferida para la entrega (obvio no necesariamente el proveedor podra cumplirla, obviamente no deberia seleccionar una fecha y hora anterior a la del dia). El nombre del lavadero lo tiene que obtener automaticamente a partir de la Configuracion del sistema que ahi se define eso. Te autorizo a editar

Recomendación aceptada: Solicitud: pedir explícitamente al proveedor precio, disponibilidad y plazo de entrega, en lugar de presentar esos datos como si ya estuvieran definidos. Orden: mostrar el plazo cotizado y la ubicación del lavadero; quitar fecha y hora del formato recomendado mientras no exista un campo real para acordarlas. Ayuda: distinguir los marcadores comunes de los que solo tienen sentido en una orden.

Zona confirmada por el usuario: «Opera en esa zona horaria si, es algo que implementarias desde el back?» y luego «Si, segui trabajando y no pares hasta terminar con lo solicitado», tras explicación de zona Argentina en backend y persistencia UTC.

### L2 — Evidencia y continuación propuesta

- Producción 2/2 agotada: implementadas preferencia opcional conjunta en zona argentina con persistencia UTC, mínimos de cliente y validación de servidor, nombre automático de configuración, formatos recomendados, plantillas personalizadas y ayuda. Sin afirmar completitud de S1–S3.
- Parent job 2: 16/16 aprobadas. Suite ampliada job 3: 17/18, sin skips; `.local/insumos-stock-test/preferred-delivery-current/runs/2026-11-17T05-20-29-804Z-8632/preferred-delivery.trx`. Falló `PlantillasPersonalizadasConservanPreferenciaYValoresAusentes`: esperaba literalmente `&lt;jabón&gt;`, aunque la codificación del acento como código de carácter es válida. Fixture corregido 1/2 conservando comprobaciones de texto decodificado y ausencia de HTML inseguro; rerun pendiente, no ejecutado en este seguimiento.
- Verificación independiente `mwk8uzla-9-ziyz`: Node 3/3, S2 MET; S1 y S3 señalados como no satisfechos. La emisión manual registra solo GrupoId y no incluye el nuevo instante preferido; la captura de solicitud ampliada no es usada por dicha emisión. La ausencia de auditoría en emisión automática es preexistente, no causada por este candidato.
- Continuación propuesta en ese momento: mismo owner, máximo una corrección de producción limitada a incluir la preferencia en la auditoría real de emisión manual y prueba de integración de auditoría pública; después el parent ejecuta las pruebas focalizadas y comprueba render/navegador. No se realizaron correos reales, cambios DEV, escrituras de configuración ni commits.

### L3 — Continuación autorizada y corrección

- Usuario: «Si, segui trabajando y no pares hasta terminar con lo solicitado». El parent autorizó específicamente una corrección adicional 1/1, sin reiniciar producción 2/2 ni fixture 1/2 históricos.
- RED parent job 4: 18/19 aprobadas; `EmisionManualAuditaPreferenciaDeCadaProveedor` falló por clave `Solicitudes` ausente en auditoría pública. Informe `.local/insumos-stock-test/preferred-delivery-current/runs/2026-11-17T05-30-22-442Z-24068/preferred-delivery.trx`.
- Corrección adicional 1/1 aplicada: la misma transacción de emisión conserva `GrupoId` y registra capturas por solicitud con identificador, proveedor y preferencia UTC usando `AuditCampos.CapturarSolicitudCotizacion`. No cambia la auditoría automática preexistente.

### L4 — Verificación y preservación

- Parent informa job 5 .NET GREEN 19/19, sin errores ni advertencias C#, incluyendo auditoría de ambos proveedores y UTC; evaluación independiente S1, S2 y S3 MET tras las correcciones. La ausencia de auditoría automática es preexistente y fuera de alcance.
- Parent informa RED de preservación Node 22/25: tres mocks VM carecían de `form.elements`; un HTMLFormElement real sí lo expone. Fixture 2/2 usado para modelar `elements.namedItem` con `null` para controles ausentes, sin alterar aserciones; rerun 25/25 GREEN. Producción adicional 1/1 agotada.
- En ese momento navegador job 6 seguía en curso. Native assess UNASSESSABLE por archivos sin seguimiento, declaración explícita sin aprobación; no se realizaron commits.

### L5 — Evidencia funcional y bloqueo de entrega

- Parent informa job 5 GREEN 19/19, sin skips ni errores o advertencias del compilador C#, incluyendo auditoría pública de ambos proveedores y preferencia UTC: `.local/insumos-stock-test/preferred-delivery-current/runs/2026-11-17T05-36-33-234Z-28808/preferred-delivery.trx`. Se reportaron avisos Java/Netty durante limpieza, distintos de advertencias C#.
- Parent informa navegador job 6 GREEN 1/1, sin skips ni errores o advertencias del compilador C#: `.local/insumos-stock-test/preferred-delivery-current/browser-runs/2026-11-17T05-37-47-770Z-16780/ui.trx`. Cobertura GET en 390/1280, claro/oscuro; parent revisó captura 390 claro con campos visibles y ayuda de preferencia. No prueba POST ni correo en navegador; las 19 pruebas HTTP cubren persistencia, correo simulado y horario. Se reportaron avisos Java/Netty durante limpieza.
- Parent reejecutó Node 25/25 tras corregir mocks, preservando todas las aserciones. Seguimiento independiente `mwk9foqw-f-ljtv`: S1, S2 y S3 MET con auditoría manual corregida; ausencia de auditoría automática preexistente y fuera de alcance.
- Evaluación nativa UNASSESSABLE con declaración explícita de archivos sin seguimiento; no equivale a aprobación. Entrega bloqueada hasta elegir resolución del alcance de candidatos sin seguimiento y evaluación del proveedor. Producción 2/2 más extra 1/1 y fixture 2/2 agotados, sin reinicio. Cuarentena histórica y Kardex ajenos sin cambios.
- Sin correo real, escrituras DEV, sobrescritura de plantillas ni commits de la tarea.
