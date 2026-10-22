# LavaFácil

## *Sistema de Gestión de Servicios y Turnos para Lavaderos de Vehículos*

 

**Documento de Requisitos del Sistema**

**Versión 2.0**

**Fecha:** 10/01/2026

**Realizado por:** Gelabert André

## **Lista de Cambios**

| Nro | Fecha | Descripción | Autor |
| :---- | :---- | :---- | :---- |
| 0 | 08/09/2025 | Versión 1.0 \- Documento inicial | Gelabert André |
| 1 | 10/01/2026 | Versión 2.0 \- Actualización completa del documento | Gelabert André |
| 2 | 05/07/2026 | Versión 3.0 \- Refactorizacion de todos los casos de uso | Gelabert André |

## **Índice**

[Lista de Cambios	1](#heading=)

[Índice	1](#heading=)

[Presentación General	5](#heading=)

[Participantes del Proyecto	6](#heading=)

[Objetivos del Sistema	6](#heading=)

[Diagrama de Caso de Uso del Sistema	9](#heading=)

[Calificación de los Casos de Uso	10](#heading=)

[Diagrama de subsistema	13](#heading=)

[Descripción de Subsistema	13](#heading=)

[Requisitos del Sistema	14](#heading=)

[Requisitos de Información	14](#heading=)

[Requisitos Funcionales	16](#heading=)

[Descripción de Actores	16](#heading=)

[Casos de Uso del Sistema (Detalle)	16](#heading=)

[Módulo: Seguridad	16](#heading=)

[CU-001 \- Iniciar Sesión	16](#cu-001---iniciar-sesión)

[CU-001.1 \- Iniciar sesión con correo y contraseña	17](#cu-001.1---iniciar-sesión-con-correo-y-contraseña)

[CU-001.2 \- Iniciar sesión con Google	18](#cu-001.2---iniciar-sesión-con-google)

[CU-002 \- Cerrar sesión	18](#cu-002---cerrar-sesión)

[CU-003 \- Recuperar contraseña	19](#cu-003---recuperar-contraseña)

[CU-004 \- Cierre de sesión automático por inactividad	19](#cu-004---cierre-de-sesión-automático-por-inactividad)

[Módulo: Gestión de Empleados	20](#heading=)

[CU-005 \- Registrarse en el sistema	20](#cu-005---registrarse-en-el-sistema)

[CU-005.1 \- Registrarse por correo	21](#cu-005.1---registrarse-por-correo)

[CU-005.2 \- Registrarse por Google	21](#cu-005.2---registrarse-por-google)

[CU-006 \- Modificar empleado	22](#cu-006---modificar-empleado)

[CU-007 \- Desactivar empleado	23](#cu-007---desactivar-empleado)

[CU-008 \- Reactivar empleado	23](#cu-008---reactivar-empleado)

[CU-009 \- Consultar empleados	24](#cu-009---consultar-empleados)

[CU-010 \- Asignar roles a empleados	25](#cu-010---asignar-roles-a-empleados)

[CU-011 \- Autenticar usuario con Google y registrar perfil si es nuevo	25](#cu-011---autenticar-usuario-con-google-y-registrar-perfil-si-es-nuevo)

[Módulo: Gestión de Clientes y Vehículos	26](#heading=)

[CU-012 \- Crear cliente	26](#cu-012---crear-cliente)

[CU-013 \- Modificar cliente	27](#cu-013---modificar-cliente)

[CU-014 \- Desactivar cliente	28](#cu-014---desactivar-cliente)

[CU-015 \- Reactivar cliente	29](#cu-015---reactivar-cliente)

[CU-016 \- Consultar clientes	30](#cu-016---consultar-clientes)

[CU-017 \- Buscar clientes	30](#cu-017---buscar-clientes)

[CU-018 \- Crear vehículo	31](#cu-018---crear-vehículo)

[CU-019 \- Modificar vehículo	32](#cu-019---modificar-vehículo)

[CU-020 \- Desactivar vehículo	32](#cu-020---desactivar-vehículo)

[CU-021 \- Reactivar vehículo	33](#cu-021---reactivar-vehículo)

[CU-022 \- Consultar vehículos	34](#cu-022---consultar-vehículos)

[CU-023 \- Buscar vehículos	34](#cu-023---buscar-vehículos)

[CU-024 \- Vincular vehículo a cliente	35](#cu-024---vincular-vehículo-a-cliente)

[CU-025 \- Desvincular vehículo de cliente	36](#cu-025---desvincular-vehículo-de-cliente)

[CU-026 \- Registrarse como cliente por WhatsApp	36](#cu-026---registrarse-como-cliente-por-whatsapp)

[CU-027 \- Registrar vehículo por WhatsApp	37](#cu-027---registrar-vehículo-por-whatsapp)

[CU-028 \- Identificar si el número de teléfono está registrado	38](#cu-028---identificar-si-el-número-de-teléfono-está-registrado)

[CU-029 \- Editar datos personales por WhatsApp	38](#cu-029---editar-datos-personales-por-whatsapp)

[Módulo: Gestión de Servicios	39](#heading=)

[CU-030 \- Crear servicio	39](#cu-030---crear-servicio)

[CU-031 \- Modificar servicio	40](#cu-031---modificar-servicio)

[CU-032 \- Desactivar servicio	41](#cu-032---desactivar-servicio)

[CU-033 \- Reactivar servicio	42](#cu-033---reactivar-servicio)

[CU-034 \- Consultar servicios	42](#cu-034---consultar-servicios)

[CU-035 \- Buscar servicios	43](#cu-035---buscar-servicios)

[CU-036 \- Crear tipo de servicio	43](#cu-036---crear-tipo-de-servicio)

[CU-037 \- Eliminar tipo de servicio	44](#cu-037---eliminar-tipo-de-servicio)

[CU-038 \- Crear tipo de vehículo	45](#cu-038---crear-tipo-de-vehículo)

[CU-039 \- Eliminar tipo de vehículo	45](#cu-039---eliminar-tipo-de-vehículo)

[CU-040 \- Gestionar etapas del servicio	46](#cu-040---gestionar-etapas-del-servicio)

[CU-041 \- Crear paquete de servicios	47](#cu-041---crear-paquete-de-servicios)

[CU-042 \- Modificar paquete de servicios	47](#cu-042---modificar-paquete-de-servicios)

[CU-043 \- Desactivar paquete de servicios	48](#cu-043---desactivar-paquete-de-servicios)

[CU-044 \- Reactivar paquete de servicios	49](#cu-044---reactivar-paquete-de-servicios)

[CU-045 \- Consultar paquetes de servicios	49](#cu-045---consultar-paquetes-de-servicios)

[CU-111 \- Actualizar precios masivamente	50](#cu-111---actualizar-precios-masivamente)

[CU-112 \- Aplicar actualizaciones programadas de precios	51](#cu-112---aplicar-actualizaciones-programadas-de-precios)

[CU-113 \- Cancelar actualización programada de precios	52](#cu-113---cancelar-actualización-programada-de-precios)

[Módulo: Registro de Lavados	53](#heading=)

[CU-046 \- Registrar realización de un servicio (lavado)	53](#cu-046---registrar-realización-de-un-servicio-\(lavado\))

[CU-047 \- Consultar lavados	54](#cu-047---consultar-lavados)

[CU-048 \- Buscar lavados	55](#cu-048---buscar-lavados)

[CU-049 \- Ver detalle de lavado	55](#cu-049---ver-detalle-de-lavado)

[CU-050 \- Iniciar servicio en lavado	56](#cu-050---iniciar-servicio-en-lavado)

[CU-051 \- Iniciar etapa de servicio	57](#cu-051---iniciar-etapa-de-servicio)

[CU-052 \- Finalizar etapa de servicio	57](#cu-052---finalizar-etapa-de-servicio)

[CU-053 \- Finalizar servicio en lavado	58](#cu-053---finalizar-servicio-en-lavado)

[CU-054 \- Finalizar lavado completo	59](#cu-054---finalizar-lavado-completo)

[CU-055 \- Cancelar lavado	60](#cu-055---cancelar-lavado)

[CU-056 \- Cancelar servicio en lavado	61](#cu-056---cancelar-servicio-en-lavado)

[CU-057 \- Registrar pago recibido	61](#cu-057---registrar-pago-recibido)

[CU-058 \- Registrar pago parcial	62](#cu-058---registrar-pago-parcial)

[CU-059 \- Marcar vehículo como retirado	63](#cu-059---marcar-vehículo-como-retirado)

[CU-060 \- Calcular duración estimada de lavado	63](#cu-060---calcular-duración-estimada-de-lavado)

[Módulo: Configuración	64](#heading=)

[CU-061 \- Configurar horarios del lavadero	64](#cu-061---configurar-horarios-del-lavadero)

[CU-062 \- Configurar capacidad concurrente	65](#cu-062---configurar-capacidad-concurrente)

[CU-063 \- Configurar tiempos de tolerancia y notificación	65](#cu-063---configurar-tiempos-de-tolerancia-y-notificación)

[CU-064 \- Configurar duración de sesión	66](#cu-064---configurar-duración-de-sesión)

[CU-065 \- Configurar nombre y ubicación del lavadero	66](#cu-065---configurar-nombre-y-ubicación-del-lavadero)

[CU-066 \- Configurar paso de descuento para paquetes	67](#cu-066---configurar-paso-de-descuento-para-paquetes)

[Módulo: Planificación de Turnos	68](#heading=)

[CU-067 \- Registrar turno	68](#cu-067---registrar-turno)

[CU-068 \- Modificar turno	68](#cu-068---modificar-turno)

[CU-069 \- Consultar turnos asignados	69](#cu-069---consultar-turnos-asignados)

[CU-070 \- Cancelar turno	70](#cu-070---cancelar-turno)

[CU-071 \- Solicitar turno por WhatsApp	70](#cu-071---solicitar-turno-por-whatsapp)

[CU-072 \- Consultar turnos próximos por WhatsApp	71](#cu-072---consultar-turnos-próximos-por-whatsapp)

[CU-073 \- Cancelar turno por WhatsApp	72](#cu-073---cancelar-turno-por-whatsapp)

[CU-074 \- Asignar turno automáticamente sin superposición	72](#cu-074---asignar-turno-automáticamente-sin-superposición)

[CU-075 \- Validar disponibilidad al mover un turno	73](#cu-075---validar-disponibilidad-al-mover-un-turno)

[CU-076 \- Reorganizar agenda ante cancelaciones	74](#cu-076---reorganizar-agenda-ante-cancelaciones)

[CU-077 \- Enviar notificación por WhatsApp	74](#cu-077---enviar-notificación-por-whatsapp)

[CU-078 \- Enviar notificación por correo electrónico	75](#cu-078---enviar-notificación-por-correo-electrónico)

[CU-079 \- Notificar etapa finalizada	76](#cu-079---notificar-etapa-finalizada)

[CU-080 \- Notificar lavado finalizado	76](#cu-080---notificar-lavado-finalizado)

[CU-081 \- Solicitar hablar con el personal	77](#cu-081---solicitar-hablar-con-el-personal)

[Módulo: Estadísticas y Reportes	77](#heading=)

[CU-082 \- Consultar estadísticas básicas	77](#cu-082---consultar-estadísticas-básicas)

[CU-083 \- Consultar historial de pagos	78](#cu-083---consultar-historial-de-pagos)

[CU-084 \- Generar reportes	79](#cu-084---generar-reportes)

[CU-084.1 \- Exportar reportes a PDF o Excel	79](#cu-084.1---exportar-reportes-a-pdf-o-excel)

[Módulo: Auditoría	80](#heading=)

[CU-085 \- Consultar historial de auditoría	80](#cu-085---consultar-historial-de-auditoría)

[CU-086 \- Filtrar registros de auditoría	81](#cu-086---filtrar-registros-de-auditoría)

[CU-087 \- Ver detalle de registro de auditoría	81](#cu-087---ver-detalle-de-registro-de-auditoría)

[CU-088 \- Registrar todas las acciones para auditoría	82](#cu-088---registrar-todas-las-acciones-para-auditoría)

[CU-089 \- Crear rol	82](#cu-089---crear-rol)

[CU-090 \- Modificar rol	83](#cu-090---modificar-rol)

[CU-091 \- Consultar roles	84](#cu-091---consultar-roles)

[CU-092 \- Eliminar rol	85](#cu-092---eliminar-rol)

[Módulo: Gestión de Insumos y Stock	85](#heading=)

[CU-093 \- Crear insumo	85](#heading=)

[CU-094 \- Modificar insumo	86](#heading=)

[CU-095 \- Desactivar insumo	87](#heading=)

[CU-096 \- Reactivar insumo	87](#heading=)

[CU-097 \- Consultar inventario y alertas de stock	88](#heading=)

[CU-098 \- Crear proveedor	89](#heading=)

[CU-099 \- Modificar proveedor	89](#heading=)

[CU-100 \- Desactivar proveedor	90](#heading=)

[CU-101 \- Reactivar proveedor	91](#heading=)

[CU-102 \- Consultar proveedores	91](#heading=)

[CU-103 \- Registrar ingreso manual de insumos	92](#heading=)

[CU-104 \- Registrar egreso manual / baja de insumo	93](#heading=)

[CU-105 \- Consultar movimientos de stock	94](#heading=)

[CU-106 \- Disparar orden de compra automática	94](#heading=)

[CU-107 \- Generar orden de compra manual	95](#heading=)

[CU-108 \- Consultar y gestionar órdenes de compra	96](#heading=)

[CU-109 \- Registrar recepción de orden de compra	97](#heading=)

[CU-110 \- Configurar directivas de reabastecimiento automático	97](#heading=)

[CU-114 \- Generar solicitud de cotización	98](#cu-114---generar-solicitud-de-cotización)

[CU-115 \- Registrar respuesta de cotización de proveedor	99](#cu-115---registrar-respuesta-de-cotización-de-proveedor)

[CU-116 \- Adjudicar cotización y generar orden de compra	100](#cu-116---adjudicar-cotización-y-generar-orden-de-compra)

[Requisitos No Funcionales	101](#heading=)

[Matriz de Rastreabilidad: Objetivos vs Requisitos de Información	102](#heading=)

[Glosario de Términos	103](#heading=)

[Apéndice: Diagrama de Casos de Uso por Actor	104](#heading=)

[Figura 2 \- Diagrama Caso Usos del actor "Administrador"	104](#heading=)

[Figura 3 \- Diagrama Caso Usos del actor "Trabajador"	105](#heading=)

[Figura 4 \- Diagrama Caso Usos del actor "Cliente"	105](#heading=)

[Figura 5 \- Diagrama Caso Usos del actor "Sistema"	106](#heading=)

## **Presentación General**

Los lavaderos de autos pequeños y medianos enfrentan problemas operativos recurrentes que afectan su productividad y la calidad del servicio ofrecido. Entre las dificultades detectadas se encuentran:

* Gestión manual e ineficiente de turnos, lo que provoca solapamientos y tiempos de espera prolongados.

* Falta de visibilidad en la asignación de recursos humanos (quién está haciendo qué y cuándo).

* Carencia de herramientas para estimar correctamente la duración de los lavados según el tipo de vehículo y los servicios solicitados.

* Comunicación deficiente con los clientes y ausencia de estrategias automatizadas para reducir huecos en la agenda.

* Falta de registro formal de pagos y recibos, así como de trazabilidad de quién realizó cada acción.

* Ausencia de reportes e indicadores que permitan medir ocupación, rendimiento y cumplimiento de turnos.

Ante estos problemas, LavaFacil ofrece una solución integral que:

* Digitaliza y centraliza la gestión de turnos, evitando superposiciones y mejorando la organización de la agenda.

* Permite gestionar los clientes y su flota de vehiculos en un mismo lugar, incluso con soporte multipropietario.

* Calcula automáticamente la duración estimada de un lavado según los servicios y el tipo de vehículo.

* Integra canales de comunicación con clientes (WhatsApp y correo) para notificaciones automáticas y confirmaciones.

* Registra de manera estructurada pagos y acciones realizadas, garantizando trazabilidad.

* Genera reportes e indicadores clave para evaluar el rendimiento del lavadero y apoyar la toma de decisiones.

## **Participantes del Proyecto**

**Desarrolladores:**

* Gelabert André

## **Objetivos del Sistema**

| Código | Objetivo | Descripción | Estabilidad | Comentarios |
| :---- | :---- | :---- | :---- | :---- |
| **OBJ–01** | **Gestión de Empleados** | Permitir la creación, modificación, consulta, desactivación y reactivación de empleados. Incluye la asignación de roles (Administrador/Empleado) y la integración con Google Authentication para inicio de sesión. | Alta | Fundamental para el control de accesos y la gestión interna del personal. |
| **OBJ–02** | **Gestión de Clientes y Vehículos** | Centralizar el registro y consulta de clientes y sus vehículos, incluyendo creación, modificación, desactivación y reactivación. Soporta vehículos con múltiples dueños mediante clave de asociación y vinculación/desvinculación de vehículos a clientes. | Alta | Es la base de datos principal para vincular los servicios y lavados del lavadero. |
| **OBJ–03** | **Gestión de Servicios y Paquetes** | Administrar servicios individuales y paquetes de servicios con descuentos configurables. Incluye la definición de duración estimada según tipo de vehículo, etapas del servicio, tipos de servicio y tipos de vehículo personalizables. | Alta | Se vincula con la estimación automática de la duración de lavados. |
| **OBJ–04** | **Registro y Gestión de Lavados** | Registrar y gestionar lavados incluyendo inicio, seguimiento de etapas por servicio, finalización parcial o total, cancelación de servicios individuales o del lavado completo, y registro de pagos. | Alta | Es el núcleo operativo del sistema y crítico para el negocio. |
| **OBJ–05** | **Registro de Pagos** | Permitir al personal registrar pagos totales y parciales, gestionar estados de pago y mantener historial de pagos por lavado. | Alta | Se integra con el historial de clientes y la auditoría. |
| **OBJ–06** | **Planificación y Gestión de Turnos** | Asignar turnos evitando solapamientos, reorganizar automáticamente ante cancelaciones y permitir solicitudes de turno por WhatsApp. Incluye validación de disponibilidad y notificación de adelantos. | Alta | Permite organizar la agenda del lavadero de forma eficiente. |
| **OBJ–07** | **Registro de Auditoría** | Registrar automáticamente todas las acciones realizadas en el sistema para control y trazabilidad. Incluye filtros por fecha, tipo de acción, tipo de entidad y usuario. | Alta | Garantiza transparencia y control administrativo. |
| **OBJ–08** | **Módulo de Estadísticas y Reportes** | Generar reportes e indicadores clave sobre clientes, servicios, lavados y pagos, exportables en PDF o Excel. | Media | Permite análisis de desempeño y apoyo en la toma de decisiones. |
| **OBJ–09** | **Gestión de Seguridad** | Gestionar la autenticación mediante correo/contraseña o Google Authentication. Incluye verificación de email, cierre de sesión manual y automático por inactividad, y recuperación de contraseñas. | Alta | Indispensable para proteger la información del sistema y la privacidad de los usuarios. |
| **OBJ–10** | **Integración con WhatsApp** | Permitir a los clientes interactuar con el sistema a través de WhatsApp para registrarse, registrar vehículos, editar datos personales, gestionar turnos, consultar información del lavadero y comunicarse con el personal. | Alta | Mejora la experiencia del cliente y reduce la carga operativa del personal. |
| **OBJ–11** | **Gestión de Configuración del Sistema** | Permitir al administrador configurar parámetros del sistema como horarios de operación, información del lavadero, capacidad máxima, tiempos de tolerancia, duración de sesión y configuración de descuentos para paquetes. | Alta | Centraliza la configuración operativa del lavadero. |
| **OBJ–12** | **Notificación al Cliente** | Enviar notificaciones automáticas y manuales a los clientes por WhatsApp y correo electrónico sobre el estado de sus servicios, etapas finalizadas y vehículos listos para retirar. | Media | Mejora la comunicación con el cliente y la experiencia de servicio. |
| **OBJ–13** | **Gestión de Insumos y Stock** | Administrar el inventario de insumos químicos, consumibles y herramientas del lavadero, centralizar los datos de proveedores comerciales, controlar las existencias en tiempo real mediante el registro y cómputo semiautomático del consumo en etapas operativas, y gestionar el reabastecimiento manual y automatizado ante niveles críticos de inventario. | Alta | Esencial para el control de costos operativos, cálculo del margen bruto real por servicio y prevención del desabastecimiento operativo. |

## **Diagrama de Caso de Uso del Sistema**

A continuación, se presenta el modelo de casos de uso general del sistema.

![][image1]

## **Calificación de los Casos de Uso**

Se establece una lista de los casos de uso al cual se le califica de acuerdo a la importancia y los casos de uso críticos.

| Caso de Uso | Importancia |
| :---- | :---- |
| **Módulo Seguridad** |  |
| CU-001 \- Iniciar Sesión | Alta |
| CU-001. 1 \- Iniciar sesión con correo y contraseña | Alta |
| CU-001.2 \- Iniciar sesión con Google | Alta |
| CU-002 \- Cerrar sesión | Media |
| CU-003 \- Recuperar contraseña | Alta |
| CU-004 \- Cierre de sesión automático por inactividad | Media |
| **Módulo Gestión de Empleados** |  |
| CU-005 \- Registrarse en el sistema | Alta |
| CU-005.1 \- Registrarse por correo | Alta |
| CU-005.2 \- Registrarse por Google | Alta |
| CU-006 \- Modificar empleado | Media |
| CU-007 \- Desactivar empleado | Media |
| CU-008 \- Reactivar empleado | Media |
| CU-009 \- Consultar empleados | Media |
| CU-010 \- Asignar roles a empleados | Alta |
| CU-011 \- Autenticar usuario con Google y registrar perfil si es nuevo | Alta |
| **Módulo Gestión de Clientes y Vehículos** |  |
| CU-012 \- Crear cliente | Alta |
| CU-013 \- Modificar cliente | Media |
| CU-014 \- Desactivar cliente | Media |
| CU-015 \- Reactivar cliente | Media |
| CU-016 \- Consultar clientes | Alta |
| CU-017 \- Buscar clientes | Alta |
| CU-018 \- Crear vehículo | Alta |
| CU-019 \- Modificar vehículo | Media |
| CU-020 \- Desactivar vehículo | Media |
| CU-021 \- Reactivar vehículo | Media |
| CU-022 \- Consultar vehículos | Alta |
| CU-023 \- Buscar vehículos | Alta |
| CU-024 \- Vincular vehículo a cliente | Alta |
| CU-025 \- Desvincular vehículo de cliente | Media |
| CU-026 \- Registrarse como cliente por WhatsApp | Alta |
| CU-027 \- Registrar vehículo por WhatsApp | Alta |
| CU-028 \- Identificar si el número de teléfono está registrado | Alta |
| CU-029 \- Editar datos personales por WhatsApp | Media |
| **Módulo Gestión de Servicios** |  |
| CU-030 \- Crear servicio | Alta |
| CU-031 \- Modificar servicio | Media |
| CU-032 \- Desactivar servicio | Media |
| CU-033 \- Reactivar servicio | Media |
| CU-034 \- Consultar servicios | Alta |
| CU-035 \- Buscar servicios | Media |
| CU-036 \- Crear tipo de servicio | Media |
| CU-037 \- Eliminar tipo de servicio | Baja |
| CU-038 \- Crear tipo de vehículo | Media |
| CU-039 \- Eliminar tipo de vehículo | Baja |
| CU-040 \- Gestionar etapas del servicio | Media |
| CU-041 \- Crear paquete de servicios | Media |
| CU-042 \- Modificar paquete de servicios | Baja |
| CU-043 \- Desactivar paquete de servicios | Baja |
| CU-044 \- Reactivar paquete de servicios | Baja |
| CU-045 \- Consultar paquetes de servicios | Media |
| CU-111 \- Actualizar precios masivamente | Media |
| CU-112 \- Aplicar actualizaciones programadas de precios | Media |
| CU-113 \- Cancelar actualización programada de precios | Baja |
| **Módulo Registro de Lavados** |  |
| CU-046 \- Registrar realización de un servicio (lavado) | Alta |
| CU-047 \- Consultar lavados | Alta |
| CU-048 \- Buscar lavados | Alta |
| CU-049 \- Ver detalle de lavado | Alta |
| CU-050 \- Iniciar servicio en lavado | Alta |
| CU-051 \- Iniciar etapa de servicio | Alta |
| CU-052 \- Finalizar etapa de servicio | Alta |
| CU-053 \- Finalizar servicio en lavado | Alta |
| CU-054 \- Finalizar lavado completo | Alta |
| CU-055 \- Cancelar lavado | Alta |
| CU-056 \- Cancelar servicio en lavado | Alta |
| CU-057 \- Registrar pago recibido | Alta |
| CU-058 \- Registrar pago parcial | Alta |
| CU-059 \- Marcar vehículo como retirado | Alta |
| CU-060 \- Calcular duración estimada de lavado | Alta |
| **Módulo Configuración** |  |
| CU-061 \- Configurar horarios del lavadero | Media |
| CU-062 \- Configurar capacidad concurrente | Media |
| CU-063 \- Configurar tiempos de tolerancia y notificación | Media |
| CU-064 \- Configurar duración de sesión | Baja |
| CU-065 \- Configurar nombre y ubicación del lavadero | Media |
| CU-066 \- Configurar paso de descuento para paquetes | Baja |
| **Módulo Planificación de Turnos** |  |
| CU-067 \- Registrar turno | Alta |
| CU-068 \- Modificar turno | Alta |
| CU-069 \- Consultar turnos asignados | Alta |
| CU-070 \- Cancelar turno | Alta |
| CU-071 \- Solicitar turno por WhatsApp | Alta |
| CU-072 \- Consultar turnos próximos por WhatsApp | Alta |
| CU-073 \- Cancelar turno por WhatsApp | Alta |
| CU-074 \- Asignar turno automáticamente sin superposición | Alta |
| CU-075 \- Validar disponibilidad al mover un turno | Alta |
| CU-076 \- Reorganizar agenda ante cancelaciones | Alta |
| **Módulo Notificación al Cliente** |  |
| CU-077 \- Enviar notificación por WhatsApp | Media |
| CU-078 \- Enviar notificación por correo electrónico | Media |
| CU-079 \- Notificar etapa finalizada | Alta |
| CU-080 \- Notificar lavado finalizado | Alta |
| CU-081 \- Solicitar hablar con el personal | Baja |
| **Módulo Estadísticas y Reportes** |  |
| CU-082 \- Consultar estadísticas básicas | Alta |
| CU-083 \- Consultar historial de pagos | Alta |
| CU-084 \- Generar reportes | Alta |
| CU-084.1 \- Exportar reportes a PDF o Excel | Media |
| **Módulo Auditoría** |  |
| CU-085 \- Consultar historial de auditoría | Alta |
| CU-086 \- Filtrar registros de auditoría | Media |
| CU-087 \- Ver detalle de registro de auditoría | Media |
| CU-088 \- Registrar todas las acciones para auditoría | Alta |
| **Módulo Gestión de Roles y Permisos** |  |
| CU-089 \- Crear rol | Media |
| CU-090 \- Modificar rol | Media |
| CU-091 \- Consultar roles | Media |
| CU-092 \- Eliminar rol | Media |
| **Módulo Gestión de Insumos y Stock** |  |
| CU-093 \- Crear insumo | Alta |
| CU-094 \- Modificar insumo | Media |
| CU-095 \- Desactivar insumo | Media |
| CU-096 \- Reactivar insumo | Baja |
| CU-097 \- Consultar inventario y alertas de stock | Alta |
| CU-098 \- Crear proveedor | Alta |
| CU-099 \- Modificar proveedor | Media |
| CU-100 \- Desactivar proveedor | Media |
| CU-101 \- Reactivar proveedor | Baja |
| CU-102 \- Consultar proveedores | Media |
| CU-103 \- Registrar ingreso manual de insumos | Alta |
| CU-104 \- Registrar egreso manual / baja de insumo | Alta |
| CU-105 \- Consultar movimientos de stock | Alta |
| CU-106 \- Disparar orden de compra automática | Alta |
| CU-107 \- Generar orden de compra manual | Media |
| CU-108 \- Consultar y gestionar órdenes de compra | Alta |
| CU-109 \- Registrar recepción de orden de compra | Alta |
| CU-110 \- Configurar directivas de reabastecimiento automático | Media |
| CU-114 \- Generar solicitud de cotización | Alta |
| CU-115 \- Registrar respuesta de cotización de proveedor | Alta |
| CU-116 \- Adjudicar cotización y generar orden de compra | Alta |

 

## **Diagrama de subsistema**

![][image2]

### **Descripción de Subsistema**

| Subsistema | Descripción |
| :---- | :---- |
| **Gestión de Clientes y Vehículos** | Centraliza el padrón de clientes y las unidades vehiculares vinculadas. Soporta la co-propiedad vehicular mediante claves de asociación generadas con SHA-256. Controla las operaciones de alta, modificación, consulta y reactivación, así como la desvinculación o desactivación lógica en cascada de los rodados asociados. |
| **Gestión de Empleados** | Administra el personal operativo y administrativo del lavadero. Permite el alta, edición, consulta, desactivación y reactivación de empleados internos. Incluye el alta automática de perfiles en el primer inicio de sesión mediante autenticación federada con Google. |
| **Gestión de Servicios y Paquetes** | Administra el catálogo comercial de las prestaciones ofrecidas por el lavadero. Permite estructurar servicios individuales en etapas secuenciales y fijar duraciones según el tipo de vehículo. Configura las categorías funcionales, clasificaciones de rodados y la creación de paquetes de servicios con descuentos promocionales. |
| **Planificación y Gestión de Turnos** | Coordina la agenda de reservas del establecimiento asegurando la no superposición de horarios según la capacidad concurrente. Permite agendar, reprogramar y cancelar turnos tanto desde la gestión interna como por autogestión vía WhatsApp. Incorpora la reorganización automática de la agenda ante cancelaciones para optimizar tiempos muertos. |
| **Seguridad** | Regula el acceso al sistema mediante credenciales de correo/contraseña o Google Authentication. Gestiona la verificación de cuentas de correo, el restablecimiento de contraseñas olvidadas y el ciclo de vida de las sesiones de usuario. Aplica el cierre automático de sesión por inactividad para salvaguardar la privacidad de la información. |
| **Registro de Lavados y Pagos** | Constituye el núcleo operativo del sistema al gestionar el ingreso y seguimiento de las órdenes de lavado en taller. Registra la asignación de empleados, el avance en tiempo real de cada servicio y etapa, y el marcado final de retiro de la unidad. Gestiona los cobros totales o parciales por diferentes medios y mantiene el balance financiero del lavado. |
| **Estadísticas y Reportes** | Procesa la información histórica de operaciones para la evaluación y control de la gestión del negocio. Proporciona métricas e indicadores de rendimiento sobre recaudación, productividad, ocupación y servicios más solicitados. Permite consultar el historial de pagos y exportar informes consolidados en formatos PDF o Excel. |
| **Configuración** | Permite al administrador configurar parámetros del sistema como nombre del lavadero, horarios de operación, ubicación, teléfono, capacidad máxima, tiempos de tolerancia, duración de sesión y porcentajes de descuento para paquetes. |
| **Auditoría** | Brinda trazabilidad y control administrativo mediante la captura automática de las operaciones realizadas en el sistema. Almacena una bitácora inmutable con información del usuario responsable, tipo de acción, entidad afectada y marca temporal. Permite realizar búsquedas granulares mediante filtros y examinar el detalle de los cambios. |
| **Roles y Permisos** | Administra el esquema de control de acceso y seguridad granular para el personal. Permite crear, modificar, consultar y eliminar roles del sistema, configurando la matriz de permisos aplicable a cada perfil. Garantiza la integridad institucional impidiendo la modificación de roles base o la eliminación de aquellos con dependencias activas. |
| **Gestión de Insumos y Stock** | Centraliza la administración de insumos químicos, consumibles físicos y herramientas de trabajo, vinculándolos con sus respectivos proveedores comerciales. Controla las existencias en tiempo real mediante el cómputo y registro semiautomático del consumo en las etapas operativas de lavado, gestiona los ingresos y egresos manuales debidamente justificados y coordina la emisión, seguimiento y recepción de órdenes de compra tanto manuales como automáticas. |

## **Requisitos del Sistema**

### **Requisitos de Información**

| Código | Nombre | Descripción | Datos Específicos |
| :---- | :---- | :---- | :---- |
| **IRQ–01** | **Información sobre Empleados** | Almacenar datos de empleados activos e inactivos, credenciales y roles. | Id, Nombre, Apellido, Email, Rol, Estado, EmailVerificado, FechaCreacion. |
| **IRQ–02** | **Información sobre Clientes** | Información completa de clientes y vehículos para trazabilidad. | Id, Nombre, Apellido, Teléfono, TipoDocumento, NumeroDocumento, Email, Estado, VehiculosIds. |
| **IRQ–03** | **Información sobre Vehículos** | Información de vehículos, soportando múltiples dueños. | Id, Patente, TipoVehiculo, Marca, Modelo, Color, ClientesIds, ClaveAsociacionHash, Estado. |
| **IRQ–04** | **Información sobre Servicios** | Registrar servicios individuales ofrecidos. | Id, Nombre, Descripcion, TipoServicio, TipoVehiculo, Precio, TiempoEstimado, Estado, Etapas. |
| **IRQ–05** | **Información sobre Tipos de Servicio** | Categorías de servicios disponibles. | Id, Nombre, Estado. |
| **IRQ–06** | **Información sobre Tipos de Vehículo** | Tipos de vehículos disponibles. | Id, Nombre, FormatoPatente, CantidadEmpleadosRequeridos, Estado. |
| **IRQ–07** | **Información sobre Paquetes de Servicios** | Paquetes que agrupan servicios con descuento. | Id, Nombre, Estado, Precio, PorcentajeDescuento, TiempoEstimado, ServiciosIds. |
| **IRQ–08** | **Información sobre Lavados** | Historial de lavados, servicios, pagos y tiempos. | Id, Estado, ClienteId, VehiculoId, ServiciosDetalles, EmpleadosAsignados, Tiempos, Precio, Pago. |
| **IRQ–09** | **Información sobre Turnos** | Agenda de turnos asignados. | Id, FechaHora, ClienteId, VehiculoId, ServiciosIds, DuracionEstimada, Estado. |
| **IRQ–10** | **Información de Auditoría** | Registro inmutable de acciones. | UserId, UserEmail, Action, TargetId, TargetType, Timestamp. |
| **IRQ–11** | **Información de Configuración** | Parámetros globales del sistema. | Horarios, Capacidad, TiemposTolerancia, InfoLavadero, Descuentos. |
| **IRQ–12** | **Información de Sesiones WhatsApp** | Estado de conversaciones con bots. | PhoneNumber, ClienteId, CurrentState, TemporaryData, LastActivity. |
| **IRQ–13** | **Información sobre Insumos y Proveedores** | Almacenar datos de insumos (químicos, consumibles y herramientas), proveedores asociados, órdenes de compra emitidas y el historial cronológico inmutable de movimientos de stock (Kardex). | **Insumos:** Id, Nombre, TipoInsumo (QuimicoMedible, ConsumibleFisico, ActivoHerramienta), UnidadMedida, StockActual, StockMinimo, CantidadReabastecimiento, PermiteReabastecimientoAutomatico, ProveedoresIds, Estado.\<br\>**Proveedores:** Id, RazonSocial, CUIT, Telefono, Direccion, EmailOrdenes, Estado.\<br\>**Órdenes de Compra:** Id, GrupoSolicitudId, ProveedorId, ProveedorRazonSocial, FechaEmision, Estado (PendienteAprobacion, Enviada, Confirmada, Recibida, Cancelada), Detalles (InsumoId, InsumoNombre, CantidadPedida, PrecioUnitarioEstimado), Notas, MotivoCancelacion.\<br\>**Kardex (Movimientos):** Id, FechaHora, InsumoId, InsumoNombre, TipoMovimiento (IngresoManual, EgresoManual, ConsumoEtapa, RecepcionOrden, MermaCancelacion), Cantidad, SaldoResultante, UserId, JustificacionDesvio.\<br\>**Directivas de Configuración:** ModoEmisionAutomatica (EnvioDirecto, RequiereAprobacion), AsuntoEmail, EncabezadoEmail, NotasPredeterminadas. |

### **Requisitos Funcionales**

#### **Descripción de Actores**

| Actor | Descripción |
| :---- | :---- |
| **Trabajador** | Usuario operativo. Gestiona clientes, vehículos, lavados y pagos. |
| **Administrador** | Dueño/Gerente. Acceso total, incluyendo gestión de empleados, servicios y configuración. |
| **Cliente** | Usuario externo. Interactúa vía WhatsApp (Bot). |
| **Sistema** | Actor lógico. Ejecuta procesos automáticos (validaciones, notificaciones). |

## 

## **Casos de Uso del Sistema (Detalle)**

### **Módulo: Seguridad**

#### **CU-001 \- Iniciar Sesión** {#cu-001---iniciar-sesión}

| UC–001 | Iniciar Sesión |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El usuario accede al sistema mediante sus credenciales registradas. Este caso de uso es la puerta de entrada al sistema y verifica que el usuario tenga estado activo. |  |
| **Actor/es** | Empleado, Administrador |  |
| **Precondición** | El usuario debe tener una cuenta registrada en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario accede a la página de inicio de sesión. |
|  | 2 | El sistema muestra un formulario con opciones de inicio de sesión: correo/contraseña o Google. |
|  | 3 | El usuario selecciona el método de autenticación. |
|  | 4 | Se ejecuta el caso de uso correspondiente (CU-001.1 o CU-001.2). |
|  | 5 | El sistema verifica que el usuario esté activo en el sistema. |
|  | 6 | El sistema crea una sesión autenticada con cookies seguras. |
|  | 7 | El sistema redirige al usuario al inicio. |
|  | 8 | El sistema registra el evento de inicio de sesión en auditoría. |
| **Postcondición** | El usuario ha iniciado sesión correctamente y tiene acceso al sistema según su rol. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si el usuario está inactivo, el sistema muestra mensaje de cuenta deshabilitada y no permite el acceso. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 4-7 | 2 segundos |
| **Frecuencia** | Diaria (múltiples veces) |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | La autenticación se gestiona a través de Firebase Authentication. |  |

#### **CU-001.1 \- Iniciar sesión con correo y contraseña** {#cu-001.1---iniciar-sesión-con-correo-y-contraseña}

| UC–001.1 | Iniciar sesión con correo y contraseña |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | Extiende de CU-001. El usuario ingresa sus credenciales para validar su identidad en el sistema. Se verifica el estado de validación de la cuenta antes de otorgar acceso. |  |
| **Precondición** | El usuario debe tener una cuenta registrada previamente. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario ingresa sus credenciales de acceso en el formulario. |
|  | 2 | El usuario solicita iniciar sesión. |
|  | 3 | El sistema envía las credenciales al servicio de autenticación. |
|  | 4 | El servicio de autenticación valida las credenciales y genera la sesión. |
|  | 5 | El sistema comprueba las verificaciones de seguridad de la cuenta. |
|  | 6 | Se termina el caso de uso y se vuelve al flujo principal. |
| **Postcondición** | Las credenciales han sido validadas y el usuario accede al sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si las credenciales son inválidas, el sistema informa el error correspondiente. |
|  | 5a | Si la cuenta requiere verificación pendiente, el sistema muestra el aviso correspondiente y permite reenviar la solicitud. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3-4 | 2 segundos |
| **Frecuencia** | Diaria |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Ninguno. |  |

#### **CU-001.2 \- Iniciar sesión con Google** {#cu-001.2---iniciar-sesión-con-google}

| UC–001.2 | Iniciar sesión con Google |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | Extiende de CU-001. El usuario se autentica mediante su cuenta de Google. Si es un usuario nuevo, se ejecuta automáticamente el registro de su perfil. |  |
| **Precondición** | El usuario debe contar con una cuenta de proveedor externo válida. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario selecciona la opción de autenticación externa. |
|  | 2 | El sistema redirige al flujo de autenticación del proveedor. |
|  | 3 | El usuario autoriza el acceso en la plataforma del proveedor. |
|  | 4 | El proveedor retorna la confirmación de identidad al sistema. |
|  | 5 | El sistema verifica la existencia del usuario en la base de datos. |
|  | 6a | Si el usuario ya existe, se termina el caso de uso y se vuelve al flujo principal. |
|  | 6b | Si no existe, se ejecuta CU-011 para registrar el perfil. |
| **Postcondición** | El usuario ha sido autenticado exitosamente. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si el usuario cancela el flujo externo, se regresa a la pantalla de acceso. |
|  | 4a | Si el proveedor reporta un fallo, el sistema presenta el mensaje de error correspondiente. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-4 | 3 segundos |
| **Frecuencia** | Diaria |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | La identidad provista por el proveedor externo se considera verificada. |  |

#### **CU-002 \- Cerrar sesión** {#cu-002---cerrar-sesión}

| UC–002 | Cerrar sesión |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El usuario finaliza voluntariamente su sesión activa en el sistema. |  |
| **Precondición** | El usuario debe tener una sesión autenticada activa. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario solicita cerrar su sesión. |
|  | 2 | El sistema invalida los identificadores y credenciales de la sesión activa. |
|  | 3 | El sistema finaliza el contexto autenticado del usuario. |
|  | 4 | El sistema registra el evento en auditoría. |
|  | 5 | El sistema confirma la finalización del acceso seguro. |
| **Postcondición** | La sesión queda revocada y se requiere una nueva autenticación para ingresar. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | \- | \- |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-4 | 1 segundo |
| **Frecuencia** | Diaria |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Revoca el acceso a todos los recursos protegidos del sistema. |  |

#### **CU-003 \- Recuperar contraseña** {#cu-003---recuperar-contraseña}

| UC–003 | Recuperar contraseña |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | Permite a un usuario restablecer su contraseña mediante el envío seguro de instrucciones a su dirección de correo. |  |
| **Precondición** | El usuario debe poseer una cuenta registrada con credenciales locales. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario solicita la recuperación de su contraseña. |
|  | 2 | El sistema solicita el correo electrónico vinculado a la cuenta. |
|  | 3 | El usuario ingresa su correo electrónico. |
|  | 4 | El sistema genera y envía un enlace seguro de restablecimiento. |
|  | 5 | El sistema confirma el envío de las instrucciones. |
|  | 6 | El usuario accede mediante el enlace recibido e ingresa su nueva contraseña. |
|  | 7 | El sistema valida y actualiza la nueva contraseña. |
| **Postcondición** | La contraseña del usuario ha sido actualizada exitosamente. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el correo no existe, por seguridad se emite la misma confirmación sin enviar el enlace. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 4-5 | 5 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Garantiza la privacidad sin revelar la existencia de cuentas registradas. |  |

#### **CU-004 \- Cierre de sesión automático por inactividad** {#cu-004---cierre-de-sesión-automático-por-inactividad}

| UC–004 | Cierre de sesión automático por inactividad |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad, OBJ–11 Gestión de Configuración |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados, IRQ–11 Información de Configuración |  |
| **Descripción** | El sistema revoca automáticamente la sesión de un usuario tras detectar la ausencia de interacción durante el tiempo límite configurado. |  |
| **Precondición** | El usuario cuenta con una sesión activa y supera el tiempo máximo de inactividad permitido. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema monitorea el tiempo transcurrido desde la última interacción del usuario. |
|  | 2 | El sistema detecta que se ha alcanzado el límite de inactividad configurado. |
|  | 3 | El sistema invalida la sesión del usuario. |
|  | 4 | El sistema bloquea el acceso a las operaciones y notifica la expiración de la sesión. |
|  | 5 | El sistema registra el evento de expiración en auditoría. |
| **Postcondición** | La sesión queda cerrada y se requiere una nueva autenticación. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | \- | \- |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3 | Inmediato |
| **Frecuencia** | Variable |  |
| **Estabilidad** | Media |  |
| **Comentarios** | El parámetro de tolerancia es configurable por el administrador. |  |

### **Módulo: Gestión de Empleados**

#### **CU-005 \- Registrarse en el sistema** {#cu-005---registrarse-en-el-sistema}

| UC–005 | Registrarse en el sistema |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados, OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | Un nuevo usuario registra su cuenta en el sistema para incorporarse con el rol base configurado. |  |
| **Precondición** | La cuenta no debe estar registrada previamente en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario accede a la sección de registro. |
|  | 2 | El sistema presenta los métodos de registro disponibles. |
|  | 3 | El usuario selecciona el método deseado. |
|  | 4 | Se ejecuta el flujo correspondiente al método seleccionado (CU-005.1 o CU-005.2). |
|  | 5 | El sistema crea el registro del empleado con los parámetros por defecto. |
|  | 6 | El sistema registra la acción en auditoría. |
|  | 7 | El sistema presenta la confirmación del registro. |
| **Postcondición** | El empleado queda registrado en el sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el identificador ya existe, el sistema informa el conflicto y detiene el proceso. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 4-5 | 3 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Ninguno. |  |

#### **CU-005.1 \- Registrarse por correo** {#cu-005.1---registrarse-por-correo}

| UC–005.1 | Registrarse por correo |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados, OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | Extiende de CU-005. El usuario completa el formulario de registro y valida su cuenta mediante correo electrónico. |  |
| **Precondición** | Ninguna adicional. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema presenta el formulario de registro solicitando los datos del nuevo usuario. |
|  | 2 | El usuario completa los datos requeridos. |
|  | 3 | El sistema valida los datos ingresados según las reglas de negocio y seguridad. |
|  | 4 | El sistema registra la cuenta en el servicio de autenticación. |
|  | 5 | El sistema despacha el mensaje de verificación al usuario. |
|  | 6 | El sistema presenta un mensaje indicando que debe verificar su cuenta antes del primer acceso. |
|  | 7 | Se continúa con el flujo principal del CU-005. |
| **Postcondición** | La cuenta queda creada a la espera de verificación. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si los datos no cumplen con las reglas de validación o seguridad, el sistema indica las correcciones necesarias. |
|  | 4a | Si la cuenta ya existe, el sistema informa el error correspondiente. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 4-5 | 3 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Ninguno. |  |

#### **CU-005.2 \- Registrarse por Google** {#cu-005.2---registrarse-por-google}

| UC–005.2 | Registrarse por Google |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados, OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | Extiende de CU-005. El registro se realiza mediante la vinculación con una cuenta de Google, validando automáticamente la identidad del usuario. |  |
| **Precondición** | El usuario debe poseer una cuenta de Google activa. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario solicita el registro mediante proveedor externo Google. |
|  | 2 | El sistema inicia el proceso de autorización federada. |
|  | 3 | El usuario autoriza la transferencia de sus datos de perfil. |
|  | 4 | El sistema valida que el correo obtenido no exista en los registros. |
|  | 5 | Se continúa con el flujo principal del CU-005 asignando los datos del proveedor externo. |
| **Postcondición** | La cuenta de empleado queda creada y automáticamente confirmada. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si el usuario cancela el proceso de autorización, el sistema interrumpe el registro. |
|  | 4a | Si el correo ya existe, el sistema informa que debe iniciar sesión. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-4 | 3 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | La verificación de correo es heredada del proveedor de identidad. |  |

#### **CU-006 \- Modificar empleado** {#cu-006---modificar-empleado}

| UC–006 | Modificar empleado |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El administrador actualiza la información de un empleado existente en el sistema. |  |
| **Precondición** | El usuario debe tener rol de Administrador. El empleado debe existir en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-009 Consultar empleados. |
|  | 2 | El administrador selecciona el empleado a modificar. |
|  | 3 | El sistema presenta el formulario de edición con los datos actuales del empleado. |
|  | 4 | El administrador modifica los campos editables deseados. |
|  | 5 | El administrador confirma los cambios. |
|  | 6 | El sistema valida los datos ingresados. |
|  | 7 | El sistema actualiza el registro del empleado en la base de datos. |
|  | 8 | El sistema presenta un mensaje de confirmación del cambio. |
|  | 9 | El sistema registra la acción en auditoría. |
| **Postcondición** | Los datos del empleado quedan actualizados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 6a | Si hay errores de validación, el sistema informa los campos a corregir. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-009 Consultar empleados. |  |

#### **CU-007 \- Desactivar empleado** {#cu-007---desactivar-empleado}

| UC–007 | Desactivar empleado |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El administrador deshabilita el acceso de un empleado al sistema. |  |
| **Precondición** | El usuario debe tener rol de Administrador. El empleado debe existir y estar activo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-009 Consultar empleados. |
|  | 2 | El administrador selecciona el empleado a desactivar. |
|  | 3 | El sistema presenta una solicitud de confirmación de la acción. |
|  | 4 | El administrador confirma la desactivación. |
|  | 5 | El sistema actualiza el estado del empleado a inactivo. |
|  | 6 | El sistema presenta un mensaje de confirmación. |
|  | 7 | El sistema registra la acción en auditoría. |
| **Postcondición** | El empleado queda desactivado y no puede acceder al sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el administrador cancela, se interrumpe la operación. |
|  | 5a | Si la desactivación incumple una regla de negocio (ej. único administrador activo), el sistema bloquea la acción e informa el motivo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | La desactivación es lógica para preservar la trazabilidad. |  |

#### **CU-008 \- Reactivar empleado** {#cu-008---reactivar-empleado}

| UC–008 | Reactivar empleado |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El administrador rehabilita a un empleado previamente desactivado para permitirle operar en el sistema. |  |
| **Precondición** | El usuario debe tener rol de Administrador. El empleado debe existir y estar inactivo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-009 Consultar empleados. |
|  | 2 | El administrador selecciona el empleado inactivo a reactivar. |
|  | 3 | El sistema presenta una solicitud de confirmación. |
|  | 4 | El administrador confirma la reactivación. |
|  | 5 | El sistema actualiza el estado del empleado a activo. |
|  | 6 | El sistema presenta un mensaje de confirmación. |
|  | 7 | El sistema registra la acción en auditoría. |
| **Postcondición** | El empleado queda activo nuevamente. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el administrador cancela, se interrumpe la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-009 Consultar empleados. |  |

#### **CU-009 \- Consultar empleados** {#cu-009---consultar-empleados}

|  UC–009 | Consultar empleados |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El administrador visualiza la lista de empleados registrados con opciones de filtrado, ordenamiento y paginación. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador accede a la sección de gestión de empleados. |
|  | 2 | El sistema recupera los registros de empleados aplicando los filtros por defecto. |
|  | 3 | El sistema presenta el listado de empleados registrados. |
|  | 4 | El administrador puede aplicar filtros y ordenamiento según los campos y criterios disponibles. |
|  | 5 | El administrador navega entre los resultados utilizando los controles de paginación. |
|  | 6 | El sistema actualiza la visualización según los criterios seleccionados. |
| **Postcondición** | El administrador visualiza los empleados según los criterios aplicados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no existen registros que coincidan, el sistema presenta un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Permite alternar la visualización entre empleados activos e inactivos. |  |

#### **CU-010 \- Asignar roles a empleados** {#cu-010---asignar-roles-a-empleados}

| UC–010 | Asignar roles a empleados |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El administrador modifica el nivel de acceso o rol asignado a un empleado para gestionar sus permisos. |  |
| **Precondición** | El usuario debe tener rol de Administrador. El empleado debe existir en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador accede a la edición del empleado (incluye CU-006). |
|  | 2 | El sistema presenta los roles disponibles en el sistema. |
|  | 3 | El administrador selecciona el nuevo rol para el empleado. |
|  | 4 | El administrador confirma la modificación. |
|  | 5 | El sistema valida las reglas de integridad de roles. |
|  | 6 | El sistema actualiza el rol del empleado en la base de datos. |
|  | 7 | El sistema registra el cambio en auditoría. |
| **Postcondición** | El empleado cuenta con el nuevo rol asignado y sus permisos asociados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si el cambio infringe restricciones de seguridad (ej. dejar el sistema sin administradores activos), el sistema bloquea la acción y muestra el error correspondiente. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Este caso de uso se ejecuta dentro del flujo de CU-006. |  |

#### **CU-011 \- Autenticar usuario con Google y registrar perfil si es nuevo** {#cu-011---autenticar-usuario-con-google-y-registrar-perfil-si-es-nuevo}

| UC–011 | Autenticar usuario con Google y registrar perfil si es nuevo |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados, OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El sistema valida el inicio de sesión externo y crea automáticamente el perfil de empleado si no existía previamente. |  |
| **Actor/es** | Sistema |  |
| **Precondición** | El usuario completó la autenticación con el proveedor externo exitosamente. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema recibe los datos de perfil provistos por la plataforma externa. |
|  | 2 | El sistema consulta si el usuario se encuentra registrado en la base de datos. |
|  | 3 | Si no existe, el sistema genera un nuevo registro de empleado. |
|  | 4 | El sistema completa los datos del empleado a partir de la información del perfil externo. |
|  | 5 | El sistema asigna el rol y estado inicial por defecto. |
|  | 6 | El sistema marca la cuenta como validada. |
|  | 7 | El sistema almacena el registro en la base de datos. |
|  | 8 | El sistema registra el alta automática en auditoría. |
| **Postcondición** | El usuario queda registrado y listo para operar en el sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si el usuario ya existe, el sistema omite la creación y continúa el acceso. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3-7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Caso de uso automatizado por el sistema. |  |

### **Módulo: Gestión de Clientes y Vehículos**

#### **CU-012 \- Crear cliente** {#cu-012---crear-cliente}

| UC–012 | Crear cliente |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos |  |
| **Descripción** | El personal registra un nuevo cliente en el sistema junto con la carga obligatoria de al menos un vehículo asociado a su flota. |  |
| **Actor/es** | Trabajador, Administrador |  |
| **Precondición** | El usuario debe estar autenticado en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario solicita crear un nuevo cliente. |
|  | 2 | El sistema presenta el formulario de registro solicitando los datos del cliente. |
|  | 3 | El usuario ingresa los datos requeridos. |
|  | 4 | El sistema valida que los identificadores principales no se encuentren duplicados. |
|  | 5 | **Bucle de Gestión de Vehículos \[Obligatorio: Mínimo 1\]:** Mientras el usuario necesite incorporar vehículos a la flota del cliente, selecciona una opción por cada unidad:\<br\>a) Si el vehículo es nuevo: se ejecuta CU-018.\<br\>b) Si el vehículo ya existe en el sistema: se ejecuta CU-024. |
|  | 6 | Por cada vehículo procesado con éxito, el sistema lo lista en la flota temporal del cliente. |
|  | 7 | El usuario solicita confirmar el alta del cliente y su flota. |
|  | 8 | El sistema valida que exista al menos un vehículo asociado, persiste las entidades de forma transaccional, registra la acción en auditoría y emite la confirmación de éxito. |
| **Postcondición** | El cliente y sus vehículos quedan registrados en estado activo en el sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si los datos principales ya pertenecen a otro cliente registrado, el sistema informa el conflicto. |
|  | 8a | Si no se ha vinculado al menos un vehículo, el sistema impide guardar el registro. |
|  | 7a | Si el usuario cancela la operación antes de confirmar, se descartan todos los datos temporales. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 8 | 2 segundos |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | La persistencia del cliente y sus vehículos asociados se realiza de manera atómica y transaccional. |  |

#### **CU-013 \- Modificar cliente** {#cu-013---modificar-cliente}

| UC–013 | Modificar cliente |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos |  |
| **Descripción** | El personal actualiza la información de un cliente existente y gestiona sus vehículos asociados mediante un bucle de opciones. |  |
| **Precondición** | El usuario debe estar autenticado. El cliente debe existir y estar activo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | **Sección:** | **Principal** |
|  | 1 | Se ejecuta CU-016 Consultar clientes y se selecciona el perfil a editar. |
|  | 2 | El sistema presenta el formulario con los datos actuales del cliente y el listado de sus vehículos vinculados. |
|  | 3 | El usuario actualiza los campos editables del cliente. |
|  | 4 | **Bucle de Gestión de Vehículos \[Opcional: 0 o más\]:** Mientras el usuario desee actualizar la flota del cliente, selecciona una opción por unidad:\<br\>a) Si desea registrar un vehículo nuevo: consúltese *Opción: Registrar Vehículo Nuevo*.\<br\>b) Si desea asociar otro vehículo existente: consúltese *Opción: Vincular Vehículo Existente*.\<br\>c) Si desea remover un vehículo de la lista: consúltese *Opción: Quitar Vehículo de la Flota*. |
|  | 5 | Por cada vehículo procesado, el sistema actualiza temporalmente la lista de unidades asociadas en la interfaz. |
|  | 6 | El usuario confirma el guardado general de los cambios. |
|  | 7 | El sistema valida los datos modificados junto con las operaciones acumuladas en el bucle, impacta los cambios en la base de datos de forma unificada, muestra confirmación y registra la acción en auditoría. |
|  | **Sección:** | **Opción: Registrar Vehículo Nuevo** |
|  | 1 | El usuario solicita agregar un nuevo vehículo para el cliente. |
|  | 2 | El sistema invoca contextualmente la ejecución de CU-018 Crear vehículo y retorna al bucle principal. |
|  | **Sección:** | **Opción: Vincular Vehículo Existente** |
|  | 1 | El usuario solicita vincular un vehículo ya existente. |
|  | 2 | El sistema invoca contextualmente la ejecución de CU-024 Vincular vehículo a cliente y retorna al bucle principal. |
|  | **Sección:** | **Opción: Quitar Vehículo de la Flota** |
|  | 1 | El usuario selecciona un vehículo asociado y solicita su remoción. |
|  | 2 | El sistema evalúa la propiedad de la unidad: si es de titularidad única prepara su baja lógica mediante CU-020 Desactivar vehículo; si cuenta con otros titulares activos solo prepara la desvinculación con este cliente, retornando al bucle principal. |
| **Postcondición** | Los datos del cliente y sus asociaciones vehiculares son actualizados de forma consistente. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 7a | Si hay errores de validación, el sistema frena el guardado y señala los campos a corregir. |
|  | 6a | Si el usuario cancela la edición, se descartan las modificaciones y operaciones temporales sin alterar la base de datos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Los identificadores de documento no son editables para garantizar la integridad. |  |

#### **CU-014 \- Desactivar cliente** {#cu-014---desactivar-cliente}

| UC–014 | Desactivar cliente |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos |  |
| **Descripción** | El personal inhabilita a un cliente en el sistema, validando previamente que no posea compromisos operativos pendientes y aplicando la baja lógica sobre sus vehículos exclusivos. |  |
| **Precondición** | El usuario debe estar autenticado. El cliente debe existir y encontrarse activo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-016 Consultar clientes. |
|  | 2 | El usuario selecciona al cliente a desactivar. |
|  | 3 | El sistema verifica que el cliente no posea turnos en estado pendiente ni lavados en proceso en el lavadero |
|  | 4 | El sistema evalúa la flota del cliente e identifica los vehículos exclusivos que se desactivarán junto con él. |
|  | 5 | El sistema solicita la confirmación de la desactivación informando el impacto sobre los vehículos. |
|  | 6 | El usuario confirma la operación. |
|  | 7 | El sistema cambia el estado del cliente a "Inactivo" y ejecuta la baja lógica de los vehículos exclusivos mediante CU-020. |
|  | 8 | Por cada vehículo multipropietario, el sistema remueve únicamente la relación con este cliente, preservándolo activo. |
|  | 9 | El sistema registra la desactivación en auditoría y emite la confirmación correspondiente. |
| **Postcondición** | El cliente y sus vehículos exclusivos pasan a estado inactivo. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 6a | Si el usuario cancela la confirmación, se interrumpe la operación sin aplicar cambios. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7-8 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Los vehículos compartidos con otros clientes activos conservan su estado activo en el lavadero. |  |

#### **CU-015 \- Reactivar cliente** {#cu-015---reactivar-cliente}

| UC–015 | Reactivar cliente |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos |  |
| **Descripción** | El administrador habilita nuevamente a un cliente inactivo y reactiva automáticamente aquellos vehículos exclusivos que se desactivaron con su baja. |  |
| **Precondición** | El usuario debe ser Administrador. El cliente debe existir y estar inactivo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-016 Consultar clientes. |
|  | 2 | El administrador selecciona al cliente inactivo a reactivar. |
|  | 3 | El sistema identifica los vehículos que habían sido dados de baja de forma exclusiva por este cliente. |
|  | 4 | El sistema solicita la confirmación de reactivación informando la restauración de los vehículos. |
|  | 5 | El administrador confirma la reactivación. |
|  | 6 | El sistema cambia el estado del cliente a "Activo". |
|  | 7 | El sistema ejecuta el caso de uso CU-021 para reactivar cada vehículo exclusivo identificado. |
|  | 8 | El sistema registra la reactivación en auditoría y emite la confirmación de éxito. |
| **Postcondición** | El cliente y sus vehículos exclusivos vuelven a estar en estado activo. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si el administrador cancela la confirmación, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6-7 | 2 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Los vehículos multipersonales que se mantuvieron activos con otros dueños no sufren alteraciones. |  |

#### **CU-016 \- Consultar clientes** {#cu-016---consultar-clientes}

| UC–016 | Consultar clientes |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes |  |
| **Descripción** | El personal consulta la lista de clientes registrados en el sistema con opciones de filtrado, ordenamiento y paginación. |  |
| **Precondición** | El usuario debe estar autenticado. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario accede a la sección de clientes. |
|  | 2 | El sistema recupera la lista de clientes aplicando los filtros por defecto. |
|  | 3 | El sistema presenta el listado de clientes registrados. |
|  | 4 | El usuario puede aplicar filtros y ordenamiento según los campos y criterios disponibles. |
|  | 5 | El usuario navega entre páginas mediante los controles de paginación. |
|  | 6 | El sistema actualiza la visualización según los criterios seleccionados. |
| **Postcondición** | El usuario visualiza los clientes según los criterios aplicados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no existen clientes para los criterios indicados, el sistema muestra un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 1 segundo |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Por defecto presenta únicamente clientes activos. |  |

#### **CU-017 \- Buscar clientes** {#cu-017---buscar-clientes}

| UC–017 | Buscar clientes |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes |  |
| **Descripción** | El personal busca clientes de forma ágil mediante criterios de coincidencia de texto. |  |
| **Precondición** | El usuario debe estar autenticado. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario accede a la sección de clientes. |
|  | 2 | El usuario ingresa un término en el campo de búsqueda. |
|  | 3 | El sistema busca coincidencias según los criterios de búsqueda disponibles. |
|  | 4 | El sistema presenta los resultados coincidentes. |
|  | 5 | El usuario selecciona el cliente deseado de la lista. |
| **Postcondición** | El usuario localiza y visualiza los clientes que coinciden con la búsqueda. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si no hay coincidencias, el sistema muestra un mensaje indicando la ausencia de resultados. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3-4 | 500ms |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | La búsqueda no distingue entre mayúsculas y minúsculas. |  |

#### **CU-018 \- Crear vehículo** {#cu-018---crear-vehículo}

| UC–018 | Crear vehículo |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos, IRQ–06 Información sobre Tipos de Vehículo |  |
| **Descripción** | Permite registrar un vehículo nuevo dentro del contexto del flujo de un cliente (alta o edición). |  |
| **Precondición** | Ejecutado de manera dependiente desde los casos de uso CU-012 o CU-013. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El caso de uso inicia al ser invocado por el flujo de gestión de cliente. |
|  | 2 | El sistema presenta el formulario solicitando los datos del vehículo. |
|  | 3 | El usuario ingresa los datos solicitados. |
|  | 4 | El sistema valida el formato del identificador vehicular y comprueba que no exista activo en el sistema. |
|  | 5 | El sistema genera la clave de asociación del vehículo y lo incorpora temporalmente a la flota del cliente en proceso. |
|  | 6 | El caso de uso finaliza retornando el control al caso de uso invocador. |
| **Postcondición** | El vehículo queda preparado para ser persistido junto con la confirmación del cliente. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el identificador vehicular ya se encuentra registrado y activo, el sistema informa el error y sugiere utilizar la vinculación. |
|  | 3a | Si el usuario cancela la carga, se descartan los datos del vehículo y se retorna al flujo de cliente. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 4-5 | Inmediato |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | La persistencia definitiva depende de la confirmación final del caso de uso principal. |  |

#### **CU-019 \- Modificar vehículo** {#cu-019---modificar-vehículo}

| UC–019 | Modificar vehículo |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos |  |
| **Descripción** | El personal actualiza los datos modificables de un vehículo existente. |  |
| **Precondición** | El usuario debe estar autenticado. El vehículo debe existir en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-022 Consultar vehículos. |
|  | 2 | El usuario selecciona el vehículo a modificar. |
|  | 3 | El sistema presenta el formulario de edición con los datos actuales del vehículo. |
|  | 4 | El sistema presenta los campos no editables en modo de solo lectura. |
|  | 5 | El usuario modifica los campos editables del vehículo. |
|  | 6 | El usuario confirma los cambios. |
|  | 7 | El sistema valida los datos modificados. |
|  | 8 | El sistema actualiza el registro del vehículo en la base de datos. |
|  | 9 | El sistema presenta un mensaje de confirmación. |
|  | 10 | El sistema registra la acción en auditoría. |
| **Postcondición** | Los datos del vehículo son actualizados exitosamente. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 7a | Si hay errores de validación, el sistema informa los campos a corregir. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 8 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-022 Consultar vehículos. |  |

#### **CU-020 \- Desactivar vehículo** {#cu-020---desactivar-vehículo}

| UC–020 | Desactivar vehículo |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos |  |
| **Descripción** | Permite dar de baja lógica a un vehículo cuando este queda sin clientes asociados o es desactivado en cascada con su único dueño. |  |
| **Precondición** | El vehículo debe existir y encontrarse en estado activo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El caso de uso inicia al ser invocado por la desactivación de un cliente o desvinculación de su único dueño. |
|  | 2 | El sistema cambia el estado del vehículo a "Inactivo". |
|  | 3 | El sistema actualiza el registro del vehículo en la base de datos. |
|  | 4 | El sistema registra la desactivación en auditoría. |
| **Postcondición** | El vehículo pasa a estado inactivo y no está disponible para nuevos servicios. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | \- | \- |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | Inmediato |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Es un caso de uso contextual y dependiente de los flujos de cliente. |  |

#### **CU-021 \- Reactivar vehículo** {#cu-021---reactivar-vehículo}

| UC–021 | Reactivar vehículo |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos |  |
| **Descripción** | Restaura el estado activo de un vehículo inactivo como parte de la reactivación transaccional de un cliente. |  |
| **Precondición** | Ejecutado desde el caso de uso CU-015 Reactivar cliente. El vehículo debe encontrarse inactivo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El caso de uso inicia al ser invocado automáticamente por el flujo de reactivación de cliente. |
|  | 2 | El sistema cambia el estado del vehículo a "Activo". |
|  | 3 | El sistema asocia el identificador del cliente a la lista de propietarios del vehículo. |
|  | 4 | El sistema persiste el estado actualizado del vehículo en la base de datos. |
|  | 5 | El caso de uso finaliza y retorna el control al flujo principal. |
| **Postcondición** | El vehículo vuelve a encontrarse activo y vinculado al cliente. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | \- | \- |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-4 | Inmediato |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Mantiene la consistencia de datos de vehículos con dueños únicos restaurados. |  |

#### **CU-022 \- Consultar vehículos** {#cu-022---consultar-vehículos}

| UC–022 | Consultar vehículos |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos |  |
| **Descripción** | El personal visualiza la lista de vehículos registrados con opciones de filtrado, ordenamiento y paginación. |  |
| **Precondición** | El usuario debe estar autenticado. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario accede a la sección de vehículos. |
|  | 2 | El sistema recupera la lista de vehículos aplicando los filtros por defecto. |
|  | 3 | El sistema presenta el listado de vehículos registrados. |
|  | 4 | El usuario puede aplicar filtros y ordenamiento según los campos y criterios disponibles. |
|  | 5 | El usuario navega entre las páginas mediante los controles de paginación. |
|  | 6 | El sistema actualiza la visualización según los criterios seleccionados. |
| **Postcondición** | El usuario visualiza los vehículos según los criterios aplicados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no hay vehículos registrados para los filtros activos, el sistema presenta un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Por defecto presenta únicamente vehículos en estado activo. |  |

#### **CU-023 \- Buscar vehículos** {#cu-023---buscar-vehículos}

| UC–023 | Buscar vehículos |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos |  |
| **Descripción** | El personal busca vehículos por coincidencia de texto en tiempo real. |  |
| **Precondición** | El usuario debe estar autenticado. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario accede a la sección de vehículos. |
|  | 2 | El usuario ingresa un término en el campo de búsqueda. |
|  | 3 | El sistema busca coincidencias según los criterios de búsqueda disponibles. |
|  | 4 | El sistema presenta los resultados coincidentes. |
|  | 5 | El usuario selecciona un vehículo del listado. |
| **Postcondición** | El usuario localiza los vehículos coincidentes. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si no existen coincidencias, el sistema muestra un mensaje de sin resultados. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3-4 | 500ms |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | La búsqueda no distingue entre mayúsculas y minúsculas. |  |

#### **CU-024 \- Vincular vehículo a cliente** {#cu-024---vincular-vehículo-a-cliente}

| UC–024 | Vincular vehículo a cliente |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos |  |
| **Descripción** | Permite asociar un vehículo ya existente en el sistema a un cliente mediante la validación de su clave de asociación (soporte multipropietario). |  |
| **Precondición** | El vehículo debe estar registrado y activo. El usuario debe poseer la clave de asociación del vehículo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario solicita vincular un vehículo existente dentro del flujo del cliente. |
|  | 2 | El sistema solicita el identificador vehicular y la clave de asociación. |
|  | 3 | El usuario ingresa los datos solicitados. |
|  | 4 | El sistema valida la existencia del vehículo y verifica que la clave ingresada sea correcta. |
|  | 5 | El sistema valida que el vehículo no esté ya vinculado a este cliente. |
|  | 6 | El sistema asocia la relación entre el cliente y el vehículo en memoria temporal. |
|  | 7 | El sistema registra la vinculación y retorna al flujo principal. |
| **Postcondición** | El vehículo queda asociado al cliente como co-propietario. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el vehículo no existe o la clave de asociación es incorrecta, el sistema rechaza la vinculación e informa el error. |
|  | 5a | Si el vehículo ya pertenece al cliente, el sistema informa que la asociación ya existe. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 4-6 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Garantiza la seguridad y privacidad al requerir la clave provista por el dueño original. |  |

#### **CU-025 \- Desvincular vehículo de cliente** {#cu-025---desvincular-vehículo-de-cliente}

| UC–025 | Desvincular vehículo de cliente |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos |  |
| **Descripción** | Remueve la relación entre un cliente y un vehículo multipropietario. Si el vehículo solo pertenecía a este cliente, se procede a su desactivación. |  |
| **Precondición** | El vehículo debe estar vinculado al cliente. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario solicita desvincular un vehículo de la flota del cliente. |
|  | 2 | El sistema comprueba la cantidad de propietarios asociados al vehículo. |
|  | 3a | Si el vehículo tiene más dueños activos, el sistema remueve únicamente al cliente de la lista de propietarios. |
|  | 3b | Si el vehículo solo posee a este cliente como dueño, el sistema ejecuta el caso de uso CU-020 Desactivar vehículo. |
|  | 4 | El sistema actualiza los registros en la base de datos. |
|  | 5 | El sistema registra la desvinculación en auditoría y emite la confirmación correspondiente. |
| **Postcondición** | El vehículo deja de estar asociado al cliente. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | \- | \- |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3-4 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Protege la existencia de vehículos compartidos en el sistema. |  |

#### **CU-026 \- Registrarse como cliente por WhatsApp** {#cu-026---registrarse-como-cliente-por-whatsapp}

| UC–026 | Registrarse como cliente por WhatsApp |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–10 Integración con WhatsApp, OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–12 Información de Sesiones WhatsApp, IRQ–03 Información sobre Vehículos |  |
| **Descripción** | El cliente se registra de forma guiada interactuando con el bot de WhatsApp, siendo obligatoria la carga inicial de al menos un vehículo para completar el alta. |  |
| **Precondición** | El número de teléfono no debe pertenecer a un cliente activo en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El cliente envía un mensaje al canal de WhatsApp del lavadero. |
|  | 2 | El sistema identifica que el número no está registrado e inicia el flujo de registro. |
|  | 3 | El sistema solicita secuencialmente los datos personales requeridos. |
|  | 4 | El cliente proporciona los datos solicitados en cada interacción. |
|  | 5 | El sistema valida los datos y los almacena temporalmente en el contexto de la sesión. |
|  | 6 | El sistema solicita la carga obligatoria del primer vehículo ejecutando CU-027. |
|  | 7 | El cliente completa el registro del vehículo. |
|  | 8 | El sistema presenta el resumen de los datos y solicita la confirmación final. |
|  | 9 | El cliente confirma el registro. |
|  | 10 | El sistema almacena de forma definitiva al cliente y a su vehículo en la base de datos. |
|  | 11 | El sistema envía un mensaje de bienvenida formal con las opciones del menú principal y registra la acción en auditoría. |
| **Postcondición** | El cliente queda registrado en el sistema con su vehículo asociado y habilitado para operar vía WhatsApp. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si los datos de identificación ya están registrados, el sistema notifica el error y cancela el flujo. |
|  | 9a | Si el cliente solicita cancelar en cualquier momento, el sistema elimina los datos temporales y cancela el alta. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 10 | 2 segundos |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Gestionado a través de sesiones de conversación automatizadas. |  |

#### **CU-027 \- Registrar vehículo por WhatsApp** {#cu-027---registrar-vehículo-por-whatsapp}

| UC–027 | Registrar vehículo por WhatsApp |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–10 Integración con WhatsApp, OBJ–02 Gestión de Clientes y Vehículos |  |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos, IRQ–12 Información de Sesiones WhatsApp |  |
| **Descripción** | El cliente registra un vehículo nuevo o vincula uno existente a través de la interacción conversacional por WhatsApp. |  |
| **Precondición** | El cliente debe estar en proceso de registro o autenticado en la sesión de WhatsApp. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema solicita la opción deseada (Registrar vehículo nuevo o Vincular vehículo existente). |
|  | 2a | Si selecciona registrar nuevo: el sistema solicita los datos del vehículo, el cliente los proporciona, el sistema valida el identificador y genera la clave de asociación. |
|  | 2b | Si selecciona vincular existente: el sistema solicita el identificador y la clave de asociación, el cliente los ingresa y el sistema valida la coincidencia. |
|  | 3 | El sistema añade el vehículo a la flota del cliente en la sesión. |
|  | 4 | El sistema emite la confirmación del vehículo registrado. |
| **Postcondición** | El vehículo queda incorporado a la flota del cliente. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a.1 | Si el identificador ya está registrado en el lavadero, el sistema informa y sugiere la opción de vinculación. |
|  | 2b.1 | Si la clave de asociación es inválida, el sistema rechaza la vinculación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Puede ejecutarse dentro de CU-026 o de forma autónoma desde el menú principal de WhatsApp. |  |

#### **CU-028 \- Identificar si el número de teléfono está registrado** {#cu-028---identificar-si-el-número-de-teléfono-está-registrado}

| UC–028 | Identificar si el número de teléfono está registrado |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–10 Integración con WhatsApp |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–12 Información de Sesiones WhatsApp |  |
| **Descripción** | Proceso del sistema que determina si un número entrante de WhatsApp pertenece a un cliente registrado y activo para enrutar el flujo conversacional. |  |
| **Precondición** | Mensaje entrante recibido vía WhatsApp. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema extrae el número de teléfono del mensaje entrante. |
|  | 2 | El sistema consulta en la base de datos la existencia de un cliente activo con dicho teléfono. |
|  | 3a | Si el cliente existe y está activo, el sistema asocia la sesión al cliente y deriva al menú autenticado (CU-092). |
|  | 3b | Si el número no existe, el sistema deriva al flujo de registro de nuevo cliente (CU-026). |
| **Postcondición** | El sistema identifica el estado del usuario y enruta la conversación. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | \- | \- |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2 | \< 500ms |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Ejecutado automáticamente por el webhook del sistema ante cada mensaje inicial. |  |

#### **CU-029 \- Editar datos personales por WhatsApp** {#cu-029---editar-datos-personales-por-whatsapp}

| UC–029 | Editar datos personales por WhatsApp |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–10 Integración con WhatsApp |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–12 Información de Sesiones WhatsApp |  |
| **Descripción** | El cliente modifica sus datos de perfil permitidos mediante la interfaz conversacional. |  |
| **Precondición** | El cliente debe encontrarse registrado en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El cliente accede al menú de gestión de su cuenta en WhatsApp. |
|  | 2 | El sistema presenta los datos actuales del cliente. |
|  | 3 | El sistema presenta las opciones de modificación disponibles. |
|  | 4 | El cliente selecciona el dato editable a modificar. |
|  | 5 | El sistema solicita el nuevo valor. |
|  | 6 | El cliente ingresa el valor actualizado. |
|  | 7 | El sistema valida el formato del dato ingresado. |
|  | 8 | El sistema actualiza el registro en la base de datos. |
|  | 9 | El sistema confirma la modificación al cliente. |
|  | 10 | El sistema registra la acción en auditoría. |
| **Postcondición** | Los datos del cliente quedan actualizados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 7a | Si el valor ingresado no es válido, el sistema informa el error y solicita un nuevo ingreso. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 8 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Los identificadores de cuenta y documento no son editables por esta vía por seguridad. |  |

### **Módulo: Gestión de Servicios**

#### **CU-030 \- Crear servicio** {#cu-030---crear-servicio}

| UC–030 | Crear servicio |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes, OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios, IRQ–05 Información sobre Tipos de Servicio, IRQ–06 Información sobre Tipos de Vehículo, IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El Administrador da de alta un nuevo servicio indicando datos comerciales, categoría, tipo de vehículo, estructura de etapas y la receta teórica de insumos químicos requerida para su ejecución. |  |
| **Precondición** | El usuario debe ser Administrador. Deben existir tipos de servicio, tipos de vehículo e insumos químicos activos. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El Administrador solicita crear un nuevo servicio. |
|  | 2 | El sistema presenta el formulario solicitando los datos generales: denominación, descripción, precio y tiempo estimado. |
|  | 3 | El Administrador ingresa los datos y solicita estructurar la composición técnica del servicio. |
|  | 4 | El sistema solicita la definición secuencial de etapas operativas (CU-040) y la especificación de la receta de insumos químicos medibles con su dosis teórica. |
|  | 5 | El Administrador define las etapas, asigna los insumos químicos correspondientes y confirma el guardado. |
|  | 6 | El sistema valida que el nombre sea único para el tipo de vehículo, que precios y tiempos sean mayores a cero y que los insumos sean químicos activos. |
|  | 7 | El sistema almacena el servicio con su receta técnica y estado "Activo". |
|  | 8 | El sistema registra la creación en auditoría y confirma el alta. |
| **Postcondición** | El servicio queda registrado y disponible para cotizaciones y lavados con su dosificación base precargada. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 6a | Si los datos no cumplen las reglas de validación o el nombre está duplicado, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | La receta teórica sirve de plantilla para el consumo en la ejecución operativa de lavados. |  |

#### **CU-031 \- Modificar servicio** {#cu-031---modificar-servicio}

| UC–031 | Modificar servicio |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes, OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios, IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El Administrador actualiza la configuración comercial, etapas o receta de dosificación de un servicio, con posibilidad de programar la vigencia del nuevo precio para una fecha futura. |  |
| **Precondición** | El usuario debe ser Administrador y el servicio debe existir. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-034 Consultar servicios. |
|  | 2 | El Administrador selecciona el servicio a modificar. |
|  | 3 | El sistema presenta los datos vigentes del servicio, sus etapas, receta técnica y condiciones de precio. |
|  | 4 | El Administrador modifica los datos requeridos. En caso de actualizar el precio, define si la aplicación es inmediata o si se programa para una fecha de entrada en vigencia futura. |
|  | 5 | El Administrador solicita guardar los cambios. |
|  | 6 | El sistema valida la consistencia de los datos numéricos y descriptivos. |
|  | 7 | El sistema actualiza inmediatamente los atributos operativos del servicio. |
|  | 8a | Si el cambio de precio es inmediato: el sistema actualiza el precio del servicio y recalcula los paquetes asociados. |
|  | 8b | Si el cambio de precio es programado: el sistema genera un registro de actualización de precio en estado pendiente con la fecha estipulada, manteniendo vigente el precio actual hasta dicha fecha. |
|  | 9 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | Las modificaciones operativas quedan aplicadas. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 6a | Si hay errores de validación, el sistema informa los campos a corregir. |
|  | 6b | Si la fecha programada es anterior o igual al momento actual, el sistema informa el error y exige una fecha futura válida. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7-8 | 2 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-034 Consultar servicios. |  |

#### **CU-032 \- Desactivar servicio** {#cu-032---desactivar-servicio}

| UC–032 | Desactivar servicio |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios |  |
| **Descripción** | El administrador deshabilita un servicio para que no pueda ser seleccionado en nuevos lavados o turnos. |  |
| **Precondición** | El usuario debe ser Administrador. El servicio debe existir y estar activo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-034 Consultar servicios. |
|  | 2 | El administrador selecciona el servicio a desactivar. |
|  | 3 | El sistema comprueba si el servicio forma parte de paquetes activos. |
|  | 4 | El sistema solicita confirmación advirtiendo el impacto sobre los paquetes si corresponde. |
|  | 5 | El administrador confirma la desactivación. |
|  | 6 | El sistema cambia el estado del servicio a "Inactivo". |
|  | 7 | El sistema registra la acción en auditoría y confirma la desactivación. |
| **Postcondición** | El servicio queda en estado inactivo. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si el administrador cancela la confirmación, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | No afecta a los lavados o turnos registrados previamente. |  |

#### **CU-033 \- Reactivar servicio** {#cu-033---reactivar-servicio}

| UC–033 | Reactivar servicio |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios |  |
| **Descripción** | El administrador habilita nuevamente un servicio previamente desactivado. |  |
| **Precondición** | El usuario debe ser Administrador. El servicio debe existir y estar inactivo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-034 Consultar servicios. |
|  | 2 | El administrador selecciona el servicio a reactivar. |
|  | 3 | El sistema solicita la confirmación de la reactivación. |
|  | 4 | El administrador confirma la reactivación. |
|  | 5 | El sistema cambia el estado del servicio a "Activo". |
|  | 6 | El sistema registra la acción en auditoría y confirma la operación. |
| **Postcondición** | El servicio vuelve a estar disponible para cotizaciones y registros. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el administrador cancela la confirmación, se aborta la reactivación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-034 Consultar servicios. |  |

#### **CU-034 \- Consultar servicios** {#cu-034---consultar-servicios}

| UC–034 | Consultar servicios |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios |  |
| **Descripción** | El personal consulta la lista de servicios con opciones de filtrado, ordenamiento y paginación. |  |
| **Precondición** | El usuario debe estar autenticado. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario accede a la sección de servicios. |
|  | 2 | El sistema recupera los servicios aplicando los filtros por defecto. |
|  | 3 | El sistema presenta el listado de servicios registrados. |
|  | 4 | El usuario puede aplicar filtros y ordenamiento según los campos y criterios disponibles. |
|  | 5 | El usuario navega entre páginas mediante los controles de paginación. |
|  | 6 | El sistema actualiza la visualización según los criterios seleccionados. |
| **Postcondición** | El usuario visualiza los servicios según los criterios aplicados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no existen registros que coincidan, el sistema muestra un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Por defecto presenta únicamente servicios activos. |  |

#### **CU-035 \- Buscar servicios** {#cu-035---buscar-servicios}

| UC–035 | Buscar servicios |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios |  |
| **Descripción** | El personal busca servicios por coincidencia de texto en tiempo real. |  |
| **Precondición** | El usuario debe estar autenticado. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario accede a la sección de servicios. |
|  | 2 | El usuario ingresa un término en el campo de búsqueda. |
|  | 3 | El sistema busca coincidencias según los criterios de búsqueda disponibles. |
|  | 4 | El sistema presenta los resultados coincidentes. |
|  | 5 | El usuario selecciona un servicio de la lista. |
| **Postcondición** | El usuario visualiza los servicios que coinciden con el criterio. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si no hay coincidencias, el sistema muestra un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3-4 | 500ms |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | La búsqueda no distingue entre mayúsculas y minúsculas. |  |

#### **CU-036 \- Crear tipo de servicio** {#cu-036---crear-tipo-de-servicio}

| UC–036 | Crear tipo de servicio |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–05 Información sobre Tipos de Servicio |  |
| **Descripción** | El administrador registra una nueva categoría o tipo de servicio para clasificar las prestaciones del lavadero. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador solicita crear un nuevo tipo de servicio. |
|  | 2 | El sistema presenta el formulario solicitando la denominación del tipo de servicio. |
|  | 3 | El administrador ingresa los datos requeridos. |
|  | 4 | El administrador solicita guardar el registro. |
|  | 5 | El sistema valida que la denominación sea única y no se encuentre duplicada. |
|  | 6 | El sistema almacena el nuevo tipo de servicio con estado "Activo". |
|  | 7 | El sistema registra la acción en auditoría y confirma la creación. |
| **Postcondición** | El tipo de servicio queda registrado y disponible para clasificar servicios. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si el nombre ya existe, el sistema rechaza el alta e informa la duplicidad. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Permite organizar los paquetes asegurando un único servicio por tipo. |  |

#### **CU-037 \- Eliminar tipo de servicio** {#cu-037---eliminar-tipo-de-servicio}

| UC–037 | Eliminar tipo de servicio |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–05 Información sobre Tipos de Servicio, IRQ–04 Información sobre Servicios |  |
| **Descripción** | El administrador elimina una categoría de servicio que no posea servicios asociados. |  |
| **Precondición** | El usuario debe ser Administrador. El tipo de servicio debe existir. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador selecciona el tipo de servicio a eliminar. |
|  | 2 | El sistema comprueba que no existan servicios asociados a dicha categoría. |
|  | 3 | El sistema solicita confirmación para la eliminación. |
|  | 4 | El administrador confirma la operación. |
|  | 5 | El sistema elimina el tipo de servicio de la base de datos. |
|  | 6 | El sistema registra la acción en auditoría y confirma la eliminación. |
| **Postcondición** | El tipo de servicio queda eliminado del sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si existen servicios vinculados a esta categoría, el sistema bloquea la eliminación. |
|  | 4a | Si el administrador cancela, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Baja |  |
| **Comentarios** | Aplica integridad referencial estricta. |  |

#### **CU-038 \- Crear tipo de vehículo** {#cu-038---crear-tipo-de-vehículo}

| UC–038 | Crear tipo de vehículo |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–06 Información sobre Tipos de Vehículo |  |
| **Descripción** | El administrador define un nuevo tipo de vehículo con sus reglas de validación y dotación operativa requerida. |  |
| **Precondición** | El usuario debe ser Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador solicita registrar un nuevo tipo de vehículo. |
|  | 2 | El sistema presenta el formulario solicitando los datos del tipo de vehículo. |
|  | 3 | El administrador ingresa los datos solicitados. |
|  | 4 | El administrador solicita guardar el registro. |
|  | 5 | El sistema valida que el nombre sea único y que los parámetros operativos sean válidos. |
|  | 6 | El sistema almacena el tipo de vehículo con estado "Activo". |
|  | 7 | El sistema registra la creación en auditoría y confirma el éxito. |
| **Postcondición** | El tipo de vehículo queda registrado y disponible para tarifas y validaciones. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si los datos ya existen o los valores son inválidos, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Permite adaptar el sistema a diferentes tipos de rodados. |  |

#### **CU-039 \- Eliminar tipo de vehículo** {#cu-039---eliminar-tipo-de-vehículo}

| UC–039 | Eliminar tipo de vehículo |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–06 Información sobre Tipos de Vehículo, IRQ–03 Información sobre Vehículos, IRQ–04 Información sobre Servicios |  |
| **Descripción** | El administrador elimina un tipo de vehículo que no tenga vehículos ni servicios asociados. |  |
| **Precondición** | El usuario debe ser Administrador. El tipo de vehículo debe existir. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador selecciona el tipo de vehículo a eliminar. |
|  | 2 | El sistema valida que no existan vehículos registrados ni servicios asociados a este tipo. |
|  | 3 | El sistema solicita confirmación de la eliminación. |
|  | 4 | El administrador confirma la acción. |
|  | 5 | El sistema elimina el tipo de vehículo de la base de datos. |
|  | 6 | El sistema registra la acción en auditoría y emite la confirmación. |
| **Postcondición** | El tipo de vehículo queda eliminado del sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si existen vehículos o servicios que utilizan este tipo, el sistema impide la eliminación. |
|  | 4a | Si el administrador cancela, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Baja |  |
| **Comentarios** | Asegura la integridad referencial de la base de datos. |  |

#### **CU-040 \- Gestionar etapas del servicio** {#cu-040---gestionar-etapas-del-servicio}

| UC–040 | Gestionar etapas del servicio |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes, OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios, IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El Administrador define la secuencia ordenada de tareas que integran un servicio y asocia los insumos químicos específicos que se consumen en cada una. |  |
| **Precondición** | El usuario debe ser Administrador y el servicio debe existir. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El Administrador accede a la gestión de etapas del servicio. |
|  | 2 | El sistema presenta las etapas actuales asociadas al servicio con sus insumos vinculados. |
|  | 3 | El Administrador agrega, edita, reordena o quita etapas, asignando a cada una el insumo químico medible y la cantidad teórica consumida. |
|  | 4 | El Administrador confirma los cambios. |
|  | 5 | El sistema valida la correlatividad de las etapas y la consistencia de las dosis de insumos. |
|  | 6 | El sistema actualiza la estructura de etapas e insumos en la base de datos. |
|  | 7 | El sistema registra la acción en auditoría. |
| **Postcondición** | La secuencia de etapas y su consumo teórico quedan actualizados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si se asigna un insumo clasificado como activo o consumible físico no medible, el sistema informa que solo se permiten insumos químicos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Las etapas se ejecutan secuencialmente durante el lavado, posibilitando el descuento escalonado de stock. |  |

#### **CU-041 \- Crear paquete de servicios** {#cu-041---crear-paquete-de-servicios}

| UC–041 | Crear paquete de servicios |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–07 Información sobre Paquetes de Servicios |  |
| **Descripción** | El administrador agrupa dos o más servicios para ofrecerlos de forma combinada con un beneficio promocional. |  |
| **Precondición** | El usuario debe tener rol de Administrador. Deben existir servicios activos compatibles. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador accede a la sección de paquetes. |
|  | 2 | El administrador inicia la creación de un nuevo paquete. |
|  | 3 | El sistema presenta el formulario de creación solicitando los datos del paquete. |
|  | 4 | El administrador completa los datos y selecciona los servicios a incluir. |
|  | 5 | El sistema calcula automáticamente los valores derivados del paquete. |
|  | 6 | El administrador confirma el registro. |
|  | 7 | El sistema valida el cumplimiento de las reglas de composición. |
|  | 8 | El sistema guarda el paquete en la base de datos. |
|  | 9 | El sistema presenta un mensaje de éxito y registra la acción en auditoría. |
| **Postcondición** | El paquete queda registrado y disponible para su uso. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 7a | Si no cumple con las restricciones mínimas de servicios o tipos, el sistema bloquea el guardado e informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 8 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Los valores finales se calculan según los servicios componentes. |  |

#### **CU-042 \- Modificar paquete de servicios** {#cu-042---modificar-paquete-de-servicios}

| UC–042 | Modificar paquete de servicios |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–07 Información sobre Paquetes de Servicios |  |
| **Descripción** | El administrador actualiza la configuración de un paquete existente. |  |
| **Precondición** | El usuario debe tener rol de Administrador. El paquete debe existir. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta CU-045 Consultar paquetes de servicios. |
|  | 2 | El administrador selecciona el paquete a modificar. |
|  | 3 | El sistema presenta el formulario de edición con los datos actuales del paquete. |
|  | 4 | El administrador actualiza los campos editables deseados. |
|  | 5 | El sistema recalcula los valores derivados en tiempo real. |
|  | 6 | El administrador confirma los cambios. |
|  | 7 | El sistema valida las reglas de negocio del paquete. |
|  | 8 | El sistema actualiza el registro en la base de datos. |
|  | 9 | El sistema presenta un mensaje de éxito y registra la acción en auditoría. |
| **Postcondición** | El paquete queda actualizado. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 7a | Si hay errores de validación, el sistema informa los campos a corregir. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 8 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Baja |  |
| **Comentarios** | Incluye el caso de uso CU-045 Consultar paquetes de servicios. |  |

#### **CU-043 \- Desactivar paquete de servicios** {#cu-043---desactivar-paquete-de-servicios}

| UC–043 | Desactivar paquete de servicios |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–07 Información sobre Paquetes de Servicios |  |
| **Descripción** | El administrador deshabilita un paquete para que no se ofrezca en nuevos turnos o lavados. |  |
| **Precondición** | El usuario debe ser Administrador. El paquete debe existir y estar activo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-045 Consultar paquetes de servicios. |
|  | 2 | El administrador selecciona el paquete a desactivar. |
|  | 3 | El sistema solicita la confirmación de la desactivación. |
|  | 4 | El administrador confirma la operación. |
|  | 5 | El sistema cambia el estado del paquete a "Inactivo". |
|  | 6 | El sistema registra la acción en auditoría y emite la confirmación. |
| **Postcondición** | El paquete pasa a estado inactivo. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el administrador cancela, se interrumpe la operación sin aplicar cambios. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Baja |  |
| **Comentarios** | No altera los turnos ya agendados con este paquete. |  |

#### **CU-044 \- Reactivar paquete de servicios** {#cu-044---reactivar-paquete-de-servicios}

| UC–044 | Reactivar paquete de servicios |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–07 Información sobre Paquetes de Servicios, IRQ–04 Información sobre Servicios |  |
| **Descripción** | El administrador habilita nuevamente un paquete previamente desactivado, verificando que sus servicios componentes sigan activos. |  |
| **Precondición** | El usuario debe ser Administrador. El paquete debe existir y estar inactivo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-045 Consultar paquetes de servicios. |
|  | 2 | El administrador selecciona el paquete a reactivar. |
|  | 3 | El sistema comprueba que todos los servicios que componen el paquete se encuentren en estado activo. |
|  | 4 | El sistema solicita confirmación de la reactivación. |
|  | 5 | El administrador confirma la operación. |
|  | 6 | El sistema cambia el estado del paquete a "Activo". |
|  | 7 | El sistema registra la acción en auditoría y confirma la reactivación. |
| **Postcondición** | El paquete de servicios vuelve a estar disponible para su uso. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si alguno de los servicios incluidos está inactivo, el sistema impide la reactivación y notifica la necesidad de reactivar el servicio o modificar el paquete. |
|  | 5a | Si el administrador cancela la confirmación, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Baja |  |
| **Comentarios** | Incluye el caso de uso CU-045 Consultar paquetes de servicios. |  |

#### **CU-045 \- Consultar paquetes de servicios** {#cu-045---consultar-paquetes-de-servicios}

| UC–045 | Consultar paquetes de servicios |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–07 Información sobre Paquetes de Servicios |  |
| **Descripción** | El personal visualiza los paquetes disponibles con sus parámetros consolidados. |  |
| **Precondición** | El usuario debe estar autenticado. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario accede a la sección de paquetes. |
|  | 2 | El sistema recupera los paquetes aplicando los filtros por defecto. |
|  | 3 | El sistema calcula dinámicamente los valores agregados de cada paquete. |
|  | 4 | El sistema presenta el listado de paquetes de servicios con sus valores consolidados. |
|  | 5 | El usuario puede aplicar filtros y ordenamiento según los campos y criterios disponibles. |
|  | 6 | El usuario navega entre páginas mediante los controles de paginación. |
|  | 7 | El sistema actualiza la visualización según los criterios seleccionados. |
| **Postcondición** | El usuario visualiza los paquetes según los criterios aplicados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no existen paquetes registrados, el sistema presenta un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-4 | 2 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Por defecto presenta únicamente paquetes activos. |  |

#### **CU-111 \- Actualizar precios masivamente** {#cu-111---actualizar-precios-masivamente}

| UC–111 | Actualizar precios masivamente |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios |  |
| **Descripción** | El Administrador aplica un ajuste tarifario uniforme sobre múltiples servicios simultáneamente (por porcentaje o monto fijo), con aplicación inmediata o programada para una fecha futura. |  |
| **Actor/es** | Administrador |  |
| **Precondición** | El usuario debe tener rol de Administrador. Deben existir servicios activos registrados. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El Administrador solicita realizar una actualización masiva de precios. |
|  | 2 | El sistema presenta los criterios de alcance disponibles (todos los servicios, por tipo de vehículo o por tipo de servicio) y las reglas de ajuste (porcentaje incremental o valor fijo). |
|  | 3 | El Administrador define los criterios de alcance, el factor de aumento y la modalidad de vigencia (inmediata o programada para una fecha futura). |
|  | 4 | El sistema calcula la proyección de los nuevos precios resultantes para cada servicio involucrado y presenta una vista previa de la actualización. |
|  | 5 | El Administrador confirma la operación. |
|  | 6a | Si la aplicación es inmediata: el sistema actualiza de forma atómica los precios en el catálogo de servicios, recalcula los paquetes asociados y registra la auditoría. |
|  | 6b | Si la aplicación es programada: el sistema persiste la programación en estado Pendiente con la fecha estipulada y la lista de nuevos importes proyectados, sin alterar las tarifas actuales del catálogo. |
|  | 7 | El sistema emite la confirmación correspondiente y registra la acción en auditoría. |
| **Postcondición** | Los precios quedan actualizados de inmediato o programados para su impacto en la fecha definida. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si ningún servicio coincide con los filtros de alcance seleccionados, el sistema informa la situación y solicita ajustar los criterios. |
|  | 5a | Si se seleccionó vigencia programada pero la fecha no es futura, el sistema solicita corregir el parámetro temporal. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 4-6 | 2 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Agiliza la gestión comercial ante variaciones inflacionarias o cambios generales de lista. |  |

#### **CU-112 \- Aplicar actualizaciones programadas de precios** {#cu-112---aplicar-actualizaciones-programadas-de-precios}

| UC–112 | Aplicar actualizaciones programadas de precios |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios |  |
| **Descripción** | Proceso automático en segundo plano que examina periódicamente las programaciones de precios pendientes y aplica las nuevas tarifas cuando se alcanza su fecha de vigencia. |  |
| **Actor/es** | Sistema |  |
| **Precondición** | Existen programaciones de precios en estado Pendiente cuya fecha de vigencia es menor o igual al momento actual. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema identifica las programaciones de precios individuales o masivas pendientes de entrada en vigor. |
|  | 2 | Por cada programación alcanzada por la fecha, el sistema recupera la nómina de servicios y los nuevos importes asignados. |
|  | 3 | El sistema actualiza los precios en los documentos de los servicios respectivos. |
|  | 4 | El sistema recalcula los precios originales, descuentos y precios finales de los paquetes de servicios asociados. |
|  | 5 | El sistema actualiza el estado de la programación a Aplicado y registra la marca temporal de ejecución. |
|  | 6 | El sistema asienta el evento en auditoría indicando la ejecución del proceso automatizado. |
| **Postcondición** | El catálogo comercial asume las nuevas tarifas programadas a partir de la fecha establecida. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si un servicio involucrado en la programación fue dado de baja previamente, el sistema omite su actualización y continúa con los restantes. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-5 | 2 segundos |
| **Frecuencia** | Ocasional (ejecución al inicio del día o temporizada) |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Permite preparar listas de precios con semanas de antelación sin requerir intervención manual durante feriados o fines de semana. |  |

#### **CU-113 \- Cancelar actualización programada de precios** {#cu-113---cancelar-actualización-programada-de-precios}

| UC–113 | Cancelar actualización programada de precios |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes |  |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios |  |
| **Descripción** | El Administrador anula una programación de precios pendiente antes de que se cumpla su fecha de vigencia, dejando sin efecto el incremento proyectado. |  |
| **Actor/es** | Administrador |  |
| **Precondición** | El usuario debe ser Administrador. Debe existir una programación en estado Pendiente. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El Administrador accede a la sección de actualizaciones de precios programadas. |
|  | 2 | El sistema presenta el listado de programaciones pendientes con sus fechas de vigencia, servicios alcanzados y montos proyectados. |
|  | 3 | El Administrador selecciona la programación a anular y solicita su cancelación. |
|  | 4 | El sistema solicita confirmación de la cancelación. |
|  | 5 | El Administrador confirma la operación. |
|  | 6 | El sistema actualiza el estado de la programación a Cancelado. |
|  | 7 | El sistema confirma la anulación y asienta el registro en auditoría. |
| **Postcondición** | La programación queda sin efecto y los precios vigentes del catálogo no sufren modificaciones. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si la programación ya fue ejecutada por el sistema, el sistema bloquea la acción informando que no es reversible. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6-7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Otorga reversibilidad total sobre las decisiones comerciales planificadas. |  |

### **Módulo: Registro de Lavados**

#### **CU-046 \- Registrar realización de un servicio (lavado)** {#cu-046---registrar-realización-de-un-servicio-(lavado)}

| UC–046 | Registrar realización de un servicio (lavado) |  |
| :---- | ----- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados, OBJ–06 Planificación y Gestión de Turnos |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados, IRQ–09 Información sobre Turnos, IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos, IRQ–04 Información sobre Servicios |  |
| **Descripción** | El personal inicia una orden operativa de trabajo en el lavadero para un vehículo y cliente. La atención puede originarse a partir de un turno previamente agendado o por ingreso espontáneo directo en el momento. El lavado comienza de forma inmediata. |  |
| **Precondición** | El usuario debe estar autenticado. Deben existir clientes, vehículos, servicios y personal activos. El establecimiento debe encontrarse dentro del horario de atención, contar con capacidad física disponible y los insumos requeridos. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario accede al módulo de registro de lavados. |
|  | 2 | El sistema valida que el horario actual se encuentre dentro de la franja operativa del establecimiento. |
|  | 3 | El sistema comprueba que no se supere la capacidad máxima de vehículos simultáneos en el lavadero. |
|  | 4 | El sistema inicializa el contexto de recepción del vehículo. |
|  | 5 | Bifurcación por modalidad de atención \[Opción A o B\]: |
|  | A | **Atención de turno previamente agendado:** a.1) El usuario selecciona el turno correspondiente de la agenda del día (o ingresa la identificación del vehículo citado). a.2) El sistema identifica la reserva y precarga automáticamente el vehículo, el cliente y los servicios o paquetes pactados. a.3) El sistema adopta como base comercial el precio estimado congelado en el turno. a.4) Si el cliente solicita incorporar servicios adicionales en el mostrador, el usuario los agrega a la orden y el sistema los tarifa al precio de lista vigente. |
|  | B | **Ingreso espontáneo en el momento (sin turno):** b.1) El usuario busca el vehículo por su identificación y selecciona el cliente que entrega la unidad. b.2) El sistema presenta los servicios y paquetes disponibles compatibles con el tipo de vehículo. b.3) El usuario selecciona los servicios o paquete a realizar. b.4) El sistema liquida el importe comercial tomando las tarifas vigentes del catálogo. |
|  | 6 | El sistema totaliza el importe comercial definitivo y calcula la duración estimada total. |
|  | 7 | El sistema informa la dotación mínima de empleados requerida según el tipo de vehículo. |
|  | 8 | El usuario puede registrar descuentos comerciales extraordinarios o notas adicionales. |
|  | 9 | El usuario confirma el inicio del servicio. |
|  | 10 | El sistema asigna los empleados operativos responsables del lavado. |
|  | 11 | El sistema valida la consistencia total de la orden. |
|  | 12 | El sistema crea la orden de lavado con estado en proceso y registra la marca temporal de inicio. |
|  | **Si la atención procedía de un turno agendado (Opción A):** |  |
|  | 13 | El sistema actualiza de forma automática el estado de dicho turno y lo establece en proceso. |
|  | 14 | El sistema confirma el registro exitoso y asienta el evento en auditoría. |
| **Postcondición** | El lavado queda en ejecución en el taller con sus precios fijados, los operarios asignados y el turno llevandose a cabo si correspondía. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si el lavadero se encuentra fuera de horario de atención, el sistema informa el impedimento. |
|  | 3a | Si se supera la capacidad máxima de atención simultánea configurada, el sistema emite una advertencia. |
|  | 10 | Si el sistema falla al asignar el/los empleado/s requerido/s para el servicio por falta de personal, se cancela el caso de uso. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 11-14 | 2 segundos |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Núcleo operativo del sistema para la gestión del servicio. |  |

#### **CU-047 \- Consultar lavados** {#cu-047---consultar-lavados}

| UC–047 | Consultar lavados |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados |  |
| **Descripción** | El personal visualiza los registros de lavados con opciones avanzadas de filtrado, ordenamiento y paginación. |  |
| **Precondición** | El usuario debe estar autenticado. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario accede a la sección de lavados. |
|  | 2 | El sistema recupera los lavados aplicando los filtros por defecto. |
|  | 3 | El sistema presenta el listado de lavados registrados. |
|  | 4 | El usuario puede aplicar filtros y ordenamiento según los campos y criterios disponibles. |
|  | 5 | El usuario navega entre páginas mediante los controles de paginación. |
|  | 6 | El sistema actualiza la visualización según los criterios seleccionados. |
| **Postcondición** | El usuario visualiza los lavados según los criterios aplicados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no hay lavados que coincidan, el sistema muestra un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 2 segundos |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Por defecto prioriza la vista de lavados en curso. |  |

#### **CU-048 \- Buscar lavados** {#cu-048---buscar-lavados}

| UC–048 | Buscar lavados |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados |  |
| **Descripción** | El personal busca lavados mediante términos de coincidencia en tiempo real. |  |
| **Precondición** | El usuario debe estar autenticado. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario accede a la sección de lavados. |
|  | 2 | El usuario ingresa un término en el campo de búsqueda. |
|  | 3 | El sistema busca coincidencias según los criterios de búsqueda disponibles. |
|  | 4 | El sistema presenta los resultados coincidentes. |
|  | 5 | El usuario selecciona un lavado del listado. |
| **Postcondición** | El usuario localiza los lavados correspondientes al criterio ingresado. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si no existen coincidencias, el sistema muestra un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3-4 | 500ms |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | La búsqueda no distingue entre mayúsculas y minúsculas. |  |

#### **CU-049 \- Ver detalle de lavado** {#cu-049---ver-detalle-de-lavado}

| UC–049 | Ver detalle de lavado |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados |  |
| **Descripción** | El personal accede a la ficha completa de un lavado, su avance operativo y las acciones contextuales disponibles. |  |
| **Precondición** | El usuario debe estar autenticado. El lavado debe existir. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-047 Consultar lavados. |
|  | 2 | El usuario selecciona un lavado para inspeccionar su detalle. |
|  | 3 | El sistema recupera toda la información vinculada al registro. |
|  | 4 | El sistema presenta el detalle completo del lavado, incluyendo información del cliente, vehículo, servicios, etapas asociadas, empleados, tiempos y pagos. |
|  | 5 | El sistema habilita las acciones contextuales según el estado actual del lavado. |
| **Postcondición** | El usuario visualiza la totalidad de la información y estado del lavado. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si el registro no se encuentra disponible, el sistema informa el error y retorna a la lista. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3-4 | 1 segundo |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Incluye el caso de uso CU-047 Consultar lavados. |  |

#### **CU-050 \- Iniciar servicio en lavado** {#cu-050---iniciar-servicio-en-lavado}

| UC–050 | Iniciar servicio en lavado |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados |  |
| **Descripción** | El personal registra el inicio formal de un servicio específico dentro de un lavado en curso, registrando la marca de tiempo correspondiente. |  |
| **Precondición** | El usuario debe estar autenticado. El lavado debe estar "EnProceso" y el servicio pendiente de inicio. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
|  | 2 | El usuario solicita iniciar el servicio seleccionado. |
|  | 3 | El sistema registra la fecha y hora de inicio del servicio. |
|  | 4 | El sistema actualiza el estado del servicio a "EnProceso". |
|  | 5 | Si el servicio cuenta con etapas, el sistema habilita el inicio de la primera etapa. |
|  | 6 | El sistema registra la acción en auditoría y actualiza el estado del lavado. |
| **Postcondición** | El servicio queda registrado en estado de ejecución con su tiempo de inicio. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | \- | \- |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3-5 | 1 segundo |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. |  |

#### **CU-051 \- Iniciar etapa de servicio** {#cu-051---iniciar-etapa-de-servicio}

| UC–051 | Iniciar etapa de servicio |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados |  |
| **Descripción** | El personal inicia una fase operativa específica dentro de un servicio en ejecución. |  |
| **Precondición** | El servicio debe encontrarse en curso y las etapas previas finalizadas. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
|  | 2 | El sistema presenta las etapas del servicio y sus estados. |
|  | 3 | El usuario solicita el inicio de la etapa correspondiente. |
|  | 4 | El sistema valida la correlatividad de las etapas previas. |
|  | 5 | El sistema registra la marca temporal de inicio y actualiza el estado de la etapa. |
|  | 6 | El sistema actualiza la vista de detalle y registra la acción en auditoría. |
| **Postcondición** | La etapa queda registrada en estado en proceso. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si existen etapas previas pendientes, el sistema frena la acción e informa la correlatividad requerida. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5 | 1 segundo |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. |  |

#### **CU-052 \- Finalizar etapa de servicio** {#cu-052---finalizar-etapa-de-servicio}

| UC–052 | Finalizar etapa de servicio |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados, OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados, IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El personal marca como concluida una fase operativa, visualizando las cantidades teóricas de insumos, permitiendo su ajuste real y exigiendo justificación obligatoria ante cualquier desvío antes de impactar el inventario. |  |
| **Precondición** | La etapa debe encontrarse en estado "En Proceso". |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
|  | 2 | El usuario solicita finalizar la etapa operativa en curso. |
|  | 3 | El sistema presenta los insumos químicos asignados a la etapa con sus cantidades estándar predeterminadas. |
|  | 4 | El usuario revisa los consumos; si la cantidad real varió respecto a la teórica, modifica el valor e ingresa de forma obligatoria la justificación del desvío. |
|  | 5 | El usuario confirma la finalización. |
|  | 6 | El sistema valida que las cantidades sean mayores o iguales a cero y que existan justificaciones ante cualquier desvío. |
|  | 7 | El sistema registra la marca temporal de culminación y actualiza el estado de la etapa a completada. |
|  | 8 | El sistema descuenta las cantidades reales del stock disponible y asienta los movimientos en el Kardex. |
|  | 9 | El sistema evalúa si algún insumo alcanzó el stock crítico; de ser así y permitirlo, dispara la orden automática (CU-106). |
|  | 10 | Si corresponde a la última etapa, completa el servicio; además puede invocar CU-079 para notificar al cliente. |
|  | 11 | El sistema registra la acción en auditoría. |
| **Postcondición** | La etapa queda finalizada, el stock actualizado y el movimiento de Kardex registrado. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 6a | Si se modificó la cantidad teórica pero no se especificó el motivo del desvío, el sistema bloquea la confirmación exigiendo la justificación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6-8 | 1 segundo |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. |  |

#### **CU-053 \- Finalizar servicio en lavado** {#cu-053---finalizar-servicio-en-lavado}

| UC–053 | Finalizar servicio en lavado |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados, OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados, IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El personal registra la finalización de un servicio puntual dentro de un lavado, consolidando que sus etapas e insumos hayan sido procesados. |  |
| **Precondición** | El servicio debe encontrarse en estado en proceso con todos los servicios que lo componen finalizados. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
|  | 2 | El usuario solicita finalizar el servicio correspondiente. |
|  | 3 | El sistema verifica el estado de las etapas asociadas y la imputación de sus consumos en el Kardex. |
|  | 4 | El sistema actualiza el estado del servicio en completado y asienta la marca temporal de finalización. |
|  | 5 | El sistema actualiza la vista del lavado y registra la acción en auditoría. |
| **Postcondición** | El servicio queda registrado como completado. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si existen etapas inconclusas, el sistema solicita confirmación antes de registrar el cierre. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3-4 | 1 segundo |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. |  |

#### **CU-054 \- Finalizar lavado completo** {#cu-054---finalizar-lavado-completo}

| UC–054 | Finalizar lavado completo |  |
| :---- | ----- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados, OBJ–06 Planificación y Gestión de Turnos, OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores, IRQ–08 Información sobre Lavados, IRQ–09 Información sobre Turnos, IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos, IRQ–04 Información sobre Servicios |  |
| **Descripción** | El personal concluye la orden integral de lavado registrando los tiempos finales, consolidando el consumo real de insumos de la orden, finalizando el turno asociado si correspondía y disparando el aviso de retiro al cliente. |  |
| **Precondición** | El lavado debe encontrarse en estado en proceso. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
|  | 2 | El usuario solicita finalizar el lavado integral. |
|  | 3 | El sistema valida que todos los servicios estén concluidos y totaliza los consumos reales registrados en el Kardex. |
|  | 4 | El sistema actualiza el estado general del lavado según el resultado de sus servicios y asienta la marca temporal final. |
|  | **Si la atención procedía de un turno agendado:** |  |
|  | 5 | El sistema actualiza de forma automática el estado de dicho turno a completado |
|  | 6 | El sistema invoca la notificación de lavado concluido al cliente (CU-080). |
|  | 7 | El sistema confirma el cierre y registra la acción en auditoría. |
| **Postcondición** | El lavado queda finalizado y listo para retiro con sus costos consolidados, y el turno asociado queda completado si corresponde. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si existen servicios pendientes o cancelados, el sistema solicita confirmación y justificación antes de concluir. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3-6 | 2 segundos |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. |  |

#### **CU-055 \- Cancelar lavado** {#cu-055---cancelar-lavado}

| UC–055 | Cancelar lavado |  |
| :---- | ----- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados, OBJ–06 Planificación y Gestión de Turnos, OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores, IRQ–08 Información sobre Lavados, IRQ–09 Información sobre Turnos, IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos, IRQ–04 Información sobre Servicios |  |
| **Descripción** | El personal anula un lavado en curso registrando el motivo obligatorio; si se completaron etapas previas, los insumos gastados se conservan en Kardex como merma operativa. |  |
| **Precondición** | El lavado debe encontrarse activo o en proceso. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
|  | 2 | El usuario solicita la cancelación del lavado. |
|  | 3 | El sistema solicita el motivo de cancelación obligatorio. |
|  | 4 | El usuario ingresa la justificación correspondiente. |
|  | 5 | El sistema verifica si existieron etapas o servicios previos concluidos: de haberlos, asienta en Kardex los productos descontados como "Merma por cancelación"; si no, no descuenta inventario. |
|  | 6 | El usuario confirma la cancelación. |
|  | 7 | El sistema actualiza el estado del lavado a cancelado y registra la información del motivo. |
|  | **Si la atención procedía de un turno agendado:** |  |
|  | 8 | El sistema actualiza de forma automática el estado de dicho turno a cancelado, asentando el motivo correspondiente |
|  | 9 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | El lavado queda cancelado y el stock preserva la trazabilidad de los químicos ya utilizados físicamente. Además el turno asociado queda cancelado si corresponde. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si no se ingresa justificación, el sistema exige completarla para continuar. |
|  | 6a | Si el usuario cancela la confirmación, se aborta la anulación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. |  |

#### **CU-056 \- Cancelar servicio en lavado** {#cu-056---cancelar-servicio-en-lavado}

| UC–056 | Cancelar servicio en lavado |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados, OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados, IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El personal anula un servicio particular asentando los insumos consumidos en etapas previas como merma y recalculando el total de la orden. |  |
| **Precondición** | El lavado debe estar en proceso y el servicio no concluido. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
|  | 2 | El usuario solicita cancelar un servicio específico. |
|  | 3 | El sistema solicita el motivo de cancelación obligatorio. |
|  | 4 | El usuario ingresa el motivo correspondiente y confirma. |
|  | 5 | El sistema valida las etapas: aquellas concluidas conservan su consumo en Kardex como merma, mientras que las pendientes quedan anuladas sin impacto en el stock. |
|  | 6 | El sistema actualiza el estado del servicio a cancelado y recalcula los montos del lavado. |
|  | 7 | El sistema actualiza la vista de detalle y registra la acción en auditoría. |
| **Postcondición** | El servicio queda cancelado, los montos ajustados y las existencias preservan la salida real. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si se omite la justificación, el sistema impide cancelar el servicio. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. |  |

#### **CU-057 \- Registrar pago recibido** {#cu-057---registrar-pago-recibido}

| UC–057 | Registrar pago recibido |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–05 Registro de Pagos |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados |  |
| **Descripción** | El personal asienta el cobro total del saldo pendiente de un lavado, actualizando su estado a "Pagado". |  |
| **Precondición** | El lavado debe existir y contar con saldo pendiente de pago. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
|  | 2 | El usuario solicita registrar un pago. |
|  | 3 | El sistema presenta el balance del lavado indicando el saldo pendiente. |
|  | 4 | El usuario confirma el monto total del saldo y selecciona el medio de pago. |
|  | 5 | El usuario solicita asentar el pago. |
|  | 6 | El sistema valida el monto, registra la transacción con fecha y hora, y actualiza el estado a "Pagado". |
|  | 7 | El sistema registra la acción en auditoría y emite la confirmación de pago. |
| **Postcondición** | El lavado queda registrado como totalmente pagado. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 6a | Si el monto ingresado es inconsistente con el saldo, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Almacena el método de pago utilizado para reportes financieros. |  |

#### **CU-058 \- Registrar pago parcial** {#cu-058---registrar-pago-parcial}

| UC–058 | Registrar pago parcial |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–05 Registro de Pagos |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados |  |
| **Descripción** | El personal asienta un abono parcial a la cuenta de un lavado pendiente. |  |
| **Precondición** | El lavado debe existir y contar con saldo pendiente. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
|  | 2 | El usuario solicita registrar un pago. |
|  | 3 | El sistema presenta el balance y saldos del lavado. |
|  | 4 | El usuario ingresa el monto y selecciona el medio de pago correspondiente. |
|  | 5 | El usuario confirma la transacción. |
|  | 6 | El sistema valida que el valor no supere el saldo pendiente. |
|  | 7 | El sistema asienta el pago, actualiza los balances y el estado de la cuenta. |
|  | 8 | El sistema presenta la confirmación con el saldo resultante y registra la acción en auditoría. |
| **Postcondición** | El pago parcial queda registrado y el saldo actualizado. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 6a | Si el monto es menor o igual a cero o excede el saldo restante, el sistema rechaza la operación e informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. |  |

#### **CU-059 \- Marcar vehículo como retirado** {#cu-059---marcar-vehículo-como-retirado}

| UC–059 | Marcar vehículo como retirado |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados |  |
| **Descripción** | El personal registra la entrega y salida efectiva de la unidad del establecimiento. |  |
| **Precondición** | El lavado debe encontrarse finalizado. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
|  | 2 | El usuario solicita registrar el retiro del vehículo. |
|  | 3 | El sistema presenta los clientes o responsables vinculados. |
|  | 4 | El usuario selecciona la persona que retira la unidad. |
|  | 5 | El usuario confirma la entrega. |
|  | 6 | El sistema actualiza el estado de retiro y asienta la marca temporal. |
|  | 7 | El sistema presenta la confirmación y registra la acción en auditoría. |
| **Postcondición** | El vehículo queda registrado como retirado. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si no se especifica responsable, el sistema permite registrar la salida con advertencia. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. |  |

#### **CU-060 \- Calcular duración estimada de lavado** {#cu-060---calcular-duración-estimada-de-lavado}

| UC–060 | Calcular duración estimada de lavado |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes, OBJ–04 Registro y Gestión de Lavados |  |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios, IRQ–06 Información sobre Tipos de Vehículo, IRQ–08 Información sobre Lavados |  |
| **Descripción** | Proceso del sistema que calcula la duración estimada total de un lavado en función del tipo de vehículo y los servicios o paquetes seleccionados. |  |
| **Precondición** | Ejecutado automáticamente durante el registro de un turno o lavado. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema recibe los servicios o paquetes solicitados y el tipo de vehículo. |
|  | 2 | El sistema recupera los tiempos estimados parametrizados para cada servicio según el tipo de vehículo. |
|  | 3 | El sistema suma los tiempos individuales aplicando reglas de optimización si existen servicios concurrentes. |
|  | 4 | El sistema retorna la duración estimada total en minutos. |
| **Postcondición** | La duración estimada queda calculada y disponible para la agenda y el lavado. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | \- | \- |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-4 | \< 500ms |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Esencial para evitar solapamientos en la planificación de turnos. |  |

### **Módulo: Configuración**

#### **CU-061 \- Configurar horarios del lavadero** {#cu-061---configurar-horarios-del-lavadero}

| UC–061 | Configurar horarios del lavadero |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–11 Gestión de Configuración del Sistema |  |
| **Requisitos asociados** | IRQ–11 Información de Configuración |  |
| **Descripción** | El administrador establece los días y rangos horarios de atención del lavadero. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador solicita configurar los horarios de operación. |
|  | 2 | El sistema presenta la configuración horaria actual por cada día de la semana. |
|  | 3 | El administrador define los días laborales y los horarios de apertura y cierre. |
|  | 4 | El administrador solicita guardar los cambios. |
|  | 5 | El sistema valida que la hora de apertura sea anterior a la de cierre. |
|  | 6 | El sistema actualiza los parámetros de horarios en el sistema. |
|  | 7 | El sistema registra la acción en auditoría y confirma la configuración. |
| **Postcondición** | Los horarios de atención quedan actualizados y rigen la asignación de turnos. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si los rangos horarios son inconsistentes, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Restringe automáticamente la solicitud de turnos fuera del horario comercial. |  |

#### **CU-062 \- Configurar capacidad concurrente** {#cu-062---configurar-capacidad-concurrente}

| UC–062 | Configurar capacidad concurrente |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–11 Gestión de Configuración del Sistema |  |
| **Requisitos asociados** | IRQ–11 Información de Configuración del Sistema |  |
| **Descripción** | El administrador define los límites de operaciones simultáneas admitidas en el lavadero. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador accede a la sección de configuración. |
|  | 2 | El sistema presenta los parámetros de capacidad actuales. |
|  | 3 | El administrador actualiza los valores de configuración requeridos. |
|  | 4 | El administrador confirma el guardado. |
|  | 5 | El sistema valida que los valores se encuentren en rangos válidos. |
|  | 6 | El sistema actualiza los parámetros en la base de datos. |
|  | 7 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | Los parámetros de capacidad quedan actualizados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si los valores son inválidos, el sistema presenta el error correspondiente. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Afecta directamente la validación al admitir nuevos lavados simultáneos. |  |

#### **CU-063 \- Configurar tiempos de tolerancia y notificación** {#cu-063---configurar-tiempos-de-tolerancia-y-notificación}

| UC–063 | Configurar tiempos de tolerancia y notificación |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–11 Gestión de Configuración del Sistema, OBJ–12 Notificación al Cliente |  |
| **Requisitos asociados** | IRQ–11 Información de Configuración |  |
| **Descripción** | El administrador parametriza los minutos de tolerancia de espera para turnos y el tiempo de anticipación para el envío de recordatorios. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador solicita configurar los tiempos de tolerancia y avisos. |
|  | 2 | El sistema presenta los valores actuales configurados. |
|  | 3 | El administrador ingresa los parámetros de tolerancia y anticipación de notificaciones. |
|  | 4 | El administrador solicita guardar los cambios. |
|  | 5 | El sistema valida que los valores sean positivos y coherentes. |
|  | 6 | El sistema actualiza la configuración en la base de datos. |
|  | 7 | El sistema registra la acción en auditoría y confirma la operación. |
| **Postcondición** | Los parámetros de tolerancia y recordatorios quedan actualizados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si los valores son inválidos, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Rige la cancelación automática de turnos por inasistencia y el disparo de alertas. |  |

#### **CU-064 \- Configurar duración de sesión** {#cu-064---configurar-duración-de-sesión}

| UC–064 | Configurar duración de sesión |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–11 Gestión de Configuración del Sistema |  |
| **Requisitos asociados** | IRQ–11 Información de Configuración |  |
| **Descripción** | El administrador establece el tiempo de inactividad antes del cierre automático y la duración máxima permitida para una sesión de usuario. |  |
| **Precondición** | El usuario debe ser Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador solicita configurar los parámetros de sesión. |
|  | 2 | El sistema presenta los valores vigentes de inactividad y duración máxima. |
|  | 3 | El administrador ingresa los nuevos límites temporales. |
|  | 4 | El administrador solicita guardar la configuración. |
|  | 5 | El sistema valida que los tiempos sean mayores a cero y coherentes. |
|  | 6 | El sistema actualiza la configuración global del sistema. |
|  | 7 | El sistema registra la acción en auditoría y confirma el cambio. |
| **Postcondición** | Los nuevos límites de expiración rigen para las sesiones activas y futuras. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si los valores son inválidos, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Impacta de forma directa sobre la ejecución de CU-004. |  |

#### **CU-065 \- Configurar nombre y ubicación del lavadero** {#cu-065---configurar-nombre-y-ubicación-del-lavadero}

| UC–065 | Configurar nombre y ubicación del lavadero |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–11 Gestión de Configuración del Sistema |  |
| **Requisitos asociados** | IRQ–11 Información de Configuración del Sistema |  |
| **Descripción** | El administrador actualiza la información institucional y de contacto del establecimiento. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador accede a la sección de configuración. |
|  | 2 | El sistema presenta la información institucional actual del lavadero. |
|  | 3 | El administrador actualiza los datos institucionales y de contacto deseados. |
|  | 4 | El administrador confirma el guardado. |
|  | 5 | El sistema valida la consistencia de los datos ingresados. |
|  | 6 | El sistema actualiza la información en la base de datos. |
|  | 7 | El sistema presenta un mensaje de éxito y registra la acción en auditoría. |
| **Postcondición** | La información institucional queda actualizada. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si existen fallos de formato o campos obligatorios vacíos, el sistema informa los errores. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Estos datos son utilizados por las plantillas de notificación y el canal conversacional. |  |

#### **CU-066 \- Configurar paso de descuento para paquetes** {#cu-066---configurar-paso-de-descuento-para-paquetes}

| UC–066 | Configurar paso de descuento para paquetes |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–11 Gestión de Configuración del Sistema |  |
| **Requisitos asociados** | IRQ–11 Información de Configuración |  |
| **Descripción** | El administrador establece el valor del incremento porcentual mínimo permitido al configurar descuentos en paquetes de servicios. |  |
| **Precondición** | El usuario debe ser Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador solicita configurar el paso de descuento. |
|  | 2 | El sistema presenta el valor actual configurado. |
|  | 3 | El administrador ingresa el nuevo valor de incremento permitido. |
|  | 4 | El administrador solicita guardar el valor. |
|  | 5 | El sistema valida que el valor se encuentre dentro del rango porcentual permitido. |
|  | 6 | El sistema actualiza el parámetro de configuración. |
|  | 7 | El sistema registra la acción en auditoría y emite la confirmación. |
| **Postcondición** | El nuevo paso de descuento rige para la creación y edición de paquetes. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si el valor no se encuentra en el rango permitido, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Baja |  |
| **Comentarios** | Estandariza la escala de promociones del lavadero. |  |

### **Módulo: Planificación de Turnos**

#### **CU-067 \- Registrar turno** {#cu-067---registrar-turno}

| UC–067 | Registrar turno |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos |  |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos, IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos |  |
| **Descripción** | El personal agenda un nuevo turno para un cliente y vehículo, validando que no existan superposiciones en la agenda y congelando el precio estimado pactado de los servicios solicitados. |  |
| **Precondición** | El usuario debe estar autenticado. El cliente y vehículo deben existir y estar activos. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario solicita agendar un nuevo turno. |
|  | 2 | El sistema presenta el formulario de reserva solicitando los datos del turno. |
|  | 3 | El usuario ingresa los datos solicitados. |
|  | 4 | El sistema ejecuta CU-060 para calcular la duración estimada total. |
|  | 5 | El sistema ejecuta CU-074 para validar la disponibilidad y ausencia de solapamientos. |
|  | 6 | El usuario solicita confirmar el registro del turno. |
|  | 7 | El sistema almacena el turno en estado "Pendiente" y congelando el precio estimado pactado junto con el desglose de servicios solicitados. |
|  | 8 | El sistema registra la acción en auditoría y emite la confirmación. |
| **Postcondición** | El turno queda agendado, reservado en la planificación del lavadero y la tarifa acordada queda congelada para la posterior atención. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si no hay disponibilidad horaria o capacidad disponible, el sistema informa el conflicto y ofrece alternativas. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5-7 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Puede derivar en el envío automático de confirmación por WhatsApp. |  |

#### **CU-068 \- Modificar turno** {#cu-068---modificar-turno}

| UC–068 | Modificar turno |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos |  |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos |  |
| **Descripción** | El personal actualiza la reserva de un turno validando la disponibilidad de la agenda. |  |
| **Precondición** | El usuario debe estar autenticado. El turno debe encontrarse en estado pendiente. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-069 Consultar turnos asignados. |
|  | 2 | El usuario selecciona el turno a reprogramar. |
|  | 3 | El sistema presenta el formulario de edición con los datos actuales del turno. |
|  | 4 | El usuario modifica los campos editables requeridos. |
|  | 5 | El sistema ejecuta CU-075 para validar la disponibilidad en la agenda. |
|  | 6 | El usuario confirma los cambios. |
|  | 7 | El sistema actualiza el turno en la base de datos. |
|  | 8 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | El turno queda reprogramado. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si no existe disponibilidad horaria, el sistema informa el conflicto y sugiere horarios alternativos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-069 Consultar turnos asignados. |  |

#### **CU-069 \- Consultar turnos asignados** {#cu-069---consultar-turnos-asignados}

| UC–069 | Consultar turnos asignados |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos |  |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos |  |
| **Descripción** | Permite al personal consultar la agenda de turnos con filtros por fecha, estado y cliente. |  |
| **Precondición** | El usuario debe estar autenticado en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario solicita consultar los turnos asignados. |
|  | 2 | El sistema recupera los turnos aplicando los filtros de fecha actuales. |
|  | 3 | El sistema presenta la agenda con los turnos planificados. |
|  | 4 | El usuario puede aplicar filtros por estado o cambiar el rango de fechas. |
|  | 5 | El sistema actualiza la agenda presentada según los criterios seleccionados. |
| **Postcondición** | El usuario visualiza la agenda de turnos planificados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no existen turnos para el período indicado, el sistema presenta la agenda vacía. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 1 segundo |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Base para la reorganización y seguimiento de la atención programada. |  |

#### **CU-070 \- Cancelar turno** {#cu-070---cancelar-turno}

| UC–070 | Cancelar turno |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos |  |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos |  |
| **Descripción** | El personal cancela un turno pendiente, liberando el espacio en la agenda y disparando la optimización de turnos posteriores. |  |
| **Precondición** | El turno debe existir y encontrarse en estado "Pendiente". |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-069 Consultar turnos asignados. |
|  | 2 | El usuario selecciona el turno a cancelar. |
|  | 3 | El sistema solicita el motivo de cancelación. |
|  | 4 | El usuario ingresa el motivo y confirma la cancelación. |
|  | 5 | El sistema cambia el estado del turno a "Cancelado". |
|  | 6 | El sistema ejecuta CU-076 Reorganizar agenda ante cancelaciones. |
|  | 7 | El sistema registra la acción en auditoría y emite la confirmación de cancelación. |
| **Postcondición** | El turno queda cancelado y el espacio de agenda liberado. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el usuario no confirma la operación, se aborta la cancelación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5-6 | 2 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Permite reasignar turnos de clientes que acepten adelantar su horario. |  |

#### **CU-071 \- Solicitar turno por WhatsApp** {#cu-071---solicitar-turno-por-whatsapp}

| UC–071 | Solicitar turno por WhatsApp |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–10 Integración con WhatsApp, OBJ–06 Planificación y Gestión de Turnos |  |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos, IRQ–12 Información de Sesiones WhatsApp |  |
| **Descripción** | El cliente solicita y reserva un turno de lavado mediante la interacción guiada con el bot de WhatsApp. |  |
| **Precondición** | El cliente debe estar registrado y autenticado en la sesión de WhatsApp. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El cliente solicita agendar un turno desde el menú de WhatsApp. |
|  | 2 | El sistema presenta los vehículos asociados al cliente. |
|  | 3 | El cliente selecciona el vehículo deseado. |
|  | 4 | El sistema presenta los servicios y paquetes disponibles para ese tipo de vehículo. |
|  | 5 | El cliente selecciona los servicios deseados. |
|  | 6 | El sistema calcula la duración (CU-060) y presenta las fechas y horarios disponibles sin superposición (CU-074). |
|  | 7 | El cliente selecciona el horario deseado. |
|  | 8 | El sistema presenta el resumen del turno y solicita confirmación. |
|  | 9 | El cliente confirma la reserva. |
|  | 10 | El sistema almacena el turno en estado "Pendiente", congelando el precio estimado pactado junto con el desglose de servicios solicitados y registra la auditoría y envía la confirmación con los detalles al cliente. |
| **Postcondición** | El turno queda agendado automáticamente en el sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 7a | Si el horario seleccionado fue ocupado durante la interacción, el sistema informa y presenta nuevos horarios disponibles. |
|  | 9a | Si el cliente cancela la confirmación, se descarta la reserva. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 10 | 2 segundos |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Automatiza completamente la agenda sin intervención del personal. |  |

#### **CU-072 \- Consultar turnos próximos por WhatsApp** {#cu-072---consultar-turnos-próximos-por-whatsapp}

| UC–072 | Consultar turnos próximos por WhatsApp |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–10 Integración con WhatsApp, OBJ–06 Planificación y Gestión de Turnos |  |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos, IRQ–12 Información de Sesiones WhatsApp |  |
| **Descripción** | El cliente consulta por WhatsApp sus turnos pendientes y programados. |  |
| **Precondición** | El cliente debe estar registrado y autenticado en la sesión de WhatsApp. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El cliente solicita consultar sus turnos desde el menú de WhatsApp. |
|  | 2 | El sistema recupera los turnos en estado "Pendiente" asociados al cliente. |
|  | 3 | El sistema envía un mensaje con el detalle de cada turno programado. |
|  | 4 | El sistema presenta opciones para solicitar un nuevo turno o cancelar alguno existente. |
| **Postcondición** | El cliente visualiza el estado de sus reservas programadas. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no tiene turnos pendientes, el sistema informa que no posee reservas activas. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Provee autoservicio de consulta a los clientes. |  |

#### **CU-073 \- Cancelar turno por WhatsApp** {#cu-073---cancelar-turno-por-whatsapp}

| UC–073 | Cancelar turno por WhatsApp |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–10 Integración con WhatsApp, OBJ–06 Planificación y Gestión de Turnos |  |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos, IRQ–12 Información de Sesiones WhatsApp |  |
| **Descripción** | El cliente cancela uno de sus turnos programados a través del flujo de WhatsApp. |  |
| **Precondición** | El cliente debe tener al menos un turno en estado "Pendiente". |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El cliente solicita cancelar un turno desde el menú de turnos de WhatsApp. |
|  | 2 | El sistema presenta la lista de turnos pendientes del cliente. |
|  | 3 | El cliente selecciona el turno que desea cancelar. |
|  | 4 | El sistema solicita la confirmación de la cancelación. |
|  | 5 | El cliente confirma la acción. |
|  | 6 | El sistema cambia el estado del turno a "Cancelado" y ejecuta CU-076 para reorganizar la agenda. |
|  | 7 | El sistema registra la acción en auditoría y envía la confirmación de cancelación por WhatsApp. |
| **Postcondición** | El turno queda cancelado y la disponibilidad es restituida al sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si el cliente cancela la confirmación, se mantiene el turno programado. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6-7 | 2 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Permite optimizar la agenda ante avisos anticipados de inasistencia. |  |

#### **CU-074 \- Asignar turno automáticamente sin superposición** {#cu-074---asignar-turno-automáticamente-sin-superposición}

| UC–074 | Asignar turno automáticamente sin superposición |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos |  |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos, IRQ–11 Información de Configuración |  |
| **Descripción** | Proceso del sistema que evalúa la agenda y la duración estimada para garantizar que la asignación de un turno no genere solapamientos ni supere la capacidad máxima. |  |
| **Precondición** | Ejecutado durante la creación o reprogramación de turnos. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema recibe la fecha, hora de inicio y duración estimada del turno solicitado. |
|  | 2 | El sistema calcula la hora de finalización estimada. |
|  | 3 | El sistema recupera los turnos ya agendados para esa fecha. |
|  | 4 | El sistema valida que el intervalo se encuentre dentro de los horarios de atención (CU-061). |
|  | 5 | El sistema comprueba que en ningún momento del intervalo se exceda la capacidad máxima concurrente (CU-062). |
|  | 6 | El sistema confirma la viabilidad del horario solicitado. |
| **Postcondición** | La disponibilidad horaria queda validada para la reserva. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el turno excede el horario de cierre o es en día no laboral, el sistema informa el error. |
|  | 5a | Si se supera la capacidad máxima simultánea, el sistema rechaza el intervalo y calcula las alternativas más cercanas. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3-6 | 500ms |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Ejecutado automáticamente por el motor de turnos del sistema. |  |

#### **CU-075 \- Validar disponibilidad al mover un turno** {#cu-075---validar-disponibilidad-al-mover-un-turno}

| UC–075 | Validar disponibilidad al mover un turno |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos |  |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos |  |
| **Descripción** | El sistema valida automáticamente la disponibilidad de agenda al modificar la asignación de un turno. |  |
| **Precondición** | Se encuentra en ejecución la modificación de un turno. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema recibe los parámetros de la nueva reserva. |
|  | 2 | El sistema excluye el turno en edición del análisis de solapamientos. |
|  | 3 | El sistema valida la capacidad y reglas de agenda disponibles. |
|  | 4 | Si las validaciones son satisfactorias, el sistema confirma la disponibilidad horaria. |
| **Postcondición** | La disponibilidad del nuevo intervalo queda validada. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si no hay cupo o existe solapamiento, el sistema retorna los horarios alternativos más cercanos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-4 | 500ms |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Ejecutado automáticamente por el sistema durante la reprogramación. |  |

#### **CU-076 \- Reorganizar agenda ante cancelaciones** {#cu-076---reorganizar-agenda-ante-cancelaciones}

| UC–076 | Reorganizar agenda ante cancelaciones |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos |  |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos |  |
| **Descripción** | El sistema optimiza la agenda ante la anulación de un turno y notifica opciones de adelanto a clientes con turnos posteriores. |  |
| **Precondición** | Se ha cancelado un turno agendado previamente. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema identifica el intervalo horario liberado. |
|  | 2 | El sistema evalúa los turnos candidatos posteriores susceptibles de adelanto. |
|  | 3 | El sistema despacha notificaciones automáticas ofreciendo la vacante liberada. |
|  | 4 | Al recibir la aceptación de un cliente, el sistema reubica el turno en el nuevo horario. |
|  | 5 | El sistema actualiza la agenda y registra la optimización en auditoría. |
| **Postcondición** | La agenda queda optimizada con los nuevos horarios. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no hay turnos candidatos para adelanto, el proceso finaliza sin cambios. |
|  | 4a | Si el cliente rechaza o no responde en el tiempo estipulado, se evalúa al siguiente candidato. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 1-3 | 3 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Ejecutado automáticamente por el sistema de optimización de turnos. |  |

#### **CU-077 \- Enviar notificación por WhatsApp** {#cu-077---enviar-notificación-por-whatsapp}

| UC–077 | Enviar notificación por WhatsApp |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–12 Notificación al Cliente, OBJ–10 Integración con WhatsApp |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–12 Información de Sesiones WhatsApp |  |
| **Descripción** | Servicio del sistema encargado del envío de mensajes estructurados a clientes a través del canal oficial de WhatsApp. |  |
| **Precondición** | El cliente debe poseer un número de teléfono válido y el servicio de mensajería debe estar activo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema recibe la solicitud de notificación con el destinatario y la información a despachar. |
|  | 2 | El sistema formatea los datos y envía la solicitud al servicio de mensajería. |
|  | 3 | El sistema recibe la confirmación de entrega por parte del servicio externo. |
|  | 4 | El sistema registra el envío de la notificación en el historial. |
| **Postcondición** | El mensaje es entregado al cliente vía WhatsApp. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si el servicio de mensajería reporta un fallo de entrega, el sistema registra el error sin interrumpir la operación del lavadero. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 2 segundos |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Componente transversal utilizado para avisos de turnos, etapas y finalización. |  |

#### **CU-078 \- Enviar notificación por correo electrónico** {#cu-078---enviar-notificación-por-correo-electrónico}

| UC–078 | Enviar notificación por correo electrónico |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–12 Notificación al Cliente, OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados, IRQ–02 Información sobre Clientes |  |
| **Descripción** | Servicio del sistema encargado del envío de correos electrónicos transaccionales y de seguridad. |  |
| **Precondición** | Debe existir una dirección de correo válida para el destinatario. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema recibe la solicitud de envío de correo con destinatario, asunto y contenido. |
|  | 2 | El sistema procesa la solicitud mediante el servicio de correo configurado. |
|  | 3 | El sistema registra el despacho del correo en los registros del sistema. |
| **Postcondición** | El correo electrónico ha sido enviado al destinatario. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si ocurre un fallo en el servidor de correo, el sistema registra el error para reintento posterior. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2 | 2 segundos |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Componente transversal utilizado para avisos de turnos, etapas y finalización. |  |

#### **CU-079 \- Notificar etapa finalizada** {#cu-079---notificar-etapa-finalizada}

| UC–079 | Notificar etapa finalizada |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–12 Notificación al Cliente |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–08 Información sobre Lavados |  |
| **Descripción** | El sistema despacha una notificación automática por WhatsApp informando al cliente la culminación de una etapa de su servicio. |  |
| **Precondición** | Una etapa de servicio ha sido completada en CU-052 y la notificación automática está habilitada. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema detecta la finalización de una etapa en el lavado. |
|  | 2 | El sistema obtiene los datos del cliente, vehículo y etapa concluida. |
|  | 3 | El sistema genera el mensaje informativo con el detalle del avance del servicio. |
|  | 4 | El sistema despacha la notificación a través de CU-077 o CU-078. |
|  | 5 | El sistema registra la notificación emitida. |
| **Postcondición** | El cliente recibe el aviso del avance de su vehículo. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el envío falla, se registra la contingencia sin detener el flujo operativo del lavado. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-4 | 2 segundos |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Mejora la transparencia y comunicación durante la atención del vehículo. |  |

#### **CU-080 \- Notificar lavado finalizado** {#cu-080---notificar-lavado-finalizado}

| UC–080 | Notificar lavado finalizado |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–12 Notificación al Cliente, OBJ–04 Registro y Gestión de Lavados |  |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–08 Información sobre Lavados |  |
| **Descripción** | El sistema envía una notificación automática por WhatsApp informando al cliente que su vehículo se encuentra listo para ser retirado. |  |
| **Precondición** | El lavado ha sido finalizado en CU-054. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema detecta la finalización completa del lavado. |
|  | 2 | El sistema obtiene los datos del cliente, vehículo, saldo pendiente y ubicación del lavadero. |
|  | 3 | El sistema genera el mensaje informando que el vehículo está listo para retirar, detallando el saldo pendiente si existe. |
|  | 4 | El sistema despacha la notificación ejecutando CU-077 o CU-078. |
|  | 5 | El sistema registra el envío de la notificación en el lavado. |
| **Postcondición** | El cliente recibe el aviso para retirar su vehículo. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si falla el canal de mensajería, se registra el evento en logs de auditoría. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-4 | 2 segundos |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Notificación crítica para agilizar el retiro de vehículos y la rotación del espacio. |  |

#### **CU-081 \- Solicitar hablar con el personal** {#cu-081---solicitar-hablar-con-el-personal}

| UC–081 | Solicitar hablar con el personal |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–10 Integración con WhatsApp |  |
| **Requisitos asociados** | IRQ–12 Información de Sesiones WhatsApp |  |
| **Descripción** | El cliente solicita derivar la conversación de WhatsApp a un operador humano del lavadero. |  |
| **Precondición** | El cliente interactúa en una sesión de WhatsApp activa. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El cliente selecciona la opción de comunicarse con el personal desde el menú de WhatsApp. |
|  | 2 | El sistema cambia el estado de la sesión para requerir atención humana. |
|  | 3 | El sistema envía un mensaje al cliente confirmando la solicitud e informando los horarios de atención. |
|  | 4 | El sistema emite una notificación interna para el personal del lavadero. |
|  | 5 | El sistema pausa las respuestas automatizadas del bot para dicho número. |
| **Postcondición** | La conversación queda en espera de atención manual por parte del personal. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | \- | \- |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-4 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Baja |  |
| **Comentarios** | Permite atender consultas especiales no contempladas en las opciones del bot. |  |

### **Módulo: Estadísticas y Reportes**

#### **CU-082 \- Consultar estadísticas básicas** {#cu-082---consultar-estadísticas-básicas}

| UC–082 | Consultar estadísticas básicas |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–08 Módulo de Estadísticas y Reportes, OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados, IRQ–04 Información sobre Servicios, IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El personal consulta indicadores operativos clave sobre la actividad general del lavadero, incorporando métricas de insumos críticos y rotación de stock. |  |
| **Precondición** | El usuario debe estar autenticado en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario solicita consultar el panel de estadísticas. |
|  | 2 | El sistema calcula y consolida las métricas operativas de lavados, facturación e inventario. |
|  | 3 | El sistema presenta los indicadores consolidados, incorporando alertas de productos en o bajo stock mínimo e insumos de mayor consumo. |
|  | 4 | El usuario puede solicitar el cambio de período temporal de análisis. |
|  | 5 | El sistema actualiza los indicadores según el rango solicitado. |
| **Postcondición** | El usuario visualiza los indicadores de desempeño operativo y de inventario. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no hay datos registrados para el período, el sistema presenta los indicadores en cero. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Provee información para la toma de decisiones y control de gestión de existencias. |  |

#### **CU-083 \- Consultar historial de pagos** {#cu-083---consultar-historial-de-pagos}

| UC–083 | Consultar historial de pagos |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–05 Registro de Pagos, OBJ–08 Módulo de Estadísticas y Reportes |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados |  |
| **Descripción** | Permite al personal consultar los cobros registrados con opciones de filtrado, ordenamiento y totales. |  |
| **Precondición** | El usuario debe estar autenticado en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario solicita consultar el historial de pagos. |
|  | 2 | El sistema recupera los registros de cobro aplicando los filtros por defecto. |
|  | 3 | El sistema presenta el listado de pagos registrados y sus totales. |
|  | 4 | El usuario puede aplicar filtros por medio de pago, montos o rango de fechas. |
|  | 5 | El sistema actualiza los registros presentados y totaliza los montos filtrados. |
| **Postcondición** | El usuario visualiza el detalle de los pagos recibidos. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no existen pagos registrados para los criterios seleccionados, el sistema informa la situación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Base para el arqueo de caja y control de ingresos. |  |

#### **CU-084 \- Generar reportes** {#cu-084---generar-reportes}

| UC–084 | Generar reportes |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–08 Módulo de Estadísticas y Reportes, OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados, IRQ–02 Información sobre Clientes, IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El Administrador genera informes analíticos consolidados del negocio, incorporando balances de consumo de insumos por orden de trabajo, desvíos registrados y balance de inventario. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El Administrador accede a la sección de reportes. |
|  | 2 | El sistema presenta los tipos de informes disponibles, incluyendo los balances de costo y consumo de insumos por servicio. |
|  | 3 | El Administrador selecciona el tipo de informe y define los parámetros y filtros temporales. |
|  | 4 | El Administrador solicita generar el informe. |
|  | 5 | El sistema procesa los datos y presenta la vista previa del reporte generado. |
|  | 6 | El Administrador puede exportar el reporte generado mediante CU-084.1. |
| **Postcondición** | El reporte queda generado para su análisis. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si no existen datos para los filtros indicados, el sistema muestra un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5 | 5 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Permite evaluar el costo real de químicos para determinar el margen de contribución del lavadero. |  |

#### **CU-084.1 \- Exportar reportes a PDF o Excel** {#cu-084.1---exportar-reportes-a-pdf-o-excel}

| UC–084.1 | Exportar reportes a PDF o Excel |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–08 Módulo de Estadísticas y Reportes |  |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados |  |
| **Descripción** | Extiende de CU-084. El sistema permite descargar el reporte generado en formatos estándar de archivo. |  |
| **Precondición** | Debe existir un reporte generado previamente. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador visualiza el reporte generado. |
|  | 2 | El administrador selecciona el formato de archivo deseado. |
|  | 3 | El administrador confirma la exportación. |
|  | 4 | El sistema compila y genera el archivo correspondiente. |
|  | 5 | El sistema inicia la descarga del documento en el dispositivo. |
| **Postcondición** | El archivo del reporte queda descargado. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si ocurre un error en la compilación del archivo, el sistema informa el problema al usuario. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 4-5 | 5 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Ninguno. |  |

### **Módulo: Auditoría**

#### **CU-085 \- Consultar historial de auditoría** {#cu-085---consultar-historial-de-auditoría}

| UC–085 | Consultar historial de auditoría |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–07 Registro de Auditoría |  |
| **Requisitos asociados** | IRQ–10 Información de Auditoría |  |
| **Descripción** | El administrador visualiza la bitácora inmutable de eventos y acciones realizadas en el sistema. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador accede a la sección de auditoría. |
|  | 2 | El sistema recupera los registros de auditoría aplicando los filtros por defecto. |
|  | 3 | El sistema resuelve dinámicamente las referencias a usuarios y entidades. |
|  | 4 | El sistema presenta el listado de registros de auditoría. |
|  | 5 | El administrador puede aplicar filtros y ordenamiento según los campos disponibles. |
|  | 6 | El administrador navega entre páginas mediante los controles de paginación. |
| **Postcondición** | El administrador visualiza los registros de auditoría solicitados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no se encuentran eventos registrados, el sistema presenta un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-4 | 2 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | El historial es de solo lectura para preservar la trazabilidad. |  |

#### **CU-086 \- Filtrar registros de auditoría** {#cu-086---filtrar-registros-de-auditoría}

| UC–086 | Filtrar registros de auditoría |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–07 Registro de Auditoría |  |
| **Requisitos asociados** | IRQ–10 Información de Auditoría |  |
| **Descripción** | El administrador acota la búsqueda en la bitácora mediante filtros específicos de fecha, acción o entidad. |  |
| **Precondición** | Se encuentra en ejecución CU-085 Consultar historial de auditoría. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-085 Consultar historial de auditoría. |
|  | 2 | El sistema presenta los controles de filtrado disponibles. |
|  | 3 | El administrador selecciona los criterios de filtrado deseados. |
|  | 4 | El administrador aplica los filtros. |
|  | 5 | El sistema actualiza la lista de registros de auditoría con los resultados coincidentes. |
| **Postcondición** | Los registros de auditoría quedan filtrados según los parámetros. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si ningún registro coincide con los filtros, el sistema informa la situación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-085 Consultar historial de auditoría. |  |

#### **CU-087 \- Ver detalle de registro de auditoría** {#cu-087---ver-detalle-de-registro-de-auditoría}

| UC–087 | Ver detalle de registro de auditoría |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–07 Registro de Auditoría |  |
| **Requisitos asociados** | IRQ–10 Información de Auditoría |  |
| **Descripción** | El administrador examina el detalle granular de un evento de auditoría puntual. |  |
| **Precondición** | Se encuentra en ejecución CU-085 Consultar historial de auditoría. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-085 Consultar historial de auditoría. |
|  | 2 | El administrador selecciona un registro de la lista. |
|  | 3 | El sistema presenta el detalle completo del registro de auditoría. |
|  | 4 | El sistema ofrece enlaces contextuales para navegar a la entidad afectada si continúa existiendo. |
| **Postcondición** | El administrador visualiza los detalles del evento. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si la entidad vinculada fue eliminada, el sistema indica que la navegación no está disponible. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3 | 500ms |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-085 Consultar historial de auditoría. |  |

#### **CU-088 \- Registrar todas las acciones para auditoría** {#cu-088---registrar-todas-las-acciones-para-auditoría}

| UC–088 | Registrar todas las acciones para auditoría |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–07 Registro de Auditoría, OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–10 Información de Auditoría, IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El sistema persiste de forma automática e inmutable las acciones realizadas sobre las entidades, incluyendo ajustes manuales de stock, bajas de insumos y justificaciones de desvíos en etapas. |  |
| **Precondición** | Un usuario o proceso ejecuta una acción transaccional en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se produce un evento relevante en el sistema. |
|  | 2 | El sistema captura el contexto de la sesión, usuario y marca temporal. |
|  | 3 | El sistema determina el tipo de acción y la entidad involucrada (incluyendo insumos, kardex y órdenes de compra). |
|  | 4 | El sistema genera el registro de auditoría con la información del evento, usuario y entidad afectada. |
|  | 5 | El sistema almacena el registro en el almacenamiento inmutable de auditoría. |
| **Postcondición** | El evento queda asentado en la bitácora del sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si ocurre un fallo en el guardado, se asienta en los logs técnicos sin interrumpir la operación del usuario. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5 | \< 500ms (asíncrono) |
| **Frecuencia** | Muy frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Se ejecuta de forma automática y asíncrona para no penalizar el rendimiento del sistema. |  |

#### **CU-089 \- Crear rol** {#cu-089---crear-rol}

| UC–089 | Crear rol |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El administrador da de alta un nuevo rol dentro del sistema estableciendo su denominación y alcance de permisos. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador accede a la sección de gestión de roles. |
|  | 2 | El administrador selecciona la opción para registrar un nuevo rol. |
|  | 3 | El sistema presenta el formulario de alta con el catálogo de permisos disponibles. |
|  | 4 | El administrador ingresa el nombre, descripción y selecciona los permisos correspondientes. |
|  | 5 | El administrador confirma la creación del rol. |
|  | 6 | El sistema valida la unicidad del nombre y la consistencia de los datos ingresados. |
|  | 7 | El sistema registra el nuevo rol en la base de datos. |
|  | 8 | El sistema presenta un mensaje de confirmación de creación. |
|  | 9 | El sistema registra la acción en auditoría. |
| **Postcondición** | El nuevo rol queda registrado y disponible para su asignación a empleados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 6a | Si el nombre del rol ya existe o faltan campos obligatorios, el sistema informa el error y solicita corrección. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Permite definir esquemas de autorización personalizados en el sistema. |  |

#### **CU-090 \- Modificar rol** {#cu-090---modificar-rol}

| UC–090 | Modificar rol |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El administrador actualiza los datos y la configuración de permisos de un rol existente. |  |
| **Precondición** | El usuario debe tener rol de Administrador. El rol debe existir en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-096 Consultar roles. |
|  | 2 | El administrador selecciona el rol a modificar. |
|  | 3 | El sistema presenta el formulario de edición con los datos y permisos actuales. |
|  | 4 | El administrador modifica el nombre, descripción o la matriz de permisos asociados. |
|  | 5 | El administrador confirma los cambios. |
|  | 6 | El sistema valida la integridad de los datos y las restricciones de roles reservados. |
|  | 7 | El sistema actualiza el registro del rol en la base de datos. |
|  | 8 | El sistema presenta un mensaje de confirmación. |
|  | 9 | El sistema registra la acción en auditoría. |
| **Postcondición** | Los datos y permisos del rol quedan actualizados en el sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 6a | Si se intenta modificar un rol predefinido protegido o hay errores de validación, el sistema bloquea la acción e informa el motivo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Incluye el caso de uso CU-091 Consultar roles. |  |

#### **CU-091 \- Consultar roles** {#cu-091---consultar-roles}

| UC–091 | Consultar roles |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El administrador visualiza la lista de roles definidos en el sistema con información de permisos y empleados asignados. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador accede a la sección de gestión de roles. |
|  | 2 | El sistema recupera el listado de roles aplicando los filtros por defecto. |
|  | 3 | El sistema presenta la grilla de roles con nombre, descripción y cantidad de usuarios vinculados. |
|  | 4 | El administrador puede aplicar filtros de búsqueda o criterios de ordenamiento. |
|  | 5 | El administrador navega entre páginas mediante los controles de paginación. |
|  | 6 | El sistema actualiza la visualización según los criterios seleccionados. |
| **Postcondición** | El administrador visualiza los roles según los filtros aplicados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no se encuentran roles registrados, el sistema presenta un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Permite visualizar roles base del sistema y roles personalizados creados por el administrador. |  |

#### **CU-092 \- Eliminar rol** {#cu-092---eliminar-rol}

| UC–092 | Eliminar rol |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El administrador elimina un rol existente garantizando que no existan dependencias con usuarios activos ni sea un rol base del sistema. |  |
| **Precondición** | El usuario debe tener rol de Administrador. El rol debe existir en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-091 Consultar roles. |
|  | 2 | El administrador selecciona el rol a eliminar. |
|  | 3 | El sistema presenta una solicitud de confirmación de la acción. |
|  | 4 | El administrador confirma la eliminación. |
|  | 5 | El sistema valida que el rol no sea un rol protegido del sistema ni posea empleados vinculados. |
|  | 6 | El sistema elimina el registro del rol de la base de datos. |
|  | 7 | El sistema presenta un mensaje de confirmación. |
|  | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | El rol es eliminado del sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el administrador cancela, se interrumpe la operación. |
|  | 5a | Si el rol posee empleados asignados o es un rol protegido (ej. Administrador), el sistema bloquea la eliminación y notifica el motivo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Incluye el caso de uso CU-091 Consultar roles. |  |

### **Módulo: Gestión de Insumos y Stock**

#### **CU-093 \- Crear insumo**

| UC–093 | Crear insumo |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El administrador da de alta un nuevo insumo en el sistema definiendo sus características operativas, parámetros de reposición y, de forma opcional (o requerida únicamente si se activa el reabastecimiento automático), los proveedores asociados. |  |
| **Precondición** | El usuario debe tener rol de Administrador. Los proveedores a asociar (si se especifican) deben existir en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador solicita crear un nuevo insumo. |
|  | 2 | El sistema presenta el formulario solicitando los datos del insumo. |
|  | 3 | El administrador ingresa los datos requeridos. |
|  | 4 | El administrador solicita guardar el nuevo insumo. |
|  | 5 | El sistema valida que los datos ingresados sean consistentes, que la denominación no esté duplicada y que, si se habilita el reabastecimiento automático, se haya asociado al menos un proveedor activo. |
|  | 6 | El sistema registra el nuevo insumo con estado "Activo" y stock inicial en cero. |
|  | 7 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | El insumo queda registrado en el inventario y disponible para su uso en el sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si los datos no cumplen las reglas de validación, la denominación está duplicada o se intenta activar el reabastecimiento automático sin especificar proveedores, el sistema informa el error correspondiente. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Permite categorizar el insumo para definir su mecanismo de descuento y control. |  |

#### **CU-094 \- Modificar insumo**

| UC–094 | Modificar insumo |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El administrador actualiza los datos y parámetros de control de un insumo existente. |  |
| **Precondición** | El usuario debe tener rol de Administrador. El insumo debe existir. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-097 Consultar inventario y alertas de stock. |
|  | 2 | El administrador selecciona el insumo a modificar. |
|  | 3 | El sistema presenta el formulario de edición con los datos actuales del insumo. |
|  | 4 | El administrador modifica los campos editables requeridos. |
|  | 5 | El administrador confirma los cambios. |
|  | 6 | El sistema valida los datos modificados. |
|  | 7 | El sistema actualiza el registro del insumo en la base de datos. |
|  | 8 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | Los datos del insumo quedan actualizados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 6a | Si hay errores de validación, el sistema informa los campos a corregir. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-097 Consultar inventario y alertas de stock. |  |

#### **CU-095 \- Desactivar insumo**

| UC–095 | Desactivar insumo |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El administrador deshabilita un insumo para que no pueda ser utilizado en nuevos servicios ni compras. |  |
| **Precondición** | El usuario debe ser Administrador. El insumo debe existir y estar activo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-097 Consultar inventario y alertas de stock. |
|  | 2 | El administrador selecciona el insumo a desactivar. |
|  | 3 | El sistema comprueba que el insumo no esté asociado a operaciones activas pendientes. |
|  | 4 | El sistema solicita confirmación de la desactivación. |
|  | 5 | El administrador confirma la operación. |
|  | 6 | El sistema cambia el estado del insumo a "Inactivo". |
|  | 7 | El sistema registra la acción en auditoría y confirma la desactivación. |
| **Postcondición** | El insumo pasa a estado inactivo. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si el insumo posee operaciones activas pendientes, el sistema impide la desactivación. |
|  | 5a | Si el administrador cancela la confirmación, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-097 Consultar inventario y alertas de stock. |  |

#### **CU-096 \- Reactivar insumo**

| UC–096 | Reactivar insumo |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El administrador rehabilita nuevamente un insumo previamente desactivado. |  |
| **Precondición** | El usuario debe ser Administrador. El insumo debe existir y estar inactivo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-097 Consultar inventario y alertas de stock. |
|  | 2 | El administrador selecciona el insumo a reactivar. |
|  | 3 | El sistema solicita confirmación de la reactivación. |
|  | 4 | El administrador confirma la operación. |
|  | 5 | El sistema cambia el estado del insumo a "Activo". |
|  | 6 | El sistema registra la acción en auditoría y confirma la reactivación. |
| **Postcondición** | El insumo vuelve a estar disponible para su uso en el sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el administrador cancela la confirmación, se aborta la reactivación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-097 Consultar inventario y alertas de stock. |  |

#### **CU-097 \- Consultar inventario y alertas de stock**

| UC–097 | Consultar inventario y alertas de stock |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El personal visualiza las existencias de insumos con opciones de filtrado, ordenamiento, paginación y alertas de stock mínimo. |  |
| **Precondición** | El usuario debe estar autenticado en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario accede a la sección de inventario de insumos. |
|  | 2 | El sistema recupera los registros de insumos aplicando los filtros por defecto. |
|  | 3 | El sistema presenta el listado de insumos registrados y señala aquellos con nivel de stock crítico. |
|  | 4 | El usuario puede aplicar filtros y ordenamiento según los campos y criterios disponibles. |
|  | 5 | El usuario navega entre los resultados utilizando los controles de paginación. |
|  | 6 | El sistema actualiza la visualización según los criterios seleccionados. |
| **Postcondición** | El usuario visualiza el inventario según los criterios aplicados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no existen registros que coincidan, el sistema presenta un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Permite alternar la visualización entre insumos activos e inactivos. |  |

#### **CU-098 \- Crear proveedor**

| UC–098 | Crear proveedor |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El administrador registra un nuevo proveedor en el sistema para la gestión de adquisiciones. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador solicita crear un nuevo proveedor. |
|  | 2 | El sistema presenta el formulario solicitando los datos del proveedor. |
|  | 3 | El administrador ingresa los datos requeridos. |
|  | 4 | El administrador solicita guardar el nuevo proveedor. |
|  | 5 | El sistema valida que los datos ingresados sean consistentes y que la identificación fiscal y razón social no estén duplicadas. |
|  | 6 | El sistema registra el nuevo proveedor con estado "Activo". |
|  | 7 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | El proveedor queda registrado en el sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si los datos no cumplen las reglas de validación o el proveedor ya existe, el sistema informa el error correspondiente. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Ninguno. |  |

#### **CU-099 \- Modificar proveedor**

| UC–099 | Modificar proveedor |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El administrador actualiza los datos comerciales y de contacto de un proveedor existente. |  |
| **Precondición** | El usuario debe tener rol de Administrador. El proveedor debe existir. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-102 Consultar proveedores. |
|  | 2 | El administrador selecciona el proveedor a modificar. |
|  | 3 | El sistema presenta el formulario de edición con los datos actuales del proveedor. |
|  | 4 | El administrador modifica los campos editables requeridos. |
|  | 5 | El administrador confirma los cambios. |
|  | 6 | El sistema valida los datos modificados. |
|  | 7 | El sistema actualiza el registro del proveedor en la base de datos. |
|  | 8 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | Los datos del proveedor quedan actualizados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 6a | Si hay errores de validación, el sistema informa los campos a corregir. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-102 Consultar proveedores. |  |

#### **CU-100 \- Desactivar proveedor**

| UC–100 | Desactivar proveedor |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El administrador deshabilita un proveedor para que no reciba nuevas órdenes de compra. |  |
| **Precondición** | El usuario debe ser Administrador. El proveedor debe existir y estar activo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-102 Consultar proveedores. |
|  | 2 | El administrador selecciona el proveedor a desactivar. |
|  | 3 | El sistema comprueba que el proveedor no posea órdenes de compra pendientes. |
|  | 4 | El sistema solicita confirmación de la desactivación. |
|  | 5 | El administrador confirma la operación. |
|  | 6 | El sistema cambia el estado del proveedor a "Inactivo". |
|  | 7 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | El proveedor pasa a estado inactivo. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si el proveedor posee órdenes de compra pendientes, el sistema impide la desactivación. |
|  | 5a | Si el administrador cancela la confirmación, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-102 Consultar proveedores. |  |

#### **CU-101 \- Reactivar proveedor**

| UC–101 | Reactivar proveedor |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El administrador rehabilita a un proveedor previamente desactivado. |  |
| **Precondición** | El usuario debe ser Administrador. El proveedor debe existir y estar inactivo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-102 Consultar proveedores. |
|  | 2 | El administrador selecciona el proveedor inactivo a reactivar. |
|  | 3 | El sistema solicita confirmación de la reactivación. |
|  | 4 | El administrador confirma la operación. |
|  | 5 | El sistema cambia el estado del proveedor a "Activo". |
|  | 6 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | El proveedor vuelve a estar disponible para la emisión de pedidos. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 4a | Si el administrador cancela la confirmación, se aborta la reactivación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Incluye el caso de uso CU-102 Consultar proveedores. |  |

#### **CU-102 \- Consultar proveedores**

| UC–102 | Consultar proveedores |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El administrador visualiza la lista de proveedores registrados con opciones de filtrado, ordenamiento y paginación. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador accede a la sección de proveedores. |
|  | 2 | El sistema recupera los registros de proveedores aplicando los filtros por defecto. |
|  | 3 | El sistema presenta el listado de proveedores registrados. |
|  | 4 | El administrador puede aplicar filtros y ordenamiento según los campos y criterios disponibles. |
|  | 5 | El administrador navega entre los resultados utilizando los controles de paginación. |
|  | 6 | El sistema actualiza la visualización según los criterios seleccionados. |
| **Postcondición** | El administrador visualiza los proveedores según los criterios aplicados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no existen registros que coincidan, el sistema presenta un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Permite alternar la visualización entre proveedores activos e inactivos. |  |

#### **CU-103 \- Registrar ingreso manual de insumos**

| UC–103 | Registrar ingreso manual de insumos |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El personal registra un incremento extraordinario de existencias justificando el motivo del ajuste. |  |
| **Precondición** | El usuario debe estar autenticado en el sistema. El insumo debe existir y estar activo. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario solicita registrar un ingreso manual de insumo. |
|  | 2 | El sistema presenta el formulario solicitando los datos del movimiento. |
|  | 3 | El usuario ingresa los datos requeridos y el motivo del ajuste. |
|  | 4 | El usuario confirma la operación. |
|  | 5 | El sistema valida que los datos sean consistentes y la cantidad sea válida. |
|  | 6 | El sistema incrementa el stock actual del insumo en la base de datos. |
|  | 7 | El sistema registra el movimiento en el historial inmutable de stock. |
|  | 8 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | El stock del insumo queda actualizado y el movimiento registrado en el historial. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si los datos no cumplen las reglas de validación o falta la justificación, el sistema informa el error correspondiente. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6-7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Utilizado para ajustes por inventario físico o adquisiciones menores fuera de circuito. |  |

#### **CU-104 \- Registrar egreso manual / baja de insumo**

| UC–104 | Registrar egreso manual / baja de insumo |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El personal registra una descarga directa de existencias por desgaste, avería o descarte justificando la causa. |  |
| **Precondición** | El usuario debe estar autenticado en el sistema. El insumo debe existir y contar con stock disponible. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario solicita registrar una baja o egreso manual de insumo. |
|  | 2 | El sistema presenta el formulario solicitando los datos del egreso y la causa correspondiente. |
|  | 3 | El usuario ingresa los datos requeridos y la justificación. |
|  | 4 | El usuario confirma la operación. |
|  | 5 | El sistema valida que la cantidad a egresar no supere las existencias disponibles y que se haya ingresado la justificación. |
|  | 6 | El sistema descuenta la cantidad indicada del stock del insumo. |
|  | 7 | El sistema registra el movimiento en el historial inmutable de stock. |
|  | 8 | Si el stock resultante alcanza el nivel crítico, el sistema evalúa la ejecución automática de CU-106. |
|  | 9 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | El stock del insumo queda reducido y el movimiento registrado con su justificación. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si la cantidad excede el stock disponible o falta la justificación, el sistema informa el error correspondiente. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6-7 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Aplica a consumibles que cumplen su vida útil, avería de herramientas o descarte de productos. |  |

#### **CU-105 \- Consultar movimientos de stock**

| UC–105 | Consultar movimientos de stock |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El administrador visualiza el registro cronológico inmutable de movimientos de stock con opciones de filtrado, ordenamiento y paginación. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador accede a la sección de movimientos de stock. |
|  | 2 | El sistema recupera los registros de movimientos aplicando los filtros por defecto. |
|  | 3 | El sistema presenta el listado cronológico de movimientos detallando tipo de operación, cantidades, saldos y usuarios intervinientes. |
|  | 4 | El administrador puede aplicar filtros por período, tipo de movimiento o insumo. |
|  | 5 | El administrador navega entre los resultados utilizando los controles de paginación. |
|  | 6 | El sistema actualiza la visualización según los criterios seleccionados. |
| **Postcondición** | El administrador visualiza la trazabilidad histórica de los movimientos de inventario. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si no existen movimientos registrados para los criterios seleccionados, el sistema presenta un mensaje informativo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Base para auditorías de inventario y control de consumos operativos. |  |

#### **CU-106 \- Disparar orden de compra automática**

| UC–106 | Disparar orden de compra automática |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores, IRQ–11 Información de Configuración |  |
| **Descripción** | Proceso del sistema que detecta insumos en nivel crítico con proveedores asignados y emite solicitudes formales de cotización para relevar precios y disponibilidad antes de comprar. |  |
| **Precondición** | El stock de uno o más insumos con reposición automática habilitada alcanza o perfora el umbral mínimo configurado. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El sistema detecta que el stock de un insumo ha alcanzado o perforado su nivel mínimo de reposición. |
|  | 2 | El sistema verifica que no existan solicitudes de cotización ni órdenes de compra abiertas para ese insumo. |
|  | 3 | El sistema recupera los proveedores activos vinculados al insumo y la cantidad de reabastecimiento parametrizada. |
|  | 4 | El sistema genera un identificador de grupo de cotización unificado. |
|  | 5 | Por cada proveedor asociado, el sistema crea un registro de Solicitud de Cotización en estado Pendiente y despacha un correo electrónico formal consultando precio unitario, disponibilidad y plazo de entrega. |
|  | 6 | El sistema asienta el evento en auditoría e informa en el panel de control la apertura de la licitación de precios. |
| **Postcondición** | Las solicitudes de cotización quedan emitidas hacia los proveedores para el concurso de precios. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si ya existe una gestión de abastecimiento en curso para ese insumo, el sistema omite emitir nuevas consultas para evitar duplicidad |
|  | 3a | Si el insumo no posee proveedores asignados, el sistema emite una alerta interna para la intervención manual del administrador. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 3-4 | 2 segundos (asíncrono) |
| **Frecuencia** | Automática |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Formaliza la etapa de averiguación de precios previa a la asunción del compromiso en firme de compra. |  |

#### **CU-107 \- Generar orden de compra manual**

| UC–107 | Generar orden de compra manual |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El administrador confecciona y emite una orden de compra a un proveedor seleccionado. |  |
| **Precondición** | El usuario debe tener rol de Administrador. Deben existir proveedores e insumos activos. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador solicita crear una nueva orden de compra. |
|  | 2 | El sistema presenta el formulario solicitando la selección del proveedor y los detalles de la orden. |
|  | 3 | El administrador selecciona el proveedor e ingresa los insumos, cantidades y observaciones requeridas. |
|  | 4 | El administrador solicita emitir la orden de compra. |
|  | 5 | El sistema valida que los datos ingresados sean consistentes y las cantidades mayores a cero. |
|  | 6 | El sistema registra la orden de compra en estado "Enviada". |
|  | 7 | El sistema despacha la notificación formal del pedido al proveedor. |
|  | 8 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | La orden de compra queda registrada y despachada al proveedor. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si la orden no contiene insumos o las cantidades son inválidas, el sistema informa el error correspondiente. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6-7 | 2 segundos |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Permite compras extraordinarias o pedidos directos fuera del flujo automatizado. |  |

#### **CU-108 \- Consultar y gestionar órdenes de compra**

| UC–108 | Consultar y gestionar órdenes de compra |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El administrador visualiza las órdenes de compra registradas con opciones de filtrado, consulta de detalle y actualización de estado. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador accede a la sección de órdenes de compra. |
|  | 2 | El sistema recupera los registros aplicando los filtros por defecto. |
|  | 3 | El sistema presenta el listado de órdenes de compra con sus estados vigentes. |
|  | 4 | El administrador puede aplicar filtros y ordenamiento según los criterios disponibles. |
|  | 5 | El administrador selecciona una orden para visualizar su detalle. |
|  | 6 | El sistema presenta el detalle completo y las acciones contextuales disponibles. |
|  | 7 | El administrador puede confirmar el envío de órdenes pendientes o cancelar solicitudes descartadas ingresando la justificación correspondiente. |
|  | 8 | El sistema actualiza el estado del registro y asienta la acción en auditoría. |
| **Postcondición** | El administrador visualiza o actualiza el estado de las órdenes de compra. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 7a | Si la orden ya se encuentra en estado finalizado o recibida, el sistema impide su cancelación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 / 8 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Permite gestionar cotizaciones concurrentes y pedidos pendientes. |  |

#### **CU-109 \- Registrar recepción de orden de compra**

| UC–109 | Registrar recepción de orden de compra |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El personal registra la entrega de mercadería de una orden de compra, actualizando el stock y gestionando las órdenes concurrentes vinculadas. |  |
| **Precondición** | El usuario debe estar autenticado en el sistema. La orden de compra debe existir en estado enviada o confirmada. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El usuario solicita registrar la recepción de una orden de compra. |
|  | 2 | El sistema presenta las órdenes de compra pendientes de entrega. |
|  | 3 | El usuario selecciona la orden correspondiente al pedido recibido. |
|  | 4 | El sistema presenta los detalles de los insumos y cantidades solicitadas. |
|  | 5 | El usuario verifica las cantidades recibidas y confirma la recepción. |
|  | 6 | El sistema valida las cantidades ingresadas. |
|  | 7 | El sistema incrementa el stock de los insumos comprendidos. |
|  | 8 | El sistema registra las entradas en el historial inmutable de movimientos de stock. |
|  | 9 | El sistema actualiza el estado de la orden a "Recibida". |
|  | 10 | Opcionalmente, el sistema permite cancelar de forma asistida las órdenes concurrentes que no fueron seleccionadas. |
|  | 11 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | El stock queda incrementado con la mercadería recibida y la orden completada. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a | Si las cantidades ingresadas son inválidas, el sistema informa el error correspondiente. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7-9 | 2 segundos |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Admite entregas parciales manteniendo la orden abierta hasta su compleción. |  |

#### **CU-110 \- Configurar directivas de reabastecimiento automático**

| UC–110 | Configurar directivas de reabastecimiento automático |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock, OBJ–11 Gestión de Configuración del Sistema |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores, IRQ–11 Información de Configuración |  |
| **Descripción** | El administrador parametriza las reglas operativas de emisión de pedidos automáticos y los textos de comunicación con proveedores. |  |
| **Precondición** | El usuario debe tener rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El administrador solicita configurar las directivas de reabastecimiento. |
|  | 2 | El sistema presenta los parámetros vigentes de automatización y plantillas de notificación. |
|  | 3 | El administrador actualiza los parámetros y textos requeridos. |
|  | 4 | El administrador solicita guardar los cambios. |
|  | 5 | El sistema valida la consistencia de los datos ingresados. |
|  | 6 | El sistema actualiza la configuración en la base de datos. |
|  | 7 | El sistema presenta un mensaje de confirmación y registra la acción en auditoría. |
| **Postcondición** | Los parámetros de reabastecimiento automático quedan actualizados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si existen valores inconsistentes o campos obligatorios vacíos, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Media |  |
| **Comentarios** | Rige el comportamiento del proceso automático de generación de órdenes. |  |

#### **CU-114 \- Generar solicitud de cotización** {#cu-114---generar-solicitud-de-cotización}

| UC–114 | Generar solicitud de cotización |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El Administrador genera manualmente una consulta formal de precios y disponibilidad hacia uno o varios proveedores para determinados insumos. |  |
| **Precondición** | El usuario debe ser Administrador. Deben existir insumos y proveedores activos. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El Administrador solicita crear una nueva solicitud de cotización. |
|  | 2 | El sistema presenta los proveedores disponibles y el catálogo de insumos. |
|  | 3 | El Administrador selecciona los insumos requeridos, define las cantidades estimadas e indica los proveedores a los cuales consultar. |
|  | 4 | El Administrador confirma la emisión de la solicitud. |
|  | 5 | El sistema genera las instancias de Solicitud de Cotización asociadas bajo un mismo grupo de consulta. |
|  | 6 | El sistema despacha las comunicaciones por correo electrónico a los proveedores con el requerimiento de cotización formal. |
|  | 7 | El sistema emite la confirmación y registra la acción en auditoría. |
| **Postcondición** | Las solicitudes quedan emitidas a la espera de las ofertas de los proveedores. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si no se seleccionan insumos o proveedores válidos, el sistema solicita completar los campos indispensables. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 5-7 | 2 segundos |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | No genera compromiso de compra ni afecta existencias de inventario. |  |

#### **CU-115 \- Registrar respuesta de cotización de proveedor** {#cu-115---registrar-respuesta-de-cotización-de-proveedor}

| UC–115 | Registrar respuesta de cotización de proveedor |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El Administrador asienta los valores de precios unitarios cotizados, disponibilidad de stock y plazos de entrega comunicados por el proveedor. |  |
| **Precondición** | Debe existir una solicitud de cotización en estado Pendiente o Enviada. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El Administrador accede a la gestión de solicitudes de cotización. |
|  | 2 | El Administrador selecciona la solicitud correspondiente al proveedor que respondió. |
|  | 3 | El sistema presenta los insumos consultados en dicha solicitud. |
|  | 4 | El Administrador ingresa el precio unitario cotizado para cada ítem, la confirmación de stock disponible y la fecha o plazo estimado de entrega. |
|  | 5 | El Administrador solicita guardar la respuesta. |
|  | 6 | El sistema valida que los precios y plazos sean valores positivos. |
|  | 7 | El sistema actualiza la solicitud a estado Respondida con los datos económicos cargados. |
|  | 8 | El sistema confirma el registro y asienta el evento en auditoría. |
| **Postcondición** | La cotización queda disponible con sus precios formalizados para la etapa de adjudicación. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 6a | Si se omiten precios o se ingresan valores negativos, el sistema solicita subsanar los datos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6-8 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Permite centralizar la lista de precios provista por cada firma para comparar ofertas objetivamente. |  |

#### **CU-116 \- Adjudicar cotización y generar orden de compra** {#cu-116---adjudicar-cotización-y-generar-orden-de-compra}

| UC–116 | Adjudicar cotización y generar orden de compra |  |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–13 Gestión de Insumos y Stock |  |
| **Requisitos asociados** | IRQ–13 Información sobre Insumos y Proveedores |  |
| **Descripción** | El Administrador evalúa las ofertas de una compulsa, selecciona la cotización más conveniente, genera automáticamente la orden de compra formal en firme y descarta las alternativas restantes. |  |
| **Precondición** | Debe existir al menos una cotización en estado Respondida. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El Administrador accede al comparador de cotizaciones de un grupo de solicitud. |
|  | 2 | El sistema presenta la grilla comparativa con las cotizaciones recibidas, contrastando precios totales, precios unitarios y plazos de entrega de cada proveedor. |
|  | 3 | El Administrador selecciona la oferta adjudicataria y solicita generar la orden de compra. |
|  | 4 | El sistema solicita confirmación informando que las cotizaciones concurrentes del mismo grupo quedarán descartadas. |
|  | 5 | El Administrador confirma la adjudicación. |
|  | 6 | El sistema genera la Orden de Compra definitiva en firme (CU-108) con los ítems y precios pactados en la cotización. |
|  | 7 | El sistema actualiza la cotización seleccionada a estado Adjudicada y cancela las demás cotizaciones del grupo bajo motivo Adjudicación a otra oferta. |
|  | 8 | El sistema despacha el correo formal con la orden de compra aprobada al proveedor ganador. |
|  | 9 | El sistema confirma la adjudicación y registra la acción en auditoría. |
| **Postcondición** | Se perfecciona la compra en firme mediante una orden de compra activa y se cierran las solicitudes concurrentes. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 5a | Si el Administrador cancela la confirmación, se mantiene la compulsa abierta sin generar órdenes. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6-9 | 2 segundos |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Culmina el circuito de compras transparentando la elección de proveedores por precio y conveniencia operativa. |  |

## **Requisitos No Funcionales**

| Código | Requisito | Descripción |
| :---- | :---- | :---- |
| **NFR–01** | **Seguridad de Autenticación** | Uso de Firebase Auth, encriptación y tokens JWT. |
| **NFR–02** | **Protección de Datos** | HTTPS obligatorio, Hash SHA256 para claves sensibles. |
| **NFR–03** | **Usabilidad** | Diseño responsive y flujos de bot intuitivos. |
| **NFR–04** | **Auditoría** | Registro obligatorio de todas las transacciones. |
| **NFR–05** | **Rendimiento** | Respuestas \< 2s en web y \< 5s en WhatsApp. |
| **NFR–06** | **Disponibilidad** | 99% de uptime operativo. |
| **NFR–07** | **Escalabilidad** | Arquitectura cloud (Firestore) escalable. |
| **NFR–08** | **Compatibilidad** | Soporte para Chrome, Edge, Firefox, Safari. |
| **NFR–09** | **Integraciones** | Conexión robusta con Firebase y WhatsApp API. |
| **NFR–10** | **Mantenibilidad** | Código limpio, patrón MVC/Blazor. |
| **NFR–11** | **Internacionalización** | Estructura lista para múltiples idiomas (inicialmente ES). |
| **NFR–12** | **Configurabilidad** | Parámetros operativos editables sin deploy. |

## 

## **Matriz de Rastreabilidad: Objetivos vs Requisitos de Información**

|  | OBJ-01 | OBJ-02 | OBJ-03 | OBJ-04 | OBJ-05 | OBJ-06 | OBJ-07 | OBJ-08 | OBJ-09 | OBJ-10 | OBJ-11 | OBJ-12 |
| :---- | :---- | :---- | :---- | :---- | :---- | :---- | :---- | :---- | :---- | :---- | :---- | :---- |
| **IRQ-01** | X |  |  |  |  |  |  |  | X |  |  |  |
| **IRQ-02** |  | X |  |  |  |  |  |  |  | X |  | X |
| **IRQ-03** |  | X |  |  |  |  |  |  |  | X |  |  |
| **IRQ-04** |  |  | X | X |  |  |  |  |  |  |  |  |
| **IRQ-05** |  |  | X |  |  |  |  |  |  |  |  |  |
| **IRQ-06** |  |  | X |  |  |  |  |  |  |  |  |  |
| **IRQ-07** |  |  | X |  |  |  |  |  |  |  |  |  |
| **IRQ-08** |  |  |  | X | X |  |  | X |  |  |  |  |
| **IRQ-09** |  |  |  |  |  | X |  |  |  | X |  |  |
| **IRQ-10** |  |  |  |  |  |  | X |  |  |  |  |  |
| **IRQ-11** |  |  |  |  |  | X |  |  |  |  | X |  |
| **IRQ-12** |  |  |  |  |  |  |  |  |  | X |  |  |

## **Glosario de Términos** 

| Término | Categoría | Descripción |
| :---- | :---- | :---- |
| **Administrador** | Rol | Usuario con acceso total al sistema. |
| **Bot** | Componente | Interfaz automatizada de WhatsApp. |
| **Cliente** | Rol | Usuario final que solicita servicios. |
| **CRUD** | Técnica | Create, Read, Update, Delete (ABM). |
| **Firebase** | Plataforma | Backend-as-a-Service usado para Auth y DB. |
| **Lavado** | Entidad | Instancia de un servicio prestado a un vehículo. |
| **Paquete** | Entidad | Conjunto de servicios con precio promocional. |
| **Servicio** | Entidad | Tarea individual (ej. Aspirado). |
| **Trabajador** | Rol | Empleado que opera el sistema día a día. |
| **Turno** | Entidad | Reserva de tiempo en la agenda. |
| **WhatsApp API** | Integración | Interfaz de Meta para mensajería empresarial. |
| **Kardex** | Componente | Sistema de registro detallado que documenta el movimiento de entradas, salidas y saldos de un inventario, permitiendo el control permanente de las existencias de mercancía. |

## 

## **Apéndice: Diagrama de Casos de Uso por Actor**

### **Figura 2 \- Diagrama Caso Usos del actor "Administrador"**

![][image3] 

### **Figura 3 \- Diagrama Caso Usos del actor "Trabajador"**

![][image4]

### **Figura 4 \- Diagrama Caso Usos del actor "Cliente"**

![][image5]

### **Figura 5 \- Diagrama Caso Usos del actor "Sistema"**

![][image6]

[image1]: <data:image/png;base64,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>

[image2]: <data:image/png;base64,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>

[image3]: <data:image/png;base64,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>

[image4]: <data:image/png;base64,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>

[image5]: <data:image/png;base64,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>

[image6]: <data:image/png;base64,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>