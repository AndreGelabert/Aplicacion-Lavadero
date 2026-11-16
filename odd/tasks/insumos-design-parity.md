# Insumos — design parity and email clarity
Delivered locally with bounded verification and the limitations below; cierre documental en este commit. This request supersedes conflicting presentation requirements in earlier task documents without rewriting historical quotes or treating prior closeout as evidence for this request.

## Specs
S1. «Pues hubo algunas regresiones y mejoras pero muy pequeñas, lejos de lo que yo quiero. Para empezar, los botones de TODAS las vistas, mas precisamente de los formularios de creacion o edicion, en el modo claro los botones son invisibles hasta que paso el cursor por arriba, recien ahi salen, si tengo mi cursor arriba basicamente.»

S2. «Ya hablando especificamente de la vista. Ahora ire por vistas, en la vista de configuracion, el texto libre de la Plantilla de email es muy confuso, no hay nada que indique ni un formato recomendado ni para que sirve esa plantilla, si es para una solicitud de cotizacion o de compra, y si es una plantilla para uno, y la plantilla del otro? Aproveche el ejemplo de hoy y genere una orden de compra por ejemplo, y el formato del correo es confuso, solo dice: Orden de compra:

[PRUEBA] Shampoo: 20 × 1000
[PRUEBA] Desengrasante: 10 × 2000
[PRUEBA] Limpiavidrios: 5 × 1000
[PRUEBA] Interior: 5 × 800
[PRUEBA] Neumaticos: 3 × 500
[PRUEBA] Microfibras: 12 × 2500 .Los precios no tienen ni punto ni espacio, ni se menciona la hora ni dia de entrega, quien se contacta, nada, demasiado basico.»

S3. «Por otra parte, el estilo sigue estando lejos del estandar del sistema:
las vistas del módulo de Insumos y Stock (incluyendo sus subvistas: Kardex, Proveedores, Órdenes de compra y Solicitudes de cotización) siguen violando el sistema de diseño, la estructura del DOM y los patrones de interacción del proyecto. @Views/Servicio/_ServicioForm.cshtml @Views/Servicio/_ServicioTable.cshtml @Views/Servicio/Index.cshtml @Views/PaqueteServicio/_PaqueteServicioForm.cshtml @Views/PaqueteServicio/_PaqueteServicioTable.cshtml @Views/PaqueteServicio/Index.cshtml @wwwroot/js/servicio.js @wwwroot/js/paqueteservicio.js 
Para hacer esta corrección, inspecciona obligatoriamente el código de [archivo_servicios] y [archivo_paquetes_servicios] y copia exactamente su estructura HTML, clases y componentes.»

S4. «1. Reglas Globales Prohibidas (Eliminar inmediatamente)

PROHIBIDO usar <details> y <summary> nativos de HTML: Elimina todos los triángulos nativos (▶ / ▼) que pusiste en acordeones de registro, grupos de tablas y botones de acciones de fila ("Editar", "Detalle y acciones"). Usa el mismo componente de acordeón con icono chevron a la derecha del título que usa la vista de Servicios ("Registrando un... ^").

PROHIBIDO editar o expandir acciones inline en las filas de la tabla: Al hacer clic en "Editar" en cualquier tabla, NO se debe desplegar nada dentro ni debajo de la fila. El botón "Editar" debe cargar los datos del registro en el formulario principal del acordeón superior, cambiar el título del acordeón a "Editando un [Entidad]" y expandir el acordeón (exactamente como funciona en Servicios y Paquetes).

PROHIBIDO usar enlaces de texto para acciones de tabla: En la columna "ACCIONES" de las tablas, elimina los textos "+ Ajustar", "Editar", "Desactivar" o "Detalle y acciones". Usa únicamente botones cuadrados sólidos con icono adentro (azul con icono de lápiz para editar, rojo con icono de papelera/desactivar para baja, y botones equivalentes con icono para acciones extra como ajustar o ver detalle).

Ancho de tablas y columnas: Las tablas deben ocupar el 100% del ancho del contenedor (w-full) distribuyendo proporcionalmente el espacio entre columnas, con encabezados que incluyan los iconos de ordenamiento (⇅) igual que en Servicios.

