/**
 * ================================================
 * AUDITORIA.JS - FUNCIONALIDAD DE LA PÁGINA DE AUDITORÍA
 * ================================================
 */

(function () {
    'use strict';

    let searchTimeout = null;

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

        newSearchInput.addEventListener('input', function () {
            const searchTerm = this.value.trim();

            // Cancelar timeout anterior
            if (searchTimeout) {
                clearTimeout(searchTimeout);
            }

            // Si está vacío, recargar sin búsqueda
            if (searchTerm === '') {
                reloadAuditoriaTable(1);
                return;
            }

            // Debouncing: esperar 500ms después de que el usuario deje de escribir
            searchTimeout = setTimeout(() => {
                performServerSearch(searchTerm);
            }, 500);
        });
    }

    /**
     * Realiza búsqueda en el servidor
     */
    function performServerSearch(searchTerm) {
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

        // Obtener ordenamiento actual
        const currentSort = getCurrentSort();
        
        // Asegurar que se incluyan todos los parámetros
        params.set('pageNumber', page.toString());
        params.set('sortBy', currentSort.sortBy);
        params.set('sortOrder', currentSort.sortOrder);

        const url = `/Auditoria/TablePartial?${params.toString()}`;
        
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
     * Obtiene la página actual de la tabla
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