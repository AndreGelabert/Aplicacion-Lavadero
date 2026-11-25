const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');

// ============================================================================================
// SIMULADOR DE LÓGICA DE SERVIDOR (LavadoService - IniciarEtapa, CancelarEtapa, FinalizarEtapa)
// Replica la máquina de estados exacta implementada en C# en Services/LavadoService.cs
// ============================================================================================

class LavadoServiceSimulator {
    constructor(lavado) {
        // Deep clone para garantizar aislamiento e inmutabilidad en caso de rollback
        this.lavado = JSON.parse(JSON.stringify(lavado));
    }

    iniciarEtapa(servicioId, etapaId) {
        // Clon para transacción atómica: si falla, no se muta el estado persistido
        const copiaLavado = JSON.parse(JSON.stringify(this.lavado));

        const servicio = copiaLavado.Servicios.find(s => s.ServicioId === servicioId);
        if (!servicio) throw new Error(`No se encontró el servicio ${servicioId}`);

        const etapa = servicio.Etapas.find(e => e.EtapaId === etapaId);
        if (!etapa) throw new Error(`No se encontró la etapa ${etapaId}`);

        if (etapa.Estado !== 'Pendiente') {
            throw new Error('La etapa ya fue iniciada o finalizada.');
        }

        // 1. Prevención de etapas simultáneas
        const etapaEnProceso = servicio.Etapas.find(e => e.Estado === 'EnProceso');
        if (etapaEnProceso && etapaEnProceso.EtapaId !== etapaId) {
            throw new Error(`No se puede iniciar esta etapa porque la etapa '${etapaEnProceso.Nombre}' está en proceso. Las etapas deben completarse en orden.`);
        }

        // 2. Orden secuencial estricto: todas las etapas precedentes deben ser terminales (Realizado o Cancelado)
        const indiceEtapa = servicio.Etapas.indexOf(etapa);
        for (let i = 0; i < indiceEtapa; i++) {
            const anterior = servicio.Etapas[i];
            if (anterior.Estado !== 'Realizado' && anterior.Estado !== 'Cancelado') {
                throw new Error(`No se puede iniciar la etapa '${etapa.Nombre}' porque la etapa anterior '${anterior.Nombre}' aún no ha finalizado ni ha sido cancelada.`);
            }
        }

        // Aplicar inicio
        etapa.Estado = 'EnProceso';
        etapa.TiempoInicio = new Date().toISOString();
        if (servicio.Estado === 'Pendiente') {
            servicio.Estado = 'EnProceso';
            servicio.TiempoInicio = new Date().toISOString();
        }

        // Commit atómico
        this.lavado = copiaLavado;
    }

    cancelarEtapa(servicioId, etapaId, motivo) {
        if (!motivo || !motivo.trim()) {
            throw new Error('El motivo de cancelación es obligatorio');
        }

        const copiaLavado = JSON.parse(JSON.stringify(this.lavado));
        const servicio = copiaLavado.Servicios.find(s => s.ServicioId === servicioId);
        if (!servicio) throw new Error(`No se encontró el servicio ${servicioId}`);

        const etapa = servicio.Etapas.find(e => e.EtapaId === etapaId);
        if (!etapa) throw new Error(`No se encontró la etapa ${etapaId}`);

        if (etapa.Estado === 'Cancelado') return;
        if (etapa.Estado === 'Realizado') {
            throw new Error('No se puede cancelar una etapa que ya fue realizada.');
        }

        etapa.Estado = 'Cancelado';
        etapa.MotivoCancelacion = motivo;
        etapa.TiempoFinalizacion = new Date().toISOString();

        // Evaluar si restan etapas pendientes o en proceso
        const pendientes = servicio.Etapas.filter(e => e.Estado === 'Pendiente' || e.Estado === 'EnProceso');
        if (pendientes.length === 0) {
            const tieneRealizadas = servicio.Etapas.some(e => e.Estado === 'Realizado');
            const tieneCanceladas = servicio.Etapas.some(e => e.Estado === 'Cancelado');

            if (!tieneRealizadas && tieneCanceladas) {
                // Todas las etapas canceladas
                servicio.Estado = 'Cancelado';
                servicio.MotivoCancelacion = 'Todas las etapas fueron canceladas';
            } else if (tieneRealizadas && tieneCanceladas) {
                servicio.Estado = 'RealizadoParcialmente';
            } else {
                servicio.Estado = 'Realizado';
            }
            servicio.TiempoFinalizacion = new Date().toISOString();
        }

        if (copiaLavado.Servicios.every(s => s.Estado === 'Cancelado')) {
            copiaLavado.Estado = 'Cancelado';
            copiaLavado.MotivoCancelacion = 'Todos los servicios fueron cancelados';
            copiaLavado.TiempoFinalizacion = new Date().toISOString();
            if (copiaLavado.Pago && copiaLavado.Pago.Estado !== 'Pagado') {
                copiaLavado.Pago.Estado = 'Cancelado';
            }
        } else {
            this._finalizarLavadoSiCorresponde(copiaLavado);
        }

        // Commit atómico
        this.lavado = copiaLavado;
    }