Badges de Estado obligatorios: Ninguna columna "ESTADO" (en Insumos, Proveedores, Órdenes o Cotizaciones) puede ser texto plano. Todos los estados deben renderizarse como etiquetas tipo pastilla (badges) con fondo suave y texto de color semántico (verde para Activo/Realizado/Pagado, rojo para Cancelado/Inactivo, amarillo/ámbar para Pendiente/Enviada, gris o azul para otros estados).

Paginación estándar: Elimina la paginación en texto plano ("Anterior | Navegación por cursores | Siguiente"). Usa la barra inferior de paginación del sistema: texto "Mostrando página X de Y" a la izquierda y botonera cuadrada con bordes y flechas (< 1 2 >) a la derecha.

Botones de navegación superior: Los botones para ir a subvistas (Kardex, Proveedores, Órdenes, Cotizaciones) deben ubicarse en la barra de herramientas junto al buscador y filtros, usando el estilo de botón secundario destacado (como el botón violeta de "Paquetes de Servicios"), dejando en la esquina superior derecha del título principal únicamente el botón cuadrado azul de ayuda (?) o el botón "Volver" cuando se está en una subvista.»

S5. «2. Correcciones Específicas por Vista

Vista Principal: Insumos y Stock

Alta y Edición en Acordeón (NO Modal): Elimina el botón "+ Nuevo insumo" y el modal emergente de creación. El registro y edición de insumos debe estar fijo en la parte superior dentro del bloque colapsable "Registrando un Insumo ^", con sus labels arriba de cada input, asteriscos rojos para obligatorios y botón primario azul (no verde).

Gestión de Tipos de Insumo: En lugar de un botón ancho "Gestionar tipos" al lado del selector, usa el patrón del sistema: pega a la derecha del <select> de Tipo de Insumo los dos botones cuadrados compactos: azul (+) para crear tipo y rojo (papelera) para eliminar.

Acciones de Fila y Modales:

Ajustar stock: Al hacer clic en el botón de icono de ajuste en la fila, NO muestres campos inline en la tabla; abre un modal limpio y centrado para ingresar la cantidad y justificación del ajuste.

Desactivar: Al hacer clic en el botón cuadrado rojo de la fila, abre el Modal de Confirmación de Baja estándar del sistema (icono circular rojo con cruz arriba, título "Desactivar [Entidad]", texto "¿Confirma desactivar el [entidad] [Nombre]?", botón blanco "Cancelar" y botón rojo "Desactivar").»

S6. «Subvista: Kardex

Eliminar filtros cuadrados expuestos: Quita las cajas de listas de checkboxes y campos de fecha sueltos arriba de la tabla.

Implementar Toolbar + Popover de Filtros: Coloca la barra de búsqueda a la izquierda y el botón desplegable "Filtros" a la derecha. Al abrir "Filtros", muestra el panel flotante (popover) dividido en columnas con títulos subrayados (Insumo, Tipo de movimiento, Rango de fechas) y pie con "Limpiar filtros" a la izquierda y botón azul "Aplicar Filtros" a la derecha.»

S7. «Subvista: Proveedores

Ajusta el acordeón superior al estilo de Servicios (quita el triángulo azul ▼, usa el estilo de inputs gris claro con bordes redondeados y cambia el botón verde "+ Agregar" por el botón primario azul del sistema).

En la tabla, reemplaza los textos de acciones por los botones cuadrados de icono (azul editar que carga datos arriba en el acordeón, rojo desactivar que abre el modal de confirmación) y ponle el badge verde al estado "Activo".»

S8. «Subvista: Solicitudes de Cotización (Cambio de patrón de UX)

Eliminar listado estático de inputs por insumo: Es inviable mostrar un input numérico por cada insumo existente.

Implementar el selector dinámico de [archivo_paquetes_servicios]:

Dentro del formulario del acordeón, crea una subsección "Insumos de la Solicitud" con un buscador/selector de insumos por nombre y un botón azul + Agregar a la derecha.

Debajo, muestra el bloque "Insumos Seleccionados": una lista de tarjetas/filas horizontales donde cada insumo agregado muestra su número de orden en un círculo azul claro, nombre del insumo en negrita, tipo/unidad debajo, un input numérico compacto a la derecha para indicar la cantidad solicitada, y el botón circular rojo (x) en el extremo derecho para quitarlo de la lista.

En la tabla inferior de solicitudes, aplica badges de color a la columna ESTADO y botones de acción con icono.»

