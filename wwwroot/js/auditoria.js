/**
 * ================================================
 * AUDITORIA.JS - FUNCIONALIDAD DE LA P�GINA DE AUDITOR�A
 * ================================================
 */

(function () {
    'use strict';

    let searchTimeout = null;
    let currentSearchTerm = '';

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

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const cont = document.getElementById('auditoria-table-container');
                cont.innerHTML = html;
                const cp = document.getElementById('current-page-value')?.value;
                if (cp) cont.dataset.currentPage = cp;
            })
            .catch(e => {
                console.error('Error en b�squeda:', e);
                showMessage('error', 'Error al realizar la b�squeda.');
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

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const cont = document.getElementById('auditoria-table-container');
                cont.innerHTML = html;
                const cp = document.getElementById('current-page-value')?.value;
                if (cp) cont.dataset.currentPage = cp;
            })
            .catch(e => {
                console.error('Error cargando la tabla:', e);
                showMessage('error', 'Error cargando la tabla.');
            });
    }

    /**
     * Obtiene la p�gina actual de la tabla
     */
    function getCurrentTablePage() {
        return parseInt(document.getElementById('auditoria-table-container')?.dataset.currentPage || '1');
    }

    // =====================================
    // MENSAJES
    // =====================================
    /**
     * Muestra un mensaje en la parte superior de la tabla
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

})();