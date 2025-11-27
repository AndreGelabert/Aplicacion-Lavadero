/**
 * ================================================
 * VEHICULO.JS - FUNCIONALIDAD DE LA PÁGINA DE VEHÍCULOS
 * ================================================
 * Responsabilidades:
 *  - Búsqueda con debounce y ordenamiento de tabla
 *  - Filtros y recarga parcial (tabla)
 *  - Formulario AJAX solo para edición
 *  - Gestión de activación/desactivación (modal de confirmación)
 *  - Notificaciones de operaciones
 */

(function () {
    'use strict';

    // ===================== Estado interno =====================
    let currentPage = 1;
    let currentSearchTerm = "";
    let currentTipoVehiculo = "";
    let currentSortBy = "Patente";
    let currentSortOrder = "asc";
    let searchTimeout;
    let vehiculoMsgTimeout = null;
    let tableMsgTimeout = null;

    // ===================== Inicialización del módulo =====================
    window.PageModules = window.PageModules || {};
    window.PageModules.vehiculos = { init: initializeVehiculosPage };

    document.addEventListener("DOMContentLoaded", () => {
        try { window.PageModules?.vehiculos?.init(); }
        catch { initializeVehiculosPage(); }
    });

    /**
     * Inicializa el comportamiento principal de la página de Vehículos
     */
    function initializeVehiculosPage() {
        setupInitialState();
        setupSearchWithDebounce();
        setupFilterFormSubmit();
        setupModals();
        checkEditMode();
        window.CommonUtils?.setupDefaultFilterForm();
    }

    // ===================== Configuración inicial =====================

    function setupInitialState() {
        const pageInput = document.getElementById("current-page-value");
        if (pageInput) currentPage = parseInt(pageInput.value);

        const sortInput = document.getElementById("current-sort-by");
        if (sortInput) currentSortBy = sortInput.value;

        const orderInput = document.getElementById("current-sort-order");
        if (orderInput) currentSortOrder = orderInput.value;
    }

    function checkEditMode() {
        const formTitle = document.getElementById('form-title');
        if (formTitle && formTitle.textContent.includes('Editando')) {
            const accordion = document.getElementById('accordion-flush-body-1');
            if (accordion) {
                accordion.classList.remove('hidden');
                accordion.style.display = 'block';
            }
        }
    }

    // ===================== Búsqueda (debounce) =====================

    function setupSearchWithDebounce() {
        const searchInput = document.getElementById("simple-search");
        if (!searchInput) return;

        const cloned = searchInput.cloneNode(true);
        searchInput.parentNode.replaceChild(cloned, searchInput);

        currentSearchTerm = cloned.value?.trim() || '';

        cloned.addEventListener("input", function () {
            clearTimeout(searchTimeout);
            const term = this.value.trim();

            if (term === '') {
                currentSearchTerm = '';
                currentPage = 1;
                reloadVehiculoTable();
                return;
            }

            searchTimeout = setTimeout(() => {
                currentSearchTerm = term;
                currentPage = 1;
                reloadVehiculoTable();
            }, 500);
        });
    }

    // ===================== Ordenamiento tabla =====================

    window.sortTable = function (column) {
        if (currentSortBy === column) {
            currentSortOrder = currentSortOrder === "asc" ? "desc" : "asc";
        } else {
            currentSortBy = column;
            currentSortOrder = "asc";
        }
        reloadVehiculoTable();
    };

    // ===================== Filtros y recarga de tabla =====================

    function buildFilterParams() {
        const form = document.getElementById('filterForm');
        const params = new URLSearchParams();
        if (!form) return params;

        const fd = new FormData(form);
        for (const [k, v] of fd.entries()) {
            params.append(k, v);
        }
        return params;
    }

    function reloadVehiculoTable(page) {
        if (page) currentPage = page;

        const params = buildFilterParams();
        
        // Obtener filtro de tipo de vehículo
        const tipoRadio = document.querySelector('input[name="tipoVehiculo"]:checked');
        if (tipoRadio) {
            currentTipoVehiculo = tipoRadio.value;
            params.set('tipoVehiculo', currentTipoVehiculo);
        }

        params.set('searchTerm', currentSearchTerm);
        params.set('pageNumber', currentPage.toString());
        params.set('sortBy', currentSortBy);
        params.set('sortOrder', currentSortOrder);

        const url = `/Vehiculo/TablePartial?${params.toString()}`;

        fetch(url, {
            headers: { 'X-Requested-With': 'XMLHttpRequest', 'Cache-Control': 'no-cache' },
            cache: 'no-store'
        })
            .then(r => r.text())
            .then(html => {
                const container = document.getElementById("vehiculo-table-container");
                if (container) container.innerHTML = html;

                const cp = document.getElementById('current-page-value')?.value;
                if (cp && container) container.dataset.currentPage = cp;

                window.CommonUtils?.setupDefaultFilterForm?.();
            })
            .catch(error => {
                console.error('Error al cargar la tabla:', error);
                showTableMessage('error', 'Error al cargar los datos.');
            });
    }

    window.reloadVehiculoTable = reloadVehiculoTable;

    window.getCurrentTablePage = function () {
        return parseInt(document.getElementById('vehiculo-table-container')?.dataset.currentPage || '1');
    };

    function setupFilterFormSubmit() {
        const form = document.getElementById('filterForm');
        if (!form || form.dataset.submitSetup === 'true') return;

        form.addEventListener('submit', (e) => {
            e.preventDefault();
            e.stopPropagation();

            const pg = form.querySelector('input[name="pageNumber"]');
            if (pg) pg.value = '1';

            const searchInput = document.getElementById('simple-search');
            if (searchInput) currentSearchTerm = searchInput.value.trim();

            document.getElementById('filterDropdown')?.classList.add('hidden');

            reloadVehiculoTable(1);

            if (history.replaceState) history.replaceState(null, '', window.location.pathname);

            showTableMessage('info', 'Filtros aplicados.');
        });

        form.dataset.submitSetup = 'true';
    }

    window.clearVehiculoFilters = function () {
        const form = document.getElementById('filterForm');
        if (!form) return;

        try {
            if (typeof window.clearAllFilters === 'function') {
                window.clearAllFilters();
            } else {
                form.querySelectorAll('input[name="estados"][type="checkbox"]').forEach(cb => cb.checked = false);
                form.querySelector('input[name="estados"][value="Activo"]')?.setAttribute('checked', 'checked');
            }
        } catch { }

        form.querySelectorAll('input[type="radio"]').forEach(r => r.checked = false);
        
        const searchInput = document.getElementById('simple-search');
        if (searchInput) searchInput.value = '';
        currentSearchTerm = '';
        currentTipoVehiculo = '';

        document.getElementById('filterDropdown')?.classList.add('hidden');

        if (history.replaceState) history.replaceState({}, document.title, '/Vehiculo/Index');

        window.CommonUtils?.setupDefaultFilterForm?.();

        reloadVehiculoTable(1);
        showTableMessage('info', 'Filtros restablecidos.');
    };

    // ===================== Formulario (solo para edición) =====================

    window.loadVehiculoForm = function (id) {
        if (!id) {
            showTableMessage('error', 'ID de vehículo no válido.');
            return;
        }

        const url = `/Vehiculo/FormPartial?id=${id}`;

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(response => response.text())
            .then(html => {
                document.getElementById("vehiculo-form-container").innerHTML = html;
                document.getElementById('form-title').textContent = 'Editando Vehículo';

                const accordionBtn = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
                const accordionBody = document.getElementById("accordion-flush-body-1");
                
                if (accordionBody) {
                    accordionBody.classList.remove('hidden');
                    accordionBody.style.display = 'block';
                }

                setTimeout(() => {
                    const formContainer = document.getElementById('accordion-flush');
                    if (formContainer) {
                        formContainer.scrollIntoView({ behavior: 'smooth', block: 'start' });
                    }
                }, 100);
            })
            .catch(error => console.error('Error al cargar el formulario:', error));
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

                if (result.valid) {
                    showFormMessage('success', result.msg || 'Vehículo actualizado correctamente.', 4000);
                    reloadVehiculoTable(getCurrentTablePage());

                    setTimeout(() => {
                        const accordion = document.getElementById('accordion-flush-body-1');
                        if (accordion) {
                            accordion.classList.add('hidden');
                            accordion.style.display = 'none';
                        }
                    }, 1500);
                } else {
                    const summary = document.getElementById('vehiculo-validation-summary');
                    if (summary && summary.textContent.trim().length > 0) {
                        summary.classList.remove('hidden');
                    }
                    showFormMessage('error', 'Revise los errores del formulario.', 8000);
                }
            })
            .catch(e => {
                showFormMessage('error', 'Error de comunicación con el servidor.', 8000);
            });

        return false;
    };

    // ===================== Modales =====================

    function setupModals() {
        setupDeleteModal();
    }

    function setupDeleteModal() {
        const form = document.getElementById("vehiculoConfirmForm") || document.getElementById("formEliminarVehiculo");
        if (!form || form.dataset.setup === 'true') return;

        form.addEventListener("submit", function (e) {
            e.preventDefault();
            e.stopPropagation();

            const formData = new FormData(this);

            fetch(this.action, {
                method: 'POST',
                body: formData,
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
                .then(response => response.json())
                .then(data => {
                    closeVehiculoConfirmModal();
                    if (data.success) {
                        showTableMessage('success', data.message);
                        reloadVehiculoTable(getCurrentTablePage());
                    } else {
                        showTableMessage('error', data.message);
                    }
                })
                .catch(error => {
                    console.error('Error:', error);
                    closeVehiculoConfirmModal();
                    showTableMessage('error', 'Error al procesar la solicitud.');
                });
        });

        form.dataset.setup = 'true';
    }

    window.openVehiculoConfirmModal = function (tipoAccion, id, patente) {
        const modalId = 'vehiculoConfirmModal';
        const modal = document.getElementById(modalId);
        const title = document.getElementById('vehiculoConfirmTitle');
        const message = document.getElementById('vehiculoConfirmMessage');
        const idInput = document.getElementById('vehiculoConfirmId') || document.getElementById('idVehiculoEliminar');
        const submitBtn = document.getElementById('vehiculoConfirmSubmit');
        const form = document.getElementById('vehiculoConfirmForm') || document.getElementById('formEliminarVehiculo');
        const iconWrapper = document.getElementById('vehiculoConfirmIconWrapper');
        const icon = document.getElementById('vehiculoConfirmIcon');

        const esDesactivar = tipoAccion === 'desactivar';

        if (title) title.textContent = esDesactivar ? 'Desactivar Vehículo' : 'Reactivar Vehículo';

        if (message) {
            message.innerHTML = esDesactivar
                ? '¿Confirma desactivar el vehículo con patente <strong>' + escapeHtml(patente) + '</strong>?'
                : '¿Confirma reactivar el vehículo con patente <strong>' + escapeHtml(patente) + '</strong>?';
        }

        if (idInput) idInput.value = id;

        if (form) {
            form.action = esDesactivar ? '/Vehiculo/DeactivateVehiculo' : '/Vehiculo/ReactivateVehiculo';
        }

        if (submitBtn) {
            submitBtn.textContent = esDesactivar ? 'Desactivar' : 'Reactivar';
            submitBtn.className = esDesactivar
                ? 'py-2 px-3 text-sm font-medium text-center text-white bg-red-600 rounded-lg hover:bg-red-700 focus:ring-4 focus:outline-none focus:ring-red-300 dark:bg-red-500 dark:hover:bg-red-600 dark:focus:ring-red-900'
                : 'py-2 px-3 text-sm font-medium text-center text-white bg-green-600 rounded-lg hover:bg-green-700 focus:ring-4 focus:outline-none focus:ring-green-300 dark:bg-green-500 dark:hover:bg-green-600 dark:focus:ring-green-900';
        }

        if (iconWrapper && icon) {
            if (esDesactivar) {
                iconWrapper.className = 'w-12 h-12 rounded-full bg-red-100 dark:bg-red-900 p-2 flex items-center justify-center mx-auto mb-3.5';
                icon.setAttribute('fill', 'currentColor');
                icon.setAttribute('viewBox', '0 0 20 20');
                icon.setAttribute('class', 'w-8 h-8 text-red-600 dark:text-red-400');
                icon.innerHTML = `<path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-11.293a1 1 0 00-1.414-1.414L10 7.586 7.707 5.293a1 1 0 00-1.414 1.414L8.586 10l-2.293 2.293a1 1 0 001.414 1.414L10 11.414l2.293 2.293a1 1 0 001.414-1.414L11.414 10l2.293-2.293z" clip-rule="evenodd"/>`;
            } else {
                iconWrapper.className = 'w-12 h-12 rounded-full bg-green-100 dark:bg-green-900 p-2 flex items-center justify-center mx-auto mb-3.5';
                icon.setAttribute('fill', 'currentColor');
                icon.setAttribute('viewBox', '0 0 24 24');
                icon.setAttribute('class', 'w-8 h-8 text-green-500 dark:text-green-400');
                icon.innerHTML = `<path fill-rule="evenodd" d="M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 4.365 9.75 9.75-4.365 9.75-9.75 9.75S2.25 17.385 2.25 12Zm13.36-1.814a.75.75 0 1 0-1.22-.872l-3.236 4.53L9.53 12.22a.75.75 0 0 0-1.06 1.06l2.25 2.25a.75.75 0 0 0 1.14-.094l3.75-5.25Z" clip-rule="evenodd"/>`;
            }
        }

        abrirModal(modalId);
    };

    window.closeVehiculoConfirmModal = function () {
        cerrarModal('vehiculoConfirmModal');
    };

    window.submitVehiculoEstado = function (form) {
        const fd = new FormData(form);
        fetch(form.action, {
            method: 'POST',
            body: fd,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(r => {
                if (!r.ok) throw new Error('Estado HTTP ' + r.status);
                return r.json();
            })
            .then(data => {
                closeVehiculoConfirmModal();
                if (data.success) {
                    const accion = form.action.includes('Deactivate') ? 'desactivado' : 'reactivado';
                    showTableMessage('success', `Vehículo ${accion} correctamente.`);
                } else {
                    showTableMessage('error', data.message || 'No se pudo completar la acción.');
                }
                reloadVehiculoTable(getCurrentTablePage());
            })
            .catch(err => {
                console.error('submitVehiculoEstado error:', err);
                closeVehiculoConfirmModal();
                showTableMessage('error', 'No se pudo completar la acción.');
            });
        return false;
    };

    // ===================== Mensajería =====================

    function showFormMessage(type, message, disappearMs = 5000) {
        const container = document.getElementById('ajax-form-messages');
        if (!container) return;

        if (vehiculoMsgTimeout) {
            clearTimeout(vehiculoMsgTimeout);
            vehiculoMsgTimeout = null;
        }

        const color = type === 'success'
            ? { bg: 'green-50', text: 'green-800', darkText: 'green-400', border: 'green-300' }
            : type === 'info'
                ? { bg: 'blue-50', text: 'blue-800', darkText: 'blue-400', border: 'blue-300' }
                : { bg: 'red-50', text: 'red-800', darkText: 'red-400', border: 'red-300' };

        container.innerHTML = `<div class="opacity-100 transition-opacity duration-700
            p-4 mb-4 text-sm rounded-lg border
            bg-${color.bg} text-${color.text} border-${color.border}
            dark:bg-gray-800 dark:text-${color.darkText}">
            ${escapeHtml(message)}
        </div>`;

        vehiculoMsgTimeout = setTimeout(() => {
            const alertEl = container.firstElementChild;
            if (alertEl) {
                alertEl.classList.add('opacity-0');
                setTimeout(() => { try { alertEl.remove(); } catch { } }, 700);
            }
        }, disappearMs);
    }

    function showTableMessage(type, msg, disappearMs = 5000) {
        let container = document.getElementById('table-messages-container');

        if (!container) {
            container = document.createElement('div');
            container.id = 'table-messages-container';
            container.className = 'mb-4';

            const tableContainer = document.getElementById('vehiculo-table-container');
            if (tableContainer?.parentNode) {
                tableContainer.parentNode.insertBefore(container, tableContainer);
            } else {
                document.body.prepend(container);
            }
        }

        container.style.display = 'block';

        if (tableMsgTimeout) {
            clearTimeout(tableMsgTimeout);
            tableMsgTimeout = null;
        }

        const color = type === 'success'
            ? { bg: 'green-50', text: 'green-800', darkText: 'green-400', border: 'green-300' }
            : type === 'info'
                ? { bg: 'blue-50', text: 'blue-800', darkText: 'blue-400', border: 'blue-300' }
                : { bg: 'red-50', text: 'red-800', darkText: 'red-400', border: 'red-300' };

        container.innerHTML = `<div class="opacity-100 transition-opacity duration-700
            p-4 mb-4 text-sm rounded-lg border
            bg-${color.bg} text-${color.text} border-${color.border}
            dark:bg-gray-800 dark:text-${color.darkText}">
            ${escapeHtml(msg)}
        </div>`;

        try { container.scrollIntoView({ behavior: 'smooth', block: 'start' }); } catch { }

        tableMsgTimeout = setTimeout(() => {
            const alertEl = container.firstElementChild;
            if (alertEl) {
                alertEl.classList.add('opacity-0');
                setTimeout(() => {
                    try {
                        alertEl.remove();
                        if (container.children.length === 0) {
                            container.style.display = 'none';
                        }
                    } catch { }
                }, 750);
            }
        }, disappearMs);
    }

    // ===================== Utilidades =====================

    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function getFlowbiteModal(modalEl) {
        if (!modalEl || typeof window !== 'object' || typeof window.Modal === 'undefined') return null;
        const opts = { backdrop: 'dynamic', closable: true };
        if (typeof Modal.getInstance === 'function') {
            const existing = Modal.getInstance(modalEl);
            if (existing) return existing;
        }
        if (typeof Modal.getOrCreateInstance === 'function') {
            return Modal.getOrCreateInstance(modalEl, opts);
        }
        try { return new Modal(modalEl, opts); } catch { return null; }
    }

    function abrirModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        try {
            const inst = getFlowbiteModal(modal);
            if (inst && typeof inst.show === 'function') {
                inst.show();
                return;
            }
        } catch { }

        modal.classList.remove('hidden');
        modal.setAttribute('aria-hidden', 'false');
    }

    function cerrarModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        try {
            const inst = getFlowbiteModal(modal);
            if (inst && typeof inst.hide === 'function') {
                inst.hide();
            }
        } catch { }

        modal.classList.add('hidden');
        modal.setAttribute('aria-hidden', 'true');
        document.querySelectorAll('[modal-backdrop]').forEach(b => b.remove());
        document.body.classList.remove('overflow-hidden');
    }

})();