    finalizarEtapa(servicioId, etapaId) {
        const copiaLavado = JSON.parse(JSON.stringify(this.lavado));
        const servicio = copiaLavado.Servicios.find(s => s.ServicioId === servicioId);
        if (!servicio) throw new Error(`No se encontró el servicio ${servicioId}`);

        const etapa = servicio.Etapas.find(e => e.EtapaId === etapaId);
        if (!etapa) throw new Error(`No se encontró la etapa ${etapaId}`);

        if (etapa.Estado === 'Realizado') return;
        if (etapa.Estado !== 'EnProceso') {
            throw new Error('La etapa no está en proceso.');
        }

        etapa.Estado = 'Realizado';
        etapa.TiempoFinalizacion = new Date().toISOString();

        const pendientes = servicio.Etapas.filter(e => e.Estado === 'Pendiente' || e.Estado === 'EnProceso');
        if (pendientes.length === 0) {
            servicio.Estado = servicio.Etapas.some(e => e.Estado === 'Cancelado')
                ? 'RealizadoParcialmente'
                : 'Realizado';
            servicio.TiempoFinalizacion = new Date().toISOString();
        }

        this._finalizarLavadoSiCorresponde(copiaLavado);
        this.lavado = copiaLavado;
    }

    _finalizarLavadoSiCorresponde(lavado) {
        const pendientes = lavado.Servicios.filter(s => s.Estado === 'Pendiente' || s.Estado === 'EnProceso');
        if (pendientes.length === 0) {
            const realizados = lavado.Servicios.filter(s => s.Estado === 'Realizado' || s.Estado === 'RealizadoParcialmente');
            const cancelados = lavado.Servicios.filter(s => s.Estado === 'Cancelado');

            if (cancelados.length === lavado.Servicios.length) {
                return false;
            } else if (realizados.length > 0 && cancelados.length > 0) {
                lavado.Estado = 'RealizadoParcialmente';
            } else if (realizados.length === lavado.Servicios.length) {
                const hayParciales = lavado.Servicios.some(s => s.Estado === 'RealizadoParcialmente');
                lavado.Estado = hayParciales ? 'RealizadoParcialmente' : 'Realizado';
            }
            lavado.TiempoFinalizacion = new Date().toISOString();
            return true;
        }
        return false;
    }
}

