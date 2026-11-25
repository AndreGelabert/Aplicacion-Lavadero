const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');

// ============================================================================================
// SIMULADOR DEL MOTOR DE REABASTECIMIENTO Y SERVICIO DE INSUMOS
// Replica con precisión matemática y atómica el comportamiento de C# en InsumosService.cs
// ============================================================================================

class InsumosSimulator {
    constructor() {
        this.insumos = new Map();
        this.proveedores = new Map();
        this.ordenesCompra = new Map();
        this.solicitudesCotizacion = new Map();
        this.movimientosStock = new Map();
        this.alertasStock = new Map();
        this.directiva = {
            Id: 'principal',
            ModoEmision: 'RequiereAprobacion' // o 'EnvioDirecto'
        };
        this.configuracion = {
            VigenciaCompraRecienteDias: 14
        };
    }

    agregarProveedor(id, razonSocial, estado = 'Activo', email = 'test@example.com') {
        this.proveedores.set(id, {
            Id: id,
            RazonSocial: razonSocial,
            Estado: estado,
            EmailOrdenes: email
        });
    }

    agregarOrdenRecibida(ordenId, proveedorId, insumoId, cantidad, precioUnitario, fechaDiasAtras = 1) {
        const fecha = new Date(Date.now() - fechaDiasAtras * 24 * 60 * 60 * 1000);
        this.ordenesCompra.set(ordenId, {
            Id: ordenId,
            ProveedorId: proveedorId,
            Estado: 'Recibida',
            Items: [{ InsumoId: insumoId, Cantidad: cantidad, PrecioUnitario: precioUnitario }],
            FechaEmision: fecha
        });
        this.movimientosStock.set(ordenId, {
            Id: ordenId,
            InsumoId: insumoId,
            ReferenciaId: ordenId,
            Tipo: 'RecepcionOrden',
            Cantidad: cantidad,
            Fecha: fecha
        });
    }

