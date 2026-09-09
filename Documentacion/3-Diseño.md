# LavaFácil

**Sistema de Gestión de Servicios y Turnos para Lavaderos de Vehículos**

**Documento de Requisitos del Sistema**

**Versión 2.0**

**Fecha:** 20/07/2026

**Realizado por:** Gelabert André

---

## **Diagrama de clases**

A continuación se presenta el modelo de clases del sistema.



## **Diagrama de base de datos**

A continuación se presenta el modelo de base de datos del sistema.



## **Casos de Uso Reales del Sistema**

## **Casos de Uso del Reales**

### Módulo: Seguridad

### CU-001 - Iniciar Sesión

| UC–001 | Iniciar Sesión | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | El usuario accede al sistema mediante sus credenciales registradas. Este caso de uso es la puerta de entrada al sistema y verifica que el usuario tenga estado activo. | |
| **Precondición** | El usuario debe tener una cuenta registrada en el sistema. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la página de inicio de sesión. |
| | 2 | El sistema muestra un formulario con opciones de inicio de sesión: correo/contraseña o Google. |
| | 3 | El usuario selecciona el método de autenticación. |
| | 4 | Se ejecuta el caso de uso correspondiente (CU-001.1 o CU-001.2). |
| | 5 | El sistema verifica que el usuario esté activo en el sistema. |
| | 6 | El sistema crea una sesión autenticada con cookies seguras. |
| | 7 | El sistema redirige al usuario al dashboard principal. |
| | 8 | El sistema registra el evento de inicio de sesión en auditoría. |
| **Postcondición** | El usuario ha iniciado sesión correctamente y tiene acceso al sistema según su rol. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si el usuario está inactivo, el sistema muestra mensaje de cuenta deshabilitada y no permite el acceso. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 4-7 | 2 segundos |
| **Frecuencia** | Diaria (múltiples veces) | |
| **Estabilidad** | Alta | |
| **Comentarios** | La autenticación se gestiona a través de Firebase Authentication. | |

---

### CU-001.1 - Iniciar sesión con correo y contraseña

| UC–001.1 | Iniciar sesión con correo y contraseña | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | Extiende de CU-001. El usuario ingresa su correo electrónico y contraseña para validar su identidad mediante Firebase Authentication. Se verifica que el correo esté verificado antes de permitir el acceso. | |
| **Precondición** | El usuario debe tener una cuenta registrada con correo y contraseña. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario ingresa su correo electrónico en el formulario. |
| | 2 | El usuario ingresa su contraseña. |
| | 3 | El usuario hace clic en el botón "Iniciar Sesión". |
| | 4 | El sistema envía las credenciales a Firebase Authentication. |
| | 5 | Firebase valida las credenciales y retorna el token de autenticación. |
| | 6 | El sistema verifica que el email esté verificado. |
| | 7 | El sistema continúa con el paso 5 del CU-001. |
| **Postcondición** | Las credenciales han sido validadas correctamente. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si las credenciales son inválidas, el sistema muestra "Correo o contraseña incorrectos". |
| | 6a | Si el email no está verificado, el sistema muestra mensaje indicando que debe verificar su correo y ofrece reenviar el email de verificación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 4-5 | 2 segundos |
| **Frecuencia** | Diaria | |
| **Estabilidad** | Alta | |
| **Comentarios** | Ninguno. | |

---

### CU-001.2 - Iniciar sesión con Google

| UC–001.2 | Iniciar sesión con Google | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | Extiende de CU-001. El usuario se autentica mediante Google Authentication, evitando la necesidad de contraseña propia. Si es un usuario nuevo, se ejecuta CU-011 para crear automáticamente su perfil. | |
| **Precondición** | El usuario debe tener una cuenta de Google válida. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario hace clic en el botón "Continuar con Google". |
| | 2 | El sistema redirige al flujo de autenticación OAuth de Google. |
| | 3 | El usuario selecciona su cuenta de Google y autoriza el acceso. |
| | 4 | Google retorna el token de autenticación al sistema. |
| | 5 | El sistema verifica si el usuario existe en la base de datos. |
| | 6a | Si existe, continúa con el paso 5 del CU-001. |
| | 6b | Si no existe, se ejecuta CU-011 para registrar el nuevo usuario. |
| **Postcondición** | El usuario ha sido autenticado mediante Google. | |
| **Excepciones** | **Paso** | **Acción** |
| | 3a | Si el usuario cancela la autenticación de Google, se retorna al formulario de login. |
| | 4a | Si hay un error en la autenticación de Google, el sistema muestra el mensaje de error correspondiente. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-4 | 3 segundos |
| **Frecuencia** | Diaria | |
| **Estabilidad** | Alta | |
| **Comentarios** | El email de Google se considera verificado automáticamente. | |

---

### CU-002 - Cerrar sesión

| UC–002 | Cerrar sesión | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | El usuario cierra su sesión de manera manual desde la aplicación. Se invalida la sesión actual y se redirige a la página de login. | |
| **Precondición** | El usuario debe haber iniciado sesión previamente en el sistema. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario hace clic en el botón o enlace "Cerrar Sesión" del menú. |
| | 2 | El sistema invalida la sesión actual eliminando las cookies de autenticación. |
| | 3 | El sistema cierra la sesión en Firebase Authentication. |
| | 4 | El sistema redirige al usuario a la página de inicio de sesión. |
| | 5 | El sistema registra el evento de cierre de sesión en auditoría. |
| **Postcondición** | El usuario ha cerrado sesión exitosamente y no puede acceder a páginas protegidas sin autenticarse nuevamente. | |
| **Excepciones** | **Paso** | **Acción** |
| | - | - |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-4 | 1 segundo |
| **Frecuencia** | Diaria | |
| **Estabilidad** | Alta | |
| **Comentarios** | Ninguno. | |

---

### CU-003 - Recuperar contraseña

| UC–003 | Recuperar contraseña | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | El sistema permite al usuario recuperar su contraseña mediante un enlace enviado a su correo electrónico a través de Firebase Authentication. | |
| **Precondición** | El usuario debe tener una cuenta registrada con correo y contraseña. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario hace clic en "¿Olvidaste tu contraseña?" en la página de login. |
| | 2 | El sistema muestra un formulario solicitando el correo electrónico. |
| | 3 | El usuario ingresa su correo electrónico y hace clic en "Enviar". |
| | 4 | El sistema solicita a Firebase Authentication el envío del correo de recuperación. |
| | 5 | Firebase envía un correo con el enlace para restablecer la contraseña. |
| | 6 | El sistema muestra un mensaje de confirmación indicando que se envió el correo. |
| | 7 | El usuario accede al enlace del correo y establece una nueva contraseña. |
| **Postcondición** | El usuario ha restablecido su contraseña y puede iniciar sesión con la nueva. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si el correo no está registrado, Firebase no envía el correo pero el sistema muestra el mensaje de éxito por seguridad (no revelar si el correo existe). |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 4-5 | 5 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | El enlace de recuperación es gestionado completamente por Firebase Authentication. | |

---

### CU-004 - Cierre de sesión automático por inactividad

| UC–004 | Cierre de sesión automático por inactividad | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados, IRQ–11 Información de Configuración | |
| **Descripción** | El sistema cierra automáticamente la sesión de los usuarios tras un periodo prolongado de inactividad configurable desde el módulo de configuración. | |
| **Precondición** | El usuario tiene una sesión activa y no ha realizado acciones durante el tiempo configurado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El sistema monitorea la actividad del usuario en la aplicación. |
| | 2 | El sistema detecta que ha transcurrido el tiempo máximo de inactividad configurado. |
| | 3 | El sistema invalida la sesión del usuario automáticamente. |
| | 4 | Cuando el usuario intenta realizar una acción, es redirigido a la página de login. |
| | 5 | El sistema muestra un mensaje indicando que la sesión expiró por inactividad. |
| **Postcondición** | La sesión del usuario ha sido cerrada y debe autenticarse nuevamente. | |
| **Excepciones** | **Paso** | **Acción** |
| | - | - |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 3 | Inmediato |
| **Frecuencia** | Variable | |
| **Estabilidad** | Media | |
| **Comentarios** | El tiempo de inactividad es configurable por el administrador en CU-064. | |

---

### Módulo: Gestión de Empleados

### CU-005 - Registrarse en el sistema

| UC–005 | Registrarse en el sistema | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados, OBJ–09 Gestión de Seguridad | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | Un nuevo usuario crea su perfil en el sistema, quedando registrado como empleado con rol por defecto de 'Empleado'. Requiere verificación de correo electrónico. | |
| **Precondición** | El correo electrónico no debe estar registrado previamente. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la página de registro. |
| | 2 | El sistema muestra opciones de registro: correo/contraseña o Google. |
| | 3 | El usuario selecciona el método de registro. |
| | 4 | Se ejecuta el caso de uso correspondiente (CU-005.1 o CU-005.2). |
| | 5 | El sistema crea el registro del empleado con rol "Empleado" y estado "Activo". |
| | 6 | El sistema registra la acción en auditoría. |
| | 7 | El sistema muestra mensaje de éxito. |
| **Postcondición** | El nuevo empleado está registrado y puede acceder al sistema tras verificar su email (si aplica). | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si el correo ya existe, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 4-5 | 3 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | Ninguno. | |

---

### CU-005.1 - Registrarse por correo

| UC–005.1 | Registrarse por correo | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados, OBJ–09 Gestión de Seguridad | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | Extiende de CU-005. El usuario completa un formulario con correo y contraseña para generar su cuenta. Se envía un correo de verificación que debe ser confirmado antes de poder iniciar sesión. | |
| **Precondición** | Ninguna adicional. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El sistema muestra formulario con campos: Nombre, Apellido, Correo, Contraseña, Confirmar Contraseña. |
| | 2 | El usuario completa todos los campos. |
| | 3 | El sistema valida que las contraseñas coincidan y cumplan requisitos de seguridad. |
| | 4 | El sistema crea la cuenta en Firebase Authentication. |
| | 5 | Firebase envía un correo de verificación al usuario. |
| | 6 | El sistema muestra mensaje indicando que debe verificar su correo para iniciar sesión. |
| | 7 | Se continúa con el paso 5 del CU-005. |
| **Postcondición** | La cuenta está creada pero requiere verificación de email. | |
| **Excepciones** | **Paso** | **Acción** |
| | 3a | Si las contraseñas no coinciden, el sistema muestra el error. |
| | 3b | Si la contraseña no cumple requisitos, el sistema indica los requisitos faltantes. |
| | 4a | Si el correo ya existe en Firebase, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 4-5 | 3 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | Ninguno. | |

---

### CU-005.2 - Registrarse por Google

| UC–005.2 | Registrarse por Google | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados, OBJ–09 Gestión de Seguridad | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | Extiende de CU-005. El registro se realiza mediante autenticación directa con Google, verificando automáticamente el correo electrónico. | |
| **Precondición** | El usuario debe tener una cuenta de Google válida. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario hace clic en "Registrarse con Google". |
| | 2 | El sistema redirige al flujo de autenticación OAuth de Google. |
| | 3 | El usuario selecciona su cuenta de Google y autoriza el acceso. |
| | 4 | Google retorna los datos del usuario (nombre, apellido, correo). |
| | 5 | El sistema verifica que el correo no esté registrado. |
| | 6 | Se continúa con el paso 5 del CU-005 usando los datos de Google. |
| **Postcondición** | La cuenta está creada y verificada automáticamente. | |
| **Excepciones** | **Paso** | **Acción** |
| | 3a | Si el usuario cancela, se retorna al formulario de registro. |
| | 5a | Si el correo ya existe, el sistema informa que debe iniciar sesión en lugar de registrarse. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-4 | 3 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | El email de Google se considera verificado automáticamente. | |

---

### CU-006 - Modificar empleado

| UC–006 | Modificar empleado | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | El administrador actualiza la información de un empleado existente, incluyendo nombre completo y rol asignado. | |
| **Precondición** | El usuario actual debe tener rol de Administrador. El empleado debe existir en el sistema. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-009 Consultar empleados. |
| | 2 | El administrador selecciona el empleado a modificar haciendo clic en el botón de edición. |
| | 3 | El sistema muestra el formulario de edición con los datos actuales: Nombre, Apellido, Rol. |
| | 4 | El administrador modifica los campos deseados. |
| | 5 | El administrador hace clic en "Guardar". |
| | 6 | El sistema valida los datos ingresados. |
| | 7 | El sistema actualiza el registro del empleado en la base de datos. |
| | 8 | El sistema muestra un mensaje de éxito. |
| | 9 | El sistema registra la acción en auditoría. |
| **Postcondición** | Los datos del empleado han sido actualizados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 6a | Si hay errores de validación, el sistema muestra los errores específicos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 7 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-009 Consultar empleados. | |

---

### CU-007 - Desactivar empleado

| UC–007 | Desactivar empleado | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | El administrador desactiva a un empleado del sistema. Un empleado desactivado no puede iniciar sesión hasta ser reactivado. | |
| **Precondición** | El usuario actual debe tener rol de Administrador. El empleado debe existir y estar activo. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-009 Consultar empleados. |
| | 2 | El administrador selecciona el empleado a desactivar. |
| | 3 | El sistema muestra un diálogo de confirmación con mensaje de advertencia. |
| | 4 | El administrador confirma la desactivación. |
| | 5 | El sistema cambia el estado del empleado a "Inactivo". |
| | 6 | El sistema muestra un mensaje de éxito. |
| | 7 | El sistema registra la acción en auditoría. |
| **Postcondición** | El empleado ha sido desactivado y no puede acceder al sistema. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si el administrador cancela, se aborta la operación. |
| | 5a | Si el empleado es el único administrador activo, el sistema impide la desactivación mostrando mensaje de error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 5 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-009 Consultar empleados. La desactivación es lógica, no física. | |

---

### CU-008 - Reactivar empleado

| UC–008 | Reactivar empleado | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | El administrador reactiva a un empleado previamente desactivado, permitiéndole nuevamente iniciar sesión en el sistema. | |
| **Precondición** | El usuario actual debe tener rol de Administrador. El empleado debe existir y estar inactivo. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-009 Consultar empleados (filtrando por inactivos). |
| | 2 | El administrador selecciona el empleado a reactivar. |
| | 3 | El sistema muestra un diálogo de confirmación. |
| | 4 | El administrador confirma la reactivación. |
| | 5 | El sistema cambia el estado del empleado a "Activo". |
| | 6 | El sistema muestra un mensaje de éxito. |
| | 7 | El sistema registra la acción en auditoría. |
| **Postcondición** | El empleado ha sido reactivado y puede acceder nuevamente al sistema. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si el administrador cancela, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 5 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-009 Consultar empleados. | |

---

### CU-009 - Consultar empleados

| UC–009 | Consultar empleados | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | El administrador visualiza la lista de empleados registrados en el sistema con opciones de filtrado por estado y rol, ordenamiento y paginación. | |
| **Precondición** | El usuario actual debe tener rol de Administrador. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de gestión de empleados desde el menú. |
| | 2 | El sistema obtiene la lista de empleados aplicando filtros por defecto (Activos). |
| | 3 | El sistema muestra la tabla de empleados con columnas: Nombre, Apellido, Correo, Rol, Estado. |
| | 4 | El administrador puede aplicar filtros por estado (Activo/Inactivo) y/o rol (Administrador/Empleado). |
| | 5 | El administrador puede ordenar por cualquier columna haciendo clic en el encabezado. |
| | 6 | El administrador puede navegar entre páginas usando los controles de paginación. |
| | 7 | El sistema actualiza la vista según los criterios seleccionados. |
| **Postcondición** | El administrador visualiza la lista de empleados según los filtros aplicados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 2a | Si no hay empleados, el sistema muestra un mensaje indicándolo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Por defecto muestra solo empleados activos. | |

---

### CU-010 - Asignar roles a empleados

| UC–010 | Asignar roles a empleados | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | El administrador define los roles de los empleados (Empleado o Administrador) para determinar sus permisos en el sistema. | |
| **Precondición** | El usuario actual debe tener rol de Administrador. El empleado debe existir en el sistema. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la edición del empleado (incluye CU-006). |
| | 2 | El sistema muestra el campo de selección de Rol con las opciones disponibles. |
| | 3 | El administrador selecciona el nuevo rol (Administrador o Empleado). |
| | 4 | El administrador guarda los cambios. |
| | 5 | El sistema valida que quede al menos un administrador activo si se está degradando un administrador. |
| | 6 | El sistema actualiza el rol del empleado. |
| | 7 | El sistema registra la acción en auditoría con detalle del cambio de rol. |
| **Postcondición** | El rol del empleado ha sido actualizado y los permisos se aplican inmediatamente. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si no quedaría ningún administrador activo, el sistema impide el cambio y muestra mensaje de error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | Los roles disponibles son: Administrador y Empleado. Este caso de uso se realiza a través de CU-006. | |

---

### CU-011 - Autenticar usuario con Google y registrar perfil si es nuevo