function crearLavadoEjemplo(numEtapas = 3) {
    return {
        Id: 'lav-001',
        ClienteId: 'cli-001',
        VehiculoId: 'veh-001',
        Estado: 'EnProceso',
        Precio: 30000,
        Pago: { MontoPagado: 0, Estado: 'Pendiente' },
        Servicios: [
            {
                ServicioId: 'serv-001',
                ServicioNombre: 'Lavado Completo',
                Estado: 'EnProceso',
                Orden: 1,
                Etapas: Array.from({ length: numEtapas }, (_, i) => ({
                    EtapaId: `etapa-${i + 1}`,
                    Nombre: `Etapa ${i + 1}`,
                    Estado: 'Pendiente',
                    TiempoInicio: null,
                    TiempoFinalizacion: null,
                    MotivoCancelacion: null,
                    Receta: []
                }))
            }
        ]
    };
}

// ============================================================================================
// TESTS DE PREVENCIÓN DE ETAPAS SIMULTÁNEAS Y ORDEN SECUENCIAL
// ============================================================================================

test('Prevención de etapas simultáneas: rechazar iniciar etapa 2 cuando etapa 1 está EnProceso', () => {
    const lavado = crearLavadoEjemplo(3);
    const service = new LavadoServiceSimulator(lavado);

    // Iniciar etapa 1
    service.iniciarEtapa('serv-001', 'etapa-1');
    assert.equal(service.lavado.Servicios[0].Etapas[0].Estado, 'EnProceso');

    // Intentar iniciar etapa 2 simultáneamente
    assert.throws(
        () => service.iniciarEtapa('serv-001', 'etapa-2'),
        /está en proceso.*deben completarse en orden/
    );

    // Verificación de que la operación rechazada NO muta el estado persistido
    assert.equal(service.lavado.Servicios[0].Etapas[1].Estado, 'Pendiente');
    assert.equal(service.lavado.Servicios[0].Etapas[1].TiempoInicio, null);
    assert.equal(service.lavado.Servicios[0].Etapas[0].Estado, 'EnProceso');
});

test('Prevención de ejecución fuera de orden: rechazar iniciar etapa 3 si etapa 1 o 2 siguen Pendiente', () => {
    const lavado = crearLavadoEjemplo(3);
    const service = new LavadoServiceSimulator(lavado);

    // Intentar saltar directamente a etapa 3 cuando etapa 1 y 2 están pendientes
    assert.throws(
        () => service.iniciarEtapa('serv-001', 'etapa-3'),
        /no ha finalizado ni ha sido cancelada/
    );

    // Intentar iniciar etapa 2 cuando etapa 1 está pendiente
    assert.throws(
        () => service.iniciarEtapa('serv-001', 'etapa-2'),
        /no ha finalizado ni ha sido cancelada/
    );

    // El estado persistido no cambió
    assert.equal(service.lavado.Servicios[0].Etapas[0].Estado, 'Pendiente');
    assert.equal(service.lavado.Servicios[0].Etapas[1].Estado, 'Pendiente');
    assert.equal(service.lavado.Servicios[0].Etapas[2].Estado, 'Pendiente');
});

test('Orden secuencial con etapa cancelada: etapa 3 puede iniciar si etapa 1 fue Realizada y etapa 2 Cancelada', () => {
    const lavado = crearLavadoEjemplo(3);
    const service = new LavadoServiceSimulator(lavado);

    // Flujo: Iniciar y finalizar etapa 1
    service.iniciarEtapa('serv-001', 'etapa-1');
    service.finalizarEtapa('serv-001', 'etapa-1');
    assert.equal(service.lavado.Servicios[0].Etapas[0].Estado, 'Realizado');

    // Cancelar etapa 2 (etapa intermedia)
    service.cancelarEtapa('serv-001', 'etapa-2', 'Insumo no disponible');
    assert.equal(service.lavado.Servicios[0].Etapas[1].Estado, 'Cancelado');

    // Ahora etapa 3 DEBE poder iniciarse porque las anteriores (1 y 2) son terminales
    assert.doesNotThrow(() => service.iniciarEtapa('serv-001', 'etapa-3'));
    assert.equal(service.lavado.Servicios[0].Etapas[2].Estado, 'EnProceso');

    // Finalizar etapa 3
    service.finalizarEtapa('serv-001', 'etapa-3');
    assert.equal(service.lavado.Servicios[0].Etapas[2].Estado, 'Realizado');

    // El servicio debe quedar RealizadoParcialmente (porque etapa 2 fue cancelada y 1 y 3 realizadas)
    assert.equal(service.lavado.Servicios[0].Estado, 'RealizadoParcialmente');
    assert.ok(service.lavado.Servicios[0].TiempoFinalizacion);
    // Y el lavado también pasa a RealizadoParcialmente
    assert.equal(service.lavado.Estado, 'RealizadoParcialmente');
});