S9. «Subvista: Órdenes de Compra

Aplica badges de color al estado (ej. "Enviada") y reemplaza el <details> inline de "Detalle y acciones" por botones de icono en la columna de acciones que abran un modal limpio con el detalle de la orden y sus botones de aprobación/recepción.»

S10. Operational constraints: preserve permissions, CSRF protection, audit, conversions and existing business contracts. Kardex uses real cursors; totals and page counts may be unknown, so never fabricate pages, totals or arbitrary page access. Use the closest truthful standard pagination compatible with the cursor contract; escalate any unresolved conflict. No real mail or DEV writes. Workers may read all reference files, but must not share edit surfaces. T2 and T3 clients use existing controller endpoints; report missing endpoints to the parent rather than expanding scope. T4 owns controllers.

S11. «Ah y casi me olvido, para agregar los insumos a un servicio, o sea, armar la receta, esta bien, solo falta que los botones tengan los iconos correspondientes y que @"Capturas de pantalla/{4BE5586C-1806-4AC9-98E1-C36795A469F2}.png" el desplegable no muestre tantas unidades de medidas repetidas, debe haber solo una opcion con L,mm, cm y cm3, nada mas, que es eso de que haya 3 versiones de litro, de ultima que diga la unidad de medida, por ejemplo L y entre parentesis Litros, tipo la aclaracion inline a la opcion, nada mas».

The reported screenshot shows duplicate variants `cc`, `cm3`, `Litro (L)`, `litro`, `litros`, `lt`, `mililitro`, `mililitros` and `Mililitro (ml)`. As clarified in L4, present the volume options `L (Litros)`, `ml (Mililitros)` and `cm³ (Centímetros cúbicos)`, without repeated aliases such as `cc`, `litro`, `litros` or `lt`, and without length units. Preserve the existing conversion `1 ml = 1 cm³`, canonical storage, legacy reads, existing data and dimensional boundaries; do not perform unsupported data migration.

## Tasks
Planning | complete | Request and parallel boundaries recorded; planning is not implementation or delivery.

T1 | S1 | verified, bounded | commit `85600df` | Parent reports job53 CSS build exit 0 and job56 Playwright 11/11: fragments of seven real form markups with compiled CSS and Flowbite, both themes and hover/non-hover contrast ratio >= 3, plus click/send interaction. This is not coverage of all served pages or a complete accessibility assessment. Shared CSS and browser form tests remain this unit's surfaces.

T2 | S3–S5,S10 | implemented, verified bounded | commit `2ed8f1b` | Node 16/16 after the hidden-input fix and job60 UI 1/1 reported; ordering by name applies only within a group. This does not establish acceptance of every workflow. Own `Views/Insumos/Index.cshtml`, `Views/Insumos/_InsumoForm.cshtml`, `Views/Insumos/_TiposInsumoModal.cshtml`, and insumos/tipos JavaScript; preserve grouping, totals and conversions.

T3 | S3–S4,S6–S10 | implemented, verified bounded | commit `ee819e9` | Node 13/13 and job60 UI 1/1 reported; numeric/date sorting and selector valid/remove/invalid DOM checks are covered. Kardex search is labeled page-only, not global. This does not establish acceptance of every workflow. Own `Views/Insumos/Kardex.cshtml`, `Views/Insumos/Proveedores.cshtml`, `Views/Insumos/Cotizaciones.cshtml`, `Views/Insumos/Ordenes.cshtml` and dedicated JavaScript; preserve truthful cursor navigation.

T4 | S2,S10 | verified done | commit `beb4340` | Job64 email GREEN 12/12, no skips, 0 warnings and 0 C# errors, after job63 RED HTTP 500: the regression asserts HTTP 400 with the exact error, zero emails and unchanged request and audit counts. Same-worker additional correction 1/1 applied and exhausted; historical source and email fixture budgets remain exhausted 2/2, not reset. Independent `mwijj1fh` reported the prior S10 blocker met and no other severe finding; catalog-name read before transaction is an inherited race advisory and successful manual HTTP was not newly tested. No real mail or DEV writes.

T5 | S1–S11 | delivered locally, verified bounded | Rewritten UI fixture passed job60 1/1 without skips, 0 warnings and 0 errors; UI fixture correction budget 0/2. Product units committed locally; cierre documental en este commit. UI functional evidence is bounded, not proof of all workflow persistence, permissions or accessibility. No push or PR.