| UC–011 | Autenticar usuario con Google y registrar perfil si es nuevo | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–01 Gestión de Empleados, OBJ–09 Gestión de Seguridad | |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados | |
| **Descripción** | El sistema valida un inicio de sesión por Google y, si no existe el usuario, lo registra automáticamente con los datos del perfil de Google y rol por defecto "Empleado". | |
| **Precondición** | El usuario ha completado la autenticación de Google exitosamente (desde CU-001.2). | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El sistema recibe los datos del perfil de Google (UID, nombre, apellido, correo). |
| | 2 | El sistema busca en la base de datos un empleado con el UID de Google. |
| | 3 | Si no existe, el sistema crea un nuevo registro de empleado. |
| | 4 | El sistema asigna los datos del perfil de Google: Nombre, Apellido, Correo. |
| | 5 | El sistema asigna el rol por defecto "Empleado" y estado "Activo". |
| | 6 | El sistema establece EmailVerificado como verdadero. |
| | 7 | El sistema guarda el nuevo empleado en la base de datos. |
| | 8 | El sistema registra la acción en auditoría como "Registro automático por Google". |
| **Postcondición** | El usuario existe en el sistema y puede continuar con el inicio de sesión. | |
| **Excepciones** | **Paso** | **Acción** |
| | 2a | Si el usuario ya existe, el sistema simplemente continúa con el flujo de login. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 3-7 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | Este caso de uso es ejecutado automáticamente por el sistema. | |
### Módulo: Gestión de Clientes y Vehículos

### CU-012 - Crear cliente

| UC–012 | Crear cliente | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos | |
| **Descripción** | El personal del lavadero registra un nuevo cliente en el sistema. Es de carácter estrictamente obligatorio que el cliente posea al menos un vehículo asociado para completar el alta. El flujo permite la carga repetitiva de múltiples vehículos mediante un bucle de opciones integradas. | |
| **Precondición** | El usuario debe estar autenticado con rol de Trabajador o Administrador. Deben existir tipos de vehículo activos. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | **Sección:** | **Principal** |
| | 1 | El usuario accede a la sección de gestión de clientes desde el menú. |
| | 2 | El usuario hace clic en el botón "Nuevo Cliente". |
| | 3 | El sistema muestra un formulario con campos: Tipo Documento, Número Documento, Nombre, Apellido, Teléfono, Email. |
| | 4 | El sistema carga dinámicamente los tipos de documento disponibles. |
| | 5 | El usuario completa los datos personales del cliente. |
| | 6 | **Bucle de Gestión de Vehículos [Obligatorio: Mínimo 1]:** Mientras el usuario necesite incorporar vehículos a la flota del cliente, selecciona una de las siguientes opciones por cada unidad:<br>a) Si el vehículo es nuevo: consúltese la sección *Opción: Registrar Vehículo Nuevo*.<br>b) Si el vehículo ya existe en el sistema: consúltese la sección *Opción: Vincular Vehículo Existente*. |
| | 7 | Por cada vehículo finalizado con éxito en las subsecciones, el sistema lo lista en una grilla temporal en la interfaz y habilita el botón "Confirmar Alta". |
| | 8 | El usuario hace clic en el botón "Confirmar Alta". |
| | 9 | El sistema valida de forma transaccional los datos del cliente junto con la lista de vehículos acumulados en el bucle, persiste las entidades en estado "Activo" en la base de datos, muestra un mensaje de éxito y registra la acción en auditoría. |
| | **Sección:** | **Opción: Registrar Vehículo Nuevo** |
| | 1 | El usuario selecciona la opción de añadir un nuevo vehículo desde el formulario. |
| | 2 | El sistema invoca de forma contextual la ejecución del **CU-018 - Crear vehículo**. Al finalizar, se retorna al *Bucle de Gestión de Vehículos* de la sección principal. |
| | **Sección:** | **Opción: Vincular Vehículo Existente** |
| | 1 | El usuario selecciona la opción de asociar un vehículo existente por patente. |
| | 2 | El sistema invoca de forma contextual la ejecución del **CU-024 - Vincular vehículo a cliente**. Al finalizar, se retorna al *Bucle de Gestión de Vehículos* de la sección principal. |
| **Postcondición** | El cliente queda registrado en el sistema con su correspondiente flota de vehículos asociados (mínimo un vehículo requerido). | |
| **Excepciones** | **Paso** | **Acción** |
| | 8a | El usuario intenta confirmar el alta sin haber cargado ningún vehículo en el bucle: El sistema bloquea la acción, muestra una alerta indicando la obligatoriedad de asociar un vehículo y congela la persistencia. |
| | 9a | Si los datos del cliente son inválidos o el número de documento ya existe, el sistema informa el error y detiene la transacción. |
| | 8b | Si el usuario cancela el formulario principal antes de confirmar, se descartan todos los datos personales y las operaciones temporales hechas dentro del bucle de vehículos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 9 | 2 segundos |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | El alta inicial cliente-vehículo se consolida transaccionalmente en este flujo unificado. CU-024 se utiliza para asociaciones adicionales posteriores. | |

---

### CU-013 - Modificar cliente

| UC–013 | Modificar cliente | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos | |
| **Descripción** | El personal actualiza la información de un cliente existente y permite gestionar su flota de vehículos asociados (añadiendo, vinculando o quitando unidades) mediante un bucle de opciones. | |
| **Precondición** | El usuario debe estar autenticado. El cliente debe existir y estar activo. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | **Sección:** | **Principal** |
| | 1 | Se ejecuta el caso de uso **CU-016 Consultar clientes** y se selecciona el perfil del cliente a editar. |
| | 2 | El sistema muestra el formulario con los datos actuales del cliente y una sección con el listado de sus vehículos vinculados. |
| | 3 | El usuario modifica los campos editables del cliente (Nombre, Apellido, Teléfono, Email). |
| | 4 | **Bucle de Gestión de Vehículos [Opcional: 0 o más]:** Mientras el usuario desee actualizar la flota del cliente, selecciona una opción por cada unidad:<br>a) Si desea registrar un vehículo nuevo: consúltese *Opción: Registrar Vehículo Nuevo*.<br>b) Si desea asociar otro vehículo existente: consúltese *Opción: Vincular Vehículo Existente*.<br>c) Si desea remover un vehículo de la lista: consúltese *Opción: Quitar Vehículo de la Flota*. |
| | 5 | Por cada vehículo procesado en las subsecciones, el sistema actualiza temporalmente la lista de la flota en la interfaz de cambios. |
| | 6 | El usuario hace clic en el botón "Guardar Cambios". |
| | 7 | El sistema valida los datos modificados del cliente junto con la cola de cambios acumulada en el bucle, impacta la base de datos de manera unificada, muestra un mensaje de éxito y registra la acción en auditoría. |
| | **Sección:** | **Opción: Registrar Vehículo Nuevo** |
| | 1 | El usuario presiona el botón para agregar un nuevo vehículo a la lista del cliente. |
| | 2 | El sistema invoca de forma contextual la ejecución del **CU-018 - Crear vehículo**. Al cerrar el modal, se retorna al *Bucle de Gestión de Vehículos* de la sección principal. |
| | **Sección:** | **Opción: Vincular Vehículo Existente** |
| | 1 | El usuario presiona el botón para asociar un vehículo existente. |
| | 2 | El sistema invoca de forma contextual la ejecución del **CU-024 - Vincular vehículo a cliente**. Al cerrar el modal, se retorna al *Bucle de Gestión de Vehículos* de la sección principal. |
| | **Sección:** | **Opción: Quitar Vehículo de la Flota** |
| | 1 | El usuario selecciona un vehículo de la grilla del cliente y presiona el botón "Quitar/Eliminar". |
| | 2 | El sistema evalúa la co-propiedad de la unidad: si es un vehículo unipersonal (solo asociado a este cliente), prepara su baja lógica mediante **CU-020 - Desactivar vehículo**. Si es multipersonal (asociado a más dueños activos), solo prepara la desvinculación de este cliente manteniendo el vehículo activo. Al finalizar, retorna al *Bucle de Gestión de Vehículos*. |
| **Postcondición** | Los datos del cliente y sus asociaciones o estados de vehículos son actualizados de forma segura en la base de datos. | |
| **Excepciones** | **Paso** | **Acción** |
| | 7a | Si hay errores de validación en los campos modificados, el sistema frena el flujo y muestra los errores específicos. |
| | 6a | Si el usuario cancela la edición principal, se descartan todas las modificaciones y las operaciones temporales del bucle de vehículos sin alterar la base de datos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 7 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-016 Consultar clientes. Los campos Tipo y Número de documento no son editables por integridad del negocio. | |

---

### CU-014 - Desactivar cliente

| UC–014 | Desactivar cliente | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos | |
| **Descripción** | El administrador desactiva un cliente del sistema de forma lógica. La desactivación desencadena un análisis de su flota para desactivar vehículos exclusivos o desvincular los compartidos. | |
| **Precondición** | El usuario debe tener rol de Administrador. El cliente debe existir y estar activo. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | **Sección:** | **Principal** |
| | 1 | Se ejecuta el caso de uso **CU-016 Consultar clientes**. |
| | 2 | El administrador selecciona el cliente a desactivar y presiona "Desactivar". |
| | 3 | El sistema analiza la lista de vehículos vinculados al cliente para identificar cuáles son de propiedad exclusiva (unipersonales) y cuáles son compartidos (multipersonales). |
| | 4 | El sistema muestra un diálogo de confirmación detallando que el cliente pasará a estar inactivo, se desvinculará de los vehículos compartidos y se desactivarán por completo sus vehículos exclusivos. |
| | 5 | El administrador confirma la operación. |
| | 6 | El sistema cambia el estado del cliente a "Inactivo". |
| | 7 | **Bucle de Cascada de Flota:** Por cada vehículo que pertenece a la flota del cliente:<br>a) Si el vehículo es unipersonal: el sistema invoca de manera automática el **CU-020 - Desactivar vehículo**.<br>b) Si el vehículo es multipersonal: el sistema remueve la relación con este cliente, pero mantiene el vehículo en estado "Activo" para los co-propietarios restantes. |
| | 8 | El sistema muestra un mensaje de éxito confirmando la baja lógica del cliente y el procesamiento de su flota. |
| | 9 | El sistema registra la acción en el historial de auditoría. |
| **Postcondición** | El cliente queda inactivo y su flota es procesada resguardando la co-propiedad de vehículos compartidos. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si el administrador cancela en el diálogo de confirmación, se aborta la operación completa sin alterar datos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6-7 | 2 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-016 Consultar clientes. La baja siempre es lógica. | |

---

### CU-015 - Reactivar cliente

| UC–015 | Reactivar cliente | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos | |
| **Descripción** | El administrador reactiva un cliente previamente desactivado. La reactivación vuelve a activar de forma automática aquellos vehículos exclusivos que se habían desactivado en cascada junto con él. | |
| **Precondición** | El usuario debe tener rol de Administrador. El cliente debe existir y estar inactivo. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | **Sección:** | **Principal** |
| | 1 | Se ejecuta el caso de uso **CU-016 Consultar clientes** (utilizando el filtro de estado "Inactivo"). |
| | 2 | El administrador selecciona el cliente a reactivar y presiona "Reactivar". |
| | 3 | El sistema identifica los vehículos de la flota que pasaron a estar inactivos de forma exclusiva y directa debido a la baja de este cliente. |
| | 4 | El sistema muestra un diálogo de confirmación listando los vehículos que se reactivarán de forma conjunta con el usuario. |
| | 5 | El administrador confirma la reactivación. |
| | 6 | El sistema cambia el estado del cliente a "Activo". |
| | 7 | **Bucle de Reactivación de Flota:** Por cada vehículo exclusivo identificado en el paso 3, el sistema invoca automáticamente el caso de uso **CU-021 - Reactivar vehículo**. |
| | 8 | El sistema muestra un mensaje de éxito en pantalla. |
| | 9 | El sistema registra la reactivación en el historial de auditoría. |
| **Postcondición** | El cliente y sus vehículos de propiedad exclusiva vuelven a estar activos y disponibles para operar. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si el administrador cancela la confirmación, se interrumpe la operación sin impactar cambios. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6-7 | 2 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Los vehículos multipersonales que se mantuvieron activos con otros dueños no sufren alteraciones en este caso de uso. | |

---

### CU-016 - Consultar clientes

| UC–016 | Consultar clientes | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes | |
| **Descripción** | El personal consulta la lista de clientes registrados en el sistema con opciones de filtrado por estado, ordenamiento y paginación. | |
| **Precondición** | El usuario debe estar autenticado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la sección de gestión de clientes desde el menú. |
| | 2 | El sistema obtiene la lista de clientes aplicando filtros por defecto (Activos). |
| | 3 | El sistema muestra la tabla de clientes con columnas: Nombre Completo, Documento, Teléfono, Email, Estado. |
| | 4 | El usuario puede aplicar filtros por estado (Activo/Inactivo). |
| | 5 | El usuario puede ordenar por cualquier columna haciendo clic en el encabezado. |
| | 6 | El usuario puede navegar entre páginas usando los controles de paginación. |
| | 7 | El sistema actualiza la vista según los criterios seleccionados. |
| **Postcondición** | El usuario visualiza la lista de clientes según los filtros aplicados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 2a | Si no hay clientes, el sistema muestra un mensaje indicándolo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-3 | 1 segundo |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Por defecto muestra solo clientes activos. | |

---

### CU-017 - Buscar clientes

| UC–017 | Buscar clientes | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes | |
| **Descripción** | El personal busca clientes por nombre, apellido, documento, teléfono o email. La búsqueda se realiza en tiempo real con resultados paginados. | |
| **Precondición** | El usuario debe estar autenticado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la sección de gestión de clientes. |
| | 2 | El usuario ingresa texto en el campo de búsqueda. |
| | 3 | El sistema busca coincidencias en: nombre, apellido, nombre completo, número de documento, teléfono y email. |
| | 4 | El sistema muestra los resultados filtrados en tiempo real. |
| | 5 | El usuario puede seleccionar un cliente de los resultados. |
| **Postcondición** | El usuario visualiza los clientes que coinciden con el criterio de búsqueda. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si no hay coincidencias, el sistema muestra mensaje "Sin resultados". |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 3-4 | 500ms |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | La búsqueda es insensible a mayúsculas/minúsculas. | |

---

### CU-018 - Crear vehículo

| UC–018 | Crear vehículo | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos, IRQ–06 Información sobre Tipos de Vehículo | |
| **Descripción** | Permite registrar un vehículo totalmente nuevo en el sistema. Este caso de uso no es de acceso libre; se ejecuta de manera dependiente y contextual únicamente cuando es llamado desde las opciones cíclicas de los flujos de cliente. | |
| **Precondición** | El sistema se encuentra ejecutando la sección cíclica de opción de vehículos dentro de los flujos de **CU-012** o **CU-013**. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | **Sección:** | **Principal** |
| | 1 | El caso de uso comienza cuando es invocado por la sección cíclica del caso de uso base de clientes. |
| | 2 | El sistema abre un modal flotante sobre la interfaz con el formulario de registro de vehículo. |
| | 3 | El usuario selecciona el Tipo de Vehículo (Auto, Camioneta, Moto). |
| | 4 | El sistema carga dinámicamente los formatos de patente y reglas de negocio según el tipo seleccionado. |
| | 5 | El usuario ingresa la Patente. |
| | 6 | El sistema ofrece ayudas de autocompletado para marca/modelo (desde la API CarQuery si aplica). |
| | 7 | El usuario completa los campos restantes de Marca, Modelo y Color. |
| | 8 | El usuario confirma el modal. |
| | 9 | El sistema valida localmente que la patente no pertenezca a otro vehículo activo, genera la clave de asociación (hash SHA256) y añade el registro temporalmente a la lista de cambios del cliente en contexto. Cierra el modal, finaliza el caso de uso y retorna al flujo del cliente invocador. |
| **Postcondición** | El vehículo queda preparado en memoria temporal para su posterior persistencia final en cascada junto con el cliente. | |
| **Excepciones** | **Paso** | **Acción** |
| | 9a | Si la patente ingresada ya está registrada y activa en el lavadero, el sistema frena el flujo e informa el error (sugiriendo usar la vinculación). |
| | 8a | Si el usuario cierra o cancela el modal, se descartan los datos temporales de este vehículo y se vuelve al bucle del cliente sin registrar cambios. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 9 | Inmediato (en memoria del formulario principal) |
| **Frecuencia** | Frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Este caso de uso no persiste datos de forma autónoma; depende y delega su éxito al almacenamiento del caso de uso base (CU-012 o CU-013). | |

---

### CU-019 - Modificar vehículo

| UC–019 | Modificar vehículo | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos | |
| **Descripción** | El personal actualiza los datos editables de un vehículo (modelo y color). La patente, tipo de vehículo y marca no son editables una vez registrado el vehículo. | |
| **Precondición** | El usuario debe estar autenticado. El vehículo debe existir en el sistema. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-022 Consultar vehículos. |
| | 2 | El usuario selecciona el vehículo a modificar haciendo clic en el botón de edición. |
| | 3 | El sistema muestra el formulario de edición con los datos actuales. |
| | 4 | El sistema muestra los campos no editables (Patente, Tipo, Marca) como solo lectura. |
| | 5 | El usuario modifica los campos editables (Modelo, Color). |
| | 6 | El usuario hace clic en "Guardar". |
| | 7 | El sistema valida los datos modificados. |
| | 8 | El sistema actualiza el registro del vehículo en la base de datos. |
| | 9 | El sistema muestra un mensaje de éxito. |
| | 10 | El sistema registra la acción en auditoría. |
| **Postcondición** | Los datos editables del vehículo han sido actualizados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 7a | Si hay errores de validación, el sistema muestra los errores específicos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 8 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-022 Consultar vehículos. | |

---

### CU-020 - Desactivar vehículo

