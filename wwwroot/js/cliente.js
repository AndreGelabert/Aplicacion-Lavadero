/**
 * ================================================
 * CLIENTE.JS - FUNCIONALIDAD DE LA PÁGINA DE CLIENTES
 * ================================================
 * Responsabilidades:
 *  - Búsqueda con debounce y ordenamiento de tabla
 *  - Filtros y recarga parcial (tabla y formulario)
 *  - Formulario AJAX crear/actualizar
 *  - Selector dinámico de vehículos (estilo paquetes)
 *  - Gestión de activación/desactivación (modal de confirmación)
 *  - Creación rápida de vehículos desde modal
 */

(function () {
    'use strict';

    // ===================== Estado interno =====================
    let currentPage = 1;
    let currentSearchTerm = "";
    let currentSortBy = "Nombre";
    let currentSortOrder = "asc";
    let searchTimeout;
    let clienteMsgTimeout = null;
    let tableMsgTimeout = null;

    // Variables para gestión de vehículos
    let vehiculosSeleccionados = [];
    let vehiculosDisponibles = [];

    // ===================== Inicialización del módulo =====================
    window.PageModules = window.PageModules || {};
    window.PageModules.clientes = { init: initializeClientesPage };

    document.addEventListener('DOMContentLoaded', () => {
        try { window.PageModules?.clientes?.init(); }
        catch { initializeClientesPage(); }
    });

    /**
     * Inicializa el comportamiento principal de la página de Clientes
     */
    function initializeClientesPage() {
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
            if (accordion) accordion.classList.remove('hidden');
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
                reloadClienteTable();
                return;
            }

            searchTimeout = setTimeout(() => {
                currentSearchTerm = term;
                currentPage = 1;
                reloadClienteTable();
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
        reloadClienteTable();
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

    function reloadClienteTable(page) {
        if (page) currentPage = page;

        const params = buildFilterParams();
        params.set('searchTerm', currentSearchTerm);
        params.set('pageNumber', currentPage.toString());
        params.set('sortBy', currentSortBy);
        params.set('sortOrder', currentSortOrder);

        const url = `/Cliente/TablePartial?${params.toString()}`;

        fetch(url, { 
            headers: { 'X-Requested-With': 'XMLHttpRequest', 'Cache-Control': 'no-cache' }, 
            cache: 'no-store' 
        })
            .then(r => r.text())
            .then(html => {
                const container = document.getElementById("cliente-table-container");
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

    window.reloadClienteTable = reloadClienteTable;

    window.getCurrentTablePage = function () {
        return parseInt(document.getElementById('cliente-table-container')?.dataset.currentPage || '1');
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

            reloadClienteTable(1);

            if (history.replaceState) history.replaceState(null, '', window.location.pathname);

            showTableMessage('info', 'Filtros aplicados.');
        });

        form.dataset.submitSetup = 'true';
    }

    window.clearClienteFilters = function () {
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

        form.querySelectorAll('input[type="text"]').forEach(inp => inp.value = '');
        form.querySelectorAll('input[type="number"]').forEach(inp => inp.value = '');
        form.querySelectorAll('select').forEach(sel => sel.selectedIndex = 0);

        const searchInput = document.getElementById('simple-search');
        if (searchInput) searchInput.value = '';
        currentSearchTerm = '';

        document.getElementById('filterDropdown')?.classList.add('hidden');

        if (history.replaceState) history.replaceState({}, document.title, '/Cliente/Index');

        window.CommonUtils?.setupDefaultFilterForm?.();

        reloadClienteTable(1);
        showTableMessage('info', 'Filtros restablecidos.');
    };

    // ===================== Formulario =====================

    window.loadClienteForm = function (id) {
        const url = id ? `/Cliente/FormPartial?id=${id}` : "/Cliente/FormPartial";

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(response => response.text())
            .then(html => {
                document.getElementById("cliente-form-container").innerHTML = html;

                const isEdit = !!document.getElementById('Id')?.value;
                const titleSpan = document.getElementById('form-title');
                if (titleSpan) {
                    titleSpan.textContent = isEdit ? 'Editando Cliente' : 'Registrando Cliente';
                }

                setupVehiculoSelector();

                const accordionBtn = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
                const accordionBody = document.getElementById("accordion-flush-body-1");
                if (accordionBody?.classList.contains("hidden")) {
                    accordionBtn?.click();
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
            .catch(error => console.error('Error al cargar el formulario:', error));
    };

    window.submitClienteAjax = function (form) {
        // Validar que al menos haya un vehículo
        if (vehiculosSeleccionados.length === 0) {
            showFormMessage('error', 'Debe seleccionar al menos un vehículo para el cliente.');
            return false;
        }

        const formData = new FormData(form);

        // Agregar vehículos seleccionados
        formData.delete('VehiculosIds');
        vehiculosSeleccionados.forEach(v => {
            formData.append('VehiculosIds', v.id);
        });

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
                setupVehiculoSelector();

                const isEdit = !!document.getElementById('Id')?.value;
                const titleSpan = document.getElementById('form-title');
                if (titleSpan) {
                    titleSpan.textContent = isEdit ? 'Editando Cliente' : 'Registrando Cliente';
                }

                if (result.valid) {
                    showFormMessage('success', result.msg || 'Operación exitosa.', 4000);
                    reloadClienteTable(1);

                    setTimeout(() => {
                        document.getElementById('accordion-flush-body-1')?.classList.add('hidden');
                    }, 1500);
                } else {
                    const summary = document.getElementById('cliente-validation-summary');
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

    // ===================== Selector de Vehículos (Estilo Paquetes) =====================

    function setupVehiculoSelector() {
        loadVehiculosDisponibles();

        const searchInput = document.getElementById('vehiculo-search');
        if (searchInput) {
            searchInput.addEventListener('input', function () {
                filterVehiculosDropdown(this.value);
            });

            searchInput.addEventListener('focus', function () {
                showVehiculoDropdown();
            });
        }

        setupDropdownClickOutside();
    }

    async function loadVehiculosDisponibles() {
        try {
            const resp = await fetch('/Cliente/ObtenerVehiculosDisponibles');
            const data = await resp.json();

            if (data.success) {
                vehiculosDisponibles = data.vehiculos;

                // Cargar vehículos ya seleccionados
                const hiddenIds = document.getElementById('VehiculosIdsData')?.value;
                if (hiddenIds) {
                    const ids = hiddenIds.split(',').map(x => x.trim()).filter(x => x);
                    vehiculosSeleccionados = vehiculosDisponibles.filter(v => ids.includes(v.id));
                }

                updateVehiculosSeleccionadosList();
                renderVehiculosDropdown(vehiculosDisponibles);
            }
        } catch (error) {
            console.error('Error al cargar vehículos:', error);
        }
    }

    function renderVehiculosDropdown(vehiculos, filterText = '') {
        const target = document.getElementById('vehiculo-dropdown-content');
        if (!target) return;

        if (!Array.isArray(vehiculos) || vehiculos.length === 0) {
            target.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No hay vehículos disponibles</p>';
            return;
        }

        let lista = vehiculos;
        if (filterText) {
            const lower = filterText.toLowerCase();
            lista = lista.filter(v =>
                (v.patente && v.patente.toLowerCase().includes(lower)) ||
                (v.marca && v.marca.toLowerCase().includes(lower)) ||
                (v.modelo && v.modelo.toLowerCase().includes(lower))
            );
        }

        lista = lista.filter(v => !vehiculosSeleccionados.some(sel => sel.id === v.id));

        if (lista.length === 0) {
            target.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No se encontraron vehículos</p>';
            return;
        }

        let html = '';
        lista.forEach(v => {
            html += `<div class="px-2 py-2 hover:bg-gray-100 dark:hover:bg-gray-600 cursor-pointer"
                         onclick="selectVehiculoFromDropdown('${v.id}')">
                         <div class="text-sm font-medium text-gray-900 dark:text-white">${escapeHtml(v.patente)}</div>
                         <div class="text-xs text-gray-500 dark:text-gray-400">${escapeHtml(v.marca)} ${escapeHtml(v.modelo)} - ${escapeHtml(v.color)}</div>
                     </div>`;
        });

        target.innerHTML = html;
    }

    window.showVehiculoDropdown = function () {
        const dropdown = document.getElementById('vehiculo-dropdown');
        const searchInput = document.getElementById('vehiculo-search');
        if (dropdown && vehiculosDisponibles.length) {
            renderVehiculosDropdown(vehiculosDisponibles, searchInput?.value || '');
            dropdown.classList.remove('hidden');
        }
    };

    window.filterVehiculosDropdown = function (txt) {
        renderVehiculosDropdown(vehiculosDisponibles, txt);
        document.getElementById('vehiculo-dropdown')?.classList.remove('hidden');
    };

    window.selectVehiculoFromDropdown = function (id) {
        const v = vehiculosDisponibles.find(x => x.id === id);
        if (!v) return;

        vehiculosSeleccionados.push(v);
        updateVehiculosSeleccionadosList();
        renderVehiculosDropdown(vehiculosDisponibles, document.getElementById('vehiculo-search')?.value || '');

        const searchInput = document.getElementById('vehiculo-search');
        if (searchInput) searchInput.value = '';
    };

    window.removerVehiculoSeleccionado = function (id) {
        vehiculosSeleccionados = vehiculosSeleccionados.filter(v => v.id !== id);
        updateVehiculosSeleccionadosList();
        renderVehiculosDropdown(vehiculosDisponibles, document.getElementById('vehiculo-search')?.value || '');
    };

    function updateVehiculosSeleccionadosList() {
        const container = document.getElementById('vehiculos-seleccionados-container');
        const list = document.getElementById('vehiculos-seleccionados-list');
        if (!(container && list)) return;

        if (vehiculosSeleccionados.length === 0) {
            container.classList.add('hidden');
            list.innerHTML = '';
            return;
        }

        container.classList.remove('hidden');

        list.innerHTML = '<ul class="space-y-2">' +
            vehiculosSeleccionados.map((v, index) => {
                return `
                    <li class="flex items-center gap-3 p-2 bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-600 hover:border-blue-300 dark:hover:border-blue-500 transition-colors">
                        <div class="flex-shrink-0 w-8 h-8 flex items-center justify-center bg-blue-100 dark:bg-blue-900 text-blue-800 dark:text-blue-200 rounded-full font-bold text-sm">
                            ${index + 1}
                        </div>
                        <div class="flex-1 min-w-0">
                            <div class="font-medium text-gray-900 dark:text-white truncate">${escapeHtml(v.patente)}</div>
                            <div class="text-sm text-gray-500 dark:text-gray-400">${escapeHtml(v.marca)} ${escapeHtml(v.modelo)} (${escapeHtml(v.tipoVehiculo)})</div>
                        </div>
                        <button type="button"
                                onclick="removerVehiculoSeleccionado('${v.id}')"
                                class="flex-shrink-0 w-8 h-8 flex items-center justify-center bg-transparent rounded-md hover:border-red-200 dark:hover:border-red-700 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300"
                                title="Quitar vehículo">
                            <svg xmlns="http://www.w3.org/2000/svg" fill="currentColor" class="w-5 h-5" viewBox="0 0 24 24">
                                <path fill-rule="evenodd" d="M12 2.25c-5.385 0-9.75 4.365-9.75 9.75s4.365 9.75 9.75 9.75 9.75-4.365 9.75-9.75S17.385 2.25 12 2.25Zm-1.72 6.97a.75.75 0 1 0-1.06 1.06L10.94 12l-1.72 1.72a.75.75 0 1 0 1.06 1.06L12 13.06l1.72 1.72a.75.75 0 1 0 1.06-1.06L13.06 12l1.72-1.72a.75.75 0 1 0-1.06-1.06L12 10.94l-1.72-1.72Z" clip-rule="evenodd"/>
                            </svg>
                        </button>
                    </li>
                `;
            }).join('') +
            '</ul>';
    }

    function setupDropdownClickOutside() {
        document.addEventListener('click', (e) => {
            const dropdown = document.getElementById('vehiculo-dropdown');
            const searchInput = document.getElementById('vehiculo-search');
            const addBtn = document.getElementById('add-vehiculo-btn');
            
            if (dropdown && !dropdown.contains(e.target) && 
                e.target !== searchInput && e.target !== addBtn) {
                dropdown.classList.add('hidden');
            }
        });
    }

    // ===================== Modal de Creación Rápida de Vehículo =====================

    window.openQuickCreateVehiculoModal = function () {
        fetch('/Vehiculo/FormPartial')
            .then(response => response.text())
            .then(html => {
                let modalContainer = document.getElementById("quick-create-modal-container");
                if (!modalContainer) {
                    modalContainer = document.createElement("div");
                    modalContainer.id = "quick-create-modal-container";
                    modalContainer.className = "fixed inset-0 z-[60] flex items-center justify-center bg-black bg-opacity-50 hidden";
                    document.body.appendChild(modalContainer);
                }

                modalContainer.innerHTML = `
                    <div class="relative w-full max-w-3xl bg-white rounded-lg shadow dark:bg-gray-800 p-6 m-4 max-h-[90vh] overflow-y-auto">
                        <div class="flex justify-between items-center mb-4 border-b pb-3 dark:border-gray-600">
                            <h3 class="text-xl font-semibold text-gray-900 dark:text-white">Registrar Nuevo Vehículo</h3>
                            <button onclick="closeQuickCreateModal()" class="text-gray-400 hover:text-gray-900 dark:hover:text-white">
                                <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                                </svg>
                            </button>
                        </div>
                        <div id="quick-vehiculo-form-content">
                            ${html}
                        </div>
                    </div>
                `;

                modalContainer.classList.remove("hidden");

                const form = modalContainer.querySelector("form");
                if (form) {
                    form.onsubmit = function (e) {
                        e.preventDefault();
                        submitQuickVehiculo(this);
                        return false;
                    };

                    const cancelBtn = form.querySelector("#clear-button");
                    if (cancelBtn) {
                        cancelBtn.onclick = function(e) {
                            e.preventDefault();
                            closeQuickCreateModal();
                            return false;
                        };
                    }
                }
            })
            .catch(error => console.error('Error al cargar form vehiculo:', error));
    };

    window.closeQuickCreateModal = function () {
        const modal = document.getElementById("quick-create-modal-container");
        if (modal) modal.classList.add("hidden");
    };

    function submitQuickVehiculo(form) {
        const formData = new FormData(form);
        
        // Obtener el cliente ID si estamos en modo edición
        const clienteId = document.getElementById('Id')?.value;
        if (clienteId) {
            formData.set('ClienteId', clienteId);
        }

        fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(response => {
                const isValid = response.headers.get("X-Form-Valid") === "true";
                const message = response.headers.get("X-Form-Message");
                return response.text().then(html => ({ isValid, html, message }));
            })
            .then(result => {
                if (result.isValid) {
                    closeQuickCreateModal();
                    
                    // Recargar la lista de vehículos disponibles
                    loadVehiculosDisponibles();
                    
                    showFormMessage('success', result.message || 'Vehículo registrado correctamente. Selecciónelo en la lista.', 4000);
                } else {
                    // Mostrar errores en el modal
                    document.getElementById("quick-vehiculo-form-content").innerHTML = result.html;
                    
                    const newForm = document.getElementById("quick-vehiculo-form-content").querySelector("form");
                    if (newForm) {
                        newForm.onsubmit = function (e) {
                            e.preventDefault();
                            submitQuickVehiculo(this);
                            return false;
                        };
                        
                        const cancelBtn = newForm.querySelector("#clear-button");
                        if (cancelBtn) {
                            cancelBtn.onclick = function(e) {
                                e.preventDefault();
                                closeQuickCreateModal();
                                return false;
                            };
                        }
                    }
                }
            })
            .catch(error => {
                console.error('Error:', error);
                showFormMessage('error', 'Error al registrar el vehículo.', 5000);
            });
    }

    // ===================== Modales =====================

    function setupModals() {
        setupTipoDocumentoModal();
        setupDeleteModal();
    }

    function setupTipoDocumentoModal() {
        const form = document.getElementById("formCrearTipoDocumento");
        if (!form || form.dataset.setup === 'true') return;

        form.addEventListener("submit", async function (e) {
            e.preventDefault();
            e.stopPropagation();
            await handleCrearTipoDocumento(this);
        });

        form.dataset.setup = 'true';
    }

    async function handleCrearTipoDocumento(form) {
        const nombreTipo = document.getElementById('nombreTipoDocumento')?.value?.trim();
        if (!nombreTipo) {
            showTableMessage('error', 'El nombre del tipo de documento es obligatorio.');
            return;
        }

        try {
            const formData = new FormData(form);
            const response = await fetch(form.action, {
                method: 'POST',
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' },
                body: formData
            });

            const { ok, data } = await parseJsonSafe(response);
            const success = data?.success ?? ok;
            const message = data?.message ?? (success ? 'Tipo de documento creado.' : 'No se pudo crear el tipo de documento.');

            if (success) {
                if (data?.tipos) actualizarDropdownTipos('TipoDocumento', data.tipos, nombreTipo);
                cerrarModal('tipoDocumentoModal');
                form.reset();
                showTableMessage('success', message);
            } else {
                showTableMessage('error', message);
            }
        } catch (error) {
            console.error('Error:', error);
            showTableMessage('error', 'Error al crear el tipo de documento.');
        }
    }

    function actualizarDropdownTipos(selectId, tipos, valorSeleccionado) {
        const select = document.getElementById(selectId);
        if (!select) return;

        select.innerHTML = '';

        tipos.forEach(tipo => {
            const option = document.createElement('option');
            option.value = tipo;
            option.textContent = tipo;
            if (tipo === valorSeleccionado) {
                option.selected = true;
            }
            select.appendChild(option);
        });
    }

    function setupDeleteModal() {
        const form = document.getElementById("formEliminarCliente");
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
                    cerrarModal('clienteConfirmModal');
                    if (data.success) {
                        showTableMessage('success', data.message);
                        reloadClienteTable(getCurrentTablePage());
                    } else {
                        showTableMessage('error', data.message);
                    }
                })
                .catch(error => {
                    console.error('Error:', error);
                    cerrarModal('clienteConfirmModal');
                    showTableMessage('error', 'Error al procesar la solicitud.');
                });
        });

        form.dataset.setup = 'true';
    }

    window.openClienteConfirmModal = function (tipoAccion, id, nombre) {
        const modalId = 'clienteConfirmModal';
        const modal = document.getElementById(modalId);
        const title = document.getElementById('clienteConfirmTitle');
        const message = document.getElementById('clienteConfirmMessage');
        const idInput = document.getElementById('clienteConfirmId') || document.getElementById('idClienteEliminar');
        const submitBtn = document.getElementById('clienteConfirmSubmit');
        const form = document.getElementById('clienteConfirmForm') || document.getElementById('formEliminarCliente');
        const iconWrapper = document.getElementById('clienteConfirmIconWrapper');
        const icon = document.getElementById('clienteConfirmIcon');

        const esDesactivar = tipoAccion === 'desactivar';

        if (title) title.textContent = esDesactivar ? 'Desactivar Cliente' : 'Reactivar Cliente';
        
        if (message) {
            message.innerHTML = esDesactivar
                ? '¿Confirma desactivar el cliente <strong>' + escapeHtml(nombre) + '</strong>?'
                : '¿Confirma reactivar el cliente <strong>' + escapeHtml(nombre) + '</strong>?';
        }

        if (idInput) idInput.value = id;
        
        if (form) {
            form.action = esDesactivar ? '/Cliente/DeactivateCliente' : '/Cliente/ReactivateCliente';
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

    window.closeClienteConfirmModal = function () {
        cerrarModal('clienteConfirmModal');
    };

    window.submitClienteEstado = function (form) {
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
                closeClienteConfirmModal();
                if (data.success) {
                    const accion = form.action.includes('Deactivate') ? 'desactivado' : 'reactivado';
                    showTableMessage('success', `Cliente ${accion} correctamente.`);
                } else {
                    showTableMessage('error', data.message || 'No se pudo completar la acción.');
                }
                reloadClienteTable(getCurrentTablePage());
            })
            .catch(err => {
                console.error('submitClienteEstado error:', err);
                closeClienteConfirmModal();
                showTableMessage('error', 'No se pudo completar la acción.');
            });
        return false;
    };

    // Ver vehículos del cliente
    window.verVehiculos = async function (clienteId) {
        try {
            const resp = await fetch(`/Cliente/GetVehiculosCliente?clienteId=${clienteId}`);
            const data = await resp.json();

            if (data.success && data.vehiculos) {
                mostrarModalVehiculos(data.vehiculos);
            } else {
                showTableMessage('info', 'No se pudieron cargar los vehículos del cliente.');
            }
        } catch (error) {
            console.error('Error:', error);
            showTableMessage('error', 'Error al cargar los vehículos.');
        }
    };

    function mostrarModalVehiculos(vehiculos) {
        let modalContainer = document.getElementById("ver-vehiculos-modal");
        if (!modalContainer) {
            modalContainer = document.createElement("div");
            modalContainer.id = "ver-vehiculos-modal";
            modalContainer.className = "fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50 hidden";
            document.body.appendChild(modalContainer);
        }

        let html = '<div class="relative w-full max-w-2xl bg-white rounded-lg shadow dark:bg-gray-800 p-6 m-4">';
        html += '<div class="flex justify-between items-center mb-4 border-b pb-3 dark:border-gray-600">';
        html += '<h3 class="text-xl font-semibold text-gray-900 dark:text-white">Vehículos del Cliente</h3>';
        html += '<button onclick="closeVerVehiculosModal()" class="text-gray-400 hover:text-gray-900 dark:hover:text-white">';
        html += '<svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path></svg>';
        html += '</button></div>';
        html += '<div class="space-y-2">';

        if (vehiculos.length === 0) {
            html += '<p class="text-center text-gray-500 dark:text-gray-400 py-4">Este cliente no tiene vehículos asociados.</p>';
        } else {
            vehiculos.forEach(v => {
                html += `<div class="p-3 bg-gray-50 dark:bg-gray-700 rounded-lg border border-gray-200 dark:border-gray-600">`;
                html += `<div class="flex justify-between items-center">`;
                html += `<div>`;
                html += `<div class="font-medium text-gray-900 dark:text-white">${escapeHtml(v.patente)}</div>`;
                html += `<div class="text-sm text-gray-500 dark:text-gray-400">${escapeHtml(v.tipoVehiculo)} - ${escapeHtml(v.marca)} ${escapeHtml(v.modelo)}</div>`;
                html += `<div class="text-xs text-gray-400 dark:text-gray-500">Color: ${escapeHtml(v.color)}</div>`;
                html += `</div>`;
                
                const estadoClass = v.estado === 'Activo' 
                    ? 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300'
                    : 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-300';
                html += `<span class="px-2 py-1 text-xs font-medium ${estadoClass} rounded">${escapeHtml(v.estado)}</span>`;
                html += `</div></div>`;
            });
        }

        html += '</div></div>';
        modalContainer.innerHTML = html;
        modalContainer.classList.remove("hidden");
    }

    window.closeVerVehiculosModal = function () {
        const modal = document.getElementById("ver-vehiculos-modal");
        if (modal) modal.classList.add("hidden");
    };

    // ===================== Mensajería =====================

    function showFormMessage(type, message, disappearMs = 5000) {
        const container = document.getElementById('ajax-form-messages');
        if (!container) return;

        if (clienteMsgTimeout) {
            clearTimeout(clienteMsgTimeout);
            clienteMsgTimeout = null;
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

        clienteMsgTimeout = setTimeout(() => {
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

            const tableContainer = document.getElementById('cliente-table-container');
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

    async function parseJsonSafe(response) {
        try {
            const ct = response.headers.get('content-type') || '';
            if (ct.includes('application/json')) {
                const data = await response.json();
                return { ok: response.ok, data };
            }
            const text = await response.text();
            if (!text) return { ok: response.ok, data: null };
            try {
                return { ok: response.ok, data: JSON.parse(text) };
            } catch {
                return { ok: response.ok, data: null };
            }
        } catch {
            return { ok: response.ok, data: null };
        }
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