T6 | S11 | implemented, verified bounded | commit `69bd76c` | Confirmed L/ml/cm³ presentation implemented; Node 6/6 reported for client handlers and legacy `cc`, and job60 UI 1/1 passed. This does not establish acceptance of every workflow. Own `Views/Servicio/_ServicioForm.cshtml`, `wwwroot/js/servicio.js` and `Firebase.Tests/ServicioRecetaModalConsistency.test.cjs`; preserve persisted units, conversions, data and dimensional boundaries, with no unsupported migration.

## Log
L1. «Pues hubo algunas regresiones y mejoras pero muy pequeñas, lejos de lo que yo quiero. Para empezar, los botones de TODAS las vistas, mas precisamente de los formularios de creacion o edicion, en el modo claro los botones son invisibles hasta que paso el cursor por arriba, recien ahi salen, si tengo mi cursor arriba basicamente. Ya hablando especificamente de la vista. Ahora ire por vistas, en la vista de configuracion, el texto libre de la Plantilla de email es muy confuso, no hay nada que indique ni un formato recomendado ni para que sirve esa plantilla, si es para una solicitud de cotizacion o de compra, y si es una plantilla para uno, y la plantilla del otro? Aproveche el ejemplo de hoy y genere una orden de compra por ejemplo, y el formato del correo es confuso, solo dice: Orden de compra:

[PRUEBA] Shampoo: 20 × 1000
[PRUEBA] Desengrasante: 10 × 2000
[PRUEBA] Limpiavidrios: 5 × 1000
[PRUEBA] Interior: 5 × 800
[PRUEBA] Neumaticos: 3 × 500
[PRUEBA] Microfibras: 12 × 2500 .Los precios no tienen ni punto ni espacio, ni se menciona la hora ni dia de entrega, quien se contacta, nada, demasiado basico. Por otra parte, el estilo sigue estando lejos del estandar del sistema:
las vistas del módulo de Insumos y Stock (incluyendo sus subvistas: Kardex, Proveedores, Órdenes de compra y Solicitudes de cotización) siguen violando el sistema de diseño, la estructura del DOM y los patrones de interacción del proyecto. @Views/Servicio/_ServicioForm.cshtml @Views/Servicio/_ServicioTable.cshtml @Views/Servicio/Index.cshtml @Views/PaqueteServicio/_PaqueteServicioForm.cshtml @Views/PaqueteServicio/_PaqueteServicioTable.cshtml @Views/PaqueteServicio/Index.cshtml @wwwroot/js/servicio.js @wwwroot/js/paqueteservicio.js 
Para hacer esta corrección, inspecciona obligatoriamente el código de [archivo_servicios] y [archivo_paquetes_servicios] y copia exactamente su estructura HTML, clases y componentes.

Aplica de forma estricta las siguientes correcciones globales y por vista:

1. Reglas Globales Prohibidas (Eliminar inmediatamente)

PROHIBIDO usar <details> y <summary> nativos de HTML: Elimina todos los triángulos nativos (▶ / ▼) que pusiste en acordeones de registro, grupos de tablas y botones de acciones de fila ("Editar", "Detalle y acciones"). Usa el mismo componente de acordeón con icono chevron a la derecha del título que usa la vista de Servicios ("Registrando un... ^").

PROHIBIDO editar o expandir acciones inline en las filas de la tabla: Al hacer clic en "Editar" en cualquier tabla, NO se debe desplegar nada dentro ni debajo de la fila. El botón "Editar" debe cargar los datos del registro en el formulario principal del acordeón superior, cambiar el título del acordeón a "Editando un [Entidad]" y expandir el acordeón (exactamente como funciona en Servicios y Paquetes).

PROHIBIDO usar enlaces de texto para acciones de tabla: En la columna "ACCIONES" de las tablas, elimina los textos "+ Ajustar", "Editar", "Desactivar" o "Detalle y acciones". Usa únicamente botones cuadrados sólidos con icono adentro (azul con icono de lápiz para editar, rojo con icono de papelera/desactivar para baja, y botones equivalentes con icono para acciones extra como ajustar o ver detalle).

Ancho de tablas y columnas: Las tablas deben ocupar el 100% del ancho del contenedor (w-full) distribuyendo proporcionalmente el espacio entre columnas, con encabezados que incluyan los iconos de ordenamiento (⇅) igual que en Servicios.

