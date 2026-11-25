(() => {
    let currentSearchTerm = '';
    let searchTimeout = null;
    let tableSequence = 0;

    function notify(msg, type = 'info') {
        if (typeof window.Notificaciones !== 'undefined' && typeof window.Notificaciones.mostrar === 'function') {
            window.Notificaciones.mostrar(msg, type);
        } else if (typeof window.showNotification === 'function') {
            window.showNotification(msg, type);
        } else if (window.CommonUtils?.showNotification) {
            window.CommonUtils.showNotification(msg, type);
        }
    }

    function hideFilterDropdown() {
        const dropdown = document.getElementById('filterDropdown');
        if (dropdown) {
            dropdown.classList.add('hidden');
            dropdown.removeAttribute?.('data-popper-placement');
            if (dropdown.style) {
                dropdown.style.removeProperty?.('position');
                dropdown.style.removeProperty?.('inset');
                dropdown.style.removeProperty?.('margin');
                dropdown.style.removeProperty?.('transform');
            }
        }
    }

    function appendFormDataToParams(params, form) {
        const formData = new FormData(form);
        for (const [key, value] of formData.entries()) {
            if (value !== null && value !== undefined && value.toString().trim() !== '') {
                params.append(key, value.toString().trim());
            }
        }
    }

    function reloadTurnoTable(page) {
        const filterForm = document.getElementById('filterForm');
        const params = new URLSearchParams();

        if (filterForm) {
            appendFormDataToParams(params, filterForm);
        }

        const currentSortBy = document.getElementById('current-sort-by')?.value || 'Fecha';
        const currentSortOrder = document.getElementById('current-sort-order')?.value || 'desc';

        params.set('pageNumber', (page || 1).toString());
        params.set('sortBy', currentSortBy);
        params.set('sortOrder', currentSortOrder);

        const searchInput = document.getElementById('simple-search');
        const searchVal = searchInput?.value?.trim() || currentSearchTerm;
        if (searchVal) {
            params.set('searchTerm', searchVal);
        }

        const url = `/Turnos/TablePartial?${params.toString()}`;
        const sequence = ++tableSequence;

        const executeFetch = () => {
            const req = window.Peticiones?.enviar
                ? window.Peticiones.enviar(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                : fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });

            return req
                .then(response => {
                    if (!response.ok) throw new Error('Error al cargar la tabla de turnos');
                    return response.text();
                })
                .then(html => {
                    if (sequence !== tableSequence) return;
                    const container = document.getElementById('turno-table-container');
                    if (container) {
                        container.innerHTML = html;
                    }
                    if (container?.querySelector?.('[data-dropdown-toggle]') && typeof initDropdowns === 'function') {
                        initDropdowns();
                    }
                });
        };

        if (window.Peticiones?.filtrar) {
            return window.Peticiones.filtrar('turno', executeFetch);
        } else {
            return executeFetch().catch(() => {
                notify('Error al actualizar la tabla de turnos.', 'error');
            });
        }
    }
    window.reloadTurnoTable = reloadTurnoTable;

    function sortTurnoTable(sortBy) {
        const currentSortBy = document.getElementById('current-sort-by')?.value || 'Fecha';
        const currentSortOrder = document.getElementById('current-sort-order')?.value || 'desc';

        let newSortOrder = 'asc';
        if (currentSortBy === sortBy) {
            newSortOrder = currentSortOrder === 'asc' ? 'desc' : 'asc';
        }

        const sortByInput = document.getElementById('current-sort-by');
        const sortOrderInput = document.getElementById('current-sort-order');
        if (sortByInput) sortByInput.value = sortBy;
        if (sortOrderInput) sortOrderInput.value = newSortOrder;

        reloadTurnoTable(1);
    }
    window.sortTurnoTable = sortTurnoTable;

    function clearTurnoFilters() {
        const filterForm = document.getElementById('filterForm');
        if (filterForm) {
            filterForm.querySelectorAll('input[type="checkbox"]').forEach(cb => {
                cb.checked = true;
            });

            const fechaDesde = document.getElementById('fechaDesde');
            const fechaHasta = document.getElementById('fechaHasta');
            if (fechaDesde) fechaDesde.value = '';
            if (fechaHasta) fechaHasta.value = '';

            const precioDesde = document.getElementById('precioDesde');
            const precioHasta = document.getElementById('precioHasta');
            if (precioDesde) precioDesde.value = '';
            if (precioHasta) precioHasta.value = '';
        }

        const searchInput = document.getElementById('simple-search');
        if (searchInput) searchInput.value = '';
        currentSearchTerm = '';

        hideFilterDropdown();

        notify('Filtros restablecidos.', 'info');
        reloadTurnoTable(1);
    }
    window.clearTurnoFilters = clearTurnoFilters;

    function applyClientFilters() {
        const searchInput = document.getElementById('simple-search');
        const term = (searchInput?.value || '').trim().toLowerCase();
        const container = document.getElementById('turnoEstadoFiltros');
        const checks = container?.querySelectorAll ? [...container.querySelectorAll('input[type="checkbox"]')] : [];
        const checkedStates = checks.length > 0 ? checks.filter(c => c.checked).map(c => c.value) : null;

        const rows = typeof document.querySelectorAll === 'function' ? document.querySelectorAll('#turno-table-body tr[data-estado]') : [];
        if (rows && rows.length > 0) {
            rows.forEach(row => {
                const text = (row.textContent || '').toLowerCase();
                const estado = (typeof row.getAttribute === 'function' ? (row.getAttribute('data-estado') || row.getAttribute('estado')) : row.dataset?.estado) || row.estado;
                const matchText = !term || text.includes(term);
                const matchEstado = !checkedStates || checkedStates.includes(estado);
                row.style.display = (matchText && matchEstado) ? '' : 'none';
            });
        }
    }

    function setupSearchWithDebounce() {
        const searchInput = document.getElementById('simple-search');
        if (!searchInput) return;

        searchInput.addEventListener('input', e => {
            const hasFilterForm = !!document.getElementById('filterForm');
            if (!hasFilterForm) {
                applyClientFilters();
            }
            if (typeof clearTimeout === 'function') clearTimeout(searchTimeout);
            currentSearchTerm = (e?.target ? e.target.value : searchInput.value || '').trim();
            if (typeof setTimeout === 'function') {
                searchTimeout = setTimeout(() => {
                    reloadTurnoTable(1);
                }, 300);
            }
        });
    }

    const estadoContainer = document.getElementById('turnoEstadoFiltros');
    if (estadoContainer?.querySelectorAll) {
        estadoContainer.querySelectorAll('input[type="checkbox"]').forEach(check => {
            check.addEventListener?.('change', () => {
                const hasFilterForm = !!document.getElementById('filterForm');
                if (!hasFilterForm) {
                    applyClientFilters();
                }
            });
        });
    }

    const sortElements = typeof document.querySelectorAll === 'function' ? document.querySelectorAll('a[href*="sortBy"]') : [];
    if (sortElements && sortElements.length > 0) {
        sortElements.forEach(sort => {
            sort.addEventListener?.('click', () => {
                try {
                    const url = new URL(sort.href);
                    const searchInput = document.getElementById('simple-search');
                    if (searchInput?.value) {
                        url.searchParams.set('turnoBusqueda', searchInput.value);
                    }
                    if (estadoContainer?.querySelectorAll) {
                        const unchecked = [...estadoContainer.querySelectorAll('input[type="checkbox"]')].filter(c => !c.checked);
                        if (unchecked.length > 0) {
                            url.searchParams.set('turnoEstados', unchecked.map(c => c.value).join(','));
                        }
                    }
                    sort.href = url.toString();
                } catch { }
            });
        });
    }

    if (typeof window !== 'undefined' && window.location?.search) {
        try {
            const urlParams = new URLSearchParams(window.location.search);
            const searchVal = urlParams.get('turnoBusqueda');
            const searchInput = document.getElementById('simple-search');
            if (searchVal && searchInput) {
                searchInput.value = searchVal;
            }
            if (estadoContainer?.querySelectorAll && urlParams.has('turnoEstados')) {
                const unchecked = urlParams.get('turnoEstados').split(',');
                estadoContainer.querySelectorAll('input[type="checkbox"]').forEach(c => {
                    if (unchecked.includes(c.value)) c.checked = false;
                });
            }
            const hasFilterForm = !!document.getElementById('filterForm');
            if (!hasFilterForm) {
                applyClientFilters();
            }
        } catch { }
    }

    function setupFilterFormSubmit() {
        const form = document.getElementById('filterForm');
        if (!form || form.dataset.submitSetup === 'true') return;

        form.addEventListener('submit', function (e) {
            e.preventDefault();

            const precioDesde = document.getElementById('precioDesde');
            const precioHasta = document.getElementById('precioHasta');

            if (precioDesde && precioDesde.value.trim() !== '') {
                const valDesde = parseFloat(precioDesde.value);
                if (valDesde < 0) {
                    notify('El precio mínimo no puede ser negativo.', 'warning');
                    return;
                }
            }

            if (precioHasta && precioHasta.value.trim() !== '') {
                const valHasta = parseFloat(precioHasta.value);
                if (valHasta < 0) {
                    notify('El precio máximo no puede ser negativo.', 'warning');
                    return;
                }
            }

            if (precioDesde && precioHasta && precioDesde.value.trim() !== '' && precioHasta.value.trim() !== '') {
                const valDesde = parseFloat(precioDesde.value);
                const valHasta = parseFloat(precioHasta.value);
                if (valDesde > valHasta) {
                    notify('El precio mínimo no puede ser mayor al precio máximo.', 'warning');
                    return;
                }
            }

            const fechaDesde = document.getElementById('fechaDesde');
            const fechaHasta = document.getElementById('fechaHasta');
            if (fechaDesde && fechaHasta && fechaDesde.value && fechaHasta.value) {
                if (new Date(fechaDesde.value) > new Date(fechaHasta.value)) {
                    notify('La fecha desde no puede ser posterior a la fecha hasta.', 'warning');
                    return;
                }
            }

            hideFilterDropdown();
            reloadTurnoTable(1);
        });

        form.dataset.submitSetup = 'true';
    }

    document.getElementById('limpiarFiltrosTurno')?.addEventListener?.('click', () => {
        const container = document.getElementById('turnoEstadoFiltros');
        if (container?.querySelectorAll) {
            container.querySelectorAll('input[type="checkbox"]').forEach(check => {
                check.checked = true;
            });
        }
        const hasFilterForm = !!document.getElementById('filterForm');
        if (!hasFilterForm) {
            applyClientFilters();
        }
    });

    if (typeof document.addEventListener === 'function') {
        document.addEventListener('DOMContentLoaded', () => {
            setupSearchWithDebounce();
            setupFilterFormSubmit();
        });
    }

    setupSearchWithDebounce();
    setupFilterFormSubmit();
})();
