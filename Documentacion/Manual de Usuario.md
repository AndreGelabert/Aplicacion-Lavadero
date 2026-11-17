# LavaFácil — Manual de Usuario Integral

**Sistema de Gestión Administrativa y Operativa para Lavaderos de Vehículos**  
*Manual Oficial de Operación, Configuración y Canales de Atención*

---

## Portada e introducción

Bienvenido al **Manual de Usuario Integral de LavaFácil**. Este documento constituye la guía de referencia funcional para la operación diaria, administración, control de inventario y atención a clientes del lavadero.

### ¿A quién está dirigido este manual?

1. **Personal Operativo y de Atención:** Personal del lavadero encargado de la recepción de vehículos, asignación de tareas, ejecución de etapas de lavado, control de consumos de insumos, cobro y entrega.
2. **Personal Administrativo y Encargados:** Usuarios responsables del alta de clientes, catálogo de servicios, paquetes promocionales, control de compras, gestión de proveedores, stock y auditoría.
3. **Administradores del Sistema:** Responsables de la parametrización de la empresa (horarios de atención, capacidades operativas, políticas de sesión), gestión de cuentas de personal y asignación de roles y permisos.
4. **Clientes del Lavadero:** Personas que interactúan con el lavadero mediante el canal oficial de WhatsApp para registrarse, gestionar sus vehículos, consultar información del establecimiento y actualizar sus datos.

### ¿Qué permite hacer el sistema?

LavaFácil centraliza y agiliza la gestión integral del lavadero:
- **Atención y Seguimiento de Lavados:** Registro del ingreso del vehículo, diferenciación de quién entrega y quién retira, asignación de personal disponible, avance secuencial de etapas, registro de consumos de insumos y entrega final.
- **Cobros y Facturación Básica:** Registro de pagos en efectivo, transferencias, tarjetas o billeteras digitales, con soporte para pagos parciales o totales.
- **Gestión de Clientes y Vehículos:** Base de datos unificada de clientes con canales de notificación personalizados y vinculación de múltiples titulares por vehículo mediante claves de asociación seguras.
- **Catálogo de Servicios y Paquetes:** Definición de servicios segmentados por tipo de vehículo, división en etapas operativas, recetas de insumos asociados y combos o paquetes con descuentos programados.
- **Control de Inventario, Stock y Compras:** Monitoreo en tiempo real del stock físico, reservado y disponible; alertas de stock crítico; registro auditable de movimientos en Kardex; solicitudes de cotización a proveedores con cuadro comparativo; y ciclo de órdenes de compra con recepción total o parcial.
- **Comunicación Multicanal:** Despacho automatizado de notificaciones por Correo Electrónico y WhatsApp ante avances, cancelaciones o finalización de trabajos.
- **Bot Interactivo de WhatsApp:** Autoservicio para clientes que permite el registro autónomo, alta y edición de vehículos, asociación por clave y actualización de preferencias.
- **Seguridad y Auditoría:** Matriz de permisos granulares por módulo y registro histórico comparativo ("antes" y "después") de todas las operaciones sensibles del sistema.

### ¿Cómo consultar este manual?