| UC–020 | Desactivar vehículo | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos | |
| **Descripción** | Permite cambiar el estado de un vehículo a "Inactivo" de forma lógica. Este caso de uso no puede ser accedido de manera autónoma en el sistema; es invocado exclusivamente de forma contextual desde los flujos de modificación o desactivación de un cliente. | |
| **Precondición** | El sistema se encuentra ejecutando un flujo de desvinculación o baja en **CU-013** o **CU-014** y el vehículo no cuenta con otros dueños activos. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | **Sección:** | **Principal** |
| | 1 | El caso de uso comienza cuando es llamado por el contexto de un cliente al detectar que el vehículo ha quedado sin dueños activos asociados. |
| | 2 | El sistema cambia el estado de la entidad Vehículo a "Inactivo". |
| | 3 | El sistema remueve los identificadores de cliente de la lista de ClientesIds del vehículo, dejándolo sin propietario temporal. |
| | 4 | El sistema guarda los cambios del estado del vehículo en memoria temporal (si viene de CU-013) o los persiste inmediatamente en cascada (si viene de CU-014), finalizando su ejecución y retornando al flujo del cliente invocador. |
| **Postcondición** | El vehículo queda marcado como inactivo en el sistema, lo que impide que sea seleccionado para nuevos turnos o lavados. | |
| **Excepciones** | **Paso** | **Acción** |
| | - | No posee excepciones autónomas ya que la validación de propiedad se realiza de forma previa en el caso de uso base. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-3 | Inmediato |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | La desactivación es estrictamente lógica para preservar el historial operativo de lavados anteriores en los módulos de reportes y auditoría. | |

---

### CU-021 - Reactivar vehículo

| UC–021 | Reactivar vehículo | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos | |
| **Descripción** | Permite cambiar el estado de un vehículo de "Inactivo" a "Activo" de forma lógica. No es un caso de uso de acceso libre; es llamado únicamente desde el flujo de reactivación en cascada de un cliente. | |
| **Precondición** | El sistema se encuentra ejecutando la reactivación transaccional de un cliente en **CU-015**. El vehículo debe estar inactivo. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | **Sección:** | **Principal** |
| | 1 | El caso de uso comienza cuando es invocado de manera automática por el bucle de reactivación de flota de CU-015. |
| | 2 | El sistema cambia el estado del vehículo a "Activo". |
| | 3 | El sistema vuelve a asociar el identificador del cliente en la lista ClientesIds del vehículo. |
| | 4 | El sistema persiste el cambio de estado del vehículo en la base de datos junto con la transacción del cliente, finaliza su ejecución y retorna al flujo base. |
| **Postcondición** | El vehículo vuelve a estar en estado activo y disponible en el sistema. | |
| **Excepciones** | **Paso** | **Acción** |
| | - | No posee excepciones autónomas al depender del éxito transaccional del caso de uso principal. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-3 | Inmediato |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | Creado para acompañar las restauraciones de cuentas de clientes sin perder la integridad referencial de los autos asociados. | |

---

### CU-022 - Consultar vehículos

| UC–022 | Consultar vehículos | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos | |
| **Descripción** | El personal visualiza la lista de vehículos registrados con opciones de filtrado por tipo de vehículo, marca, color y estado. | |
| **Precondición** | El usuario debe estar autenticado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la sección de gestión de vehículos desde el menú. |
| | 2 | El sistema obtiene la lista de vehículos aplicando filtros por defecto (Activos). |
| | 3 | El sistema muestra la tabla de vehículos con columnas: Patente, Tipo, Marca, Modelo, Color, Dueño Principal, Estado. |
| | 4 | El usuario puede aplicar filtros por: estado, tipo de vehículo, marca, color. |
| | 5 | El usuario puede ordenar por cualquier columna haciendo clic en el encabezado. |
| | 6 | El usuario puede navegar entre páginas usando los controles de paginación. |
| | 7 | El sistema actualiza la vista según los criterios seleccionados. |
| **Postcondición** | El usuario visualiza la lista de vehículos según los filtros aplicados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 2a | Si no hay vehículos, el sistema muestra un mensaje indicándolo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Por defecto muestra solo vehículos activos. | |

---

### CU-023 - Buscar vehículos

| UC–023 | Buscar vehículos | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos | |
| **Descripción** | El personal busca vehículos por patente, marca o modelo. La búsqueda se realiza en tiempo real. | |
| **Precondición** | El usuario debe estar autenticado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la sección de gestión de vehículos. |
| | 2 | El usuario ingresa texto en el campo de búsqueda. |
| | 3 | El sistema busca coincidencias en: patente, marca, modelo. |
| | 4 | El sistema muestra los resultados filtrados en tiempo real. |
| | 5 | El usuario puede seleccionar un vehículo de los resultados. |
| **Postcondición** | El usuario visualiza los vehículos que coinciden con el criterio de búsqueda. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si no hay coincidencias, el sistema muestra mensaje "Sin resultados". |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 3-4 | 500ms |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | La búsqueda es insensible a mayúsculas/minúsculas. | |

---

### CU-024 - Vincular vehículo a cliente

| UC–024 | Vincular vehículo a cliente | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos | |
| **Descripción** | Permite asociar un vehículo que ya existe en la base de datos a un cliente diferente (co-propietarios), validando la seguridad mediante la clave de asociación. Se ejecuta de manera dependiente cuando es llamado desde las opciones de los flujos de cliente. | |
| **Precondición** | El sistema se encuentra ejecutando la sección cíclica de opción de vehículos dentro de los flujos de **CU-012** o **CU-013**. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | **Sección:** | **Principal** |
| | 1 | El caso de uso comienza cuando es invocado por la sección cíclica del caso de uso base de clientes. |
| | 2 | El sistema abre un modal flotante solicitando la patente del vehículo a vincular y su respectiva clave de seguridad. |
| | 3 | El usuario ingresa la Patente del vehículo y la Clave de Asociación provista por el dueño original. |
| | 4 | El usuario hace clic en confirmar dentro del modal. |
| | 5 | El sistema busca el vehículo en la base de datos por patente y valida que el hash SHA256 de la clave ingresada coincida con el almacenado. Si es correcto, añade la relación temporalmente a la cola de cambios del cliente en contexto. Cierra el modal, finaliza el caso de uso y retorna al flujo del cliente invocador. |
| **Postcondición** | La vinculación de co-propiedad queda lista en memoria temporal para impactar en la base de datos al confirmar el formulario del cliente. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si el vehículo no existe en el sistema o la clave de asociación es inválida, el sistema rechaza la vinculación y muestra el mensaje de error. |
| | 5b | Si el cliente ya posee ese vehículo asociado en su lista, el sistema lo notifica y no duplica la relación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 5 | < 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Garantiza que un vehículo solo sea compartido si se cuenta con la clave de seguridad generada originalmente. | |

---

### CU-025 - Desvincular vehículo de cliente

| UC–025 | Desvincular vehículo de cliente | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–03 Información sobre Vehículos | |
| **Descripción** | El personal desvincula un vehículo de un cliente. Si el vehículo queda sin clientes asociados, se desactiva automáticamente. | |
| **Precondición** | El usuario debe estar autenticado. El cliente debe tener el vehículo asociado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-022 Consultar vehículos o se accede desde la edición de un cliente. |
| | 2 | El usuario selecciona la opción "Desvincular" en el vehículo correspondiente. |
| | 3 | El sistema verifica cuántos clientes tiene asociado el vehículo. |
| | 4 | El sistema muestra un diálogo de confirmación (indicando si el vehículo será desactivado). |
| | 5 | El usuario confirma la desvinculación. |
| | 6 | El sistema remueve al cliente de la lista de ClientesIds del vehículo. |
| | 7 | El sistema remueve el vehículo de la lista de VehiculosIds del cliente. |
| | 8 | Si el vehículo queda sin clientes, el sistema lo desactiva automáticamente. |
| | 9 | El sistema muestra un mensaje de éxito. |
| | 10 | El sistema registra la acción en auditoría. |
| **Postcondición** | El cliente ya no está asociado al vehículo. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si el usuario cancela, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6-8 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-022 Consultar vehículos. | |

---

### CU-026 - Registrarse como cliente por WhatsApp

| UC–026 | Registrarse como cliente por WhatsApp | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–10 Integración con WhatsApp, OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–12 Información de Sesiones WhatsApp, IRQ–03 Información sobre Vehículos | |
| **Descripción** | El cliente se registra en el sistema mediante una interacción guiada con el bot de WhatsApp. Para confirmar el alta de forma exitosa, es estrictamente obligatorio que complete el bucle de registro de al menos un vehículo. Los datos se acumulan en la sesión antes de persistirse de forma unificada. | |
| **Precondición** | El número de WhatsApp no debe estar registrado previamente como un cliente activo en el sistema. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | **Sección:** | **Principal** |
| | 1 | El cliente envía un mensaje al número de WhatsApp del lavadero. |
| | 2 | El sistema ejecuta el CU-028 para verificar el número. Al comprobar que no existe, inicializa la WhatsAppSession e ingresa al flujo de registro de datos personales enviando un saludo inicial. |
| | 3 | El sistema solicita el Tipo de Documento mostrando las opciones válidas (DNI, Pasaporte, CUIL). |
| | 4 | El cliente selecciona el Tipo de Documento enviando el número de la opción. |
| | 5 | El sistema solicita el Número de Documento. |
| | 6 | El cliente ingresa su Número de Documento. |
| | 7 | El sistema valida el formato del documento y que no se encuentre duplicado. Si es correcto, solicita el Nombre. |
| | 8 | El cliente ingresa su Nombre. |
| | 9 | El sistema solicita el Apellido. |
| | 10 | El cliente ingresa su Apellido. |
| | 11 | El sistema solicita un Correo Electrónico (indicando que puede responder "omitir"). |
| | 12 | El cliente ingresa su correo electrónico o la palabra "omitir". |
| | 13 | El sistema guarda los datos personales temporalmente en el estado de la sesión (`TemporaryData`). |
| | 14 | **Bucle de Carga de Flota Inicial [Obligatorio: Mínimo 1]:** El sistema notifica al cliente que para finalizar su registro debe dar de alta al menos un vehículo. El sistema invoca automáticamente la sección principal del **CU-027 - Registrar vehículo por WhatsApp**. |
| | 15 | Tras retornar con éxito de las iteraciones de la flota, el sistema muestra un mensaje con el resumen de los datos personales y la lista de patentes cargadas, solicitando confirmación final (1: Confirmar, 2: Modificar). |
| | 16 | El cliente selecciona "Confirmar". |
| | 17 | El sistema persiste el Cliente con estado "Activo", procesa y guarda los vehículos asociados en la base de datos de forma transaccional, limpia la WhatsAppSession, envía un mensaje de bienvenida formal con el menú principal de cliente autenticado y registra la acción en auditoría (`AuditLog`). |
| **Postcondición** | El cliente queda registrado de forma definitiva en el sistema con su número de teléfono y su vehículo o flota inicial asociada. | |
| **Excepciones** | **Paso** | **Acción** |
| | 7a | Si el número de documento ya está registrado en el lavadero, el sistema envía una alerta indicando el error y cancela el flujo de registro. |
| | 14a | Si el cliente intenta forzar la finalización o escribe comandos sin completar exitosamente el registro de al menos un vehículo en CU-027, el bot reitera la obligatoriedad de la carga del vehículo. |
| | * | En cualquier momento del flujo, si el cliente escribe "cancelar", el sistema elimina los datos temporales de la sesión y aborta el registro. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 17 | 2 segundos |
| **Frecuencia** | Frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Utiliza la infraestructura de WhatsApp Cloud API y persistencia intermedia para resguardar el estado de la conversación. | |

---

### CU-027 - Registrar vehículo por WhatsApp

| UC–027 | Registrar vehículo por WhatsApp | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–10 Integración con WhatsApp, OBJ–02 Gestión de Clientes y Vehículos | |
| **Requisitos asociados** | IRQ–03 Información sobre Vehículos, IRQ–12 Información de Sesiones WhatsApp | |
| **Descripción** | Permite al cliente gestionar sus vehículos de forma guiada por chat. Puede invocarse de manera dependiente (bucle de alta en CU-026) o de forma autónoma desde el menú "Mis Vehículos" para clientes ya existentes, soportando alta de nuevos vehículos, vinculación mediante clave y desasociación (baja lógica condicional). | |
| **Precondición** | El sistema se encuentra procesando una sesión válida de WhatsApp (`WhatsAppSession`). | |
| **Secuencia normal** | **Paso** | **Acción** |
| | **Sección:** | **Principal (Alta / Vinculación)** |
| | 1 | El caso de uso inicia cuando el bot solicita los datos de un vehículo (por invocación de CU-026 o al seleccionar "Agregar Vehículo" en el menú de clientes registrados). |
| | 2 | El sistema solicita la Patente del vehículo. |
| | 3 | El cliente ingresa la Patente. |
| | 4 | El sistema valida el formato de la patente y busca si ya existe en la base de datos:<br>a) Si la patente NO existe: continúa en el paso 5 (Vehículo Nuevo).<br>b) Si la patente YA existe y está activa: consúltese la sección *Opción: Vincular Vehículo Existente*. |
| | 5 | El sistema solicita el Tipo de Vehículo enviando una lista numerada de los tipos activos (1: Auto, 2: Camioneta, 3: Moto). |
| | 6 | El cliente selecciona el Tipo de Vehículo. |
| | 7 | El sistema solicita la Marca del vehículo. |
| | 8 | El cliente ingresa la Marca. |
| | 9 | El sistema solicita el Modelo. |
| | 10 | El cliente ingresa el Modelo. |
| | 11 | El sistema solicita el Color. |
| | 12 | El cliente ingresa el Color. |
| | 13 | El sistema muestra un resumen del vehículo y solicita confirmación (1: Sí, 2: No). |
| | 14 | El cliente confirma el registro. |
| | 15 | El sistema genera internamente la clave de asociación (hash SHA256). Si el caso de uso fue llamado desde CU-026, guarda el vehículo temporalmente en la sesión; si fue llamado de forma autónoma, lo persiste directo en la base de datos, envía al cliente la clave de seguridad generada para co-propiedad, finaliza y retorna al menú. |
| | **Sección:** | **Opción: Vincular Vehículo Existente** |
| | 1 | El sistema detecta que la patente ingresada ya pertenece a un vehículo activo y le informa al cliente por mensaje que el vehículo está registrado, solicitando la Clave de Asociación provista por el dueño original para proceder. |
| | 2 | El cliente ingresa la Clave de Asociación por mensaje de texto. |
| | 3 | El sistema valida el hash SHA256 de la clave ingresada:<br>a) Si es correcto: asocia temporalmente o persiste la relación bidireccional cliente-vehículo y notifica el éxito de la vinculación, retornando al flujo correspondiente.<br>b) Si es incorrecto: informa el error y permite reingresar la clave o cancelar. |
| | **Sección:** | **Opción: Quitar Vehículo de la Flota** |
| | 1 | Un cliente autenticado accede al menú "Mis Vehículos", selecciona un vehículo de su lista y elige la opción "Eliminar/Quitar". |
| | 2 | El sistema evalúa la co-propiedad de la unidad analizando la lista de ClientesIds registrados en el vehículo:<br>a) Si es unipersonal (solo pertenece a este cliente): el sistema cambia lógicamente el estado del vehículo a "Inactivo" mediante el **CU-020 - Desactivar vehículo**.<br>b) Si es multipersonal (co-propiedad con otros dueños activos): el sistema remueve únicamente al cliente actual de las listas de asociación, manteniendo el vehículo "Activo" en el sistema. |
| | 3 | El sistema envía un mensaje confirmando la remoción exitosa del vehículo de su perfil y registra el cambio en auditoría. |
| **Postcondición** | El vehículo queda registrado, vinculado o removido del perfil del cliente según la sección ejecutada, respetando las reglas de co-propiedad y estados lógicos del dominio. | |
| **Excepciones** | **Paso** | **Acción** |
| | 13a | Si el cliente no confirma los datos en el paso 13, el sistema le permite rellenar el formulario de vehículo desde el paso 2 o cancelar la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 15 | 1 segundo |
| **Frecuencia** | Frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | La opción de desvinculación o eliminación lógica desde WhatsApp garantiza que los procesos asincrónicos mantengan las mismas restricciones de base de datos que el panel administrativo principal. | |

---

### CU-028 - Identificar si el número de teléfono está registrado

| UC–028 | Identificar si el número de teléfono está registrado | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–10 Integración con WhatsApp | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–12 Información de Sesiones WhatsApp | |
| **Descripción** | El sistema valida si el número de WhatsApp que envía un mensaje pertenece a un cliente existente, determinando el flujo de conversación apropiado. | |
| **Precondición** | Se ha recibido un mensaje de WhatsApp. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El sistema recibe un mensaje de WhatsApp con el número del remitente. |
| | 2 | El sistema busca en la base de datos un cliente con ese número de teléfono. |
| | 3a | Si existe y está activo, el sistema carga/actualiza la sesión con el ClienteId. |
| | 3b | Si no existe o está inactivo, el sistema crea una sesión sin ClienteId. |
| | 4 | El sistema retorna el estado de autenticación al flujo de procesamiento de mensajes. |
| **Postcondición** | El sistema conoce si el número corresponde a un cliente registrado. | |
| **Excepciones** | **Paso** | **Acción** |
| | - | - |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-3 | 500ms |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Este caso de uso es ejecutado automáticamente por el sistema en cada mensaje recibido. | |

---

### CU-029 - Editar datos personales por WhatsApp

