# Correcciones de Turnos y Lavados
Objetivo: validar lo pendiente del pedido autorizado y corregir los detalles correspondientes, conservando el trabajo existente.

## Specs
S1. "el modal de registro de clientes
  expres no tiene el mismo estilo que el de la vista Lavados"; "los botones tanto ahi, como en el fomrulario de turnos estan
  cortados, sus iconos estan cortados".
S2. "El desplegable tiene una opcion de seleccionar en vez
  de elegir automaticamente el primero de la lista para ambos desplegables"; "el selector de
  paquetes servicios no se bloquea despues de seleccionar un paquete como si ocurre en lavados, su
  contenido tampoco es el mismo, y como se ve, los detalles de los paquetes seleccionados no se cargan
  correctamente". Aclaraciones: "Nono, perdon, el desplegables de clientes nomas, los de paquetes y servicios clonar el funcionamiento de lavados"; "O sea ambas cosas en realidad deben clonar el comportamiento de lavados al fin y al cabo". La intención final es que tanto el selector de clientes como los de paquetes y servicios clonen el comportamiento de Lavados, sin asumir cómo funciona.
S3. "como correccion general de todo el abm, no usa el sistema de notificaciones
  popup del sistema para notificar las acciones".
S4. "Al intentar registrar un lavado con una fecha anterior a
  hoy no tiro ninguna excepcion o aviso, igual directamente no deberia dejar seleccionar una fecha pasada
  en el date picker".
S5. "al registrar el turno me cambio la vista a una que ni
  siquiera deberia existir https://localhost:7059/Turnos/Crear"; "al consultar los detalles del
  turno, sale sin empleados ni nombres de los mismos ni clientes".
S6. "en la tabla de
  la vista de lavados, todo lo que sea nombres sale vacio, ya que directamente no se guardan en la base
  de datos, solo sus ids".
S7. "si en un lavado, intento iniciar una etapa en paralelo (ya que se puede porque sale el
  boton, cosa que no deberia para evitar confusiones), la aplicacion crashea directamente por excepcion
  del servicio, ni mensaje de error ni nada".
S8. "a las columnas les falta ordenamiento, y/o no coinciden ciertos formatos, por ejemplo la columna de patente, filtros de fecha y rango de precios o de que apenas pongo una letra ya salta la notificacion y el mensaje en lugar de ser como lavados".
S9. "la edicion de un turno no debe ser dentro del detalle, sino que en el mismo formulario de Registrar turnos asi como se hace en todas las demas vistas, justamente el boton de editar debe estar afuera en la columna de acciones (manteniendo igual adentro de detalles pero agregar los iconos faltantes). Lo mismo para cancelar el turno, mismo estilo que lavados".
S10. "no hay un permiso de cancelacion para el modulo de Turnos, que recorda que los permisos de Modificar y eliminar requieren Consultar, de paso controla los permisos por defecto de Administrador y empleado (o sea los roles protegidos) para contemplar este nuevo modulo por defecto".

## Tasks
T1 | S1-S4 | done | Validar y corregir formulario, modal, selectores, notificaciones y fechas. (28/28 pruebas Node passing).
T2 | S3,S5,S8-S9 | done | Validar y corregir tabla, navegación, edición y cancelación. Paginación y denormalización de campos implementada matching Lavados.
T3 | S5-S7 | done | Validar y corregir nombres en el flujo Turno–Lavado y manejo de etapas sin crash.
T4 | S10 | done | Validar y corregir permisos y roles protegidos (AgregarPermisosTurnosRolesBaseAsync implementado y cubierto con tests).

## Log
L1. Solicitud original:

