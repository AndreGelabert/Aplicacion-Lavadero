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
    let currentSortBy = "Patente";
    let currentSortOrder = "asc";
    let searchTimeout;
    let vehiculoMsgTimeout = null;
    let tableMsgTimeout = null;
    
    // Listas para filtros dinámicos
    let marcasDisponibles = [];
    let coloresDisponibles = [];

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
        setupDynamicFilters();
        setupModals();
        checkEditMode();
        // Solo llamar una vez al inicio, no después de cada recarga
        // window.CommonUtils?.setupDefaultFilterForm();
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
        const accordion = document.getElementById('accordion-flush');
        
        // Solo mostrar el acordeón si realmente estamos en modo edición (viene de la URL)
        if (formTitle && formTitle.textContent.includes('Editando') && accordion && !accordion.classList.contains('hidden')) {
            const accordionBody = document.getElementById('accordion-flush-body-1');
            const accordionBtn = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
            
            if (accordionBody && accordionBtn) {
                // Abrir el acordeón automáticamente solo si el acordeón está visible
                if (accordionBody.classList.contains('hidden')) {
                    accordionBtn.click();
                }
            }
        }
    }

    // ===================== Búsqueda (debounce) =====================

    function setupSearchWithDebounce() {
        const searchInput = document.getElementById("simple-search");
        if (!searchInput) return;

        // Remover event listeners anteriores
        const newSearchInput = searchInput.cloneNode(true);
        searchInput.parentNode.replaceChild(newSearchInput, searchInput);

        // Inicializar estado con el valor actual del input (si vino del servidor)
        currentSearchTerm = newSearchInput.value?.trim() || '';

        newSearchInput.addEventListener('input', function () {
            const searchTerm = this.value.trim();

            if (searchTimeout) clearTimeout(searchTimeout);

            if (searchTerm === '') {
                // limpiar estado y volver a la tabla base
                currentSearchTerm = '';
                reloadVehiculoTable(1);
                return;
            }

            // Debouncing
            searchTimeout = setTimeout(() => {
                performServerSearch(searchTerm);
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

            // Cerrar el dropdown usando el botón de toggle
            const filterButton = document.getElementById('filterDropdownButton');
            if (filterButton) {
                filterButton.click();
            }

            reloadVehiculoTable(1);

            if (history.replaceState) history.replaceState(null, '', window.location.pathname);

            showTableMessage('info', 'Filtros aplicados.');
        });

        form.dataset.submitSetup = 'true';
    }

    function setupDynamicFilters() {
        // Cargar marcas y colores disponibles
        fetch('/Vehiculo/Index')
            .then(response => response.text())
            .then(html => {
                const parser = new DOMParser();
                const doc = parser.parseFromString(html, 'text/html');
                
                // Extraer datos del ViewBag (si están disponibles en data attributes)
                // Por ahora, cargaremos desde una llamada AJAX adicional si es necesario
            })
            .catch(error => console.error('Error cargando filtros:', error));

        // Setup búsqueda de marcas
        const marcaSearch = document.getElementById('marca-filter-search');
        if (marcaSearch) {
            marcaSearch.addEventListener('input', function() {
                filterMarcaList(this.value);
            });
        }

        // Setup búsqueda de colores
        const colorSearch = document.getElementById('color-filter-search');
        if (colorSearch) {
            colorSearch.addEventListener('input', function() {
                filterColorList(this.value);
            });
        }

        // Cargar listas iniciales
        loadMarcasYColores();
    }

    async function loadMarcasYColores() {
        try {
            const marcasElement = document.getElementById('marca-filter-list');
            const coloresElement = document.getElementById('color-filter-list');

            if (!marcasElement || !coloresElement) return;

            const response = await fetch('/Vehiculo/GetMarcasYColores');
            const data = await response.json();
            
            marcasDisponibles = data.marcas || [];
            coloresDisponibles = data.colores || [];

            renderMarcasList();
            renderColoresList();
        } catch (error) {
            console.error('Error cargando marcas y colores:', error);
        }
    }

    function renderMarcasList(filterText = '') {
        const container = document.getElementById('marca-filter-list');
        if (!container) return;

        let marcas = marcasDisponibles;
        if (filterText) {
            const lower = filterText.toLowerCase();
            marcas = marcas.filter(m => m.toLowerCase().includes(lower));
        }

        container.innerHTML = marcas.map(marca => `
            <label class="flex items-center">
                <input type="checkbox" name="marcas" value="${escapeHtml(marca)}"
                       class="w-4 h-4 text-blue-600 bg-gray-100 border-gray-300 rounded focus:ring-blue-500">
                <span class="ml-2 text-gray-900 dark:text-gray-100">${escapeHtml(marca)}</span>
            </label>
        `).join('');
    }

    function renderColoresList(filterText = '') {
        const container = document.getElementById('color-filter-list');
        if (!container) return;

        let colores = coloresDisponibles;
        if (filterText) {
            const lower = filterText.toLowerCase();
            colores = colores.filter(c => c.toLowerCase().includes(lower));
        }

        container.innerHTML = colores.map(color => `
            <label class="flex items-center">
                <input type="checkbox" name="colores" value="${escapeHtml(color)}"
                       class="w-4 h-4 text-blue-600 bg-gray-100 border-gray-300 rounded focus:ring-blue-500">
                <span class="ml-2 text-gray-900 dark:text-gray-100">${escapeHtml(color)}</span>
            </label>
        `).join('');
    }

    function filterMarcaList(searchText) {
        renderMarcasList(searchText);
    }

    function filterColorList(searchText) {
        renderColoresList(searchText);
    }

    window.clearVehiculoFilters = function () {
        const form = document.getElementById('filterForm');
        if (!form) return;

        // Limpiar TODOS los checkboxes
        form.querySelectorAll('input[type="checkbox"]').forEach(cb => {
            cb.checked = false;
        });

        // Marcar solo "Activo" en estados
        const activoCheckbox = form.querySelector('input[name="estados"][value="Activo"]');
        if (activoCheckbox) {
            activoCheckbox.checked = true;
        }

        // Limpiar búsqueda principal
        const searchInput = document.getElementById('simple-search');
        if (searchInput) searchInput.value = '';
        currentSearchTerm = '';

        // Limpiar búsquedas de filtros
        const marcaSearch = document.getElementById('marca-filter-search');
        if (marcaSearch) marcaSearch.value = '';
        const colorSearch = document.getElementById('color-filter-search');
        if (colorSearch) colorSearch.value = '';

        // Re-renderizar listas sin filtros
        renderMarcasList();
        renderColoresList();

        // Cerrar dropdown
        const filterButton = document.getElementById('filterDropdownButton');
        if (filterButton) {
            filterButton.click();
        }

        if (history.replaceState) history.replaceState({}, document.title, '/Vehiculo/Index');

        // NO llamar a setupDefaultFilterForm aquí, ya manejamos el estado manualmente
        // window.CommonUtils?.setupDefaultFilterForm?.();

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

                // Mostrar el acordeón completo
                const accordion = document.getElementById('accordion-flush');
                if (accordion) {
                    accordion.classList.remove('hidden');
                }

                const accordionBtn = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
                const accordionBody = document.getElementById("accordion-flush-body-1");

                if (accordionBody && accordionBtn) {
                    // Abrir el acordeón usando el botón
                    if (accordionBody.classList.contains('hidden')) {
                        accordionBtn.click();
                    }
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

    window.submitVehiculoAjax = function (form, event) {
        // Prevenir el comportamiento por defecto
        if (event) {
            event.preventDefault();
            event.stopPropagation();
        }

        // Prevenir doble envío
        const submitBtn = document.getElementById('submit-button');
        if (submitBtn && submitBtn.disabled) {
            return false;
        }

        // Deshabilitar botón
        if (submitBtn) {
            submitBtn.disabled = true;
            const originalText = submitBtn.innerHTML;
            submitBtn.innerHTML = `
                <svg class="animate-spin h-5 w-5 mr-2 inline" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 100-16 8 8 0 000 16zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
                Guardando...
            `;
            
            // Guardar el HTML original para restaurarlo
            submitBtn.dataset.originalHtml = originalText;
        }

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
            .then (result => {
                document.getElementById('vehiculo-form-container').innerHTML = result.html;

                if (result.valid) {
                    showFormMessage('success', result.msg || 'Vehículo actualizado correctamente.', 4000);
                    reloadVehiculoTable(getCurrentTablePage());

                    // Cerrar y ocultar el acordeón completo
                    setTimeout(() => {
                        const accordionBody = document.getElementById('accordion-flush-body-1');
                        const accordionBtn = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
                        const accordion = document.getElementById('accordion-flush');
                        
                        // Primero cerrar el body del acordeón si está abierto
                        if (accordionBody && accordionBtn && !accordionBody.classList.contains('hidden')) {
                            accordionBtn.click();
                        }
                        
                        // Luego ocultar todo el acordeón
                        setTimeout(() => {
                            if (accordion) {
                                accordion.classList.add('hidden');
                            }
                        }, 300); // Dar tiempo a la animación de cierre
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
                console.error('Error:', e);
                showFormMessage('error', 'Error de comunicación con el servidor.', 8000);
            })
            .finally(() => {
                // Re-habilitar botón
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = submitBtn.dataset.originalHtml || 'Guardar';
                }
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

    // ===================== Código actualizado =====================

    function performServerSearch(searchTerm = '') {
        // Persistir búsqueda activa
        currentSearchTerm = searchTerm;

        // Obtener filtros actuales
        const filterForm = document.getElementById('filterForm');
        const params = new URLSearchParams();

        if (filterForm) {
            const formData = new FormData(filterForm);
            for (const [key, value] of formData.entries()) {
                params.append(key, value);
            }
        }

        // Agregar término de búsqueda y ordenamiento
        const currentSort = getCurrentSort();
        params.set('searchTerm', searchTerm);
        params.set('pageNumber', '1');
        params.set('sortBy', currentSort.sortBy);
        params.set('sortOrder', currentSort.sortOrder);

        const url = `/Vehiculo/SearchPartial?${params.toString()}`;

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(r => r.text())
        .then(html => {
            const cont = document.getElementById('vehiculo-table-container');
            cont.innerHTML = html;
            const cp = document.getElementById('current-page-value')?.value;
            if (cp) cont.dataset.currentPage = cp;
        })
        .catch(e => {
            console.error('Error en búsqueda:', e);
            showTableMessage('error', 'Error al realizar la búsqueda.');
        });
    }

})();