Badges de Estado obligatorios: Ninguna columna "ESTADO" (en Insumos, Proveedores, Órdenes o Cotizaciones) puede ser texto plano. Todos los estados deben renderizarse como etiquetas tipo pastilla (badges) con fondo suave y texto de color semántico (verde para Activo/Realizado/Pagado, rojo para Cancelado/Inactivo, amarillo/ámbar para Pendiente/Enviada, gris o azul para otros estados).

Paginación estándar: Elimina la paginación en texto plano ("Anterior | Navegación por cursores | Siguiente"). Usa la barra inferior de paginación del sistema: texto "Mostrando página X de Y" a la izquierda y botonera cuadrada con bordes y flechas (< 1 2 >) a la derecha.

Botones de navegación superior: Los botones para ir a subvistas (Kardex, Proveedores, Órdenes, Cotizaciones) deben ubicarse en la barra de herramientas junto al buscador y filtros, usando el estilo de botón secundario destacado (como el botón violeta de "Paquetes de Servicios"), dejando en la esquina superior derecha del título principal únicamente el botón cuadrado azul de ayuda (?) o el botón "Volver" cuando se está en una subvista.

2. Correcciones Específicas por Vista

Vista Principal: Insumos y Stock

Alta y Edición en Acordeón (NO Modal): Elimina el botón "+ Nuevo insumo" y el modal emergente de creación. El registro y edición de insumos debe estar fijo en la parte superior dentro del bloque colapsable "Registrando un Insumo ^", con sus labels arriba de cada input, asteriscos rojos para obligatorios y botón primario azul (no verde).

Gestión de Tipos de Insumo: En lugar de un botón ancho "Gestionar tipos" al lado del selector, usa el patrón del sistema: pega a la derecha del <select> de Tipo de Insumo los dos botones cuadrados compactos: azul (+) para crear tipo y rojo (papelera) para eliminar.

Acciones de Fila y Modales:

Ajustar stock: Al hacer clic en el botón de icono de ajuste en la fila, NO muestres campos inline en la tabla; abre un modal limpio y centrado para ingresar la cantidad y justificación del ajuste.

Desactivar: Al hacer clic en el botón cuadrado rojo de la fila, abre el Modal de Confirmación de Baja estándar del sistema (icono circular rojo con cruz arriba, título "Desactivar [Entidad]", texto "¿Confirma desactivar el [entidad] [Nombre]?", botón blanco "Cancelar" y botón rojo "Desactivar").

Subvista: Kardex

Eliminar filtros cuadrados expuestos: Quita las cajas de listas de checkboxes y campos de fecha sueltos arriba de la tabla.

Implementar Toolbar + Popover de Filtros: Coloca la barra de búsqueda a la izquierda y el botón desplegable "Filtros" a la derecha. Al abrir "Filtros", muestra el panel flotante (popover) dividido en columnas con títulos subrayados (Insumo, Tipo de movimiento, Rango de fechas) y pie con "Limpiar filtros" a la izquierda y botón azul "Aplicar Filtros" a la derecha.

Subvista: Proveedores

Ajusta el acordeón superior al estilo de Servicios (quita el triángulo azul ▼, usa el estilo de inputs gris claro con bordes redondeados y cambia el botón verde "+ Agregar" por el botón primario azul del sistema).

En la tabla, reemplaza los textos de acciones por los botones cuadrados de icono (azul editar que carga datos arriba en el acordeón, rojo desactivar que abre el modal de confirmación) y ponle el badge verde al estado "Activo".

Subvista: Solicitudes de Cotización (Cambio de patrón de UX)

Eliminar listado estático de inputs por insumo: Es inviable mostrar un input numérico por cada insumo existente.

Implementar el selector dinámico de [archivo_paquetes_servicios]:

Dentro del formulario del acordeón, crea una subsección "Insumos de la Solicitud" con un buscador/selector de insumos por nombre y un botón azul + Agregar a la derecha.

Debajo, muestra el bloque "Insumos Seleccionados": una lista de tarjetas/filas horizontales donde cada insumo agregado muestra su número de orden en un círculo azul claro, nombre del insumo en negrita, tipo/unidad debajo, un input numérico compacto a la derecha para indicar la cantidad solicitada, y el botón circular rojo (x) en el extremo derecho para quitarlo de la lista.