- Si es su **primer ingreso**, comience por [1. Primeros pasos](#1-primeros-pasos) y [2. Roles y permisos](#2-roles-y-permisos).
- Si trabaja en la **playa de lavado o recepción**, consulte [3. Gestión de clientes y vehículos](#3-gestión-de-clientes-y-vehículos) y [5. Gestión de lavados](#5-gestión-de-lavados).
- Si administra **compras o inventario**, diríjase a [7. Insumos, stock y compras](#7-insumos-stock-y-compras).
- Si desea compartir instrucciones con un **cliente para usar WhatsApp**, consulte la sección independiente [9. Uso de WhatsApp para clientes](#9-uso-de-whatsapp-para-clientes).
- Si experimenta una traba o mensaje inesperado, revise [10. Problemas frecuentes y recomendaciones](#10-problemas-frecuentes-y-recomendaciones).

---

## Índice

- [Portada e introducción](#portada-e-introducción)
- [Índice](#índice)
- [1. Primeros pasos](#1-primeros-pasos)
  - [1.1 Acceso al sistema](#11-acceso-al-sistema)
  - [1.2 Registro de nuevo personal](#12-registro-de-nuevo-personal)
  - [1.3 Recuperación de contraseña](#13-recuperación-de-contraseña)
  - [1.4 Entorno de trabajo y navegación general](#14-entorno-de-trabajo-y-navegación-general)
  - [1.5 Políticas de sesión y advertencias de expiración](#15-políticas-de-sesión-y-advertencias-de-expiración)
  - [1.6 Cierre de sesión](#16-cierre-de-sesión)
  - [1.7 Acceso denegado](#17-acceso-denegado)
  - [1.8 Convenciones comunes en la interfaz](#18-convenciones-comunes-en-la-interfaz)
- [2. Roles y permisos](#2-roles-y-permisos)
  - [2.1 Roles base del sistema](#21-roles-base-del-sistema)
  - [2.2 Catálogo modular de permisos](#22-catálogo-modular-de-permisos)
  - [2.3 Visibilidad dinámica de funciones](#23-visibilidad-dinámica-de-funciones)
  - [2.4 Consulta ante accesos restringidos](#24-consulta-ante-accesos-restringidos)
- [3. Gestión de clientes y vehículos](#3-gestión-de-clientes-y-vehículos)
  - [3.1 Consulta y búsqueda de clientes](#31-consulta-y-búsqueda-de-clientes)
  - [3.2 Procedimiento: Registrar un nuevo cliente](#32-procedimiento-registrar-un-nuevo-cliente)
  - [3.3 Procedimiento: Editar información de un cliente](#33-procedimiento-editar-información-de-un-cliente)
  - [3.4 Reglas y validaciones de datos del cliente](#34-reglas-y-validaciones-de-datos-del-cliente)
  - [3.5 Configuración de preferencias de notificación](#35-configuración-de-preferencias-de-notificación)
  - [3.6 Desactivación y reactivación de clientes](#36-desactivación-y-reactivación-de-clientes)
  - [3.7 Consulta y administración de vehículos](#37-consulta-y-administración-de-vehículos)
  - [3.8 Procedimiento: Editar un vehículo existente](#38-procedimiento-editar-un-vehículo-existente)
  - [3.9 Clave de asociación y múltiples dueños por vehículo](#39-clave-de-asociación-y-múltiples-dueños-por-vehículo)
  - [3.10 Desactivación de vehículos](#310-desactivación-de-vehículos)
  - [3.11 Revisión de propuestas de vehículos](#311-revisión-de-propuestas-de-vehículos)
- [4. Servicios y paquetes](#4-servicios-y-paquetes)
  - [4.1 Consulta del catálogo de servicios](#41-consulta-del-catálogo-de-servicios)
  - [4.2 Procedimiento: Registrar o editar un servicio](#42-procedimiento-registrar-o-editar-un-servicio)
  - [4.3 Etapas del servicio](#43-etapas-del-servicio)
  - [4.4 Recetas de insumos asociadas al servicio](#44-recetas-de-insumos-asociadas-al-servicio)
  - [4.5 Tipos de servicio y tipos de vehículo](#45-tipos-de-servicio-y-tipos-de-vehículo)
  - [4.6 Consulta y gestión de paquetes de servicios](#46-consulta-y-gestión-de-paquetes-de-servicios)
  - [4.7 Procedimiento: Registrar o editar un paquete](#47-procedimiento-registrar-o-editar-un-paquete)
  - [4.8 Reglas y restricciones de paquetes](#48-reglas-y-restricciones-de-paquetes)
  - [4.9 Desactivación y reactivación de servicios y paquetes](#49-desactivación-y-reactivación-de-servicios-y-paquetes)
- [5. Gestión de lavados](#5-gestión-de-lavados)
  - [5.1 Vista general y panel de lavados](#51-vista-general-y-panel-de-lavados)
  - [5.2 Validación de condiciones operativas antes de crear](#52-validación-de-condiciones-operativas-antes-de-crear)
  - [5.3 Procedimiento: Registrar un nuevo lavado](#53-procedimiento-registrar-un-nuevo-lavado)
  - [5.4 Roles de personas asociadas: Quién trae y quién retira](#54-roles-de-personas-asociadas-quién-trae-y-quién-retira)
  - [5.5 Pantalla de detalle y seguimiento operativo](#55-pantalla-de-detalle-y-seguimiento-operativo)
  - [5.6 Procedimiento: Iniciar un servicio o etapa](#56-procedimiento-iniciar-un-servicio-o-etapa)
  - [5.7 Procedimiento: Finalizar un servicio o etapa](#57-procedimiento-finalizar-un-servicio-o-etapa)
  - [5.8 Procedimiento: Cancelar un lavado, servicio o etapa](#58-procedimiento-cancelar-un-lavado-servicio-o-etapa)
  - [5.9 Finalización parcial de lavados](#59-finalización-parcial-de-lavados)
  - [5.10 Procedimiento: Registrar pagos](#510-procedimiento-registrar-pagos)
  - [5.11 Procedimiento: Registrar retiro del vehículo](#511-procedimiento-registrar-retiro-del-vehículo)
- [6. Notificaciones](#6-notificaciones)
  - [6.1 Canales y resolución de destinatarios](#61-canales-y-resolución-de-destinatarios)
  - [6.2 Eventos que generan notificación automática](#62-eventos-que-generan-notificación-automática)
  - [6.3 Contenido de los mensajes enviados](#63-contenido-de-los-mensajes-enviados)
  - [6.4 Avisos y resúmenes para el personal](#64-avisos-y-resúmenes-para-el-personal)
  - [6.5 Comprensión de estados: Aceptado, Fallido e Indeterminado](#65-comprensión-de-estados-aceptado-fallido-e-indeterminado)
  - [6.6 Pautas de actuación ante resultados no exitosos](#66-pautas-de-actuación-ante-resultados-no-exitosos)
- [7. Insumos, stock y compras](#7-insumos-stock-y-compras)
  - [7.1 Inventario de insumos y estados de stock](#71-inventario-de-insumos-y-estados-de-stock)
  - [7.2 Alertas y monitoreo de stock crítico](#72-alertas-y-monitoreo-de-stock-crítico)
  - [7.3 Procedimiento: Registrar o editar un insumo](#73-procedimiento-registrar-o-editar-un-insumo)
  - [7.4 Procedimiento: Ajustar stock manualmente](#74-procedimiento-ajustar-stock-manualmente)
  - [7.5 Consulta de movimientos en Kardex](#75-consulta-de-movimientos-en-kardex)
  - [7.6 Gestión de proveedores](#76-gestión-de-proveedores)
  - [7.7 Solicitudes de cotización (RFQ) y comparativas](#77-solicitudes-de-cotización-rfq-y-comparativas)
  - [7.8 Procedimiento: Crear y enviar una solicitud de cotización](#78-procedimiento-crear-y-enviar-una-solicitud-de-cotización)
  - [7.9 Procedimiento: Registrar respuesta y adjudicar cotización](#79-procedimiento-registrar-respuesta-y-adjudicar-cotización)
  - [7.10 Ciclo de órdenes de compra](#710-ciclo-de-órdenes-de-compra)
  - [7.11 Procedimiento: Aprobar o modificar una orden pendiente](#711-procedimiento-aprobar-o-modificar-una-orden-pendiente)
  - [7.12 Procedimiento: Recepcionar mercadería (total o parcial)](#712-procedimiento-recepcionar-mercadería-total-o-parcial)
  - [7.13 Directiva de reabastecimiento](#713-directiva-de-reabastecimiento)
- [8. Personal, roles y administración](#8-personal-roles-y-administración)
  - [8.1 Gestión del personal](#81-gestión-del-personal)
  - [8.2 Procedimiento: Modificar rol o estado de un empleado](#82-procedimiento-modificar-rol-o-estado-de-un-empleado)
  - [8.3 Administración de roles y matriz de permisos](#83-administración-de-roles-y-matriz-de-permisos)
  - [8.4 Procedimiento: Crear o editar un rol personalizado](#84-procedimiento-crear-o-editar-un-rol-personalizado)
  - [8.5 Configuración general del lavadero](#85-configuración-general-del-lavadero)
  - [8.6 Configuración de horarios de operación y su impacto](#86-configuración-de-horarios-de-operación-y-su-impacto)
  - [8.7 Consulta y análisis de registros de auditoría](#87-consulta-y-análisis-de-registros-de-auditoría)
- [9. Uso de WhatsApp para clientes](#9-uso-de-whatsapp-para-clientes)
  - [9.1 Inicio de contacto](#91-inicio-de-contacto)
  - [9.2 Flujo guiado de registro inicial](#92-flujo-guiado-de-registro-inicial)
  - [9.3 Menú principal del cliente](#93-menú-principal-del-cliente)
  - [9.4 Gestión de vehículos desde WhatsApp](#94-gestión-de-vehículos-desde-whatsapp)
  - [9.5 Asociación de vehículo existente mediante clave](#95-asociación-de-vehículo-existente-mediante-clave)
  - [9.6 Consulta y edición de datos personales](#96-consulta-y-edición-de-datos-personales)
  - [9.7 Consulta de información institucional](#97-consulta-de-información-institucional)
  - [9.8 Comandos especiales de navegación y ayuda](#98-comandos-especiales-de-navegación-y-ayuda)
  - [9.9 Alcance y limitaciones del canal de WhatsApp](#99-alcance-y-limitaciones-del-canal-de-whatsapp)
- [10. Problemas frecuentes y recomendaciones](#10-problemas-frecuentes-y-recomendaciones)
- [11. Glosario de términos](#11-glosario-de-términos)

---

## 1. Primeros pasos

### 1.1 Acceso al sistema

El acceso a la aplicación web de LavaFácil se realiza a través del navegador web institucional mediante la pantalla de inicio de sesión (`/Login`).

Existen dos vías de autenticación disponibles:

1. **Autenticación mediante Correo y Contraseña:**
   - Ingrese su dirección de correo electrónico registrada y su contraseña.
   - Presione el botón **Iniciar sesión**.
2. **Autenticación con Cuenta de Google:**
   - Si su cuenta de correo corporativa está asociada a Google, presione el botón **Iniciar sesión con Google**.
   - Seleccione o confirme su cuenta en la ventana emergente de Google. El sistema validará su identidad y cargará su perfil.

> [!NOTE]
> Las direcciones de acceso a la aplicación, así como el alta de nuevos usuarios administradores, deben ser solicitadas formalmente al responsable técnico o de sistemas del establecimiento.

### 1.2 Registro de nuevo personal

Los nuevos colaboradores pueden solicitar su cuenta directamente desde la pantalla de login presionando la opción para registrarse:

1. En la ventana emergente de registro, complete:
   - **Nombre completo.**
   - **Correo electrónico.**
   - **Contraseña** y **Confirmación de contraseña.**
2. Presione **Registrarse**.
3. El sistema creará la cuenta y enviará un correo de verificación.
4. Antes de poder iniciar sesión, el usuario debe abrir el enlace de confirmación recibido en su casilla de correo.
5. Una vez verificado el correo, el usuario deberá aguardar que un administrador le asigne el rol correspondiente desde el módulo de Personal para acceder a las pantallas operativas.

### 1.3 Recuperación de contraseña

En caso de olvidar la contraseña de acceso:

1. En la pantalla de login, haga clic en el enlace **¿Olvidaste tu contraseña?**.
2. Escriba su dirección de correo electrónico registrada.
3. Presione el botón de confirmación para enviar la solicitud.
4. Si la cuenta existe en el sistema, recibirá un mensaje con las instrucciones y el enlace seguro para restablecer su clave.

### 1.4 Entorno de trabajo y navegación general

Una vez autenticado, el sistema presenta una interfaz estructurada en tres zonas principales:

1. **Barra Superior (Navbar):**
   - **Botón de Menú:** Permite desplegar u ocultar la barra lateral en pantallas pequeñas o dispositivos móviles.
   - **Identidad del Lavadero:** Nombre comercial configurado para el establecimiento.
   - **Indicador de Stock Crítico:** Si existen insumos cuyo stock es inferior o igual al mínimo definido, aparece un aviso destacado en rojo que permite acceder directamente al inventario filtrado.
   - **Selector de Tema:** Permite alternar entre el modo visual Claro y Oscuro según la comodidad del operador.
   - **Menú de Usuario:** Muestra el nombre, correo electrónico y rol asignado al operador en sesión, junto con el botón de cierre de sesión.
2. **Barra Lateral de Navegación (Sidebar):**
   - Presenta los accesos directos a los módulos a los que el usuario tiene autorización según sus permisos.
   - Los módulos se presentan en el orden operativo establecido:
     1. **Lavados**
     2. **Clientes**
     3. **Vehículos**
     4. **Servicios**
     5. **Paquetes**
     6. **Personal**
     7. **Auditoría**
     8. **Configuración**
     9. **Roles y Permisos**
     10. **Insumos y Stock**
3. **Área Principal de Contenido:**
   - Espacio central donde se cargan las tablas, paneles de control, filtros y formularios de cada módulo.

### 1.5 Políticas de sesión y advertencias de expiración

Para garantizar la seguridad de la información del establecimiento:

- **Sesión No Persistente:** Por motivos de seguridad operativa, las sesiones no se guardan de forma permanente en el navegador. Al cerrar todas las ventanas del navegador, la sesión se finaliza automáticamente.
- **Tiempo Máximo de Sesión:** Cada inicio de sesión tiene una duración máxima configurada por el administrador (por defecto, 8 horas / 480 minutos). Una vez cumplido este plazo continuo, el sistema solicitará volver a autenticarse.
- **Tiempo Máximo de Inactividad:** Si el operador no realiza ninguna interacción (clic, escritura, desplazamiento) durante el período de inactividad configurado (por defecto, 15 minutos), la sesión expira.
- **Aviso Preventivo Flotante:** Cinco (5) minutos antes de que ocurra la expiración por inactividad o duración máxima, aparece en el extremo inferior derecho una notificación de advertencia en color amarillo con un temporizador regresivo. Si el usuario realiza cualquier acción física en la pantalla, el sistema registra la actividad y actualiza la vigencia de la sesión automáticamente.

> [!WARNING]
> Si la sesión expira mientras se encuentra completando un formulario extenso sin guardar, los datos no confirmados podrían perderse al redirigirse a la pantalla de login. Preste atención al aviso preventivo.

### 1.6 Cierre de sesión

Para cerrar su sesión de manera segura:
1. Haga clic en el ícono de perfil de usuario ubicado en la esquina superior derecha de la barra de navegación.
2. En el menú desplegable, haga clic en el botón **Cerrar sesión**.
3. El sistema revocará el identificador de sesión activo, eliminará las credenciales temporales y lo redirigirá a la pantalla de ingreso.

### 1.7 Acceso denegado

Si intenta ingresar a una sección, enlace o acción para la cual su cuenta no posee permisos autorizados:
- El sistema bloqueará la petición y mostrará la pantalla de **Acceso denegado** (`/Home/AccessDenied`).
- Esta pantalla le indicará de forma clara que no posee los permisos requeridos.
- Si dispone de otros módulos habilitados, tendrá a disposición el botón **Ir a mi página inicial** para regresar a su área autorizada.
- También dispondrá del botón **Cerrar sesión** si necesita ingresar con otra cuenta de usuario.

### 1.8 Convenciones comunes en la interfaz

En todos los módulos del sistema encontrará las siguientes convenciones estándar:

- **Búsqueda Dinámica:** Las cajas de texto de búsqueda aplican filtros automáticos sobre los registros de la tabla sin necesidad de recargar la página completa.
- **Paneles de Filtro Avanzado:** Botones identificados con la etiqueta **Filtros** que despliegan casillas de verificación para segmentar información por estado (Activo/Inactivo), tipos, fechas o rangos numéricos. Los botones **Aplicar Filtros** y **Limpiar filtros** permiten actualizar o restablecer las listas.
- **Paginación:** Las listas que superan el tamaño de página configurado presentan controles numéricos de página o botones de navegación relativa (anterior / siguiente).
- **Formularios Plegables (Acordeones):** En módulos como Clientes, Servicios o Insumos, los formularios de alta y edición se encuentran integrados en un encabezado desplegable que se expande al hacer clic sobre él o al presionar un botón de creación/edición.
- **Campos Obligatorios:** Todos los campos que requieren ingreso obligatorio están señalizados con un asterisco rojo (`*`).
- **Validación en Vivo:** Si un dato no respeta el formato exigido (por ejemplo, un teléfono con longitud incorrecta o un correo inválido), se muestra un mensaje de error en color rojo inmediatamente debajo del campo.

---

## 2. Roles y permisos

LavaFácil implementa un esquema de control de acceso basado en roles con permisos granulares. Esto significa que la interfaz se adapta dinámicamente: cada persona usuaria solo visualiza los módulos y botones para los cuales su cuenta tiene autorización explícita.

### 2.1 Roles base del sistema

El sistema cuenta con dos roles protegidos predeterminados que no pueden ser eliminados ni renombrados:

1. **Administrador (`rol-administrador`):**
   - Posee acceso irrestricto y completo a todas las funcionalidades del sistema (catálogo completo de permisos).
   - Posee permisos críticos protegidos (`Roles.Consultar`, `Roles.Crear`, `Roles.Modificar`, `Roles.Eliminar`) que no pueden serle retirados bajo ninguna circunstancia, evitando bloqueos accidentales de administración.
2. **Empleado (`rol-empleado`):**
   - Rol de perfil operativo diseñado para el trabajo diario en la recepción y nave de lavado.
   - Incluye de manera predeterminada los siguientes permisos:
     - **Clientes:** Consultar, Crear y Modificar.
     - **Vehículos:** Consultar, Crear y Modificar.
     - **Servicios:** Consultar.
     - **Paquetes:** Consultar.
     - **Lavados:** Consultar, Crear y Modificar.
     - **Insumos:** Consultar y Recepcionar (permite verificar stock y dar ingreso a mercadería de órdenes de compra recibidas).

Adicionalmente, el personal administrativo puede definir **Roles Personalizados** (por ejemplo: Encargado de Compras, Supervisor de Calidad, Auditor) asignándoles combinaciones específicas de permisos.

### 2.2 Catálogo modular de permisos

Los permisos del sistema se configuran mediante la nomenclatura `Módulo.Acción`. El catálogo oficial incluye:

| Módulo | Acciones Disponibles | Descripción Funcional |
| :--- | :--- | :--- |
| **Clientes** | `Consultar`, `Crear`, `Modificar`, `Eliminar` | Consulta de listados, alta de clientes, edición de datos/preferencias y desactivación lógica. |
| **Vehículos** | `Consultar`, `Crear`, `Modificar`, `Eliminar` | Visualización de vehículos y dueños asociados, creación desde cliente, edición técnica y desactivación sin clientes vinculados. |
| **Servicios** | `Consultar`, `Crear`, `Modificar`, `Eliminar` | Consulta del catálogo, creación de servicios, configuración de etapas/recetas y desactivación. |
| **Paquetes** | `Consultar`, `Crear`, `Modificar`, `Eliminar` | Consulta, creación de combos promocionales, definición de descuentos y desactivación. |
| **Lavados** | `Consultar`, `Crear`, `Modificar` | Visualización de lavados, registro de ingresos, avance de etapas, consumos, cobro, retiro y cancelaciones. |
| **Personal** | `Consultar`, `Modificar` | Listado de empleados, asignación y cambio de roles, y activación o desactivación de cuentas. |
| **Configuración** | `Consultar`, `Modificar` | Consulta y actualización de parámetros globales del lavadero, horarios comerciales y directivas. |
| **Auditoría** | `Consultar` | Búsqueda y visualización detallada del historial de cambios antes/después del sistema. |
| **Roles** | `Consultar`, `Crear`, `Modificar`, `Eliminar` | Gestión completa de la matriz de roles y permisos del personal. |
| **Insumos** | `Consultar`, `Crear`, `Modificar`, `Eliminar`, `AjustarStock`, `Recepcionar`, `Kardex` | Catálogo de productos, proveedores, órdenes, cotizaciones, ajustes manuales, recepción de compras e historial Kardex. |

### 2.3 Visibilidad dinámica de funciones

El menú lateral y las opciones en pantalla no son estáticos:
- Si un usuario no tiene permiso para consultar un módulo (por ejemplo, `Auditoria.Consultar`), el elemento no aparecerá en el menú lateral.
- Si un usuario posee permiso de creación pero no de consulta amplia (por ejemplo, `Lavados.Crear` o `Clientes.Crear`), el acceso al módulo estará habilitado en el menú y abrirá directamente el formulario de registro correspondiente.
- Los botones de acción dentro de las tablas (tales como editar, anular, registrar pago o ajustar stock) se ocultan automáticamente si la cuenta no cuenta con el permiso puntual para ejecutar dicha operación.

> [!IMPORTANT]
> No asuma que todas las personas que comparten un mismo rol verán idéntica pantalla: los administradores pueden crear nuevos roles o ajustar los permisos de un rol existente, afectando las funciones visibles para los usuarios asignados a él.

### 2.4 Consulta ante accesos restringidos

Si durante su jornada de trabajo necesita realizar una operación (por ejemplo, registrar una orden de compra o cancelar un lavado) y el botón o la pantalla no se encuentran visibles:
1. Compruebe en su menú de perfil (esquina superior derecha) el rol que tiene asignado actualmente.
2. Contacte al **Administrador del Sistema** o responsable de personal de su lavadero.
3. Solicite la revisión de los permisos asignados a su rol o la reasignación de su cuenta al rol que corresponda a sus nuevas tareas.

---

## 3. Gestión de clientes y vehículos

El módulo de **Clientes** permite administrar el padrón de clientes del lavadero, sus canales de notificación preferidos y los vehículos que tienen a su cargo.

### 3.1 Consulta y búsqueda de clientes

**Quién puede realizarla:** Usuarios con permiso `Clientes.Consultar`.

En la pantalla principal de Clientes (`/Cliente`):
- **Búsqueda:** Puede buscar clientes en tiempo real ingresando texto en el campo de búsqueda. El sistema busca coincidencias por nombre, apellido, número de documento, teléfono o correo electrónico.
- **Filtro por Estado:** Mediante el botón de filtros puede elegir ver clientes **Activos**, **Inactivos** o ambos simultáneamente.
- **Detalle del Cliente:** Al hacer clic en el botón de ver detalle (ícono de ojo) de una fila, se abrirá una ventana emergente que exhibe la información de contacto completa del cliente y el listado de todos los vehículos que tiene vinculados.

---

### 3.2 Procedimiento: Registrar un nuevo cliente

**Quién puede realizarla:** Usuarios con permisos `Clientes.Crear` y `Vehiculos.Crear`.

**Antes de empezar:** Tenga a mano el documento de identidad del cliente, sus datos de contacto y los datos de al menos un vehículo a su cargo (patente, marca, modelo, tipo y color).

**Pasos:**

1. Ingrese a la sección **Clientes** desde el menú lateral.
2. Si el formulario no está visible, despliegue el acordeón superior titulado **Registrando un Cliente**.
3. Complete los datos de identificación y contacto del cliente:
   - **Tipo de Documento:** Seleccione el tipo de la lista (por ejemplo, DNI). Si requiere un tipo nuevo y cuenta con permiso, puede crearlo con el botón `+`.
   - **Número de Documento:** Ingrese únicamente números (sin puntos ni espacios).
   - **Nombre:** Ingrese el nombre (mínimo 3 letras por palabra).
   - **Apellido:** Ingrese el apellido (mínimo 3 letras por palabra).
   - **Teléfono:** Ingrese el número telefónico compuesto por exactamente 10 dígitos numéricos (código de área sin 0 y número sin 15, ej: `1123456789`).
   - **Email:** Ingrese una dirección de correo electrónico válida (con al menos 3 caracteres en el dominio posterior al `@`).
4. Seleccione las **Preferencias de notificación**:
   - Marque **WhatsApp**, **Email** o ambas casillas según lo solicitado por el cliente.
   - *Validación:* Es obligatorio dejar marcado al menos uno de los dos métodos.
5. Incorpore al menos un vehículo para el cliente:
   - En la sección **Vehículos del Cliente**, haga clic en el botón **Agregar Vehículo**.
   - Se abrirá una ventana modal de carga rápida de vehículo.
   - Ingrese la **Patente** (letras, números o guiones).
   - Seleccione el **Tipo de Vehículo** (ej: Automóvil, SUV, Camioneta, Motocicleta).
   - Seleccione o ingrese la **Marca**, el **Modelo** y el **Color**.
   - Presione el botón de agregar en la ventana modal. El vehículo quedará listado en la sección inferior de vehículos seleccionados.
6. Revise que la información sea correcta y presione el botón **Registrar** (ícono de verificación verde).

**Resultado esperado:**
El sistema valida la inexistencia de duplicados de documento y patente, guarda el cliente y su vehículo en la base de datos de manera conjunta, y muestra un mensaje de éxito. Si se generó una nueva clave de asociación para el vehículo, se exhibirá en pantalla.

**Situaciones a tener en cuenta:**
- Si ingresa un número de documento que ya pertenece a otro cliente activo, el formulario será rechazado indicando: *"Ya existe un cliente con este documento"*.
- El sistema no permite guardar un cliente sin al menos un vehículo vinculado.

---

### 3.3 Procedimiento: Editar información de un cliente

**Quién puede realizarla:** Usuarios con permiso `Clientes.Modificar`.

**Antes de empezar:** Identifique al cliente en la tabla de clientes.

**Pasos:**

1. Localice al cliente en la tabla y presione el botón de **Editar** (ícono de lápiz).
2. El formulario superior se cargará con los datos del cliente y cambiará su título a **Editar Cliente**.
3. Modifique los campos necesarios:
   - Nombre o Apellido.
   - Teléfono o Correo electrónico.
   - Preferencias de notificación (WhatsApp / Email).
   - Agregar o desvincular vehículos asignados.
4. Presione el botón **Guardar**.

**Resultado esperado:**
Los datos del cliente se actualizan inmediatamente y se genera un registro en la auditoría del sistema detallando los campos modificados.

**Situaciones a tener en cuenta:**
- **Inmutabilidad del Documento:** Por seguridad jurídica y consistencia del historial, el **Tipo de Documento** y el **Número de Documento** quedan bloqueados (no editables) una vez creado el cliente. Si hubo un error en la carga del documento, se debe desactivar el cliente y crearlo correctamente.

---

### 3.4 Reglas y validaciones de datos del cliente

- **Teléfono:** Obligatorio. Debe contener estrictamente **10 dígitos numéricos**, sin guiones, paréntesis ni prefijos internacionales (`+54` o `9`).
- **Email:** Obligatorio. Formato de correo válido con al menos 3 caracteres en el dominio luego del `@` (ejemplo válido: `cliente@gmail.com`).
- **Nombres y Apellidos:** Solo se admiten letras (incluyendo acentos, diéresis y la letra ñ) y espacios. Cada palabra debe contar con un mínimo de 3 caracteres. No se admiten números ni símbolos especiales.
- **Unicidad:** La combinación de Tipo de Documento y Número de Documento no puede repetirse entre clientes.

### 3.5 Configuración de preferencias de notificación

LavaFácil respeta la voluntad del cliente sobre por qué medios desea recibir actualizaciones sobre el estado de su vehículo:
- **WhatsApp:** Si está marcado, el sistema remitirá mensajes automáticos al número telefónico registrado cuando se produzcan eventos en sus lavados.
- **Email:** Si está marcado, se enviarán correos electrónicos a la casilla informada.
- **Ambos:** El cliente recibirá las alertas en ambos canales en paralelo.
- **Regla estricta:** No es posible desmarcar ambas opciones simultáneamente. Si se intenta guardar un cliente con ambas casillas desmarcadas, el sistema rechazará la acción con el mensaje: *"Debe seleccionar al menos un método de notificación"*.

### 3.6 Desactivación y reactivación de clientes

- **Desactivar Cliente:** Requiere permiso `Clientes.Eliminar`. Se realiza presionando el botón de desactivar (ícono de papelera) en la fila del cliente. Esta operación aplica un borrado lógico (el estado cambia a `Inactivo`); la información histórica y los lavados pasados se conservan intactos.
- **Reactivar Cliente:** Requiere permiso `Clientes.Modificar`. Si un cliente inactivo regresa al lavadero, filtre por estado "Inactivo" en la tabla y presione el botón de reactivación para restablecer su estado a `Activo`.

---

### 3.7 Consulta y administración de vehículos

El módulo de **Vehículos** (`/Vehiculo`) permite consultar el parque automotor registrado en el sistema:
- **Filtros Avanzados:** Permite filtrar por tipo de vehículo (Automóvil, Camioneta, SUV, etc.), marca, color y estado (Activo/Inactivo).
- **Asociados:** Al presionar el botón de personas asociadas en la tabla, el sistema abre la ventana **Clientes Asociados** (`_Asociados.cshtml`), mostrando quién es el titular principal del vehículo y qué otros clientes registrados figuran como autorizados para operar con esa patente.

### 3.8 Procedimiento: Editar un vehículo existente

**Quién puede realizarla:** Usuarios con permiso `Vehiculos.Modificar`.

**Antes de empezar:** Ubique el vehículo por patente en el módulo de Vehículos.

**Pasos:**
1. En la tabla de vehículos, haga clic en el botón de **Editar** (ícono de lápiz).
2. El formulario lateral o superior se abrirá con el título **Editar Vehículo**.
3. Puede corregir:
   - **Tipo de Vehículo:** Selección de la categoría vehicular.
   - **Marca y Modelo:** Datos descriptivos del rodado.
   - **Color:** Color predominante del vehículo.
4. Presione el botón **Guardar**.

**Resultado esperado:**
El vehículo actualiza sus datos en toda la plataforma. Todos los lavados futuros tomarán la categoría actualizada.

---

### 3.9 Clave de asociación y múltiples dueños por vehículo

LavaFácil contempla que un mismo vehículo familiar, corporativo o compartido sea llevado o retirado por distintas personas:
- **Primer Registro (Titular):** Cuando un vehículo se registra por primera vez en el sistema (ya sea desde la web o mediante WhatsApp), el sistema genera automáticamente una **Clave de Asociación de 6 caracteres** (alfanumérica en mayúsculas).
- **Asociación de Nuevos Dueños:** Cualquier otro cliente registrado que conozca la patente y dicha clave de 6 caracteres puede asociar el vehículo a su perfil (desde WhatsApp o al ser atendido en el lavadero) sin necesidad de duplicar el vehículo en la base de datos.
- **Seguridad:** En la base de datos solo se almacena un resumen criptográfico irreversible (Hash SHA-256) de la clave. La clave en texto plano solo se visualiza en el momento de su generación o cuando el cliente solicita consultarla o regenerarla desde WhatsApp.

### 3.10 Desactivación de vehículos

**Quién puede realizarla:** Usuarios con permiso `Vehiculos.Eliminar`.

Un vehículo puede ser desactivado únicamente si **no posee clientes activos asociados** en el sistema:
- Si el vehículo aún figura en el perfil de uno o más clientes activos, el sistema impedirá su desactivación para proteger la integridad operativa. Primero se debe desvincular de los clientes correspondientes.

### 3.11 Revisión de propuestas de vehículos

Cuando los clientes interactúan por WhatsApp y su marca o modelo de auto no figura en el catálogo oficial de CarQuery, el bot les permite ingresarlo manualmente. Dicha carga genera una propuesta pendiente en el sistema.

**Quién puede gestionarla:** Usuarios con permiso `Vehiculos.Modificar`.

1. Ingrese a la pantalla de revisión (`/VehicleCatalogueReview`).
2. Se listarán las propuestas ingresadas por los usuarios con fecha, marca, modelo propuesto y estado.
3. Para cada propuesta, el operador puede:
   - **Aprobar:** Incorpora la marca o modelo al catálogo oficial disponible.
   - **Corregir:** Ajusta la ortografía o nombre comercial antes de aprobar.
   - **Rechazar:** Descarta la propuesta si se trata de un ingreso no válido.

---

## 4. Servicios y paquetes

El módulo de **Servicios** y el de **Paquetes** permiten configurar la oferta comercial del lavadero, sus precios, etapas de ejecución y consumos teóricos de insumos.

### 4.1 Consulta del catálogo de servicios

**Quién puede realizarla:** Usuarios con permiso `Servicios.Consultar`.

En la pantalla de Servicios (`/Servicio`):
- Se visualizan todos los servicios registrados con su nombre, tipo, tipo de vehículo, precio, tiempo estimado en minutos y estado.
- Dispone de filtros por:
  - **Tipo de Servicio** (ej: Lavado Básico, Lavado Completo, Encerado, Limpieza de Tapizados).
  - **Tipo de Vehículo** (ej: Automóvil, Camioneta, Moto).
  - **Rango de Precios** (Precio desde / hasta).
  - **Estado** (Activo / Inactivo).

---

### 4.2 Procedimiento: Registrar o editar un servicio

**Quién puede realizarla:** Usuarios con permiso `Servicios.Crear` (para registrar) o `Servicios.Modificar` (para editar).

**Antes de empezar:** Defina el nombre del servicio, a qué tipo de vehículo aplica, su precio, tiempo estimado de trabajo y si contará con etapas o insumos asignados.

**Pasos:**

1. Ingrese a **Servicios** desde el menú lateral.
2. Despliegue el acordeón superior **Registrando un Servicio** (o presione el botón de edición en la tabla).
3. Complete los campos principales:
   - **Nombre del Servicio:** Mínimo 3 caracteres. Solo letras y espacios (no números ni símbolos).
   - **Tipo de Servicio:** Seleccione de la lista (ej: Lavado de Carrocería). Si cuenta con permisos, puede crear un nuevo tipo desde el botón `+`.
   - **Tipo de Vehículo:** Seleccione para qué clase de rodado aplica este servicio puntual (un servicio se tarifa por tipo de vehículo).
   - **Precio:** Ingrese el valor monetario (igual o mayor a 0).
   - **Tiempo Estimado:** Ingrese la duración prevista en minutos (debe ser mayor a 0).
   - **Descripción:** Detalle qué incluye el servicio para claridad del personal.
   - **Estado:** Seleccione `Activo`.
4. *(Opcional)* Configure las **Etapas del servicio** (ver sección 4.3).
5. *(Opcional)* Configure la **Receta de insumos** (ver sección 4.4).
6. Presione el botón **Guardar** (ícono de verificación verde).

**Resultado esperado:**
El servicio queda guardado en la base de datos y disponible inmediatamente para ser seleccionado al crear nuevos lavados.

---

### 4.3 Etapas del servicio

Las etapas permiten descomponer un servicio en fases operativas sucesivas que los empleados irán completando de forma independiente durante el lavado (por ejemplo: *1. Prelavado y espuma*, *2. Lavado manual*, *3. Secado y aspirado*).

- Cada etapa requiere un **Nombre** descriptivo (mínimo 3 letras).
- Las etapas se ejecutan en el orden en que son cargadas en la lista.
- Un servicio puede no tener etapas (se inicia y finaliza como un único bloque) o tener múltiples etapas secuenciales.
- Cada etapa individual puede tener asociada su propia receta de insumos.

### 4.4 Recetas de insumos asociadas al servicio

La receta de insumos define qué productos químicos o materiales físicos consume teóricamente el servicio o etapa cada vez que se ejecuta:

- **Modalidad Producto:** Se vincula a un insumo específico del inventario (ej: *Shampoo Siliconado Bidón 5L*).
- **Modalidad Tipo de Insumo:** Se vincula a una categoría general (ej: *Cera Líquida*). Al momento de ejecutar el lavado, el sistema consultará qué producto puntual de esa categoría se utilizó en la playa.
- **Dosis Teórica:** Cantidad exacta que se descuenta del inventario por cada lavado realizado (ej: `0.150` litros o `1` unidad).
- **Unidad de Medida:** Debe coincidir con la unidad configurada en el insumo para asegurar la correcta conversión y control de stock.

### 4.5 Tipos de servicio y tipos de vehículo

Para mantener organizado el catálogo:
- En la cabecera del formulario de servicios se dispone de botones para abrir los modales de **Gestión de Tipos de Servicio** y **Gestión de Tipos de Vehículo**.
- Permiten dar de alta nuevas categorías o desactivar las obsoletas.

---

### 4.6 Consulta y gestión de paquetes de servicios

**Quién puede realizarla:** Usuarios con permiso `Paquetes.Consultar`.

En la pantalla de Paquetes (`/PaqueteServicio`), el sistema permite administrar combos promocionales que agrupan dos o más servicios bajo una tarifa bonificada.

### 4.7 Procedimiento: Registrar o editar un paquete

**Quién puede realizarla:** Usuarios con permiso `Paquetes.Crear` o `Paquetes.Modificar`.

**Antes de empezar:** Asegúrese de que los servicios individuales que compondrán el paquete ya existan y estén activos en el sistema.

**Pasos:**

1. Ingrese a **Paquetes** desde el menú lateral.
2. Despliegue el formulario superior **Registrando un Paquete**.
3. Ingrese el **Nombre del Paquete** (mínimo 3 letras, solo letras y espacios).
4. Seleccione el **Tipo de Vehículo** al que aplicará el combo.
5. Seleccione los servicios a incluir:
   - En la lista de servicios disponibles, marque los servicios que integrarán el paquete.
   - Debe seleccionar **al menos dos (2) servicios**.
6. Configure el **Porcentaje de Descuento**:
   - Ingrese un valor entre 0 y 100%.
   - El porcentaje debe ajustarse al paso/incremento de descuento configurado en el sistema (por ejemplo, múltiplos de 5%: 5%, 10%, 15%).
7. Observe los campos calculados automáticamente:
   - **Tiempo Estimado Total:** Corresponde a la suma exacta de los tiempos estimados de los servicios elegidos (campo no editable manualmente).
   - **Precio Final:** Corresponde a la suma de precios de los servicios menos el porcentaje de bonificación aplicado.
8. Presione el botón **Guardar**.

**Resultado esperado:**
El paquete queda registrado y podrá seleccionarse en la creación de lavados como una única opción comercial bonificada.

---

### 4.8 Reglas y restricciones de paquetes

Para garantizar la coherencia comercial, el sistema aplica las siguientes reglas automáticas:

1. **Mismo Tipo de Vehículo:** No es posible combinar en un mismo paquete servicios destinados a vehículos distintos (por ejemplo, no se puede mezclar un lavado de moto con un encerado de camioneta). Todos deben coincidir con el tipo de vehículo del paquete.
2. **Unicidad de Tipo de Servicio:** Un paquete solo puede incluir **un único servicio por cada tipo de servicio**. Por ejemplo, no puede contener dos servicios cuyo tipo sea "Lavado Exterior"; debe contener un "Lavado Exterior" y un "Aspirado Interior".
3. **Mínimo de Servicios:** Un paquete no puede conformarse con un solo servicio; requiere al menos dos.

### 4.9 Desactivación y reactivación de servicios y paquetes

- **Desactivar:** Requiere permiso de eliminación (`Servicios.Eliminar` o `Paquetes.Eliminar`). Pasa el estado a `Inactivo`. Un servicio o paquete inactivo ya no aparece disponible para la creación de nuevos lavados, pero los lavados históricos que lo incluyeron conservan su registro intacto.
- **Reactivar:** Permite volver a poner en oferta un servicio o paquete que había sido discontinuado.

---

## 5. Gestión de lavados

El módulo de **Lavados** (`/Lavados`) constituye el núcleo operativo del lavadero. Permite gestionar todo el ciclo de vida de la atención vehicular: recepción, asignación, ejecución, consumo de insumos, cobro y entrega.

### 5.1 Vista general y panel de lavados

**Quién puede realizarla:** Usuarios con permiso `Lavados.Consultar`.

Al ingresar al módulo se visualiza la tabla principal con los trabajos registrados:
- **Filtros Disponibles:**
  - **Estado del Lavado:** `Pendiente`, `EnProceso`, `Realizado`, `RealizadoParcialmente`, `Cancelado`.
  - **Estado de Pago:** `Pendiente`, `Parcial`, `Pagado`.
  - **Estado de Retiro:** `Pendiente`, `Retirado`.
  - **Rangos de Fechas y Precios:** Filtrado por fecha de creación o valores monetarios.
- **Acciones Rápidas:** Botón para ver la pantalla completa de detalle operativo (`/Lavados/Detalle/{id}`).

---

### 5.2 Validación de condiciones operativas antes de crear

Al intentar registrar un lavado, el sistema realiza tres validaciones automáticas:

1. **Horario de Operación:** Verifica que el día y la hora actual se encuentren dentro de los horarios comerciales autorizados en Configuración. Si el lavadero está cerrado o fuera de horario, el sistema rechaza la creación informando: *"El lavadero está fuera de horario de operación"* o *"El lavadero está cerrado los días [Día]"*.
2. **Capacidad Concurrente Máxima:** Verifica que la cantidad de lavados actualmente activos (`EnProceso`) no supere la capacidad máxima establecida para el establecimiento.
3. **Personal Activo:** Si está configurada la verificación de personal, comprueba que existan suficientes empleados activos y disponibles para asumir la carga de trabajo.

---

### 5.3 Procedimiento: Registrar un nuevo lavado

**Quién puede realizarla:** Usuarios con permiso `Lavados.Crear`.

**Antes de empezar:** Identifique el vehículo por su patente. Si el cliente o el vehículo no existen previamente, deben ser dados de alta.

**Pasos:**

1. Ingrese a **Lavados** y despliegue el acordeón superior **Registrando un Lavado**.
2. **Búsqueda del Vehículo:**
   - En el campo **Patente del Vehículo**, comience a escribir la patente.
   - Seleccione el vehículo deseado en la lista desplegable de coincidencias.
   - El sistema mostrará la información del vehículo (Marca, Modelo, Tipo y Color).
3. **Información de Entrega y Retiro:**
   - Una vez seleccionado el vehículo, se habilitará la sección de personas asociadas.
   - **¿Quién trajo el vehículo?:** Seleccione la persona que está entregando el auto en la playa. Por defecto figura el titular principal.
   - **¿Quién retirará el vehículo?:** Seleccione la persona autorizada para venir a retirarlo al finalizar el trabajo.
   - *(Opcional)* Si la persona que entrega no está en la lista de dueños asociados y usted tiene permiso `Clientes.Crear`, puede usar el enlace **Registrar cliente** para incorporarla en el acto como persona asociada a esa patente.
4. **Selección de Servicios o Paquetes:**
   - El sistema listará automáticamente los servicios individuales y paquetes que coincidan con el tipo de vehículo seleccionado.
   - Marque los servicios o el paquete que solicitó el cliente.
5. **Asignación de Empleados:**
   - Seleccione de la lista de empleados disponibles quién o quiénes estarán a cargo del lavado, sin superar el límite máximo de empleados por lavado configurado.
6. **Descuento y Notas:**
   - Si corresponde, ingrese un porcentaje de descuento global.
   - Ingrese observaciones o indicaciones especiales en el campo **Notas** (ej: *"Cuidado con espejo retrovisor flojo"*).
7. Presione el botón **Crear Lavado** (o Crear e Iniciar si comienza a lavarse de inmediato).

**Resultado esperado:**
El lavado queda registrado en estado `Pendiente` (o `EnProceso`), se calcula el precio final y el tiempo estimado total, y se emiten las alertas pertinentes en el panel.

---

### 5.4 Roles de personas asociadas: Quién trae y quién retira

Para evitar confusiones en la entrega y garantizar la correcta comunicación:
- **Cliente Titular (`ClienteId`):** Persona a nombre de quien queda registrado el historial y la factura principal.
- **Cliente que Trajo (`ClienteTrajoId`):** Persona física que entregó el vehículo en la playa. Recibirá notificaciones sobre el avance del servicio si tiene preferencias activas.
- **Cliente que Retira (`ClienteRetiraId`):** Persona explícitamente autorizada para retirar el auto una vez concluido el trabajo. Las notificaciones de "Vehículo listo para retirar" priorizan a este contacto.

---

### 5.5 Pantalla de detalle y seguimiento operativo

Al presionar sobre un lavado, se accede a la pantalla de **Detalle del Lavado** (`/Lavados/Detalle/{id}`), la cual ofrece el control en tiempo real de:
- Datos del cliente, vehículo y estado general.
- Tiempos de inicio, finalización y comparativa con el tiempo estimado.
- Panel de pago con saldo pagado y restante.
- Lista de empleados asignados.
- Lista ordenada de servicios y etapas, cada una con sus botones de control de estado.

---

### 5.6 Procedimiento: Iniciar un servicio o etapa

**Quién puede realizarla:** Usuarios con permiso `Lavados.Modificar`.

**Pasos:**
1. En la pantalla de Detalle del Lavado, ubique el servicio o etapa en estado `Pendiente`.
2. Presione el botón **Iniciar** (ícono de reproducción / play azul).
3. Si el servicio o etapa posee una receta de insumos asociada:
   - El sistema comprobará si hay stock suficiente en inventario.
   - Si la receta utiliza la modalidad por *Tipo de Insumo*, se abrirá una ventana emergente para que el operador seleccione qué producto específico de esa categoría se usará físicamente.
   - Si no hay stock suficiente, el sistema advertirá de la situación antes de confirmar.
4. Confirme el inicio.

**Resultado esperado:**
El servicio o etapa pasa al estado `EnProceso`, se estampa la fecha y hora de inicio (`TiempoInicio`) y el lavado completo pasa a `EnProceso`. Los insumos requeridos quedan descontados del stock disponible y registrados en el consumo.

---

### 5.7 Procedimiento: Finalizar un servicio o etapa

**Quién puede realizarla:** Usuarios con permiso `Lavados.Modificar`.

**Pasos:**
1. En la pantalla de Detalle, ubique el servicio o etapa que se encuentra `EnProceso`.
2. Presione el botón **Finalizar** (ícono de tilde / verificación verde).
3. Confirme la finalización en el cuadro de diálogo.

**Resultado esperado:**
- La etapa o servicio cambia al estado `Realizado` y registra su `TiempoFinalizacion`.
- Si el servicio poseía etapas, al finalizar la última etapa se finaliza automáticamente el servicio completo.
- Si se finalizó el último servicio pendiente del lavado, el lavado completo pasa automáticamente al estado `Realizado` y se despacha la notificación al cliente indicando que su vehículo está listo para retirar.

---

### 5.8 Procedimiento: Cancelar un lavado, servicio o etapa

**Quién puede realizarla:** Usuarios con permiso `Lavados.Modificar`.

**Antes de empezar:** Tenga en claro el motivo de la cancelación.

**Pasos:**
1. En la cabecera superior (para cancelar el lavado entero) o en la fila correspondiente (para cancelar un servicio o etapa específica), presione el botón **Cancelar** (ícono de cesto rojo o cruz roja).
2. Se abrirá la ventana modal de cancelación.
3. Ingrese obligatoriamente el **Motivo de la cancelación** (ej: *"Cliente solicita retiro urgente por lluvia"* o *"Falla en hidrolavadora"*).
4. *(Si aplica)* Si antes de cancelar ya se habían utilizado insumos no registrados, el sistema permitirá asentar dichos consumos reales para no distorsionar el inventario.
5. Presione **Confirmar Cancelación**.

**Resultado esperado:**
El elemento pasa al estado `Cancelado`, registrando el motivo y emitiendo la notificación correspondiente al cliente informando la cancelación y su causa.

---

### 5.9 Finalización parcial de lavados

Un lavado puede concluir en estado **Realizado Parcialmente** (`RealizadoParcialmente`):
- Ocurre cuando al menos un servicio o etapa fue completado (`Realizado`), pero otro servicio o etapa del mismo lavado tuvo que ser `Cancelado`.
- En este caso, el sistema reconoce el trabajo efectivamente hecho, calcula el cobro que corresponda y en las notificaciones al cliente no anuncia el lavado como completado al 100%, sino que aclara que finalizó de forma parcial.

---

### 5.10 Procedimiento: Registrar pagos

**Quién puede realizarla:** Usuarios con permiso `Lavados.Modificar`.

El pago puede registrarse en cualquier momento (por adelantado al recibir el rodado o al momento de la entrega), siempre que el lavado no se encuentre cancelado.

**Pasos:**
1. En la pantalla de Detalle del Lavado, haga clic en el botón superior **Registrar Pago** (ícono verde de billete).
2. En la ventana modal se exhibirá el precio total del lavado, el monto ya abonado previamente y el saldo restante.
3. Ingrese los datos del cobro:
   - **Monto a Pagar:** Ingrese la cantidad de dinero que entrega el cliente (puede ser el total o un pago parcial a cuenta).
   - **Medio de Pago:** Seleccione entre Efectivo, Tarjeta de Débito, Tarjeta de Crédito, Transferencia, MercadoPago u otros.
   - **Notas:** Ingrese número de comprobante o referencia de transferencia si aplica.
4. Presione **Guardar Pago**.

**Resultado esperado:**
El pago se incorpora al historial de pagos del lavado. El estado del pago se actualiza a `Parcial` (si aún resta saldo) o a `Pagado` (si se cubrió el total).

---

### 5.11 Procedimiento: Registrar retiro del vehículo

**Quién puede realizarla:** Usuarios con permiso `Lavados.Modificar`.

**Antes de empezar:** Para que el botón de retiro se habilite, deben cumplirse obligatoriamente tres condiciones:
1. El lavado debe estar finalizado (`Realizado`, `RealizadoParcialmente`) o `Cancelado`.
2. El pago debe estar completado (`Pagado`) o marcado como resuelto (`Cancelado`).
3. El estado de retiro debe figurar como `Pendiente`.

**Pasos:**
1. En la pantalla de Detalle del Lavado, desplácese hasta la sección **Retiro del Vehículo**.
2. Verifique la advertencia en pantalla que le indica la persona autorizada: *"Verifique que la persona que retira el vehículo sea: [Nombre del cliente que retira]"*.
3. Constatada la identidad del receptor, presione el botón **Marcar como Retirado**.
4. Confirme la entrega.

**Resultado esperado:**
El estado de retiro cambia a `Retirado` y queda grabada la fecha y hora exacta de retiro (`FechaRetiro`). El ciclo operativo del lavado queda cerrado.

---

## 6. Notificaciones

LavaFácil mantiene informados a los clientes sobre el progreso de sus vehículos y asiste al personal operativo indicando el resultado técnico de cada intento de despacho.

### 6.1 Canales y resolución de destinatarios

Cuando ocurre un evento notificable en un lavado, el sistema ejecuta el siguiente procedimiento de despacho:

1. **Destinatarios:** Identifica y compila la lista de clientes involucrados en el lavado:
   - El cliente que trajo el vehículo (`ClienteTrajoId`).
   - El cliente autorizado a retirarlo (`ClienteRetiraId`).
   - El cliente titular registrado (`ClienteId`).
   - *Nota:* Si una misma persona cumple más de un rol, la lista se unifica para no enviarle mensajes duplicados.
2. **Canales por Cliente:** Para cada persona destinataria, el sistema consulta sus preferencias guardadas:
   - Si tiene activo **Email**, despacha un correo electrónico estructurado.
   - Si tiene activo **WhatsApp**, despacha un mensaje de texto mediante la API oficial de WhatsApp.

### 6.2 Eventos que generan notificación automática

Las notificaciones se disparan en tiempo real ante los siguientes hitos:
- **Finalización de Etapa (`finalizacion_etapa`):** Informa que concluyó un paso puntual del servicio y anticipa cuál es el siguiente paso.
- **Finalización de Servicio (`finalizacion_servicio`):** Informa que el servicio en cuestión fue completado.
- **Finalización de Lavado (`finalizacion_lavado`):** Comunica que todos los servicios terminaron y que el vehículo se encuentra listo para ser retirado.
- **Cancelación de Etapa (`cancelacion_etapa`), Servicio (`cancelacion_servicio`) o Lavado (`cancelacion_lavado`):** Notifica la suspensión de la tarea detallando el motivo registrado por el operador.

### 6.3 Contenido de los mensajes enviados

Los mensajes enviados a los clientes son generados automáticamente y contienen:
- Nombre oficial del lavadero.
- Descripción clara del evento ocurrido (ej: *"la etapa 'Aspirado profundo' finalizó correctamente"* o *"el lavado fue cancelado"*).
- Fecha y hora exacta del suceso.
- En caso de cancelación: el motivo explícito registrado.
- Estado siguiente: qué etapa o servicio continúa a continuación.
- Aviso de retiro: si el trabajo concluyó, incluye la leyenda explícita: *"El vehículo está listo para retirar"*.

### 6.4 Avisos y resúmenes para el personal

Una vez ejecutada una acción que genera avisos, el sistema muestra en pantalla un mensaje informativo para el operador con el siguiente formato:

> *"Notificaciones: X aceptadas por el proveedor, Y fallidas, Z indeterminadas y W con datos de contacto inválidos."*

Este aviso permite al operador saber de un vistazo si los clientes fueron notificados adecuadamente.

### 6.5 Comprensión de estados: Aceptado, Fallido e Indeterminado

Es fundamental que el personal comprenda el significado real de las respuestas de los proveedores de mensajería (Meta WhatsApp y el servidor de correo):

- **Aceptado por el proveedor (`aceptado_por_proveedor`):**
  Significa que el proveedor del servicio (Meta o el servidor de email) recibió la solicitud correctamente y la encoló para su despacho.
  > [!IMPORTANT]
  > Un estado "aceptado" **no garantiza** que el cliente ya haya recibido o leído el mensaje en su teléfono (por ejemplo, el celular del cliente podría estar apagado, fuera de cobertura o sin conexión).
- **Fallido (`fallido`):**
  La petición fue rechazada de forma confirmada por el proveedor (por ejemplo: error de autenticación con la API, número bloqueado o servidor de correo no disponible).
- **Indeterminado (`indeterminado`):**
  Ocurre ante una falla de transporte incierto (por ejemplo, un corte de red momentáneo o un tiempo de espera agotado / timeout mientras se enviaba la petición). El sistema no pudo confirmar si el mensaje llegó a salir o no.
- **Contacto inválido (`contacto_invalido`):**
  El cliente tenía habilitada la preferencia pero carecía de dirección de correo o su número de teléfono no cumplía con los 10 dígitos reglamentarios.

### 6.6 Pautas de actuación ante resultados no exitosos

- **Ante mensajes Indeterminados:** **NO repita la operación ni intente reenviar de inmediato.** Dado que el mensaje pudo haber sido aceptado por el servidor externo antes de la interrupción, un reintento a ciegas podría provocar que el cliente reciba múltiples mensajes idénticos. Verifique primero el estado del lavado.
- **Ante contactos inválidos o fallidos repetidos:** Verifique los datos de contacto del cliente en la ficha del módulo de Clientes y corríjalos de ser necesario.

---

## 7. Insumos, stock y compras

El módulo de **Insumos y Stock** (`/Insumos`) administra el inventario de productos químicos, accesorios y herramientas del lavadero, el registro de proveedores, las solicitudes de cotización y las órdenes de compra.

### 7.1 Inventario de insumos y estados de stock

En la pantalla principal de Insumos se presenta el inventario agrupado por categorías o tipos de producto:
- **Stock Físico:** Cantidad total real que existe físicamente en las instalaciones del lavadero.
- **Stock Reservado:** Cantidad de insumo comprometida por los lavados que actualmente se encuentran `EnProceso`.
- **Stock Disponible:** Cantidad libre que puede ser consumida por nuevos lavados (`Stock Físico - Stock Reservado`).

### 7.2 Alertas y monitoreo de stock crítico

- **Stock Mínimo:** Umbral de seguridad configurado individualmente para cada insumo.
- **Alerta de Stock Crítico:** Si el stock físico cae por debajo o iguala al stock mínimo, el insumo entra en estado crítico.
- **Señalización:**
  - Aparece un cartel de advertencia destacado en el encabezado del módulo de insumos indicando la cantidad de productos críticos.
  - En la barra de navegación superior (visible desde cualquier módulo del sistema) se muestra un aviso flotante con acceso directo.

---

### 7.3 Procedimiento: Registrar o editar un insumo

**Quién puede realizarla:** Usuarios con permiso `Insumos.Crear` o `Insumos.Modificar`.

**Pasos:**
1. En la pantalla de Insumos, despliegue el acordeón superior **Registrando un Insumo** (o presione editar en la fila correspondiente).
2. Complete los datos del producto:
   - **Nombre:** Denominación clara del insumo (ej: *Shampoo Cera Concentrado*).
   - **Tipo de Insumo:** Seleccione la categoría a la que pertenece (Químico, Físico, Herramienta). Si cuenta con permisos, puede crear nuevos tipos desde el botón correspondiente.
   - **Unidad de Medida:** Unidad en la que se cuantifica el stock (ej: *Litros*, *Mililitros*, *Gramos*, *Unidades*).
   - **Stock Mínimo:** Cantidad de alerta de reposición (mayor o igual a 0).
   - **Stock Actual:** Cantidad física inicial en depósito.
   - **Costo Unitario:** Precio de costo de compra por unidad.
   - **Proveedor Habitual:** Proveedor preferente de este insumo.
   - **Estado:** `Activo`.
3. Presione el botón de guardar.

**Resultado esperado:**
El insumo se incorpora al inventario y se refleja en los totales agrupados por tipo.

---

### 7.4 Procedimiento: Ajustar stock manualmente

**Quién puede realizarla:** Usuarios con permiso `Insumos.AjustarStock`.

**Antes de empezar:** Tenga justificada la razón del ajuste (rotura de envase, diferencia encontrada en recuento físico de inventario, merma o donación).

**Pasos:**
1. En la tabla de insumos, ubique el producto y haga clic en el botón de **Ajustar Stock** (ícono de balanza o deslizadores).
2. Se abrirá la ventana modal de ajuste.
3. Complete los campos:
   - **Tipo de Ajuste:** Seleccione si se trata de un **Ingreso** (aumenta el stock) o un **Egreso** (disminuye el stock).
   - **Cantidad:** Ingrese el valor a ajustar (debe ser mayor a 0).
   - **Justificación:** Ingrese obligatoriamente el motivo del ajuste (ej: *"Recuento físico mensual de depósito - ajuste de faltante por evaporación"*).
4. Presione **Confirmar Ajuste**.

**Resultado esperado:**
El stock actual del insumo se actualiza en el momento y se crea automáticamente un registro en el **Kardex** con el nuevo saldo resultante.

---

### 7.5 Consulta de movimientos en Kardex

**Quién puede realizarla:** Usuarios con permiso `Insumos.Kardex`.

Al ingresar a la sección de Kardex (`/Insumos/Kardex`):
- Se accede al libro diario de movimientos de almacén en orden cronológico.
- **Datos Mostrados:** Fecha y hora, Nombre del insumo, Tipo de movimiento (`Ingreso`, `Egreso`, `Ajuste`, `RecepcionCompra`, `ConsumoLavado`), Cantidad movilizada, Saldo resultante y Justificación.
- **Filtros:** Puede filtrar por uno o varios insumos, tipos de movimiento, rangos de fecha y orden (más recientes primero o más antiguos primero).
- **Paginación por Cursores:** La navegación entre páginas utiliza cursores para garantizar estabilidad frente a movimientos continuos de inventario.

---

### 7.6 Gestión de proveedores

**Quién puede realizarla:** Usuarios con permiso `Insumos.Consultar` y `Insumos.Modificar`.

En la pantalla de Proveedores (`/Insumos/Proveedores`):
- Se administran las empresas que abastecen de insumos al lavadero.
- **Datos Registrados:** Razón Social, CUIT/Identificador tributario, Persona de contacto, Correo electrónico (utilizado para el envío automático de cotizaciones), Teléfono, Dirección y Estado (Activo/Inactivo).

---

### 7.7 Solicitudes de cotización (RFQ) y comparativas

El módulo de Cotizaciones (`/Insumos/Cotizaciones`) permite solicitar presupuestos de compra a uno o varios proveedores simultáneamente para conseguir mejores precios y plazos de entrega.

### 7.8 Procedimiento: Crear y enviar una solicitud de cotización

**Quién puede realizarla:** Usuarios con permisos `Insumos.Crear` y `Insumos.Modificar`.

**Pasos:**
1. Ingrese a **Cotizaciones** y despliegue el formulario **Registrando una Solicitud**.
2. **Selección de Proveedores:**
   - Busque y agregue los proveedores a los cuales desea solicitar cotización. Puede seleccionar varios proveedores para crear una solicitud grupal comparativa.
3. **Selección de Insumos y Cantidades:**
   - Agregue los insumos que necesita reponer indicando la cantidad solicitada para cada uno.
4. Presione el botón **Crear Solicitud**.
5. En el listado de solicitudes, presione el botón **Enviar por Email** en la solicitud generada para que el sistema remita automáticamente el requerimiento a la casilla de correo del proveedor.

---

### 7.9 Procedimiento: Registrar respuesta y adjudicar cotización

**Quién puede realizarla:** Usuarios con permiso `Insumos.Modificar`.

**Pasos:**
1. Cuando el proveedor responda con sus precios y condiciones:
   - Ingrese a **Cotizaciones** y ubique la solicitud correspondiente.
   - Presione el botón **Registrar Respuesta**.
   - En la ventana modal, cargue los precios unitarios informados por el proveedor para cada ítem, indique si tiene disponibilidad y especifique el **Plazo de entrega (en días)**.
   - Presione **Guardar Respuesta**. El estado de la solicitud pasará a `Respondida`.
2. **Comparación y Adjudicación:**
   - Si la solicitud formaba parte de un grupo enviado a varios proveedores, abra la vista comparativa del grupo.
   - Se presentará una tabla comparativa con el total cotizado por cada proveedor y sus plazos de entrega.
   - Elija la mejor propuesta y presione el botón **Adjudicar** (ícono de tilde azul).

**Resultado esperado:**
La cotización elegida queda adjudicada y genera automáticamente una **Orden de Compra** vinculada en estado `PendienteAprobacion`.

---

### 7.10 Ciclo de órdenes de compra

Las órdenes de compra (`/Insumos/Ordenes`) atraviesan los siguientes estados:
- **`PendienteAprobacion`:** Orden creada tras una adjudicación o solicitud que requiere autorización formal.
- **`Enviada`:** Orden aprobada y transmitida formalmente al proveedor.
- **`Confirmada`:** Se registró internamente que el proveedor aceptó el pedido y prepara el despacho.
- **`Recibida`:** Toda la mercadería pedida ingresó conforme al depósito.
- **`RecibidaParcial`:** Se recibió una entrega parcial donde faltaron unidades.
- **`RevisionManual`:** Hubo un resultado incierto al enviar la orden que requiere revisión de un operador.
- **`Cancelada`:** La orden fue dejada sin efecto.

---

### 7.11 Procedimiento: Aprobar o modificar una orden pendiente

**Quién puede realizarla:** Usuarios con permiso `Insumos.Modificar`.

**Pasos:**
1. Ingrese a **Órdenes de compra**.
2. Ubique la orden en estado `PendienteAprobacion`.
3. Si requiere corregir cantidades o precios antes de aprobar:
   - Utilice el formulario de edición en línea de la fila del ítem para ajustar la cantidad o el precio unitario.
4. Presione el botón **Aprobar y enviar**.

**Resultado esperado:**
La orden pasa al estado `Enviada` y se envía la notificación formal al proveedor para su abastecimiento.

---

### 7.12 Procedimiento: Recepcionar mercadería (total o parcial)

**Quién puede realizarla:** Usuarios con permiso `Insumos.Recepcionar` (incluido en el rol Empleado).

**Antes de empezar:** Tenga la mercadería recibida físicamente a la vista y el remito del proveedor.

**Pasos:**
1. Ingrese a **Órdenes de compra** y localice la orden enviada o confirmada.
2. Presione el botón **Recepcionar Mercadería** (ícono de caja / paquete).
3. En la ventana modal de recepción:
   - Para cada insumo, verifique la cantidad pedida e ingrese en la casilla la **Cantidad Recibida físicamente**.
   - Indique si la recepción completa la orden de forma definitiva o si se trata de una entrega parcial.
4. Presione **Confirmar Recepción**.

**Resultado esperado:**
- El stock físico de cada insumo recibido se incrementa de inmediato en el inventario.
- Se genera un movimiento de ingreso en el **Kardex** referenciando el número de orden de compra.
- Si se recibieron todas las cantidades, la orden pasa a `Recibida`. Si hubo faltantes, pasa a `RecibidaParcial`, indicando claramente cuántas unidades faltan para que el personal contacte al proveedor.

---

### 7.13 Directiva de reabastecimiento

En la pantalla de Configuración de Insumos (`/Insumos/Configuracion`), los usuarios administradores pueden definir los lineamientos que rigen las compras:
- Vigencia de cotizaciones recientes en días (período durante el cual una cotización anterior puede reutilizarse para compras directas sin pedir nuevo presupuesto).
- Porcentajes de tolerancia en compras.

---

## 8. Personal, roles y administración

Esta sección detalla las funciones reservadas para la gestión del equipo de trabajo, la seguridad y la parametrización de la empresa.

### 8.1 Gestión del personal

**Quién puede realizarla:** Usuarios con permiso `Personal.Consultar`.

En la pantalla de Personal (`/Personal`):
- Se consulta la nómina completa de colaboradores registrados.
- Permite filtrar por **Estado** (`Activo`, `Inactivo`) y por **Rol** asignado.
- Permite buscar por nombre o correo electrónico.

### 8.2 Procedimiento: Modificar rol o estado de un empleado

**Quién puede realizarla:** Usuarios con permiso `Personal.Modificar`.

**Pasos:**
1. Ubique al colaborador en la tabla de Personal.
2. **Para cambiar de rol:**
   - En la columna de rol, seleccione el nuevo rol en la lista desplegable.
   - El sistema validará que el rol elegido exista y esté activo, actualizará la cuenta y registrará el cambio en la auditoría.
3. **Para desactivar al colaborador:**
   - Presione el botón **Desactivar** (ícono de bloqueo o baja).
   - El colaborador pasará a estado `Inactivo` y no podrá iniciar sesión en el sistema.
4. **Para reactivar al colaborador:**
   - Filtre por empleados inactivos y presione el botón **Reactivar**.

---

### 8.3 Administración de roles y matriz de permisos

**Quién puede realizarla:** Usuarios con permiso `Roles.Consultar`.

En la pantalla de Roles (`/Rol`):
- Se listan los roles vigentes y la cantidad de empleados asignados a cada uno.
- Se asegura que los roles base (`Administrador` y `Empleado`) estén siempre presentes y protegidos.

### 8.4 Procedimiento: Crear o editar un rol personalizado

**Quién puede realizarla:** Usuarios con permiso `Roles.Crear` (para crear) o `Roles.Modificar` (para editar).

**Pasos:**
1. Despliegue el formulario superior **Registrando un Rol** (o presione editar en un rol existente).
2. Complete:
   - **Nombre del Rol:** Denominación clara (ej: *Supervisor de Playa*). Los roles protegidos no pueden renombrarse.
   - **Descripción:** Finalidad y responsabilidades del rol.
3. **Matriz de Permisos:**
   - Marque las casillas de verificación correspondientes a los módulos y acciones que este rol tendrá autorizados.
4. Presione **Guardar**.

**Situaciones a tener en cuenta:**
- **Protección del Administrador:** El rol Administrador no puede perder los permisos críticos de gestión de roles.
- **Desactivación de Roles:** Un rol no puede ser desactivado si tiene empleados asignados a él actualmente o si es un rol protegido del sistema.

---

### 8.5 Configuración general del lavadero

**Quién puede realizarla:** Usuarios con permiso `Configuracion.Consultar` y `Configuracion.Modificar`.

En la pantalla de Configuración (`/Configuracion`), el administrador parametriza las reglas del negocio:
- **Nombre del Lavadero:** Nombre comercial que se exhibe en la barra superior, en las notificaciones y en los mensajes de WhatsApp.
- **Ubicación:** Dirección física del establecimiento.
- **Capacidad Máxima Concurrente:** Cantidad máxima de vehículos que pueden estar lavándose simultáneamente en las instalaciones (1 a 100).
- **Considerar Empleados Activos:** Si está activo, la capacidad real del lavadero estará limitada también por la cantidad de personal en servicio.
- **Empleados Máximos por Lavado:** Límite de operarios que pueden asignarse a un solo lavado (1 a 20).
- **Tiempos de Lavado:**
  - *Tiempo de Notificación (minutos):* Minutos de anticipación con que el sistema avisa al personal antes de que venza el tiempo estimado del lavado.
  - *Tiempo de Tolerancia (minutos):* Margen de tiempo máximo que puede extenderse un lavado antes de considerarse retrasado.
  - *Intervalo de Preguntas:* Frecuencia en minutos con que el sistema consulta si ya concluyó el trabajo cuando se excede la tolerancia.
- **Políticas de Sesión:**
  - *Duración Máxima de Sesión (horas):* Tiempo total de vigencia de la sesión continua (1 a 24 horas, por defecto 8).
  - *Tiempo de Inactividad (minutos):* Minutos sin uso antes del cierre automático de sesión (5 a 120 minutos, por defecto 15).
- **Paso de Descuento de Paquetes:** Incremento permitido para los descuentos de combos (ej: en pasos de 5%).
- **Porcentaje de Alerta de Stock:** Margen de seguridad para la advertencia visual de insumos.

---

### 8.6 Configuración de horarios de operación y su impacto

Dentro de la pantalla de Configuración se definen los **Horarios de Operación** para cada día de la semana (Lunes a Domingo):
- **Formato Continuo:** Ejemplo: `09:00-18:00`.
- **Formato Dividido (Turno Cortado):** Ejemplo: `09:00-13:00,15:00-19:00`.
- **Día Cerrado:** Ingrese la palabra `CERRADO`.

**Impacto Directo:**
Si el lavadero se encuentra cerrado o el reloj del sistema se halla fuera de las franjas horarias configuradas para el día, **ningún usuario podrá dar de alta nuevos lavados ni iniciar servicios**, protegiendo los turnos y el control de asistencia.

---

### 8.7 Consulta y análisis de registros de auditoría

**Quién puede realizarla:** Usuarios con permiso `Auditoria.Consultar`.

En la pantalla de Auditoría (`/Auditoria`), se almacena el historial cronológico e inalterable de todos los eventos relevantes del sistema:
- **Búsqueda General:** Búsqueda rápida por usuario, acción, tipo de objetivo o fecha.
- **Filtros Avanzados:**
  - **Rango de Fechas:** Fecha *Desde* y *Hasta*. (La fecha Desde no puede ser posterior a la fecha Hasta).
  - **Acciones:** Filtrar por tipos de acción (`Alta Rol`, `Modificacion Rol`, `Desactivacion Rol`, `Creación de cliente`, `Actualizacion de cliente`, `Desactivacion de cliente`, `Creación de lavado`, `Intento de notificación`, etc.).
  - **Tipos de Objetivo:** Filtrar por entidad (`Cliente`, `Vehiculo`, `Servicio`, `Paquete`, `Lavado`, `Empleado`, `Rol`, `Configuracion`, `Notificacion`, `Insumo`).

**Apertura del Detalle Comparativo:**
Al hacer clic en cualquier fila de la tabla de auditoría, se abre la ventana emergente de **Detalle de Auditoría**:
- Muestra el identificador del registro, la acción efectuada, el objetivo, el usuario responsable (correo y ID) y la fecha/hora exacta.
- **Comparación Antes / Después:** Presenta una tabla comparativa clara con el estado de cada propiedad antes del cambio y el nuevo valor asumido después de la operación, permitiendo investigar desvíos o modificaciones no autorizadas con total precisión.

---

## 9. Uso de WhatsApp para clientes

> [!TIP]
> Esta sección está redactada en un lenguaje directo y accesible para que el personal del lavadero pueda imprimirla, enviarla en formato digital o compartir sus instrucciones directamente con los clientes que deseen interactuar mediante WhatsApp.

El lavadero cuenta con un canal oficial de atención automática por **WhatsApp** para que puedas registrarte, consultar tus vehículos, obtener información del establecimiento y actualizar tus preferencias sin demoras.

### 9.1 Inicio de contacto

1. Agendá el número de teléfono oficial del lavadero en tus contactos de WhatsApp.
2. Abrí una conversación y enviá un mensaje con cualquier saludo (por ejemplo: *"Hola"* o *"Buenas tardes"*).
3. El sistema responderá automáticamente:
   - **Si ya sos cliente registrado:** Te dará una bienvenida personalizada por tu nombre y te mostrará el menú principal de opciones.
   - **Si es tu primera vez:** Te dará la bienvenida al lavadero y te invitará a realizar un breve registro guiado.

---

### 9.2 Flujo guiado de registro inicial

Si aún no estás registrado en el sistema, el asistente virtual te solicitará los siguientes datos paso a paso:

1. **Tipo de Documento:** Te mostrará botones o una lista para que elijas tu tipo de documento (ej: `DNI`).
2. **Número de Documento:** Escribí tu número de documento, ingresando únicamente números (sin puntos ni espacios).
3. **Nombre:** Escribí tu nombre (mínimo 3 letras).
4. **Apellido:** Escribí tu apellido (mínimo 3 letras).
5. **Correo Electrónico:** Ingresá tu dirección de email (ej: `tunombre@gmail.com`).
6. **Preferencias de Notificación:** Elegí mediante los botones si deseás recibir las novedades de tus lavados por **WhatsApp**, por **Email** o por **Ambos**.
7. **Confirmación de Datos:** El asistente te mostrará el resumen de tus datos y te preguntará si son correctos:
   - Presioná **Sí, registrar** para confirmar tu alta.
   - O presioná **Modificar** si necesitás corregir algún campo.
8. **Agregar Vehículo Opcional:** Una vez registrado, el asistente te consultará si querés dar de alta tu vehículo en ese mismo momento:
   - Podés elegir **Sí, agregar vehículo** para incorporarlo de inmediato, o **Ahora no** para pasar al menú principal.

---

### 9.3 Menú principal del cliente

Cuando ingreses al menú principal, verás las siguientes opciones disponibles:

- **🚗 Gestionar vehículos:** Consultar tus vehículos, agregar uno nuevo, asociarte a un auto familiar mediante clave o modificar datos.
- **👤 Mis datos:** Ver tu información registrada y modificar nombre, apellido, correo o preferencias de notificación.
- **ℹ️ Sobre nosotros:** Consultar el nombre oficial del lavadero, dirección física y horarios de atención al público.
- **❓ Ayuda:** Conocer los comandos rápidos de control y vías de contacto con el lavadero.

Podés responder tocando los botones interactivos, seleccionando la opción en la lista desplegable o escribiendo el número o título de la opción.

---

### 9.4 Gestión de vehículos desde WhatsApp

Al ingresar en **Gestionar vehículos**, podés realizar las siguientes operaciones:

#### Ver mis vehículos
El asistente te mostrará la lista de todos los vehículos asociados a tu número, indicando patente, marca, modelo, tipo y color.

#### Agregar un vehículo nuevo
1. Seleccioná el **Tipo de vehículo** (Automóvil, Camioneta, SUV, Motocicleta).
2. Escribí la **Patente** de tu vehículo (letras y números, ej: `AB123CD`).
3. **Marca:** El bot te mostrará una lista con las marcas más populares de la región (Toyota, Volkswagen, Ford, Chevrolet, Fiat, Renault, Peugeot, etc.). Si tu marca figura en la lista, seleccionala. Si no aparece, elegí la opción para escribirla manualmente y confirmá el nombre.
4. **Modelo:** Seleccioná el modelo en la lista o escribilo manualmente si no está listado.
5. **Color:** Escribí el color del vehículo (ej: *Gris plata*, *Blanco*, *Negro*).
6. Confirmá los datos para que el vehículo quede registrado en tu perfil.

> [!NOTE]
> Al registrar un vehículo por primera vez, el sistema te proporcionará una **Clave de Asociación de 6 caracteres**. Guardá esta clave si querés compartir el uso del vehículo con otra persona de tu familia.

---

### 9.5 Asociación de vehículo existente mediante clave

Si un familiar, pareja o compañero de trabajo ya registró el vehículo en el lavadero y necesitás asociarlo también a tu número de WhatsApp:

1. En el menú de vehículos, elegí la opción **Asociar vehículo existente**.
2. Escribí la **Patente** del vehículo.
3. El sistema te solicitará la **Clave de Asociación de 6 caracteres** (pedísela a quien registró el auto originalmente).
4. Escribí la clave.
5. El sistema validará la clave y vinculará el vehículo a tu cuenta. A partir de ese momento, vos también podrás llevarlo al lavadero, consultar su estado y recibir avisos de retiro.

#### Consultar o regenerar la clave de tu vehículo
Si sos el dueño del auto y necesitás consultar la clave para pasársela a otra persona:
- En el menú de vehículos, elegí **Ver clave de asociación** y seleccioná la patente.
- Si perdiste la clave o querés revocar accesos anteriores, podés elegir **Regenerar clave**. El sistema generará una clave nueva y dejará sin validez la anterior.

#### Modificar o desvincular un vehículo
- **Modificar:** Podés corregir el modelo o el color de tus autos registrados.
- **Desvincular:** Si vendiste el auto o ya no lo utilizás, podés eliminarlo de tu perfil personal de WhatsApp.

---

### 9.6 Consulta y edición de datos personales

Ingresando a la opción **Mis datos**:
- Podrás revisar tu nombre, documento, teléfono, correo y métodos de notificación activos.
- Para corregir información, elegí **Editar datos** y seleccioná qué campo querés actualizar:
  - Nombre
  - Apellido
  - Correo electrónico
  - Preferencias de notificación (WhatsApp, Email o Ambos)

---

### 9.7 Consulta de información institucional

Al pulsar en **ℹ️ Sobre nosotros**, el asistente te informará:
- Nombre oficial del lavadero.
- Dirección y ubicación del establecimiento.
- Horarios de atención al público para cada día de la semana.

---

### 9.8 Comandos especiales de navegación y ayuda

En cualquier momento de la conversación por WhatsApp podés escribir las siguientes palabras clave para controlar el asistente:

- `MENU` o `INICIO`: Vuelve inmediatamente al menú principal de opciones.
- `REINICIAR`: Reinicia tu sesión de conversación si sentís que el diálogo se desorientó.
- `CANCELAR`: Cancela la operación en curso (por ejemplo, si estabas cargando un vehículo y decidiste no continuar).
- `AYUDA`: Muestra un mensaje de soporte con los comandos y el teléfono de atención humana del lavadero.

---

### 9.9 Alcance y limitaciones del canal de WhatsApp

Para evitar falsas expectativas, tené en cuenta las siguientes consideraciones sobre el servicio de WhatsApp:
- **Sin Reserva de Turnos:** En la versión actual del sistema, el bot de WhatsApp **no realiza reservas ni turnos previos**. La atención en el lavadero se realiza por orden de llegada en la playa de estacionamiento dentro de los horarios comerciales vigentes.
- **Sin Cobros Integrados:** El bot no solicita datos bancarios ni procesa cobros directamente en la conversación de chat. Todos los pagos se efectúan al personal en el establecimiento mediante los medios de pago habilitados.

---

## 10. Problemas frecuentes y recomendaciones

La siguiente tabla detalla situaciones habituales, mensajes de error emitidos por el sistema, sus causas comprobables y el curso de acción recomendado:

| Situación o Mensaje del Sistema | Causa Respaldada por el Sistema | Qué puede hacer la persona usuaria | Cuándo contactar al administrador |
| :--- | :--- | :--- | :--- |
| **"Acceso denegado"** (`/Home/AccessDenied`) al intentar abrir una sección. | La cuenta del usuario no posee el permiso granular (`Módulo.Acción`) requerido para consultar o ejecutar esa función. | Verifique en su perfil el rol que tiene asignado y regrese a su página inicial autorizada. | Si sus tareas laborales requieren ese acceso, solicite al administrador que le asigne el permiso o cambie su rol. |
| **"Su sesión ha expirado por inactividad"** en la pantalla de Login. | Transcurrió el tiempo de inactividad configurado (por defecto 15 minutos) sin que se detectaran movimientos o pulsaciones. | Ingrese nuevamente su usuario y contraseña para reanudar el trabajo. | Si el tiempo resulta muy breve para la dinámica de trabajo, consulte al administrador para extender el tiempo de inactividad en Configuración. |
| **"Su sesión ha expirado por exceder el tiempo máximo permitido"**. | Se alcanzó el límite continuo de duración de sesión configurado (por defecto 8 horas). | Inicie sesión nuevamente. Es una medida de seguridad automática diaria. | No requiere contacto, salvo que el límite sea menor a la jornada laboral establecida. |
| **"El lavadero está fuera de horario de operación"** o **"El lavadero está cerrado los días [Día]"** al intentar crear un lavado. | El sistema validó el día de la semana y la hora actual contra la matriz de Horarios de Operación de Configuración y constató que el establecimiento está cerrado. | Verifique el reloj del equipo. Los lavados no pueden registrarse fuera del horario comercial habilitado. | Si el lavadero efectivamente está atendiendo en horario especial, solicite al administrador que actualice los horarios en Configuración. |
| **"Se ha alcanzado la capacidad máxima de lavados concurrentes"**. | La cantidad de lavados en estado `EnProceso` iguala o supera la `CapacidadMaximaConcurrente` permitida (o no hay personal disponible). | Espere a finalizar alguno de los lavados en proceso antes de dar ingreso a un nuevo vehículo. | Si la nave de lavado fue ampliada físicamente o hay más empleados, el administrador puede aumentar la capacidad en Configuración. |
| **"Debe seleccionar al menos un método de notificación"**. | Se intentó guardar un cliente o editarlo desmarcando simultáneamente las casillas de WhatsApp y Email. | Marque al menos una de las dos opciones (WhatsApp, Email o ambas) para continuar. | No requiere intervención del administrador. |
| **"El teléfono debe contener exactamente 10 dígitos"**. | El número telefónico ingresado contiene letras, guiones, espacios, el prefijo internacional `+54` o el prefijo celular `15`. | Ingrese exclusivamente 10 números corridos: código de área sin cero y número sin quince (ej: `1144332211`). | No requiere intervención del administrador. |
| **"Ya existe un cliente con este documento"**. | Se intentó dar de alta un cliente cuyo Tipo y Número de Documento ya están registrados en la base de datos. | Busque el cliente en la tabla por documento para verificar si ya existe. Si estaba inactivo, puede reactivarlo. | No requiere intervención del administrador. |
| **"La patente ya está registrada. Busque el vehículo para asociarlo"**. | Se intentó registrar como nuevo un vehículo cuya patente ya existe en el lavadero. | Si el cliente comparte el auto con otro dueño, utilice la opción de asociar vehículo existente mediante la clave de asociación de 6 caracteres. | Si la patente quedó huérfana de clientes, consulte al administrador para que revise el vehículo. |
| **Stock insuficiente al iniciar un servicio o etapa**. | La dosis teórica de la receta supera la cantidad de stock físico disponible en el inventario. | Si dispone de mercadería que acaba de ingresar, realice la recepción de la orden de compra pendiente o un ajuste justificado de stock. | Si no hay insumos físicos en el lavadero, solicite la compra urgente al responsable de compras. |
| Aviso de notificación: **"Indeterminadas: 1"** tras finalizar o cancelar un lavado. | Ocurrió un resultado incierto de transporte o timeout en la conexión con la API de Meta WhatsApp o el servidor de correo. | **NO intente reenviar la notificación de inmediato ni repita la operación**, para no duplicar mensajes al cliente. Verifique si el cliente acusa recibo. | Si el problema de indeterminación se reitera en todos los lavados, informe al administrador técnico para que revise las credenciales y conectividad del servidor. |
| **"No se puede desactivar un rol que tiene personal asignado"**. | Se intentó dar de baja un rol que aún está configurado en una o más cuentas de empleados. | Ingrese al módulo de Personal, reasigne a los empleados a otro rol activo y luego proceda a desactivar el rol sin uso. | No requiere intervención técnica adicional. |
| **"No se puede editar un rol inactivo"**. | Se intentó modificar permisos o descripción de un rol en estado `Inactivo`. | Primero reactive el rol desde la tabla de roles y luego aplique las modificaciones deseadas. | No requiere intervención técnica adicional. |
| **No se habilita el botón "Marcar como Retirado"** en un lavado. | No se cumplen las 3 condiciones exigidas: el lavado no está finalizado/cancelado, o el pago no está completado (`Pagado` o `Cancelado`), o ya fue retirado. | Verifique que todos los servicios y etapas estén concluidos y que el pago esté registrado en su totalidad. | No requiere intervención del administrador. |
| WhatsApp no responde al cliente. | La sesión del cliente puede haber quedado en un estado no resuelto o hubo interrupción de servicio en Meta. | Indique al cliente que escriba la palabra `REINICIAR` o `MENU` en el chat. | Si el bot no contesta a ningún cliente, contacte al responsable técnico para verificar el webhook de WhatsApp Cloud API. |

---

## 11. Glosario de términos

- **Adjudicación de Cotización:** Decisión formal mediante la cual se elige la oferta de un proveedor específico dentro de una solicitud de cotización, dando origen a una Orden de Compra.
- **Ajuste de Stock:** Modificación directa y manual del inventario físico de un insumo (por ingreso o egreso), que exige asentar obligatoriamente una justificación para su registro en Kardex.
- **Auditoría del Sistema:** Módulo de control que registra de forma inmutable quién realizó cada acción, en qué fecha/hora y sobre qué elemento, permitiendo visualizar los valores anteriores y posteriores.
- **Clave de Asociación (Vehículo):** Código alfanumérico seguro de 6 caracteres generado al dar de alta un vehículo. Permite que otros clientes registrados se vinculen al mismo rodado como cotitulares o usuarios autorizados.
- **Cliente que Retira (`ClienteRetiraId`):** Persona física formalmente autorizada para recibir y llevarse el vehículo una vez concluido el trabajo en el lavadero.
- **Cliente que Trae (`ClienteTrajoId`):** Persona física que entregó el vehículo en la playa de lavado al momento de iniciar el servicio.
- **Cliente Titular (`ClienteId`):** Persona a nombre de quien está asentada la cuenta principal del cliente en el lavadero.
- **Dosis Teórica:** Cantidad exacta de un insumo que, según la receta técnica, debe consumirse al ejecutar un servicio o etapa en un vehículo.
- **Etapa de Servicio:** Subdivisión operativa de un servicio (ej: lavado de chasis, aspirado, secado) que puede iniciarse, cronometrarse y finalizarse de forma independiente.
- **Horario de Operación:** Parámetros configurados por la empresa que determinan los días y rangos horarios comerciales en los que el sistema permite dar ingreso a lavados e iniciar tareas.
- **Insumo:** Producto químico, elemento de limpieza o accesorio fungible utilizado para la realización de los servicios de lavado.
- **Kardex:** Libro de registro cronológico y auditable de todos los movimientos de entrada, salida y ajustes de stock de cada insumo, que mantiene el saldo acumulado en tiempo real.
- **Lavado:** Orden de trabajo principal del sistema que vincula a un cliente, un vehículo, un conjunto de servicios o paquetes y a los empleados asignados a su ejecución.
- **Notificación Aceptada por Proveedor:** Estado técnico que confirma que la API externa (Meta WhatsApp o servidor de correo) recibió satisfactoriamente el mensaje para su distribución. No equivale a lectura por parte del cliente.
- **Notificación Indeterminada:** Estado generado cuando un corte de red o tiempo de espera impide confirmar si la notificación fue enviada o rechazada. Exige no duplicar envíos a ciegas.
- **Orden de Compra:** Documento administrativo que formaliza un pedido de reabastecimiento de insumos a un proveedor, permitiendo el control de entrega y la recepción física de la mercadería.
- **Pago Parcial:** Registro de una entrega de dinero que cubre solo una fracción del valor del lavado, dejando el estado de pago en `Parcial`.
- **Paquete de Servicios:** Combinación comercial de dos o más servicios activos para un mismo tipo de vehículo, que se ofrece bajo un precio bonificado mediante un porcentaje de descuento.
- **Permiso Granular:** Autorización específica con formato `Módulo.Acción` que determina si un usuario puede ver o ejecutar una tarea puntual en el sistema.
- **Realizado Parcialmente:** Estado final de un lavado en el cual se completaron uno o más servicios, pero otro de los servicios contratados debió ser cancelado por algún motivo operativo.
- **Receta de Insumo:** Definición técnica de los insumos y dosis requeridas para llevar a cabo un servicio o etapa determinada.
- **Retiro de Vehículo:** Acto físico y administrativo mediante el cual se entrega el vehículo lavado a la persona autorizada, una vez cumplimentadas las condiciones de trabajo y pago.
- **Rol:** Conjunto agrupado de permisos que se asigna a las cuentas de los empleados para determinar sus accesos en la aplicación web.
- **Solicitud de Cotización (RFQ):** Consulta formal de precios, disponibilidad y plazos de entrega enviada a uno o varios proveedores respecto a una lista de insumos.
- **Stock Disponible:** Cantidad de insumo que no está comprometida por lavados en ejecución y que puede utilizarse libremente (`Stock Físico - Stock Reservado`).
- **Stock Físico:** Cantidad total real de unidades o volumen de un insumo que se encuentra depositada físicamente en el lavadero.
- **Stock Reservado:** Cantidad de insumo asignada a lavados que se encuentran actualmente en estado `EnProceso`.
- **Tipo de Insumo:** Clasificación o familia de productos (ej: Químicos, Físicos, Herramientas).
- **Tipo de Servicio:** Categoría funcional de la tarea realizada (ej: Lavado Básico, Lavado Premium, Encerado, Limpieza Interior).
- **Tipo de Vehículo:** Segmentación vehicular (ej: Automóvil, Camioneta, SUV, Motocicleta) que determina la compatibilidad de servicios, paquetes y tarifas.
