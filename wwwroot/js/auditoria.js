/**
 * ================================================
 * AUDITORIA.JS - FUNCIONALIDAD DE LA PÁGINA DE AUDITORÍA
 * ================================================
 */

(function () {
    'use strict';

    let searchTimeout = null;
    let currentSearchTerm = '';

    // =====================================
    // INICIALIZACIÓN DEL MÓDULO
    // =====================================
    window.PageModules = window.PageModules || {};
    window.PageModules.auditoria = {
        init: initializeAuditoriaPage
    };

    /**
     * Inicializa la funcionalidad específica de la página de Auditoría
     */
    function initializeAuditoriaPage() {
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
    // BÚSQUEDA DE LA TABLA
    // =====================================
    /**
     * Configura la búsqueda con debouncing
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
     * Realiza búsqueda en el servidor
     * @param {string} searchTerm - Término de búsqueda
     */
    function performServerSearch(searchTerm) {
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

        const url = `/Auditoria/SearchPartial?${params.toString()}`;

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const cont = document.getElementById('auditoria-table-container');
                cont.innerHTML = html;
                const cp = document.getElementById('current-page-value')?.value;
                if (cp) cont.dataset.currentPage = cp;
            })
            .catch(e => {
                console.error('Error en búsqueda:', e);
                showMessage('error', 'Error al realizar la búsqueda.');
            });
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

        // Si es la misma columna, cambiar dirección
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
     * Obtiene los parámetros de ordenamiento actuales
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
     * Recarga la tabla de auditoría con filtros y ordenamiento actuales
     * @param {number} page - Número de página
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

        // Orden actual
        const currentSort = getCurrentSort();
        params.set('pageNumber', page.toString());
        params.set('sortBy', currentSort.sortBy);
        params.set('sortOrder', currentSort.sortOrder);

        // Si hay búsqueda activa, mantener paginación y ordenamiento dentro del contexto de búsqueda
        let url;
        if (currentSearchTerm && currentSearchTerm.trim().length > 0) {
            params.set('searchTerm', currentSearchTerm.trim());
            url = `/Auditoria/SearchPartial?${params.toString()}`;
        } else {
            url = `/Auditoria/TablePartial?${params.toString()}`;
        }

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const cont = document.getElementById('auditoria-table-container');
                cont.innerHTML = html;
                const cp = document.getElementById('current-page-value')?.value;
                if (cp) cont.dataset.currentPage = cp;
                // Re-inicializar filtros/dropdowns tras reemplazo dinámico
                window.CommonUtils?.setupDefaultFilterForm?.();
                if (typeof initDropdowns === 'function') { initDropdowns(); }
            })
            .catch(e => {
                console.error('Error cargando la tabla:', e);
                showMessage('error', 'Error cargando la tabla.');
            });
    }

    /**
     * Obtiene la página currentTablePage de la tabla
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
        let container = document.getElementById('table-messages-container');

        if (!container) {
            container = document.createElement('div');
            container.id = 'table-messages-container';
            container.className = 'mb-4';

            const tableContainer = document.getElementById('auditoria-table-container');
            tableContainer.parentNode.insertBefore(container, tableContainer);
        }

        container.style.display = 'block';

        const color = type === 'success'
            ? { bg: 'green-50', text: 'green-800', darkText: 'green-400', border: 'green-300' }
            : type === 'info'
                ? { bg: 'blue-50', text: 'blue-800', darkText: 'blue-400', border: 'blue-300' }
                : { bg: 'red-50', text: 'red-800', darkText: 'red-400', border: 'red-300' };

        container.innerHTML = `<div id="table-inline-alert"
            class="table-inline-alert opacity-100 transition-opacity duration-700
                   p-4 mb-4 text-sm rounded-lg border
                   bg-${color.bg} text-${color.text} border-${color.border}
                   dark:bg-gray-800 dark:text-${color.darkText}">
            ${msg}
        </div>`;

        const alertEl = document.getElementById('table-inline-alert');
        if (!alertEl) return;

        try { 
            alertEl.scrollIntoView({ behavior: 'smooth', block: 'start' }); 
        } catch {}

        setTimeout(() => {
            alertEl.classList.add('opacity-0');
            setTimeout(() => {
                if (alertEl.parentElement) {
                    alertEl.remove();
                    if (container.children.length === 0) {
                        container.style.display = 'none';
                    }
                }
            }, 750);
        }, disappearMs);
    }

    // =====================================
    // FILTROS
    // =====================================
    /**
     * Configura el envío del formulario de filtros
     */
    function setupFilterFormSubmit() {
        const form = document.getElementById('filterForm');
        if (!form || form.dataset.submitSetup === 'true') return;

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            e.stopPropagation();

            // Asegurar que la paginación inicie en 1
            const pg = form.querySelector('input[name="pageNumber"]');
            if (pg) pg.value = '1';

            // Persistir término de búsqueda actual (si hay)
            const searchInput = document.getElementById('simple-search');
            if (searchInput) {
                currentSearchTerm = searchInput.value.trim();
            }

            // Cerrar el dropdown de filtros (si está abierto)
            const dd = document.getElementById('filterDropdown');
            if (dd) dd.classList.add('hidden');

            // Recargar tabla con filtros aplicados (y búsqueda/orden actuales)
            reloadAuditoriaTable(1);
            // Mensaje informativo
            showMessage('info', 'Filtros aplicados.');
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

                // Si Flowbite creó una instancia Popper, recalcular posición
                try {
                    const instance = window.Flowbite?.Dropdown?.getInstance(dropdown);
                    if (instance?.popper) {
                        instance.popper.update();
                    }
                } catch (e) {
                    console.warn('No se pudo actualizar el dropdown:', e);
                }
            }, 50); // pequeño delay para dejar que Popper mida antes de ajustar
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
     * Limpia filtros de la vista Auditoría y recarga la tabla
     * - Delegamos limpieza de checkboxes de estados al clearAllFilters global (no marca "Activo" en auditoría)
     * - Limpiamos otros campos (texto, number, date, search) excluyendo estados
     * - Reseteamos término de búsqueda interno
     * - Cerramos dropdown de filtros si está abierto
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

        // Restaurar estados según lógica global (en auditoría no fuerza "Activo")
        if (typeof window.clearAllFilters === 'function') {
            window.clearAllFilters();
        }

        // Limpiar búsqueda y estado interno
        const searchInput = document.getElementById('simple-search');
        if (searchInput) searchInput.value = '';
        currentSearchTerm = '';

        // Cerrar dropdown de filtros si está abierto
        const filterDropdown = document.getElementById('filterDropdown');
        if (filterDropdown && !filterDropdown.classList.contains('hidden')) {
            filterDropdown.classList.add('hidden');
        }

        // Limpiar parámetros de la URL
        if (window.history && typeof window.history.replaceState === 'function') {
            window.history.replaceState(null, '', window.location.pathname);
        }
        // Re-inicializar lógica de filtros tras limpieza (evita el primer clic perdido)
        window.CommonUtils?.setupDefaultFilterForm?.();
        if (typeof initDropdowns === 'function') { initDropdowns(); }
        // Recargar tabla página1
        reloadAuditoriaTable(1);

        // Mensaje informativo
        showMessage('info', 'Filtros restablecidos.');
    };

})();