En la tabla inferior de solicitudes, aplica badges de color a la columna ESTADO y botones de acción con icono.

Subvista: Órdenes de Compra

Aplica badges de color al estado (ej. "Enviada") y reemplaza el <details> inline de "Detalle y acciones" por botones de icono en la columna de acciones que abran un modal limpio con el detalle de la orden y sus botones de aprobación/recepción.»

L2. «Lanza varios agentes en paralelo para ir mas rapido»

L3. «Ah y casi me olvido, para agregar los insumos a un servicio, o sea, armar la receta, esta bien, solo falta que los botones tengan los iconos correspondientes y que @"Capturas de pantalla/{4BE5586C-1806-4AC9-98E1-C36795A469F2}.png" el desplegable no muestre tantas unidades de medidas repetidas, debe haber solo una opcion con L,mm, cm y cm3, nada mas, que es eso de que haya 3 versiones de litro, de ultima que diga la unidad de medida, por ejemplo L y entre parentesis Litros, tipo la aclaracion inline a la opcion, nada mas».

L4. Clarification question: «¿Querías decir L, ml y cm³, con etiquetas como L (Litros) y sin variantes repetidas, o necesitás incorporar realmente milímetros y centímetros?». Human answer: «Si, eso quise decir».

L5. Parent-reported current evidence, not rerun in this documentation update: job53 CSS build exit 0; job56 Playwright 11/11 on seven form-markup fragments as bounded in T1, log `.Temp/gp43gK9J/gentle-jobs-mxz4JO/job-56.log`. T2 Node 15/15 with sorting limited as described; T3 Node 13/13 with page-only Kardex search and browser verification pending; T4 job55 .NET 5/7, two assertions corrected 1/2 without rerun, two emulator tests added and isolated email runner launching without reported outcome; T6 Node 6/6 with browser/server verification pending. T5 fixture rewritten, not yet verified. Except for T1's bounded verification, no task is declared done.

L6. Parent-reported subsequent evidence, not rerun in this documentation update: job60 UI 1/1 passed without skips, 0 warnings and 0 errors, run `2026-11-16T00-01-43-309Z-29552`; job62 email 11/11 passed, 0 skips, 0 warnings and 0 errors, run `email-parity2026-11-16T00-03-21-516Z-23516`. Neither is universal proof. Independent `mwii2tsi` reported Node 38/38 and BLOCKERS 10 on manual RFQ partial persistence at `Controllers/InsumosController.cs:327-344`; job62 verifies the final subject security fix but not that public failure state. T4 source and email fixture budgets each 2/2 exhausted; UI fixture 0/2. The proposed human-authorized continuation and checks are recorded in T4; no automatic cycle or delivery. No children active after this documentation update.

The parent opened four captures: inventory 1280 light, empty quotation 1280 light, inventory edit 390 light and configuration 1280 light. At 390 the inherited navbar covers fields; this is a documented visual issue, not a full accessibility assessment. No commits or push for this request.

L7. «Te autorizo, continua trabajando» — acceptance of the bounded T4 proposal: same worker, one additional correction only for manual RFQ validation before persistence and public HTTP invalid-input coverage with no requests, audits or sender calls; preserve the 11 email tests, focused Node and independent targeted review, with parent running .NET. Historical 2/2 budgets remain exhausted and are not reset. Parent reports no children active before launch; no outcome is claimed.

L8. Parent-reported subsequent evidence, not rerun in this documentation update: job63 RED HTTP 500; job64 GREEN 12/12, no skips, 0 warnings and 0 C# errors, email run `2026-11-16T00-43-32-907Z-27496`, including the public HTTP 400 exact-error and unchanged-state assertions described in T4. The additional T4 correction is 1/1 applied and exhausted; historical failed runs and 2/2 budgets remain intact. Independent `mwijj1fh` found the prior S10 blocker met and no other severe finding, with the inherited catalog-name transaction race advisory and no newly tested successful manual HTTP flow.

Parent reports all current product code unchanged from job60 except the additional controller fix and subject CRLF change, which may postdate its exact manifest. Actual hash audit remains with the parent; do not claim that all current files match the latest UI snapshot. UI functional verification is bounded, not proof of all workflow persistence or accessibility.

