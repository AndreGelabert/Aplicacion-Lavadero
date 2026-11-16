# Estado y reactivación de insumos

## Specs

### S1

> Siguiendo el estilo de multiseleccion, agregar un filtro de Estado a la tabla de insumos, y que ese este activado por defecto, por si el lavadero deja de usar un producto no tiene que verlo si o si. Si es el unico insumo de un tipo, su "Renglon"/Mini tabla tambien se deberia ocultar a no ser que explicitamente decida ver inactivos. Ah y actualmente, un insumo desactivado, el icono de reactivar y su modal no son correctos, dice y tienen los mismos iconos/colores del modal de desactivar, cuando deberia ser el de reactivar como por ejemplo los servicios como se ve en @"Capturas de pantalla/Modal Reactivacion Servicio.png"

Filtro multiselección: sin parámetro de estado se muestran activos; `estados=Activo` y `estados=Inactivo` seleccionan estados, ambos muestran ambos y `estados=` explícito no filtra, como tipos sin selección. Limpiar restaura activos. Filtrar antes de agrupar y paginar, preservando totales físicos de todos los productos del tipo. Preservar búsqueda, tipos, bajo mínimo, orden local, paginación y permisos. No mutar estado en GET. Modal y botón reactivar verdes con tilde, desactivar rojo, correctamente actualizados al abrir sucesivamente; mantener formulario nativo, CSRF y auditoría.

## Tasks

- [x] T1 — S1: filtro de estado y agrupación/paginación.
- [x] T2 — S1: acción y modal de reactivación.
- [x] T3 — S1: verificación funcional Node, HTTP y navegador aislados.

## Log

### L2 — Evidencia funcional y límites

El parent observó HTTP job 18: 5/5, sin omisiones, en `.local/insumos-stock-test/state-reactivation-current/runs/2026-11-16T23-27-40-994Z-24116/estado-filtro.trx`. Browser job 20: 1/1, sin omisiones, advertencias ni errores, en `.local/insumos-stock-test/state-reactivation-current/browser-runs/2026-11-16T23-31-05-655Z-456/ui.trx`; cubre 390 y 1280, claro y oscuro, filtro y paginación, reset y alternancia de modales. Node: verificación independiente 2/2 antes de negrita y writer 2/2 después.

Contrato confirmado: parámetro ausente selecciona Activo; selección vacía explícita no filtra; limpiar restaura Activo. Los totales físicos incluyen el inventario completo; no se cambiaron guards del backend. Las pruebas HTTP cubren estados, agrupación, filtros, paginación, ciclo y rechazo por permiso; el formulario nativo, CSRF y auditoría se mantienen.

Historial: job 16 RED por inactivo visible; job 17 falló por puerto de fixture y URL relativa, corregidos en fixture 1/2; job 18 GREEN. Job 19 llegó al modal y detectó nombre sin negrita; corregido en producto 2/2; job 20 GREEN. No se reintentó el timeout previo de Kardex ni se altera su cuarentena o evidencia independiente pendiente: el cierre funcional de esta tarea no cierra esa prueba antigua.

### L1 — Solicitud literal

Siguiendo el estilo de multiseleccion, agregar un filtro de Estado a la tabla de insumos, y que ese este activado por defecto, por si el lavadero deja de usar un producto no tiene que verlo si o si. Si es el unico insumo de un tipo, su "Renglon"/Mini tabla tambien se deberia ocultar a no ser que explicitamente decida ver inactivos. Ah y actualmente, un insumo desactivado, el icono de reactivar y su modal no son correctos, dice y tienen los mismos iconos/colores del modal de desactivar, cuando deberia ser el de reactivar como por ejemplo los servicios como se ve en @"Capturas de pantalla/Modal Reactivacion Servicio.png"