| UC–029 | Editar datos personales por WhatsApp | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–10 Integración con WhatsApp | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–12 Información de Sesiones WhatsApp | |
| **Descripción** | El cliente modifica sus datos personales (nombre, apellido, email) a través del flujo conversacional de WhatsApp. | |
| **Precondición** | El cliente debe estar registrado en el sistema. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El cliente accede al menú principal de WhatsApp. |
| | 2 | El cliente selecciona "Mis Datos". |
| | 3 | El sistema muestra los datos actuales del cliente. |
| | 4 | El sistema muestra opciones de modificación numeradas. |
| | 5 | El cliente selecciona el dato a modificar (nombre, apellido, email). |
| | 6 | El sistema solicita el nuevo valor. |
| | 7 | El cliente ingresa el nuevo valor. |
| | 8 | El sistema valida el dato ingresado. |
| | 9 | El sistema actualiza el registro del cliente. |
| | 10 | El sistema confirma la modificación exitosa. |
| | 11 | El sistema registra la acción en auditoría. |
| **Postcondición** | Los datos del cliente han sido actualizados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 8a | Si el dato es inválido, el sistema solicita ingresarlo nuevamente. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 9 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | El número de teléfono, tipo y número de documento no se pueden modificar por seguridad. | |
### Módulo: Gestión de Servicios

### CU-030 - Crear servicio

| UC–030 | Crear servicio | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios, IRQ–05 Información sobre Tipos de Servicio, IRQ–06 Información sobre Tipos de Vehículo | |
| **Descripción** | El administrador crea un nuevo servicio para el lavadero con nombre, descripción, precio, tiempo estimado, tipo de servicio, tipo de vehículo y opcionalmente etapas de ejecución. | |
| **Precondición** | El usuario debe tener rol de Administrador. Deben existir tipos de servicio y tipos de vehículo activos. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de gestión de servicios desde el menú. |
| | 2 | El administrador hace clic en "Nuevo Servicio". |
| | 3 | El sistema muestra un formulario con campos: Nombre, Descripción, Tipo de Servicio, Tipo de Vehículo, Precio, Tiempo Estimado. |
| | 4 | El sistema carga los tipos de servicio y tipos de vehículo activos. |
| | 5 | El administrador completa todos los campos obligatorios. |
| | 6 | Opcionalmente, el administrador agrega etapas al servicio haciendo clic en "Agregar Etapa". |
| | 7 | Para cada etapa, el administrador ingresa: Nombre, Descripción, Orden. |
| | 8 | El administrador hace clic en "Guardar". |
| | 9 | El sistema valida los datos (nombre único por tipo de vehículo, precio >= 0, tiempo > 0). |
| | 10 | El sistema crea el servicio con estado "Activo". |
| | 11 | El sistema muestra un mensaje de éxito. |
| | 12 | El sistema registra la acción en auditoría. |
| **Postcondición** | El servicio está registrado y disponible para ser utilizado en lavados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 9a | Si ya existe un servicio con el mismo nombre para ese tipo de vehículo, el sistema informa el error. |
| | 9b | Si hay errores de validación, el sistema muestra los errores específicos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 10 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | Las etapas permiten dividir el servicio en pasos que se pueden marcar como completados durante el lavado. | |

---

### CU-031 - Modificar servicio

| UC–031 | Modificar servicio | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios | |
| **Descripción** | El administrador actualiza los detalles de un servicio existente, incluyendo nombre, descripción, precio, tiempo estimado y etapas. | |
| **Precondición** | El usuario debe tener rol de Administrador. El servicio debe existir en el sistema. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-034 Consultar servicios. |
| | 2 | El administrador selecciona el servicio a modificar. |
| | 3 | El sistema muestra el formulario de edición con los datos actuales. |
| | 4 | El administrador modifica los campos deseados. |
| | 5 | El administrador puede agregar, modificar o eliminar etapas. |
| | 6 | El administrador hace clic en "Guardar". |
| | 7 | El sistema valida los datos. |
| | 8 | El sistema actualiza el registro del servicio. |
| | 9 | El sistema actualiza los paquetes que contienen este servicio (recalcula precios y tiempos). |
| | 10 | El sistema muestra un mensaje de éxito. |
| | 11 | El sistema registra la acción en auditoría. |
| **Postcondición** | Los datos del servicio han sido actualizados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 7a | Si hay errores de validación, el sistema muestra los errores específicos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 8-9 | 2 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-034 Consultar servicios. La modificación de un servicio afecta a los paquetes que lo contienen. | |

---

### CU-032 - Desactivar servicio

| UC–032 | Desactivar servicio | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios | |
| **Descripción** | El administrador desactiva un servicio del sistema. Los servicios desactivados no aparecen disponibles para seleccionar en nuevos lavados. | |
| **Precondición** | El usuario debe tener rol de Administrador. El servicio debe existir y estar activo. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-034 Consultar servicios. |
| | 2 | El administrador selecciona el servicio a desactivar. |
| | 3 | El sistema verifica si el servicio está incluido en algún paquete activo. |
| | 4 | El sistema muestra un diálogo de confirmación (con advertencia si afecta paquetes). |
| | 5 | El administrador confirma la desactivación. |
| | 6 | El sistema cambia el estado del servicio a "Inactivo". |
| | 7 | El sistema muestra un mensaje de éxito. |
| | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | El servicio ha sido desactivado. | |
| **Excepciones** | **Paso** | **Acción** |
| | 3a | Si el servicio está en un paquete activo, el sistema advierte que afectará al paquete. |
| | 5a | Si el administrador cancela, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-034 Consultar servicios. | |

---

### CU-033 - Reactivar servicio

| UC–033 | Reactivar servicio | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios | |
| **Descripción** | El administrador reactiva un servicio previamente desactivado, volviéndolo disponible para selección. | |
| **Precondición** | El usuario debe tener rol de Administrador. El servicio debe existir y estar inactivo. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-034 Consultar servicios (filtrando por inactivos). |
| | 2 | El administrador selecciona el servicio a reactivar. |
| | 3 | El sistema muestra un diálogo de confirmación. |
| | 4 | El administrador confirma la reactivación. |
| | 5 | El sistema cambia el estado del servicio a "Activo". |
| | 6 | El sistema muestra un mensaje de éxito. |
| | 7 | El sistema registra la acción en auditoría. |
| **Postcondición** | El servicio ha sido reactivado y está disponible para su uso. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si el administrador cancela, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 5 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-034 Consultar servicios. | |

---

### CU-034 - Consultar servicios

| UC–034 | Consultar servicios | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios | |
| **Descripción** | El personal consulta la lista de servicios disponibles con opciones de filtrado por estado, tipo de servicio, tipo de vehículo y rango de precios. | |
| **Precondición** | El usuario debe estar autenticado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la sección de gestión de servicios desde el menú. |
| | 2 | El sistema obtiene la lista de servicios aplicando filtros por defecto (Activos). |
| | 3 | El sistema muestra la tabla de servicios con columnas: Nombre, Tipo Servicio, Tipo Vehículo, Precio, Tiempo Estimado, Estado. |
| | 4 | El usuario puede aplicar filtros por: estado, tipo de servicio, tipo de vehículo, rango de precios. |
| | 5 | El usuario puede ordenar por cualquier columna. |
| | 6 | El usuario puede navegar entre páginas usando los controles de paginación. |
| | 7 | El sistema actualiza la vista según los criterios seleccionados. |
| **Postcondición** | El usuario visualiza la lista de servicios según los filtros aplicados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 2a | Si no hay servicios, el sistema muestra un mensaje indicándolo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Por defecto muestra solo servicios activos. | |

---

### CU-035 - Buscar servicios

| UC–035 | Buscar servicios | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios | |
| **Descripción** | El personal busca servicios por nombre. La búsqueda se realiza en tiempo real con resultados paginados. | |
| **Precondición** | El usuario debe estar autenticado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la sección de gestión de servicios. |
| | 2 | El usuario ingresa texto en el campo de búsqueda. |
| | 3 | El sistema busca coincidencias en el nombre del servicio. |
| | 4 | El sistema muestra los resultados filtrados en tiempo real. |
| | 5 | El usuario puede seleccionar un servicio de los resultados. |
| **Postcondición** | El usuario visualiza los servicios que coinciden con el criterio de búsqueda. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si no hay coincidencias, el sistema muestra mensaje "Sin resultados". |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 3-4 | 500ms |
| **Frecuencia** | Frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | La búsqueda es insensible a mayúsculas/minúsculas. | |

---

### CU-036 - Crear tipo de servicio

| UC–036 | Crear tipo de servicio | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–05 Información sobre Tipos de Servicio | |
| **Descripción** | El administrador crea un nuevo tipo de servicio (categoría) para clasificar los servicios del lavadero. | |
| **Precondición** | El usuario debe tener rol de Administrador. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de configuración de tipos de servicio. |
| | 2 | El administrador hace clic en "Nuevo Tipo de Servicio". |
| | 3 | El sistema muestra un formulario con campo: Nombre. |
| | 4 | El administrador ingresa el nombre del tipo de servicio. |
| | 5 | El administrador hace clic en "Guardar". |
| | 6 | El sistema valida que el nombre no exista previamente. |
| | 7 | El sistema crea el tipo de servicio con estado "Activo". |
| | 8 | El sistema muestra un mensaje de éxito. |
| | 9 | El sistema registra la acción en auditoría. |
| **Postcondición** | El tipo de servicio está disponible para asignar a servicios. | |
| **Excepciones** | **Paso** | **Acción** |
| | 6a | Si el nombre ya existe, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 7 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Los tipos de servicio permiten limitar a un servicio por tipo en los paquetes. | |

---

### CU-037 - Eliminar tipo de servicio

| UC–037 | Eliminar tipo de servicio | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–05 Información sobre Tipos de Servicio | |
| **Descripción** | El administrador elimina un tipo de servicio. Solo se permite si no hay servicios activos utilizando ese tipo. | |
| **Precondición** | El usuario debe tener rol de Administrador. El tipo de servicio debe existir. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de configuración de tipos de servicio. |
| | 2 | El administrador selecciona el tipo de servicio a eliminar. |
| | 3 | El sistema verifica que no haya servicios activos utilizando ese tipo. |
| | 4 | El sistema muestra un diálogo de confirmación. |
| | 5 | El administrador confirma la eliminación. |
| | 6 | El sistema elimina el tipo de servicio (eliminación física). |
| | 7 | El sistema muestra un mensaje de éxito. |
| | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | El tipo de servicio ha sido eliminado del sistema. | |
| **Excepciones** | **Paso** | **Acción** |
| | 3a | Si hay servicios utilizando ese tipo, el sistema impide la eliminación y muestra error. |
| | 5a | Si el administrador cancela, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6 | 1 segundo |
| **Frecuencia** | Rara | |
| **Estabilidad** | Baja | |
| **Comentarios** | La eliminación es física, no lógica. | |

---

### CU-038 - Crear tipo de vehículo

| UC–038 | Crear tipo de vehículo | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–06 Información sobre Tipos de Vehículo | |
| **Descripción** | El administrador crea un nuevo tipo de vehículo con nombre, formato de patente y cantidad de empleados requeridos por defecto. | |
| **Precondición** | El usuario debe tener rol de Administrador. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de configuración de tipos de vehículo. |
| | 2 | El administrador hace clic en "Nuevo Tipo de Vehículo". |
| | 3 | El sistema muestra un formulario con campos: Nombre, Formato Patente, Empleados Requeridos. |
| | 4 | El administrador completa los campos. |
| | 5 | El administrador hace clic en "Guardar". |
| | 6 | El sistema valida que el nombre no exista previamente y empleados >= 1. |
| | 7 | El sistema crea el tipo de vehículo con estado "Activo". |
| | 8 | El sistema muestra un mensaje de éxito. |
| | 9 | El sistema registra la acción en auditoría. |
| **Postcondición** | El tipo de vehículo está disponible para asignar a vehículos y servicios. | |
| **Excepciones** | **Paso** | **Acción** |
| | 6a | Si el nombre ya existe, el sistema informa el error. |
| | 6b | Si hay errores de validación, el sistema muestra los errores específicos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 7 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Los tipos de vehículo determinan qué servicios están disponibles. | |

---

### CU-039 - Eliminar tipo de vehículo

| UC–039 | Eliminar tipo de vehículo | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–06 Información sobre Tipos de Vehículo | |
| **Descripción** | El administrador elimina un tipo de vehículo. Solo se permite si no hay servicios ni vehículos activos utilizando ese tipo. | |
| **Precondición** | El usuario debe tener rol de Administrador. El tipo de vehículo debe existir. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de configuración de tipos de vehículo. |
| | 2 | El administrador selecciona el tipo de vehículo a eliminar. |
| | 3 | El sistema verifica que no haya servicios ni vehículos activos utilizando ese tipo. |
| | 4 | El sistema muestra un diálogo de confirmación. |
| | 5 | El administrador confirma la eliminación. |
| | 6 | El sistema elimina el tipo de vehículo (eliminación física). |
| | 7 | El sistema muestra un mensaje de éxito. |
| | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | El tipo de vehículo ha sido eliminado del sistema. | |
| **Excepciones** | **Paso** | **Acción** |
| | 3a | Si hay servicios o vehículos utilizando ese tipo, el sistema impide la eliminación. |
| | 5a | Si el administrador cancela, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6 | 1 segundo |
| **Frecuencia** | Rara | |
| **Estabilidad** | Baja | |
| **Comentarios** | La eliminación es física, no lógica. | |

---

### CU-040 - Gestionar etapas del servicio

| UC–040 | Gestionar etapas del servicio | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–04 Información sobre Servicios | |
| **Descripción** | El administrador define las etapas o fases en las que se divide un servicio para su ejecución, permitiendo un seguimiento granular del progreso. | |
| **Precondición** | El usuario debe tener rol de Administrador. El servicio debe existir. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la edición del servicio (dentro de CU-030 o CU-031). |
| | 2 | El sistema muestra la sección de etapas con las etapas actuales (si existen). |
| | 3 | El administrador puede: |
| | 3a | Agregar nueva etapa: clic en "Agregar Etapa", ingresa Nombre, Descripción, Orden. |
| | 3b | Modificar etapa existente: edita los campos de la etapa. |
| | 3c | Eliminar etapa: clic en botón eliminar de la etapa. |
| | 3d | Reordenar etapas: arrastra y suelta para cambiar el orden. |
| | 4 | El administrador guarda los cambios del servicio. |
| | 5 | El sistema valida que los nombres de etapa sean únicos dentro del servicio. |
| | 6 | El sistema actualiza las etapas del servicio. |
| | 7 | El sistema registra la acción en auditoría. |
| **Postcondición** | Las etapas del servicio han sido actualizadas. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si hay nombres duplicados, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Las etapas se ejecutan secuencialmente durante el lavado. Este caso de uso se realiza dentro de CU-030 o CU-031. | |

---

### CU-041 - Crear paquete de servicios

| UC–041 | Crear paquete de servicios | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–07 Información sobre Paquetes de Servicios | |
| **Descripción** | El administrador crea un paquete que agrupa 2 o más servicios del mismo tipo de vehículo con un porcentaje de descuento aplicable. | |
| **Precondición** | El usuario debe tener rol de Administrador. Deben existir al menos 2 servicios activos del mismo tipo de vehículo. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de gestión de paquetes de servicios. |
| | 2 | El administrador hace clic en "Nuevo Paquete". |
| | 3 | El sistema muestra un formulario con campos: Nombre, Tipo de Vehículo, Porcentaje de Descuento. |
| | 4 | El administrador selecciona el tipo de vehículo. |
| | 5 | El sistema carga los servicios activos disponibles para ese tipo de vehículo. |
| | 6 | El administrador selecciona al menos 2 servicios (máximo uno por tipo de servicio). |
| | 7 | El administrador define el porcentaje de descuento (según configuración del sistema). |
| | 8 | El sistema calcula automáticamente el precio final y tiempo estimado. |
| | 9 | El administrador hace clic en "Guardar". |
| | 10 | El sistema valida que no haya más de un servicio del mismo tipo de servicio. |
| | 11 | El sistema crea el paquete con estado "Activo". |
| | 12 | El sistema muestra un mensaje de éxito. |
| | 13 | El sistema registra la acción en auditoría. |
| **Postcondición** | El paquete está registrado y disponible para ser utilizado en lavados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 6a | Si se seleccionan menos de 2 servicios, el sistema informa el error. |
| | 10a | Si hay más de un servicio del mismo tipo de servicio, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 11 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | El porcentaje de descuento debe estar dentro del rango configurado en CU-066. El precio se calcula como: suma de precios de servicios × (1 - descuento/100). | |

---

### CU-042 - Modificar paquete de servicios

| UC–042 | Modificar paquete de servicios | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–07 Información sobre Paquetes de Servicios | |
| **Descripción** | El administrador actualiza la configuración de un paquete de servicios, incluyendo servicios incluidos y porcentaje de descuento. | |
| **Precondición** | El usuario debe tener rol de Administrador. El paquete debe existir en el sistema. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-045 Consultar paquetes de servicios. |
| | 2 | El administrador selecciona el paquete a modificar. |
| | 3 | El sistema muestra el formulario de edición con los datos actuales. |
| | 4 | El administrador modifica los campos deseados (Nombre, Servicios, Descuento). |
| | 5 | El sistema recalcula el precio y tiempo estimado en tiempo real. |
| | 6 | El administrador hace clic en "Guardar". |
| | 7 | El sistema valida los datos (mínimo 2 servicios, un servicio por tipo). |
| | 8 | El sistema actualiza el registro del paquete. |
| | 9 | El sistema muestra un mensaje de éxito. |
| | 10 | El sistema registra la acción en auditoría. |
| **Postcondición** | Los datos del paquete han sido actualizados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 7a | Si hay errores de validación, el sistema muestra los errores específicos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 8 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Baja | |
| **Comentarios** | Incluye el caso de uso CU-045 Consultar paquetes de servicios. | |

