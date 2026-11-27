const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const read = file => fs.readFileSync(path.join(__dirname, '..', file), 'utf8');

test('El helper ABM de catálogos de tipos solo ofrece Editar y Desactivar/Reactivar sin Eliminar físico', () => {
    const js = read('wwwroot/js/tipos-catalogo-modal.js');
    assert.doesNotMatch(js, /createActionButton\(['"]Eliminar['"]/);
    assert.match(js, /createActionButton\(['"]Editar['"]/);
    assert.match(js, /estado === ['"]Activo['"] \? ['"]Desactivar['"] : ['"]Reactivar['"]/);
});

test('Las vistas y JS inline usan desactivación lógica en lugar de borrado físico', () => {
    const servicioView = read('Views/Servicio/Index.cshtml');
    const clienteView = read('Views/Cliente/Index.cshtml');
    const vehiculoView = read('Views/Vehiculo/Index.cshtml');
    const clienteJs = read('wwwroot/js/cliente.js');

    assert.doesNotMatch(servicioView, /asp-action=["']EliminarTipo(Servicio|Vehiculo)["']/);
    assert.match(servicioView, /DesactivarTipoServicio/);
    assert.match(servicioView, /DesactivarTipoVehiculo/);
    assert.match(servicioView, /_TipoServicioModal/);
    assert.match(servicioView, /_TipoVehiculoModal/);

    assert.doesNotMatch(clienteView, /asp-action=["']EliminarTipoDocumento["']/);
    assert.match(clienteView, /_TipoDocumentoModal/);
    assert.match(clienteView, /_TipoVehiculoModal/);

    assert.match(vehiculoView, /_TipoVehiculoModal/);

    assert.doesNotMatch(clienteJs, /\/TipoDocumento\/EliminarTipoDocumento/);
    assert.doesNotMatch(clienteJs, /\/TipoDocumento\/VerificarEnUso/);
    assert.match(clienteJs, /\/TipoDocumento\/DesactivarTipoDocumento/);
});

test('Los modales parciales de TipoServicio, TipoVehiculo y TipoDocumento exponen endpoints ABM', () => {
    for (const file of [
        'Views/Shared/_TipoServicioModal.cshtml',
        'Views/Shared/_TipoVehiculoModal.cshtml',
        'Views/Shared/_TipoDocumentoModal.cshtml'
    ]) {
        const html = read(file);
        assert.match(html, /data-list-url="/);
        assert.match(html, /data-save-url="/);
        assert.match(html, /data-state-url="/);
        assert.match(html, /<tbody id="tipos/);
    }
});
