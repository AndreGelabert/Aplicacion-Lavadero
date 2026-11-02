const path = require('node:path');
const root = path.resolve(__dirname, '..');
process.env.PLAYWRIGHT_BROWSERS_PATH = path.join(root, '.local', 'playwright-browsers');

if (process.argv[2] === 'serve') {
  const http = require('node:http');
  const fs = require('node:fs');
  const fixture = `<!doctype html><html lang="es"><head><meta charset="utf-8"><title>Fixture local de auditoría</title></head><body>
    <h1>Fixture local de auditoría (sin autenticación)</h1>
    <input id="simple-search" aria-label="Buscar">
    <form id="filterForm"><input name="pageNumber" value="1"><button>Aplicar Filtros</button></form>
    <div id="auditoria-table-container" data-current-page="1"><button type="button" data-audit-detail-id="change">Ver detalle</button></div>
    <dialog id="audit-detail-dialog" aria-labelledby="audit-detail-title">
      <h2 id="audit-detail-title">Detalle de auditoría</h2>
      <p id="audit-detail-status" role="status" aria-live="polite"></p><dl id="audit-detail-meta"></dl>
      <table><thead><tr><th>Campo</th><th>Antes</th><th>Después</th></tr></thead><tbody id="audit-detail-rows"></tbody></table>
      <button id="audit-detail-close" type="button">Cerrar</button>
    </dialog><script src="/js/auditoria.js"></script></body></html>`;
  http.createServer((req, res) => {
    const url = new URL(req.url, 'http://127.0.0.1');
    if (url.pathname === '/fixture') {
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
      return res.end(fixture);
    }
    if (url.pathname === '/js/auditoria.js') {
      res.writeHead(200, { 'Content-Type': 'application/javascript; charset=utf-8' });
      return fs.createReadStream(path.join(root, 'wwwroot', 'js', 'auditoria.js')).pipe(res);
    }
    if (url.pathname === '/Auditoria/TablePartial' || url.pathname === '/Auditoria/SearchPartial') {
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
      return res.end('<input id="current-page-value" value="2"><button type="button" data-audit-detail-id="replacement">Ver detalle reemplazado</button>');
    }
    res.writeHead(404);
    res.end('Not found');
  }).listen(41783, '127.0.0.1');
} else {
  const { spawnSync } = require('node:child_process');
  const commands = {
    test: ['test'], headed: ['test', '--headed'], ui: ['test', '--ui'],
    install: ['install', 'chromium'], report: ['show-report', path.join(root, 'playwright-report')],
  };
  const args = commands[process.argv[2]];
  if (!args) {
    console.error('Usage: node scripts/playwright.cjs test|headed|ui|install|report');
    process.exitCode = 2;
  } else {
    const result = spawnSync(process.execPath, [require.resolve('@playwright/test/cli'), ...args, ...process.argv.slice(3)], {
      cwd: root, env: process.env, stdio: 'inherit',
    });
    if (result.error) console.error(result.error.message);
    process.exitCode = result.status ?? 1;
  }
}