test('Cancelación de etapa intermedia cuando las demás ya terminaron recalcula servicio a RealizadoParcialmente', () => {
    // Escenario de bug reportado: E1 Realizado, E3 finalizado, E2 es cancelada
    const lavado = crearLavadoEjemplo(3);
    const service = new LavadoServiceSimulator(lavado);

    service.iniciarEtapa('serv-001', 'etapa-1');
    service.finalizarEtapa('serv-001', 'etapa-1');

    // Iniciar etapa 2 y cancelarla
    service.iniciarEtapa('serv-001', 'etapa-2');
    service.cancelarEtapa('serv-001', 'etapa-2', 'Cliente prefirió omitirla');

    // Iniciar etapa 3 y finalizarla
    service.iniciarEtapa('serv-001', 'etapa-3');
    service.finalizarEtapa('serv-001', 'etapa-3');

    // Servicio y lavado no deben quedar "trabados en proceso"
    assert.equal(service.lavado.Servicios[0].Estado, 'RealizadoParcialmente');
    assert.notEqual(service.lavado.Servicios[0].Estado, 'EnProceso');
    assert.equal(service.lavado.Estado, 'RealizadoParcialmente');
});

test('Cancelación de etapa como última operación terminal cierra el servicio', () => {
    // E1 Realizada, E2 Realizada, E3 se cancela al final
    const lavado = crearLavadoEjemplo(3);
    const service = new LavadoServiceSimulator(lavado);

    service.iniciarEtapa('serv-001', 'etapa-1');
    service.finalizarEtapa('serv-001', 'etapa-1');

    service.iniciarEtapa('serv-001', 'etapa-2');
    service.finalizarEtapa('serv-001', 'etapa-2');

    // Cancelar la última etapa (etapa 3)
    service.cancelarEtapa('serv-001', 'etapa-3', 'Falta de tiempo del cliente');

    assert.equal(service.lavado.Servicios[0].Etapas[2].Estado, 'Cancelado');
    // Al ser la última, el servicio debe cerrarse como RealizadoParcialmente
    assert.equal(service.lavado.Servicios[0].Estado, 'RealizadoParcialmente');
    assert.ok(service.lavado.Servicios[0].TiempoFinalizacion);
    assert.equal(service.lavado.Estado, 'RealizadoParcialmente');
});

test('Regla confirmada por usuario: si TODAS las etapas se cancelan, el servicio pasa a Cancelado', () => {
    const lavado = crearLavadoEjemplo(3);
    const service = new LavadoServiceSimulator(lavado);

    service.cancelarEtapa('serv-001', 'etapa-1', 'Cancelada 1');
    assert.equal(service.lavado.Servicios[0].Estado, 'EnProceso'); // Aún quedan pendientes

    service.cancelarEtapa('serv-001', 'etapa-2', 'Cancelada 2');
    assert.equal(service.lavado.Servicios[0].Estado, 'EnProceso'); // Aún queda etapa 3

    service.cancelarEtapa('serv-001', 'etapa-3', 'Cancelada 3');
    // Al ser 0 realizadas y todas canceladas:
    assert.equal(service.lavado.Servicios[0].Estado, 'Cancelado');
    assert.equal(service.lavado.Servicios[0].MotivoCancelacion, 'Todas las etapas fueron canceladas');
    assert.ok(service.lavado.Servicios[0].TiempoFinalizacion);

    // Si todos los servicios están cancelados, el lavado también queda Cancelado
    assert.equal(service.lavado.Estado, 'Cancelado');
});