---

### CU-043 - Desactivar paquete de servicios

| UC–043 | Desactivar paquete de servicios | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–07 Información sobre Paquetes de Servicios | |
| **Descripción** | El administrador desactiva un paquete de servicios, dejándolo no disponible para selección. | |
| **Precondición** | El usuario debe tener rol de Administrador. El paquete debe existir y estar activo. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-045 Consultar paquetes de servicios. |
| | 2 | El administrador selecciona el paquete a desactivar. |
| | 3 | El sistema muestra un diálogo de confirmación. |
| | 4 | El administrador confirma la desactivación. |
| | 5 | El sistema cambia el estado del paquete a "Inactivo". |
| | 6 | El sistema muestra un mensaje de éxito. |
| | 7 | El sistema registra la acción en auditoría. |
| **Postcondición** | El paquete ha sido desactivado. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si el administrador cancela, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 5 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Baja | |
| **Comentarios** | Incluye el caso de uso CU-045 Consultar paquetes de servicios. | |

---

### CU-044 - Reactivar paquete de servicios

| UC–044 | Reactivar paquete de servicios | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–07 Información sobre Paquetes de Servicios | |
| **Descripción** | El administrador reactiva un paquete previamente desactivado. | |
| **Precondición** | El usuario debe tener rol de Administrador. El paquete debe existir y estar inactivo. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-045 Consultar paquetes de servicios (filtrando por inactivos). |
| | 2 | El administrador selecciona el paquete a reactivar. |
| | 3 | El sistema verifica que todos los servicios del paquete estén activos. |
| | 4 | El sistema muestra un diálogo de confirmación. |
| | 5 | El administrador confirma la reactivación. |
| | 6 | El sistema cambia el estado del paquete a "Activo". |
| | 7 | El sistema muestra un mensaje de éxito. |
| | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | El paquete ha sido reactivado y está disponible para su uso. | |
| **Excepciones** | **Paso** | **Acción** |
| | 3a | Si algún servicio del paquete está inactivo, el sistema advierte y sugiere editar el paquete. |
| | 5a | Si el administrador cancela, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Baja | |
| **Comentarios** | Incluye el caso de uso CU-045 Consultar paquetes de servicios. | |

---

### CU-045 - Consultar paquetes de servicios

| UC–045 | Consultar paquetes de servicios | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–03 Gestión de Servicios y Paquetes | |
| **Requisitos asociados** | IRQ–07 Información sobre Paquetes de Servicios | |
| **Descripción** | El personal visualiza los paquetes de servicios disponibles con información de precio final, descuento y tiempo estimado total. | |
| **Precondición** | El usuario debe estar autenticado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la sección de gestión de paquetes de servicios. |
| | 2 | El sistema obtiene la lista de paquetes aplicando filtros por defecto (Activos). |
| | 3 | El sistema calcula el precio final y tiempo estimado de cada paquete dinámicamente. |
| | 4 | El sistema muestra la tabla de paquetes con columnas: Nombre, Tipo Vehículo, Servicios, Precio Original, Descuento, Precio Final, Tiempo Estimado, Estado. |
| | 5 | El usuario puede aplicar filtros por: estado, tipo de vehículo, rango de precios, rango de descuento. |
| | 6 | El usuario puede ordenar por cualquier columna. |
| | 7 | El usuario puede navegar entre páginas usando los controles de paginación. |
| | 8 | El sistema actualiza la vista según los criterios seleccionados. |
| **Postcondición** | El usuario visualiza la lista de paquetes según los filtros aplicados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 2a | Si no hay paquetes, el sistema muestra un mensaje indicándolo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-4 | 2 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Por defecto muestra solo paquetes activos. El precio y tiempo se calculan dinámicamente basándose en los servicios incluidos. | |
### Módulo: Registro de Lavados

### CU-046 - Registrar realización de un servicio (lavado)

| UC–046 | Registrar realización de un servicio (lavado) | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal registra un lavado seleccionando un vehículo (por búsqueda de patente), el cliente asociado, los servicios o paquetes a realizar, el descuento aplicable y notas adicionales. El lavado inicia inmediatamente tras su creación. | |
| **Precondición** | El usuario debe estar autenticado. Deben existir clientes, vehículos, servicios y empleados activos. El lavadero debe estar en horario de operación. No debe excederse la capacidad máxima de lavados simultáneos. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la sección de gestión de lavados. |
| | 2 | El usuario hace clic en "Nuevo Lavado". |
| | 3 | El sistema valida que esté en horario de operación. |
| | 4 | El sistema valida que no se exceda la capacidad máxima de lavados simultáneos. |
| | 5 | El sistema muestra un formulario de creación. |
| | 6 | El usuario busca y selecciona el vehículo por patente. |
| | 7 | El sistema muestra los clientes asociados al vehículo. |
| | 8 | El usuario selecciona el cliente que trae el vehículo. |
| | 9 | El sistema carga los servicios y paquetes disponibles para el tipo de vehículo. |
| | 10 | El usuario selecciona servicios individuales y/o un paquete. |
| | 11 | El sistema calcula el tiempo estimado total y precio. |
| | 12 | El sistema sugiere la cantidad de empleados según el tipo de vehículo. |
| | 13 | El usuario asigna los empleados al lavado. |
| | 14 | El usuario puede agregar un descuento adicional (opcional). |
| | 15 | El usuario puede agregar notas adicionales (opcional). |
| | 16 | El usuario hace clic en "Iniciar Lavado". |
| | 17 | El sistema valida todos los datos. |
| | 18 | El sistema crea el lavado con estado "EnProceso" y registra el tiempo de inicio. |
| | 19 | El sistema muestra un mensaje de éxito y redirige al detalle del lavado. |
| | 20 | El sistema registra la acción en auditoría. |
| **Postcondición** | El lavado está registrado y en proceso. | |
| **Excepciones** | **Paso** | **Acción** |
| | 3a | Si está fuera del horario de operación, el sistema informa el error. |
| | 4a | Si se excede la capacidad máxima, el sistema informa el error. |
| | 6a | Si no se encuentra el vehículo, el sistema ofrece crearlo (CU-018). |
| | 10a | Si se intenta agregar más de un paquete, el sistema informa el error. |
| | 13a | Si no hay empleados disponibles, el sistema advierte pero permite continuar. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 18 | 2 segundos |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | El lavado inicia inmediatamente al crearse (no hay estado "Pendiente"). | |

---

### CU-047 - Consultar lavados

| UC–047 | Consultar lavados | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal visualiza el historial de lavados con opciones de filtrado por estado, cliente, vehículo, rango de fechas, rango de precios, estado de pago y estado de retiro. | |
| **Precondición** | El usuario debe estar autenticado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la sección de gestión de lavados. |
| | 2 | El sistema obtiene la lista de lavados aplicando filtros por defecto (EnProceso). |
| | 3 | El sistema muestra la tabla de lavados con columnas: ID, Vehículo, Cliente, Servicios, Estado, Pago, Retiro, Fecha. |
| | 4 | El usuario puede aplicar filtros por: estado del lavado, estado de pago, estado de retiro, cliente, vehículo, rango de fechas, rango de precios. |
| | 5 | El usuario puede ordenar por cualquier columna. |
| | 6 | El usuario puede navegar entre páginas usando los controles de paginación. |
| | 7 | El sistema actualiza la vista según los criterios seleccionados. |
| **Postcondición** | El usuario visualiza la lista de lavados según los filtros aplicados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 2a | Si no hay lavados, el sistema muestra un mensaje indicándolo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-3 | 2 segundos |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Por defecto muestra lavados en proceso. | |

---

### CU-048 - Buscar lavados

| UC–048 | Buscar lavados | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal busca lavados por patente, nombre de cliente o ID. La búsqueda se realiza en tiempo real. | |
| **Precondición** | El usuario debe estar autenticado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la sección de gestión de lavados. |
| | 2 | El usuario ingresa texto en el campo de búsqueda. |
| | 3 | El sistema busca coincidencias en: ID del lavado, patente del vehículo, nombre del cliente. |
| | 4 | El sistema muestra los resultados filtrados en tiempo real. |
| | 5 | El usuario puede seleccionar un lavado de los resultados. |
| **Postcondición** | El usuario visualiza los lavados que coinciden con el criterio de búsqueda. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si no hay coincidencias, el sistema muestra mensaje "Sin resultados". |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 3-4 | 500ms |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | La búsqueda es insensible a mayúsculas/minúsculas. | |

---

### CU-049 - Ver detalle de lavado

| UC–049 | Ver detalle de lavado | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal visualiza el detalle completo de un lavado, incluyendo servicios con su estado y progreso de etapas, información del cliente, vehículo, pago y tiempos. | |
| **Precondición** | El usuario debe estar autenticado. El lavado debe existir. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-047 Consultar lavados. |
| | 2 | El usuario hace clic en un lavado para ver su detalle. |
| | 3 | El sistema obtiene toda la información del lavado. |
| | 4 | El sistema muestra: |
| | 4a | Información del vehículo: patente, tipo, marca, modelo, color. |
| | 4b | Información del cliente: nombre, teléfono, email. |
| | 4c | Lista de servicios con estado de cada uno y progreso de etapas. |
| | 4d | Empleados asignados. |
| | 4e | Tiempos: inicio, tiempo estimado, tiempo transcurrido. |
| | 4f | Información de pago: monto total, monto pagado, estado de pago. |
| | 4g | Estado de retiro y cliente que retira (si aplica). |
| | 4h | Notas adicionales. |
| | 5 | El sistema muestra las acciones disponibles según el estado del lavado. |
| **Postcondición** | El usuario visualiza el detalle completo del lavado. | |
| **Excepciones** | **Paso** | **Acción** |
| | 3a | Si el lavado no existe, el sistema muestra error y redirige a la lista. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 3-4 | 1 segundo |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Incluye el caso de uso CU-047 Consultar lavados. | |

---

### CU-050 - Iniciar servicio en lavado

| UC–050 | Iniciar servicio en lavado | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal inicia la ejecución de un servicio específico dentro de un lavado, registrando el tiempo de inicio. | |
| **Precondición** | El usuario debe estar autenticado. El lavado debe estar en estado "EnProceso". El servicio debe estar pendiente de inicio. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
| | 2 | El usuario hace clic en "Iniciar" en el servicio correspondiente. |
| | 3 | El sistema registra la fecha y hora de inicio del servicio. |
| | 4 | El sistema actualiza el estado del servicio a "EnProceso". |
| | 5 | Si el servicio tiene etapas, la primera etapa queda disponible para iniciar. |
| | 6 | El sistema actualiza la vista del detalle del lavado. |
| | 7 | El sistema registra la acción en auditoría. |
| **Postcondición** | El servicio está en proceso y registra su tiempo de inicio. | |
| **Excepciones** | **Paso** | **Acción** |
| | - | - |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 3-5 | 1 segundo |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. | |

---

### CU-051 - Iniciar etapa de servicio

| UC–051 | Iniciar etapa de servicio | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal inicia una etapa específica dentro de un servicio que tiene múltiples etapas definidas. | |
| **Precondición** | El usuario debe estar autenticado. El servicio debe estar en estado "EnProceso". La etapa anterior debe estar completada (si existe). | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
| | 2 | El sistema muestra las etapas del servicio con sus estados. |
| | 3 | El usuario hace clic en "Iniciar" en la etapa correspondiente. |
| | 4 | El sistema valida que las etapas anteriores estén completadas. |
| | 5 | El sistema registra la fecha y hora de inicio de la etapa. |
| | 6 | El sistema actualiza el estado de la etapa a "EnProceso". |
| | 7 | El sistema actualiza la vista del detalle del lavado. |
| | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | La etapa está en proceso y registra su tiempo de inicio. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si hay etapas anteriores pendientes, el sistema informa que deben completarse primero. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 5-6 | 1 segundo |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. Las etapas se deben completar en orden secuencial. | |

---

### CU-052 - Finalizar etapa de servicio

| UC–052 | Finalizar etapa de servicio | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal marca como finalizada una etapa de un servicio en ejecución. | |
| **Precondición** | El usuario debe estar autenticado. La etapa debe estar en estado "EnProceso". | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
| | 2 | El usuario hace clic en "Finalizar" en la etapa en proceso. |
| | 3 | El sistema registra la fecha y hora de finalización de la etapa. |
| | 4 | El sistema actualiza el estado de la etapa a "Completada". |
| | 5 | Si es la última etapa del servicio, el servicio se marca como completado automáticamente. |
| | 6 | El sistema puede ejecutar CU-079 Notificar etapa finalizada (si está configurado). |
| | 7 | El sistema actualiza la vista del detalle del lavado. |
| | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | La etapa está completada. | |
| **Excepciones** | **Paso** | **Acción** |
| | - | - |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 3-5 | 1 segundo |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. | |

---

### CU-053 - Finalizar servicio en lavado

| UC–053 | Finalizar servicio en lavado | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal marca como completado un servicio específico dentro del lavado, registrando el tiempo de finalización. | |
| **Precondición** | El usuario debe estar autenticado. El servicio debe estar en estado "EnProceso". | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
| | 2 | El usuario hace clic en "Finalizar" en el servicio en proceso. |
| | 3 | El sistema verifica el estado de las etapas del servicio (si tiene). |
| | 4a | Si todas las etapas están completadas, el sistema marca el servicio como "Completado". |
| | 4b | Si hay etapas pendientes, el sistema solicita confirmación para marcar como "CompletadoParcialmente". |
| | 5 | El sistema registra la fecha y hora de finalización. |
| | 6 | El sistema actualiza la vista del detalle del lavado. |
| | 7 | El sistema registra la acción en auditoría. |
| **Postcondición** | El servicio está completado o completado parcialmente. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4b.1 | Si el usuario no confirma, se cancela la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 4-5 | 1 segundo |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. | |

---

### CU-054 - Finalizar lavado completo

| UC–054 | Finalizar lavado completo | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal marca como completado un lavado cuando todos los servicios han sido finalizados. Se registra el tiempo de finalización total. | |
| **Precondición** | El usuario debe estar autenticado. El lavado debe estar en estado "EnProceso". | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
| | 2 | El usuario hace clic en "Finalizar Lavado". |
| | 3 | El sistema verifica el estado de todos los servicios. |
| | 4a | Si todos los servicios están completados, el sistema marca el lavado como "Realizado". |
| | 4b | Si hay servicios pendientes/parciales, el sistema marca como "RealizadoParcialmente" y solicita motivo. |
| | 5 | El sistema registra la fecha y hora de finalización. |
| | 6 | El sistema ejecuta CU-080 Notificar lavado finalizado (si está configurado). |
| | 7 | El sistema actualiza la vista y muestra mensaje de éxito. |
| | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | El lavado está finalizado (Realizado o RealizadoParcialmente). | |
| **Excepciones** | **Paso** | **Acción** |
| | 4b.1 | Si el usuario no proporciona motivo, el sistema lo solicita. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 4-6 | 2 segundos |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. | |

---

### CU-055 - Cancelar lavado

| UC–055 | Cancelar lavado | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal cancela un lavado completo indicando el motivo de cancelación. | |
| **Precondición** | El usuario debe estar autenticado. El lavado debe estar en estado "EnProceso". | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
| | 2 | El usuario hace clic en "Cancelar Lavado". |
| | 3 | El sistema muestra un formulario solicitando el motivo de cancelación (obligatorio). |
| | 4 | El usuario ingresa el motivo de cancelación. |
| | 5 | El sistema solicita confirmación de la acción. |
| | 6 | El usuario confirma la cancelación. |
| | 7 | El sistema cambia el estado del lavado a "Cancelado". |
| | 8 | El sistema registra el motivo y la fecha de cancelación. |
| | 9 | El sistema muestra un mensaje de éxito. |
| | 10 | El sistema registra la acción en auditoría. |
| **Postcondición** | El lavado ha sido cancelado. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si no se ingresa motivo, el sistema informa que es obligatorio. |
| | 6a | Si el usuario no confirma, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 7-8 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. El motivo de cancelación es obligatorio para trazabilidad. | |

---

### CU-056 - Cancelar servicio en lavado

| UC–056 | Cancelar servicio en lavado | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal cancela un servicio específico dentro de un lavado indicando el motivo, permitiendo que el lavado continúe con los servicios restantes. | |
| **Precondición** | El usuario debe estar autenticado. El lavado debe estar en estado "EnProceso". El servicio debe estar pendiente o en proceso. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
| | 2 | El usuario hace clic en "Cancelar" en el servicio correspondiente. |
| | 3 | El sistema muestra un formulario solicitando el motivo de cancelación. |
| | 4 | El usuario ingresa el motivo de cancelación. |
| | 5 | El sistema solicita confirmación. |
| | 6 | El usuario confirma la cancelación. |
| | 7 | El sistema marca el servicio como "Cancelado". |
| | 8 | El sistema recalcula el precio total del lavado. |
| | 9 | El sistema actualiza la vista del detalle del lavado. |
| | 10 | El sistema registra la acción en auditoría. |
| **Postcondición** | El servicio ha sido cancelado y el precio del lavado recalculado. | |
| **Excepciones** | **Paso** | **Acción** |
| | 6a | Si el usuario no confirma, se aborta la operación. |
| | 7a | Si era el único servicio, el sistema sugiere cancelar el lavado completo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 7-8 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. | |