```text
Ahi ya tengo tokens, de igual manera, ya termine el modulo, le pedi a gemini, esta funcional, solo quedaron detalles. Le pase el siguiente prompt y lo hizo pero algunas cosas de forma incompleta y otras directamente no. Quiero que valides lo que falta hacer del siguiente prompt, me digas y ahi te autorizo a hacerlo.
Bueno, ahi me puse a probar el sistema, varios detalles a corregir, el modal de registro de clientes
  expres no tiene el mismo estilo que el de la vista Lavados @[Capturas de pantalla/Modal de registro
  expres cliente turnos.png] ,ademas que los botones tanto ahi, como en el fomrulario de turnos estan
  cortados, sus iconos estan cortados. Despues sigue habiendo diferencias visuales de con Lavados como se
  ve en las capturas @[Capturas de pantalla/Seleccion de Servicios en Turnos.png]  @[Capturas de
  pantalla/Seleccion de Servicios en Lavados.png] .El desplegable tiene una opcion de seleccionar en vez
  de elegir automaticamente el primero de la lista para ambos desplegables, ademas de que el selector de
  paquetes servicios no se bloquea despues de seleccionar un paquete como si ocurre en lavados, su
  contenido tampoco es el mismo, y como se ve, los detalles de los paquetes seleccionados no se cargan
  correctamente. Despues como correccion general de todo el abm, no usa el sistema de notificaciones
  popup del sistema para notificar las acciones. Al intentar registrar un lavado con una fecha anterior a
  hoy no tiro ninguna excepcion o aviso, igual directamente no deberia dejar seleccionar una fecha pasada
  en el date picker. Despues, por alguna razon, al registrar el turno me cambio la vista a una que ni
  siquiera deberia existir https://localhost:7059/Turnos/Crear, y ahi al consultar los detalles del
  turno, sale sin empleados ni nombres de los mismos ni clientes @[Capturas de pantalla/Turno convertido
  en Lavado.png] .Si eso lo convierto en lavado al iniciar el lavado en si, para empezar, en la tabla de
  la vista de lavados, todo lo que sea nombres sale vacio, ya que directamente no se guardan en la base
  de datos, solo sus ids como se ve en @[Capturas de pantalla/Lavado sin nombres por la conversion de
  turno.png] .
  Por otra parte, si en un lavado, intento iniciar una etapa en paralelo (ya que se puede porque sale el
  boton, cosa que no deberia para evitar confusiones), la aplicacion crashea directamente por excepcion
  del servicio, ni mensaje de error ni nada, directo al trow del lavado con:
  System.InvalidOperationException: 'No se puede iniciar esta etapa porque la etapa 'Enjuage inicial'
  está en proceso. Las etapas deben completarse en orden.'

Adicional a eso, algunas pruebas de que no quedo igual como se ve en @"Capturas de pantalla/Registro de Turnos.png" y @"Capturas de pantalla/Registro de lavados.png" ,a las columnas les falta ordenamiento, y/o no coinciden ciertos formatos, por ejemplo la columna de patente, filtros de fecha y rango de precios o de que apenas pongo una letra ya salta la notificacion y el mensaje en lugar de ser como lavados. Y un detalle adicional que olvide mencionar el prompt de arriba, es que la edicion de un turno no debe ser dentro del detalle, sino que en el mismo formulario de Registrar turnos asi como se hace en todas las demas vistas, justamente el boton de editar debe estar afuera en la columna de acciones (manteniendo igual adentro de detalles pero agregar los iconos faltantes). Lo mismo para cancelar el turno, mismo estilo que lavados. Ah y sobre roles y permisos, no hay un permiso de cancelacion para el modulo de Turnos, que recorda que los permisos de Modificar y eliminar requieren Consultar, de paso controla los permisos por defecto de Administrador y empleado (o sea los roles protegidos) para contemplar este nuevo modulo por defecto
```

L2. Autorización: "Te autorizo a avanzar con todo eso".

L3. Aclaración: "Nono, perdon, el desplegables de clientes nomas, los de paquetes y servicios clonar el funcionamiento de lavados".

L4. Aclaración: "O sea ambas cosas en realidad deben clonar el comportamiento de lavados al fin y al cabo".

L5. Denormalización y paginación autorizada: "Si, denormalizacion, igual que en Lavado por favor". Implementada la denormalización de lectura (ClienteNombre, ClienteTrajoNombre, ClienteRetiraNombre, VehiculoPatente, TipoVehiculo) en Turno y VehiculoEnTurno, paginación idéntica a Lavados en TurnosController y _TurnoTable, y actualización de roles protegidos transaccional con auditoría.

L6. Correcciones finales autorizadas:
- Validación de paquetes con mensaje "No se puede agregar el paquete completo por servicios duplicados, incompatibles o ya seleccionados." portada a Lavados.
- Filtros de estado de turnos reparados en frontend y backend, contemplando el estado "EnProceso" para que los turnos convertidos no desaparezcan.
- Validación de horario comercial antes de iniciar un lavado desde Turnos (`EstaEnHorarioDeAtencion`).
- Eliminación de `RechazarCancelacionVinculada` en `LavadoService`, permitiendo cancelar lavados, servicios o etapas vinculadas a turnos y sincronizando el estado del turno vinculado.
- Soporte de consulta y captura de consumo de insumos en cancelación de etapas.
- Habilitación de tipeo directo en campos fecha y hora de turnos (eliminación de `showPicker` forzado).
- Reorganización visual del formulario de turnos según `Capturas de pantalla\Idea de distribucion de Turnos.png` (grilla responsiva con programación a la par de resumen).
- Corrección de colores e iconos en botones de la tabla y formulario de edición.