test('Invariante de inmutabilidad: rechazo de cancelación de etapa ya realizada no altera datos', () => {
    const lavado = crearLavadoEjemplo(2);
    const service = new LavadoServiceSimulator(lavado);

    service.iniciarEtapa('serv-001', 'etapa-1');
    service.finalizarEtapa('serv-001', 'etapa-1');
    assert.equal(service.lavado.Servicios[0].Etapas[0].Estado, 'Realizado');

    const tiempoFinOriginal = service.lavado.Servicios[0].Etapas[0].TiempoFinalizacion;

    assert.throws(
        () => service.cancelarEtapa('serv-001', 'etapa-1', 'Quiero cancelar'),
        /No se puede cancelar una etapa que ya fue realizada/
    );

    // El estado sigue siendo Realizado sin motivo de cancelación
    assert.equal(service.lavado.Servicios[0].Etapas[0].Estado, 'Realizado');
    assert.equal(service.lavado.Servicios[0].Etapas[0].MotivoCancelacion, null);
    assert.equal(service.lavado.Servicios[0].Etapas[0].TiempoFinalizacion, tiempoFinOriginal);
});

// ============================================================================================
// VERIFICACIÓN ESTÁTICA DEL CÓDIGO FUENTE (C# y Razor)
// ============================================================================================

test('Código C# LavadoService.cs: IniciarEtapa contiene validación de etapas precedentes terminales', () => {
    const filePath = path.join(__dirname, '..', 'Services', 'LavadoService.cs');
    const content = fs.readFileSync(filePath, 'utf-8');

    assert.ok(content.includes('servicio.Etapas.IndexOf(etapa)'), 'Debe calcular el índice de la etapa actual');
    assert.ok(content.includes('anterior.Estado is not ("Realizado" or "Cancelado")'), 'Debe validar que las etapas anteriores sean Realizado o Cancelado');
    assert.ok(content.includes('etapaEnProceso != null && etapaEnProceso.EtapaId != etapaId'), 'Debe validar que no haya otra etapa en proceso');
});

test('Código C# LavadoService.cs: CancelarEtapa evalúa pendientes y asigna Cancelado o RealizadoParcialmente', () => {
    const filePath = path.join(__dirname, '..', 'Services', 'LavadoService.cs');
    const content = fs.readFileSync(filePath, 'utf-8');

    assert.ok(content.includes('if (!tieneRealizadas && tieneCanceladas)'), 'Debe contemplar todas canceladas');
    assert.ok(content.includes('servicio.Estado = "Cancelado"'), 'Debe marcar servicio como Cancelado si todas se cancelaron');
    assert.ok(content.includes('servicio.Estado = "RealizadoParcialmente"'), 'Debe marcar servicio como RealizadoParcialmente si hay mixtas');
    assert.ok(content.includes('FinalizarLavadoSiCorresponde(lavado)'), 'Debe invocar FinalizarLavadoSiCorresponde tras cancelar etapa');
});

test('Vista Razor _LavadoDetail.cshtml: renderiza badge RealizadoParcialmente y elimina botón manual redundante', () => {
    const filePath = path.join(__dirname, '..', 'Views', 'Lavados', '_LavadoDetail.cshtml');
    const content = fs.readFileSync(filePath, 'utf-8');

    assert.ok(content.includes('servicio.Estado == "RealizadoParcialmente"'), 'Debe manejar el estado RealizadoParcialmente para servicios');
    assert.ok(content.includes('Realizado Parcialmente</span>'), 'Debe mostrar texto Realizado Parcialmente');
    assert.ok(!content.includes("abrirModalFinalizar('@Model.Id', null, null, 'lavado'"), 'El botón manual de finalizar lavado no debe existir ya que la finalización es automática');
});
