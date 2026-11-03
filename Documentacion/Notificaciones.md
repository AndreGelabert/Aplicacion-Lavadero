# Notificaciones y peticiones reutilizables

Todas las vistas con operaciones usan el componente global para avisos generales. La validación de campos sigue junto a los campos y las confirmaciones siguen siendo diálogos. El menú lateral y las reglas de permisos continúan siendo responsabilidad del servidor.

## Mostrar un aviso

```js
Notificaciones.exito('Operación completada.');
Notificaciones.error('No se pudo completar la operación.');
Notificaciones.aviso('Revisá los datos antes de continuar.');
Notificaciones.info('Información disponible.');
const cerrar = Notificaciones.cargando('Procesando…');
// Al terminar, incluso ante un error:
cerrar();
```

`mostrar(mensaje, tipo)` admite `success`, `error`, `warning`, `info` y `loading`. Ignora texto vacío y usa información para tipos desconocidos. Muestra texto literal, no HTML. Devuelve una función de cierre.

Los mensajes se apilan arriba a la derecha, fuera de tablas y contenedores de vistas; se ajustan en móvil. Éxito e información duran cinco segundos, pausables con puntero o foco. Errores y advertencias permanecen hasta cerrarlos. El estado en curso dura hasta que lo cierra quien inició la operación. No se mueve el foco ni se bloquea la página.

## Peticiones comunes

El layout y Login (que no utiliza el layout) cargan notificaciones y luego `wwwroot/js/requests.js` antes de los módulos específicos.

| Interfaz | Responsabilidad |
| --- | --- |
| `Peticiones.enviar(url, opciones)` | Transporte compatible con fetch; devuelve Response sin consumir. Agrega cabecera AJAX solo al mismo origen. Conserva método, cuerpo, tokens, señal y opciones; no reintenta mutaciones. |
| `Peticiones.json` / `texto` | Verifican HTTP y leen contenido; rechazan si falla. |
| `Peticiones.resultado` | Devuelve `{ response, data }` para respuestas JSON con errores de negocio. |
| `Peticiones.mensajeError(error, alternativo)` | Mensajes seguros para 401/403 o fallback, sin exponer excepciones. |
| `Peticiones.filtrar(clave, aplicar)` | Aviso en curso, espera actualización de contenido, luego éxito o fallo; cancela avisos obsoletos. |

El consumidor decide qué significa la respuesta de negocio. Los servicios no conocen la UI. Los errores de permisos no se convierten en éxito ni se reemplazan por redirecciones a una vista permitida.

## Filtros

Auditoría, Clientes, Vehículos, Personal, Servicios, Paquetes, Lavados y Roles usan el ciclo común: «Aplicando filtros…» → «Filtros aplicados.» o «No se pudieron aplicar los filtros.». La búsqueda y la limpieza de filtros reutilizan la carga de tabla. Las cargas parciales que mantienen filtros también pueden mostrar ese ciclo.

La función `aplicar` debe rechazar ante errores y resolver después de actualizar el DOM. Una respuesta HTTP fallida no se inserta como tabla. Las respuestas antiguas no deben sobrescribir una más nueva. No hay filtros en Login, Configuración, revisión de catálogo ni detalle de lavado; no se agregaron filtros a esas pantallas.

## Mensajes tras recargas

`Views/Shared/_NotificationMessages.cshtml` consume una vez las claves de presentación de TempData: `Success`, `SuccessMessage`, `ReviewMessage`, `Error`, `ErrorMessage`, `ReviewError`, `Warning` e `Info`. No consume eventos de auditoría/analítica. Razor codifica el texto y JavaScript lo muestra literalmente. No se necesita un controlador exclusivo para avisos.

```csharp
TempData["Success"] = "Operación completada";
return RedirectToAction("Index");
```

Roles conserva la recarga completa al crear/editar. El controlador deja el aviso en TempData y la siguiente página actualiza el menú con los permisos vigentes. Si el usuario se quitó el acceso, el servidor mantiene acceso denegado. Las recargas de operaciones de detalle de lavado también se conservan.

## Límites y verificación

El aviso especializado de expiración y el cierre de sesión entre pestañas conservan su lógica de seguridad; no se reemplaza su mecanismo por el componente de resultados de operaciones. No se agregaron mensajes espontáneos del servidor ni infraestructura de tiempo real.

Ejecutar `npm run test:e2e` y `node --test Firebase.Tests/*.test.cjs`. Las pruebas de navegador usan Chromium y respuestas simuladas: no autentican usuarios ni modifican Firebase. La prueba de refresco de navegación verifica la recarga; las reglas de acceso siguen cubiertas por las pruebas del servidor. Compilar con `dotnet build Firebase.csproj --no-restore`.
