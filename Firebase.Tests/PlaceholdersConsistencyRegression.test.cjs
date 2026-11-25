const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
function read(file) {
    return fs.readFileSync(path.join(root, file), 'utf8');
}

test('Views/Insumos/Proveedores.cshtml tiene placeholders en todos sus inputs de formulario', () => {
    const view = read('Views/Insumos/Proveedores.cshtml');
    assert.match(view, /name="IdentificacionTributaria"[^>]*placeholder="20-12345678-9"/);
    assert.match(view, /name="RazonSocial"[^>]*placeholder="Ej: Distribuidora Química S\.A\."/);
    assert.match(view, /name="Contacto"[^>]*placeholder="1123456789"/);
    assert.match(view, /name="EmailOrdenes"[^>]*placeholder="ventas@proveedor\.com"/);
});

test('Views/Insumos/_InsumoForm.cshtml tiene placeholders en nombre, stock mínimo y reposición', () => {
    const view = read('Views/Insumos/_InsumoForm.cshtml');
    assert.match(view, /name="Nombre"[^>]*placeholder="Ej: Shampoo siliconado concentrado"/);
    assert.match(view, /name="StockMinimo"[^>]*placeholder="Ej: 5"/);
    assert.match(view, /name="CantidadReabastecimiento"[^>]*placeholder="Ej: 20"/);
});

test('Views/Insumos/_TiposInsumoModal.cshtml tiene placeholder en nombre de tipo', () => {
    const view = read('Views/Insumos/_TiposInsumoModal.cshtml');
    assert.match(view, /name="Nombre"[^>]*placeholder="Ej: Ceras y Selladores"/);
});

test('Views/Insumos/Index.cshtml tiene placeholders en modal de ajuste de stock', () => {
    const view = read('Views/Insumos/Index.cshtml');
    assert.match(view, /name="cantidad"[^>]*placeholder="Ej: 2 o 1"/);
    assert.match(view, /name="justificacion"[^>]*placeholder="Motivo del ajuste físico o merma"/);
});

test('Views/Insumos/Cotizaciones.cshtml y Ordenes.cshtml tienen placeholders en notas y precios', () => {
    const cotizaciones = read('Views/Insumos/Cotizaciones.cshtml');
    assert.match(cotizaciones, /name="notas"[^>]*placeholder="Observaciones para el proveedor\.\.\."/);
    assert.match(cotizaciones, /placeholder="0\.00"[^>]*name="Items\[@i\]\.PrecioUnitario"/);
    assert.match(cotizaciones, /placeholder="Ej: 3 \(días hábiles\)"[^>]*name="PlazoEntrega"/);

    const ordenes = read('Views/Insumos/Ordenes.cshtml');
    assert.match(ordenes, /name="cantidad"[^>]*placeholder="0\.00"/);
    assert.match(ordenes, /name="precio"[^>]*placeholder="0\.00"/);
    assert.match(ordenes, /name="cantidades"[^>]*placeholder="Cantidad recibida"/);
    assert.match(ordenes, /name="motivo"[^>]*placeholder="Motivo de la cancelación de orden\.\.\."/);
});

test('Views/Lavados/Index.cshtml y Detalle.cshtml tienen placeholders en cobro y justificación de desvío', () => {
    for (const file of ['Views/Lavados/Index.cshtml', 'Views/Lavados/Detalle.cshtml']) {
        const view = read(file);
        assert.match(view, /id="montoInput"[^>]*placeholder="0\.00"/, `Monto en ${file}`);
        assert.match(view, /id="notasPago"[^>]*placeholder="Observaciones o N° de comprobante\.\.\."/, `Notas en ${file}`);
        assert.match(view, /id="justificacionDesvio"[^>]*placeholder="Motivo de diferencia respecto a receta\.\.\."/, `Desvío en ${file}`);
    }
});

test('Views/Login/Index.cshtml tiene placeholders en registro de nuevo usuario', () => {
    const view = read('Views/Login/Index.cshtml');
    assert.match(view, /id="registerEmail"[^>]*placeholder="correo@ejemplo\.com"/);
    assert.match(view, /id="modalPassword"[^>]*placeholder="Mínimo 6 caracteres"/);
    assert.match(view, /id="confirmPassword"[^>]*placeholder="Repetí la contraseña"/);
});

test('Views/Configuracion/Index.cshtml tiene placeholders en parámetros numéricos y respaldo', () => {
    const view = read('Views/Configuracion/Index.cshtml');
    assert.match(view, /name="PlantillaEmail"[^>]*placeholder="Asunto de respaldo para cotizaciones"/);
    assert.match(view, /asp-for="PorcentajeAlertaStock"[^>]*placeholder="Ej: 80"/);
    assert.match(view, /asp-for="VigenciaCompraRecienteDias"[^>]*placeholder="Ej: 30"/);
    assert.match(view, /asp-for="CancelacionAnticipadaDescuento"[^>]*placeholder="Ej: 10"/);
    assert.match(view, /asp-for="CancelacionAnticipadaHorasMinimas"[^>]*placeholder="Ej: 2"/);
    assert.match(view, /asp-for="CancelacionAnticipadaValidezDias"[^>]*placeholder="Ej: 30"/);
    assert.match(view, /asp-for="PaquetesDescuentoStep"[^>]*placeholder="Ej: 5"/);
    assert.match(view, /asp-for="CapacidadMaximaConcurrente"[^>]*placeholder="Ej: 3"/);
    assert.match(view, /asp-for="SesionDuracionHoras"[^>]*placeholder="Ej: 8"/);
    assert.match(view, /asp-for="SesionInactividadMinutos"[^>]*placeholder="Ej: 30"/);
});

test('Views/PaqueteServicio/Index.cshtml tiene placeholders 0.00 en filtros de precio', () => {
    const view = read('Views/PaqueteServicio/Index.cshtml');
    assert.match(view, /id="precioMin"[^>]*placeholder="0\.00"/);
    assert.match(view, /id="precioMax"[^>]*placeholder="0\.00"/);
});