---

### CU-057 - Registrar pago recibido

| UC–057 | Registrar pago recibido | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–05 Registro de Pagos | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal registra un pago total recibido por un cliente, actualizando el estado de pago del lavado a "Pagado". | |
| **Precondición** | El usuario debe estar autenticado. El lavado debe existir y tener saldo pendiente. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
| | 2 | El usuario hace clic en "Registrar Pago". |
| | 3 | El sistema muestra el monto total, monto pagado y saldo pendiente. |
| | 4 | El sistema pre-selecciona el saldo pendiente como monto a pagar. |
| | 5 | El usuario confirma el monto y selecciona el método de pago. |
| | 6 | El usuario hace clic en "Confirmar Pago". |
| | 7 | El sistema valida que el monto sea válido. |
| | 8 | El sistema registra el pago con fecha y hora. |
| | 9 | El sistema actualiza el monto pagado y cambia el estado a "Pagado". |
| | 10 | El sistema muestra un mensaje de éxito. |
| | 11 | El sistema registra la acción en auditoría. |
| **Postcondición** | El pago está registrado y el lavado está marcado como pagado. | |
| **Excepciones** | **Paso** | **Acción** |
| | 7a | Si el monto es inválido, el sistema muestra error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 8-9 | 1 segundo |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. | |

---

### CU-058 - Registrar pago parcial

| UC–058 | Registrar pago parcial | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–05 Registro de Pagos | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal registra un pago parcial indicando el monto recibido, el estado de pago se actualiza a "Parcial" y se mantiene registro de cada pago individual. | |
| **Precondición** | El usuario debe estar autenticado. El lavado debe existir y tener saldo pendiente. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
| | 2 | El usuario hace clic en "Registrar Pago". |
| | 3 | El sistema muestra el monto total, monto pagado y saldo pendiente. |
| | 4 | El usuario ingresa un monto menor al saldo pendiente. |
| | 5 | El usuario selecciona el método de pago. |
| | 6 | El usuario hace clic en "Confirmar Pago". |
| | 7 | El sistema valida que el monto no exceda el saldo pendiente. |
| | 8 | El sistema registra el pago con fecha, hora y monto. |
| | 9 | El sistema actualiza el monto pagado. |
| | 10 | El sistema cambia el estado de pago a "Parcial". |
| | 11 | El sistema muestra un mensaje de éxito con el saldo restante. |
| | 12 | El sistema registra la acción en auditoría. |
| **Postcondición** | El pago parcial está registrado y el saldo pendiente actualizado. | |
| **Excepciones** | **Paso** | **Acción** |
| | 7a | Si el monto excede el saldo pendiente, el sistema informa el error. |
| | 7b | Si el monto es menor o igual a cero, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 8-10 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. Se permite registrar múltiples pagos parciales. | |

---

### CU-059 - Marcar vehículo como retirado

| UC–059 | Marcar vehículo como retirado | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El personal marca un vehículo como retirado por el cliente, registrando la fecha y hora del retiro. | |
| **Precondición** | El usuario debe estar autenticado. El lavado debe estar finalizado (Realizado o RealizadoParcialmente). | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-049 Ver detalle de lavado. |
| | 2 | El usuario hace clic en "Marcar como Retirado". |
| | 3 | El sistema muestra los clientes asociados al vehículo. |
| | 4 | El usuario selecciona el cliente que retira el vehículo. |
| | 5 | El usuario hace clic en "Confirmar Retiro". |
| | 6 | El sistema actualiza el estado de retiro a "Retirado". |
| | 7 | El sistema registra el cliente que retiró y la fecha/hora. |
| | 8 | El sistema muestra un mensaje de éxito. |
| | 9 | El sistema registra la acción en auditoría. |
| **Postcondición** | El vehículo está marcado como retirado. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si no se selecciona cliente, el sistema puede registrar el retiro sin asignar responsable. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6-7 | 1 segundo |
| **Frecuencia** | Frecuente | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-049 Ver detalle de lavado. | |

---

### CU-060 - Calcular duración estimada de lavado

| UC–060 | Calcular duración estimada de lavado | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–04 Registro y Gestión de Lavados | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados, IRQ–04 Información sobre Servicios | |
| **Descripción** | El sistema calcula automáticamente la duración estimada de un lavado sumando los tiempos estimados de cada servicio seleccionado. | |
| **Precondición** | Se están seleccionando servicios para un lavado (dentro de CU-046). | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario selecciona uno o más servicios para el lavado. |
| | 2 | El sistema obtiene el tiempo estimado de cada servicio seleccionado. |
| | 3 | El sistema suma todos los tiempos estimados. |
| | 4 | El sistema muestra el tiempo total estimado al usuario. |
| | 5 | El sistema utiliza este tiempo para calcular la hora estimada de finalización. |
| **Postcondición** | El tiempo estimado total está calculado y mostrado. | |
| **Excepciones** | **Paso** | **Acción** |
| | - | - |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-4 | Inmediato |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Este cálculo se realiza en tiempo real mientras el usuario selecciona servicios. | |
### Módulo: Configuración

### CU-061 - Configurar horarios del lavadero

| UC–061 | Configurar horarios del lavadero | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–11 Gestión de Configuración del Sistema | |
| **Requisitos asociados** | IRQ–11 Información de Configuración del Sistema | |
| **Descripción** | El administrador configura el horario de funcionamiento del establecimiento para cada día de la semana, incluyendo horarios divididos y días cerrados. | |
| **Precondición** | El usuario debe tener rol de Administrador. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de configuración del sistema. |
| | 2 | El sistema muestra la configuración actual de horarios por día de la semana. |
| | 3 | Para cada día, el administrador puede configurar: |
| | 3a | Horario continuo: ej. "09:00-18:00" |
| | 3b | Horario dividido: ej. "09:00-13:00,15:00-19:00" |
| | 3c | Día cerrado: "CERRADO" |
| | 4 | El administrador hace clic en "Guardar". |
| | 5 | El sistema valida que los horarios sean coherentes (hora fin > hora inicio). |
| | 6 | El sistema actualiza la configuración. |
| | 7 | El sistema muestra un mensaje de éxito. |
| | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | Los horarios de operación han sido actualizados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si hay errores de formato o coherencia, el sistema muestra los errores. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | Los horarios afectan la validación al crear lavados y la información mostrada a clientes por WhatsApp. | |

---

### CU-062 - Configurar capacidad concurrente

| UC–062 | Configurar capacidad concurrente | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–11 Gestión de Configuración del Sistema | |
| **Requisitos asociados** | IRQ–11 Información de Configuración del Sistema | |
| **Descripción** | El administrador configura el número máximo de lavados que se pueden atender simultáneamente y si se debe considerar el número de empleados activos para calcular la capacidad efectiva. | |
| **Precondición** | El usuario debe tener rol de Administrador. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de configuración del sistema. |
| | 2 | El sistema muestra la configuración actual de capacidad. |
| | 3 | El administrador configura: |
| | 3a | Número máximo de lavados simultáneos. |
| | 3b | Si se debe considerar el número de empleados activos. |
| | 4 | El administrador hace clic en "Guardar". |
| | 5 | El sistema valida que el número sea mayor a cero. |
| | 6 | El sistema actualiza la configuración. |
| | 7 | El sistema muestra un mensaje de éxito. |
| | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | La configuración de capacidad ha sido actualizada. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si el número es inválido, el sistema muestra error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | Esta configuración afecta la validación al crear nuevos lavados. | |

---

### CU-063 - Configurar tiempos de tolerancia y notificación

| UC–063 | Configurar tiempos de tolerancia y notificación | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–11 Gestión de Configuración del Sistema | |
| **Requisitos asociados** | IRQ–11 Información de Configuración del Sistema | |
| **Descripción** | El administrador configura los minutos de anticipación para notificar antes del tiempo estimado, los minutos de tolerancia máxima y el intervalo para preguntar si ya terminó cuando se excede el tiempo. | |
| **Precondición** | El usuario debe tener rol de Administrador. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de configuración del sistema. |
| | 2 | El sistema muestra la configuración actual de tiempos. |
| | 3 | El administrador configura: |
| | 3a | Minutos de anticipación para notificación. |
| | 3b | Minutos de tolerancia máxima después del tiempo estimado. |
| | 3c | Intervalo en minutos para consultar si ya terminó. |
| | 4 | El administrador hace clic en "Guardar". |
| | 5 | El sistema valida que los valores sean mayores a cero. |
| | 6 | El sistema actualiza la configuración. |
| | 7 | El sistema muestra un mensaje de éxito. |
| | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | La configuración de tiempos ha sido actualizada. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si hay valores inválidos, el sistema muestra error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Estos tiempos afectan el comportamiento de las notificaciones automáticas. | |

---

### CU-064 - Configurar duración de sesión

| UC–064 | Configurar duración de sesión | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–11 Gestión de Configuración del Sistema | |
| **Requisitos asociados** | IRQ–11 Información de Configuración del Sistema | |
| **Descripción** | El administrador configura la duración máxima de la sesión en horas y el tiempo de inactividad en minutos antes del cierre automático. | |
| **Precondición** | El usuario debe tener rol de Administrador. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de configuración del sistema. |
| | 2 | El sistema muestra la configuración actual de sesión. |
| | 3 | El administrador configura: |
| | 3a | Duración máxima de sesión en horas. |
| | 3b | Tiempo de inactividad en minutos para cierre automático. |
| | 4 | El administrador hace clic en "Guardar". |
| | 5 | El sistema valida que los valores sean mayores a cero. |
| | 6 | El sistema actualiza la configuración. |
| | 7 | El sistema muestra un mensaje de éxito. |
| | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | La configuración de sesión ha sido actualizada. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si hay valores inválidos, el sistema muestra error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Esta configuración afecta el comportamiento de CU-004. | |

---

### CU-065 - Configurar nombre y ubicación del lavadero

| UC–065 | Configurar nombre y ubicación del lavadero | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–11 Gestión de Configuración del Sistema | |
| **Requisitos asociados** | IRQ–11 Información de Configuración del Sistema | |
| **Descripción** | El administrador configura el nombre del lavadero y su ubicación, que se muestran en las notificaciones por WhatsApp y otros lugares del sistema. | |
| **Precondición** | El usuario debe tener rol de Administrador. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de configuración del sistema. |
| | 2 | El sistema muestra la configuración actual de información del lavadero. |
| | 3 | El administrador configura: |
| | 3a | Nombre del lavadero. |
| | 3b | Dirección/Ubicación. |
| | 3c | Teléfono de contacto. |
| | 3d | Email de contacto. |
| | 4 | El administrador hace clic en "Guardar". |
| | 5 | El sistema valida los datos (campos obligatorios, formato de email). |
| | 6 | El sistema actualiza la configuración. |
| | 7 | El sistema muestra un mensaje de éxito. |
| | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | La información del lavadero ha sido actualizada. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si hay errores de validación, el sistema muestra los errores. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Esta información se muestra a los clientes a través de WhatsApp (CU-093). | |

---

### CU-066 - Configurar paso de descuento para paquetes

| UC–066 | Configurar paso de descuento para paquetes | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–11 Gestión de Configuración del Sistema | |
| **Requisitos asociados** | IRQ–11 Información de Configuración del Sistema | |
| **Descripción** | El administrador configura el incremento mínimo de porcentaje de descuento al crear paquetes de servicios (mínimo 5%). | |
| **Precondición** | El usuario debe tener rol de Administrador. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de configuración del sistema. |
| | 2 | El sistema muestra la configuración actual de descuentos para paquetes. |
| | 3 | El administrador configura: |
| | 3a | Porcentaje mínimo de descuento. |
| | 3b | Porcentaje máximo de descuento. |
| | 3c | Incremento (step) de descuento (mínimo 5%). |
| | 4 | El administrador hace clic en "Guardar". |
| | 5 | El sistema valida que mínimo <= máximo y step >= 5. |
| | 6 | El sistema actualiza la configuración. |
| | 7 | El sistema muestra un mensaje de éxito. |
| | 8 | El sistema registra la acción en auditoría. |
| **Postcondición** | La configuración de descuentos ha sido actualizada. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si hay errores de validación, el sistema muestra los errores. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6 | 1 segundo |
| **Frecuencia** | Rara | |
| **Estabilidad** | Baja | |

---

### Módulo: Planificación de Turnos

### CU-067 - Registrar turno

| UC–067 | Registrar turno | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos | |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos | |
| **Descripción** | El personal agenda un turno para un cliente en el sistema, especificando fecha, hora y servicios solicitados. | |
| **Precondición** | El usuario debe tener rol de Administrador o Empleado. Deben existir clientes, vehículos y servicios activos. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la sección de gestión de turnos. |
| | 2 | El usuario hace clic en "Nuevo Turno". |
| | 3 | El sistema muestra un formulario con campos: Fecha, Hora, Cliente, Vehículo, Servicios. |
| | 4 | El usuario selecciona la fecha del turno. |
| | 5 | El sistema muestra los horarios disponibles según la configuración y turnos existentes. |
| | 6 | El usuario selecciona la hora del turno. |
| | 7 | El usuario busca y selecciona el cliente. |
| | 8 | El sistema muestra los vehículos asociados al cliente. |
| | 9 | El usuario selecciona el vehículo. |
| | 10 | El sistema carga los servicios disponibles para el tipo de vehículo. |
| | 11 | El usuario selecciona los servicios deseados. |
| | 12 | El sistema ejecuta CU-074 para validar disponibilidad. |
| | 13 | El usuario hace clic en "Guardar". |
| | 14 | El sistema crea el turno con estado "Pendiente". |
| | 15 | El sistema muestra un mensaje de éxito. |
| | 16 | El sistema registra la acción en auditoría. |
| **Postcondición** | El turno está registrado en la agenda. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si no hay horarios disponibles en la fecha, el sistema lo informa. |
| | 12a | Si hay conflicto de horarios, el sistema informa y sugiere alternativas. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 14 | 1 segundo |
| **Frecuencia** | Frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | El sistema valida automáticamente que no haya solapamientos. | |

---

### CU-068 - Modificar turno

| UC–068 | Modificar turno | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos | |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos | |
| **Descripción** | El personal actualiza la información de un turno ya registrado, validando la disponibilidad en el nuevo horario. | |
| **Precondición** | El usuario debe tener rol de Administrador o Empleado. El turno debe existir y estar en estado "Pendiente". | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-069 Consultar turnos asignados. |
| | 2 | El usuario selecciona el turno a modificar. |
| | 3 | El sistema muestra el formulario de edición con los datos actuales. |
| | 4 | El usuario modifica los campos deseados (fecha, hora, servicios). |
| | 5 | El sistema ejecuta CU-075 para validar disponibilidad en el nuevo horario. |
| | 6 | El usuario hace clic en "Guardar". |
| | 7 | El sistema actualiza el turno. |
| | 8 | El sistema muestra un mensaje de éxito. |
| | 9 | El sistema registra la acción en auditoría. |
| **Postcondición** | El turno ha sido actualizado. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si no hay disponibilidad, el sistema informa y sugiere alternativas. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 7 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-069 Consultar turnos asignados. | |

---

### CU-069 - Consultar turnos asignados

| UC–069 | Consultar turnos asignados | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos | |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos | |
| **Descripción** | El personal consulta la agenda de turnos registrados en el sistema con vista de calendario. | |
| **Precondición** | El usuario debe tener rol de Administrador o Empleado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la sección de gestión de turnos. |
| | 2 | El sistema obtiene los turnos del período actual (semana/mes). |
| | 3 | El sistema muestra la vista de calendario con los turnos. |
| | 4 | El usuario puede cambiar entre vista diaria, semanal o mensual. |
| | 5 | El usuario puede navegar entre fechas. |
| | 6 | El usuario puede filtrar por estado del turno. |
| | 7 | El sistema actualiza la vista según los criterios seleccionados. |
| | 8 | El usuario puede hacer clic en un turno para ver detalles. |
| **Postcondición** | El usuario visualiza la agenda de turnos. | |
| **Excepciones** | **Paso** | **Acción** |
| | 2a | Si no hay turnos en el período, el sistema muestra calendario vacío. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-3 | 2 segundos |
| **Frecuencia** | Frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | La vista de calendario permite una visualización rápida de la disponibilidad. | |

---

### CU-070 - Cancelar turno

| UC–070 | Cancelar turno | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos | |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos | |
| **Descripción** | El personal cancela un turno previamente asignado a un cliente. | |
| **Precondición** | El usuario debe tener rol de Administrador o Empleado. El turno debe existir y estar en estado "Pendiente". | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-069 Consultar turnos asignados. |
| | 2 | El usuario selecciona el turno a cancelar. |
| | 3 | El sistema muestra un diálogo de confirmación solicitando motivo. |
| | 4 | El usuario ingresa el motivo de cancelación. |
| | 5 | El usuario confirma la cancelación. |
| | 6 | El sistema cambia el estado del turno a "Cancelado". |
| | 7 | El sistema ejecuta CU-076 para reorganizar agenda si corresponde. |
| | 8 | El sistema puede notificar al cliente por WhatsApp (si está configurado). |
| | 9 | El sistema muestra un mensaje de éxito. |
| | 10 | El sistema registra la acción en auditoría. |
| **Postcondición** | El turno ha sido cancelado y la agenda puede reorganizarse. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si el administrador no confirma, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6-7 | 2 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-069 Consultar turnos asignados. | |

