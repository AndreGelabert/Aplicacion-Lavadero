# Pruebas de navegador con Playwright

## Preparación

Se necesitan Node.js y pnpm 12, acceso a la descarga de Chromium y las dependencias de sistema de Chromium para la plataforma. En un clon nuevo, desde la raíz:

```sh
pnpm install --frozen-lockfile
pnpm test:e2e:install
pnpm test:e2e
```

La descarga instala Chromium solo en `.local/playwright-browsers`, no en la caché global. En Linux pueden requerirse dependencias del sistema instaladas por el administrador. Los scripts también funcionan en Windows. La instalación de paquetes y navegadores requiere red; ejecutar las pruebas no la requiere.

## Comandos

- `pnpm test:e2e`: Chromium headless, un worker y cero reintentos.
- `pnpm test:e2e:headed`: navegador visible.
- `pnpm test:e2e:ui`: interfaz interactiva de Playwright.
- `pnpm test:e2e:report`: abrir el último informe HTML.
- `pnpm test:e2e:install`: instalar Chromium localmente tras instalar las dependencias.

Se puede pasar opciones al CLI después de `--`. La configuración fija el origen `http://127.0.0.1:41783`: el servidor de fixture se inicia por cada ejecución y se rechaza un puerto ocupado, sin reutilizar otro servidor. No hay opción de destino remoto. Para añadir pruebas, colocar archivos `*.spec.cjs` en `tests/browser` y mantener los datos sintéticos.

## Alcance y seguridad

Las pruebas cargan el archivo real `wwwroot/js/auditoria.js` en Chromium, con un HTML de prueba que reproduce selectores del diálogo Razor. Las respuestas de detalle son simuladas y el servidor local entrega respuestas sintéticas de tabla. No ejecutan Razor, aplicación ASP.NET, autenticación, Firebase ni persistencia; no prueban integración autenticada. No usar credenciales ni datos reales en fixtures. Las solicitudes del navegador fuera del origen de fixture se bloquean en estas pruebas.

Las capturas y trazas se conservan solo ante fallos en `test-results/`; el informe HTML vive en `playwright-report/`. Estos archivos y `playwright/.auth/` están ignorados, pero pueden contener datos de pruebas: nunca introducir secretos. Una futura prueba HTTP real necesita un entorno desechable aislado y un diseño explícito de autenticación; no se conecta silenciosamente a emuladores persistentes ni a producción.
