\*\*LavaFacil\*\*  

\*\*Sistema de Gestión de Servicios y Turnos para Lavaderos de Vehículos\*\*



\*\*Documento de Requisitos del Sistema\*\*



\*\*\*Versión 2.0\*\*\*



\*\*\*Fecha: 10/01/2026\*\*\*



Realizado por:  Gelabert André



\*\*Lista de Cambios\*\*



| \*Nro\* | \*Fecha\* | \*Descripción\* | \*Autor\* |

| : ---- | :---- | : ---- | :---- |

| 0 | 08/09/2025 | Versión 1.0 - Documento inicial | Gelabert André |

| 1 | 10/01/2026 | Versión 2.0 - Actualización completa del documento | Gelabert André |



\# \*\*\*Índice\*\*\*



\*\*\[Presentación General](#presentación-general)\*\*



\*\*\[Participantes del Proyecto](#participantes-del-proyecto)\*\*



\*\*\[Objetivos del Sistema](#objetivos-del-sistema)\*\*



\*\*\[Diagrama de Caso de Uso del Sistema](#diagrama-de-caso-de-uso-del-sistema)\*\*



\*\*\[Subsistemas del Proyecto](#subsistemas-del-proyecto)\*\*



\*\*\[Objetivos de la Iteración](#objetivos-de-la-iteración)\*\*



\*\*\[Requisitos del Sistema](#requisitos-del-sistema)\*\*



\*\*\[Glosario de Términos](#glosario-de-términos)\*\*



\# \*\*\*Presentación General\*\*\* {#presentación-general}



Los lavaderos de autos pequeños y medianos enfrentan problemas operativos recurrentes que afectan su productividad y la calidad del servicio ofrecido. Entre las dificultades detectadas se encuentran: 



\* Gestión manual e ineficiente de turnos, lo que provoca solapamientos y tiempos de espera prolongados. 

\* Falta de visibilidad en la asignación de recursos humanos (quién está haciendo qué y cuándo).

\* Carencia de herramientas para estimar correctamente la duración de los lavados según el tipo de vehículo y los servicios solicitados.

\* Comunicación deficiente con los clientes y ausencia de estrategias automatizadas para reducir huecos en la agenda.

\* Falta de registro formal de pagos y recibos, así como de trazabilidad de quién realizó cada acción. 

\* Ausencia de reportes e indicadores que permitan medir ocupación, rendimiento y cumplimiento de turnos. 



Ante estos problemas, \*\*LavaFacil\*\* ofrece una solución integral que: 



\* Digitaliza y centraliza la gestión de turnos, evitando superposiciones y mejorando la organización de la agenda.

\* Permite asignar personal de forma clara y con visibilidad en tiempo real.

\* Calcula automáticamente la duración estimada de un lavado según los servicios y el tipo de vehículo.

\* Integra canales de comunicación con clientes (WhatsApp y correo) para notificaciones automáticas y confirmaciones.

\* Registra de manera estructurada pagos y acciones realizadas, garantizando trazabilidad. 

\* Genera reportes e indicadores clave para evaluar el rendimiento del lavadero y apoyar la toma de decisiones.



\# \*\*\*Participantes del Proyecto\*\*\* {#participantes-del-proyecto}



Desarrolladores: 



\* Gelabert André



\# \*\*\*Objetivos del Sistema\*\*\* {#objetivos-del-sistema}



| OBJ–01 | Gestión de Empleados |

| : ---- | :---- |

| \*\*Descripción\*\* | Permitir la creación, modificación, consulta, desactivación y reactivación de empleados.  Incluye la asignación de roles (Administrador/Empleado) y la integración con Google Authentication para inicio de sesión. |

| \*\*Estabilidad\*\* | Alta.  |

| \*\*Comentarios\*\* | Fundamental para el control de accesos y la gestión interna del personal. |



| OBJ–02 | Gestión de Clientes y Vehículos |

| :---- | : ---- |

| \*\*Descripción\*\* | Centralizar el registro y consulta de clientes y sus vehículos, incluyendo creación, modificación, desactivación y reactivación. Soporta vehículos con múltiples dueños mediante clave de asociación y vinculación/desvinculación de vehículos a clientes. |

| \*\*Estabilidad\*\* | Alta. |

| \*\*Comentarios\*\* | Es la base de datos principal para vincular los servicios y lavados del lavadero. |



| OBJ–03 | Gestión de Servicios y Paquetes |

| : ---- | :---- |

| \*\*Descripción\*\* | Administrar servicios individuales y paquetes de servicios con descuentos configurables. Incluye la definición de duración estimada según tipo de vehículo, etapas del servicio, tipos de servicio y tipos de vehículo personalizables. |

| \*\*Estabilidad\*\* | Alta. |

| \*\*Comentarios\*\* | Se vincula con la estimación automática de la duración de lavados. |



| OBJ–04 | Registro y Gestión de Lavados |

| : ---- | :---- |

| \*\*Descripción\*\* | Registrar y gestionar lavados incluyendo inicio, seguimiento de etapas por servicio, finalización parcial o total, cancelación de servicios individuales o del lavado completo, y registro de pagos. |

| \*\*Estabilidad\*\* | Alta. |

| \*\*Comentarios\*\* | Es el núcleo operativo del sistema y crítico para el negocio. |



| OBJ–05 | Registro de Pagos |

| :---- | :---- |

| \*\*Descripción\*\* | Permitir al personal registrar pagos totales y parciales, gestionar estados de pago y mantener historial de pagos por lavado. |

| \*\*Estabilidad\*\* | Alta. |

| \*\*Comentarios\*\* | Se integra con el historial de clientes y la auditoría.  |



| OBJ–06 | Planificación y Gestión de Turnos |

| :---- | :---- |

| \*\*Descripción\*\* | Asignar turnos evitando solapamientos, reorganizar automáticamente ante cancelaciones y permitir solicitudes de turno por WhatsApp.  Incluye validación de disponibilidad y notificación de adelantos.  |

| \*\*Estabilidad\*\* | Alta. |

| \*\*Comentarios\*\* | Permite organizar la agenda del lavadero de forma eficiente. |



| OBJ–07 | Registro de Auditoría |

| :---- | :---- |

| \*\*Descripción\*\* | Registrar automáticamente todas las acciones realizadas en el sistema para control y trazabilidad.  Incluye filtros por fecha, tipo de acción, tipo de entidad y usuario. |

| \*\*Estabilidad\*\* | Alta. |

| \*\*Comentarios\*\* | Garantiza transparencia y control administrativo. |



| OBJ–08 | Módulo de Estadísticas y Reportes |

| : ---- | :---- |

| \*\*Descripción\*\* | Generar reportes e indicadores clave sobre clientes, servicios, lavados y pagos, exportables en PDF o Excel. |

| \*\*Estabilidad\*\* | Media. |

| \*\*Comentarios\*\* | Permite análisis de desempeño y apoyo en la toma de decisiones. |



| OBJ–09 | Gestión de Seguridad |

| :---- | :---- |

| \*\*Descripción\*\* | Gestionar la autenticación mediante correo/contraseña o Google Authentication. Incluye verificación de email, cierre de sesión manual y automático por inactividad, y recuperación de contraseñas. |

| \*\*Estabilidad\*\* | Alta. |

| \*\*Comentarios\*\* | Indispensable para proteger la información del sistema y la privacidad de los usuarios. |



| OBJ–10 | Integración con WhatsApp |

| :---- | :---- |

| \*\*Descripción\*\* | Permitir a los clientes interactuar con el sistema a través de WhatsApp para registrarse, registrar vehículos, editar datos personales, gestionar turnos, consultar información del lavadero y comunicarse con el personal.  |

| \*\*Estabilidad\*\* | Alta. |

| \*\*Comentarios\*\* | Mejora la experiencia del cliente y reduce la carga operativa del personal. |



| OBJ–11 | Gestión de Configuración del Sistema |

| : ---- | :---- |

| \*\*Descripción\*\* | Permitir al administrador configurar parámetros del sistema como horarios de operación, información del lavadero, capacidad máxima, tiempos de tolerancia, duración de sesión y configuración de descuentos para paquetes.  |

| \*\*Estabilidad\*\* | Alta. |

| \*\*Comentarios\*\* | Centraliza la configuración operativa del lavadero.  |



| OBJ–12 | Notificación al Cliente |

| : ---- | :---- |

| \*\*Descripción\*\* | Enviar notificaciones automáticas y manuales a los clientes por WhatsApp y correo electrónico sobre el estado de sus servicios, etapas finalizadas y vehículos listos para retirar. |

| \*\*Estabilidad\*\* | Media. |

| \*\*Comentarios\*\* | Mejora la comunicación con el cliente y la experiencia de servicio. |



\# \*\*\*Diagrama de Caso de Uso del Sistema\*\*\* {#diagrama-de-caso-de-uso-del-sistema}



\## Figura 1 - Diagrama Caso Usos Sistema General



```plantuml

@startuml

left to right direction

skinparam packageStyle rectangle

skinparam actorStyle awesome



actor "Trabajador\\n(Empleado)" as Trabajador

actor "Administrador" as Administrador

actor "Cliente" as Cliente

actor "Sistema" as SistemaActor



Administrador --|> Trabajador



rectangle "Sistema LavaFacil" {

&nbsp;   package "Seguridad" {

&nbsp;       usecase "CU-001 Iniciar Sesión" as CU001

&nbsp;       usecase "CU-002 Cerrar Sesión" as CU002

&nbsp;       usecase "CU-003 Recuperar Contraseña" as CU003

&nbsp;       usecase "CU-004 Cierre Automático\\npor Inactividad" as CU004

&nbsp;   }

&nbsp;   

&nbsp;   package "Gestión de Empleados" {

&nbsp;       usecase "CU-005 Registrarse en el Sistema" as CU005

&nbsp;       usecase "CU-006 Modificar Empleado" as CU006

&nbsp;       usecase "CU-007 Desactivar Empleado" as CU007

&nbsp;       usecase "CU-008 Reactivar Empleado" as CU008

&nbsp;       usecase "CU-009 Consultar Empleados" as CU009

&nbsp;       usecase "CU-010 Asignar Roles" as CU010

&nbsp;       usecase "CU-011 Autenticar con Google\\ny registrar si es nuevo" as CU011

&nbsp;   }

&nbsp;   

&nbsp;   package "Gestión de Clientes y Vehículos" {

&nbsp;       usecase "CU-012 Crear Cliente" as CU012

&nbsp;       usecase "CU-013 Modificar Cliente" as CU013

&nbsp;       usecase "CU-014 Desactivar Cliente" as CU014

&nbsp;       usecase "CU-015 Reactivar Cliente" as CU015

&nbsp;       usecase "CU-016 Consultar Clientes" as CU016

&nbsp;       usecase "CU-017 Buscar Clientes" as CU017

&nbsp;       usecase "CU-018 Crear Vehículo" as CU018

&nbsp;       usecase "CU-019 Modificar Vehículo" as CU019

&nbsp;       usecase "CU-020 Desactivar Vehículo" as CU020

&nbsp;       usecase "CU-021 Consultar Vehículos" as CU021

&nbsp;       usecase "CU-022 Buscar Vehículos" as CU022

&nbsp;       usecase "CU-023 Vincular Vehículo a Cliente" as CU023

&nbsp;       usecase "CU-024 Desvincular Vehículo de Cliente" as CU024

&nbsp;       usecase "CU-025 Registrarse como Cliente (WhatsApp)" as CU025

&nbsp;       usecase "CU-026 Registrar Vehículo (WhatsApp)" as CU026

&nbsp;       usecase "CU-027 Identificar Teléfono Registrado" as CU027

&nbsp;       usecase "CU-028 Editar Datos Personales (WhatsApp)" as CU028

&nbsp;   }

&nbsp;   

&nbsp;   package "Gestión de Servicios" {

&nbsp;       usecase "CU-029 Crear Servicio" as CU029

&nbsp;       usecase "CU-030 Modificar Servicio" as CU030

&nbsp;       usecase "CU-031 Desactivar Servicio" as CU031

&nbsp;       usecase "CU-032 Reactivar Servicio" as CU032

&nbsp;       usecase "CU-033 Consultar Servicios" as CU033

&nbsp;       usecase "CU-034 Buscar Servicios" as CU034

&nbsp;       usecase "CU-035 Crear Tipo de Servicio" as CU035

&nbsp;       usecase "CU-036 Eliminar Tipo de Servicio" as CU036

&nbsp;       usecase "CU-037 Crear Tipo de Vehículo" as CU037

&nbsp;       usecase "CU-038 Eliminar Tipo de Vehículo" as CU038

&nbsp;       usecase "CU-039 Gestionar Etapas del Servicio" as CU039

&nbsp;       usecase "CU-040 Crear Paquete de Servicios" as CU040

&nbsp;       usecase "CU-041 Modificar Paquete de Servicios" as CU041

&nbsp;       usecase "CU-042 Desactivar Paquete de Servicios" as CU042

&nbsp;       usecase "CU-043 Reactivar Paquete de Servicios" as CU043

&nbsp;       usecase "CU-044 Consultar Paquetes de Servicios" as CU044

&nbsp;   }

&nbsp;   

&nbsp;   package "Registro de Lavados" {

&nbsp;       usecase "CU-045 Registrar Lavado" as CU045

&nbsp;       usecase "CU-046 Consultar Lavados" as CU046

&nbsp;       usecase "CU-047 Buscar Lavados" as CU047

&nbsp;       usecase "CU-048 Ver Detalle de Lavado" as CU048

&nbsp;       usecase "CU-049 Iniciar Servicio en Lavado" as CU049

&nbsp;       usecase "CU-050 Iniciar Etapa de Servicio" as CU050

&nbsp;       usecase "CU-051 Finalizar Etapa de Servicio" as CU051

&nbsp;       usecase "CU-052 Finalizar Servicio en Lavado" as CU052

&nbsp;       usecase "CU-053 Finalizar Lavado Completo" as CU053

&nbsp;       usecase "CU-054 Cancelar Lavado" as CU054

&nbsp;       usecase "CU-055 Cancelar Servicio en Lavado" as CU055

&nbsp;       usecase "CU-056 Registrar Pago Recibido" as CU056

&nbsp;       usecase "CU-057 Registrar Pago Parcial" as CU057

&nbsp;       usecase "CU-058 Marcar Vehículo Retirado" as CU058

&nbsp;       usecase "CU-059 Calcular Duración Estimada" as CU059

&nbsp;   }

&nbsp;   

&nbsp;   package "Configuración" {

&nbsp;       usecase "CU-060 Configurar Horarios" as CU060

&nbsp;       usecase "CU-061 Configurar Capacidad" as CU061

&nbsp;       usecase "CU-062 Configurar Tiempos Tolerancia" as CU062

&nbsp;       usecase "CU-063 Configurar Duración Sesión" as CU063

&nbsp;       usecase "CU-064 Configurar Nombre y Ubicación" as CU064

&nbsp;       usecase "CU-065 Configurar Descuento Paquetes" as CU065

&nbsp;   }

&nbsp;   

&nbsp;   package "Planificación de Turnos" {

&nbsp;       usecase "CU-066 Registrar Turno" as CU066

&nbsp;       usecase "CU-067 Modificar Turno" as CU067

&nbsp;       usecase "CU-068 Consultar Turnos Asignados" as CU068

&nbsp;       usecase "CU-069 Cancelar Turno" as CU069

&nbsp;       usecase "CU-070 Solicitar Turno (WhatsApp)" as CU070

&nbsp;       usecase "CU-071 Consultar Turnos (WhatsApp)" as CU071

&nbsp;       usecase "CU-072 Cancelar Turno (WhatsApp)" as CU072

&nbsp;       usecase "CU-073 Asignar Turno sin Superposición" as CU073

&nbsp;       usecase "CU-074 Validar Disponibilidad al Mover" as CU074

&nbsp;       usecase "CU-075 Reorganizar Agenda" as CU075

&nbsp;   }

&nbsp;   

&nbsp;   package "Notificación al Cliente" {

&nbsp;       usecase "CU-076 Enviar Notificación WhatsApp" as CU076

&nbsp;       usecase "CU-077 Enviar Notificación Email" as CU077

&nbsp;       usecase "CU-078 Notificar Etapa Finalizada" as CU078

&nbsp;       usecase "CU-079 Notificar Lavado Finalizado" as CU079

&nbsp;       usecase "CU-080 Solicitar Hablar con Personal" as CU080

&nbsp;   }

&nbsp;   

&nbsp;   package "Estadísticas y Reportes" {

&nbsp;       usecase "CU-081 Consultar Estadísticas" as CU081

&nbsp;       usecase "CU-082 Consultar Historial Pagos" as CU082

&nbsp;       usecase "CU-083 Generar Reportes" as CU083

&nbsp;   }

&nbsp;   

&nbsp;   package "Auditoría" {

&nbsp;       usecase "CU-084 Consultar Historial Auditoría" as CU084

&nbsp;       usecase "CU-085 Filtrar Registros Auditoría" as CU085

&nbsp;       usecase "CU-086 Ver Detalle Registro Auditoría" as CU086

&nbsp;       usecase "CU-087 Registrar Acciones para Auditoría" as CU087

&nbsp;   }

&nbsp;   

&nbsp;   package "Integración WhatsApp" {

&nbsp;       usecase "CU-088 Procesar Mensaje Entrante" as CU088

&nbsp;       usecase "CU-089 Validar Webhook WhatsApp" as CU089

&nbsp;       usecase "CU-090 Gestionar Sesión Conversación" as CU090

&nbsp;       usecase "CU-091 Mostrar Menú Cliente" as CU091

&nbsp;       usecase "CU-092 Mostrar Info Lavadero" as CU092

&nbsp;   }

}



' Relaciones Trabajador

Trabajador --> CU001

Trabajador --> CU002

Trabajador --> CU003

Trabajador --> CU012

Trabajador --> CU013

Trabajador --> CU016

Trabajador --> CU017

Trabajador --> CU018

Trabajador --> CU019

Trabajador --> CU021

Trabajador --> CU022

Trabajador --> CU023

Trabajador --> CU024

Trabajador --> CU033

Trabajador --> CU034

Trabajador --> CU044

Trabajador --> CU045

Trabajador --> CU046

Trabajador --> CU047

Trabajador --> CU048

Trabajador --> CU049

Trabajador --> CU050

Trabajador --> CU051

Trabajador --> CU052

Trabajador --> CU053

Trabajador --> CU054

Trabajador --> CU055

Trabajador --> CU056

Trabajador --> CU057

Trabajador --> CU058

Trabajador --> CU076

Trabajador --> CU077



' Relaciones Administrador (además de heredadas)

Administrador --> CU005

Administrador --> CU006

Administrador --> CU007

Administrador --> CU008

Administrador --> CU009

Administrador --> CU010

Administrador --> CU014

Administrador --> CU015

Administrador --> CU020

Administrador --> CU029

Administrador --> CU030

Administrador --> CU031

Administrador --> CU032

Administrador --> CU035

Administrador --> CU036

Administrador --> CU037

Administrador --> CU038

Administrador --> CU039

Administrador --> CU040

Administrador --> CU041

Administrador --> CU042

Administrador --> CU043

Administrador --> CU060

Administrador --> CU061

Administrador --> CU062

Administrador --> CU063

Administrador --> CU064

Administrador --> CU065

Administrador --> CU066

Administrador --> CU067

Administrador --> CU068

Administrador --> CU069

Administrador --> CU081

Administrador --> CU082

Administrador --> CU083

Administrador --> CU084

Administrador --> CU085

Administrador --> CU086



' Relaciones Cliente

Cliente --> CU025

Cliente --> CU026

Cliente --> CU028

Cliente --> CU070

Cliente --> CU071

Cliente --> CU072

Cliente --> CU080

Cliente --> CU092



' Relaciones Sistema

SistemaActor --> CU004

SistemaActor --> CU011

SistemaActor --> CU027

SistemaActor --> CU059

SistemaActor --> CU073

SistemaActor --> CU074

SistemaActor --> CU075

SistemaActor --> CU078

SistemaActor --> CU079

SistemaActor --> CU087

SistemaActor --> CU088

SistemaActor --> CU089

SistemaActor --> CU090

SistemaActor --> CU091



' Includes para Modificar/Desactivar/Reactivar

CU006 .. > CU009 : <<include>>

CU007 ..> CU009 : <<include>>

CU008 ..> CU009 : <<include>>

CU013 ..> CU016 : <<include>>

CU014 ..> CU016 : <<include>>

CU015 ..> CU016 : <<include>>

CU019 ..> CU021 : <<include>>

CU020 ..> CU021 : <<include>>

CU023 ..> CU021 : <<include>>

CU024 ..> CU021 : <<include>>

CU030 ..> CU033 : <<include>>

CU031 ..> CU033 : <<include>>

CU032 ..> CU033 : <<include>>

CU041 ..> CU044 : <<include>>

CU042 ..> CU044 : <<include>>

CU043 ..> CU044 : <<include>>

CU048 ..> CU046 : <<include>>

CU049 ..> CU046 : <<include>>

CU050 ..> CU046 : <<include>>

CU051 ..> CU046 : <<include>>

CU052 ..> CU046 : <<include>>

CU053 ..> CU046 : <<include>>

CU054 ..> CU046 : <<include>>

CU055 ..> CU046 : <<include>>

CU056 ..> CU046 : <<include>>

CU057 ..> CU046 : <<include>>

CU058 ..> CU046 : <<include>>

CU067 ..> CU068 : <<include>>

CU069 ..> CU068 : <<include>>

CU085 ..> CU084 : <<include>>

CU086 ..> CU084 : <<include>>



@enduml

```



\## Lista Completa de Casos de Uso (92 CU)



| Caso de Uso | Importancia |

| :---- | :---- |

| \*\*Módulo Seguridad\*\* | |

| CU-001 - Iniciar Sesión | Alta |

| CU-001. 1 - Iniciar sesión con correo y contraseña | Alta |

| CU-001.2 - Iniciar sesión con Google | Alta |

| CU-002 - Cerrar sesión | Media |

| CU-003 - Recuperar contraseña | Alta |

| CU-004 - Cierre de sesión automático por inactividad | Media |

| \*\*Módulo Gestión de Empleados\*\* | |

| CU-005 - Registrarse en el sistema | Alta |

| CU-005.1 - Registrarse por correo | Alta |

| CU-005.2 - Registrarse por Google | Alta |

| CU-006 - Modificar empleado | Media |

| CU-007 - Desactivar empleado | Media |

| CU-008 - Reactivar empleado | Media |

| CU-009 - Consultar empleados | Media |

| CU-010 - Asignar roles a empleados | Alta |

| CU-011 - Autenticar usuario con Google y registrar perfil si es nuevo | Alta |

| \*\*Módulo Gestión de Clientes y Vehículos\*\* | |

| CU-012 - Crear cliente | Alta |

| CU-013 - Modificar cliente | Media |

| CU-014 - Desactivar cliente | Media |

| CU-015 - Reactivar cliente | Media |

| CU-016 - Consultar clientes | Alta |

| CU-017 - Buscar clientes | Alta |

| CU-018 - Crear vehículo | Alta |

| CU-019 - Modificar vehículo | Media |

| CU-020 - Desactivar vehículo | Media |

| CU-021 - Consultar vehículos | Alta |

| CU-022 - Buscar vehículos | Alta |

| CU-023 - Vincular vehículo a cliente | Media |

| CU-024 - Desvincular vehículo de cliente | Media |

| CU-025 - Registrarse como cliente por WhatsApp | Alta |

| CU-026 - Registrar vehículo por WhatsApp | Alta |

| CU-027 - Identificar si el número de teléfono está registrado | Alta |

| CU-028 - Editar datos personales por WhatsApp | Media |

| \*\*Módulo Gestión de Servicios\*\* | |

| CU-029 - Crear servicio | Alta |

| CU-030 - Modificar servicio | Media |

| CU-031 - Desactivar servicio | Media |

| CU-032 - Reactivar servicio | Media |

| CU-033 - Consultar servicios | Alta |

| CU-034 - Buscar servicios | Media |

| CU-035 - Crear tipo de servicio | Media |

| CU-036 - Eliminar tipo de servicio | Baja |

| CU-037 - Crear tipo de vehículo | Media |

| CU-038 - Eliminar tipo de vehículo | Baja |

| CU-039 - Gestionar etapas del servicio | Media |

| CU-040 - Crear paquete de servicios | Media |

| CU-041 - Modificar paquete de servicios | Baja |

| CU-042 - Desactivar paquete de servicios | Baja |

| CU-043 - Reactivar paquete de servicios | Baja |

| CU-044 - Consultar paquetes de servicios | Media |

| \*\*Módulo Registro de Lavados\*\* | |

| CU-045 - Registrar realización de un servicio (lavado) | Alta |

| CU-046 - Consultar lavados | Alta |

| CU-047 - Buscar lavados | Alta |

| CU-048 - Ver detalle de lavado | Alta |

| CU-049 - Iniciar servicio en lavado | Alta |

| CU-050 - Iniciar etapa de servicio | Alta |

| CU-051 - Finalizar etapa de servicio | Alta |

| CU-052 - Finalizar servicio en lavado | Alta |

| CU-053 - Finalizar lavado completo | Alta |

| CU-054 - Cancelar lavado | Alta |

| CU-055 - Cancelar servicio en lavado | Media |

| CU-056 - Registrar pago recibido | Alta |

| CU-057 - Registrar pago parcial | Alta |

| CU-058 - Marcar vehículo como retirado | Media |

| CU-059 - Calcular duración estimada de lavado | Alta |

| \*\*Módulo Configuración\*\* | |

| CU-060 - Configurar horarios del lavadero | Alta |

| CU-061 - Configurar capacidad concurrente | Alta |

| CU-062 - Configurar tiempos de tolerancia y notificación | Media |

| CU-063 - Configurar duración de sesión | Media |

| CU-064 - Configurar nombre y ubicación del lavadero | Media |

| CU-065 - Configurar paso de descuento para paquetes | Baja |

| \*\*Módulo Planificación de Turnos\*\* | |

| CU-066 - Registrar turno | Alta |

| CU-067 - Modificar turno | Media |

| CU-068 - Consultar turnos asignados | Alta |

| CU-069 - Cancelar turno | Media |

| CU-070 - Solicitar turno por WhatsApp | Alta |

| CU-071 - Consultar turnos próximos por WhatsApp | Media |

| CU-072 - Cancelar turno por WhatsApp | Media |

| CU-073 - Asignar turno automáticamente sin superposición | Alta |

| CU-074 - Validar disponibilidad al mover un turno | Alta |

| CU-075 - Reorganizar agenda ante cancelaciones | Media |

| \*\*Módulo Notificación al Cliente\*\* | |

| CU-076 - Enviar notificación por WhatsApp | Media |

| CU-077 - Enviar notificación por correo electrónico | Media |

| CU-078 - Notificar etapa finalizada | Media |

| CU-079 - Notificar lavado finalizado | Alta |

| CU-080 - Solicitar hablar con el personal | Baja |

| \*\*Módulo Estadísticas y Reportes\*\* | |

| CU-081 - Consultar estadísticas básicas | Media |

| CU-082 - Consultar historial de pagos | Media |

| CU-083 - Generar reportes | Media |

| CU-083.1 - Exportar reportes a PDF o Excel | Media |

| \*\*Módulo Auditoría\*\* | |

| CU-084 - Consultar historial de auditoría | Alta |

| CU-085 - Filtrar registros de auditoría | Media |

| CU-086 - Ver detalle de registro de auditoría | Media |

| CU-087 - Registrar todas las acciones para auditoría | Alta |

| \*\*Módulo Integración WhatsApp\*\* | |

| CU-088 - Procesar mensaje entrante de WhatsApp | Alta |

| CU-089 - Validar webhook de WhatsApp | Alta |

| CU-090 - Gestionar sesión de conversación | Alta |

| CU-091 - Mostrar menú de cliente autenticado | Alta |

| CU-092 - Mostrar información del lavadero | Media |



\# \*\*\*Subsistemas del Proyecto\*\*\* {#subsistemas-del-proyecto}



\## \*\*Diagrama de los Subsistemas\*\*



```plantuml

@startuml

skinparam packageStyle rectangle

skinparam linetype ortho



package "Sistema LavaFacil" {

&nbsp;   package "Seguridad" as SEC {

&nbsp;       \[Autenticación]

&nbsp;       \[Gestión de Sesiones]

&nbsp;       \[Recuperación de Contraseña]

&nbsp;   }

&nbsp;   

&nbsp;   package "Gestión de Empleados" as EMP {

&nbsp;       \[CRUD Empleados]

&nbsp;       \[Roles y Permisos]

&nbsp;   }

&nbsp;   

&nbsp;   package "Gestión de Clientes y Vehículos" as CLI {

&nbsp;       \[CRUD Clientes]

&nbsp;       \[CRUD Vehículos]

&nbsp;       \[Vinculación/Desvinculación]

&nbsp;   }

&nbsp;   

&nbsp;   package "Gestión de Servicios" as SER {

&nbsp;       \[CRUD Servicios]

&nbsp;       \[CRUD Paquetes]

&nbsp;       \[Tipos de Servicio]

&nbsp;       \[Tipos de Vehículo]

&nbsp;       \[Etapas de Servicio]

&nbsp;   }

&nbsp;   

&nbsp;   package "Registro de Lavados" as LAV {

&nbsp;       \[Registro de Lavados]

&nbsp;       \[Gestión de Etapas]

&nbsp;       \[Registro de Pagos]

&nbsp;       \[Estado de Retiro]

&nbsp;   }

&nbsp;   

&nbsp;   package "Planificación de Turnos" as TUR {

&nbsp;       \[Agenda de Turnos]

&nbsp;       \[Validación Disponibilidad]

&nbsp;       \[Reorganización Automática]

&nbsp;   }

&nbsp;   

&nbsp;   package "Integración WhatsApp" as WA {

&nbsp;       \[Flujos Conversacionales]

&nbsp;       \[Gestión de Sesiones WA]

&nbsp;       \[Procesamiento Mensajes]

&nbsp;   }

&nbsp;   

&nbsp;   package "Configuración" as CFG {

&nbsp;       \[Configuración del Sistema]

&nbsp;       \[Horarios]

&nbsp;       \[Capacidad]

&nbsp;   }

&nbsp;   

&nbsp;   package "Notificación al Cliente" as NOT {

&nbsp;       \[Notificaciones WhatsApp]

&nbsp;       \[Notificaciones Email]

&nbsp;       \[Notificaciones Automáticas]

&nbsp;   }

&nbsp;   

&nbsp;   package "Auditoría y Reportes" as AUD {

&nbsp;       \[Registro de Auditoría]

&nbsp;       \[Estadísticas]

&nbsp;       \[Generación de Reportes]

&nbsp;   }

}



SEC --> EMP :  usa

EMP --> AUD : registra

CLI --> AUD : registra

SER --> AUD : registra

LAV --> AUD : registra

LAV --> CLI : consulta

LAV --> SER : consulta

LAV --> EMP : asigna

LAV --> NOT : notifica

TUR --> CLI : consulta

TUR --> SER :  consulta

TUR --> CFG : valida horarios

WA --> CLI : gestiona

WA --> TUR : gestiona turnos

WA --> CFG : consulta

SER --> CFG : consulta

LAV --> CFG :  consulta



@enduml

```



\## \*\*Descripción de Subsistema\*\*



| Subsistema | Descripción |

| ----- | ----- |

| \*\*Seguridad\*\* | Gestiona el acceso al sistema mediante autenticación con correo/contraseña o Google Authentication.  Incluye verificación de email, manejo de sesiones, cierre automático por inactividad y recuperación de contraseñas.  Valida que el usuario esté activo y tenga el email verificado antes de permitir el acceso. |

| \*\*Gestión de Empleados\*\* | Permite la alta, desactivación, reactivación, modificación y consulta del personal del lavadero. Administra roles (Administrador/Empleado) y niveles de acceso.  Solo los administradores pueden gestionar empleados.  Incluye registro automático de usuarios que inician sesión con Google por primera vez. |

| \*\*Gestión de Clientes y Vehículos\*\* | Permite registrar, modificar, consultar, desactivar y reactivar clientes y sus vehículos.  Soporta vehículos con múltiples dueños mediante clave de asociación SHA256. Incluye vinculación y desvinculación de vehículos a clientes, con desactivación en cascada cuando corresponde. |

| \*\*Gestión de Servicios\*\* | Permite definir, modificar, desactivar y reactivar servicios individuales o paquetes de servicios. Los servicios tienen duración estimada por tipo de vehículo y pueden incluir etapas secuenciales. Los paquetes agrupan servicios con descuentos configurables.  Incluye gestión de tipos de servicio y tipos de vehículo. |

| \*\*Registro de Lavados\*\* | Centraliza el registro y seguimiento de lavados, incluyendo creación, inicio de servicios y etapas, finalización parcial o total, cancelación de servicios individuales o completos, registro de pagos totales y parciales, y marcado de retiro de vehículo.  Calcula automáticamente el tiempo estimado.  |

| \*\*Planificación de Turnos\*\* | Gestiona la agenda de turnos del lavadero, evitando solapamientos mediante validación automática.  Permite reorganización automática ante cancelaciones y notificación a clientes sobre posibles adelantos. Incluye gestión de turnos por WhatsApp.  |

| \*\*Integración WhatsApp\*\* | Permite a los clientes interactuar con el sistema a través de WhatsApp Cloud API. Incluye flujos conversacionales para registro de clientes, vehículos, gestión de turnos, consulta de información del lavadero y edición de datos personales.  Mantiene sesiones de conversación para contexto.  |

| \*\*Configuración\*\* | Permite al administrador configurar parámetros del sistema como nombre del lavadero, horarios de operación, ubicación, teléfono, capacidad máxima, tiempos de tolerancia, duración de sesión y porcentajes de descuento para paquetes. |

| \*\*Notificación al Cliente\*\* | Envía notificaciones automáticas y manuales a los clientes por WhatsApp y correo electrónico.  Incluye notificaciones de etapa finalizada, lavado completo y vehículo listo para retirar. |

| \*\*Auditoría y Reportes\*\* | Registra automáticamente todas las acciones realizadas en el sistema.  Permite consultar el historial con filtros por fecha, tipo de acción, entidad afectada y usuario.  Proporciona estadísticas, historial de pagos y generación de reportes exportables. |



\# \*\*\*Requisitos del Sistema\*\*\* {#requisitos-del-sistema}



\## \*\*Requisitos de Información\*\*



| IRQ–01 | Información sobre Empleados |

| : ---: | ----- |

| \*\*Objetivos asociados\*\* | OBJ–01 Gestión de Empleados, OBJ–09 Gestión de Seguridad |

| \*\*Requisitos asociados\*\* | CU-001, CU-002, CU-003, CU-004, CU-005, CU-006, CU-007, CU-008, CU-009, CU-010, CU-011 |

| \*\*Descripción\*\* | El sistema deberá almacenar la información relacionada con los empleados del lavadero, tanto activos como inactivos.  Se registrarán sus credenciales, datos personales y su rol dentro del sistema. |

| \*\*Datos específicos\*\* | Id (UID de Firebase), Nombre, Apellido, NombreCompleto, Correo electrónico, Rol asignado (Administrador / Empleado), Estado del empleado (Activo / Inactivo), EmailVerificado (booleano), FechaCreacion, FechaActualizacion |

| \*\*Estabilidad\*\* | Alta |

| \*\*Comentarios\*\* | Los roles y estados se vinculan con el módulo de seguridad y auditoría.  La autenticación se gestiona a través de Firebase Authentication con soporte para Google Sign-In. |



| IRQ–02 | Información sobre Clientes |

| :---: | ----- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |

| \*\*Requisitos asociados\*\* | CU-012, CU-013, CU-014, CU-015, CU-016, CU-017, CU-025, CU-028 |

| \*\*Descripción\*\* | El sistema deberá almacenar la información completa de los clientes, permitiendo mantener una trazabilidad completa de los servicios prestados. |

| \*\*Datos específicos\*\* | Id, Nombre, Apellido, NombreCompleto, Teléfono, TipoDocumento, NumeroDocumento, Correo electrónico, Estado del cliente (Activo / Inactivo), VehiculosIds (lista de IDs de vehículos asociados), FechaCreacion |

| \*\*Estabilidad\*\* | Alta |

| \*\*Comentarios\*\* | Los clientes pueden registrarse a través de la aplicación web (por el personal) o a través de WhatsApp (autoservicio). |



| IRQ–03 | Información sobre Vehículos |

| :---: | ----- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |

| \*\*Requisitos asociados\*\* | CU-018, CU-019, CU-020, CU-021, CU-022, CU-023, CU-024, CU-026 |

| \*\*Descripción\*\* | El sistema deberá almacenar la información de los vehículos, soportando múltiples dueños por vehículo mediante clave de asociación.  |

| \*\*Datos específicos\*\* | Id, Patente, TipoVehiculo, Marca, Modelo, Color, ClienteId (dueño principal), ClienteNombreCompleto, ClientesIds (lista de todos los clientes asociados), ClaveAsociacionHash (hash SHA256 para permitir asociación), Estado (Activo / Inactivo) |

| \*\*Estabilidad\*\* | Alta |

| \*\*Comentarios\*\* | Los vehículos pueden tener múltiples dueños.  La clave de asociación permite que otros clientes se vinculen al vehículo ingresando la clave correcta. |



| IRQ–04 | Información sobre Servicios |

| :---: | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |

| \*\*Requisitos asociados\*\* | CU-029, CU-030, CU-031, CU-032, CU-033, CU-034, CU-039 |

| \*\*Descripción\*\* | El sistema deberá registrar y administrar la información de los servicios individuales ofrecidos por el lavadero. |

| \*\*Datos específicos\*\* | Id, Nombre, Descripcion, TipoServicio, TipoVehiculo, Precio, TiempoEstimado (en minutos), Estado (Activo / Inactivo), Etapas (lista de etapas con Id, Nombre, Descripcion, Orden) |

| \*\*Estabilidad\*\* | Alta |

| \*\*Comentarios\*\* | Los servicios pueden tener múltiples etapas que se ejecutan secuencialmente durante el lavado. El tiempo estimado y precio varían según el tipo de vehículo. |



| IRQ–05 | Información sobre Tipos de Servicio |

| :---:  | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |

| \*\*Requisitos asociados\*\* | CU-035, CU-036 |

| \*\*Descripción\*\* | El sistema deberá almacenar las categorías de servicios disponibles.  |

| \*\*Datos específicos\*\* | Id, Nombre, Estado (Activo / Inactivo) |

| \*\*Estabilidad\*\* | Alta |

| \*\*Comentarios\*\* | Los tipos de servicio permiten categorizar y organizar los servicios ofrecidos.  Solo puede haber un servicio de cada tipo en un paquete. |



| IRQ–06 | Información sobre Tipos de Vehículo |

| : ---: | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |

| \*\*Requisitos asociados\*\* | CU-037, CU-038 |

| \*\*Descripción\*\* | El sistema deberá almacenar los tipos de vehículos disponibles para asociar a servicios y vehículos de clientes. |

| \*\*Datos específicos\*\* | Id, Nombre, FormatoPatente, CantidadEmpleadosRequeridos, Estado (Activo / Inactivo) |

| \*\*Estabilidad\*\* | Alta |

| \*\*Comentarios\*\* | Los tipos de vehículo determinan la cantidad de empleados necesarios y se usan para filtrar servicios compatibles. |



| IRQ–07 | Información sobre Paquetes de Servicios |

| :---: | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |

| \*\*Requisitos asociados\*\* | CU-040, CU-041, CU-042, CU-043, CU-044 |

| \*\*Descripción\*\* | El sistema deberá registrar paquetes que agrupan múltiples servicios con un descuento aplicado. |

| \*\*Datos específicos\*\* | Id, Nombre, Estado (Activo / Inactivo), Precio (calculado), PorcentajeDescuento, TiempoEstimado (suma de servicios), TipoVehiculo, ServiciosIds (lista de IDs de servicios incluidos) |

| \*\*Estabilidad\*\* | Alta |

| \*\*Comentarios\*\* | Un paquete debe contener al menos 2 servicios.  Solo puede haber un servicio de cada tipo dentro del paquete.  Todos los servicios deben ser para el mismo tipo de vehículo. |



| IRQ–08 | Información sobre Lavados |

| : ---: | :---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados, OBJ–05 Registro de Pagos |

| \*\*Requisitos asociados\*\* | CU-045 a CU-059 |

| \*\*Descripción\*\* | El sistema deberá almacenar los lavados realizados, incluyendo servicios, empleados asignados, estados, pagos y tiempos. |

| \*\*Datos específicos\*\* | Id, Estado (EnProceso, Realizado, RealizadoParcialmente, Cancelado), ClienteId, ClienteNombre, VehiculoId, VehiculoPatente, VehiculoTipo, ServiciosDetalles (lista con estado de cada servicio y sus etapas), PaquetesIds, EmpleadosAsignadosIds, TiempoEstimado, TiempoInicio, TiempoFinalizacion, FechaCreacion, MotivoCancelacion, Notas, ClienteTrajoId, ClienteRetiraId, EstadoRetiro (Pendiente/Retirado), Precio, Pago (objeto con MontoTotal, MontoPagado, EstadoPago, Pagos lista de pagos individuales) |

| \*\*Estabilidad\*\* | Alta |

| \*\*Comentarios\*\* | La información se actualiza automáticamente al avanzar en las etapas, cancelar servicios o registrar pagos. |



| IRQ–09 | Información sobre Turnos |

| : ---: | :---- |

| \*\*Objetivos asociados\*\* | OBJ–06 Planificación y Gestión de Turnos |

| \*\*Requisitos asociados\*\* | CU-066 a CU-075 |

| \*\*Descripción\*\* | El sistema deberá almacenar los turnos asignados a los clientes.  |

| \*\*Datos específicos\*\* | Id, FechaHora, ClienteId, ClienteNombre, VehiculoId, VehiculoPatente, ServiciosIds, DuracionEstimada, Estado (Pendiente, Confirmado, Cancelado, Completado), FechaCreacion |

| \*\*Estabilidad\*\* | Alta |

| \*\*Comentarios\*\* | El sistema valida automáticamente que no haya solapamientos al crear o modificar turnos.  |



| IRQ–10 | Información de Auditoría |

| :---: | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–07 Registro de Auditoría |

| \*\*Requisitos asociados\*\* | CU-084, CU-085, CU-086, CU-087 |

| \*\*Descripción\*\* | El sistema deberá mantener un registro de todas las acciones realizadas por los usuarios, con detalle de fecha, hora, usuario y entidad afectada.  |

| \*\*Datos específicos\*\* | UserId, UserEmail, Action (descripción de la acción), TargetId (ID del objeto afectado), TargetType (tipo de entidad:  Servicio, Empleado, Cliente, Vehiculo, Lavado, etc.), Timestamp |

| \*\*Estabilidad\*\* | Alta |

| \*\*Comentarios\*\* | Datos críticos para control interno, auditorías y trazabilidad. Se registran automáticamente en cada operación CRUD. |



| IRQ–11 | Información de Configuración del Sistema |

| : ---: | :---- |

| \*\*Objetivos asociados\*\* | OBJ–11 Gestión de Configuración del Sistema |

| \*\*Requisitos asociados\*\* | CU-060, CU-061, CU-062, CU-063, CU-064, CU-065 |

| \*\*Descripción\*\* | El sistema deberá almacenar los parámetros de configuración del lavadero. |

| \*\*Datos específicos\*\* | Id, NombreLavadero, Telefono, Email, Ubicacion, HorariosOperacion (diccionario día -> horario), MaxLavadosSimultaneos, ConsiderarEmpleados, TiempoNotificacionAnticipada, TiempoToleranciaMaxima, IntervaloConsultaExceso, DuracionSesionHoras, TiempoInactividadMinutos, PaquetesDescuentoStep, PaquetesDescuentoMin, PaquetesDescuentoMax |

| \*\*Estabilidad\*\* | Alta |

| \*\*Comentarios\*\* | La configuración afecta la operación diaria del lavadero y las validaciones del sistema. |



| IRQ–12 | Información de Sesiones WhatsApp |

| :---: | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–10 Integración con WhatsApp |

| \*\*Requisitos asociados\*\* | CU-088, CU-089, CU-090, CU-091 |

| \*\*Descripción\*\* | El sistema deberá mantener el estado de las conversaciones con clientes a través de WhatsApp. |

| \*\*Datos específicos\*\* | PhoneNumber, ClienteId (si está autenticado), CurrentState (estado del flujo conversacional), TemporaryData (datos temporales del flujo), LastActivity, CreatedAt |

| \*\*Estabilidad\*\* | Alta |

| \*\*Comentarios\*\* | Las sesiones permiten mantener contexto en las conversaciones y guiar al usuario a través de flujos interactivos. |



\## \*\*Requisitos Funcionales\*\*



A continuación se presentan los casos de uso extendidos del sistema. 



\### \*\*Descripción de Actores\*\*



| ACT–01 | Trabajador (Empleado) |

| :---- | :---- |

| \*\*Descripción\*\* | Usuario operativo del sistema.  Sus funciones principales son la gestión diaria del lavadero:  registro y recepción de clientes, gestión de vehículos, operación del flujo de lavado, registro de pagos y notificación al cliente. |

| \*\*Comentarios\*\* | Ninguno.  |



| ACT–02 | Administrador |

| :---- | :---- |

| \*\*Descripción\*\* | Dueño o gerente del lavadero. Hereda todos los permisos del Trabajador, pero posee acceso exclusivo a módulos críticos:  gestión de usuarios (empleados), configuración de servicios y paquetes, auditoría, estadísticas, reportes, planificación de turnos y configuración del sistema. |

| \*\*Comentarios\*\* | Hereda todos los permisos del Trabajador. |



| ACT–03 | Cliente |

| :---- | :---- |

| \*\*Descripción\*\* | Usuario externo.  Interactúa con el sistema principalmente a través de la integración con WhatsApp (Bot), permitiéndole registrarse, gestionar sus vehículos, solicitar turnos, consultar información del lavadero y comunicarse con el personal. |

| \*\*Comentarios\*\* | Interactúa exclusivamente a través de WhatsApp. |



| ACT–04 | Sistema |

| :---- | :---- |

| \*\*Descripción\*\* | Actor lógico encargado de ejecutar procesos automáticos en segundo plano, como la validación de webhooks de WhatsApp, el cierre de sesiones inactivas, el cálculo de tiempos estimados, la reorganización de agenda, el registro de auditoría y el envío de notificaciones automáticas. |

| \*\*Comentarios\*\* | Actor lógico sin intervención humana directa. |



---



\## \*\*Casos de Uso del Sistema\*\*



\### \*\*Módulo:  Seguridad\*\*



---



\### CU-001 - Iniciar Sesión



| UC–001 | Iniciar Sesión |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–09 Gestión de Seguridad |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | El usuario accede al sistema mediante sus credenciales registradas. Este caso de uso es la puerta de entrada al sistema y verifica que el usuario tenga estado activo. |  |

| \*\*Precondición\*\* | El usuario debe tener una cuenta registrada en el sistema.  |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la página de inicio de sesión.  |

|  | 2 | El sistema muestra un formulario con opciones de inicio de sesión:  correo/contraseña o Google. |

|  | 3 | El usuario selecciona el método de autenticación. |

|  | 4 | Se ejecuta el caso de uso correspondiente (CU-001.1 o CU-001.2). |

|  | 5 | El sistema verifica que el usuario esté activo en el sistema. |

|  | 6 | El sistema crea una sesión autenticada con cookies seguras. |

|  | 7 | El sistema redirige al usuario al dashboard principal. |

|  | 8 | El sistema registra el evento de inicio de sesión en auditoría. |

| \*\*Postcondición\*\* | El usuario ha iniciado sesión correctamente y tiene acceso al sistema según su rol. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si el usuario está inactivo, el sistema muestra mensaje de cuenta deshabilitada y no permite el acceso. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 4-7 | 2 segundos |

| \*\*Frecuencia\*\* | Diaria (múltiples veces) |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | La autenticación se gestiona a través de Firebase Authentication.  |  |



---



\### CU-001.1 - Iniciar sesión con correo y contraseña



| UC–001.1 | Iniciar sesión con correo y contraseña |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–09 Gestión de Seguridad |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | Extiende de CU-001. El usuario ingresa su correo electrónico y contraseña para validar su identidad mediante Firebase Authentication. Se verifica que el correo esté verificado antes de permitir el acceso. |  |

| \*\*Precondición\*\* | El usuario debe tener una cuenta registrada con correo y contraseña. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario ingresa su correo electrónico en el formulario.  |

|  | 2 | El usuario ingresa su contraseña. |

|  | 3 | El usuario hace clic en el botón "Iniciar Sesión".  |

|  | 4 | El sistema envía las credenciales a Firebase Authentication. |

|  | 5 | Firebase valida las credenciales y retorna el token de autenticación. |

|  | 6 | El sistema verifica que el email esté verificado.  |

|  | 7 | El sistema continúa con el paso 5 del CU-001. |

| \*\*Postcondición\*\* | Las credenciales han sido validadas correctamente. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si las credenciales son inválidas, el sistema muestra "Correo o contraseña incorrectos". |

|  | 6a | Si el email no está verificado, el sistema muestra mensaje indicando que debe verificar su correo y ofrece reenviar el email de verificación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 4-5 | 2 segundos |

| \*\*Frecuencia\*\* | Diaria |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Ninguno. |  |



---



\### CU-001.2 - Iniciar sesión con Google



| UC–001.2 | Iniciar sesión con Google |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–09 Gestión de Seguridad |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | Extiende de CU-001. El usuario se autentica mediante Google Authentication, evitando la necesidad de contraseña propia. Si es un usuario nuevo, se ejecuta CU-011 para crear automáticamente su perfil.  |  |

| \*\*Precondición\*\* | El usuario debe tener una cuenta de Google válida. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario hace clic en el botón "Continuar con Google". |

|  | 2 | El sistema redirige al flujo de autenticación OAuth de Google. |

|  | 3 | El usuario selecciona su cuenta de Google y autoriza el acceso.  |

|  | 4 | Google retorna el token de autenticación al sistema. |

|  | 5 | El sistema verifica si el usuario existe en la base de datos. |

|  | 6a | Si existe, continúa con el paso 5 del CU-001. |

|  | 6b | Si no existe, se ejecuta CU-011 para registrar el nuevo usuario. |

| \*\*Postcondición\*\* | El usuario ha sido autenticado mediante Google.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si el usuario cancela la autenticación de Google, se retorna al formulario de login. |

|  | 4a | Si hay un error en la autenticación de Google, el sistema muestra el mensaje de error correspondiente. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-4 | 3 segundos |

| \*\*Frecuencia\*\* | Diaria |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | El email de Google se considera verificado automáticamente. |  |



---



\### CU-002 - Cerrar sesión



| UC–002 | Cerrar sesión |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–09 Gestión de Seguridad |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | El usuario cierra su sesión de manera manual desde la aplicación. Se invalida la sesión actual y se redirige a la página de login. |  |

| \*\*Precondición\*\* | El usuario debe haber iniciado sesión previamente en el sistema. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario hace clic en el botón o enlace "Cerrar Sesión" del menú. |

|  | 2 | El sistema invalida la sesión actual eliminando las cookies de autenticación. |

|  | 3 | El sistema cierra la sesión en Firebase Authentication. |

|  | 4 | El sistema redirige al usuario a la página de inicio de sesión. |

|  | 5 | El sistema registra el evento de cierre de sesión en auditoría.  |

| \*\*Postcondición\*\* | El usuario ha cerrado sesión exitosamente y no puede acceder a páginas protegidas sin autenticarse nuevamente. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | - | - |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-4 | 1 segundo |

| \*\*Frecuencia\*\* | Diaria |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Ninguno. |  |



---



\### CU-003 - Recuperar contraseña



| UC–003 | Recuperar contraseña |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–09 Gestión de Seguridad |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | El sistema permite al usuario recuperar su contraseña mediante un enlace enviado a su correo electrónico a través de Firebase Authentication. |  |

| \*\*Precondición\*\* | El usuario debe tener una cuenta registrada con correo y contraseña. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario hace clic en "¿Olvidaste tu contraseña?" en la página de login. |

|  | 2 | El sistema muestra un formulario solicitando el correo electrónico.  |

|  | 3 | El usuario ingresa su correo electrónico y hace clic en "Enviar". |

|  | 4 | El sistema solicita a Firebase Authentication el envío del correo de recuperación. |

|  | 5 | Firebase envía un correo con el enlace para restablecer la contraseña. |

|  | 6 | El sistema muestra un mensaje de confirmación indicando que se envió el correo. |

|  | 7 | El usuario accede al enlace del correo y establece una nueva contraseña. |

| \*\*Postcondición\*\* | El usuario ha restablecido su contraseña y puede iniciar sesión con la nueva.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si el correo no está registrado, Firebase no envía el correo pero el sistema muestra el mensaje de éxito por seguridad (no revelar si el correo existe). |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 4-5 | 5 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | El enlace de recuperación es gestionado completamente por Firebase Authentication. |  |



---



\### CU-004 - Cierre de sesión automático por inactividad



| UC–004 | Cierre de sesión automático por inactividad |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–09 Gestión de Seguridad |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados, IRQ–11 Información de Configuración |  |

| \*\*Descripción\*\* | El sistema cierra automáticamente la sesión de los usuarios tras un periodo prolongado de inactividad configurable desde el módulo de configuración. |  |

| \*\*Precondición\*\* | El usuario tiene una sesión activa y no ha realizado acciones durante el tiempo configurado. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El sistema monitorea la actividad del usuario en la aplicación. |

|  | 2 | El sistema detecta que ha transcurrido el tiempo máximo de inactividad configurado. |

|  | 3 | El sistema invalida la sesión del usuario automáticamente. |

|  | 4 | Cuando el usuario intenta realizar una acción, es redirigido a la página de login.  |

|  | 5 | El sistema muestra un mensaje indicando que la sesión expiró por inactividad. |

| \*\*Postcondición\*\* | La sesión del usuario ha sido cerrada y debe autenticarse nuevamente.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | - | - |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3 | Inmediato |

| \*\*Frecuencia\*\* | Variable |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | El tiempo de inactividad es configurable por el administrador en CU-063. |  |



---



\### \*\*Módulo:  Gestión de Empleados\*\*



---



\### CU-005 - Registrarse en el sistema



| UC–005 | Registrarse en el sistema |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–01 Gestión de Empleados, OBJ–09 Gestión de Seguridad |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | Un nuevo usuario crea su perfil en el sistema, quedando registrado como empleado con rol por defecto de 'Empleado'.  Requiere verificación de correo electrónico.  |  |

| \*\*Precondición\*\* | El correo electrónico no debe estar registrado previamente. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la página de registro.  |

|  | 2 | El sistema muestra opciones de registro:  correo/contraseña o Google. |

|  | 3 | El usuario selecciona el método de registro. |

|  | 4 | Se ejecuta el caso de uso correspondiente (CU-005.1 o CU-005.2). |

|  | 5 | El sistema crea el registro del empleado con rol "Empleado" y estado "Activo".  |

|  | 6 | El sistema registra la acción en auditoría. |

|  | 7 | El sistema muestra mensaje de éxito.  |

| \*\*Postcondición\*\* | El nuevo empleado está registrado y puede acceder al sistema tras verificar su email (si aplica). |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si el correo ya existe, el sistema informa el error.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 4-5 | 3 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Ninguno. |  |



---



\### CU-005.1 - Registrarse por correo



| UC–005.1 | Registrarse por correo |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–01 Gestión de Empleados, OBJ–09 Gestión de Seguridad |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | Extiende de CU-005. El usuario completa un formulario con correo y contraseña para generar su cuenta. Se envía un correo de verificación que debe ser confirmado antes de poder iniciar sesión.  |  |

| \*\*Precondición\*\* | Ninguna adicional. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El sistema muestra formulario con campos:  Nombre, Apellido, Correo, Contraseña, Confirmar Contraseña. |

|  | 2 | El usuario completa todos los campos.  |

|  | 3 | El sistema valida que las contraseñas coincidan y cumplan requisitos de seguridad. |

|  | 4 | El sistema crea la cuenta en Firebase Authentication. |

|  | 5 | Firebase envía un correo de verificación al usuario.  |

|  | 6 | El sistema muestra mensaje indicando que debe verificar su correo para iniciar sesión.  |

|  | 7 | Se continúa con el paso 5 del CU-005. |

| \*\*Postcondición\*\* | La cuenta está creada pero requiere verificación de email.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si las contraseñas no coinciden, el sistema muestra el error.  |

|  | 3b | Si la contraseña no cumple requisitos, el sistema indica los requisitos faltantes. |

|  | 4a | Si el correo ya existe en Firebase, el sistema informa el error. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 4-5 | 3 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Ninguno. |  |



---



\### CU-005.2 - Registrarse por Google



| UC–005.2 | Registrarse por Google |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–01 Gestión de Empleados, OBJ–09 Gestión de Seguridad |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | Extiende de CU-005. El registro se realiza mediante autenticación directa con Google, verificando automáticamente el correo electrónico.  |  |

| \*\*Precondición\*\* | El usuario debe tener una cuenta de Google válida. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario hace clic en "Registrarse con Google".  |

|  | 2 | El sistema redirige al flujo de autenticación OAuth de Google. |

|  | 3 | El usuario selecciona su cuenta de Google y autoriza el acceso. |

|  | 4 | Google retorna los datos del usuario (nombre, apellido, correo). |

|  | 5 | El sistema verifica que el correo no esté registrado. |

|  | 6 | Se continúa con el paso 5 del CU-005 usando los datos de Google. |

| \*\*Postcondición\*\* | La cuenta está creada y verificada automáticamente. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si el usuario cancela, se retorna al formulario de registro. |

|  | 5a | Si el correo ya existe, el sistema informa que debe iniciar sesión en lugar de registrarse.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-4 | 3 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | El email de Google se considera verificado automáticamente.  |  |



---



\### CU-006 - Modificar empleado



| UC–006 | Modificar empleado |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–01 Gestión de Empleados |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | El administrador actualiza la información de un empleado existente, incluyendo nombre completo y rol asignado. |  |

| \*\*Precondición\*\* | El usuario actual debe tener rol de Administrador. El empleado debe existir en el sistema. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-009 Consultar empleados. |

|  | 2 | El administrador selecciona el empleado a modificar haciendo clic en el botón de edición. |

|  | 3 | El sistema muestra el formulario de edición con los datos actuales:  Nombre, Apellido, Rol.  |

|  | 4 | El administrador modifica los campos deseados. |

|  | 5 | El administrador hace clic en "Guardar".  |

|  | 6 | El sistema valida los datos ingresados.  |

|  | 7 | El sistema actualiza el registro del empleado en la base de datos.  |

|  | 8 | El sistema muestra un mensaje de éxito. |

|  | 9 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | Los datos del empleado han sido actualizados.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 6a | Si hay errores de validación, el sistema muestra los errores específicos. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 7 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-009 Consultar empleados. |  |



---



\### CU-007 - Desactivar empleado



| UC–007 | Desactivar empleado |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–01 Gestión de Empleados |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | El administrador desactiva a un empleado del sistema. Un empleado desactivado no puede iniciar sesión hasta ser reactivado. |  |

| \*\*Precondición\*\* | El usuario actual debe tener rol de Administrador. El empleado debe existir y estar activo. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-009 Consultar empleados. |

|  | 2 | El administrador selecciona el empleado a desactivar.  |

|  | 3 | El sistema muestra un diálogo de confirmación con mensaje de advertencia. |

|  | 4 | El administrador confirma la desactivación. |

|  | 5 | El sistema cambia el estado del empleado a "Inactivo".  |

|  | 6 | El sistema muestra un mensaje de éxito. |

|  | 7 | El sistema registra la acción en auditoría.  |

| \*\*Postcondición\*\* | El empleado ha sido desactivado y no puede acceder al sistema. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si el administrador cancela, se aborta la operación. |

|  | 5a | Si el empleado es el único administrador activo, el sistema impide la desactivación mostrando mensaje de error. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 5 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-009 Consultar empleados. La desactivación es lógica, no física. |  |



---



\### CU-008 - Reactivar empleado



| UC–008 | Reactivar empleado |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–01 Gestión de Empleados |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | El administrador reactiva a un empleado previamente desactivado, permitiéndole nuevamente iniciar sesión en el sistema. |  |

| \*\*Precondición\*\* | El usuario actual debe tener rol de Administrador. El empleado debe existir y estar inactivo. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-009 Consultar empleados (filtrando por inactivos). |

|  | 2 | El administrador selecciona el empleado a reactivar. |

|  | 3 | El sistema muestra un diálogo de confirmación.  |

|  | 4 | El administrador confirma la reactivación. |

|  | 5 | El sistema cambia el estado del empleado a "Activo". |

|  | 6 | El sistema muestra un mensaje de éxito. |

|  | 7 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El empleado ha sido reactivado y puede acceder nuevamente al sistema. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si el administrador cancela, se aborta la operación.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 5 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-009 Consultar empleados. |  |



---



\### CU-009 - Consultar empleados



| UC–009 | Consultar empleados |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–01 Gestión de Empleados |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | El administrador visualiza la lista de empleados registrados en el sistema con opciones de filtrado por estado y rol, ordenamiento y paginación. |  |

| \*\*Precondición\*\* | El usuario actual debe tener rol de Administrador. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de gestión de empleados desde el menú.  |

|  | 2 | El sistema obtiene la lista de empleados aplicando filtros por defecto (Activos). |

|  | 3 | El sistema muestra la tabla de empleados con columnas: Nombre, Apellido, Correo, Rol, Estado.  |

|  | 4 | El administrador puede aplicar filtros por estado (Activo/Inactivo) y/o rol (Administrador/Empleado). |

|  | 5 | El administrador puede ordenar por cualquier columna haciendo clic en el encabezado.  |

|  | 6 | El administrador puede navegar entre páginas usando los controles de paginación. |

|  | 7 | El sistema actualiza la vista según los criterios seleccionados. |

| \*\*Postcondición\*\* | El administrador visualiza la lista de empleados según los filtros aplicados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si no hay empleados, el sistema muestra un mensaje indicándolo. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-3 | 1 segundo |

| \*\*Frecuencia\*\* | Frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Por defecto muestra solo empleados activos. |  |



---



\### CU-010 - Asignar roles a empleados



| UC–010 | Asignar roles a empleados |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–01 Gestión de Empleados |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | El administrador define los roles de los empleados (Empleado o Administrador) para determinar sus permisos en el sistema. |  |

| \*\*Precondición\*\* | El usuario actual debe tener rol de Administrador. El empleado debe existir en el sistema. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la edición del empleado (incluye CU-006). |

|  | 2 | El sistema muestra el campo de selección de Rol con las opciones disponibles. |

|  | 3 | El administrador selecciona el nuevo rol (Administrador o Empleado). |

|  | 4 | El administrador guarda los cambios.  |

|  | 5 | El sistema valida que quede al menos un administrador activo si se está degradando un administrador. |

|  | 6 | El sistema actualiza el rol del empleado.  |

|  | 7 | El sistema registra la acción en auditoría con detalle del cambio de rol. |

| \*\*Postcondición\*\* | El rol del empleado ha sido actualizado y los permisos se aplican inmediatamente. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si no quedaría ningún administrador activo, el sistema impide el cambio y muestra mensaje de error. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Los roles disponibles son: Administrador y Empleado. Este caso de uso se realiza a través de CU-006. |  |



---



\### CU-011 - Autenticar usuario con Google y registrar perfil si es nuevo



| UC–011 | Autenticar usuario con Google y registrar perfil si es nuevo |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–01 Gestión de Empleados, OBJ–09 Gestión de Seguridad |  |

| \*\*Requisitos asociados\*\* | IRQ–01 Información sobre Empleados |  |

| \*\*Descripción\*\* | El sistema valida un inicio de sesión por Google y, si no existe el usuario, lo registra automáticamente con los datos del perfil de Google y rol por defecto "Empleado". |  |

| \*\*Precondición\*\* | El usuario ha completado la autenticación de Google exitosamente (desde CU-001. 2). |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El sistema recibe los datos del perfil de Google (UID, nombre, apellido, correo). |

|  | 2 | El sistema busca en la base de datos un empleado con el UID de Google.  |

|  | 3 | Si no existe, el sistema crea un nuevo registro de empleado. |

|  | 4 | El sistema asigna los datos del perfil de Google:  Nombre, Apellido, Correo.  |

|  | 5 | El sistema asigna el rol por defecto "Empleado" y estado "Activo".  |

|  | 6 | El sistema establece EmailVerificado como verdadero. |

|  | 7 | El sistema guarda el nuevo empleado en la base de datos. |

|  | 8 | El sistema registra la acción en auditoría como "Registro automático por Google". |

| \*\*Postcondición\*\* | El usuario existe en el sistema y puede continuar con el inicio de sesión. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si el usuario ya existe, el sistema simplemente continúa con el flujo de login. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3-7 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Este caso de uso es ejecutado automáticamente por el sistema. |  |



---



\### \*\*Módulo:  Gestión de Clientes y Vehículos\*\*



---



\### CU-012 - Crear cliente



| UC–012 | Crear cliente |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes |  |

| \*\*Descripción\*\* | El personal del lavadero registra un nuevo cliente en el sistema con sus datos personales:  tipo de documento, número de documento, nombre, apellido, teléfono y correo electrónico.  |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado con rol de Trabajador o Administrador. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la sección de gestión de clientes desde el menú. |

|  | 2 | El usuario hace clic en el botón "Nuevo Cliente".  |

|  | 3 | El sistema muestra un formulario con campos: Tipo Documento, Número Documento, Nombre, Apellido, Teléfono, Email. |

|  | 4 | El sistema carga dinámicamente los tipos de documento disponibles. |

|  | 5 | El usuario selecciona el tipo de documento. |

|  | 6 | El sistema actualiza la validación del número de documento según el formato del tipo seleccionado. |

|  | 7 | El usuario completa todos los campos del formulario. |

|  | 8 | El usuario hace clic en "Guardar". |

|  | 9 | El sistema valida los datos (formato de documento, email válido si se ingresa, teléfono único). |

|  | 10 | El sistema crea el cliente con estado "Activo".  |

|  | 11 | El sistema muestra un mensaje de éxito. |

|  | 12 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El cliente está registrado en el sistema.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 9a | Si el número de documento ya existe para ese tipo, el sistema informa el error. |

|  | 9b | Si el teléfono ya está registrado, el sistema informa el error. |

|  | 9c | Si el formato del documento no es válido, el sistema muestra el error. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 10 | 1 segundo |

| \*\*Frecuencia\*\* | Frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Los vehículos se asocian al cliente mediante casos de uso separados (CU-023). |  |



---



\### CU-013 - Modificar cliente



| UC–013 | Modificar cliente |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes |  |

| \*\*Descripción\*\* | El personal del lavadero actualiza la información de un cliente existente.  |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  El cliente debe existir en el sistema. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-016 Consultar clientes. |

|  | 2 | El usuario selecciona el cliente a modificar haciendo clic en el botón de edición. |

|  | 3 | El sistema muestra el formulario de edición con los datos actuales.  |

|  | 4 | El usuario modifica los campos deseados (Nombre, Apellido, Teléfono, Email). |

|  | 5 | El usuario hace clic en "Guardar".  |

|  | 6 | El sistema valida los datos modificados. |

|  | 7 | El sistema actualiza el registro del cliente en la base de datos.  |

|  | 8 | El sistema muestra un mensaje de éxito. |

|  | 9 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | Los datos del cliente han sido actualizados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 6a | Si hay errores de validación, el sistema muestra los errores específicos. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 7 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-016 Consultar clientes. El tipo y número de documento no son editables. |  |



---



\### CU-014 - Desactivar cliente



| UC–014 | Desactivar cliente |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos |  |

| \*\*Descripción\*\* | El administrador desactiva un cliente del sistema. La desactivación es lógica y también desactiva en cascada los vehículos asociados exclusivamente a ese cliente. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. El cliente debe existir y estar activo. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-016 Consultar clientes. |

|  | 2 | El administrador selecciona el cliente a desactivar.  |

|  | 3 | El sistema verifica si el cliente tiene vehículos asociados exclusivamente. |

|  | 4 | El sistema muestra un diálogo de confirmación con advertencia sobre vehículos que serán desactivados. |

|  | 5 | El administrador confirma la desactivación. |

|  | 6 | El sistema cambia el estado del cliente a "Inactivo". |

|  | 7 | El sistema desactiva en cascada los vehículos que solo tenían a este cliente como dueño. |

|  | 8 | El sistema muestra un mensaje de éxito. |

|  | 9 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El cliente y sus vehículos exclusivos han sido desactivados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si el administrador cancela, se aborta la operación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6-7 | 2 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-016 Consultar clientes. Los vehículos compartidos con otros clientes no se desactivan.  |  |



---



| UC–015 | Reactivar cliente |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos |  |

| \*\*Descripción\*\* | El administrador reactiva un cliente previamente desactivado. La reactivación también reactiva en cascada los vehículos que fueron desactivados junto con el cliente.  |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. El cliente debe existir y estar inactivo. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-016 Consultar clientes (filtrando por inactivos). |

|  | 2 | El administrador selecciona el cliente a reactivar. |

|  | 3 | El sistema muestra un diálogo de confirmación indicando los vehículos que serán reactivados. |

|  | 4 | El administrador confirma la reactivación.  |

|  | 5 | El sistema cambia el estado del cliente a "Activo". |

|  | 6 | El sistema reactiva en cascada los vehículos que fueron desactivados junto con el cliente. |

|  | 7 | El sistema muestra un mensaje de éxito. |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El cliente y sus vehículos han sido reactivados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si el administrador cancela, se aborta la operación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 5-6 | 2 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-016 Consultar clientes. |  |



---



\### CU-016 - Consultar clientes



| UC–016 | Consultar clientes |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes |  |

| \*\*Descripción\*\* | El personal consulta la lista de clientes registrados en el sistema con opciones de filtrado por estado, ordenamiento y paginación. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la sección de gestión de clientes desde el menú.  |

|  | 2 | El sistema obtiene la lista de clientes aplicando filtros por defecto (Activos). |

|  | 3 | El sistema muestra la tabla de clientes con columnas:  Nombre Completo, Documento, Teléfono, Email, Estado. |

|  | 4 | El usuario puede aplicar filtros por estado (Activo/Inactivo). |

|  | 5 | El usuario puede ordenar por cualquier columna haciendo clic en el encabezado.  |

|  | 6 | El usuario puede navegar entre páginas usando los controles de paginación. |

|  | 7 | El sistema actualiza la vista según los criterios seleccionados. |

| \*\*Postcondición\*\* | El usuario visualiza la lista de clientes según los filtros aplicados.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si no hay clientes, el sistema muestra un mensaje indicándolo. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-3 | 1 segundo |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Por defecto muestra solo clientes activos. |  |



---



\### CU-017 - Buscar clientes



| UC–017 | Buscar clientes |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes |  |

| \*\*Descripción\*\* | El personal busca clientes por nombre, apellido, documento, teléfono o email. La búsqueda se realiza en tiempo real con resultados paginados. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la sección de gestión de clientes.  |

|  | 2 | El usuario ingresa texto en el campo de búsqueda. |

|  | 3 | El sistema busca coincidencias en:  nombre, apellido, nombre completo, número de documento, teléfono y email. |

|  | 4 | El sistema muestra los resultados filtrados en tiempo real. |

|  | 5 | El usuario puede seleccionar un cliente de los resultados. |

| \*\*Postcondición\*\* | El usuario visualiza los clientes que coinciden con el criterio de búsqueda. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si no hay coincidencias, el sistema muestra mensaje "Sin resultados".  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3-4 | 500ms |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | La búsqueda es insensible a mayúsculas/minúsculas.  |  |



---



\### CU-018 - Crear vehículo



| UC–018 | Crear vehículo |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |  |

| \*\*Requisitos asociados\*\* | IRQ–03 Información sobre Vehículos, IRQ–06 Información sobre Tipos de Vehículo |  |

| \*\*Descripción\*\* | El personal registra un nuevo vehículo con patente, tipo de vehículo, marca, modelo y color.  Opcionalmente puede asociarlo a un cliente existente. Se genera una clave de asociación para permitir que otros clientes vinculen el vehículo. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  Deben existir tipos de vehículo activos. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la sección de gestión de vehículos o a la edición de un cliente. |

|  | 2 | El usuario hace clic en "Nuevo Vehículo".  |

|  | 3 | El sistema muestra un formulario con campos: Patente, Tipo Vehículo, Marca, Modelo, Color.  |

|  | 4 | El sistema carga los tipos de vehículo activos disponibles. |

|  | 5 | El usuario selecciona el tipo de vehículo. |

|  | 6 | El sistema ofrece autocompletado de marcas desde la API CarQuery. |

|  | 7 | El usuario selecciona o ingresa la marca. |

|  | 8 | El sistema ofrece autocompletado de modelos según la marca seleccionada. |

|  | 9 | El usuario completa todos los campos del formulario. |

|  | 10 | El usuario hace clic en "Guardar". |

|  | 11 | El sistema valida que la patente no esté registrada para otro vehículo activo. |

|  | 12 | El sistema genera una clave de asociación aleatoria y la almacena como hash SHA256. |

|  | 13 | El sistema crea el vehículo con estado "Activo".  |

|  | 14 | El sistema muestra un mensaje de éxito con la clave de asociación generada. |

|  | 15 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El vehículo está registrado en el sistema con su clave de asociación.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 11a | Si la patente ya existe para un vehículo activo, el sistema informa el error y sugiere vincular el vehículo existente. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 12-13 | 1 segundo |

| \*\*Frecuencia\*\* | Frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | La clave de asociación debe mostrarse al usuario para que pueda compartirla con otros dueños del vehículo.  |  |



---



\### CU-019 - Modificar vehículo



| UC–019 | Modificar vehículo |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |  |

| \*\*Requisitos asociados\*\* | IRQ–03 Información sobre Vehículos |  |

| \*\*Descripción\*\* | El personal actualiza los datos editables de un vehículo (modelo y color). La patente, tipo de vehículo y marca no son editables una vez registrado el vehículo.  |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  El vehículo debe existir en el sistema. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-021 Consultar vehículos. |

|  | 2 | El usuario selecciona el vehículo a modificar haciendo clic en el botón de edición. |

|  | 3 | El sistema muestra el formulario de edición con los datos actuales.  |

|  | 4 | El sistema muestra los campos no editables (Patente, Tipo, Marca) como solo lectura. |

|  | 5 | El usuario modifica los campos editables (Modelo, Color). |

|  | 6 | El usuario hace clic en "Guardar".  |

|  | 7 | El sistema valida los datos modificados. |

|  | 8 | El sistema actualiza el registro del vehículo en la base de datos. |

|  | 9 | El sistema muestra un mensaje de éxito. |

|  | 10 | El sistema registra la acción en auditoría.  |

| \*\*Postcondición\*\* | Los datos editables del vehículo han sido actualizados.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 7a | Si hay errores de validación, el sistema muestra los errores específicos. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 8 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-021 Consultar vehículos.  |  |



---



\### CU-020 - Desactivar vehículo



| UC–020 | Desactivar vehículo |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |  |

| \*\*Requisitos asociados\*\* | IRQ–03 Información sobre Vehículos |  |

| \*\*Descripción\*\* | El administrador desactiva un vehículo del sistema. Solo se puede desactivar si no está asignado a ningún cliente activo. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. El vehículo debe existir y estar activo. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-021 Consultar vehículos. |

|  | 2 | El administrador selecciona el vehículo a desactivar.  |

|  | 3 | El sistema verifica que el vehículo no tenga clientes activos asociados. |

|  | 4 | El sistema muestra un diálogo de confirmación.  |

|  | 5 | El administrador confirma la desactivación. |

|  | 6 | El sistema cambia el estado del vehículo a "Inactivo". |

|  | 7 | El sistema muestra un mensaje de éxito. |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El vehículo ha sido desactivado.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si el vehículo tiene clientes activos asociados, el sistema muestra error indicando que primero debe desvincularse.  |

|  | 5a | Si el administrador cancela, se aborta la operación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-021 Consultar vehículos.  |  |



---



\### CU-021 - Consultar vehículos



| UC–021 | Consultar vehículos |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |  |

| \*\*Requisitos asociados\*\* | IRQ–03 Información sobre Vehículos |  |

| \*\*Descripción\*\* | El personal visualiza la lista de vehículos registrados con opciones de filtrado por tipo de vehículo, marca, color y estado. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la sección de gestión de vehículos desde el menú. |

|  | 2 | El sistema obtiene la lista de vehículos aplicando filtros por defecto (Activos). |

|  | 3 | El sistema muestra la tabla de vehículos con columnas:  Patente, Tipo, Marca, Modelo, Color, Dueño Principal, Estado. |

|  | 4 | El usuario puede aplicar filtros por:  estado, tipo de vehículo, marca, color. |

|  | 5 | El usuario puede ordenar por cualquier columna haciendo clic en el encabezado.  |

|  | 6 | El usuario puede navegar entre páginas usando los controles de paginación. |

|  | 7 | El sistema actualiza la vista según los criterios seleccionados.  |

| \*\*Postcondición\*\* | El usuario visualiza la lista de vehículos según los filtros aplicados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si no hay vehículos, el sistema muestra un mensaje indicándolo. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-3 | 1 segundo |

| \*\*Frecuencia\*\* | Frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Por defecto muestra solo vehículos activos. |  |



---



\### CU-022 - Buscar vehículos



| UC–022 | Buscar vehículos |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |  |

| \*\*Requisitos asociados\*\* | IRQ–03 Información sobre Vehículos |  |

| \*\*Descripción\*\* | El personal busca vehículos por patente, marca o modelo. La búsqueda se realiza en tiempo real.  |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la sección de gestión de vehículos.  |

|  | 2 | El usuario ingresa texto en el campo de búsqueda. |

|  | 3 | El sistema busca coincidencias en:  patente, marca, modelo.  |

|  | 4 | El sistema muestra los resultados filtrados en tiempo real. |

|  | 5 | El usuario puede seleccionar un vehículo de los resultados. |

| \*\*Postcondición\*\* | El usuario visualiza los vehículos que coinciden con el criterio de búsqueda. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si no hay coincidencias, el sistema muestra mensaje "Sin resultados". |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3-4 | 500ms |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | La búsqueda es insensible a mayúsculas/minúsculas. |  |



---



\### CU-023 - Vincular vehículo a cliente



| UC–023 | Vincular vehículo a cliente |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos |  |

| \*\*Descripción\*\* | El personal asocia un vehículo existente a un cliente.  Un vehículo puede estar asociado a múltiples clientes (dueños compartidos). |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. El cliente y el vehículo deben existir y estar activos. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-021 Consultar vehículos o se accede desde la edición de un cliente.  |

|  | 2 | El usuario selecciona la opción "Vincular a cliente". |

|  | 3 | El sistema solicita la patente del vehículo (si no está preseleccionado). |

|  | 4 | El sistema busca el vehículo por patente.  |

|  | 5 | El sistema solicita la clave de asociación.  |

|  | 6 | El usuario ingresa la clave de asociación. |

|  | 7 | El sistema valida la clave contra el hash SHA256 almacenado. |

|  | 8 | El sistema agrega al cliente a la lista de ClientesIds del vehículo.  |

|  | 9 | El sistema agrega el vehículo a la lista de VehiculosIds del cliente.  |

|  | 10 | El sistema muestra un mensaje de éxito.  |

|  | 11 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El cliente está asociado al vehículo. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si no se encuentra el vehículo, el sistema informa el error.  |

|  | 7a | Si la clave es incorrecta, el sistema informa el error.  |

|  | 7b | Si el cliente ya está asociado al vehículo, el sistema lo informa. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 7-9 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-021 Consultar vehículos.  Permite que múltiples clientes compartan un vehículo.  |  |



---



\### CU-024 - Desvincular vehículo de cliente



| UC–024 | Desvincular vehículo de cliente |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–02 Gestión de Clientes y Vehículos |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos |  |

| \*\*Descripción\*\* | El personal desvincula un vehículo de un cliente. Si el vehículo queda sin clientes asociados, se desactiva automáticamente.  |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  El cliente debe tener el vehículo asociado. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-021 Consultar vehículos o se accede desde la edición de un cliente. |

|  | 2 | El usuario selecciona la opción "Desvincular" en el vehículo correspondiente. |

|  | 3 | El sistema verifica cuántos clientes tiene asociado el vehículo. |

|  | 4 | El sistema muestra un diálogo de confirmación (indicando si el vehículo será desactivado). |

|  | 5 | El usuario confirma la desvinculación. |

|  | 6 | El sistema remueve al cliente de la lista de ClientesIds del vehículo.  |

|  | 7 | El sistema remueve el vehículo de la lista de VehiculosIds del cliente. |

|  | 8 | Si el vehículo queda sin clientes, el sistema lo desactiva automáticamente.  |

|  | 9 | El sistema muestra un mensaje de éxito. |

|  | 10 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El cliente ya no está asociado al vehículo.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si el usuario cancela, se aborta la operación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6-8 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-021 Consultar vehículos.  |  |



---



\### CU-025 - Registrarse como cliente por WhatsApp



| UC–025 | Registrarse como cliente por WhatsApp |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–10 Integración con WhatsApp |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes, IRQ–12 Información de Sesiones WhatsApp |  |

| \*\*Descripción\*\* | El cliente se registra en el sistema mediante interacción guiada con el bot de WhatsApp, proporcionando sus datos personales paso a paso. |  |

| \*\*Precondición\*\* | El número de WhatsApp no debe estar registrado previamente como cliente. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El cliente envía un mensaje al número de WhatsApp del lavadero. |

|  | 2 | El sistema ejecuta CU-027 para verificar si el número está registrado. |

|  | 3 | Al no estar registrado, el sistema muestra un menú de bienvenida con opciones.  |

|  | 4 | El cliente selecciona "Registrarme".  |

|  | 5 | El sistema solicita el tipo de documento (muestra opciones numeradas). |

|  | 6 | El cliente selecciona el tipo de documento. |

|  | 7 | El sistema solicita el número de documento.  |

|  | 8 | El cliente ingresa su número de documento. |

|  | 9 | El sistema valida el formato según el tipo de documento seleccionado. |

|  | 10 | El sistema solicita el nombre.  |

|  | 11 | El cliente ingresa su nombre.  |

|  | 12 | El sistema solicita el apellido. |

|  | 13 | El cliente ingresa su apellido. |

|  | 14 | El sistema solicita el correo electrónico (opcional). |

|  | 15 | El cliente ingresa su email o escribe "omitir".  |

|  | 16 | El sistema muestra un resumen de los datos y solicita confirmación. |

|  | 17 | El cliente confirma los datos. |

|  | 18 | El sistema crea el cliente con el número de teléfono de WhatsApp. |

|  | 19 | El sistema envía mensaje de bienvenida y muestra el menú principal. |

|  | 20 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El cliente está registrado y puede acceder a las funcionalidades de WhatsApp. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si el número ya está registrado, el sistema muestra el menú principal directamente. |

|  | 9a | Si el formato del documento es inválido, el sistema solicita ingresarlo nuevamente. |

|  | 17a | Si el cliente no confirma, el sistema permite corregir los datos.  |

|  | \* | En cualquier momento, el cliente puede escribir "cancelar" para abortar el proceso. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 18 | 2 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | La conversación se mantiene mediante sesiones temporales en Firestore. |  |



---



\### CU-026 - Registrar vehículo por WhatsApp



| UC–026 | Registrar vehículo por WhatsApp |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–10 Integración con WhatsApp |  |

| \*\*Requisitos asociados\*\* | IRQ–03 Información sobre Vehículos, IRQ–12 Información de Sesiones WhatsApp |  |

| \*\*Descripción\*\* | El cliente asocia un nuevo vehículo a su cuenta mediante el flujo conversacional de WhatsApp, indicando tipo de vehículo, patente, marca, modelo y color. |  |

| \*\*Precondición\*\* | El cliente debe estar registrado en el sistema (número de WhatsApp vinculado). |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El cliente accede al menú principal de WhatsApp. |

|  | 2 | El cliente selecciona "Mis Vehículos".  |

|  | 3 | El sistema muestra el submenú de vehículos.  |

|  | 4 | El cliente selecciona "Agregar vehículo". |

|  | 5 | El sistema solicita el tipo de vehículo (muestra opciones numeradas). |

|  | 6 | El cliente selecciona el tipo de vehículo. |

|  | 7 | El sistema solicita la patente del vehículo.  |

|  | 8 | El cliente ingresa la patente.  |

|  | 9 | El sistema valida que la patente no esté registrada para otro cliente. |

|  | 10 | El sistema solicita la marca del vehículo. |

|  | 11 | El cliente ingresa la marca. |

|  | 12 | El sistema solicita el modelo del vehículo. |

|  | 13 | El cliente ingresa el modelo.  |

|  | 14 | El sistema solicita el color del vehículo. |

|  | 15 | El cliente ingresa el color. |

|  | 16 | El sistema muestra un resumen y solicita confirmación.  |

|  | 17 | El cliente confirma los datos. |

|  | 18 | El sistema crea el vehículo y lo asocia al cliente. |

|  | 19 | El sistema genera la clave de asociación y la envía al cliente. |

|  | 20 | El sistema confirma el registro exitoso. |

|  | 21 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El vehículo está registrado y asociado al cliente. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 9a | Si la patente ya existe, el sistema ofrece asociarse al vehículo existente mediante clave.  |

|  | 17a | Si el cliente no confirma, permite corregir los datos. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 18-19 | 2 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | La clave de asociación permite que otros clientes se vinculen al mismo vehículo.  |  |



---



\### CU-027 - Identificar si el número de teléfono está registrado



| UC–027 | Identificar si el número de teléfono está registrado |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–10 Integración con WhatsApp |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes, IRQ–12 Información de Sesiones WhatsApp |  |

| \*\*Descripción\*\* | El sistema valida si el número de WhatsApp que envía un mensaje pertenece a un cliente existente, determinando el flujo de conversación apropiado. |  |

| \*\*Precondición\*\* | Se ha recibido un mensaje de WhatsApp.  |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El sistema recibe un mensaje de WhatsApp con el número del remitente. |

|  | 2 | El sistema busca en la base de datos un cliente con ese número de teléfono. |

|  | 3a | Si existe y está activo, el sistema carga/actualiza la sesión con el ClienteId. |

|  | 3b | Si no existe o está inactivo, el sistema crea una sesión sin ClienteId. |

|  | 4 | El sistema retorna el estado de autenticación al flujo de procesamiento de mensajes. |

| \*\*Postcondición\*\* | El sistema conoce si el número corresponde a un cliente registrado. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | - | - |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-3 | 500ms |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Este caso de uso es ejecutado automáticamente por el sistema en cada mensaje recibido. |  |



---



\### CU-028 - Editar datos personales por WhatsApp



| UC–028 | Editar datos personales por WhatsApp |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–10 Integración con WhatsApp |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes, IRQ–12 Información de Sesiones WhatsApp |  |

| \*\*Descripción\*\* | El cliente modifica sus datos personales (nombre, apellido, email) a través del flujo conversacional de WhatsApp.  |  |

| \*\*Precondición\*\* | El cliente debe estar registrado en el sistema. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El cliente accede al menú principal de WhatsApp. |

|  | 2 | El cliente selecciona "Mis Datos". |

|  | 3 | El sistema muestra los datos actuales del cliente. |

|  | 4 | El sistema muestra opciones de modificación numeradas. |

|  | 5 | El cliente selecciona el dato a modificar (nombre, apellido, email). |

|  | 6 | El sistema solicita el nuevo valor. |

|  | 7 | El cliente ingresa el nuevo valor.  |

|  | 8 | El sistema valida el dato ingresado. |

|  | 9 | El sistema actualiza el registro del cliente. |

|  | 10 | El sistema confirma la modificación exitosa. |

|  | 11 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | Los datos del cliente han sido actualizados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 8a | Si el dato es inválido, el sistema solicita ingresarlo nuevamente.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 9 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | El número de teléfono, tipo y número de documento no se pueden modificar por seguridad. |  |



---



\### \*\*Módulo:  Gestión de Servicios\*\*



---



\### CU-029 - Crear servicio



| UC–029 | Crear servicio |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–04 Información sobre Servicios, IRQ–05 Información sobre Tipos de Servicio, IRQ–06 Información sobre Tipos de Vehículo |  |

| \*\*Descripción\*\* | El administrador crea un nuevo servicio para el lavadero con nombre, descripción, precio, tiempo estimado, tipo de servicio, tipo de vehículo y opcionalmente etapas de ejecución. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador.  Deben existir tipos de servicio y tipos de vehículo activos. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de gestión de servicios desde el menú.  |

|  | 2 | El administrador hace clic en "Nuevo Servicio". |

|  | 3 | El sistema muestra un formulario con campos:  Nombre, Descripción, Tipo de Servicio, Tipo de Vehículo, Precio, Tiempo Estimado.  |

|  | 4 | El sistema carga los tipos de servicio y tipos de vehículo activos. |

|  | 5 | El administrador completa todos los campos obligatorios. |

|  | 6 | Opcionalmente, el administrador agrega etapas al servicio haciendo clic en "Agregar Etapa".  |

|  | 7 | Para cada etapa, el administrador ingresa:  Nombre, Descripción, Orden.  |

|  | 8 | El administrador hace clic en "Guardar".  |

|  | 9 | El sistema valida los datos (nombre único por tipo de vehículo, precio >= 0, tiempo > 0). |

|  | 10 | El sistema crea el servicio con estado "Activo".  |

|  | 11 | El sistema muestra un mensaje de éxito. |

|  | 12 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El servicio está registrado y disponible para ser utilizado en lavados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 9a | Si ya existe un servicio con el mismo nombre para ese tipo de vehículo, el sistema informa el error.  |

|  | 9b | Si hay errores de validación, el sistema muestra los errores específicos. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 10 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Las etapas permiten dividir el servicio en pasos que se pueden marcar como completados durante el lavado. |  |



---



\### CU-030 - Modificar servicio



| UC–030 | Modificar servicio |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–04 Información sobre Servicios |  |

| \*\*Descripción\*\* | El administrador actualiza los detalles de un servicio existente, incluyendo nombre, descripción, precio, tiempo estimado y etapas.  |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. El servicio debe existir en el sistema. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-033 Consultar servicios. |

|  | 2 | El administrador selecciona el servicio a modificar. |

|  | 3 | El sistema muestra el formulario de edición con los datos actuales.  |

|  | 4 | El administrador modifica los campos deseados. |

|  | 5 | El administrador puede agregar, modificar o eliminar etapas. |

|  | 6 | El administrador hace clic en "Guardar". |

|  | 7 | El sistema valida los datos.  |

|  | 8 | El sistema actualiza el registro del servicio. |

|  | 9 | El sistema actualiza los paquetes que contienen este servicio (recalcula precios y tiempos). |

|  | 10 | El sistema muestra un mensaje de éxito. |

|  | 11 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | Los datos del servicio han sido actualizados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 7a | Si hay errores de validación, el sistema muestra los errores específicos. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 8-9 | 2 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-033 Consultar servicios. La modificación de un servicio afecta a los paquetes que lo contienen. |  |



---



\### CU-031 - Desactivar servicio



| UC–031 | Desactivar servicio |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–04 Información sobre Servicios |  |

| \*\*Descripción\*\* | El administrador desactiva un servicio del sistema. Los servicios desactivados no aparecen disponibles para seleccionar en nuevos lavados. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. El servicio debe existir y estar activo. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-033 Consultar servicios. |

|  | 2 | El administrador selecciona el servicio a desactivar.  |

|  | 3 | El sistema verifica si el servicio está incluido en algún paquete activo. |

|  | 4 | El sistema muestra un diálogo de confirmación (con advertencia si afecta paquetes). |

|  | 5 | El administrador confirma la desactivación. |

|  | 6 | El sistema cambia el estado del servicio a "Inactivo". |

|  | 7 | El sistema muestra un mensaje de éxito. |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El servicio ha sido desactivado.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si el servicio está en un paquete activo, el sistema advierte que afectará al paquete. |

|  | 5a | Si el administrador cancela, se aborta la operación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-033 Consultar servicios. |  |



---



\### CU-032 - Reactivar servicio



| UC–032 | Reactivar servicio |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–04 Información sobre Servicios |  |

| \*\*Descripción\*\* | El administrador reactiva un servicio previamente desactivado, volviéndolo disponible para selección. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. El servicio debe existir y estar inactivo. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-033 Consultar servicios (filtrando por inactivos). |

|  | 2 | El administrador selecciona el servicio a reactivar. |

|  | 3 | El sistema muestra un diálogo de confirmación. |

|  | 4 | El administrador confirma la reactivación. |

|  | 5 | El sistema cambia el estado del servicio a "Activo". |

|  | 6 | El sistema muestra un mensaje de éxito. |

|  | 7 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El servicio ha sido reactivado y está disponible para su uso. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si el administrador cancela, se aborta la operación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 5 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-033 Consultar servicios. |  |



---



\### CU-033 - Consultar servicios



| UC–033 | Consultar servicios |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–04 Información sobre Servicios |  |

| \*\*Descripción\*\* | El personal consulta la lista de servicios disponibles con opciones de filtrado por estado, tipo de servicio, tipo de vehículo y rango de precios.  |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la sección de gestión de servicios desde el menú. |

|  | 2 | El sistema obtiene la lista de servicios aplicando filtros por defecto (Activos). |

|  | 3 | El sistema muestra la tabla de servicios con columnas: Nombre, Tipo Servicio, Tipo Vehículo, Precio, Tiempo Estimado, Estado. |

|  | 4 | El usuario puede aplicar filtros por:  estado, tipo de servicio, tipo de vehículo, rango de precios. |

|  | 5 | El usuario puede ordenar por cualquier columna. |

|  | 6 | El usuario puede navegar entre páginas usando los controles de paginación. |

|  | 7 | El sistema actualiza la vista según los criterios seleccionados. |

| \*\*Postcondición\*\* | El usuario visualiza la lista de servicios según los filtros aplicados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si no hay servicios, el sistema muestra un mensaje indicándolo. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-3 | 1 segundo |

| \*\*Frecuencia\*\* | Frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Por defecto muestra solo servicios activos. |  |



---



\### CU-034 - Buscar servicios



| UC–034 | Buscar servicios |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–04 Información sobre Servicios |  |

| \*\*Descripción\*\* | El personal busca servicios por nombre.  La búsqueda se realiza en tiempo real con resultados paginados. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la sección de gestión de servicios.  |

|  | 2 | El usuario ingresa texto en el campo de búsqueda. |

|  | 3 | El sistema busca coincidencias en el nombre del servicio.  |

|  | 4 | El sistema muestra los resultados filtrados en tiempo real. |

|  | 5 | El usuario puede seleccionar un servicio de los resultados. |

| \*\*Postcondición\*\* | El usuario visualiza los servicios que coinciden con el criterio de búsqueda. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si no hay coincidencias, el sistema muestra mensaje "Sin resultados". |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3-4 | 500ms |

| \*\*Frecuencia\*\* | Frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | La búsqueda es insensible a mayúsculas/minúsculas.  |  |



---



\### CU-035 - Crear tipo de servicio



| UC–035 | Crear tipo de servicio |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–05 Información sobre Tipos de Servicio |  |

| \*\*Descripción\*\* | El administrador crea un nuevo tipo de servicio (categoría) para clasificar los servicios del lavadero. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de configuración de tipos de servicio. |

|  | 2 | El administrador hace clic en "Nuevo Tipo de Servicio". |

|  | 3 | El sistema muestra un formulario con campo: Nombre.  |

|  | 4 | El administrador ingresa el nombre del tipo de servicio. |

|  | 5 | El administrador hace clic en "Guardar". |

|  | 6 | El sistema valida que el nombre no exista previamente. |

|  | 7 | El sistema crea el tipo de servicio con estado "Activo". |

|  | 8 | El sistema muestra un mensaje de éxito. |

|  | 9 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El tipo de servicio está disponible para asignar a servicios.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 6a | Si el nombre ya existe, el sistema informa el error.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 7 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Los tipos de servicio permiten limitar a un servicio por tipo en los paquetes. |  |



---



\### CU-036 - Eliminar tipo de servicio



| UC–036 | Eliminar tipo de servicio |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–05 Información sobre Tipos de Servicio |  |

| \*\*Descripción\*\* | El administrador elimina un tipo de servicio.  Solo se permite si no hay servicios activos utilizando ese tipo. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador.  El tipo de servicio debe existir.  |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de configuración de tipos de servicio. |

|  | 2 | El administrador selecciona el tipo de servicio a eliminar. |

|  | 3 | El sistema verifica que no haya servicios activos utilizando ese tipo. |

|  | 4 | El sistema muestra un diálogo de confirmación.  |

|  | 5 | El administrador confirma la eliminación. |

|  | 6 | El sistema elimina el tipo de servicio (eliminación física). |

|  | 7 | El sistema muestra un mensaje de éxito.  |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El tipo de servicio ha sido eliminado del sistema. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si hay servicios utilizando ese tipo, el sistema impide la eliminación y muestra error. |

|  | 5a | Si el administrador cancela, se aborta la operación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6 | 1 segundo |

| \*\*Frecuencia\*\* | Rara |  |

| \*\*Estabilidad\*\* | Baja |  |

| \*\*Comentarios\*\* | La eliminación es física, no lógica. |  |



---



\### CU-037 - Crear tipo de vehículo



| UC–037 | Crear tipo de vehículo |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–06 Información sobre Tipos de Vehículo |  |

| \*\*Descripción\*\* | El administrador crea un nuevo tipo de vehículo con nombre, formato de patente y cantidad de empleados requeridos por defecto. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador.  |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de configuración de tipos de vehículo. |

|  | 2 | El administrador hace clic en "Nuevo Tipo de Vehículo".  |

|  | 3 | El sistema muestra un formulario con campos: Nombre, Formato Patente, Empleados Requeridos. |

|  | 4 | El administrador completa los campos.  |

|  | 5 | El administrador hace clic en "Guardar". |

|  | 6 | El sistema valida que el nombre no exista previamente y empleados >= 1. |

|  | 7 | El sistema crea el tipo de vehículo con estado "Activo". |

|  | 8 | El sistema muestra un mensaje de éxito. |

|  | 9 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El tipo de vehículo está disponible para asignar a vehículos y servicios. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 6a | Si el nombre ya existe, el sistema informa el error. |

|  | 6b | Si hay errores de validación, el sistema muestra los errores específicos.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 7 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Los tipos de vehículo determinan qué servicios están disponibles. |  |



---



\### CU-038 - Eliminar tipo de vehículo



| UC–038 | Eliminar tipo de vehículo |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–06 Información sobre Tipos de Vehículo |  |

| \*\*Descripción\*\* | El administrador elimina un tipo de vehículo. Solo se permite si no hay servicios ni vehículos activos utilizando ese tipo.  |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. El tipo de vehículo debe existir. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de configuración de tipos de vehículo. |

|  | 2 | El administrador selecciona el tipo de vehículo a eliminar. |

|  | 3 | El sistema verifica que no haya servicios ni vehículos activos utilizando ese tipo. |

|  | 4 | El sistema muestra un diálogo de confirmación. |

|  | 5 | El administrador confirma la eliminación. |

|  | 6 | El sistema elimina el tipo de vehículo (eliminación física). |

|  | 7 | El sistema muestra un mensaje de éxito. |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El tipo de vehículo ha sido eliminado del sistema. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si hay servicios o vehículos utilizando ese tipo, el sistema impide la eliminación.  |

|  | 5a | Si el administrador cancela, se aborta la operación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6 | 1 segundo |

| \*\*Frecuencia\*\* | Rara |  |

| \*\*Estabilidad\*\* | Baja |  |

| \*\*Comentarios\*\* | La eliminación es física, no lógica. |  |



---



\### CU-039 - Gestionar etapas del servicio



| UC–039 | Gestionar etapas del servicio |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–04 Información sobre Servicios |  |

| \*\*Descripción\*\* | El administrador define las etapas o fases en las que se divide un servicio para su ejecución, permitiendo un seguimiento granular del progreso. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. El servicio debe existir. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la edición del servicio (dentro de CU-029 o CU-030). |

|  | 2 | El sistema muestra la sección de etapas con las etapas actuales (si existen). |

|  | 3 | El administrador puede:  |

|  | 3a | Agregar nueva etapa:  clic en "Agregar Etapa", ingresa Nombre, Descripción, Orden. |

|  | 3b | Modificar etapa existente: edita los campos de la etapa. |

|  | 3c | Eliminar etapa: clic en botón eliminar de la etapa. |

|  | 3d | Reordenar etapas: arrastra y suelta para cambiar el orden. |

|  | 4 | El administrador guarda los cambios del servicio. |

|  | 5 | El sistema valida que los nombres de etapa sean únicos dentro del servicio. |

|  | 6 | El sistema actualiza las etapas del servicio. |

|  | 7 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | Las etapas del servicio han sido actualizadas. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si hay nombres duplicados, el sistema informa el error. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Las etapas se ejecutan secuencialmente durante el lavado. Este caso de uso se realiza dentro de CU-029 o CU-030. |  |



---



| UC–040 | Crear paquete de servicios |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–07 Información sobre Paquetes de Servicios |  |

| \*\*Descripción\*\* | El administrador crea un paquete que agrupa 2 o más servicios del mismo tipo de vehículo con un porcentaje de descuento aplicable. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador.  Deben existir al menos 2 servicios activos del mismo tipo de vehículo. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de gestión de paquetes de servicios.  |

|  | 2 | El administrador hace clic en "Nuevo Paquete". |

|  | 3 | El sistema muestra un formulario con campos: Nombre, Tipo de Vehículo, Porcentaje de Descuento.  |

|  | 4 | El administrador selecciona el tipo de vehículo. |

|  | 5 | El sistema carga los servicios activos disponibles para ese tipo de vehículo. |

|  | 6 | El administrador selecciona al menos 2 servicios (máximo uno por tipo de servicio). |

|  | 7 | El administrador define el porcentaje de descuento (según configuración del sistema). |

|  | 8 | El sistema calcula automáticamente el precio final y tiempo estimado. |

|  | 9 | El administrador hace clic en "Guardar". |

|  | 10 | El sistema valida que no haya más de un servicio del mismo tipo de servicio. |

|  | 11 | El sistema crea el paquete con estado "Activo".  |

|  | 12 | El sistema muestra un mensaje de éxito. |

|  | 13 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El paquete está registrado y disponible para ser utilizado en lavados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 6a | Si se seleccionan menos de 2 servicios, el sistema informa el error. |

|  | 10a | Si hay más de un servicio del mismo tipo de servicio, el sistema informa el error. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 11 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | El porcentaje de descuento debe estar dentro del rango configurado en CU-065. El precio se calcula como:  suma de precios de servicios × (1 - descuento/100). |  |



---



\### CU-041 - Modificar paquete de servicios



| UC–041 | Modificar paquete de servicios |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–07 Información sobre Paquetes de Servicios |  |

| \*\*Descripción\*\* | El administrador actualiza la configuración de un paquete de servicios, incluyendo servicios incluidos y porcentaje de descuento.  |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. El paquete debe existir en el sistema. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-044 Consultar paquetes de servicios. |

|  | 2 | El administrador selecciona el paquete a modificar. |

|  | 3 | El sistema muestra el formulario de edición con los datos actuales.  |

|  | 4 | El administrador modifica los campos deseados (Nombre, Servicios, Descuento). |

|  | 5 | El sistema recalcula el precio y tiempo estimado en tiempo real. |

|  | 6 | El administrador hace clic en "Guardar". |

|  | 7 | El sistema valida los datos (mínimo 2 servicios, un servicio por tipo). |

|  | 8 | El sistema actualiza el registro del paquete.  |

|  | 9 | El sistema muestra un mensaje de éxito. |

|  | 10 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | Los datos del paquete han sido actualizados.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 7a | Si hay errores de validación, el sistema muestra los errores específicos. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 8 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Baja |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-044 Consultar paquetes de servicios. |  |



---



\### CU-042 - Desactivar paquete de servicios



| UC–042 | Desactivar paquete de servicios |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–07 Información sobre Paquetes de Servicios |  |

| \*\*Descripción\*\* | El administrador desactiva un paquete de servicios, dejándolo no disponible para selección. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. El paquete debe existir y estar activo. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-044 Consultar paquetes de servicios. |

|  | 2 | El administrador selecciona el paquete a desactivar.  |

|  | 3 | El sistema muestra un diálogo de confirmación. |

|  | 4 | El administrador confirma la desactivación. |

|  | 5 | El sistema cambia el estado del paquete a "Inactivo". |

|  | 6 | El sistema muestra un mensaje de éxito. |

|  | 7 | El sistema registra la acción en auditoría.  |

| \*\*Postcondición\*\* | El paquete ha sido desactivado.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si el administrador cancela, se aborta la operación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 5 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Baja |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-044 Consultar paquetes de servicios. |  |



---



\### CU-043 - Reactivar paquete de servicios



| UC–043 | Reactivar paquete de servicios |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–07 Información sobre Paquetes de Servicios |  |

| \*\*Descripción\*\* | El administrador reactiva un paquete previamente desactivado.  |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. El paquete debe existir y estar inactivo. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-044 Consultar paquetes de servicios (filtrando por inactivos). |

|  | 2 | El administrador selecciona el paquete a reactivar. |

|  | 3 | El sistema verifica que todos los servicios del paquete estén activos. |

|  | 4 | El sistema muestra un diálogo de confirmación.  |

|  | 5 | El administrador confirma la reactivación. |

|  | 6 | El sistema cambia el estado del paquete a "Activo". |

|  | 7 | El sistema muestra un mensaje de éxito. |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El paquete ha sido reactivado y está disponible para su uso. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si algún servicio del paquete está inactivo, el sistema advierte y sugiere editar el paquete. |

|  | 5a | Si el administrador cancela, se aborta la operación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Baja |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-044 Consultar paquetes de servicios.  |  |



---



\### CU-044 - Consultar paquetes de servicios



| UC–044 | Consultar paquetes de servicios |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–03 Gestión de Servicios y Paquetes |  |

| \*\*Requisitos asociados\*\* | IRQ–07 Información sobre Paquetes de Servicios |  |

| \*\*Descripción\*\* | El personal visualiza los paquetes de servicios disponibles con información de precio final, descuento y tiempo estimado total. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la sección de gestión de paquetes de servicios. |

|  | 2 | El sistema obtiene la lista de paquetes aplicando filtros por defecto (Activos). |

|  | 3 | El sistema calcula el precio final y tiempo estimado de cada paquete dinámicamente. |

|  | 4 | El sistema muestra la tabla de paquetes con columnas: Nombre, Tipo Vehículo, Servicios, Precio Original, Descuento, Precio Final, Tiempo Estimado, Estado. |

|  | 5 | El usuario puede aplicar filtros por:  estado, tipo de vehículo, rango de precios, rango de descuento. |

|  | 6 | El usuario puede ordenar por cualquier columna. |

|  | 7 | El usuario puede navegar entre páginas usando los controles de paginación. |

|  | 8 | El sistema actualiza la vista según los criterios seleccionados. |

| \*\*Postcondición\*\* | El usuario visualiza la lista de paquetes según los filtros aplicados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si no hay paquetes, el sistema muestra un mensaje indicándolo. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-4 | 2 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Por defecto muestra solo paquetes activos.  El precio y tiempo se calculan dinámicamente basándose en los servicios incluidos. |  |



---



\### \*\*Módulo:  Registro de Lavados\*\*



---



\### CU-045 - Registrar realización de un servicio (lavado)



| UC–045 | Registrar realización de un servicio (lavado) |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal registra un lavado seleccionando un vehículo (por búsqueda de patente), el cliente asociado, los servicios o paquetes a realizar, el descuento aplicable y notas adicionales. El lavado inicia inmediatamente tras su creación. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  Deben existir clientes, vehículos, servicios y empleados activos. El lavadero debe estar en horario de operación. No debe excederse la capacidad máxima de lavados simultáneos. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la sección de gestión de lavados. |

|  | 2 | El usuario hace clic en "Nuevo Lavado". |

|  | 3 | El sistema valida que esté en horario de operación. |

|  | 4 | El sistema valida que no se exceda la capacidad máxima de lavados simultáneos. |

|  | 5 | El sistema muestra un formulario de creación.  |

|  | 6 | El usuario busca y selecciona el vehículo por patente. |

|  | 7 | El sistema muestra los clientes asociados al vehículo. |

|  | 8 | El usuario selecciona el cliente que trae el vehículo. |

|  | 9 | El sistema carga los servicios y paquetes disponibles para el tipo de vehículo. |

|  | 10 | El usuario selecciona servicios individuales y/o un paquete. |

|  | 11 | El sistema calcula el tiempo estimado total y precio. |

|  | 12 | El sistema sugiere la cantidad de empleados según el tipo de vehículo. |

|  | 13 | El usuario asigna los empleados al lavado.  |

|  | 14 | El usuario puede agregar un descuento adicional (opcional). |

|  | 15 | El usuario puede agregar notas adicionales (opcional). |

|  | 16 | El usuario hace clic en "Iniciar Lavado".  |

|  | 17 | El sistema valida todos los datos.  |

|  | 18 | El sistema crea el lavado con estado "EnProceso" y registra el tiempo de inicio. |

|  | 19 | El sistema muestra un mensaje de éxito y redirige al detalle del lavado.  |

|  | 20 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El lavado está registrado y en proceso.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si está fuera del horario de operación, el sistema informa el error. |

|  | 4a | Si se excede la capacidad máxima, el sistema informa el error. |

|  | 6a | Si no se encuentra el vehículo, el sistema ofrece crearlo (CU-018). |

|  | 10a | Si se intenta agregar más de un paquete, el sistema informa el error.  |

|  | 13a | Si no hay empleados disponibles, el sistema advierte pero permite continuar. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 18 | 2 segundos |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | El lavado inicia inmediatamente al crearse (no hay estado "Pendiente"). |  |



---



\### CU-046 - Consultar lavados



| UC–046 | Consultar lavados |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal visualiza el historial de lavados con opciones de filtrado por estado, cliente, vehículo, rango de fechas, rango de precios, estado de pago y estado de retiro. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la sección de gestión de lavados. |

|  | 2 | El sistema obtiene la lista de lavados aplicando filtros por defecto (EnProceso). |

|  | 3 | El sistema muestra la tabla de lavados con columnas: ID, Vehículo, Cliente, Servicios, Estado, Pago, Retiro, Fecha.  |

|  | 4 | El usuario puede aplicar filtros por:  estado del lavado, estado de pago, estado de retiro, cliente, vehículo, rango de fechas, rango de precios. |

|  | 5 | El usuario puede ordenar por cualquier columna. |

|  | 6 | El usuario puede navegar entre páginas usando los controles de paginación. |

|  | 7 | El sistema actualiza la vista según los criterios seleccionados. |

| \*\*Postcondición\*\* | El usuario visualiza la lista de lavados según los filtros aplicados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si no hay lavados, el sistema muestra un mensaje indicándolo. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-3 | 2 segundos |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Por defecto muestra lavados en proceso. |  |



---



\### CU-047 - Buscar lavados



| UC–047 | Buscar lavados |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal busca lavados por patente, nombre de cliente o ID. La búsqueda se realiza en tiempo real. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la sección de gestión de lavados. |

|  | 2 | El usuario ingresa texto en el campo de búsqueda.  |

|  | 3 | El sistema busca coincidencias en:  ID del lavado, patente del vehículo, nombre del cliente.  |

|  | 4 | El sistema muestra los resultados filtrados en tiempo real. |

|  | 5 | El usuario puede seleccionar un lavado de los resultados. |

| \*\*Postcondición\*\* | El usuario visualiza los lavados que coinciden con el criterio de búsqueda. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si no hay coincidencias, el sistema muestra mensaje "Sin resultados". |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3-4 | 500ms |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | La búsqueda es insensible a mayúsculas/minúsculas.  |  |



---



\### CU-048 - Ver detalle de lavado



| UC–048 | Ver detalle de lavado |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal visualiza el detalle completo de un lavado, incluyendo servicios con su estado y progreso de etapas, información del cliente, vehículo, pago y tiempos.  |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  El lavado debe existir. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-046 Consultar lavados. |

|  | 2 | El usuario hace clic en un lavado para ver su detalle. |

|  | 3 | El sistema obtiene toda la información del lavado. |

|  | 4 | El sistema muestra:  |

|  | 4a | Información del vehículo:  patente, tipo, marca, modelo, color.  |

|  | 4b | Información del cliente: nombre, teléfono, email. |

|  | 4c | Lista de servicios con estado de cada uno y progreso de etapas. |

|  | 4d | Empleados asignados.  |

|  | 4e | Tiempos:  inicio, tiempo estimado, tiempo transcurrido. |

|  | 4f | Información de pago: monto total, monto pagado, estado de pago. |

|  | 4g | Estado de retiro y cliente que retira (si aplica). |

|  | 4h | Notas adicionales.  |

|  | 5 | El sistema muestra las acciones disponibles según el estado del lavado. |

| \*\*Postcondición\*\* | El usuario visualiza el detalle completo del lavado.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si el lavado no existe, el sistema muestra error y redirige a la lista.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3-4 | 1 segundo |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-046 Consultar lavados. |  |



---



\### CU-049 - Iniciar servicio en lavado



| UC–049 | Iniciar servicio en lavado |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal inicia la ejecución de un servicio específico dentro de un lavado, registrando el tiempo de inicio.  |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  El lavado debe estar en estado "EnProceso". El servicio debe estar pendiente de inicio. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-048 Ver detalle de lavado. |

|  | 2 | El usuario hace clic en "Iniciar" en el servicio correspondiente. |

|  | 3 | El sistema registra la fecha y hora de inicio del servicio. |

|  | 4 | El sistema actualiza el estado del servicio a "EnProceso". |

|  | 5 | Si el servicio tiene etapas, la primera etapa queda disponible para iniciar. |

|  | 6 | El sistema actualiza la vista del detalle del lavado. |

|  | 7 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El servicio está en proceso y registra su tiempo de inicio. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | - | - |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3-5 | 1 segundo |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-048 Ver detalle de lavado.  |  |



---



\### CU-050 - Iniciar etapa de servicio



| UC–050 | Iniciar etapa de servicio |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal inicia una etapa específica dentro de un servicio que tiene múltiples etapas definidas. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. El servicio debe estar en estado "EnProceso". La etapa anterior debe estar completada (si existe). |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-048 Ver detalle de lavado. |

|  | 2 | El sistema muestra las etapas del servicio con sus estados. |

|  | 3 | El usuario hace clic en "Iniciar" en la etapa correspondiente. |

|  | 4 | El sistema valida que las etapas anteriores estén completadas. |

|  | 5 | El sistema registra la fecha y hora de inicio de la etapa. |

|  | 6 | El sistema actualiza el estado de la etapa a "EnProceso".  |

|  | 7 | El sistema actualiza la vista del detalle del lavado. |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | La etapa está en proceso y registra su tiempo de inicio. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si hay etapas anteriores pendientes, el sistema informa que deben completarse primero. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 5-6 | 1 segundo |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-048 Ver detalle de lavado.  Las etapas se deben completar en orden secuencial. |  |



---



\### CU-051 - Finalizar etapa de servicio



| UC–051 | Finalizar etapa de servicio |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal marca como finalizada una etapa de un servicio en ejecución. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. La etapa debe estar en estado "EnProceso". |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-048 Ver detalle de lavado. |

|  | 2 | El usuario hace clic en "Finalizar" en la etapa en proceso. |

|  | 3 | El sistema registra la fecha y hora de finalización de la etapa. |

|  | 4 | El sistema actualiza el estado de la etapa a "Completada". |

|  | 5 | Si es la última etapa del servicio, el servicio se marca como completado automáticamente. |

|  | 6 | El sistema puede ejecutar CU-078 Notificar etapa finalizada (si está configurado). |

|  | 7 | El sistema actualiza la vista del detalle del lavado.  |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | La etapa está completada.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | - | - |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3-5 | 1 segundo |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-048 Ver detalle de lavado. |  |



---



\### CU-052 - Finalizar servicio en lavado



| UC–052 | Finalizar servicio en lavado |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal marca como completado un servicio específico dentro del lavado, registrando el tiempo de finalización. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. El servicio debe estar en estado "EnProceso". |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-048 Ver detalle de lavado. |

|  | 2 | El usuario hace clic en "Finalizar" en el servicio en proceso. |

|  | 3 | El sistema verifica el estado de las etapas del servicio (si tiene). |

|  | 4a | Si todas las etapas están completadas, el sistema marca el servicio como "Completado". |

|  | 4b | Si hay etapas pendientes, el sistema solicita confirmación para marcar como "CompletadoParcialmente". |

|  | 5 | El sistema registra la fecha y hora de finalización.  |

|  | 6 | El sistema actualiza la vista del detalle del lavado. |

|  | 7 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El servicio está completado o completado parcialmente. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4b. 1 | Si el usuario no confirma, se cancela la operación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 4-5 | 1 segundo |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-048 Ver detalle de lavado. |  |



---



\### CU-053 - Finalizar lavado completo



| UC–053 | Finalizar lavado completo |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal marca como completado un lavado cuando todos los servicios han sido finalizados. Se registra el tiempo de finalización total. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. El lavado debe estar en estado "EnProceso". |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-048 Ver detalle de lavado.  |

|  | 2 | El usuario hace clic en "Finalizar Lavado".  |

|  | 3 | El sistema verifica el estado de todos los servicios.  |

|  | 4a | Si todos los servicios están completados, el sistema marca el lavado como "Realizado". |

|  | 4b | Si hay servicios pendientes/parciales, el sistema marca como "RealizadoParcialmente" y solicita motivo. |

|  | 5 | El sistema registra la fecha y hora de finalización. |

|  | 6 | El sistema ejecuta CU-079 Notificar lavado finalizado (si está configurado). |

|  | 7 | El sistema actualiza la vista y muestra mensaje de éxito.  |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El lavado está finalizado (Realizado o RealizadoParcialmente). |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4b.1 | Si el usuario no proporciona motivo, el sistema lo solicita. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 4-6 | 2 segundos |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-048 Ver detalle de lavado.  |  |



---



\### CU-054 - Cancelar lavado



| UC–054 | Cancelar lavado |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal cancela un lavado completo indicando el motivo de cancelación. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. El lavado debe estar en estado "EnProceso".  |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-048 Ver detalle de lavado. |

|  | 2 | El usuario hace clic en "Cancelar Lavado". |

|  | 3 | El sistema muestra un formulario solicitando el motivo de cancelación (obligatorio). |

|  | 4 | El usuario ingresa el motivo de cancelación. |

|  | 5 | El sistema solicita confirmación de la acción. |

|  | 6 | El usuario confirma la cancelación. |

|  | 7 | El sistema cambia el estado del lavado a "Cancelado". |

|  | 8 | El sistema registra el motivo y la fecha de cancelación. |

|  | 9 | El sistema muestra un mensaje de éxito. |

|  | 10 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El lavado ha sido cancelado. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si no se ingresa motivo, el sistema informa que es obligatorio. |

|  | 6a | Si el usuario no confirma, se aborta la operación.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 7-8 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-048 Ver detalle de lavado. El motivo de cancelación es obligatorio para trazabilidad. |  |



---



\### CU-055 - Cancelar servicio en lavado



| UC–055 | Cancelar servicio en lavado |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal cancela un servicio específico dentro de un lavado indicando el motivo, permitiendo que el lavado continúe con los servicios restantes.  |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  El lavado debe estar en estado "EnProceso". El servicio debe estar pendiente o en proceso. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-048 Ver detalle de lavado. |

|  | 2 | El usuario hace clic en "Cancelar" en el servicio correspondiente. |

|  | 3 | El sistema muestra un formulario solicitando el motivo de cancelación.  |

|  | 4 | El usuario ingresa el motivo de cancelación.  |

|  | 5 | El sistema solicita confirmación.  |

|  | 6 | El usuario confirma la cancelación. |

|  | 7 | El sistema marca el servicio como "Cancelado". |

|  | 8 | El sistema recalcula el precio total del lavado. |

|  | 9 | El sistema actualiza la vista del detalle del lavado. |

|  | 10 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El servicio ha sido cancelado y el precio del lavado recalculado. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 6a | Si el usuario no confirma, se aborta la operación. |

|  | 7a | Si era el único servicio, el sistema sugiere cancelar el lavado completo. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 7-8 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-048 Ver detalle de lavado.  |  |



---



\### CU-056 - Registrar pago recibido



| UC–056 | Registrar pago recibido |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–05 Registro de Pagos |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal registra un pago total recibido por un cliente, actualizando el estado de pago del lavado a "Pagado". |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. El lavado debe existir y tener saldo pendiente. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-048 Ver detalle de lavado. |

|  | 2 | El usuario hace clic en "Registrar Pago".  |

|  | 3 | El sistema muestra el monto total, monto pagado y saldo pendiente. |

|  | 4 | El sistema pre-selecciona el saldo pendiente como monto a pagar. |

|  | 5 | El usuario confirma el monto y selecciona el método de pago. |

|  | 6 | El usuario hace clic en "Confirmar Pago".  |

|  | 7 | El sistema valida que el monto sea válido.  |

|  | 8 | El sistema registra el pago con fecha y hora. |

|  | 9 | El sistema actualiza el monto pagado y cambia el estado a "Pagado". |

|  | 10 | El sistema muestra un mensaje de éxito. |

|  | 11 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El pago está registrado y el lavado está marcado como pagado.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 7a | Si el monto es inválido, el sistema muestra error.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 8-9 | 1 segundo |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-048 Ver detalle de lavado. |  |



---



\### CU-057 - Registrar pago parcial



| UC–057 | Registrar pago parcial |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–05 Registro de Pagos |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal registra un pago parcial indicando el monto recibido, el estado de pago se actualiza a "Parcial" y se mantiene registro de cada pago individual. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. El lavado debe existir y tener saldo pendiente.  |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-048 Ver detalle de lavado. |

|  | 2 | El usuario hace clic en "Registrar Pago". |

|  | 3 | El sistema muestra el monto total, monto pagado y saldo pendiente. |

|  | 4 | El usuario ingresa un monto menor al saldo pendiente. |

|  | 5 | El usuario selecciona el método de pago. |

|  | 6 | El usuario hace clic en "Confirmar Pago". |

|  | 7 | El sistema valida que el monto no exceda el saldo pendiente. |

|  | 8 | El sistema registra el pago con fecha, hora y monto.  |

|  | 9 | El sistema actualiza el monto pagado.  |

|  | 10 | El sistema cambia el estado de pago a "Parcial". |

|  | 11 | El sistema muestra un mensaje de éxito con el saldo restante. |

|  | 12 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El pago parcial está registrado y el saldo pendiente actualizado. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 7a | Si el monto excede el saldo pendiente, el sistema informa el error. |

|  | 7b | Si el monto es menor o igual a cero, el sistema informa el error. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 8-10 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-048 Ver detalle de lavado. Se permite registrar múltiples pagos parciales. |  |



---



\### CU-058 - Marcar vehículo como retirado



| UC–058 | Marcar vehículo como retirado |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El personal marca un vehículo como retirado por el cliente, registrando la fecha y hora del retiro. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado. El lavado debe estar finalizado (Realizado o RealizadoParcialmente). |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-048 Ver detalle de lavado. |

|  | 2 | El usuario hace clic en "Marcar como Retirado". |

|  | 3 | El sistema muestra los clientes asociados al vehículo. |

|  | 4 | El usuario selecciona el cliente que retira el vehículo. |

|  | 5 | El usuario hace clic en "Confirmar Retiro". |

|  | 6 | El sistema actualiza el estado de retiro a "Retirado". |

|  | 7 | El sistema registra el cliente que retiró y la fecha/hora. |

|  | 8 | El sistema muestra un mensaje de éxito. |

|  | 9 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El vehículo está marcado como retirado.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si no se selecciona cliente, el sistema puede registrar el retiro sin asignar responsable. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6-7 | 1 segundo |

| \*\*Frecuencia\*\* | Frecuente |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-048 Ver detalle de lavado.  |  |



---



\### CU-059 - Calcular duración estimada de lavado



| UC–059 | Calcular duración estimada de lavado |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–04 Registro y Gestión de Lavados |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados, IRQ–04 Información sobre Servicios |  |

| \*\*Descripción\*\* | El sistema calcula automáticamente la duración estimada de un lavado sumando los tiempos estimados de cada servicio seleccionado. |  |

| \*\*Precondición\*\* | Se están seleccionando servicios para un lavado (dentro de CU-045). |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario selecciona uno o más servicios para el lavado. |

|  | 2 | El sistema obtiene el tiempo estimado de cada servicio seleccionado. |

|  | 3 | El sistema suma todos los tiempos estimados.  |

|  | 4 | El sistema muestra el tiempo total estimado al usuario. |

|  | 5 | El sistema utiliza este tiempo para calcular la hora estimada de finalización. |

| \*\*Postcondición\*\* | El tiempo estimado total está calculado y mostrado.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | - | - |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-4 | Inmediato |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Este cálculo se realiza en tiempo real mientras el usuario selecciona servicios.  |  |



---



\### \*\*Módulo:  Configuración\*\*



---



\### CU-060 - Configurar horarios del lavadero



| UC–060 | Configurar horarios del lavadero |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–11 Gestión de Configuración del Sistema |  |

| \*\*Requisitos asociados\*\* | IRQ–11 Información de Configuración del Sistema |  |

| \*\*Descripción\*\* | El administrador configura el horario de funcionamiento del establecimiento para cada día de la semana, incluyendo horarios divididos y días cerrados. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de configuración del sistema. |

|  | 2 | El sistema muestra la configuración actual de horarios por día de la semana. |

|  | 3 | Para cada día, el administrador puede configurar:  |

|  | 3a | Horario continuo:  ej. "09:00-18:00" |

|  | 3b | Horario dividido: ej.  "09:00-13:00,15:00-19:00" |

|  | 3c | Día cerrado: "CERRADO" |

|  | 4 | El administrador hace clic en "Guardar". |

|  | 5 | El sistema valida que los horarios sean coherentes (hora fin > hora inicio). |

|  | 6 | El sistema actualiza la configuración.  |

|  | 7 | El sistema muestra un mensaje de éxito.  |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | Los horarios de operación han sido actualizados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si hay errores de formato o coherencia, el sistema muestra los errores. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Los horarios afectan la validación al crear lavados y la información mostrada a clientes por WhatsApp. |  |



---



\### CU-061 - Configurar capacidad concurrente



| UC–061 | Configurar capacidad concurrente |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–11 Gestión de Configuración del Sistema |  |

| \*\*Requisitos asociados\*\* | IRQ–11 Información de Configuración del Sistema |  |

| \*\*Descripción\*\* | El administrador configura el número máximo de lavados que se pueden atender simultáneamente y si se debe considerar el número de empleados activos para calcular la capacidad efectiva. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador.  |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de configuración del sistema. |

|  | 2 | El sistema muestra la configuración actual de capacidad.  |

|  | 3 | El administrador configura:  |

|  | 3a | Número máximo de lavados simultáneos.  |

|  | 3b | Si se debe considerar el número de empleados activos. |

|  | 4 | El administrador hace clic en "Guardar". |

|  | 5 | El sistema valida que el número sea mayor a cero. |

|  | 6 | El sistema actualiza la configuración. |

|  | 7 | El sistema muestra un mensaje de éxito. |

|  | 8 | El sistema registra la acción en auditoría.  |

| \*\*Postcondición\*\* | La configuración de capacidad ha sido actualizada. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si el número es inválido, el sistema muestra error.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Esta configuración afecta la validación al crear nuevos lavados.  |  |



---



\### CU-062 - Configurar tiempos de tolerancia y notificación



| UC–062 | Configurar tiempos de tolerancia y notificación |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–11 Gestión de Configuración del Sistema |  |

| \*\*Requisitos asociados\*\* | IRQ–11 Información de Configuración del Sistema |  |

| \*\*Descripción\*\* | El administrador configura los minutos de anticipación para notificar antes del tiempo estimado, los minutos de tolerancia máxima y el intervalo para preguntar si ya terminó cuando se excede el tiempo. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de configuración del sistema. |

|  | 2 | El sistema muestra la configuración actual de tiempos.  |

|  | 3 | El administrador configura:  |

|  | 3a | Minutos de anticipación para notificación. |

|  | 3b | Minutos de tolerancia máxima después del tiempo estimado. |

|  | 3c | Intervalo en minutos para consultar si ya terminó. |

|  | 4 | El administrador hace clic en "Guardar". |

|  | 5 | El sistema valida que los valores sean mayores a cero.  |

|  | 6 | El sistema actualiza la configuración. |

|  | 7 | El sistema muestra un mensaje de éxito. |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | La configuración de tiempos ha sido actualizada.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si hay valores inválidos, el sistema muestra error.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Estos tiempos afectan el comportamiento de las notificaciones automáticas. |  |



---



\### CU-063 - Configurar duración de sesión



| UC–063 | Configurar duración de sesión |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–11 Gestión de Configuración del Sistema |  |

| \*\*Requisitos asociados\*\* | IRQ–11 Información de Configuración del Sistema |  |

| \*\*Descripción\*\* | El administrador configura la duración máxima de la sesión en horas y el tiempo de inactividad en minutos antes del cierre automático.  |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de configuración del sistema.  |

|  | 2 | El sistema muestra la configuración actual de sesión. |

|  | 3 | El administrador configura: |

|  | 3a | Duración máxima de sesión en horas. |

|  | 3b | Tiempo de inactividad en minutos para cierre automático.  |

|  | 4 | El administrador hace clic en "Guardar". |

|  | 5 | El sistema valida que los valores sean mayores a cero. |

|  | 6 | El sistema actualiza la configuración.  |

|  | 7 | El sistema muestra un mensaje de éxito.  |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | La configuración de sesión ha sido actualizada. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si hay valores inválidos, el sistema muestra error.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Esta configuración afecta el comportamiento de CU-004.  |  |



---



\### CU-064 - Configurar nombre y ubicación del lavadero



| UC–064 | Configurar nombre y ubicación del lavadero |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–11 Gestión de Configuración del Sistema |  |

| \*\*Requisitos asociados\*\* | IRQ–11 Información de Configuración del Sistema |  |

| \*\*Descripción\*\* | El administrador configura el nombre del lavadero y su ubicación, que se muestran en las notificaciones por WhatsApp y otros lugares del sistema. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de configuración del sistema. |

|  | 2 | El sistema muestra la configuración actual de información del lavadero. |

|  | 3 | El administrador configura: |

|  | 3a | Nombre del lavadero.  |

|  | 3b | Dirección/Ubicación.  |

|  | 3c | Teléfono de contacto. |

|  | 3d | Email de contacto. |

|  | 4 | El administrador hace clic en "Guardar". |

|  | 5 | El sistema valida los datos (campos obligatorios, formato de email). |

|  | 6 | El sistema actualiza la configuración. |

|  | 7 | El sistema muestra un mensaje de éxito. |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | La información del lavadero ha sido actualizada. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si hay errores de validación, el sistema muestra los errores. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Esta información se muestra a los clientes a través de WhatsApp (CU-092). |  |



---



\### CU-065 - Configurar paso de descuento para paquetes



| UC–065 | Configurar paso de descuento para paquetes |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–11 Gestión de Configuración del Sistema |  |

| \*\*Requisitos asociados\*\* | IRQ–11 Información de Configuración del Sistema |  |

| \*\*Descripción\*\* | El administrador configura el incremento mínimo de porcentaje de descuento al crear paquetes de servicios (mínimo 5%). |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de configuración del sistema.  |

|  | 2 | El sistema muestra la configuración actual de descuentos para paquetes. |

|  | 3 | El administrador configura: |

|  | 3a | Porcentaje mínimo de descuento.  |

|  | 3b | Porcentaje máximo de descuento. |

|  | 3c | Incremento (step) de descuento (mínimo 5%). |

|  | 4 | El administrador hace clic en "Guardar". |

|  | 5 | El sistema valida que mínimo <= máximo y step >= 5. |

|  | 6 | El sistema actualiza la configuración. |

|  | 7 | El sistema muestra un mensaje de éxito. |

|  | 8 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | La configuración de descuentos ha sido actualizada. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si hay errores de validación, el sistema muestra los errores. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6 | 1 segundo |

| \*\*Frecuencia\*\* | Rara |  |

| \*\*Estabilidad\*\* | Baja |

Perfecto, aquí está la \*\*quinta parte\*\* del documento continuando con los Casos de Uso Extendidos (CU-066 a CU-092) y las secciones finales:



---



| UC–066 | Registrar turno |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–06 Planificación y Gestión de Turnos |  |

| \*\*Requisitos asociados\*\* | IRQ–09 Información sobre Turnos |  |

| \*\*Descripción\*\* | El personal agenda un turno para un cliente en el sistema, especificando fecha, hora y servicios solicitados. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador.  Deben existir clientes, vehículos y servicios activos. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de gestión de turnos.  |

|  | 2 | El administrador hace clic en "Nuevo Turno". |

|  | 3 | El sistema muestra un formulario con campos:  Fecha, Hora, Cliente, Vehículo, Servicios. |

|  | 4 | El administrador selecciona la fecha del turno. |

|  | 5 | El sistema muestra los horarios disponibles según la configuración y turnos existentes. |

|  | 6 | El administrador selecciona la hora del turno.  |

|  | 7 | El administrador busca y selecciona el cliente.  |

|  | 8 | El sistema muestra los vehículos asociados al cliente. |

|  | 9 | El administrador selecciona el vehículo. |

|  | 10 | El sistema carga los servicios disponibles para el tipo de vehículo. |

|  | 11 | El administrador selecciona los servicios deseados. |

|  | 12 | El sistema ejecuta CU-073 para validar disponibilidad. |

|  | 13 | El administrador hace clic en "Guardar".  |

|  | 14 | El sistema crea el turno con estado "Pendiente". |

|  | 15 | El sistema muestra un mensaje de éxito. |

|  | 16 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El turno está registrado en la agenda.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si no hay horarios disponibles en la fecha, el sistema lo informa. |

|  | 12a | Si hay conflicto de horarios, el sistema informa y sugiere alternativas. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 14 | 1 segundo |

| \*\*Frecuencia\*\* | Frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | El sistema valida automáticamente que no haya solapamientos. |  |



---



\### CU-067 - Modificar turno



| UC–067 | Modificar turno |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–06 Planificación y Gestión de Turnos |  |

| \*\*Requisitos asociados\*\* | IRQ–09 Información sobre Turnos |  |

| \*\*Descripción\*\* | El personal actualiza la información de un turno ya registrado, validando la disponibilidad en el nuevo horario. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. El turno debe existir y estar en estado "Pendiente". |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-068 Consultar turnos asignados. |

|  | 2 | El administrador selecciona el turno a modificar. |

|  | 3 | El sistema muestra el formulario de edición con los datos actuales.  |

|  | 4 | El administrador modifica los campos deseados (fecha, hora, servicios). |

|  | 5 | El sistema ejecuta CU-074 para validar disponibilidad en el nuevo horario.  |

|  | 6 | El administrador hace clic en "Guardar". |

|  | 7 | El sistema actualiza el turno.  |

|  | 8 | El sistema muestra un mensaje de éxito.  |

|  | 9 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El turno ha sido actualizado. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si no hay disponibilidad, el sistema informa y sugiere alternativas.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 7 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-068 Consultar turnos asignados. |  |



---



\### CU-068 - Consultar turnos asignados



| UC–068 | Consultar turnos asignados |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–06 Planificación y Gestión de Turnos |  |

| \*\*Requisitos asociados\*\* | IRQ–09 Información sobre Turnos |  |

| \*\*Descripción\*\* | El personal consulta la agenda de turnos registrados en el sistema con vista de calendario. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de gestión de turnos.  |

|  | 2 | El sistema obtiene los turnos del período actual (semana/mes). |

|  | 3 | El sistema muestra la vista de calendario con los turnos.  |

|  | 4 | El administrador puede cambiar entre vista diaria, semanal o mensual. |

|  | 5 | El administrador puede navegar entre fechas.  |

|  | 6 | El administrador puede filtrar por estado del turno. |

|  | 7 | El sistema actualiza la vista según los criterios seleccionados. |

|  | 8 | El administrador puede hacer clic en un turno para ver detalles. |

| \*\*Postcondición\*\* | El administrador visualiza la agenda de turnos. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si no hay turnos en el período, el sistema muestra calendario vacío. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-3 | 2 segundos |

| \*\*Frecuencia\*\* | Frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | La vista de calendario permite una visualización rápida de la disponibilidad. |  |



---



\### CU-069 - Cancelar turno



| UC–069 | Cancelar turno |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–06 Planificación y Gestión de Turnos |  |

| \*\*Requisitos asociados\*\* | IRQ–09 Información sobre Turnos |  |

| \*\*Descripción\*\* | El personal cancela un turno previamente asignado a un cliente. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. El turno debe existir y estar en estado "Pendiente". |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-068 Consultar turnos asignados.  |

|  | 2 | El administrador selecciona el turno a cancelar. |

|  | 3 | El sistema muestra un diálogo de confirmación solicitando motivo. |

|  | 4 | El administrador ingresa el motivo de cancelación. |

|  | 5 | El administrador confirma la cancelación.  |

|  | 6 | El sistema cambia el estado del turno a "Cancelado". |

|  | 7 | El sistema ejecuta CU-075 para reorganizar agenda si corresponde. |

|  | 8 | El sistema puede notificar al cliente por WhatsApp (si está configurado). |

|  | 9 | El sistema muestra un mensaje de éxito. |

|  | 10 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El turno ha sido cancelado y la agenda puede reorganizarse. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si el administrador no confirma, se aborta la operación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6-7 | 2 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-068 Consultar turnos asignados.  |  |



---



\### CU-070 - Solicitar turno por WhatsApp



| UC–070 | Solicitar turno por WhatsApp |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–06 Planificación y Gestión de Turnos, OBJ–10 Integración con WhatsApp |  |

| \*\*Requisitos asociados\*\* | IRQ–09 Información sobre Turnos, IRQ–12 Información de Sesiones WhatsApp |  |

| \*\*Descripción\*\* | El cliente agenda un turno directamente mediante el flujo conversacional de WhatsApp, indicando vehículo y servicios deseados. |  |

| \*\*Precondición\*\* | El cliente debe estar registrado y tener al menos un vehículo asociado. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El cliente accede al menú principal de WhatsApp. |

|  | 2 | El cliente selecciona "Solicitar Turno".  |

|  | 3 | El sistema muestra los vehículos del cliente. |

|  | 4 | El cliente selecciona el vehículo.  |

|  | 5 | El sistema muestra los servicios disponibles para ese tipo de vehículo. |

|  | 6 | El cliente selecciona los servicios deseados. |

|  | 7 | El sistema calcula la duración estimada.  |

|  | 8 | El sistema muestra las fechas disponibles. |

|  | 9 | El cliente selecciona la fecha. |

|  | 10 | El sistema muestra los horarios disponibles.  |

|  | 11 | El cliente selecciona el horario. |

|  | 12 | El sistema ejecuta CU-073 para validar disponibilidad. |

|  | 13 | El sistema muestra resumen y solicita confirmación. |

|  | 14 | El cliente confirma el turno.  |

|  | 15 | El sistema crea el turno con estado "Pendiente". |

|  | 16 | El sistema envía confirmación del turno al cliente.  |

|  | 17 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El turno está registrado y el cliente notificado. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si no tiene vehículos, el sistema ofrece registrar uno (CU-026). |

|  | 12a | Si no hay disponibilidad, el sistema ofrece alternativas.  |

|  | 14a | Si el cliente no confirma, puede modificar la selección. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 15 | 2 segundos |

| \*\*Frecuencia\*\* | Frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | El flujo guía al cliente paso a paso de forma conversacional. |  |



---



\### CU-071 - Consultar turnos próximos por WhatsApp



| UC–071 | Consultar turnos próximos por WhatsApp |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–06 Planificación y Gestión de Turnos, OBJ–10 Integración con WhatsApp |  |

| \*\*Requisitos asociados\*\* | IRQ–09 Información sobre Turnos, IRQ–12 Información de Sesiones WhatsApp |  |

| \*\*Descripción\*\* | El cliente visualiza los turnos futuros registrados a su nombre mediante WhatsApp. |  |

| \*\*Precondición\*\* | El cliente debe estar registrado en el sistema. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El cliente accede al menú principal de WhatsApp. |

|  | 2 | El cliente selecciona "Mis Turnos". |

|  | 3 | El sistema busca los turnos pendientes del cliente. |

|  | 4 | El sistema muestra la lista de turnos con:  fecha, hora, vehículo, servicios. |

|  | 5 | El cliente puede seleccionar un turno para ver más detalles. |

|  | 6 | El sistema muestra el detalle del turno seleccionado. |

|  | 7 | El sistema ofrece opciones:  cancelar turno, volver al menú.  |

| \*\*Postcondición\*\* | El cliente ha visualizado sus turnos próximos. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si no tiene turnos, el sistema informa y ofrece solicitar uno. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3-4 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Solo se muestran turnos con estado "Pendiente" o "Confirmado". |  |



---



\### CU-072 - Cancelar turno por WhatsApp



| UC–072 | Cancelar turno por WhatsApp |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–06 Planificación y Gestión de Turnos, OBJ–10 Integración con WhatsApp |  |

| \*\*Requisitos asociados\*\* | IRQ–09 Información sobre Turnos, IRQ–12 Información de Sesiones WhatsApp |  |

| \*\*Descripción\*\* | El cliente cancela un turno previamente asignado mediante el flujo de WhatsApp. |  |

| \*\*Precondición\*\* | El cliente debe estar registrado y tener turnos pendientes. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta CU-071 para mostrar los turnos del cliente. |

|  | 2 | El cliente selecciona el turno a cancelar. |

|  | 3 | El sistema muestra el detalle del turno.  |

|  | 4 | El cliente selecciona "Cancelar Turno". |

|  | 5 | El sistema solicita confirmación. |

|  | 6 | El cliente confirma la cancelación. |

|  | 7 | El sistema cambia el estado del turno a "Cancelado". |

|  | 8 | El sistema ejecuta CU-075 para reorganizar agenda si corresponde. |

|  | 9 | El sistema confirma la cancelación al cliente. |

|  | 10 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El turno ha sido cancelado.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 6a | Si el cliente no confirma, se aborta la operación. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 7-8 | 2 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | La cancelación puede activar la reorganización de agenda para otros clientes. |  |



---



\### CU-073 - Asignar turno automáticamente sin superposición



| UC–073 | Asignar turno automáticamente sin superposición |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–06 Planificación y Gestión de Turnos |  |

| \*\*Requisitos asociados\*\* | IRQ–09 Información sobre Turnos, IRQ–11 Información de Configuración |  |

| \*\*Descripción\*\* | El sistema asigna turnos asegurando que no existan solapamientos en la agenda, considerando la duración estimada de los servicios. |  |

| \*\*Precondición\*\* | Se está creando o modificando un turno. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El sistema recibe la fecha, hora y duración estimada del turno solicitado. |

|  | 2 | El sistema calcula la hora de finalización estimada. |

|  | 3 | El sistema obtiene los turnos existentes en esa fecha. |

|  | 4 | El sistema verifica que no haya solapamiento con otros turnos. |

|  | 5 | El sistema verifica que esté dentro del horario de operación. |

|  | 6 | El sistema verifica que no se exceda la capacidad máxima de lavados simultáneos. |

|  | 7 | Si todas las validaciones pasan, el sistema confirma la disponibilidad. |

| \*\*Postcondición\*\* | La disponibilidad del horario ha sido validada. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si hay solapamiento, el sistema retorna error con los horarios alternativos disponibles. |

|  | 5a | Si está fuera del horario de operación, el sistema informa el error. |

|  | 6a | Si se excede la capacidad, el sistema informa el error. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3-7 | 500ms |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Este caso de uso es ejecutado automáticamente por el sistema. |  |



---



\### CU-074 - Validar disponibilidad al mover un turno



| UC–074 | Validar disponibilidad al mover un turno |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–06 Planificación y Gestión de Turnos |  |

| \*\*Requisitos asociados\*\* | IRQ–09 Información sobre Turnos |  |

| \*\*Descripción\*\* | El sistema valida si existe disponibilidad en la agenda al modificar la fecha u hora de un turno. |  |

| \*\*Precondición\*\* | Se está modificando un turno existente (dentro de CU-067). |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El sistema recibe la nueva fecha, hora y duración del turno.  |

|  | 2 | El sistema excluye el turno actual de la verificación de solapamientos. |

|  | 3 | El sistema ejecuta las mismas validaciones que CU-073. |

|  | 4 | Si todas las validaciones pasan, el sistema confirma la disponibilidad. |

| \*\*Postcondición\*\* | La disponibilidad del nuevo horario ha sido validada. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si no hay disponibilidad, el sistema retorna los horarios alternativos. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-4 | 500ms |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Este caso de uso es ejecutado automáticamente por el sistema.  |  |



---



\### CU-075 - Reorganizar agenda ante cancelaciones



| UC–075 | Reorganizar agenda ante cancelaciones |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–06 Planificación y Gestión de Turnos |  |

| \*\*Requisitos asociados\*\* | IRQ–09 Información sobre Turnos |  |

| \*\*Descripción\*\* | El sistema reordena automáticamente la agenda de turnos cuando ocurre una cancelación, notificando a clientes sobre posibles adelantos. |  |

| \*\*Precondición\*\* | Se ha cancelado un turno (dentro de CU-069 o CU-072). |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El sistema identifica el horario liberado por la cancelación. |

|  | 2 | El sistema busca turnos posteriores que podrían adelantarse. |

|  | 3 | Para cada turno candidato, el sistema verifica si el cliente podría beneficiarse.  |

|  | 4 | El sistema envía notificación por WhatsApp ofreciendo el adelanto. |

|  | 5 | Si el cliente acepta, el sistema actualiza el horario del turno. |

|  | 6 | El sistema registra los cambios en auditoría. |

| \*\*Postcondición\*\* | La agenda ha sido optimizada y los clientes notificados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si no hay turnos que puedan adelantarse, el proceso termina. |

|  | 5a | Si el cliente rechaza o no responde, se consulta al siguiente candidato. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 1-4 | 3 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Este caso de uso es ejecutado automáticamente por el sistema. |  |



---



\### \*\*Módulo: Notificación al Cliente\*\*



---



\### CU-076 - Enviar notificación por WhatsApp



| UC–076 | Enviar notificación por WhatsApp |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–12 Notificación al Cliente |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes |  |

| \*\*Descripción\*\* | El personal envía notificaciones personalizadas a los clientes por WhatsApp utilizando la integración con WhatsApp Cloud API. |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  El cliente debe tener número de teléfono registrado. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la función de enviar notificación (desde detalle de lavado u otro contexto). |

|  | 2 | El sistema muestra el formulario de notificación con el cliente preseleccionado. |

|  | 3 | El usuario redacta el mensaje personalizado.  |

|  | 4 | El usuario hace clic en "Enviar".  |

|  | 5 | El sistema envía el mensaje a través de WhatsApp Cloud API. |

|  | 6 | El sistema muestra confirmación del envío. |

|  | 7 | El sistema registra la acción en auditoría. |

| \*\*Postcondición\*\* | El mensaje ha sido enviado al cliente. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si hay error en el envío, el sistema muestra el mensaje de error. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 5 | 2 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Requiere configuración válida de WhatsApp Cloud API. |  |



---



\### CU-077 - Enviar notificación por correo electrónico



| UC–077 | Enviar notificación por correo electrónico |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–12 Notificación al Cliente |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes |  |

| \*\*Descripción\*\* | El personal envía notificaciones personalizadas a los clientes por correo electrónico.  |  |

| \*\*Precondición\*\* | El usuario debe estar autenticado.  El cliente debe tener email registrado. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario accede a la función de enviar notificación por email. |

|  | 2 | El sistema muestra el formulario de notificación con el cliente preseleccionado.  |

|  | 3 | El usuario redacta el asunto y cuerpo del mensaje.  |

|  | 4 | El usuario hace clic en "Enviar".  |

|  | 5 | El sistema envía el correo electrónico.  |

|  | 6 | El sistema muestra confirmación del envío. |

|  | 7 | El sistema registra la acción en auditoría.  |

| \*\*Postcondición\*\* | El correo ha sido enviado al cliente. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si el cliente no tiene email, el sistema informa el error. |

|  | 5a | Si hay error en el envío, el sistema muestra el mensaje de error.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 5 | 3 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Requiere configuración de servicio de correo electrónico. |  |



---



\### CU-078 - Notificar etapa finalizada



| UC–078 | Notificar etapa finalizada |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–12 Notificación al Cliente |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes, IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El sistema envía automáticamente una notificación por WhatsApp al cliente cuando una etapa de su servicio ha sido finalizada. |  |

| \*\*Precondición\*\* | Una etapa de servicio ha sido completada (dentro de CU-051). La notificación automática está habilitada. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El sistema detecta que una etapa ha sido finalizada. |

|  | 2 | El sistema obtiene los datos del cliente y del lavado. |

|  | 3 | El sistema genera el mensaje de notificación con detalles de la etapa completada. |

|  | 4 | El sistema envía el mensaje por WhatsApp al cliente. |

|  | 5 | El sistema registra la notificación enviada. |

| \*\*Postcondición\*\* | El cliente ha sido notificado del progreso de su servicio. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si hay error en el envío, se registra el error pero no se interrumpe el proceso.  |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-4 | 2 segundos |

| \*\*Frecuencia\*\* | Frecuente |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Este caso de uso es ejecutado automáticamente por el sistema.  |  |



---



\### CU-079 - Notificar lavado finalizado



| UC–079 | Notificar lavado finalizado |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–12 Notificación al Cliente |  |

| \*\*Requisitos asociados\*\* | IRQ–02 Información sobre Clientes, IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El sistema envía automáticamente una notificación por WhatsApp al cliente cuando el lavado de su vehículo está completo y listo para retirar. |  |

| \*\*Precondición\*\* | Un lavado ha sido finalizado (dentro de CU-053). La notificación automática está habilitada. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El sistema detecta que un lavado ha sido finalizado. |

|  | 2 | El sistema obtiene los datos del cliente, vehículo y lavado. |

|  | 3 | El sistema genera el mensaje de notificación indicando que el vehículo está listo.  |

|  | 4 | El sistema incluye información del lavadero (nombre, ubicación). |

|  | 5 | El sistema envía el mensaje por WhatsApp al cliente. |

|  | 6 | El sistema registra la notificación enviada. |

| \*\*Postcondición\*\* | El cliente ha sido notificado que su vehículo está listo. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si hay error en el envío, se registra el error pero no se interrumpe el proceso. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-5 | 2 segundos |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Este caso de uso es ejecutado automáticamente por el sistema.  |  |



---



\### CU-080 - Solicitar hablar con el personal



| UC–080 | Solicitar hablar con el personal |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–10 Integración con WhatsApp |  |

| \*\*Requisitos asociados\*\* | IRQ–12 Información de Sesiones WhatsApp |  |

| \*\*Descripción\*\* | El cliente envía un mensaje por WhatsApp para comunicarse directamente con el personal del lavadero, saliendo del flujo automatizado. |  |

| \*\*Precondición\*\* | El cliente está en una conversación activa de WhatsApp. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El cliente selecciona "Hablar con personal" del menú o escribe un mensaje libre. |

|  | 2 | El sistema detecta la solicitud de atención humana. |

|  | 3 | El sistema marca la sesión como "requiere atención humana".  |

|  | 4 | El sistema envía mensaje confirmando que un empleado responderá pronto. |

|  | 5 | El sistema notifica al personal del lavadero sobre la solicitud. |

|  | 6 | Los mensajes siguientes se almacenan para revisión del personal. |

| \*\*Postcondición\*\* | La conversación ha sido escalada a atención humana. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | - | - |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3-4 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Baja |  |

| \*\*Comentarios\*\* | Permite manejar casos que el bot no puede resolver automáticamente. |  |



---



\### \*\*Módulo:  Estadísticas y Reportes\*\*



---



\### CU-081 - Consultar estadísticas básicas



| UC–081 | Consultar estadísticas básicas |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–08 Módulo de Estadísticas y Reportes |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados, IRQ–02 Información sobre Clientes |  |

| \*\*Descripción\*\* | El administrador consulta estadísticas sobre la actividad general del lavadero:  lavados realizados, clientes activos, servicios más solicitados, cumplimiento de turnos.  |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de estadísticas. |

|  | 2 | El sistema calcula y muestra indicadores clave:  |

|  | 2a | Total de lavados realizados (por período). |

|  | 2b | Lavados activos actualmente. |

|  | 2c | Total de clientes registrados (activos/nuevos). |

|  | 2d | Ingresos totales (por período). |

|  | 2e | Servicios más solicitados.  |

|  | 2f | Promedio de tiempo por lavado. |

|  | 2g | Tasa de cumplimiento de turnos. |

|  | 3 | El administrador puede filtrar por rango de fechas. |

|  | 4 | El sistema actualiza las estadísticas según el filtro.  |

| \*\*Postcondición\*\* | El administrador visualiza las estadísticas del lavadero. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si no hay datos, el sistema muestra valores en cero. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2 | 3 segundos |

| \*\*Frecuencia\*\* | Frecuente |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Las estadísticas se calculan en tiempo real. |  |



---



\### CU-082 - Consultar historial de pagos



| UC–082 | Consultar historial de pagos |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–08 Módulo de Estadísticas y Reportes |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | El administrador accede al historial de todos los pagos registrados con opciones de filtrado por fecha, cliente y monto. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador.  |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de historial de pagos. |

|  | 2 | El sistema obtiene los pagos registrados. |

|  | 3 | El sistema muestra la tabla de pagos con:  fecha, lavado, cliente, monto, método de pago.  |

|  | 4 | El administrador puede filtrar por:  rango de fechas, cliente, rango de montos.  |

|  | 5 | El administrador puede ordenar por cualquier columna. |

|  | 6 | El sistema muestra totales:  suma de pagos, cantidad de transacciones. |

| \*\*Postcondición\*\* | El administrador visualiza el historial de pagos. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si no hay pagos, el sistema muestra mensaje indicándolo. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-3 | 2 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Ninguno. |  |



---



\### CU-083 - Generar reportes



| UC–083 | Generar reportes |  |

| :---- | :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–08 Módulo de Estadísticas y Reportes |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados, IRQ–02 Información sobre Clientes |  |

| \*\*Descripción\*\* | El administrador genera reportes personalizables de los diversos aspectos del sistema para un período de tiempo específico. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador.  |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de reportes.  |

|  | 2 | El sistema muestra los tipos de reportes disponibles:  |

|  | 2a | Reporte de lavados (por período, estado, cliente). |

|  | 2b | Reporte de ingresos (por período, método de pago). |

|  | 2c | Reporte de clientes (nuevos registros, frecuencia). |

|  | 2d | Reporte de servicios (más solicitados, ingresos por servicio). |

|  | 3 | El administrador selecciona el tipo de reporte. |

|  | 4 | El administrador configura los filtros del reporte (fechas, criterios). |

|  | 5 | El administrador hace clic en "Generar Reporte". |

|  | 6 | El sistema genera el reporte con los datos filtrados. |

|  | 7 | El sistema muestra una vista previa del reporte. |

|  | 8 | El administrador puede exportar el reporte (CU-083. 1). |

| \*\*Postcondición\*\* | El reporte ha sido generado. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 6a | Si no hay datos para el reporte, el sistema informa que no hay resultados. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 6-7 | 5 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Los reportes permiten análisis detallado para la toma de decisiones.  |  |



---



\### CU-083.1 - Exportar reportes a PDF o Excel



| UC–083.1 | Exportar reportes a PDF o Excel |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–08 Módulo de Estadísticas y Reportes |  |

| \*\*Requisitos asociados\*\* | IRQ–08 Información sobre Lavados |  |

| \*\*Descripción\*\* | Extiende del CU-083. El sistema permite exportar los reportes generados en formato PDF o Excel. |  |

| \*\*Precondición\*\* | Se ha generado un reporte (dentro de CU-083). |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador visualiza el reporte generado. |

|  | 2 | El administrador selecciona el formato de exportación (PDF o Excel). |

|  | 3 | El administrador hace clic en "Exportar".  |

|  | 4 | El sistema genera el archivo en el formato seleccionado. |

|  | 5 | El sistema inicia la descarga del archivo.  |

|  | 6 | El sistema muestra confirmación de exportación exitosa. |

| \*\*Postcondición\*\* | El reporte ha sido exportado y descargado. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si hay error en la generación, el sistema muestra el error. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 4-5 | 5 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Ninguno. |  |



---



\### \*\*Módulo:  Auditoría\*\*



---



\### CU-084 - Consultar historial de auditoría



| UC–084 | Consultar historial de auditoría |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–07 Registro de Auditoría |  |

| \*\*Requisitos asociados\*\* | IRQ–10 Información de Auditoría |  |

| \*\*Descripción\*\* | El administrador accede al registro completo de acciones realizadas en el sistema, con información del usuario, acción, fecha/hora y entidad afectada. |  |

| \*\*Precondición\*\* | El usuario debe tener rol de Administrador. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El administrador accede a la sección de auditoría. |

|  | 2 | El sistema obtiene los registros de auditoría aplicando filtros por defecto. |

|  | 3 | El sistema resuelve los nombres de usuarios y entidades afectadas. |

|  | 4 | El sistema muestra la tabla de registros con: fecha/hora, usuario, acción, entidad afectada.  |

|  | 5 | El administrador puede usar CU-085 para filtrar los registros. |

|  | 6 | El administrador puede ordenar por fecha (ascendente/descendente). |

|  | 7 | El administrador puede paginar los resultados.  |

| \*\*Postcondición\*\* | El administrador visualiza el historial de auditoría. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si no hay registros, el sistema muestra mensaje indicándolo. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-4 | 2 segundos |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Los nombres de usuarios y entidades se resuelven dinámicamente. |  |



---



\### CU-085 - Filtrar registros de auditoría



| UC–085 | Filtrar registros de auditoría |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–07 Registro de Auditoría |  |

| \*\*Requisitos asociados\*\* | IRQ–10 Información de Auditoría |  |

| \*\*Descripción\*\* | El administrador filtra los registros de auditoría por rango de fechas, tipo de acción, tipo de entidad objetivo y usuario. |  |

| \*\*Precondición\*\* | Se está consultando el historial de auditoría (dentro de CU-084). |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-084 Consultar historial de auditoría. |

|  | 2 | El sistema muestra los controles de filtrado.  |

|  | 3 | El administrador puede filtrar por:  |

|  | 3a | Rango de fechas (desde/hasta). |

|  | 3b | Tipo de acción (creación, modificación, eliminación, login, etc.). |

|  | 3c | Tipo de entidad afectada (Servicio, Cliente, Empleado, Lavado, etc.). |

|  | 3d | Usuario que realizó la acción.  |

|  | 4 | El administrador aplica los filtros.  |

|  | 5 | El sistema actualiza la lista de registros según los filtros.  |

| \*\*Postcondición\*\* | Los registros de auditoría están filtrados según los criterios seleccionados. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si no hay registros que coincidan, el sistema lo informa. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 5 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-084 Consultar historial de auditoría. |  |



---



\### CU-086 - Ver detalle de registro de auditoría



| UC–086 | Ver detalle de registro de auditoría |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–07 Registro de Auditoría |  |

| \*\*Requisitos asociados\*\* | IRQ–10 Información de Auditoría |  |

| \*\*Descripción\*\* | El administrador visualiza el detalle completo de un registro de auditoría específico, incluyendo navegación a la entidad afectada.  |  |

| \*\*Precondición\*\* | Se está consultando el historial de auditoría (dentro de CU-084). |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | Se ejecuta el caso de uso CU-084 Consultar historial de auditoría.  |

|  | 2 | El administrador hace clic en un registro para ver su detalle.  |

|  | 3 | El sistema muestra toda la información del registro:  |

|  | 3a | Fecha y hora exacta.  |

|  | 3b | Usuario (ID y email). |

|  | 3c | Acción realizada (descripción completa). |

|  | 3d | Tipo de entidad afectada. |

|  | 3e | ID de la entidad afectada. |

|  | 4 | El sistema ofrece enlace para navegar a la entidad afectada (si existe). |

|  | 5 | El administrador puede hacer clic en el enlace para ver la entidad. |

| \*\*Postcondición\*\* | El administrador visualiza el detalle del registro de auditoría. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 4a | Si la entidad fue eliminada, el sistema informa que no está disponible. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3 | 500ms |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Incluye el caso de uso CU-084 Consultar historial de auditoría. |  |



---



\### CU-087 - Registrar todas las acciones para auditoría



| UC–087 | Registrar todas las acciones para auditoría |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–07 Registro de Auditoría |  |

| \*\*Requisitos asociados\*\* | IRQ–10 Información de Auditoría |  |

| \*\*Descripción\*\* | El sistema almacena automáticamente todas las acciones relevantes de los usuarios en un historial para fines de auditoría, incluyendo creación, modificación, activación y desactivación de entidades. |  |

| \*\*Precondición\*\* | Un usuario está realizando una acción en el sistema. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El usuario ejecuta una acción (crear, modificar, desactivar, reactivar, login, logout). |

|  | 2 | El sistema captura el ID y email del usuario actual. |

|  | 3 | El sistema determina el tipo de acción y la entidad afectada. |

|  | 4 | El sistema crea un registro de auditoría con:  |

|  | 4a | UserId:  ID del usuario. |

|  | 4b | UserEmail: correo del usuario. |

|  | 4c | Action: descripción de la acción. |

|  | 4d | TargetId: ID de la entidad afectada.  |

|  | 4e | TargetType: tipo de entidad.  |

|  | 4f | Timestamp: fecha y hora actual. |

|  | 5 | El sistema almacena el registro en la colección de auditoría. |

| \*\*Postcondición\*\* | La acción ha sido registrada en el historial de auditoría. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 5a | Si hay un error al guardar, se registra en logs pero no se interrumpe la operación principal. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 5 | < 500ms (asíncrono) |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | El registro de auditoría no debe afectar el rendimiento de las operaciones principales. |  |



---



\### \*\*Módulo:  Integración WhatsApp\*\*



---



\### CU-088 - Procesar mensaje entrante de WhatsApp



| UC–088 | Procesar mensaje entrante de WhatsApp |  |

| :---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–10 Integración con WhatsApp |  |

| \*\*Requisitos asociados\*\* | IRQ–12 Información de Sesiones WhatsApp |  |

| \*\*Descripción\*\* | El sistema recibe y procesa los mensajes entrantes de WhatsApp, identificando el estado de la conversación y ejecutando el flujo correspondiente. |  |

| \*\*Precondición\*\* | El webhook de WhatsApp está configurado y activo. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El sistema recibe una notificación de webhook de WhatsApp. |

|  | 2 | El sistema valida la firma del mensaje (seguridad). |

|  | 3 | El sistema extrae el número de teléfono y contenido del mensaje. |

|  | 4 | El sistema ejecuta CU-027 para identificar si el número está registrado. |

|  | 5 | El sistema ejecuta CU-090 para obtener/crear la sesión de conversación. |

|  | 6 | El sistema determina el flujo a ejecutar según el estado de la sesión. |

|  | 7 | El sistema procesa el mensaje y ejecuta el caso de uso correspondiente. |

|  | 8 | El sistema actualiza la sesión con el nuevo estado. |

|  | 9 | El sistema envía la respuesta al cliente por WhatsApp. |

| \*\*Postcondición\*\* | El mensaje ha sido procesado y respondido. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si la firma es inválida, se ignora el mensaje. |

|  | 7a | Si hay error en el procesamiento, se envía mensaje de error genérico. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 3-9 | 3 segundos |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Este caso de uso es el punto de entrada para toda la integración de WhatsApp. |  |



---



\### CU-089 - Validar webhook de WhatsApp



| UC–089 | Validar webhook de WhatsApp |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–10 Integración con WhatsApp |  |

| \*\*Requisitos asociados\*\* | IRQ–12 Información de Sesiones WhatsApp |  |

| \*\*Descripción\*\* | El sistema valida las solicitudes de verificación del webhook de Meta/WhatsApp Cloud API. |  |

| \*\*Precondición\*\* | Se recibe una solicitud GET al endpoint del webhook. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El sistema recibe una solicitud GET con parámetros de verificación. |

|  | 2 | El sistema extrae los parámetros:  hub. mode, hub.verify\_token, hub.challenge.  |

|  | 3 | El sistema verifica que hub.mode sea "subscribe".  |

|  | 4 | El sistema compara hub.verify\_token con el token configurado. |

|  | 5 | Si la verificación es exitosa, el sistema retorna hub.challenge. |

| \*\*Postcondición\*\* | El webhook ha sido verificado y registrado en Meta.  |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 3a | Si hub.mode no es "subscribe", se rechaza la solicitud.  |

|  | 4a | Si el token no coincide, se rechaza la solicitud con error 403. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-5 | Inmediato |

| \*\*Frecuencia\*\* | Rara (solo durante configuración) |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Este proceso se ejecuta una vez durante la configuración inicial del webhook. |  |



---



\### CU-090 - Gestionar sesión de conversación



| UC–090 | Gestionar sesión de conversación |  |

| :---- | : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–10 Integración con WhatsApp |  |

| \*\*Requisitos asociados\*\* | IRQ–12 Información de Sesiones WhatsApp |  |

| \*\*Descripción\*\* | El sistema mantiene el estado de la conversación de cada usuario de WhatsApp, almacenando datos temporales y el paso actual del flujo conversacional. |  |

| \*\*Precondición\*\* | Se está procesando un mensaje de WhatsApp (dentro de CU-088). |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El sistema busca una sesión existente para el número de teléfono. |

|  | 2a | Si existe sesión, el sistema la carga con el estado actual. |

|  | 2b | Si no existe sesión, el sistema crea una nueva con estado inicial. |

|  | 3 | El sistema actualiza el timestamp de última actividad. |

|  | 4 | Durante el procesamiento, el sistema puede actualizar:  |

|  | 4a | Estado actual del flujo conversacional. |

|  | 4b | Datos temporales recolectados (nombre, documento, etc.). |

|  | 4c | ClienteId si el usuario se autentica. |

|  | 5 | El sistema guarda los cambios en la sesión. |

| \*\*Postcondición\*\* | La sesión está actualizada con el estado actual de la conversación. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | - | - |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 1-3 | 500ms |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Las sesiones expiran después de un período de inactividad configurable. |  |



---



\### CU-091 - Mostrar menú de cliente autenticado



| UC–091 | Mostrar menú de cliente autenticado |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–10 Integración con WhatsApp |  |

| \*\*Requisitos asociados\*\* | IRQ–12 Información de Sesiones WhatsApp |  |

| \*\*Descripción\*\* | El sistema presenta al cliente autenticado un menú interactivo con las opciones disponibles:  ver datos, gestionar vehículos, consultar turnos, información del lavadero.  |  |

| \*\*Precondición\*\* | El cliente está registrado y su número está identificado. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El sistema detecta que el cliente está autenticado. |

|  | 2 | El sistema genera el mensaje del menú principal con opciones:  |

|  | 2a | 1. Mis Datos (CU-028) |

|  | 2b | 2. Mis Vehículos (CU-026, CU-040) |

|  | 2c | 3. Mis Turnos (CU-070, CU-071, CU-072) |

|  | 2d | 4. Información del Lavadero (CU-092) |

|  | 2e | 5. Hablar con personal (CU-080) |

|  | 3 | El sistema envía el menú al cliente por WhatsApp. |

|  | 4 | El sistema actualiza la sesión para esperar selección del menú. |

| \*\*Postcondición\*\* | El cliente visualiza el menú principal y puede seleccionar una opción. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | - | - |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-3 | 1 segundo |

| \*\*Frecuencia\*\* | Muy frecuente |  |

| \*\*Estabilidad\*\* | Alta |  |

| \*\*Comentarios\*\* | Este caso de uso es ejecutado automáticamente por el sistema.  |  |



---



\### CU-092 - Mostrar información del lavadero



| UC–092 | Mostrar información del lavadero |  |

| : ---- | :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–10 Integración con WhatsApp, OBJ–11 Gestión de Configuración |  |

| \*\*Requisitos asociados\*\* | IRQ–11 Información de Configuración del Sistema |  |

| \*\*Descripción\*\* | El cliente consulta información del lavadero (nombre, ubicación, horarios, servicios disponibles) a través de WhatsApp. |  |

| \*\*Precondición\*\* | El cliente está en una conversación activa de WhatsApp. |  |

| \*\*Secuencia normal\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 1 | El cliente selecciona "Información del Lavadero" del menú. |

|  | 2 | El sistema obtiene la configuración del lavadero. |

|  | 3 | El sistema genera el mensaje con:  |

|  | 3a | Nombre del lavadero.  |

|  | 3b | Dirección/Ubicación. |

|  | 3c | Teléfono de contacto. |

|  | 3d | Email de contacto. |

|  | 3e | Horarios de atención por día de la semana. |

|  | 4 | El sistema envía la información al cliente. |

|  | 5 | El sistema muestra opción de volver al menú principal. |

| \*\*Postcondición\*\* | El cliente ha visualizado la información del lavadero. |  |

| \*\*Excepciones\*\* | \*\*Paso\*\* | \*\*Acción\*\* |

|  | 2a | Si no hay configuración, el sistema muestra información por defecto. |

| \*\*Rendimiento\*\* | \*\*Paso\*\* | \*\*Cota de tiempo\*\* |

|  | 2-4 | 1 segundo |

| \*\*Frecuencia\*\* | Ocasional |  |

| \*\*Estabilidad\*\* | Media |  |

| \*\*Comentarios\*\* | Esta opción está disponible tanto para clientes registrados como no registrados. |  |



---





\## \*\*Requisitos No Funcionales\*\* {#requisitos-no-funcionales}



| NFR–01 | Seguridad de Autenticación |

| : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–09 Gestión de Seguridad |

| \*\*Requisitos asociados\*\* | CU-001, CU-002, CU-003, CU-004, CU-005 |

| \*\*Descripción\*\* | El sistema debe implementar autenticación segura mediante Firebase Authentication, soportando inicio de sesión con correo/contraseña y Google OAuth 2.0. Las contraseñas deben almacenarse de forma encriptada y las sesiones deben gestionarse mediante cookies seguras con tokens JWT. |

| \*\*Comentarios\*\* | Se requiere verificación de email para cuentas creadas con correo/contraseña. |



| NFR–02 | Protección de Datos |

| :---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–09 Gestión de Seguridad |

| \*\*Requisitos asociados\*\* | Todos los casos de uso |

| \*\*Descripción\*\* | El sistema debe garantizar la protección y confidencialidad de los datos de clientes, empleados y operaciones del lavadero.  Las claves de asociación de vehículos deben almacenarse como hash SHA256.  La comunicación debe realizarse exclusivamente mediante HTTPS. |

| \*\*Comentarios\*\* | Cumplimiento con buenas prácticas de seguridad de la información. |



| NFR–03 | Usabilidad |

| :---- | :---- |

| \*\*Objetivos asociados\*\* | Todos los objetivos |

| \*\*Requisitos asociados\*\* | Todos los casos de uso |

| \*\*Descripción\*\* | El sistema debe ser intuitivo y fácil de usar para los diferentes usuarios (Administrador, Trabajador y Cliente). La interfaz web debe ser responsive y funcionar correctamente en dispositivos móviles, tablets y computadoras de escritorio. Los flujos conversacionales de WhatsApp deben ser claros y guiar al usuario paso a paso. |

| \*\*Comentarios\*\* | Se recomienda seguir principios de diseño UX/UI modernos.  |



| NFR–04 | Registro de Actividad (Auditoría) |

| :---- | : ---- |

| \*\*Objetivos asociados\*\* | OBJ–07 Registro de Auditoría |

| \*\*Requisitos asociados\*\* | CU-084, CU-085, CU-086, CU-087 |

| \*\*Descripción\*\* | El sistema debe contar con un registro automático de todas las acciones realizadas por los usuarios, incluyendo creación, modificación, activación, desactivación, inicio y cierre de sesión.  Cada registro debe incluir identificación del usuario, acción realizada, entidad afectada y marca de tiempo. |

| \*\*Comentarios\*\* | El registro de auditoría es fundamental para control interno y trazabilidad. |



| NFR–05 | Rendimiento |

| : ---- | :---- |

| \*\*Objetivos asociados\*\* | Todos los objetivos |

| \*\*Requisitos asociados\*\* | Todos los casos de uso |

| \*\*Descripción\*\* | El sistema debe responder a las solicitudes de los usuarios en tiempos razonables:  operaciones de lectura en menos de 2 segundos, operaciones de escritura en menos de 3 segundos, y generación de reportes en menos de 10 segundos. La integración con WhatsApp debe responder en menos de 5 segundos. |

| \*\*Comentarios\*\* | Los tiempos pueden variar según la conexión a internet del usuario. |



| NFR–06 | Disponibilidad |

| :---- | :---- |

| \*\*Objetivos asociados\*\* | Todos los objetivos |

| \*\*Requisitos asociados\*\* | Todos los casos de uso |

| \*\*Descripción\*\* | El sistema debe estar disponible el 99% del tiempo durante el horario de operación del lavadero. Se permiten ventanas de mantenimiento programadas fuera del horario de atención. |

| \*\*Comentarios\*\* | La disponibilidad depende también de los servicios de terceros (Firebase, WhatsApp Cloud API). |



| NFR–07 | Escalabilidad |

| :---- | :---- |

| \*\*Objetivos asociados\*\* | Todos los objetivos |

| \*\*Requisitos asociados\*\* | Todos los casos de uso |

| \*\*Descripción\*\* | El sistema debe poder manejar el crecimiento gradual de datos y usuarios sin degradación significativa del rendimiento. La arquitectura basada en Firebase Firestore permite escalabilidad automática. |

| \*\*Comentarios\*\* | El diseño actual está orientado a un único lavadero, pero la arquitectura permite expansión futura. |



| NFR–08 | Compatibilidad de Navegadores |

| : ---- | :---- |

| \*\*Objetivos asociados\*\* | Todos los objetivos relacionados con la interfaz web |

| \*\*Requisitos asociados\*\* | Todos los casos de uso de la aplicación web |

| \*\*Descripción\*\* | El sistema web debe ser compatible con las versiones actuales de los principales navegadores:  Google Chrome, Mozilla Firefox, Microsoft Edge y Safari. Se requiere soporte para JavaScript habilitado. |

| \*\*Comentarios\*\* | Se recomienda mantener el navegador actualizado para mejor experiencia. |



| NFR–09 | Integración con Servicios Externos |

| : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–09, OBJ–10, OBJ–12 |

| \*\*Requisitos asociados\*\* | CU-001, CU-005, CU-025 a CU-028, CU-070 a CU-080, CU-088 a CU-092 |

| \*\*Descripción\*\* | El sistema debe integrarse correctamente con los siguientes servicios externos:  Firebase Authentication (autenticación), Firebase Firestore (base de datos), WhatsApp Cloud API (mensajería), CarQuery API (autocompletado de marcas y modelos de vehículos). |

| \*\*Comentarios\*\* | Se requiere configuración de credenciales y tokens para cada servicio.  |



| NFR–10 | Mantenibilidad del Código |

| :---- | :---- |

| \*\*Objetivos asociados\*\* | Todos los objetivos |

| \*\*Requisitos asociados\*\* | Todos los casos de uso |

| \*\*Descripción\*\* | El código fuente debe seguir principios de arquitectura limpia con separación de capas (Presentación, Servicios, Repositorios, Modelos). Se debe utilizar inyección de dependencias y patrones de diseño que faciliten el mantenimiento y extensión del sistema. |

| \*\*Comentarios\*\* | Se utiliza el patrón MVC con Blazor Server y servicios inyectables.  |



| NFR–11 | Internacionalización |

| :---- | :---- |

| \*\*Objetivos asociados\*\* | Todos los objetivos |

| \*\*Requisitos asociados\*\* | Todos los casos de uso |

| \*\*Descripción\*\* | El sistema debe estar preparado para soportar múltiples idiomas en el futuro, aunque la versión inicial estará en español. Los textos deben ser externalizables para facilitar la traducción. |

| \*\*Comentarios\*\* | La versión 1.0 está en español (Argentina). |



| NFR–12 | Configurabilidad |

| : ---- | :---- |

| \*\*Objetivos asociados\*\* | OBJ–11 Gestión de Configuración del Sistema |

| \*\*Requisitos asociados\*\* | CU-060 a CU-065 |

| \*\*Descripción\*\* | El sistema debe permitir la configuración de parámetros operativos sin necesidad de modificar el código fuente, incluyendo:  horarios de operación, capacidad máxima, tiempos de tolerancia, duración de sesión, información del lavadero y configuración de descuentos. |

| \*\*Comentarios\*\* | La configuración se almacena en Firestore y es editable por el administrador.  |



---



\### Matriz de Rastreabilidad:  Objetivos vs Requisitos de Información



|  | OBJ-01 | OBJ-02 | OBJ-03 | OBJ-04 | OBJ-05 | OBJ-06 | OBJ-07 | OBJ-08 | OBJ-09 | OBJ-10 | OBJ-11 | OBJ-12 |

| : ---- | :----: | :----:  | :----: | :----: | : ----: | :----: | :----:  | :----: | :----: | : ----: | :----: | :----:  |

| IRQ-01 | ✓ |  |  |  |  |  |  |  | ✓ |  |  |  |

| IRQ-02 |  | ✓ |  |  |  |  |  |  |  | ✓ |  | ✓ |

| IRQ-03 |  | ✓ |  |  |  |  |  |  |  | ✓ |  |  |

| IRQ-04 |  |  | ✓ | ✓ |  |  |  |  |  |  |  |  |

| IRQ-05 |  |  | ✓ |  |  |  |  |  |  |  |  |  |

| IRQ-06 |  |  | ✓ |  |  |  |  |  |  |  |  |  |

| IRQ-07 |  |  | ✓ |  |  |  |  |  |  |  |  |  |

| IRQ-08 |  |  |  | ✓ | ✓ |  |  | ✓ |  |  |  |  |

| IRQ-09 |  |  |  |  |  | ✓ |  |  |  | ✓ |  |  |

| IRQ-10 |  |  |  |  |  |  | ✓ |  |  |  |  |  |

| IRQ-11 |  |  |  |  |  | ✓ |  |  |  |  | ✓ |  |

| IRQ-12 |  |  |  |  |  |  |  |  |  | ✓ |  |  |



\### Matriz de Rastreabilidad:  Objetivos vs Casos de Uso (Resumen por Módulo)



|  | OBJ-01 | OBJ-02 | OBJ-03 | OBJ-04 | OBJ-05 | OBJ-06 | OBJ-07 | OBJ-08 | OBJ-09 | OBJ-10 | OBJ-11 | OBJ-12 |

| :---- | : ----: | :----: | :----:  | :----: | :----: | : ----: | :----: | :----:  | :----: | :----: | : ----: | :----: |

| Módulo Seguridad (CU-001 a CU-004) |  |  |  |  |  |  |  |  | ✓ |  |  |  |

| Módulo Gestión Empleados (CU-005 a CU-011) | ✓ |  |  |  |  |  |  |  | ✓ |  |  |  |

| Módulo Clientes y Vehículos (CU-012 a CU-028) |  | ✓ |  |  |  |  |  |  |  | ✓ |  |  |

| Módulo Servicios (CU-029 a CU-044) |  |  | ✓ |  |  |  |  |  |  |  |  |  |

| Módulo Lavados (CU-045 a CU-059) |  |  |  | ✓ | ✓ |  |  |  |  |  |  |  |

| Módulo Configuración (CU-060 a CU-065) |  |  |  |  |  |  |  |  |  |  | ✓ |  |

| Módulo Turnos (CU-066 a CU-075) |  |  |  |  |  | ✓ |  |  |  | ✓ |  |  |

| Módulo Notificaciones (CU-076 a CU-080) |  |  |  |  |  |  |  |  |  | ✓ |  | ✓ |

| Módulo Estadísticas (CU-081 a CU-083. 1) |  |  |  |  |  |  |  | ✓ |  |  |  |  |

| Módulo Auditoría (CU-084 a CU-087) |  |  |  |  |  |  | ✓ |  |  |  |  |  |

| Módulo WhatsApp (CU-088 a CU-092) |  |  |  |  |  |  |  |  |  | ✓ |  |  |



\### Matriz de Rastreabilidad:  Requisitos No Funcionales vs Objetivos



|  | OBJ-01 | OBJ-02 | OBJ-03 | OBJ-04 | OBJ-05 | OBJ-06 | OBJ-07 | OBJ-08 | OBJ-09 | OBJ-10 | OBJ-11 | OBJ-12 |

| :---- | :----: | :----:  | :----: | :----: | : ----: | :----: | :----:  | :----: | :----: | : ----: | :----: | :----:  |

| NFR-01 |  |  |  |  |  |  |  |  | ✓ |  |  |  |

| NFR-02 |  |  |  |  |  |  |  |  | ✓ |  |  |  |

| NFR-03 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

| NFR-04 |  |  |  |  |  |  | ✓ |  |  |  |  |  |

| NFR-05 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

| NFR-06 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

| NFR-07 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

| NFR-08 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |  | ✓ |  |

| NFR-09 |  |  |  |  |  |  |  |  | ✓ | ✓ |  | ✓ |

| NFR-10 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

| NFR-11 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

| NFR-12 |  |  |  |  |  | ✓ |  |  |  |  | ✓ |  |



---



\## \*\*Glosario de Términos\*\* {#glosario-de-términos}



| \*Término\* | \*Categoría\* | \*Descripción\* |

| :---- | : ---- | :---- |

| \*\*Administrador\*\* | Actor | Dueño o gerente del lavadero con acceso completo al sistema.  Puede gestionar empleados, servicios, configuración y acceder a reportes y auditoría. |

| \*\*API\*\* | Tecnología | Application Programming Interface.  Interfaz que permite la comunicación entre diferentes sistemas de software. |

| \*\*Auditoría\*\* | Funcionalidad | Registro automático de todas las acciones realizadas en el sistema para control y trazabilidad. |

| \*\*Blazor Server\*\* | Tecnología | Framework de Microsoft para construir aplicaciones web interactivas usando C# en lugar de JavaScript. |

| \*\*Bot\*\* | Funcionalidad | Programa automatizado que interactúa con los clientes a través de WhatsApp siguiendo flujos conversacionales predefinidos. |

| \*\*Caso de Uso\*\* | Metodología | Descripción de una secuencia de acciones que realiza el sistema para proporcionar un resultado observable de valor a un actor. |

| \*\*Cliente\*\* | Actor/Entidad | Usuario externo del lavadero que solicita servicios de lavado.  Interactúa principalmente a través de WhatsApp. |

| \*\*Clave de Asociación\*\* | Funcionalidad | Código generado por el sistema que permite vincular múltiples clientes a un mismo vehículo.  Se almacena como hash SHA256. |

| \*\*CRUD\*\* | Tecnología | Create, Read, Update, Delete. Operaciones básicas de gestión de datos. |

| \*\*Desactivación\*\* | Funcionalidad | Eliminación lógica de una entidad.  La entidad permanece en la base de datos pero no está disponible para operaciones normales. |

| \*\*Empleado\*\* | Actor | Usuario operativo del sistema con permisos para gestionar clientes, vehículos y lavados. También llamado "Trabajador". |

| \*\*Etapa\*\* | Entidad | Subdivisión de un servicio que permite un seguimiento más granular del progreso del lavado. |

| \*\*Firebase\*\* | Tecnología | Plataforma de desarrollo de Google que proporciona servicios como autenticación (Firebase Authentication) y base de datos (Firestore). |

| \*\*Firestore\*\* | Tecnología | Base de datos NoSQL en la nube de Firebase utilizada para almacenar todos los datos del sistema.  |

| \*\*Flujo Conversacional\*\* | Funcionalidad | Secuencia de mensajes e interacciones guiadas en WhatsApp que permiten al cliente realizar operaciones paso a paso. |

| \*\*Hash SHA256\*\* | Tecnología | Algoritmo criptográfico utilizado para almacenar de forma segura las claves de asociación de vehículos. |

| \*\*Include\*\* | Metodología | Relación entre casos de uso donde un caso de uso incluye obligatoriamente la funcionalidad de otro.  |

| \*\*JWT\*\* | Tecnología | JSON Web Token. Estándar para la creación de tokens de acceso que permiten la autenticación segura.  |

| \*\*Lavado\*\* | Entidad | Registro de un servicio de lavado completo, incluyendo cliente, vehículo, servicios realizados, pagos y tiempos.  |

| \*\*MVC\*\* | Tecnología | Model-View-Controller.  Patrón de arquitectura de software utilizado en el desarrollo del sistema. |

| \*\*OAuth 2.0\*\* | Tecnología | Protocolo de autorización utilizado para la autenticación con Google.  |

| \*\*Paquete\*\* | Entidad | Agrupación de dos o más servicios con un porcentaje de descuento aplicado. |

| \*\*Paginación\*\* | Funcionalidad | División de grandes conjuntos de datos en páginas más pequeñas para mejorar el rendimiento y la usabilidad. |

| \*\*Rol\*\* | Funcionalidad | Nivel de permisos asignado a un usuario. Los roles disponibles son Administrador y Empleado. |

| \*\*Servicio\*\* | Entidad | Tipo de lavado ofrecido por el lavadero, con nombre, descripción, precio y tiempo estimado.  |

| \*\*Sesión\*\* | Funcionalidad | Estado de autenticación de un usuario en el sistema o estado de una conversación de WhatsApp. |

| \*\*Sistema\*\* | Actor | Actor lógico que representa los procesos automáticos del sistema que no requieren intervención humana. |

| \*\*Tipo de Servicio\*\* | Entidad | Categoría para clasificar los servicios (ej: Lavado Exterior, Lavado Interior, Pulido). |

| \*\*Tipo de Vehículo\*\* | Entidad | Clasificación de vehículos (ej: Auto, Camioneta, Moto) que determina servicios disponibles y empleados requeridos. |

| \*\*Trabajador\*\* | Actor | Sinónimo de Empleado. Usuario operativo del sistema.  |

| \*\*Turno\*\* | Entidad | Reserva de un horario específico para realizar un lavado a un cliente. |

| \*\*UP (Unified Process)\*\* | Metodología | Proceso Unificado.  Metodología de desarrollo de software iterativa e incremental utilizada en este proyecto. |

| \*\*Vehículo\*\* | Entidad | Automóvil, camioneta, moto u otro tipo de vehículo registrado en el sistema y asociado a uno o más clientes. |

| \*\*Webhook\*\* | Tecnología | Mecanismo que permite a WhatsApp Cloud API enviar notificaciones al sistema cuando se reciben mensajes. |

| \*\*WhatsApp Cloud API\*\* | Tecnología | API oficial de Meta para integrar WhatsApp en aplicaciones de negocio. |



---



\## \*\*Apéndice:  Diagrama de Casos de Uso por Actor\*\*



\### Figura 2 - Diagrama Caso Usos del actor "Administrador"



```plantuml

@startuml

left to right direction

skinparam packageStyle rectangle

skinparam actorStyle awesome



actor "Administrador" as Admin



rectangle "Casos de Uso del Administrador" {

&nbsp;   package "Gestión de Empleados" {

&nbsp;       usecase "CU-005 Registrarse" as CU005

&nbsp;       usecase "CU-006 Modificar Empleado" as CU006

&nbsp;       usecase "CU-007 Desactivar Empleado" as CU007

&nbsp;       usecase "CU-008 Reactivar Empleado" as CU008

&nbsp;       usecase "CU-009 Consultar Empleados" as CU009

&nbsp;       usecase "CU-010 Asignar Roles" as CU010

&nbsp;   }

&nbsp;   

&nbsp;   package "Gestión de Servicios" {

&nbsp;       usecase "CU-029 Crear Servicio" as CU029

&nbsp;       usecase "CU-030 Modificar Servicio" as CU030

&nbsp;       usecase "CU-031 Desactivar Servicio" as CU031

&nbsp;       usecase "CU-032 Reactivar Servicio" as CU032

&nbsp;       usecase "CU-035 Crear Tipo Servicio" as CU035

&nbsp;       usecase "CU-036 Eliminar Tipo Servicio" as CU036

&nbsp;       usecase "CU-037 Crear Tipo Vehículo" as CU037

&nbsp;       usecase "CU-038 Eliminar Tipo Vehículo" as CU038

&nbsp;       usecase "CU-040 Crear Paquete" as CU040

&nbsp;       usecase "CU-041 Modificar Paquete" as CU041

&nbsp;       usecase "CU-042 Desactivar Paquete" as CU042

&nbsp;       usecase "CU-043 Reactivar Paquete" as CU043

&nbsp;   }

&nbsp;   

&nbsp;   package "Configuración" {

&nbsp;       usecase "CU-060 Configurar Horarios" as CU060

&nbsp;       usecase "CU-061 Configurar Capacidad" as CU061

&nbsp;       usecase "CU-062 Configurar Tiempos" as CU062

&nbsp;       usecase "CU-063 Configurar Sesión" as CU063

&nbsp;       usecase "CU-064 Configurar Info Lavadero" as CU064

&nbsp;       usecase "CU-065 Configurar Descuentos" as CU065

&nbsp;   }

&nbsp;   

&nbsp;   package "Planificación de Turnos" {

&nbsp;       usecase "CU-066 Registrar Turno" as CU066

&nbsp;       usecase "CU-067 Modificar Turno" as CU067

&nbsp;       usecase "CU-068 Consultar Turnos" as CU068

&nbsp;       usecase "CU-069 Cancelar Turno" as CU069

&nbsp;   }

&nbsp;   

&nbsp;   package "Estadísticas y Reportes" {

&nbsp;       usecase "CU-081 Consultar Estadísticas" as CU081

&nbsp;       usecase "CU-082 Consultar Historial Pagos" as CU082

&nbsp;       usecase "CU-083 Generar Reportes" as CU083

&nbsp;   }

&nbsp;   

&nbsp;   package "Auditoría" {

&nbsp;       usecase "CU-084 Consultar Auditoría" as CU084

&nbsp;       usecase "CU-085 Filtrar Auditoría" as CU085

&nbsp;       usecase "CU-086 Ver Detalle Auditoría" as CU086

&nbsp;   }

}



Admin --> CU005

Admin --> CU006

Admin --> CU007

Admin --> CU008

Admin --> CU009

Admin --> CU010

Admin --> CU029

Admin --> CU030

Admin --> CU031

Admin --> CU032

Admin --> CU035

Admin --> CU036

Admin --> CU037

Admin --> CU038

Admin --> CU040

Admin --> CU041

Admin --> CU042

Admin --> CU043

Admin --> CU060

Admin --> CU061

Admin --> CU062

Admin --> CU063

Admin --> CU064

Admin --> CU065

Admin --> CU066

Admin --> CU067

Admin --> CU068

Admin --> CU069

Admin --> CU081

Admin --> CU082

Admin --> CU083

Admin --> CU084

Admin --> CU085

Admin --> CU086



@enduml

```



\### Figura 3 - Diagrama Caso Usos del actor "Trabajador"



```plantuml

@startuml

left to right direction

skinparam packageStyle rectangle

skinparam actorStyle awesome



actor "Trabajador\\n(Empleado)" as Trabajador



rectangle "Casos de Uso del Trabajador" {

&nbsp;   package "Seguridad" {

&nbsp;       usecase "CU-001 Iniciar Sesión" as CU001

&nbsp;       usecase "CU-002 Cerrar Sesión" as CU002

&nbsp;       usecase "CU-003 Recuperar Contraseña" as CU003

&nbsp;   }

&nbsp;   

&nbsp;   package "Gestión de Clientes" {

&nbsp;       usecase "CU-012 Crear Cliente" as CU012

&nbsp;       usecase "CU-013 Modificar Cliente" as CU013

&nbsp;       usecase "CU-016 Consultar Clientes" as CU016

&nbsp;       usecase "CU-017 Buscar Clientes" as CU017

&nbsp;   }

&nbsp;   

&nbsp;   package "Gestión de Vehículos" {

&nbsp;       usecase "CU-018 Crear Vehículo" as CU018

&nbsp;       usecase "CU-019 Modificar Vehículo" as CU019

&nbsp;       usecase "CU-021 Consultar Vehículos" as CU021

&nbsp;       usecase "CU-022 Buscar Vehículos" as CU022

&nbsp;       usecase "CU-023 Vincular Vehículo" as CU023

&nbsp;       usecase "CU-024 Desvincular Vehículo" as CU024

&nbsp;   }

&nbsp;   

&nbsp;   package "Consulta de Servicios" {

&nbsp;       usecase "CU-033 Consultar Servicios" as CU033

&nbsp;       usecase "CU-034 Buscar Servicios" as CU034

&nbsp;       usecase "CU-044 Consultar Paquetes" as CU044

&nbsp;   }

&nbsp;   

&nbsp;   package "Registro de Lavados" {

&nbsp;       usecase "CU-045 Registrar Lavado" as CU045

&nbsp;       usecase "CU-046 Consultar Lavados" as CU046

&nbsp;       usecase "CU-047 Buscar Lavados" as CU047

&nbsp;       usecase "CU-048 Ver Detalle Lavado" as CU048

&nbsp;       usecase "CU-049 Iniciar Servicio" as CU049

&nbsp;       usecase "CU-050 Iniciar Etapa" as CU050

&nbsp;       usecase "CU-051 Finalizar Etapa" as CU051

&nbsp;       usecase "CU-052 Finalizar Servicio" as CU052

&nbsp;       usecase "CU-053 Finalizar Lavado" as CU053

&nbsp;       usecase "CU-054 Cancelar Lavado" as CU054

&nbsp;       usecase "CU-055 Cancelar Servicio" as CU055

&nbsp;       usecase "CU-056 Registrar Pago" as CU056

&nbsp;       usecase "CU-057 Registrar Pago Parcial" as CU057

&nbsp;       usecase "CU-058 Marcar Retirado" as CU058

&nbsp;   }

&nbsp;   

&nbsp;   package "Notificaciones" {

&nbsp;       usecase "CU-076 Notificar WhatsApp" as CU076

&nbsp;       usecase "CU-077 Notificar Email" as CU077

&nbsp;   }

}



Trabajador --> CU001

Trabajador --> CU002

Trabajador --> CU003

Trabajador --> CU012

Trabajador --> CU013

Trabajador --> CU016

Trabajador --> CU017

Trabajador --> CU018

Trabajador --> CU019

Trabajador --> CU021

Trabajador --> CU022

Trabajador --> CU023

Trabajador --> CU024

Trabajador --> CU033

Trabajador --> CU034

Trabajador --> CU044

Trabajador --> CU045

Trabajador --> CU046

Trabajador --> CU047

Trabajador --> CU048

Trabajador --> CU049

Trabajador --> CU050

Trabajador --> CU051

Trabajador --> CU052

Trabajador --> CU053

Trabajador --> CU054

Trabajador --> CU055

Trabajador --> CU056

Trabajador --> CU057

Trabajador --> CU058

Trabajador --> CU076

Trabajador --> CU077



@enduml

```



\### Figura 4 - Diagrama Caso Usos del actor "Cliente"



```plantuml

@startuml

left to right direction

skinparam packageStyle rectangle

skinparam actorStyle awesome



actor "Cliente" as Cliente



rectangle "Casos de Uso del Cliente (WhatsApp)" {

&nbsp;   package "Registro y Datos" {

&nbsp;       usecase "CU-025 Registrarse por WhatsApp" as CU025

&nbsp;       usecase "CU-026 Registrar Vehículo" as CU026

&nbsp;       usecase "CU-028 Editar Datos Personales" as CU028

&nbsp;   }

&nbsp;   

&nbsp;   package "Gestión de Turnos" {

&nbsp;       usecase "CU-070 Solicitar Turno" as CU070

&nbsp;       usecase "CU-071 Consultar Turnos" as CU071

&nbsp;       usecase "CU-072 Cancelar Turno" as CU072

&nbsp;   }

&nbsp;   

&nbsp;   package "Otros" {

&nbsp;       usecase "CU-080 Hablar con Personal" as CU080

&nbsp;       usecase "CU-092 Ver Info Lavadero" as CU092

&nbsp;   }

}



Cliente --> CU025

Cliente --> CU026

Cliente --> CU028

Cliente --> CU070

Cliente --> CU071

Cliente --> CU072

Cliente --> CU080

Cliente --> CU092



@enduml

```



\### Figura 5 - Diagrama Caso Usos del actor "Sistema"



```plantuml

@startuml

left to right direction

skinparam packageStyle rectangle

skinparam actorStyle awesome



actor "Sistema" as Sistema



rectangle "Casos de Uso del Sistema (Automáticos)" {

&nbsp;   package "Seguridad" {

&nbsp;       usecase "CU-004 Cierre Automático" as CU004

&nbsp;       usecase "CU-011 Autenticar Google\\ny Registrar" as CU011

&nbsp;   }

&nbsp;   

&nbsp;   package "Identificación" {

&nbsp;       usecase "CU-027 Identificar Teléfono" as CU027

&nbsp;   }

&nbsp;   

&nbsp;   package "Cálculos" {

&nbsp;       usecase "CU-059 Calcular Duración" as CU059

&nbsp;   }

&nbsp;   

&nbsp;   package "Gestión de Turnos" {

&nbsp;       usecase "CU-073 Asignar sin Superposición" as CU073

&nbsp;       usecase "CU-074 Validar Disponibilidad" as CU074

&nbsp;       usecase "CU-075 Reorganizar Agenda" as CU075

&nbsp;   }

&nbsp;   

&nbsp;   package "Notificaciones Automáticas" {

&nbsp;       usecase "CU-078 Notificar Etapa" as CU078

&nbsp;       usecase "CU-079 Notificar Lavado" as CU079

&nbsp;   }

&nbsp;   

&nbsp;   package "Auditoría" {

&nbsp;       usecase "CU-087 Registrar Acciones" as CU087

&nbsp;   }

&nbsp;   

&nbsp;   package "WhatsApp" {

&nbsp;       usecase "CU-088 Procesar Mensaje" as CU088

&nbsp;       usecase "CU-089 Validar Webhook" as CU089

&nbsp;       usecase "CU-090 Gestionar Sesión" as CU090

&nbsp;       usecase "CU-091 Mostrar Menú" as CU091

&nbsp;   }

}



Sistema --> CU004

Sistema --> CU011

Sistema --> CU027

Sistema --> CU059

Sistema --> CU073

Sistema --> CU074

Sistema --> CU075

Sistema --> CU078

Sistema --> CU079

Sistema --> CU087

Sistema --> CU088

Sistema --> CU089

Sistema --> CU090

Sistema --> CU091



@enduml

```