---

### CU-071 - Solicitar turno por WhatsApp

| UC–071 | Solicitar turno por WhatsApp | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos, OBJ–10 Integración con WhatsApp | |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos, IRQ–12 Información de Sesiones WhatsApp | |
| **Descripción** | El cliente agenda un turno directamente mediante el flujo conversacional de WhatsApp, indicando vehículo y servicios deseados. | |
| **Precondición** | El cliente debe estar registrado y tener al menos un vehículo asociado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El cliente accede al menú principal de WhatsApp. |
| | 2 | El cliente selecciona "Solicitar Turno". |
| | 3 | El sistema muestra los vehículos del cliente. |
| | 4 | El cliente selecciona el vehículo. |
| | 5 | El sistema muestra los servicios disponibles para ese tipo de vehículo. |
| | 6 | El cliente selecciona los servicios deseados. |
| | 7 | El sistema calcula la duración estimada. |
| | 8 | El sistema muestra las fechas disponibles. |
| | 9 | El cliente selecciona la fecha. |
| | 10 | El sistema muestra los horarios disponibles. |
| | 11 | El cliente selecciona el horario. |
| | 12 | El sistema ejecuta CU-074 para validar disponibilidad. |
| | 13 | El sistema muestra resumen y solicita confirmación. |
| | 14 | El cliente confirma el turno. |
| | 15 | El sistema crea el turno con estado "Pendiente". |
| | 16 | El sistema envía confirmación del turno al cliente. |
| | 17 | El sistema registra la acción en auditoría. |
| **Postcondición** | El turno está registrado y el cliente notificado. | |
| **Excepciones** | **Paso** | **Acción** |
| | 3a | Si no tiene vehículos, el sistema ofrece registrar uno (CU-027). |
| | 12a | Si no hay disponibilidad, el sistema ofrece alternativas. |
| | 14a | Si el cliente no confirma, puede modificar la selección. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 15 | 2 segundos |
| **Frecuencia** | Frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | El flujo guía al cliente paso a paso de forma conversacional. | |

---

### CU-072 - Consultar turnos próximos por WhatsApp

| UC–072 | Consultar turnos próximos por WhatsApp | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos, OBJ–10 Integración con WhatsApp | |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos, IRQ–12 Información de Sesiones WhatsApp | |
| **Descripción** | El cliente visualiza los turnos futuros registrados a su nombre mediante WhatsApp. | |
| **Precondición** | El cliente debe estar registrado en el sistema. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El cliente accede al menú principal de WhatsApp. |
| | 2 | El cliente selecciona "Mis Turnos". |
| | 3 | El sistema busca los turnos pendientes del cliente. |
| | 4 | El sistema muestra la lista de turnos con: fecha, hora, vehículo, servicios. |
| | 5 | El cliente puede seleccionar un turno para ver más detalles. |
| | 6 | El sistema muestra el detalle del turno seleccionado. |
| | 7 | El sistema ofrece opciones: cancelar turno, volver al menú. |
| **Postcondición** | El cliente ha visualizado sus turnos próximos. | |
| **Excepciones** | **Paso** | **Acción** |
| | 3a | Si no tiene turnos, el sistema informa y ofrece solicitar uno. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 3-4 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Solo se muestran turnos con estado "Pendiente" o "Confirmado". | |

---

### CU-073 - Cancelar turno por WhatsApp

| UC–073 | Cancelar turno por WhatsApp | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos, OBJ–10 Integración con WhatsApp | |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos, IRQ–12 Información de Sesiones WhatsApp | |
| **Descripción** | El cliente cancela un turno previamente asignado mediante el flujo de WhatsApp. | |
| **Precondición** | El cliente debe estar registrado y tener turnos pendientes. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta CU-072 para mostrar los turnos del cliente. |
| | 2 | El cliente selecciona el turno a cancelar. |
| | 3 | El sistema muestra el detalle del turno. |
| | 4 | El cliente selecciona "Cancelar Turno". |
| | 5 | El sistema solicita confirmación. |
| | 6 | El cliente confirma la cancelación. |
| | 7 | El sistema cambia el estado del turno a "Cancelado". |
| | 8 | El sistema ejecuta CU-076 para reorganizar agenda si corresponde. |
| | 9 | El sistema confirma la cancelación al cliente. |
| | 10 | El sistema registra la acción en auditoría. |
| **Postcondición** | El turno ha sido cancelado. | |
| **Excepciones** | **Paso** | **Acción** |
| | 6a | Si el cliente no confirma, se aborta la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 7-8 | 2 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | La cancelación puede activar la reorganización de agenda para otros clientes. | |

---

### CU-074 - Asignar turno automáticamente sin superposición

| UC–074 | Asignar turno automáticamente sin superposición | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos | |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos, IRQ–11 Información de Configuración | |
| **Descripción** | El sistema asigna turnos asegurando que no existan solapamientos en la agenda, considerando la duración estimada de los servicios. | |
| **Precondición** | Se está creando o modificando un turno. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El sistema recibe la fecha, hora y duración estimada del turno solicitado. |
| | 2 | El sistema calcula la hora de finalización estimada. |
| | 3 | El sistema obtiene los turnos existentes en esa fecha. |
| | 4 | El sistema verifica que no haya solapamiento con otros turnos. |
| | 5 | El sistema verifica que esté dentro del horario de operación. |
| | 6 | El sistema verifica que no se exceda la capacidad máxima de lavados simultáneos. |
| | 7 | Si todas las validaciones pasan, el sistema confirma la disponibilidad. |
| **Postcondición** | La disponibilidad del horario ha sido validada. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si hay solapamiento, el sistema retorna error con los horarios alternativos disponibles. |
| | 5a | Si está fuera del horario de operación, el sistema informa el error. |
| | 6a | Si se excede la capacidad, el sistema informa el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 3-7 | 500ms |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Este caso de uso es ejecutado automáticamente por el sistema. | |

---

### CU-075 - Validar disponibilidad al mover un turno

| UC–075 | Validar disponibilidad al mover un turno | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos | |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos | |
| **Descripción** | El sistema valida si existe disponibilidad en la agenda al modificar la fecha u hora de un turno. | |
| **Precondición** | Se está modificando un turno existente (dentro de CU-068). | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El sistema recibe la nueva fecha, hora y duración del turno. |
| | 2 | El sistema excluye el turno actual de la verificación de solapamientos. |
| | 3 | El sistema ejecuta las mismas validaciones que CU-074. |
| | 4 | Si todas las validaciones pasan, el sistema confirma la disponibilidad. |
| **Postcondición** | La disponibilidad del nuevo horario ha sido validada. | |
| **Excepciones** | **Paso** | **Acción** |
| | 3a | Si no hay disponibilidad, el sistema retorna los horarios alternativos. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-4 | 500ms |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | Este caso de uso es ejecutado automáticamente por el sistema. | |

---

### CU-076 - Reorganizar agenda ante cancelaciones

| UC–076 | Reorganizar agenda ante cancelaciones | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–06 Planificación y Gestión de Turnos | |
| **Requisitos asociados** | IRQ–09 Información sobre Turnos | |
| **Descripción** | El sistema reordena automáticamente la agenda de turnos cuando ocurre una cancelación, notificando a clientes sobre posibles adelantos. | |
| **Precondición** | Se ha cancelado un turno (dentro de CU-070 o CU-073). | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El sistema identifica el horario liberado por la cancelación. |
| | 2 | El sistema busca turnos posteriores que podrían adelantarse. |
| | 3 | Para cada turno candidato, el sistema verifica si el cliente podría beneficiarse. |
| | 4 | El sistema envía notificación por WhatsApp ofreciendo el adelanto. |
| | 5 | Si el cliente acepta, el sistema actualiza el horario del turno. |
| | 6 | El sistema registra los cambios en auditoría. |
| **Postcondición** | La agenda ha sido optimizada y los clientes notificados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 2a | Si no hay turnos que puedan adelantarse, el proceso termina. |
| | 5a | Si el cliente rechaza o no responde, se consulta al siguiente candidato. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 1-4 | 3 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Este caso de uso es ejecutado automáticamente por el sistema. | |
### Módulo: Notificación al Cliente

### CU-077 - Enviar notificación por WhatsApp

| UC–077 | Enviar notificación por WhatsApp | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–12 Notificación al Cliente | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes | |
| **Descripción** | El personal envía notificaciones personalizadas a los clientes por WhatsApp utilizando la integración con WhatsApp Cloud API. | |
| **Precondición** | El usuario debe estar autenticado. El cliente debe tener número de teléfono registrado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la función de enviar notificación (desde detalle de lavado u otro contexto). |
| | 2 | El sistema muestra el formulario de notificación con el cliente preseleccionado. |
| | 3 | El usuario redacta el mensaje personalizado. |
| | 4 | El usuario hace clic en "Enviar". |
| | 5 | El sistema envía el mensaje a través de WhatsApp Cloud API. |
| | 6 | El sistema muestra confirmación del envío. |
| | 7 | El sistema registra la acción en auditoría. |
| **Postcondición** | El mensaje ha sido enviado al cliente. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si hay error en el envío, el sistema muestra el mensaje de error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 5 | 2 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Requiere configuración válida de WhatsApp Cloud API. | |

---

### CU-078 - Enviar notificación por correo electrónico

| UC–078 | Enviar notificación por correo electrónico | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–12 Notificación al Cliente | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes | |
| **Descripción** | El personal envía notificaciones personalizadas a los clientes por correo electrónico. | |
| **Precondición** | El usuario debe estar autenticado. El cliente debe tener email registrado. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario accede a la función de enviar notificación por email. |
| | 2 | El sistema muestra el formulario de notificación con el cliente preseleccionado. |
| | 3 | El usuario redacta el asunto y cuerpo del mensaje. |
| | 4 | El usuario hace clic en "Enviar". |
| | 5 | El sistema envía el correo electrónico. |
| | 6 | El sistema muestra confirmación del envío. |
| | 7 | El sistema registra la acción en auditoría. |
| **Postcondición** | El correo ha sido enviado al cliente. | |
| **Excepciones** | **Paso** | **Acción** |
| | 2a | Si el cliente no tiene email, el sistema informa el error. |
| | 5a | Si hay error en el envío, el sistema muestra el mensaje de error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 5 | 3 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Requiere configuración de servicio de correo electrónico. | |

---

### CU-079 - Notificar etapa finalizada

| UC–079 | Notificar etapa finalizada | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–12 Notificación al Cliente | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–08 Información sobre Lavados | |
| **Descripción** | El sistema envía automáticamente una notificación por WhatsApp al cliente cuando una etapa de su servicio ha sido finalizada. | |
| **Precondición** | Una etapa de servicio ha sido completada (dentro de CU-052). La notificación automática está habilitada. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El sistema detecta que una etapa ha sido finalizada. |
| | 2 | El sistema obtiene los datos del cliente y del lavado. |
| | 3 | El sistema genera el mensaje de notificación con detalles de la etapa completada. |
| | 4 | El sistema envía el mensaje por WhatsApp al cliente. |
| | 5 | El sistema registra la notificación enviada. |
| **Postcondición** | El cliente ha sido notificado del progreso de su servicio. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si hay error en el envío, se registra el error pero no se interrumpe el proceso. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-4 | 2 segundos |
| **Frecuencia** | Frecuente | |
| **Estabilidad** | Media | |
| **Comentarios** | Este caso de uso es ejecutado automáticamente por el sistema. | |

---

### CU-080 - Notificar lavado finalizado

| UC–080 | Notificar lavado finalizado | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–12 Notificación al Cliente | |
| **Requisitos asociados** | IRQ–02 Información sobre Clientes, IRQ–08 Información sobre Lavados | |
| **Descripción** | El sistema envía automáticamente una notificación por WhatsApp al cliente cuando el lavado de su vehículo está completo y listo para retirar. | |
| **Precondición** | Un lavado ha sido finalizado (dentro de CU-054). La notificación automática está habilitada. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El sistema detecta que un lavado ha sido finalizado. |
| | 2 | El sistema obtiene los datos del cliente, vehículo y lavado. |
| | 3 | El sistema genera el mensaje de notificación indicando que el vehículo está listo. |
| | 4 | El sistema incluye información del lavadero (nombre, ubicación). |
| | 5 | El sistema envía el mensaje por WhatsApp al cliente. |
| | 6 | El sistema registra la notificación enviada. |
| **Postcondición** | El cliente ha sido notificado que su vehículo está listo. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si hay error en el envío, se registra el error pero no se interrumpe el proceso. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-5 | 2 segundos |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | Este caso de uso es ejecutado automáticamente por el sistema. | |

---

### CU-081 - Solicitar hablar con el personal

| UC–081 | Solicitar hablar con el personal | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–10 Integración con WhatsApp | |
| **Requisitos asociados** | IRQ–12 Información de Sesiones WhatsApp | |
| **Descripción** | El cliente envía un mensaje por WhatsApp para comunicarse directamente con el personal del lavadero, saliendo del flujo automatizado. | |
| **Precondición** | El cliente está en una conversación activa de WhatsApp. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El cliente selecciona "Hablar con personal" del menú o escribe un mensaje libre. |
| | 2 | El sistema detecta la solicitud de atención humana. |
| | 3 | El sistema marca la sesión como "requiere atención humana". |
| | 4 | El sistema envía mensaje confirmando que un empleado responderá pronto. |
| | 5 | El sistema notifica al personal del lavadero sobre la solicitud. |
| | 6 | Los mensajes siguientes se almacenan para revisión del personal. |
| **Postcondición** | La conversación ha sido escalada a atención humana. | |
| **Excepciones** | **Paso** | **Acción** |
| | - | - |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 3-4 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Baja | |
| **Comentarios** | Permite manejar casos que el bot no puede resolver automáticamente. | |

---

### Módulo: Estadísticas y Reportes

### CU-082 - Consultar estadísticas básicas

| UC–082 | Consultar estadísticas básicas | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–08 Módulo de Estadísticas y Reportes | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados, IRQ–02 Información sobre Clientes | |
| **Descripción** | El administrador consulta estadísticas sobre la actividad general del lavadero: lavados realizados, clientes activos, servicios más solicitados, cumplimiento de turnos. | |
| **Precondición** | El usuario debe tener rol de Administrador. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de estadísticas. |
| | 2 | El sistema calcula y muestra indicadores clave: |
| | 2a | Total de lavados realizados (por período). |
| | 2b | Lavados activos actualmente. |
| | 2c | Total de clientes registrados (activos/nuevos). |
| | 2d | Ingresos totales (por período). |
| | 2e | Servicios más solicitados. |
| | 2f | Promedio de tiempo por lavado. |
| | 2g | Tasa de cumplimiento de turnos. |
| | 3 | El administrador puede filtrar por rango de fechas. |
| | 4 | El sistema actualiza las estadísticas según el filtro. |
| **Postcondición** | El administrador visualiza las estadísticas del lavadero. | |
| **Excepciones** | **Paso** | **Acción** |
| | 2a | Si no hay datos, el sistema muestra valores en cero. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2 | 3 segundos |
| **Frecuencia** | Frecuente | |
| **Estabilidad** | Media | |
| **Comentarios** | Las estadísticas se calculan en tiempo real. | |

---

### CU-083 - Consultar historial de pagos

| UC–083 | Consultar historial de pagos | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–08 Módulo de Estadísticas y Reportes | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | El administrador accede al historial de todos los pagos registrados con opciones de filtrado por fecha, cliente y monto. | |
| **Precondición** | El usuario debe tener rol de Administrador. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de historial de pagos. |
| | 2 | El sistema obtiene los pagos registrados. |
| | 3 | El sistema muestra la tabla de pagos con: fecha, lavado, cliente, monto, método de pago. |
| | 4 | El administrador puede filtrar por: rango de fechas, cliente, rango de montos. |
| | 5 | El administrador puede ordenar por cualquier columna. |
| | 6 | El sistema muestra totales: suma de pagos, cantidad de transacciones. |
| **Postcondición** | El administrador visualiza el historial de pagos. | |
| **Excepciones** | **Paso** | **Acción** |
| | 2a | Si no hay pagos, el sistema muestra mensaje indicándolo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-3 | 2 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Ninguno. | |

---

### CU-084 - Generar reportes

| UC–084 | Generar reportes | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–08 Módulo de Estadísticas y Reportes | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados, IRQ–02 Información sobre Clientes | |
| **Descripción** | El administrador genera reportes personalizables de los diversos aspectos del sistema para un período de tiempo específico. | |
| **Precondición** | El usuario debe tener rol de Administrador. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de reportes. |
| | 2 | El sistema muestra los tipos de reportes disponibles: |
| | 2a | Reporte de lavados (por período, estado, cliente). |
| | 2b | Reporte de ingresos (por período, método de pago). |
| | 2c | Reporte de clientes (nuevos registros, frecuencia). |
| | 2d | Reporte de servicios (más solicitados, ingresos por servicio). |
| | 3 | El administrador selecciona el tipo de reporte. |
| | 4 | El administrador configura los filtros del reporte (fechas, criterios). |
| | 5 | El administrador hace clic en "Generar Reporte". |
| | 6 | El sistema genera el reporte con los datos filtrados. |
| | 7 | El sistema muestra una vista previa del reporte. |
| | 8 | El administrador puede exportar el reporte (CU-084). |
| **Postcondición** | El reporte ha sido generado. | |
| **Excepciones** | **Paso** | **Acción** |
| | 6a | Si no hay datos para el reporte, el sistema informa que no hay resultados. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 6-7 | 5 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Los reportes permiten análisis detallado para la toma de decisiones. | |

