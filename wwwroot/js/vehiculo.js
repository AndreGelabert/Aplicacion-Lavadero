/**
 * ================================================
 * VEHICULO.JS - FUNCIONALIDAD DE LA PÁGINA DE VEHÍCULOS
 * ================================================
 */

(function () {
    'use strict';

    let vehiculoMsgTimeout = null;
    let tableMsgTimeout = null;
    let searchTimeout = null;
    let currentSearchTerm = '';
    let currentClienteId = '';

    // =====================================
    // INICIALIZACIÓN DEL MÓDULO
    // =====================================
    window.PageModules = window.PageModules || {};
    window.PageModules.vehiculos = {
        init: initializeVehiculosPage
    };

    function initializeVehiculosPage() {
        setupFormValidation();
        setupSearchWithDebounce();
        protegerCamposEnEdicion();
        checkEditMode();
        captureClienteId();
    }

    document.addEventListener('DOMContentLoaded', () => {
        try {
            window.PageModules?.vehiculos?.init();
        } catch (e) {
            initializeVehiculosPage();
        }
    });

    // =====================================
    // CONFIGURACIÓN INICIAL
    // =====================================
    function checkEditMode() {
        const formTitle = document.getElementById('form-title');
        if (formTitle && formTitle.textContent.includes('Editando')) {
            const accordion = document.getElementById('accordion-flush-body-1');
            if (accordion) accordion.classList.remove('hidden');
        }
    }

    function captureClienteId() {
        const cont = document.getElementById('vehiculo-table-container');
        currentClienteId = cont?.dataset.clienteId || '';
    }

    function protegerCamposEnEdicion() {
        const placaInput = document.getElementById('Placa');
        const tipoVehSelect = document.getElementById('TipoVehiculo');
        const marcaInput = document.getElementById('Marca');
        const idInput = document.getElementById('Id');
        
        if (idInput && idInput.value) {
            if (placaInput) placaInput.readOnly = true;
            if (tipoVehSelect) tipoVehSelect.disabled = true;
            if (marcaInput) marcaInput.readOnly = true;
        }
    }

    function setupFormValidation() {
        const form = document.getElementById('vehiculo-form');
        if (!form) return;

        const inputs = form.querySelectorAll('input[required], select[required]');
        inputs.forEach(input => {
            input.addEventListener('invalid', function(e) {
                e.preventDefault();
                this.classList.add('border-red-500');
            });
            input.addEventListener('input', function() {
                this.classList.remove('border-red-500');
            });
        });

        // Convertir placa a mayúsculas automáticamente
        const placaInput = document.getElementById('Placa');
        if (placaInput) {
            placaInput.addEventListener('input', function() {
                this.value = this.value.toUpperCase();
            });
        }
    }

    // =====================================
    // BUSQUEDA
    // =====================================
    function setupSearchWithDebounce() {
        const searchInput = document.getElementById('simple-search');
        if (!searchInput) return;

        const newSearchInput = searchInput.cloneNode(true);
        searchInput.parentNode.replaceChild(newSearchInput, searchInput);

        currentSearchTerm = newSearchInput.value?.trim() || '';

        newSearchInput.addEventListener('input', function () {
            const searchTerm = this.value.trim();

            if (searchTimeout) clearTimeout(searchTimeout);

            if (searchTerm === '') {
                currentSearchTerm = '';
                loadVehiculoPage(1);
                return;
            }

            searchTimeout = setTimeout(() => {
                performSearch(searchTerm);
            }, 500);
        });
    }

    function performSearch(searchTerm) {
        currentSearchTerm = searchTerm;
        const params = new URLSearchParams();
        params.set('searchTerm', searchTerm);
        params.set('pageNumber', '1');
        if (currentClienteId) params.set('clienteId', currentClienteId);

        const url = `/Vehiculo/SearchPartial?${params.toString()}`;

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const cont = document.getElementById('vehiculo-table-container');
                cont.innerHTML = html;
            })
            .catch(e => {
                console.error('Error en búsqueda:', e);
                showTableMessage('error', 'Error al realizar la búsqueda.');
            });
    }

    // =====================================
    // GESTIÓN DE FORMULARIOS
    // =====================================
    window.loadVehiculoForm = function (id) {
        const url = '/Vehiculo/FormPartial' + (id ? ('?id=' + encodeURIComponent(id)) : '') + 
                    (currentClienteId && !id ? ('?clienteId=' + encodeURIComponent(currentClienteId)) : '');

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                document.getElementById('vehiculo-form-container').innerHTML = html;
                setupFormValidation();
                protegerCamposEnEdicion();

                const isEdit = !!document.getElementById('Id')?.value;
                const titleSpan = document.getElementById('form-title');
                if (titleSpan) {
                    titleSpan.textContent = isEdit ? 'Editando un Vehículo' : 'Registrando un Vehículo';
                }

                const accordionBody = document.getElementById('accordion-flush-body-1');
                if (accordionBody) {
                    if (isEdit) {
                        accordionBody.classList.remove('hidden');
                    } else {
                        accordionBody.classList.add('hidden');
                    }
                }

                if (isEdit) {
                    setTimeout(() => {
                        const formContainer = document.getElementById('accordion-flush');
                        if (formContainer) {
                            formContainer.scrollIntoView({ behavior: 'smooth', block: 'start' });
                        }
                    }, 100);
                }
            })
            .catch(e => console.error('Error cargando formulario:', e));
    };

    window.submitVehiculoAjax = function (form) {
        const formData = new FormData(form);

        fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(resp => {
                const valid = resp.headers.get('X-Form-Valid') === 'true';
                const msg = resp.headers.get('X-Form-Message');
                return resp.text().then(html => ({ html, valid, msg }));
            })
            .then(result => {
                document.getElementById('vehiculo-form-container').innerHTML = result.html;
                setupFormValidation();
                protegerCamposEnEdicion();

                const isEdit = !!document.getElementById('Id')?.value;
                const titleSpan = document.getElementById('form-title');
                if (titleSpan) {
                    titleSpan.textContent = isEdit ? 'Editando un Vehículo' : 'Registrando un Vehículo';
                }

                if (result.valid) {
                    showMessage('success', result.msg || 'Operación exitosa.', 4000);
                    loadVehiculoPage(1);
                } else {
                    const summary = document.getElementById('vehiculo-validation-summary');
                    if (summary && summary.textContent.trim().length > 0) {
                        summary.classList.remove('hidden');
                    }
                    showMessage('error', 'Revise los errores del formulario.', 8000);
                }
            })
            .catch(e => {
                showMessage('error', 'Error de comunicación con el servidor.', 8000);
            });

        return false;
    };

    // =====================================
    // GESTIÓN DE ESTADO
    // =====================================
    window.openVehiculoConfirmModal = function (tipoAccion, id, placa) {
        const modal = document.getElementById('vehiculoConfirmModal');
        const title = document.getElementById('vehiculoConfirmTitle');
        const msg = document.getElementById('vehiculoConfirmMessage');
        const submitBtn = document.getElementById('vehiculoConfirmSubmit');
        const form = document.getElementById('vehiculoConfirmForm');
        const idInput = document.getElementById('vehiculoConfirmId');

        idInput.value = id;

        if (tipoAccion === 'desactivar') {
            title.textContent = 'Desactivar Vehículo';
            msg.innerHTML = '¿Confirma desactivar el vehículo con placa <strong>' + (window.SiteModule?.escapeHtml(placa) || placa) + '</strong>?';
            form.action = '/Vehiculo/DeactivateVehiculo';
            submitBtn.textContent = 'Desactivar';
            submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-red-600 rounded-lg hover:bg-red-700 focus:ring-4 focus:outline-none focus:ring-red-300 dark:bg-red-500 dark:hover:bg-red-600 dark:focus:ring-red-900';
        } else {
            title.textContent = 'Reactivar Vehículo';
            msg.innerHTML = '¿Confirma reactivar el vehículo con placa <strong>' + (window.SiteModule?.escapeHtml(placa) || placa) + '</strong>?';
            form.action = '/Vehiculo/ReactivateVehiculo';
            submitBtn.textContent = 'Reactivar';
            submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-green-600 rounded-lg hover:bg-green-700 focus:ring-4 focus:outline-none focus:ring-green-300 dark:bg-green-500 dark:hover:bg-green-600 dark:focus:ring-green-900';
        }

        window.SiteModule?.abrirModal('vehiculoConfirmModal');
    };

    window.submitVehiculoEstado = function (form) {
        const formData = new FormData(form);

        fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(r => {
                if (!r.ok) throw new Error('Error estado');
                window.SiteModule?.cerrarModal('vehiculoConfirmModal');

                const isDeactivate = form.action.includes('DeactivateVehiculo');
                const message = isDeactivate ? 'Vehículo desactivado correctamente.' : 'Vehículo reactivado correctamente.';

                showTableMessage('success', message);
                loadVehiculoPage(getCurrentTablePage());
            })
            .catch(e => {
                showTableMessage('error', 'Error procesando la operación.');
            });

        return false;
    };

    // =====================================
    // PAGINACIÓN Y TABLA
    // =====================================
    window.loadVehiculoPage = function (page) {
        const params = new URLSearchParams();
        params.set('pageNumber', page);
        if (currentSearchTerm) params.set('searchTerm', currentSearchTerm);
        if (currentClienteId) params.set('clienteId', currentClienteId);

        const url = `/Vehiculo/TablePartial?${params.toString()}`;

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const cont = document.getElementById('vehiculo-table-container');
                cont.innerHTML = html;
                cont.dataset.currentPage = page;
            })
            .catch(e => console.error('Error cargando tabla:', e));
    };

    function getCurrentTablePage() {
        const cont = document.getElementById('vehiculo-table-container');
        return parseInt(cont?.dataset.currentPage || '1', 10);
    }

    // =====================================
    // MENSAJES
    // =====================================
    function showMessage(type, text, duration = 4000) {
        const container = document.getElementById('ajax-form-messages');
        if (!container) return;

        if (vehiculoMsgTimeout) {
            clearTimeout(vehiculoMsgTimeout);
            vehiculoMsgTimeout = null;
        }

        const bgClass = type === 'success' ? 'bg-green-50 text-green-800 dark:bg-gray-800 dark:text-green-400' : 'bg-red-50 text-red-800 dark:bg-gray-800 dark:text-red-400';
        container.innerHTML = `<div class="p-4 mb-4 text-sm rounded-lg ${bgClass}" role="alert">${text}</div>`;

        vehiculoMsgTimeout = setTimeout(() => {
            container.innerHTML = '';
        }, duration);
    }

    function showTableMessage(type, text, duration = 4000) {
        const container = document.getElementById('table-message-container');
        if (!container) {
            console.log('Mensaje (no hay contenedor):', text);
            return;
        }

        if (tableMsgTimeout) {
            clearTimeout(tableMsgTimeout);
            tableMsgTimeout = null;
        }

        const bgClass = type === 'success' ? 'text-green-800 bg-green-50 dark:bg-gray-800 dark:text-green-400' : 'text-red-800 bg-red-50 dark:bg-gray-800 dark:text-red-400';
        
        container.className = `p-4 mb-4 mx-4 text-sm rounded-lg ${bgClass}`;
        container.querySelector('#table-message-text').textContent = text;
        container.classList.remove('hidden');

        tableMsgTimeout = setTimeout(() => {
            container.classList.add('hidden');
        }, duration);
    }

    // =====================================
    // MODAL TIPO VEHÍCULO
    // =====================================
    window.limpiarModalTipoVehiculo = function () {
        const input = document.getElementById('nombreTipoVehiculo');
        if (input) input.value = '';
    };

})();