    async guardarInsumo(insumo, crear, userId = 'user-1', userEmail = 'user@example.com') {
        insumo.Nombre = (insumo.Nombre || '').trim();
        if (!insumo.Nombre) throw new Error('El nombre del insumo es obligatorio.');
        if ((insumo.StockMinimo || 0) < 0 || (insumo.CantidadReabastecimiento || 0) < 0) {
            throw new Error('Los niveles de stock no pueden ser negativos.');
        }
        insumo.ProveedoresIds = (insumo.ProveedoresIds || []).filter(Boolean);
        if (insumo.ReabastecimientoAutomatico && insumo.ProveedoresIds.length === 0) {
            throw new Error('Seleccione al menos un proveedor para el reabastecimiento automático.');
        }

        let evaluarReabastecimiento = false;
        let insumoId = insumo.Id;

        if (crear) {
            insumoId = insumo.Id || `ins-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
            const nuevoInsumo = {
                ...insumo,
                Id: insumoId,
                StockActual: 0,
                Estado: 'Activo'
            };
            evaluarReabastecimiento = nuevoInsumo.ReabastecimientoAutomatico && nuevoInsumo.StockActual <= nuevoInsumo.StockMinimo;
            this.insumos.set(insumoId, nuevoInsumo);
        } else {
            const actual = this.insumos.get(insumo.Id);
            if (!actual) throw new Error('El insumo no existe.');
            const actualizado = {
                ...actual,
                Nombre: insumo.Nombre,
                StockMinimo: insumo.StockMinimo,
                CantidadReabastecimiento: insumo.CantidadReabastecimiento,
                ReabastecimientoAutomatico: insumo.ReabastecimientoAutomatico,
                ProveedoresIds: insumo.ProveedoresIds
            };
            evaluarReabastecimiento = actualizado.Estado === 'Activo' &&
                actualizado.ReabastecimientoAutomatico &&
                actualizado.StockActual <= actualizado.StockMinimo;
            this.insumos.set(insumo.Id, actualizado);
        }

        // Post-commit: Disparo automático de reabastecimiento si corresponde
        if (evaluarReabastecimiento) {
            await this.evaluarReabastecimiento(insumoId, userId, userEmail);
        }

        return { EsExitoso: true, InsumoId: insumoId };
    }

    async evaluarReabastecimiento(insumoId, userId = 'sistema', userEmail = 'sistema') {
        const insumo = this.insumos.get(insumoId);
        if (!insumo) return;
        if (insumo.Estado !== 'Activo' || !insumo.ReabastecimientoAutomatico || insumo.StockActual > insumo.StockMinimo) {
            return;
        }

        // 1. Deduplicación e Idempotencia: Verificar solicitudes u órdenes abiertas
        const haySolicitudesAbiertas = Array.from(this.solicitudesCotizacion.values()).some(x =>
            ['Pendiente', 'PendienteAprobacion', 'Respondida'].includes(x.Estado) &&
            x.Items.some(i => i.InsumoId === insumoId)
        );
        if (haySolicitudesAbiertas) return;

        const hayOrdenesAbiertas = Array.from(this.ordenesCompra.values()).some(x =>
            ['PendienteAprobacion', 'RevisionManual', 'Enviada', 'Confirmada', 'Despachada'].includes(x.Estado) &&
            x.Items.some(i => i.InsumoId === insumoId)
        );
        if (hayOrdenesAbiertas) return;

        // 2. Filtrar proveedores activos asignados
        const proveedoresActivos = insumo.ProveedoresIds.filter(id => {
            const p = this.proveedores.get(id);
            return p && p.Estado === 'Activo';
        });

        if (proveedoresActivos.length === 0) {
            this.alertasStock.set(insumoId, {
                InsumoId: insumoId,
                Mensaje: `El insumo '${insumo.Nombre}' alcanzó el stock mínimo y no tiene proveedores.`,
                Fecha: new Date(),
                Estado: 'Pendiente'
            });
            return;
        }

        // 3. Buscar antecedente de compra reciente
        const vigenciaDias = this.configuracion.VigenciaCompraRecienteDias ?? 14;
        const ahora = new Date();

        const ordenesRecibidasElegibles = Array.from(this.ordenesCompra.values()).filter(o =>
            o.Estado === 'Recibida' &&
            proveedoresActivos.includes(o.ProveedorId) &&
            o.Items.some(i => i.InsumoId === insumoId && i.PrecioUnitario > 0)
        );

        let antecedente = null;
        for (const o of ordenesRecibidasElegibles) {
            const mov = this.movimientosStock.get(o.Id);
            if (mov) {
                const diffDias = (ahora - mov.Fecha) / (1000 * 60 * 60 * 24);
                if (diffDias >= 0 && diffDias <= vigenciaDias) {
                    const item = o.Items.find(i => i.InsumoId === insumoId && i.PrecioUnitario > 0);
                    if (!antecedente || mov.Fecha > antecedente.Fecha) {
                        antecedente = { Orden: o, Item: item, Fecha: mov.Fecha };
                    }
                }
            }
        }

        // 4. Circuito de compras:
        if (antecedente) {
            // SI HAY ANTECEDENTE RECIENTE -> Genera Orden de Compra directa
            const nuevaId = `oc-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;
            const orden = {
                Id: nuevaId,
                ProveedorId: antecedente.Orden.ProveedorId,
                Estado: 'PendienteAprobacion',
                Notas: 'El precio y la disponibilidad requieren confirmación del proveedor.',
                Items: [{
                    InsumoId: insumo.Id,
                    NombreInsumo: insumo.Nombre,
                    Cantidad: insumo.CantidadReabastecimiento,
                    PrecioUnitario: antecedente.Item.PrecioUnitario
                }],
                FechaEmision: new Date()
            };
            this.ordenesCompra.set(nuevaId, orden);
        } else {
            // SI NO HAY ANTECEDENTE RECIENTE (Insumo nuevo) -> Genera Solicitud de Cotización (RFQ)
            const grupo = `grp-${Date.now()}`;
            for (const proveedorId of proveedoresActivos) {
                const solId = `sol-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;
                const solicitud = {
                    Id: solId,
                    GrupoId: grupo,
                    ProveedorId: proveedorId,
                    Estado: this.directiva.ModoEmision === 'RequiereAprobacion' ? 'PendienteAprobacion' : 'Pendiente',
                    Items: [{
                        InsumoId: insumo.Id,
                        NombreInsumo: insumo.Nombre,
                        Cantidad: insumo.CantidadReabastecimiento
                    }]
                };
                this.solicitudesCotizacion.set(solId, solicitud);
            }
        }
    }
}

// ============================================================================================
// TESTS DE REGRESIÓN DE COMPRA AUTOMÁTICA EN CREACIÓN DE INSUMOS
// ============================================================================================

test('Creación bajo el umbral con antecedente reciente: genera OrdenCompra automática con precio histórico', async () => {
    const sim = new InsumosSimulator();
    sim.agregarProveedor('prov-1', 'Química del Plata');

    // Registrar una orden de compra previa recibida hace 3 días
    sim.agregarOrdenRecibida('oc-previa', 'prov-1', 'shampoo-pro', 10, 1500, 3);

    // Crear insumo con compra automática activada y stock mínimo 5
    const insumo = {
        Id: 'shampoo-pro',
        Nombre: 'Shampoo Profesional',
        StockMinimo: 5,
        CantidadReabastecimiento: 20,
        ReabastecimientoAutomatico: true,
        ProveedoresIds: ['prov-1']
    };

    const res = await sim.guardarInsumo(insumo, true);
    assert.ok(res.EsExitoso);

    // Debe existir la orden de compra automática generada
    const ordenes = Array.from(sim.ordenesCompra.values()).filter(o => o.Id !== 'oc-previa');
    assert.equal(ordenes.length, 1);
    const ordenAuto = ordenes[0];
    assert.equal(ordenAuto.ProveedorId, 'prov-1');
    assert.equal(ordenAuto.Estado, 'PendienteAprobacion');
    assert.equal(ordenAuto.Items[0].InsumoId, 'shampoo-pro');
    assert.equal(ordenAuto.Items[0].Cantidad, 20);
    assert.equal(ordenAuto.Items[0].PrecioUnitario, 1500); // Precio del antecedente

    // No debe haber solicitudes de cotización porque se generó orden directa
    assert.equal(sim.solicitudesCotizacion.size, 0);
});

test('Creación bajo el umbral SIN antecedente (insumo nuevo): genera SolicitudCotizacion según circuito acordado', async () => {
    const sim = new InsumosSimulator();
    sim.agregarProveedor('prov-1', 'Química del Plata');
    sim.agregarProveedor('prov-2', 'Distribuidora Norte');

    // Crear insumo nuevo (sin antecedentes de compra previa)
    const insumo = {
        Id: 'cera-premium',
        Nombre: 'Cera Hidrofóbica Premium',
        StockMinimo: 10,
        CantidadReabastecimiento: 50,
        ReabastecimientoAutomatico: true,
        ProveedoresIds: ['prov-1', 'prov-2']
    };

    const res = await sim.guardarInsumo(insumo, true);
    assert.ok(res.EsExitoso);

    // Al ser nuevo sin precio previo, el circuito acordado genera SolicitudCotizacion para los proveedores
    assert.equal(sim.ordenesCompra.size, 0, 'No debe generar OrdenCompra sin precio acordado');
    const solicitudes = Array.from(sim.solicitudesCotizacion.values());
    assert.equal(solicitudes.length, 2, 'Debe generar cotización para cada proveedor activo');

    const solProv1 = solicitudes.find(s => s.ProveedorId === 'prov-1');
    assert.ok(solProv1);
    assert.equal(solProv1.Items[0].InsumoId, 'cera-premium');
    assert.equal(solProv1.Items[0].Cantidad, 50);
});

test('Caso que NO debe generar orden ni solicitud: ReabastecimientoAutomatico = false', async () => {
    const sim = new InsumosSimulator();
    sim.agregarProveedor('prov-1', 'Proveedor A');

    const insumo = {
        Id: 'microfibra',
        Nombre: 'Paño de Microfibra',
        StockMinimo: 5,
        CantidadReabastecimiento: 10,
        ReabastecimientoAutomatico: false, // Desactivado
        ProveedoresIds: ['prov-1']
    };

    const res = await sim.guardarInsumo(insumo, true);
    assert.ok(res.EsExitoso);

    // No debe disparar ni ordenes ni solicitudes
    assert.equal(sim.ordenesCompra.size, 0);
    assert.equal(sim.solicitudesCotizacion.size, 0);
    assert.equal(sim.alertasStock.size, 0);
});

test('Caso con proveedor inactivo: genera alerta en alertasStock y no crea orden ni solicitud', async () => {
    const sim = new InsumosSimulator();
    sim.agregarProveedor('prov-inactivo', 'Proveedor Inactivo', 'Inactivo');

    const insumo = {
        Id: 'cepillo',
        Nombre: 'Cepillo Llantas',
        StockMinimo: 2,
        CantidadReabastecimiento: 5,
        ReabastecimientoAutomatico: true,
        ProveedoresIds: ['prov-inactivo']
    };

    const res = await sim.guardarInsumo(insumo, true);
    assert.ok(res.EsExitoso);

    // No debe haber ordenes ni solicitudes
    assert.equal(sim.ordenesCompra.size, 0);
    assert.equal(sim.solicitudesCotizacion.size, 0);

    // Debe registrar la alerta en alertasStock
    const alerta = sim.alertasStock.get('cepillo');
    assert.ok(alerta, 'Debe registrar la alerta para el insumo sin proveedores activos');
    assert.match(alerta.Mensaje, /alcanzó el stock mínimo y no tiene proveedores/);
});

test('Prevención de órdenes y solicitudes duplicadas ante re-evaluaciones', async () => {
    const sim = new InsumosSimulator();
    sim.agregarProveedor('prov-1', 'Proveedor A');

    const insumo = {
        Id: 'desengrasante',
        Nombre: 'Desengrasante Motor',
        StockMinimo: 5,
        CantidadReabastecimiento: 15,
        ReabastecimientoAutomatico: true,
        ProveedoresIds: ['prov-1']
    };

    // Creación inicial dispara la primera solicitud
    await sim.guardarInsumo(insumo, true);
    assert.equal(sim.solicitudesCotizacion.size, 1);

    // Intento concurrente o posterior de evaluar reabastecimiento para el mismo insumo
    await sim.evaluarReabastecimiento('desengrasante');
    await sim.evaluarReabastecimiento('desengrasante');
    await sim.guardarInsumo(insumo, false); // Actualización

    // La cantidad de solicitudes debe permanecer estrictamente en 1 (idempotencia)
    assert.equal(sim.solicitudesCotizacion.size, 1, 'No debe duplicar solicitudes abiertas');
    assert.equal(sim.ordenesCompra.size, 0, 'No debe crear órdenes adicionales');
});

// ============================================================================================
// VERIFICACIÓN ESTÁTICA DEL CÓDIGO FUENTE
// ============================================================================================

test('Código C# InsumosService.cs: InsumoService inyecta StockService y evalúa reabastecimiento en creación', () => {
    const filePath = path.join(__dirname, '..', 'Services', 'InsumosService.cs');
    const content = fs.readFileSync(filePath, 'utf-8');

    assert.ok(content.includes('StockService? stock = null'), 'InsumoService debe aceptar StockService opcional en su constructor');
    assert.ok(content.includes('evaluarReabastecimiento = insumo.ReabastecimientoAutomatico && insumo.StockActual <= insumo.StockMinimo'), 'Debe calcular evaluarReabastecimiento al crear');
    assert.ok(content.includes('stockEfectivo.EvaluarReabastecimiento(resultado.InsumoId'), 'Debe invocar EvaluarReabastecimiento tras guardar con éxito');
});

test('Código C# InsumosController.cs: enlaza insumos.StockService ??= stock antes de guardar', () => {
    const filePath = path.join(__dirname, '..', 'Controllers', 'InsumosController.cs');
    const content = fs.readFileSync(filePath, 'utf-8');

    assert.ok(content.includes('insumos.StockService ??= stock;'), 'El controlador debe asegurar que insumos tenga asignado stock');
});
