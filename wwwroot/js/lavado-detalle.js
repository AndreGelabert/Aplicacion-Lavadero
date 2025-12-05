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
        document.getElementById('pagoTotal').textContent = formatCurrency(precioTotal);
        document.getElementById('pagoPagado').textContent = formatCurrency(pagado);

        const restante = precioTotal - pagado;
        document.getElementById('pagoRestante').textContent = formatCurrency(restante);

        // Configurar el input para no permitir más del restante
        const montoInput = document.getElementById('montoInput');
        montoInput.value = restante.toFixed(2);
        montoInput.max = restante.toFixed(2);
        montoInput.dataset.restante = restante.toFixed(2);

        document.getElementById('notasPago').value = '';

        abrirModal('pagoModal');
    };

    window.validarMontoPago = function () {
        const montoInput = document.getElementById('montoInput');
        const montoError = document.getElementById('montoError');
        const restante = parseFloat(montoInput.dataset.restante || 0);
        const monto = parseFloat(montoInput.value || 0);

        if (monto > restante) {
            montoInput.value = restante.toFixed(2);
            montoError.classList.remove('hidden');
            setTimeout(() => montoError.classList.add('hidden'), 3000);
        }
    };

    window.abrirModalFinalizar = function (lavadoId, servicioId, etapaId, tipo, nombre) {
        document.getElementById('finalizarLavadoId').value = lavadoId;
        document.getElementById('finalizarLavadoIdAlt').value = lavadoId;
        document.getElementById('finalizarServicioId').value = servicioId || '';
        document.getElementById('finalizarEtapaId').value = etapaId || '';
        document.getElementById('finalizarTipo').value = tipo;

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

    window.iniciarServicioDesdeDetalle = async function (lavadoId, servicioId) {
        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const formData = new FormData();
            formData.append('lavadoId', lavadoId);
            formData.append('servicioId', servicioId);

            const response = await fetch('/Lavados/IniciarServicio', {
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
            showMessage('error', 'Error al iniciar el servicio.');
        }
    };

    window.iniciarEtapaDesdeDetalle = async function (lavadoId, servicioId, etapaId) {
        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const formData = new FormData();
            formData.append('lavadoId', lavadoId);
            formData.append('servicioId', servicioId);
            formData.append('etapaId', etapaId);

            const response = await fetch('/Lavados/IniciarEtapa', {
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
            showMessage('error', 'Error al iniciar la etapa.');
        }
    };

    window.registrarRetiro = async function (lavadoId) {
        if (!confirm('¿Confirma que el vehículo ha sido retirado por el cliente autorizado?')) {
            return;
        }

        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const formData = new FormData();
            formData.append('lavadoId', lavadoId);

            const response = await fetch('/Lavados/RegistrarRetiro', {
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
                const formData = new FormData(this);

                try {
                    const response = await fetch(this.action, {
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
            formCancelar.addEventListener('submit', async function (e) {
                e.preventDefault();

                const motivo = document.getElementById('motivoCancelacion').value;
                if (!motivo.trim()) {
                    showMessage('error', 'Debe ingresar un motivo de cancelación.');
                    return;
                }

                const formData = new FormData(this);

                try {
                    const response = await fetch(this.action, {
                        method: 'POST',
                        body: formData
                    });

                    const result = await response.json();
                    cerrarModal('cancelarModal');

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

                const formData = new FormData(this);

                try {
                    const response = await fetch(this.action, {
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
        let container = document.getElementById('messages-container');

        if (!container) {
            container = document.createElement('div');
            container.id = 'messages-container';
            container.className = 'fixed top-4 right-4 z-50';
            document.body.appendChild(container);
        }

        const color = type === 'success'
            ? { bg: 'green-50', text: 'green-800', darkText: 'green-400', border: 'green-300' }
            : type === 'info'
                ? { bg: 'blue-50', text: 'blue-800', darkText: 'blue-400', border: 'blue-300' }
                : { bg: 'red-50', text: 'red-800', darkText: 'red-400', border: 'red-300' };

        const alert = document.createElement('div');
        alert.className = `opacity-100 transition-opacity duration-700 p-4 mb-4 text-sm rounded-lg border bg-${color.bg} text-${color.text} border-${color.border} dark:bg-gray-800 dark:text-${color.darkText} min-w-[300px]`;
        alert.textContent = msg;

        container.appendChild(alert);

        setTimeout(() => {
            alert.classList.add('opacity-0');
            setTimeout(() => alert.remove(), 750);
        }, 5000);
    }

    function formatCurrency(value) {
        return new Intl.NumberFormat('es-AR', {
            style: 'currency',
            currency: 'ARS'
        }).format(value);
    }

    // Exportar funciones necesarias
    window.cerrarModal = cerrarModal;
    window.abrirModal = abrirModal;
})();