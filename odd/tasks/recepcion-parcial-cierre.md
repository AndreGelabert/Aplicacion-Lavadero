# Cierre de recepción parcial de orden

Seguimiento pendiente; preservar trabajo previo y cuarentena existente, sin reiniciar presupuestos ni hacer commits.

## Specs

### S1

> marque que recibi todo lo de la orden menos los trapos de microfibra, o sea en realidad deberia haber quedado parcial, ya que recibi 10 en lugar de 12

### S2

> Cosa que para empezar, no tendria ni que haber podido interactuar de nuevo con el modal. Tendria que haber quedado como completado parcialmente el estado de la orden y al entrar, todo como labels de solo lectura y agregando una nota o algo que aclarase la diferencia. O sea por ejemplo: Se recibieron 10 paños cuando se esperaban 12, contactarse con el proveedor y ya

### S3

> System.InvalidOperationException: 'Las cantidades de recepción deben ser válidas y mayores a cero.'

## Tasks

- [x] T1 — S1–S3: funcionalmente completo para los escenarios probados; un solo owner de comportamiento reproduce antes de cambiar y corrige backend, UI y pruebas de forma cohesiva. Superficies propuestas para esa implementación: `Models/Insumos.cs`, `Services/InsumosService.cs`, `Controllers/InsumosController.cs`, `Views/Insumos/Ordenes.cshtml`, `Services/AuditCampos.cs`, pruebas de integración relevantes y Node. Probar explícitamente los consumidores del estado existente, incluidos órdenes activas y reabastecimiento. Estas rutas no autorizan escritura ahora.
- [x] T2 — S1–S3: funcionalmente completo para los escenarios probados mediante integración HTTP/Razor, Node y verificación independiente; no constituye prueba Playwright ni paridad visual universal.
- [ ] T3 — Entrega históricamente bloqueada por la cuarentena previa y la prueba requerida; preservar presupuestos históricos y su estado, sin reset. No commits.

Decisión derivada de implementación del parent, no cita ni nueva decisión solicitada: la recepción parcial terminal puede usar `RecibidaParcial`, mostrada como `Recibida parcialmente`; actualizar stock solo por lo efectivamente recibido, presentar discrepancias en solo lectura y no permitir recepciones adicionales. Se permiten ceros por ítem si al menos uno es positivo, nunca todos cero. Preservar atomicidad de stock y auditoría; entradas inválidas y repeticiones no deben producir efectos.

Preservar y no alterar cambios previos de Stock/login, capturas y diarios. No modificar producción durante este seguimiento.

## Log

### L1 — Solicitud literal

Ah mientras hacias pruebas de eso me puse a probar ya directamente el sistema y encontre un comportamiento no deseado: System.InvalidOperationException: 'Las cantidades de recepción deben ser válidas y mayores a cero.' Lo obtuve cuando despues de haber completado el despacho de la orden para poder registrar su recepcion, mas precistamente en la recepcion, ya que marque que recibi todo lo de la orden menos los trapos de microfibra, o sea en realidad deberia haber quedado parcial, ya que recibi 10 en lugar de 12, y no se actualizo el estado de la orden para reflejar esto, simplemente se cerro el modal, creo que salio una notificacion pero no se actualizo su estado ni nada, podia acceder nuevamente al formulario y todos los campos estaban en 0 menos el de los trapos que decia 2, intente a darle a recibir insumos y ahi se produjo la excepcion en el servicio de insumo. Cosa que para empezar, no tendria ni que haber podido interactuar de nuevo con el modal. Tendria que haber quedado como completado parcialmente el estado de la orden y al entrar, todo como labels de solo lectura y agregando una nota o algo que aclarase la diferencia. O sea por ejemplo: Se recibieron 10 paños cuando se esperaban 12, contactarse con el proveedor y ya

### L2 — Evidencia y continuación propuesta

- Causa raíz: el servicio mantenía la recepción incremental y dejaba el estado parcial abierto; la UI reabría con ceros, que provocaban la excepción.
- Job 8 del parent RED: cuatro pruebas, una pasó y tres fallaron. Primera pasada de producción 1/2: cierre terminal `RecibidaParcial`, stock real atómico, ceros por ítem con al menos uno positivo, rechazo de todos cero, negativos, exceso y replay; detalle de 10/12, faltante 2 y contacto al proveedor en solo lectura.
- Fixture job 9: error de sintaxis en regex generado antes del lanzamiento; corrección 1/2. Job 10: nueve pruebas, siete pasaron y dos fallaron por fixtures desactualizados de proveedor `Contacto` y página `Crear orden`; corrección 2/2.
- Job 11, ejecución `2026-11-16T22-01-55-769Z-9264`, `partial-receipt.trx`: 8/9. Las ocho aprobadas cubren cuatro de cierre real, rollback de auditoría, atomicidad de stock anterior, recepción parcial HTTP con auditoría y recepción de stock. La restante, `OrdManualValidaYExponeErrorDeEnvio`, espera `Fallo de prueba` en línea 221; `Services/InsumosService.cs:2355-2362` actualmente prohíbe creación manual directa y orienta a cotización/repetición, contrato existente y no regresión S1–S3. No excluir ni ignorar la prueba. Presupuesto del fixture agotado: se necesita continuación explícita.
- Verificador independiente nuevo `mwjt6wky-z-cb6v`: S1, S2 y S3 MET, Node 7/7; observación advisory sobre label crudo. Segunda pasada de producción 2/2 corrigió el label: Node RED 1/2, GREEN 2/2, combinado 8/8. La vista ahora muestra `Estado actual: Recibida parcialmente (orden cerrada)`.
- No se ejecutó .NET después del último cambio del label y todavía no hay prueba de navegador. Las órdenes históricas parcialmente recibidas en `Despachada` no se migraron y siguen siendo recepcionables. Sin cambios DEV, emails ni commits.
- Continuación propuesta, no autorizada: owner original del fixture actualiza la prueba obsoleta de orden manual al flujo público real aprobado de cotización/repetición, conservando las comprobaciones de fallo de email y seguridad de creación; máximo una corrección de fixture. Luego el parent ejecuta las nueve pruebas y comprueba render y solo lectura. No reiniciar presupuestos ni cuarentena histórica.

### L3 — Continuación y prueba funcional

- Respuesta humana «Segui si»: autorizó una corrección adicional de fixture. El owner original actualizó `OrdManualValidaYExponeErrorDeEnvio` al flujo de cotización, respuesta y adjudicación, conservando permisos, conteos del email simulado y recepción. Amplió la prueba HTTP para comprobar el label terminal en español y la ausencia de formularios al mostrar inicialmente y reabrir tras el intento repetido.
- Job 12 del parent GREEN: 9/9, exit 0, 25 s, 0 advertencias y 0 errores. Resultado: `.local/insumos-stock-test/partial-receipt-current/runs/2026-11-16T22-14-11-275Z-27196/partial-receipt.trx`. Junto con Node 8/8 y el anterior verificador independiente S1–S3 MET, T1 y T2 quedan funcionalmente completos para los escenarios probados. La prueba de render usa HTTP/Razor real, no Playwright; no demuestra paridad visual universal.
- Presupuestos preservados: producción 2/2, fixture 2/2 y corrección adicional humana 1/1, todos usados. La entrega T3 sigue bloqueada por la cuarentena histórica. Sin commits, cambios DEV, emails reales ni migración histórica.