---

### CU-084.1 - Exportar reportes a PDF o Excel

| UC–084.1 | Exportar reportes a PDF o Excel | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–08 Módulo de Estadísticas y Reportes | |
| **Requisitos asociados** | IRQ–08 Información sobre Lavados | |
| **Descripción** | Extiende del CU-084. El sistema permite exportar los reportes generados en formato PDF o Excel. | |
| **Precondición** | Se ha generado un reporte (dentro de CU-084). | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador visualiza el reporte generado. |
| | 2 | El administrador selecciona el formato de exportación (PDF o Excel). |
| | 3 | El administrador hace clic en "Exportar". |
| | 4 | El sistema genera el archivo en el formato seleccionado. |
| | 5 | El sistema inicia la descarga del archivo. |
| | 6 | El sistema muestra confirmación de exportación exitosa. |
| **Postcondición** | El reporte ha sido exportado y descargado. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si hay error en la generación, el sistema muestra el error. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 4-5 | 5 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Ninguno. | |

---

### Módulo: Auditoría

### CU-085 - Consultar historial de auditoría

| UC–085 | Consultar historial de auditoría | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–07 Registro de Auditoría | |
| **Requisitos asociados** | IRQ–10 Información de Auditoría | |
| **Descripción** | El administrador accede al registro completo de acciones realizadas en el sistema, con información del usuario, acción, fecha/hora y entidad afectada. | |
| **Precondición** | El usuario debe tener rol de Administrador. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El administrador accede a la sección de auditoría. |
| | 2 | El sistema obtiene los registros de auditoría aplicando filtros por defecto. |
| | 3 | El sistema resuelve los nombres de usuarios y entidades afectadas. |
| | 4 | El sistema muestra la tabla de registros con: fecha/hora, usuario, acción, entidad afectada. |
| | 5 | El administrador puede usar CU-086 para filtrar los registros. |
| | 6 | El administrador puede ordenar por fecha (ascendente/descendente). |
| | 7 | El administrador puede paginar los resultados. |
| **Postcondición** | El administrador visualiza el historial de auditoría. | |
| **Excepciones** | **Paso** | **Acción** |
| | 2a | Si no hay registros, el sistema muestra mensaje indicándolo. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 2-4 | 2 segundos |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Alta | |
| **Comentarios** | Los nombres de usuarios y entidades se resuelven dinámicamente. | |

---

### CU-086 - Filtrar registros de auditoría

| UC–086 | Filtrar registros de auditoría | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–07 Registro de Auditoría | |
| **Requisitos asociados** | IRQ–10 Información de Auditoría | |
| **Descripción** | El administrador filtra los registros de auditoría por rango de fechas, tipo de acción, tipo de entidad objetivo y usuario. | |
| **Precondición** | Se está consultando el historial de auditoría (dentro de CU-085). | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-085 Consultar historial de auditoría. |
| | 2 | El sistema muestra los controles de filtrado. |
| | 3 | El administrador puede filtrar por: |
| | 3a | Rango de fechas (desde/hasta). |
| | 3b | Tipo de acción (creación, modificación, eliminación, login, etc.). |
| | 3c | Tipo de entidad afectada (Servicio, Cliente, Empleado, Lavado, etc.). |
| | 3d | Usuario que realizó la acción. |
| | 4 | El administrador aplica los filtros. |
| | 5 | El sistema actualiza la lista de registros según los filtros. |
| **Postcondición** | Los registros de auditoría están filtrados según los criterios seleccionados. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si no hay registros que coincidan, el sistema lo informa. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 5 | 1 segundo |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-085 Consultar historial de auditoría. | |

---

### CU-087 - Ver detalle de registro de auditoría

| UC–087 | Ver detalle de registro de auditoría | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–07 Registro de Auditoría | |
| **Requisitos asociados** | IRQ–10 Información de Auditoría | |
| **Descripción** | El administrador visualiza el detalle completo de un registro de auditoría específico, incluyendo navegación a la entidad afectada. | |
| **Precondición** | Se está consultando el historial de auditoría (dentro de CU-085). | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | Se ejecuta el caso de uso CU-085 Consultar historial de auditoría. |
| | 2 | El administrador hace clic en un registro para ver su detalle. |
| | 3 | El sistema muestra toda la información del registro: |
| | 3a | Fecha y hora exacta. |
| | 3b | Usuario (ID y email). |
| | 3c | Acción realizada (descripción completa). |
| | 3d | Tipo de entidad afectada. |
| | 3e | ID de la entidad afectada. |
| | 4 | El sistema ofrece enlace para navegar a la entidad afectada (si existe). |
| | 5 | El administrador puede hacer clic en el enlace para ver la entidad. |
| **Postcondición** | El administrador visualiza el detalle del registro de auditoría. | |
| **Excepciones** | **Paso** | **Acción** |
| | 4a | Si la entidad fue eliminada, el sistema informa que no está disponible. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 3 | 500ms |
| **Frecuencia** | Ocasional | |
| **Estabilidad** | Media | |
| **Comentarios** | Incluye el caso de uso CU-085 Consultar historial de auditoría. | |

---

### CU-088 - Registrar todas las acciones para auditoría

| UC–088 | Registrar todas las acciones para auditoría | |
| :---- | :---- | :---- |
| **Objetivos asociados** | OBJ–07 Registro de Auditoría | |
| **Requisitos asociados** | IRQ–10 Información de Auditoría | |
| **Descripción** | El sistema almacena automáticamente todas las acciones relevantes de los usuarios en un historial para fines de auditoría, incluyendo creación, modificación, activación y desactivación de entidades. | |
| **Precondición** | Un usuario está realizando una acción en el sistema. | |
| **Secuencia normal** | **Paso** | **Acción** |
| | 1 | El usuario ejecuta una acción (crear, modificar, desactivar, reactivar, login, logout). |
| | 2 | El sistema captura el ID y email del usuario actual. |
| | 3 | El sistema determina el tipo de acción y la entidad afectada. |
| | 4 | El sistema crea un registro de auditoría con: |
| | 4a | UserId: ID del usuario. |
| | 4b | UserEmail: correo del usuario. |
| | 4c | Action: descripción de la acción. |
| | 4d | TargetId: ID de la entidad afectada. |
| | 4e | TargetType: tipo de entidad. |
| | 4f | Timestamp: fecha y hora actual. |
| | 5 | El sistema almacena el registro en la colección de auditoría. |
| **Postcondición** | La acción ha sido registrada en el historial de auditoría. | |
| **Excepciones** | **Paso** | **Acción** |
| | 5a | Si hay un error al guardar, se registra en logs pero no se interrumpe la operación principal. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
| | 5 | < 500ms (asíncrono) |
| **Frecuencia** | Muy frecuente | |
| **Estabilidad** | Alta | |
| **Comentarios** | El registro de auditoría no debe afectar el rendimiento de las operaciones principales. | |

---

### Módulo: Gestión de Roles

#### CU-089 - Crear rol

| UC–089 | Crear rol |  |
| :--- | :--- | :--- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El Administrador da de alta un nuevo rol dentro del sistema a través de la interfaz web, especificando denominación, descripción funcional y seleccionando mediante casillas de verificación los permisos asignados. |  |
| **Actor/es** | Administrador |  |
| **Precondición** | El usuario debe haber iniciado sesión con el rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El Administrador hace clic sobre la opción **"Roles y Permisos"** en la barra lateral de navegación principal. |
|  | 2 | El sistema renderiza la pantalla de gestión de roles (CU-091) exhibiendo en la cabecera superior el botón **"+ Nuevo Rol"**. |
|  | 3 | El Administrador presiona el botón **"+ Nuevo Rol"**. |
|  | 4 | El sistema despliega la ventana modal **"Crear Nuevo Rol"**, presentando el campo de texto `Nombre del Rol`, el área de texto `Descripción`, una grilla interactiva con los permisos organizados por módulo (Clientes, Vehículos, Servicios, Lavados, Turnos, Configuración, Auditoría, Reportes) con casillas de verificación individuales, el botón **"Seleccionar todos"**, y los botones de acción **"Guardar"** y **"Cancelar"**. |
|  | 5 | El Administrador escribe el nombre en el campo `Nombre del Rol`, añade una descripción en el área `Descripción` y marca los permisos deseados mediante los checkboxes correspondientes. |
|  | 6 | El Administrador presiona el botón **"Guardar"**. |
|  | 7 | El sistema valida en el cliente y servidor que el campo `Nombre del Rol` no esté vacío, que su valor sea único en el lavadero (insensible a mayúsculas) y que se haya marcado al menos un permiso de la lista. |
|  | 8 | El sistema registra el nuevo documento `Rol` en la base de datos Firestore con estado inicial "Activo". |
|  | 9 | El sistema genera de forma asíncrona un registro en `AuditLog` con la acción de alta de rol. |
|  | 10 | El sistema cierra la ventana modal, actualiza la grilla de roles incorporando la nueva fila y muestra una notificación flotante (Toast) de color verde: *"Rol creado exitosamente"*. |
| **Postcondición** | El nuevo rol queda registrado y habilitado para ser asignado a empleados en el formulario de edición de personal. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 7a | Si el campo `Nombre del Rol` está vacío o no se seleccionó ningún permiso, el sistema resalta los campos con borde rojo, muestra mensajes de validación inline (*"El nombre es obligatorio"*, *"Debe asignar al menos un permiso"*) y cancela el guardado. |
|  | 7b | Si el nombre del rol ya existe en el sistema, el sistema presenta un aviso de alerta en la parte superior del modal: *"Ya existe un rol registrado con esa denominación"* y mantiene los datos cargados para su edición. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7-10 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Permite definir perfiles de acceso a medida sin necesidad de cambios en el código fuente del sistema. |  |

---

#### CU-090 - Modificar rol

| UC–090 | Modificar rol |  |
| :--- | :--- | :--- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El Administrador actualiza la denominación, descripción o el conjunto de permisos operativos asociados a un rol previamente registrado a través de un formulario modal de edición. |  |
| **Actor/es** | Administrador |  |
| **Precondición** | El usuario debe tener rol de Administrador y el rol a editar debe existir en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta la consulta de roles (CU-091). |
|  | 2 | En la grilla de resultados, el Administrador ubica el rol y presiona el botón con icono de lápiz **"Editar"** ubicado en la columna de `Acciones` de la fila correspondiente. |
|  | 3 | El sistema abre la ventana modal **"Modificar Rol"**, precargando el `Nombre del Rol`, la `Descripción` y marcando los checkboxes correspondientes a la matriz de permisos actualmente asignada. Si se trata de un rol base del sistema ("Administrador" o "Empleado"), el campo `Nombre del Rol` se presenta deshabilitado junto a un badge *"Rol Protegido del Sistema"*. |
|  | 4 | El Administrador ajusta los campos de texto o marca/desmarca las casillas de verificación de los permisos. |
|  | 5 | El Administrador presiona el botón **"Guardar Cambios"**. |
|  | 6 | El sistema valida la consistencia de los datos ingresados, verificando que no se duplique el nombre con otro rol existente y que no se remuevan permisos esenciales si el rol es protegido. |
|  | 7 | El sistema actualiza el documento de `Rol` en la base de datos Firestore. |
|  | 8 | El sistema genera un registro en `AuditLog` guardando la traza de auditoría con los campos y permisos modificados. |
|  | 9 | El sistema cierra la ventana modal, refresca la grilla de roles con los datos actualizados y muestra una notificación Toast de color verde: *"Rol actualizado exitosamente"*. |
| **Postcondición** | El rol queda actualizado, impactando de forma inmediata en las facultades de los empleados asociados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 6a | Si se intenta desmarcar un permiso crítico en un rol base protegido (como revocar el acceso a roles en el rol Administrador), el sistema bloquea la acción, revierte la casilla y muestra un banner de advertencia: *"No es posible remover permisos críticos de un rol base protegido"*. |
|  | 6b | Si se desmarcan todos los permisos del rol, el sistema alerta: *"El rol debe conservar al menos un permiso asignado"* y cancela la operación. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 7-9 | 1 segundo |
| **Frecuencia** | Ocasional |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Incluye el caso de uso CU-091 Consultar roles para la selección inicial del registro. |  |

---

#### CU-091 - Consultar roles

| UC–091 | Consultar roles |  |
| :--- | :--- | :--- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El Administrador accede al panel principal de roles para visualizar la lista estructurada de perfiles de seguridad, filtrando por estado, ordenando por columnas y navegando mediante paginación. |  |
| **Actor/es** | Administrador |  |
| **Precondición** | El usuario debe haber iniciado sesión con el rol de Administrador. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | El Administrador presiona la opción **"Roles y Permisos"** en el menú de navegación lateral. |
|  | 2 | El sistema recupera los roles registrados en Firestore y computa en memoria la cantidad de empleados activos asociados a cada uno. |
|  | 3 | El sistema despliega la pantalla principal presentando: barra de búsqueda interactiva con el campo `Buscar rol...`, selector desplegable `Estado` (con opciones "Todos", "Activo", "Inactivo"), selector `Ordenar por` y una tabla de datos (DataGrid) con las columnas: `Nombre`, `Descripción`, `Permisos` (badge numérico con tooltip listando accesos), `Usuarios Asignados` (contador), `Estado` (badge verde/gris) y `Acciones` (botones "Editar" y "Eliminar"). |
|  | 4 | El Administrador ingresa texto en el campo `Buscar rol...` o selecciona una opción del desplegable de estado. |
|  | 5 | El sistema filtra las filas de la tabla de manera reactiva en pantalla a medida que se ingresan los criterios. |
|  | 6 | El Administrador navega entre las páginas mediante los controles de paginación inferiores (**"Anterior"**, número de página, **"Siguiente"**). |
| **Postcondición** | El Administrador visualiza el padrón de roles conforme a los filtros y ordenamientos aplicados. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 2a / 5a | Si no existen roles cargados o el criterio de búsqueda no arroja coincidencias, el sistema oculta la paginación y renderiza en la tabla una fila informativa: *"No se encontraron roles registrados que coincidan con la búsqueda"*. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 2-3 | 1 segundo |
| **Frecuencia** | Frecuente |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Permite visualizar tanto los roles predeterminados del sistema como los creados de forma personalizada. |  |

---

#### CU-092 - Eliminar rol

| UC–092 | Eliminar rol |  |
| :--- | :--- | :--- |
| **Objetivos asociados** | OBJ–09 Gestión de Seguridad |  |
| **Requisitos asociados** | IRQ–01 Información sobre Empleados |  |
| **Descripción** | El Administrador elimina un rol de seguridad del sistema desde la grilla principal, tras verificar que no se trate de un rol base reservado y que no posea personal asignado actualmente. |  |
| **Actor/es** | Administrador |  |
| **Precondición** | El usuario debe ser Administrador y el rol debe estar registrado en el sistema. |  |
| **Secuencia normal** | **Paso** | **Acción** |
|  | 1 | Se ejecuta el caso de uso CU-091 Consultar roles. |
|  | 2 | En la tabla de roles, el Administrador localiza el rol y hace clic en el botón con icono de papelera roja **"Eliminar"** en la columna `Acciones`. |
|  | 3 | El sistema verifica de forma preventiva que el rol no corresponda a un rol protegido del sistema ("Administrador" o "Empleado") y que el contador de usuarios asociados sea igual a cero. |
|  | 4 | El sistema abre la ventana modal de confirmación **"Confirmar Eliminación"**, mostrando el texto: *"¿Está seguro de que desea eliminar el rol [Nombre del Rol]? Esta operación es irreversible."*, junto a los botones **"Cancelar"** y **"Confirmar Eliminación"**. |
|  | 5 | El Administrador presiona el botón **"Confirmar Eliminación"**. |
|  | 6 | El sistema elimina físicamente el documento del rol en la base de datos Firestore. |
|  | 7 | El sistema registra el evento en `AuditLog` asentando el identificador del rol eliminado y el administrador responsable. |
|  | 8 | El sistema cierra la ventana modal, remueve la fila de la tabla mediante una animación de salida y emite una notificación Toast de color verde: *"Rol eliminado exitosamente"*. |
| **Postcondición** | El rol queda eliminado de la base de datos y deja de estar disponible para su selección en el sistema. |  |
| **Excepciones** | **Paso** | **Acción** |
|  | 3a | Si el rol seleccionado es un rol base inmutable del sistema, el botón "Eliminar" se presenta deshabilitado con un tooltip explicativo; si se fuerza la acción, el sistema muestra un modal de bloqueo: *"Acción no permitida: Los roles predeterminados del sistema no pueden ser eliminados"*. |
|  | 3b | Si el rol cuenta con uno o más empleados asignados (`Usuarios Asignados > 0`), el sistema bloquea el guardado y abre una alerta modal informativa: *"No es posible eliminar el rol: Existen [X] empleados que tienen este rol asignado. Reasígnelos a otro rol antes de continuar"*. |
| **Rendimiento** | **Paso** | **Cota de tiempo** |
|  | 6-8 | 1 segundo |
| **Frecuencia** | Baja |  |
| **Estabilidad** | Alta |  |
| **Comentarios** | Incluye el caso de uso CU-091 Consultar roles. |  |
