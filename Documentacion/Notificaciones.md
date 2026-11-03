# Notificaciones reutilizables

Roles y permisos es la vista piloto del componente global. El layout compartido carga `wwwroot/css/notifications.css` y `wwwroot/js/notifications.js` antes de los scripts de cada página. No requiere dependencias nuevas ni cambios en servicios o controladores.

## Uso desde una vista

```js
Notificaciones.exito('Operación completada.');
Notificaciones.error('No se pudo completar la operación.');
Notificaciones.aviso('Revisá los datos antes de continuar.');
Notificaciones.info('Hay información disponible.');
```

Para respuestas AJAX, el módulo de la vista interpreta el resultado del servidor y llama al componente:

```js
Notificaciones.mostrar(data.message, data.success ? 'success' : 'error');
```

`mostrar(mensaje, tipo)` acepta `success`, `error`, `warning` e `info`. Los mensajes vacíos se ignoran y un tipo desconocido se muestra como información. El texto se muestra literalmente, nunca como HTML. Cada llamada devuelve una función que permite cerrar ese aviso desde código.

## Comportamiento

- Los avisos se apilan arriba a la derecha, fuera de los contenedores de las vistas y por encima de tablas y modales del layout. En pantallas pequeñas se ajustan al ancho disponible.
- Éxito e información se cierran después de cinco segundos; el tiempo se pausa mientras el puntero o el foco están sobre el aviso.
- Error y advertencia permanecen hasta que se cierran. Todos tienen botón de cierre accesible.
- Los errores de campos permanecen junto a sus campos. Los avisos no cambian el foco ni bloquean el resto de la página.
- Roles muestra los mensajes de creación, edición, cambio de estado y errores generales mediante este componente. Tras guardar actualiza la tabla sin recargar toda la página, para no perder el aviso.

## Peticiones comunes

El layout carga `Peticiones` después de `Notificaciones` y antes de los módulos de vista:

- `enviar(url, opciones)` conserva la interfaz de `fetch` y devuelve la respuesta sin consumirla. Agrega `X-Requested-With` únicamente para el mismo origen; no reintenta solicitudes ni altera formularios o tokens.
- `json` y `texto` verifican el estado HTTP y devuelven el contenido esperado. `resultado` devuelve `{ response, data }` para acciones JSON con validaciones de negocio.
- `mensajeError(error, mensajeAlternativo)` permite mostrar mensajes seguros de sesión/acceso o un mensaje genérico, sin revelar excepciones.
- `filtrar(clave, aplicar)` muestra «Aplicando filtros…», espera la promesa que actualiza la tabla y la reemplaza por éxito o error. La función `aplicar` debe rechazar ante fallos y resolver solamente después de actualizar el contenido. Las cancelaciones no muestran éxito ni error. La clave identifica una tabla; un resultado anterior no debe anunciar éxito cuando haya un filtro posterior.

## Alcance y verificación

Las otras vistas y el aviso de sesión por inactividad conservan su comportamiento. El transporte de mensajes mediante `TempData` y las notificaciones espontáneas del servidor no forman parte de este piloto. Para migrar otra vista, su módulo debe llamar a esta misma interfaz en lugar de construir alertas propias.

Ejecutar `npm run test:e2e`. Las pruebas de notificaciones usan Chromium con un DOM local y respuestas AJAX simuladas: no autentican usuarios ni modifican Firebase.