Planned local work units on source baseline `92a9694`: CSS with its tests; recipe units with tests; inventory with tests; secondary views with tests; email with tests; documentation last. Email may exceed approximately 400 lines: perform an honest split pass preserving minimal cohesion, without compression or invented exception. RDD assessment and local commit delivery remain pending; there are no commit hashes yet and no push or PR.

L9. Parent-reported boundary before further commits: CSS unit `92a9694..85600df` committed locally, four files and 49 authored diff lines, tests with code. Native assessment MEDIUM, `reviewDue=false`, `under_budget`: review not due, without START, consent or approval. The runtime commit guard counts approximately 22,000 historical branch lines since `origin/main`; delivery concerns current-feature local units since `92a9694`, not an accumulated branch review, and no PR was requested. Parent's latest Node run after the RFQ fix passed 38/38, exit 0, no failures or skips.

Remaining planned units with tests: recipe approximately 80 lines, inventory approximately 300, secondary approximately 450 including new JS and tests, email approximately 450 including new formatter and tests; documentation last. These are estimates, not actual numstat: obtain actual counts and make an honest cohesive split pass, without arbitrary shrinking or an invented exception. Remaining commits and delivery are pending; no push.

L10. Parent-reported committed boundaries: CSS `92a9694..85600df`, 49 authored lines; recipe `85600df..69bd76c`, 80; inventory `69bd76c..2ed8f1b`, 276. Each native assessment was MEDIUM, `reviewDue=false`, `under_budget`, without START or an approval claim.

Secondary unit staged, not yet committed: six files, 317 insertions and 111 deletions, 428 total lines. One honest cohesive splitting pass found the shared 143-line JavaScript serves four views and the combined tests span 127 lines; slicing would require partial JS and test hunks separating the unit and shared pagination. The cohesive 428-line unit is retained, 28 above the approximate 400-line target, without claiming an invented exception; no PR requested. Actual email count is not yet known and remains pending. T5 remains in progress; no overall closeout.

L11. Parent-reported subsequent boundary: secondary unit committed locally as `2ed8f1b..e819e94`, 428 lines; native assessment MEDIUM, review not due. Email unit is staged, ready for commit but without a commit hash: eight files, 394 insertions and 28 deletions, 422 lines. Its cohesive scope joins the 71-line formatter, 147-line test and 61-line HTTP test with shared manual/automatic RFQ and purchase-order wiring, configuration defaults and backward compatibility. An honest cohesive split pass found the formatter cannot be separated from its tests and public integration without breaking the unit; retain 422 lines, 22 above the approximate 400-line target, without inventing an exception. Delivery is local only; no PR.

Parent reports independent `mwijj1fh` checked risk 2 (header injection), risk 3 (shared controller) and risk 4 (partial-send preservation), with latest GREEN 12/12. T5 remains in progress for local delivery; no overall closeout.

L12. Current closeout, superseding the historical task and delivery states above: local product units are T1 `92a9694..85600df` (49 authored lines), T6 `85600df..69bd76c` (80), T2 `69bd76c..2ed8f1b` (276), T3 `2ed8f1b..ee819e9` (428), and T4 `ee819e9..beb4340` (422). Each committed unit's native assessment was MEDIUM, `reviewDue=false`, `under_budget` and review not due, without START, lineage or reviewer approval. Independent high-risk verification by `mwijj1fh` was separate. The L7 authorization «Te autorizo, continua trabajando» supplied one additional correction, used 1/1; job64 GREEN 12/12, no skips or C# errors. Historical failed runs and exhausted budgets remain unchanged.

T5 is delivered locally with bounded verification; cierre documental en este commit, intended message `docs(odd): close verified design parity and mail correction`. No self-hash is claimed. Parent reports configuration distinguishes RFQ and purchase-order templates; unavailable actual delivery date/time is `No informado`, and a quoted delivery period is used when unambiguous, without inventing delivery data. Inventory name sorting is within complete groups only; Kardex search is page-only with cursor totals unknown. The inherited navbar overlap at 390 remains unfixed. No claim of browser database persistence of Servicio, all workflow permissions, full accessibility or universal acceptance is made. The inherited catalog-name race advisory and the snapshot hash limitation in L8 remain. No real email or DEV writes, push or PR. The pre-existing untracked `Capturas de pantalla/` is preserved; no artifact or cache cleanup.
