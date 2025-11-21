/**
 * ================================================
 * CLIENTE.JS - FUNCIONALIDAD DE LA PÁGINA DE CLIENTES
 * ================================================
 */

(function () {
    'use strict';

    let clienteMsgTimeout = null;
    let tableMsgTimeout = null;
    let searchTimeout = null;
    let currentSearchTerm = '';

    // =====================================
    // INICIALIZACIÓN DEL MÓDULO
    // =====================================
    window.PageModules = window.PageModules || {};
    window.PageModules.clientes = {
        init: initializeClientesPage
    };

    function initializeClientesPage() {
        setupFormValidation();
        setupSearchWithDebounce();
        protegerCamposEnEdicion();
        checkEditMode();
    }

    document.addEventListener('DOMContentLoaded', () => {
        try {
            window.PageModules?.clientes?.init();
        } catch (e) {
            initializeClientesPage();
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

    function protegerCamposEnEdicion() {
        const tipoDocSelect = document.getElementById('TipoDocumento');
        const numDocInput = document.getElementById('NumeroDocumento');
        const idInput = document.getElementById('Id');
        
        if (idInput && idInput.value) {
            if (tipoDocSelect) tipoDocSelect.disabled = true;
            if (numDocInput) numDocInput.readOnly = true;
        }
    }

    function setupFormValidation() {
        const form = document.getElementById('cliente-form');
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
                loadClientePage(1);
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

        const url = `/Cliente/SearchPartial?${params.toString()}`;

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const cont = document.getElementById('cliente-table-container');
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
    window.loadClienteForm = function (id) {
        const url = '/Cliente/FormPartial' + (id ? ('?id=' + encodeURIComponent(id)) : '');

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                document.getElementById('cliente-form-container').innerHTML = html;
                setupFormValidation();
                protegerCamposEnEdicion();

                const isEdit = !!document.getElementById('Id')?.value;
                const titleSpan = document.getElementById('form-title');
                if (titleSpan) {
                    titleSpan.textContent = isEdit ? 'Editando un Cliente' : 'Registrando un Cliente';
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

    window.submitClienteAjax = function (form) {
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
                document.getElementById('cliente-form-container').innerHTML = result.html;
                setupFormValidation();
                protegerCamposEnEdicion();

                const isEdit = !!document.getElementById('Id')?.value;
                const titleSpan = document.getElementById('form-title');
                if (titleSpan) {
                    titleSpan.textContent = isEdit ? 'Editando un Cliente' : 'Registrando un Cliente';
                }

                if (result.valid) {
                    showMessage('success', result.msg || 'Operación exitosa.', 4000);
                    loadClientePage(1);
                } else {
                    const summary = document.getElementById('cliente-validation-summary');
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
    window.openClienteConfirmModal = function (tipoAccion, id, nombre) {
        const modal = document.getElementById('clienteConfirmModal');
        const title = document.getElementById('clienteConfirmTitle');
        const msg = document.getElementById('clienteConfirmMessage');
        const submitBtn = document.getElementById('clienteConfirmSubmit');
        const form = document.getElementById('clienteConfirmForm');
        const idInput = document.getElementById('clienteConfirmId');

        idInput.value = id;

        if (tipoAccion === 'desactivar') {
            title.textContent = 'Desactivar Cliente';
            msg.innerHTML = '¿Confirma desactivar el cliente <strong>' + (window.SiteModule?.escapeHtml(nombre) || nombre) + '</strong>?';
            form.action = '/Cliente/DeactivateCliente';
            submitBtn.textContent = 'Desactivar';
            submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-red-600 rounded-lg hover:bg-red-700 focus:ring-4 focus:outline-none focus:ring-red-300 dark:bg-red-500 dark:hover:bg-red-600 dark:focus:ring-red-900';
        } else {
            title.textContent = 'Reactivar Cliente';
            msg.innerHTML = '¿Confirma reactivar el cliente <strong>' + (window.SiteModule?.escapeHtml(nombre) || nombre) + '</strong>?';
            form.action = '/Cliente/ReactivateCliente';
            submitBtn.textContent = 'Reactivar';
            submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-green-600 rounded-lg hover:bg-green-700 focus:ring-4 focus:outline-none focus:ring-green-300 dark:bg-green-500 dark:hover:bg-green-600 dark:focus:ring-green-900';
        }

        window.SiteModule?.abrirModal('clienteConfirmModal');
    };

    window.submitClienteEstado = function (form) {
        const formData = new FormData(form);

        fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(r => {
                if (!r.ok) throw new Error('Error estado');
                window.SiteModule?.cerrarModal('clienteConfirmModal');

                const isDeactivate = form.action.includes('DeactivateCliente');
                const message = isDeactivate ? 'Cliente desactivado correctamente.' : 'Cliente reactivado correctamente.';

                showTableMessage('success', message);
                loadClientePage(getCurrentTablePage());
            })
            .catch(e => {
                showTableMessage('error', 'Error procesando la operación.');
            });

        return false;
    };

    // =====================================
    // PAGINACIÓN Y TABLA
    // =====================================
    window.loadClientePage = function (page) {
        const params = new URLSearchParams();
        params.set('pageNumber', page);
        if (currentSearchTerm) params.set('searchTerm', currentSearchTerm);

        const url = `/Cliente/TablePartial?${params.toString()}`;

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const cont = document.getElementById('cliente-table-container');
                cont.innerHTML = html;
                cont.dataset.currentPage = page;
            })
            .catch(e => console.error('Error cargando tabla:', e));
    };

    function getCurrentTablePage() {
        const cont = document.getElementById('cliente-table-container');
        return parseInt(cont?.dataset.currentPage || '1', 10);
    }

    // =====================================
    // MENSAJES
    // =====================================
    function showMessage(type, text, duration = 4000) {
        const container = document.getElementById('ajax-form-messages');
        if (!container) return;

        if (clienteMsgTimeout) {
            clearTimeout(clienteMsgTimeout);
            clienteMsgTimeout = null;
        }

        const bgClass = type === 'success' ? 'bg-green-50 text-green-800 dark:bg-gray-800 dark:text-green-400' : 'bg-red-50 text-red-800 dark:bg-gray-800 dark:text-red-400';
        container.innerHTML = `<div class="p-4 mb-4 text-sm rounded-lg ${bgClass}" role="alert">${text}</div>`;

        clienteMsgTimeout = setTimeout(() => {
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
    // MODAL TIPO DOCUMENTO
    // =====================================
    window.limpiarModalTipoDocumento = function () {
        const input = document.getElementById('nombreTipoDocumento');
        if (input) input.value = '';
    };

})();
