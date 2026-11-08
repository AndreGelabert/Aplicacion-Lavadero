/**
 * ================================================
 * AUDITORIA.JS - FUNCIONALIDAD DE LA P�GINA DE AUDITOR�A
 * ================================================
 */

(function () {
    'use strict';

    let searchTimeout = null;
    let currentSearchTerm = '';
    let tableSequence = 0;
    let detailRequest = null;
    let detailSequence = 0;
    let detailTrigger = null;

    function setupAuditDetail() {
        const dialog = document.getElementById('audit-detail-dialog');
        if (!dialog || dialog.dataset.initialized) return;
        dialog.dataset.initialized = 'true';
        const status = document.getElementById('audit-detail-status');
        const meta = document.getElementById('audit-detail-meta');
        const rows = document.getElementById('audit-detail-rows');
        document.getElementById('audit-detail-close').addEventListener('click', () => dialog.close());
        dialog.addEventListener('close', () => {
            detailSequence++;
            detailRequest?.abort();
            detailTrigger?.focus();
        });
        document.addEventListener('click', async event => {
            const button = event.target.closest('[data-audit-detail-id]');
            if (!button) return;
            detailTrigger = button;
            detailRequest?.abort();
            detailRequest = new AbortController();
            const sequence = ++detailSequence;
            status.textContent = 'Cargando detalle…';
            meta.replaceChildren();
            rows.replaceChildren();
            if (!dialog.open) dialog.showModal();
            try {
                const response = await Peticiones.enviar(`/Auditoria/Detalle?id=${encodeURIComponent(button.dataset.auditDetailId)}`, { signal: detailRequest.signal, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                if (!response.ok) throw new Error('detail unavailable');
                const detail = await response.json();
                if (sequence !== detailSequence || !dialog.open) return;
                for (const [label, value] of [['Fecha', detail.timestamp], ['Usuario', detail.userEmail || detail.userId], ['Acción', detail.action], ['Tipo de objeto', detail.targetType], ['Objeto histórico', detail.nombreHistorico], ['ID del objeto', detail.targetId]]) {
                    if (!value) continue;
                    const dt = document.createElement('dt');
                    const dd = document.createElement('dd');
                    dt.textContent = `${label}:`;
                    dd.textContent = value;
                    meta.appendChild(dt);
                    meta.appendChild(dd);
                }
                if (detail.antes == null || detail.despues == null) {
                    status.textContent = 'Detalle histórico no disponible para este registro.';
                } else if (detail.comparacionNoAplica === true) {
                    status.textContent = 'La comparación no aplica a eventos de sesión.';
                } else {
                    const fields = new Set([...Object.keys(detail.antes), ...Object.keys(detail.despues)]);
                    for (const field of fields) {
                        const before = Object.hasOwn(detail.antes, field) ? detail.antes[field] : null;
                        const after = Object.hasOwn(detail.despues, field) ? detail.despues[field] : null;
                        if (before === after) continue;
                        const row = document.createElement('tr');
                        for (const value of [field, before, after]) {
                            const cell = document.createElement('td');
                            cell.textContent = value == null ? '—' : value;
                            row.appendChild(cell);
                        }
                        rows.appendChild(row);
                    }
                    status.textContent = rows.children.length ? '' : 'No hay cambios registrados.';
                }
            } catch (error) {
                if (sequence === detailSequence && dialog.open && error.name !== 'AbortError') status.textContent = 'No se pudo cargar el detalle de auditoría.';
            }
        });
    }

    // =====================================
    // INICIALIZACI�N DEL M�DULO
    // =====================================
    window.PageModules = window.PageModules || {};
    window.PageModules.auditoria = {
        init: initializeAuditoriaPage
    };

    /**
     * Inicializa la funcionalidad espec�fica de la p�gina de Auditor�a
     */
    function initializeAuditoriaPage() {
        setupAuditDetail();
        setupSearchWithDebounce();
        setupFilterFormSubmit();
        window.CommonUtils?.setupDefaultFilterForm();
    }

    // Asegurar que init se ejecute al cargar el DOM
    document.addEventListener('DOMContentLoaded', () => {
        try {
            window.PageModules?.auditoria?.init();
        } catch (e) {
            initializeAuditoriaPage();
        }
    });

    // =====================================
    // B�SQUEDA DE LA TABLA
    // =====================================
    /**
     * Configura la b�squeda con debouncing
     */
    function setupSearchWithDebounce() {
        const searchInput = document.getElementById('simple-search');
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
                reloadAuditoriaTable(1);
                return;
            }

            searchTimeout = setTimeout(() => {
                performServerSearch(searchTerm);
            }, 500);
        });
    }

    /**
     * Realiza b�squeda en el servidor
     * @param {string} searchTerm - T�rmino de b�squeda
     */
    function performServerSearch(searchTerm) {
        // Persistir b�squeda activa
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

        // Agregar t�rmino de b�squeda y ordenamiento
        const currentSort = getCurrentSort();
        params.set('searchTerm', searchTerm);
        params.set('pageNumber', '1');
        params.set('sortBy', currentSort.sortBy);
        params.set('sortOrder', currentSort.sortOrder);

        const url = `/Auditoria/SearchPartial?${params.toString()}`;

        applyAuditoriaTable(url, false);
    }

    // =====================================
    // ORDENAMIENTO DE TABLA
    // =====================================
    /**
     * Maneja el ordenamiento de la tabla
     * @param {string} sortBy - Campo por el cual ordenar
     */
    function sortTable(sortBy) {
        // Obtener valores actuales
        const currentSortBy = document.getElementById('current-sort-by')?.value || 'Timestamp';
        const currentSortOrder = document.getElementById('current-sort-order')?.value || 'desc';

        let newSortOrder = 'asc';

        // Si es la misma columna, cambiar direcci�n
        if (currentSortBy === sortBy) {
            newSortOrder = currentSortOrder === 'asc' ? 'desc' : 'asc';
        }

        // Actualizar inputs ocultos
        const sortByInput = document.getElementById('current-sort-by');
        const sortOrderInput = document.getElementById('current-sort-order');

        if (sortByInput) sortByInput.value = sortBy;
        if (sortOrderInput) sortOrderInput.value = newSortOrder;

        // Recargar tabla
        reloadAuditoriaTable(1);
    }

    /**
     * Obtiene los par�metros de ordenamiento actuales
     */
    function getCurrentSort() {
        const sortByInput = document.getElementById('current-sort-by');
        const sortOrderInput = document.getElementById('current-sort-order');

        return {
            sortBy: sortByInput?.value || 'Timestamp',
            sortOrder: sortOrderInput?.value || 'desc'
        };
    }

    // =====================================
    // RECARGA DE TABLA CON ORDENAMIENTO
    // =====================================
    /**
     * Recarga la tabla de auditor�a con filtros y ordenamiento actuales
     * @param {number} page - N�mero de p�gina
     */
    function reloadAuditoriaTable(page) {
        // Obtener filtros actuales del formulario
        const filterForm = document.getElementById('filterForm');
        const params = new URLSearchParams();

        if (filterForm) {
            const formData = new FormData(filterForm);
            for (const [key, value] of formData.entries()) {
                params.append(key, value);
            }
        }

        const fechaInicio = params.get('fechaInicio');
        const fechaFin = params.get('fechaFin');
        if (fechaInicio && fechaFin && fechaInicio > fechaFin) {
            showMessage('error', 'La fecha Desde no puede ser posterior a la fecha Hasta.');
            return;
        }

        // Orden actual
        const currentSort = getCurrentSort();
        params.set('pageNumber', page.toString());
        params.set('sortBy', currentSort.sortBy);
        params.set('sortOrder', currentSort.sortOrder);

        // Si hay b�squeda activa, mantener paginaci�n y ordenamiento dentro del contexto de b�squeda
        let url;
        if (currentSearchTerm && currentSearchTerm.trim().length > 0) {
            params.set('searchTerm', currentSearchTerm.trim());
            url = `/Auditoria/SearchPartial?${params.toString()}`;
        } else {
            url = `/Auditoria/TablePartial?${params.toString()}`;
        }

        return applyAuditoriaTable(url, true);
    }

    function applyAuditoriaTable(url, initialize) {
        const sequence = ++tableSequence;
        return Peticiones.filtrar('auditoria', async () => {
            const html = await Peticiones.texto(url);
            if (sequence !== tableSequence) throw new DOMException('Obsolete', 'AbortError');
            const cont = document.getElementById('auditoria-table-container');
            cont.innerHTML = html;
            const cp = document.getElementById('current-page-value')?.value;
            if (cp) cont.dataset.currentPage = cp;
            if (initialize) {
                window.CommonUtils?.setupDefaultFilterForm?.();
                if (typeof initDropdowns === 'function') initDropdowns();
            }
        });
    }

    /**
     * Obtiene la p�gina currentTablePage de la tabla
     */
    function getCurrentTablePage() {
        return parseInt(document.getElementById('auditoria-table-container')?.dataset.currentPage || '1');
    }

    // =====================================
    // MENSAJES
    // =====================================
    /**
     * Muestra un mensaje en la parte superior de la tabla
     * @param {string} type - Tipo de mensaje ('success', 'info', 'error')
     * @param {string} msg - Mensaje a mostrar
     * @param {number} disappearMs - Milisegundos para auto-ocultar
     */
    function showMessage(type, msg, disappearMs = 5000) {
        return window.Notificaciones.mostrar(msg, type);
    }

    // =====================================
    // FILTROS
    // =====================================
    /**
     * Configura el env�o del formulario de filtros
     */
    function setupFilterFormSubmit() {
        const form = document.getElementById('filterForm');
        if (!form || form.dataset.submitSetup === 'true') return;

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            e.stopPropagation();

            // Asegurar que la paginaci�n inicie en 1
            const pg = form.querySelector('input[name="pageNumber"]');
            if (pg) pg.value = '1';

            // Persistir t�rmino de b�squeda actual (si hay)
            const searchInput = document.getElementById('simple-search');
            if (searchInput) {
                currentSearchTerm = searchInput.value.trim();
            }

            // Cerrar el dropdown de filtros (si est� abierto)
            const dd = document.getElementById('filterDropdown');
            if (dd) dd.classList.add('hidden');

            // Recargar tabla con filtros aplicados (y b�squeda/orden actuales)
            reloadAuditoriaTable(1);

        });

        form.dataset.submitSetup = 'true';
    }
    document.addEventListener('DOMContentLoaded', () => {
        const dropdownButton = document.getElementById('filterDropdownButton');
        const dropdown = document.getElementById('filterDropdown');

        if (!dropdownButton || !dropdown) return;

        dropdownButton.addEventListener('click', () => {
            // Esperar al final del ciclo de render
            setTimeout(() => {
                // Forzar que el dropdown se adapte a su contenido
                dropdown.style.maxHeight = 'fit-content';
                dropdown.style.height = 'fit-content';
                dropdown.style.overflowY = 'visible';

                // Si Flowbite cre� una instancia Popper, recalcular posici�n
                try {
                    const instance = window.Flowbite?.Dropdown?.getInstance(dropdown);
                    if (instance?.popper) {
                        instance.popper.update();
                    }
                } catch (e) {
                    console.warn('No se pudo actualizar el dropdown:', e);
                }
            }, 50); // peque�o delay para dejar que Popper mida antes de ajustar
        });
    });

    // =====================================
    // FUNCIONES GLOBALES
    // =====================================
    window.sortTable = function (sortBy) {
        sortTable(sortBy);
    };

    window.reloadAuditoriaTable = function (page) {
        reloadAuditoriaTable(page);
    };

    window.getCurrentTablePage = function () {
        return getCurrentTablePage();
    };

    /**
     * Limpia filtros de la vista Auditor�a y recarga la tabla
     * - Delegamos limpieza de checkboxes de estados al clearAllFilters global (no marca "Activo" en auditor�a)
     * - Limpiamos otros campos (texto, number, date, search) excluyendo estados
     * - Reseteamos t�rmino de b�squeda interno
     * - Cerramos dropdown de filtros si est� abierto
     * - Limpiamos query string de la URL
     */
    window.clearAuditoriaFilters = function () {
        const filterForm = document.getElementById('filterForm');

        if (filterForm) {
            // Limpiar inputs de texto / number / date / search que NO sean los de estados
            filterForm.querySelectorAll('input').forEach(inp => {
                const type = (inp.type || '').toLowerCase();
                if (inp.name === 'estados') return; // dejar estados para clearAllFilters
                if (['hidden', 'submit', 'button', 'checkbox', 'radio'].includes(type)) return;
                if (['text', 'number', 'date', 'search'].includes(type)) inp.value = '';
            });

            // Limpiar checkboxes distintos a estados (si existieran)
            filterForm.querySelectorAll('input[type="checkbox"]').forEach(cb => {
                if (cb.name !== 'estados') cb.checked = false;
            });

            // Limpiar selects que no sean de estados (si existieran)
            filterForm.querySelectorAll('select').forEach(sel => {
                sel.selectedIndex =0;
            });
        }

        // Restaurar estados seg�n l�gica global (en auditor�a no fuerza "Activo")
        if (typeof window.clearAllFilters === 'function') {
            window.clearAllFilters();
        }

        // Limpiar b�squeda y estado interno
        const searchInput = document.getElementById('simple-search');
        if (searchInput) searchInput.value = '';
        currentSearchTerm = '';

        // Cerrar dropdown de filtros si est� abierto
        const filterDropdown = document.getElementById('filterDropdown');
        if (filterDropdown && !filterDropdown.classList.contains('hidden')) {
            filterDropdown.classList.add('hidden');
        }

        // Limpiar par�metros de la URL
        if (window.history && typeof window.history.replaceState === 'function') {
            window.history.replaceState(null, '', window.location.pathname);
        }
        // Re-inicializar l�gica de filtros tras limpieza (evita el primer clic perdido)
        window.CommonUtils?.setupDefaultFilterForm?.();
        if (typeof initDropdowns === 'function') { initDropdowns(); }
        // Recargar tabla p�gina1
        reloadAuditoriaTable(1);


    };

})();