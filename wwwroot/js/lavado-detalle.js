/**
 * ================================================
 * LAVADO-DETALLE.JS - FUNCIONALIDAD PÁGINA DETALLE LAVADO
 * ================================================
 */

(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', () => {
        setupModalHandlers();
        setupFormHandlers();
    });

    // =====================================
    // MANEJO DE MODALES
    // =====================================
    function setupModalHandlers() {
        // Manejar clicks en botones con data-modal-hide
        document.querySelectorAll('[data-modal-hide]').forEach(btn => {
            btn.addEventListener('click', function () {
                const modalId = this.getAttribute('data-modal-hide');
                cerrarModal(modalId);
            });
        });
    }

    window.abrirModalCancelar = function (lavadoId, servicioId, etapaId, tipo) {
        document.getElementById('cancelarLavadoId').value = lavadoId;
        document.getElementById('cancelarLavadoIdAlt').value = lavadoId;
        document.getElementById('cancelarServicioId').value = servicioId || '';
        document.getElementById('cancelarEtapaId').value = etapaId || '';
        document.getElementById('cancelarTipo').value = tipo;
        document.getElementById('motivoCancelacion').value = '';

        // Actualizar action del formulario según el tipo
        const form = document.getElementById('formCancelar');
        if (tipo === 'lavado') {
            form.action = '/Lavados/CancelarLavado';
        } else if (tipo === 'servicio') {
            form.action = '/Lavados/CancelarServicio';
        } else {
            form.action = '/Lavados/CancelarEtapa';
        }

        abrirModal('cancelarModal');
    };

    window.abrirModalPago = function (lavadoId, precioTotal, pagado) {
        document.getElementById('pagoLavadoId').value = lavadoId;
        document.getElementById('pagoTotal').textContent = window.SiteModule.formatCurrency(precioTotal);
        document.getElementById('pagoPagado').textContent = window.SiteModule.formatCurrency(pagado);

        const restante = Math.round((precioTotal - pagado) * 100) / 100;
        document.getElementById('pagoRestante').textContent = window.SiteModule.formatCurrency(restante);

        // Configurar el input para no permitir más del restante
        const montoInput = document.getElementById('montoInput');
        montoInput.value = restante === 0 ? '0.00' : (restante % 1 === 0 ? restante : restante.toFixed(2));
        montoInput.max = restante;
        montoInput.dataset.restante = restante;

        document.getElementById('notasPago').value = '';

        abrirModal('pagoModal');
    };

    window.validarMontoPago = function () {
        const montoInput = document.getElementById('montoInput');
        const montoError = document.getElementById('montoError');
        if (!montoInput || !montoError) return true;
        const restante = parseFloat(montoInput.dataset.restante || '0');
        const monto = parseFloat(montoInput.value || '0');

        if (monto > restante) {
            montoInput.value = restante === 0 ? '0.00' : (restante % 1 === 0 ? restante : restante.toFixed(2));
            montoError.textContent = 'El monto no puede exceder el restante';
            montoError.classList.remove('hidden');
            setTimeout(() => montoError.classList.add('hidden'), 3000);
            return false;
        } else if (monto <= 0) {
            montoError.textContent = 'El monto debe ser mayor a 0';
            montoError.classList.remove('hidden');
            setTimeout(() => montoError.classList.add('hidden'), 3000);
            return false;
        } else {
            montoError.classList.add('hidden');
            return true;
        }
    };

    window.abrirModalFinalizar = function (lavadoId, servicioId, etapaId, tipo, nombre, sourceButton) {
        document.getElementById('finalizarLavadoId').value = lavadoId;
        document.getElementById('finalizarLavadoIdAlt').value = lavadoId;
        document.getElementById('finalizarServicioId').value = servicioId || '';
        document.getElementById('finalizarEtapaId').value = etapaId || '';
        document.getElementById('finalizarTipo').value = tipo;
        const consumoFields = document.getElementById('consumoRealFields');
        const consumoJson = document.getElementById('consumoRealJson');
        const justificacion = document.getElementById('justificacionDesvio');
        const consumoContenedor = document.getElementById('consumoRealContenedor');
        const herramientasSeccion = document.getElementById('herramientasUtilizadasSeccion');
        const herramientasList = document.getElementById('herramientasUtilizadasList');

        if (consumoJson) consumoJson.value = '';
        if (justificacion) { justificacion.value = ''; justificacion.required = false; }

        let receta = [];
        try { receta = JSON.parse(sourceButton?.dataset?.receta || '[]'); } catch { receta = []; }

        const consumibles = receta.filter(item => !item.EsHerramienta && !item.esHerramienta);
        const herramientas = receta.filter(item => item.EsHerramienta || item.esHerramienta);

        const aplicaReceta = (tipo === 'etapa' || tipo === 'servicio') && (consumibles.length > 0 || herramientas.length > 0);
        if (consumoFields) consumoFields.classList.toggle('hidden', !aplicaReceta);
        if (consumoContenedor) consumoContenedor.classList.toggle('hidden', consumibles.length === 0);
        if (herramientasSeccion) herramientasSeccion.classList.toggle('hidden', herramientas.length === 0);

        const inputs = document.getElementById('consumoRealInputs');
        if (inputs) {
            inputs.innerHTML = consumibles.map(item =>
                `<label class="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-gray-200 bg-gray-50 p-3 text-sm text-gray-900 dark:border-gray-600 dark:bg-gray-700 dark:text-white">
                    <span>${escapeConsumo(item.NombreInsumo || item.InsumoId)} (${escapeConsumo(item.UnidadMedida === 'l' ? 'Litro (L)' : item.UnidadMedida || '')})</span>
                    <input type="number" min="0" step="any" value="${item.DosisTeorica}" data-insumo-id="${escapeConsumo(item.InsumoId)}" data-teorica="${item.DosisTeorica}" class="consumo-real-input w-28 rounded-lg border border-gray-300 p-2 dark:border-gray-600 dark:bg-gray-800 dark:text-white" />
                </label>`).join('');
            inputs.querySelectorAll('.consumo-real-input').forEach(input => input.addEventListener('input', actualizarConsumoRealJson));
        }

        if (tipo === 'etapa' || aplicaReceta) {
            actualizarConsumoRealJson();
        }

        if (herramientasList) {
            herramientasList.innerHTML = herramientas.map(h =>
                `<div class="flex justify-between items-center bg-gray-50 dark:bg-gray-700/50 p-2 rounded border border-gray-200 dark:border-gray-600">
                    <span class="font-medium">${escapeConsumo(h.NombreInsumo || h.InsumoId)}</span>
                    <span class="text-gray-500 dark:text-gray-400">${h.DosisTeorica} ${escapeConsumo(h.UnidadMedida || 'unidad')}</span>
                </div>`).join('');
        }

        const title = tipo === 'lavado' ? 'Finalizar Lavado' :
            tipo === 'servicio' ? 'Finalizar Servicio' : 'Finalizar Etapa';
        const message = tipo === 'lavado' ? '¿Está seguro de finalizar este lavado?' :
            tipo === 'servicio' ? `¿Está seguro de finalizar el servicio "${nombre}"?` :
                `¿Está seguro de finalizar la etapa "${nombre}"?`;

        document.getElementById('finalizarModalTitle').textContent = title;
        document.getElementById('finalizarModalMessage').textContent = message;

        // Actualizar action del formulario según el tipo
        const form = document.getElementById('formFinalizar');
        if (tipo === 'lavado') {
            form.action = '/Lavados/FinalizarLavado';
        } else if (tipo === 'servicio') {
            form.action = '/Lavados/FinalizarServicio';
        } else {
            form.action = '/Lavados/FinalizarEtapa';
        }

        abrirModal('finalizarModal');
    };

    const escapeConsumo = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[c]));

    function actualizarConsumoRealJson() {
        const output = document.getElementById('consumoRealJson');
        if (!output) return;
        const result = {};
        const inputs = document.querySelectorAll('.consumo-real-input');
        inputs.forEach(input => {
            result[input.dataset.insumoId] = Number(input.value || 0);
        });
        output.value = JSON.stringify(result);
        const justificacion = document.getElementById('justificacionDesvio');
        if (justificacion) justificacion.required = Array.from(inputs).some(input => Number(input.value || 0) !== Number(input.dataset.teorica));
    }

    async function elegirProductos(params) {
        const respuesta = await window.Peticiones.enviar('/Lavados/RecursosInicio?' + new URLSearchParams(params));
        if (!respuesta.ok) throw new Error('No se pudieron consultar los productos.');
        const datos = await respuesta.json();
        if (!datos.success || !Array.isArray(datos.items)) throw new Error('No se pudieron consultar los productos.');
        return window.LavadoInsumos.seleccionar(datos.items);
    }

    window.iniciarServicioDesdeDetalle = async function (lavadoId, servicioId) {
        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const formData = new FormData();
            formData.append('lavadoId', lavadoId);
            formData.append('servicioId', servicioId);
            const seleccion = await elegirProductos({ lavadoId, servicioId });
            if (seleccion === null) return;
            formData.append('seleccionJson', JSON.stringify(seleccion));

            const response = await window.Peticiones.enviar('/Lavados/IniciarServicio', {
                method: 'POST',
                headers: { 'RequestVerificationToken': token },
                body: formData
            });

            const result = await response.json();
            if (result.success) {
                showMessage('success', result.message);
                setTimeout(() => window.location.reload(), 1000);
            } else {
                showMessage('error', result.message);
            }
        } catch (e) {
            console.error('Error:', e);
            showMessage('error', e.message || 'Error al iniciar el servicio.');
        }
    };

    window.iniciarEtapaDesdeDetalle = async function (lavadoId, servicioId, etapaId) {
        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const formData = new FormData();
            formData.append('lavadoId', lavadoId);
            formData.append('servicioId', servicioId);
            formData.append('etapaId', etapaId);
            const seleccion = await elegirProductos({ lavadoId, servicioId, etapaId });
            if (seleccion === null) return;
            formData.append('seleccionJson', JSON.stringify(seleccion));

            const response = await window.Peticiones.enviar('/Lavados/IniciarEtapa', {
                method: 'POST',
                headers: { 'RequestVerificationToken': token },
                body: formData
            });

            const result = await response.json();
            if (result.success) {
                showMessage('success', result.message);
                setTimeout(() => window.location.reload(), 1000);
            } else {
                showMessage('error', result.message);
            }
        } catch (e) {
            console.error('Error:', e);
            showMessage('error', e.message || 'Error al iniciar la etapa.');
        }
    };

    window.abrirModalRetiro = function (lavadoId) {
        document.getElementById('retiroLavadoId').value = lavadoId;
        abrirModal('retiroModal');
    };

    window.registrarRetiro = async function (lavadoId) {
        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const formData = new FormData();
            formData.append('lavadoId', lavadoId);

            const response = await window.Peticiones.enviar('/Lavados/RegistrarRetiro', {
                method: 'POST',
                headers: { 'RequestVerificationToken': token },
                body: formData
            });

            const result = await response.json();
            if (result.success) {
                showMessage('success', result.message);
                setTimeout(() => window.location.reload(), 1000);
            } else {
                showMessage('error', result.message);
            }
        } catch (e) {
            console.error('Error:', e);
            showMessage('error', 'Error al registrar el retiro.');
        }
    };

    // =====================================
    // MANEJO DE FORMULARIOS
    // =====================================
    function setupFormHandlers() {
        // Formulario de pago
        const formPago = document.getElementById('formPago');
        if (formPago) {
            formPago.addEventListener('submit', async function (e) {
                e.preventDefault();

                const montoInput = document.getElementById('montoInput');
                const restante = parseFloat(montoInput?.dataset?.restante || '0');
                const monto = parseFloat(montoInput?.value || '0');

                if (monto <= 0 || monto > restante) {
                    if (window.validarMontoPago) window.validarMontoPago();
                    showMessage('error', 'El monto no puede ser menor o igual a 0 ni superar el saldo pendiente.');
                    return;
                }

                const formData = new FormData(this);

                try {
                    const response = await window.Peticiones.enviar(this.action, {
                        method: 'POST',
                        body: formData
                    });

                    const result = await response.json();
                    cerrarModal('pagoModal');

                    if (result.success) {
                        showMessage('success', result.message);
                        setTimeout(() => window.location.reload(), 1000);
                    } else {
                        showMessage('error', result.message);
                    }
                } catch (e) {
                    console.error('Error:', e);
                    showMessage('error', 'Error al registrar el pago.');
                }
            });
        }

        // Formulario de cancelar
        const formCancelar = document.getElementById('formCancelar');
        if (formCancelar) {
            let cancelacionEnCurso = false;
            formCancelar.addEventListener('submit', async function (e) {
                e.preventDefault();
                if (cancelacionEnCurso) return;
                cancelacionEnCurso = true;
                let suspendido = false;
                try {
                const motivo = document.getElementById('motivoCancelacion').value;
                if (!motivo.trim()) {
                    showMessage('error', 'Debe ingresar un motivo de cancelación.');
                    return;
                }

                const formData = new FormData(this);
                const tipo = document.getElementById('cancelarTipo').value;
                try {
                    const params = new URLSearchParams({ lavadoId: document.getElementById('cancelarLavadoId').value });
                    if (tipo === 'servicio') params.set('servicioId', document.getElementById('cancelarServicioId').value);
                    if (tipo === 'etapa') {
                        params.set('servicioId', document.getElementById('cancelarServicioId').value);
                        params.set('etapaId', document.getElementById('cancelarEtapaId')?.value || '');
                    }
                    const consulta = await window.Peticiones.enviar('/Lavados/RecursosCancelacion?' + params);
                    if (!consulta.ok) throw new Error('No se pudieron consultar los consumos.');
                    const recursos = await consulta.json();
                    if (!recursos.success) throw new Error('No se pudieron consultar los consumos.');
                    if (recursos.productos && recursos.productos.length > 0) {
                        cerrarModal('cancelarModal');
                        suspendido = true;
                        const consumos = await window.LavadoInsumos.consumos(recursos);
                        if (consumos === null) { abrirModal('cancelarModal'); suspendido = false; return; }
                        formData.append('consumosNoRegistradosJson', JSON.stringify(consumos));
                    }
                } catch (error) { showMessage('error', error.message); return; }

                try {
                    const response = await window.Peticiones.enviar(this.action, {
                        method: 'POST',
                        body: formData
                    });

                    const result = await response.json();
                    if (!suspendido) cerrarModal('cancelarModal');
                    suspendido = false;

                    if (result.success) {
                        showMessage('success', result.message);
                        setTimeout(() => window.location.reload(), 1000);
                    } else {
                        showMessage('error', result.message);
                    }
                } catch (e) {
                    console.error('Error:', e);
                    showMessage('error', 'Error al cancelar.');
                }
                } finally {
                    if (suspendido) abrirModal('cancelarModal');
                    cancelacionEnCurso = false;
                }
            });
        }

        // Formulario de finalizar
        const formFinalizar = document.getElementById('formFinalizar');
        if (formFinalizar) {
            formFinalizar.addEventListener('submit', async function (e) {
                e.preventDefault();

                const tipo = document.getElementById('finalizarTipo').value;
                const lavadoId = document.getElementById('finalizarLavadoId').value;
                const servicioId = document.getElementById('finalizarServicioId').value;
                const etapaId = document.getElementById('finalizarEtapaId').value;

                if (!lavadoId) {
                    showMessage('error', 'ID de lavado inv\u00e1lido.');
                    return;
                }

                if (tipo === 'etapa' || tipo === 'servicio') {
                    const fields = document.querySelectorAll('.consumo-real-input');
                    if (Array.from(fields).some(field => field.value !== '' && (!Number.isFinite(Number(field.value)) || Number(field.value) < 0))) { showMessage('error', 'Cantidad inválida.'); return; }
                    const justification = document.getElementById('justificacionDesvio');
                    if (Array.from(fields).some(field => Number(field.value || 0) !== Number(field.dataset.teorica)) && !justification?.value.trim()) { showMessage('error', 'Debe informar la justificación del desvío.'); return; }
                }
                const formData = new FormData(this);

                try {
                    const response = await window.Peticiones.enviar(this.action, {
                        method: 'POST',
                        body: formData
                    });

                    const result = await response.json();
                    cerrarModal('finalizarModal');

                    if (result.success) {
                        showMessage('success', result.message);
                        setTimeout(() => window.location.reload(), 1000);
                    } else {
                        showMessage('error', result.message);
                    }
                } catch (e) {
                    console.error('Error:', e);
                    showMessage('error', 'Error al finalizar: ' + e.message);
                }
            });
        }

        // Formulario de retiro
        const formRetiro = document.getElementById('formRetiro');
        if (formRetiro) {
            formRetiro.addEventListener('submit', async function (e) {
                e.preventDefault();

                const lavadoId = document.getElementById('retiroLavadoId').value;

                if (!lavadoId) {
                    showMessage('error', 'ID de lavado inválido.');
                    return;
                }

                try {
                    cerrarModal('retiroModal');
                    await registrarRetiro(lavadoId);
                } catch (e) {
                    console.error('Error:', e);
                    showMessage('error', 'Error al registrar el retiro: ' + e.message);
                }
            });
        }
    }

    // =====================================
    // UTILIDADES
    // =====================================
    function abrirModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        // Crear backdrop si no existe
        let backdrop = document.getElementById('modal-backdrop');
        if (!backdrop) {
            backdrop = document.createElement('div');
            backdrop.id = 'modal-backdrop';
            backdrop.className = 'fixed inset-0 bg-gray-900 bg-opacity-50 z-40';
            backdrop.style.backgroundColor = 'rgba(17, 24, 39, 0.5)'; // Forzar opacidad
            document.body.appendChild(backdrop);
        }

        backdrop.style.zIndex = '51';
        modal.style.zIndex = '52';
        modal.classList.remove('hidden');
        modal.classList.add('z-50'); // Asegurar que el modal esté encima del backdrop
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('overflow-hidden');
    }

    function cerrarModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        modal.classList.add('hidden');
        modal.setAttribute('aria-hidden', 'true');

        // Remover backdrop
        const backdrop = document.getElementById('modal-backdrop');
        if (backdrop) {
            backdrop.remove();
        }

        document.body.classList.remove('overflow-hidden');
    }

    function showMessage(type, msg) {
        window.Notificaciones.mostrar(msg, type);
    }


    // Exportar funciones necesarias
    window.cerrarModal = cerrarModal;
    window.abrirModal = abrirModal;
})();