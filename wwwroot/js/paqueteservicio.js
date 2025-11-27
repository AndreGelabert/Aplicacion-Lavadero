/**
 * ================================================
 * PAQUETESERVICIO.JS - FUNCIONALIDAD PÁGINA PAQUETES
 * ================================================
 * Responsabilidades:
 *  - Búsqueda con debounce y ordenamiento de tabla.
 *  - Filtros y recarga parcial (tabla y formulario).
 *  - Formulario AJAX crear/actualizar.
 *  - Selector dinámico de servicios por tipo de vehículo.
 *  - Cálculo y visualización de precio final y tiempo estimado.
 *  - Hints dinámicos de rangos de precio.
 *  - Gestión de activación/desactivación (modal de confirmación).
 *  - Paso de descuento normalizado.
 *
 * Estilo alineado a servicio.js: secciones claras, helpers reutilizables,
 * mínima exposición global (solo funciones llamadas desde HTML).
 */

(function () {
    'use strict';

    // ===================== Estado interno =====================
    let formMsgTimeout = null;
    let tableMsgTimeout = null;
    let searchTimeout = null;

    let currentSearchTerm = '';
    let serviciosDisponibles = [];
    let serviciosSeleccionados = [];
    let servicioSeleccionadoDropdown = null;
    let ultimoTipoVehiculoSeleccionado = null;

    // ===================== Inicialización del módulo =====================
    window.PageModules = window.PageModules || {};
    window.PageModules.paquetes = { init: initializePaquetesPage };

    document.addEventListener('DOMContentLoaded', () => {
        try { window.PageModules?.paquetes?.init(); }
        catch { initializePaquetesPage(); }
    });

    /**
     * Inicializa el comportamiento principal de la página de Paquetes
     */
    function initializePaquetesPage() {
        setupFormMessageHandler();
        setupSearchWithDebounce();
        setupFilterFormSubmit();
        setupClearFiltersButton();
        setupDropdownClickOutside();
        setupDynamicPriceHints();
        setupDescuentoStep();
        window.CommonUtils?.setupDefaultFilterForm();
        protegerTipoVehiculoEnEdicion();
        setupTipoVehiculoChangeHandler();
        initializeFormFromHidden();
        checkEditMode();
        initializeFormFromHidden();
    }

    // ===================== Modo edición (acordeón) =====================

    /**
     * Abre el acordeón si el título indica modo edición.
     */
    function checkEditMode() {
        const formTitle = document.getElementById('form-title');
        if (formTitle?.textContent.includes('Editando')) {
            const acc = document.getElementById('accordion-flush-body-1');
            if (acc) acc.classList.remove('hidden');
        }
    }

    // ===================== Búsqueda (debounce) =====================

    /**
     * Configura la búsqueda de tabla con debouncing.
     */
    function setupSearchWithDebounce() {
        const searchInput = document.getElementById('simple-search');
        if (!searchInput) return;

        const cloned = searchInput.cloneNode(true);
        searchInput.parentNode.replaceChild(cloned, searchInput);

        currentSearchTerm = cloned.value?.trim() || '';

        cloned.addEventListener('input', function () {
            const term = this.value.trim();
            if (searchTimeout) clearTimeout(searchTimeout);

            if (term === '') {
                currentSearchTerm = '';
                reloadPaqueteTable(1);
                schedulePriceHintsRefresh();
                return;
            }
            searchTimeout = setTimeout(() => {
                performServerSearch(term);
                schedulePriceHintsRefresh();
            }, 500);
        });
    }

    /**
     * Realiza búsqueda en servidor preservando filtros actuales.
     * @param {string} searchTerm
     */
    function performServerSearch(searchTerm) {
        currentSearchTerm = searchTerm;
        const params = buildFilterParams();
        const { sortBy, sortOrder } = getCurrentSort();
        params.set('searchTerm', searchTerm);
        params.set('pageNumber', '1');
        params.set('sortBy', sortBy);
        params.set('sortOrder', sortOrder);
        loadTablePartial('/PaqueteServicio/SearchPartial?' + params.toString());
    }

    // ===================== Ordenamiento tabla =====================

    /**
     * Obtiene el orden actual de la tabla desde inputs ocultos.
     */
    function getCurrentSort() {
        return {
            sortBy: document.getElementById('current-sort-by')?.value || 'Nombre',
            sortOrder: document.getElementById('current-sort-order')?.value || 'asc'
        };
    }

    /**
     * Cambia el orden de la tabla y recarga.
     * @param {string} sortBy
     */
    window.sortTable = function (sortBy) {
        const currentSortBy = document.getElementById('current-sort-by')?.value || 'Nombre';
        const currentSortOrder = document.getElementById('current-sort-order')?.value || 'asc';
        const newOrder = (currentSortBy === sortBy) ? (currentSortOrder === 'asc' ? 'desc' : 'asc') : 'asc';
        setHiddenValue('current-sort-by', sortBy);
        setHiddenValue('current-sort-order', newOrder);
        reloadPaqueteTable(1);
    };

    // ===================== Filtros y recarga de tabla =====================

    /**
     * Construye un objeto plano con los parámetros del formulario de filtros.
     */
    function buildFilterParams() {
        const form = document.getElementById('filterForm');
        const params = new URLSearchParams();
        if (!form) return params;
        const fd = new FormData(form);
        for (const [k, v] of fd.entries()) {
            params.append(k, v); // preserva múltiples valores (checkboxes 'estados')
        }
        return params;
    }

    /**
     * Recarga la tabla de paquetes considerando filtros, orden y búsqueda.
     * @param {number} page
     */
    function reloadPaqueteTable(page) {
        const params = buildFilterParams();
        params.set('pageNumber', String(page));
        const { sortBy, sortOrder } = getCurrentSort();
        params.set('sortBy', sortBy);
        params.set('sortOrder', sortOrder);

        let url;
        if (currentSearchTerm && currentSearchTerm.trim().length > 0) {
            params.set('searchTerm', currentSearchTerm.trim());
            url = '/PaqueteServicio/SearchPartial?' + params.toString();
        } else {
            url = '/PaqueteServicio/TablePartial?' + params.toString();
        }
        loadTablePartial(url);
    }

    /**
     * Carga la tabla parcial vía fetch y reinstala ganchos necesarios.
     * @param {string} url
     */
    function loadTablePartial(url) {
        const antiCache = (url.includes('?') ? '&' : '?') + '_=' + Date.now();
        fetch(url + antiCache, {
            headers: { 'X-Requested-With': 'XMLHttpRequest', 'Cache-Control': 'no-cache' },
            cache: 'no-store'
        })
            .then(r => r.text())
            .then(html => {
                const container = document.getElementById('paquete-table-container');
                if (container) container.innerHTML = html;
                const cp = document.getElementById('current-page-value')?.value;
                if (cp && container) container.dataset.currentPage = cp;

                window.CommonUtils?.setupDefaultFilterForm?.();
                setupDescuentoStep();
                if (typeof initDropdowns === 'function') initDropdowns();
                schedulePriceHintsRefresh();
            })
            .catch(err => {
                console.error('loadTablePartial error:', err);
                showTableMessage('error', 'Error al cargar los datos.');
            });
    }

    /**
     * Cambia de página la tabla (expuesto globalmente).
     * @param {number} page
     */
    window.changePage = (page) => reloadPaqueteTable(page);

    /**
     * Obtiene el número de página actual de la tabla.
     */
    window.getCurrentTablePage = function () {
        return parseInt(document.getElementById('paquete-table-container')?.dataset.currentPage || '1');
    };

    /**
     * Intercepta el submit de filtros para aplicar y recargar tabla.
     */
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
            reloadPaqueteTable(1);
  if (history.replaceState) history.replaceState(null, '', window.location.pathname);
    showTableMessage('info', 'Filtros aplicados.');
            schedulePriceHintsRefresh();
        });
 form.dataset.submitSetup = 'true';
    }

    /**
     * Configura el botón de limpiar filtros.
     */
    function setupClearFiltersButton() {
        const btn = document.getElementById('clearFiltersBtn') ||
            document.querySelector('[data-action="clear-filters"]') ||
            document.querySelector('[data-clear="filters"]');
        if (!btn || btn.dataset.setup === 'true') return;
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            clearPaqueteFilters();
        });
        btn.dataset.setup = 'true';
    }

    /**
    * Limpia el formulario de creación/edición de paquete y sale de modo edición.
    * Resetea completamente valores que el reset nativo no deja en blanco (porque provienen del parcial cargado en edición).
    */
    window.limpiarFormularioPaquete = function () {
        const form = document.getElementById('paquete-form');
        if (!form) return;

        try { form.reset(); } catch { }

        const clearValue = (id) => { const el = document.getElementById(id); if (el) el.value = ''; };
        clearValue('Id');
        clearValue('Nombre');
        clearValue('PorcentajeDescuento');
        clearValue('Precio');
        clearValue('TiempoEstimado');
        clearValue('PaqueteServiciosIdsData');
        clearValue('ServiciosIdsJson');

        const tipoVehiculoSel = document.getElementById('TipoVehiculo');
        if (tipoVehiculoSel) tipoVehiculoSel.selectedIndex = 0;

        serviciosDisponibles = [];
        serviciosSeleccionados = [];
        ultimoTipoVehiculoSeleccionado = null; // NUEVO: resetear tipo de vehículo

        updateServiciosSeleccionadosList();

        document.getElementById('servicio-selector-container')?.classList.add('hidden');
        const servicioSearch = document.getElementById('servicio-search');
        if (servicioSearch) servicioSearch.value = '';
        document.getElementById('servicio-dropdown')?.classList.add('hidden');
        document.getElementById('servicios-error')?.classList.add('hidden');

        updateResumen();

        const titleSpan = document.getElementById('form-title');
        if (titleSpan) titleSpan.textContent = 'Registrando un Paquete de Services';

        hideFormMessage();
    };

    // Opción: función semántica para botón "Cancelar"
    window.cancelarEdicionPaquete = async function () {
        try {
            const resp = await fetch('/PaqueteServicio/FormPartial', {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            const srvTitle = resp.headers.get('X-Form-Title');
            const html = await resp.text();

            const cont = document.getElementById('paquete-form-container');
            if (cont) cont.innerHTML = html;

            const titleSpan = document.getElementById('form-title');
            if (titleSpan) titleSpan.textContent = srvTitle?.trim() || 'Registrando un Paquete de Servicios';

            // Reset de estado local
            serviciosDisponibles = [];
            serviciosSeleccionados = [];
            servicioSeleccionadoDropdown = null;
            ultimoTipoVehiculoSeleccionado = null; // NUEVO: resetear tipo de vehículo

            updateServiciosSeleccionadosList();
            updateResumen();

            document.getElementById('servicio-selector-container')?.classList.add('hidden');
            document.getElementById('servicio-dropdown')?.classList.add('hidden');
            document.getElementById('servicios-error')?.classList.add('hidden');

            setupDescuentoStep();
            protegerTipoVehiculoEnEdicion();
            setupTipoVehiculoChangeHandler();
            initializeFormFromHidden();

            document.getElementById('accordion-flush-body-1')?.classList.add('hidden');
            hideFormMessage();
        } catch (e) {
            console.error('cancelarEdicionPaquete error:', e);
            limpiarFormularioPaquete();
            const form = document.getElementById('paquete-form');
            if (form) form.action = '/PaqueteServicio/CrearPaqueteAjax';
            const submitBtn = document.querySelector('#paquete-form [type="submit"]');
            if (submitBtn) submitBtn.textContent = 'Registrar';
        }
        return false;
    };

    /**
     * Limpia los filtros de la tabla y recarga a estado base.
     */
    function clearPaqueteFilters() {
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

        // Limpiar checkboxes de tipos de vehículo explícitamente
        form.querySelectorAll('input[name="tiposVehiculo"]').forEach(cb => cb.checked = false);

        // Limpiar campos numéricos y selects
        form.querySelectorAll('input[type="number"]').forEach(inp => {
            inp.value = '';
            inp.removeAttribute('min');
            inp.removeAttribute('max');
        });
        form.querySelectorAll('select').forEach(sel => sel.selectedIndex = 0);

        // Limpiar búsqueda
        const searchInput = document.getElementById('simple-search');
        if (searchInput) searchInput.value = '';
        currentSearchTerm = '';
        if (searchTimeout) { clearTimeout(searchTimeout); searchTimeout = null; }

        setHiddenValue('pageNumber', '1');
        setHiddenValue('current-sort-by', 'Nombre');
        setHiddenValue('current-sort-order', 'asc');

        if (history.replaceState) history.replaceState({}, document.title, '/PaqueteServicio/Index');

      // Limpiar hints de precio
        ensureHelpElements();
        const helpPrecioMin = document.getElementById('precioMin-help');
        const helpPrecioMax = document.getElementById('precioMax-help');
     if (helpPrecioMin) helpPrecioMin.textContent = '';
    if (helpPrecioMax) helpPrecioMax.textContent = '';
        document.getElementById('precioMin')?.removeAttribute('min');
     document.getElementById('precioMax')?.removeAttribute('max');

        // Limpiar hints de descuento
        ensureDiscountHelpElements();
        const helpDescuentoMin = document.getElementById('descuentoMin-help');
   const helpDescuentoMax = document.getElementById('descuentoMax-help');
        if (helpDescuentoMin) helpDescuentoMin.textContent = '';
        if (helpDescuentoMax) helpDescuentoMax.textContent = '';
        document.getElementById('descuentoMin')?.removeAttribute('min');
        document.getElementById('descuentoMax')?.removeAttribute('max');

        document.getElementById('filterDropdown')?.classList.add('hidden');

        window.CommonUtils?.setupDefaultFilterForm?.();
        setupDescuentoStep();
        if (typeof initDropdowns === 'function') initDropdowns();

        reloadPaqueteTable(1);
        schedulePriceHintsRefresh();
        showTableMessage('info', 'Filtros restablecidos.');
    }

    /**
     * Establece el valor de un input hidden.
     * @param {string} id
     * @param {string} value
     */
    function setHiddenValue(id, value) {
        const el = document.getElementById(id);
        if (el) el.value = value;
    }

    // ===================== Hints dinámicos de precio =====================

    /**
     * Asegura que existan los elementos de ayuda para precio.
     */
    function ensureHelpElements() {
 const precioMinInput = document.getElementById('precioMin');
        const precioMaxInput = document.getElementById('precioMax');
     
        if (precioMinInput && !document.getElementById('precioMin-help')) {
          const helpMin = document.createElement('p');
         helpMin.id = 'precioMin-help';
    helpMin.className = 'mt-1 text-xs text-gray-500';
            precioMinInput.parentNode.insertBefore(helpMin, precioMinInput.nextSibling);
    }
        
        if (precioMaxInput && !document.getElementById('precioMax-help')) {
            const helpMax = document.createElement('p');
 helpMax.id = 'precioMax-help';
         helpMax.className = 'mt-1 text-xs text-gray-500';
  precioMaxInput.parentNode.insertBefore(helpMax, precioMaxInput.nextSibling);
        }
    }

    /**
     * Configura y escucha cambios para actualizar hints de rango de precio.
     */
    function setupDynamicPriceHints() {
        const form = document.getElementById('filterForm');
        if (!form) return;

      ensureHelpElements();

        async function refreshPriceRangeHints() {
 try {
        const params = new URLSearchParams();
         const fd = new FormData(form);
     for (const [k, v] of fd.entries()) {
     if (['precioMin', 'precioMax', 'pageNumber'].includes(k)) continue;
params.append(k, v);
        }
          if (currentSearchTerm) params.append('searchTerm', currentSearchTerm);

   const resp = await fetch('/PaqueteServicio/PriceRange?' + params.toString(), {
     headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' }
     });

      let data;
                try { data = await resp.json(); } catch { data = null; }

      const precioMinEl = document.getElementById('precioMin');
         const precioMaxEl = document.getElementById('precioMax');
                const helpMin = document.getElementById('precioMin-help');
  const helpMax = document.getElementById('precioMax-help');

            if (data?.success) {
       const fmt = (n) => (typeof n === 'number'
         ? n.toFixed(2)
           : (n != null ? parseFloat(n).toFixed(2) : ''));
                    
         if (helpMin) helpMin.textContent = data.min != null ? `Mín. permitido: $${fmt(data.min)}` : '';
     if (helpMax) helpMax.textContent = data.max != null ? `Máx. permitido: $${fmt(data.max)}` : '';
   
        if (precioMinEl && data.min != null) precioMinEl.setAttribute('min', data.min);
     else precioMinEl?.removeAttribute('min');
     
             if (precioMaxEl && data.max != null) precioMaxEl.setAttribute('max', data.max);
           else precioMaxEl?.removeAttribute('max');
      } else {
           if (helpMin) helpMin.textContent = '';
      if (helpMax) helpMax.textContent = '';
      precioMinEl?.removeAttribute('min');
    precioMaxEl?.removeAttribute('max');
  }
            } catch (err) {
 console.error('Error refreshPriceRangeHints:', err);
      // Fallback limpio
      const helpMin = document.getElementById('precioMin-help');
        const helpMax = document.getElementById('precioMax-help');
     if (helpMin) helpMin.textContent = '';
        if (helpMax) helpMax.textContent = '';
   document.getElementById('precioMin')?.removeAttribute('min');
                document.getElementById('precioMax')?.removeAttribute('max');
          }
      }

        // Exponer para refrescos programados
        window.refreshPriceRangeHints = refreshPriceRangeHints;
 refreshPriceRangeHints();

      // Watchers
        const watchedSelectors = [
       'input[name="estados"]',
     'input[name="tiposVehiculo"]',
   '#descuentoMin', '#descuentoMax',
    '#serviciosCantidad'
        ];
        watchedSelectors.forEach(sel => {
        document.querySelectorAll(sel).forEach(el => {
           el.addEventListener('change', refreshPriceRangeHints);
        el.addEventListener('input', refreshPriceRangeHints);
            });
  });
        const search = document.getElementById('simple-search');
        if (search) search.addEventListener('input', () => setTimeout(refreshPriceRangeHints, 550));
    
        // También configurar hints de descuento
        setupDynamicDiscountHints();
    }

    /**
     * Asegura que existan los elementos de ayuda para descuento.
     */
    function ensureDiscountHelpElements() {
        const descuentoMinInput = document.getElementById('descuentoMin');
        const descuentoMaxInput = document.getElementById('descuentoMax');
        
    if (descuentoMinInput && !document.getElementById('descuentoMin-help')) {
  const helpMin = document.createElement('p');
   helpMin.id = 'descuentoMin-help';
            helpMin.className = 'mt-1 text-xs text-gray-500';
            descuentoMinInput.parentNode.insertBefore(helpMin, descuentoMinInput.nextSibling);
        }
        
        if (descuentoMaxInput && !document.getElementById('descuentoMax-help')) {
    const helpMax = document.createElement('p');
            helpMax.id = 'descuentoMax-help';
         helpMax.className = 'mt-1 text-xs text-gray-500';
            descuentoMaxInput.parentNode.insertBefore(helpMax, descuentoMaxInput.nextSibling);
  }
    }

    /**
     * Configura y escucha cambios para actualizar hints de rango de descuento.
     */
 function setupDynamicDiscountHints() {
        const form = document.getElementById('filterForm');
     if (!form) return;

        ensureDiscountHelpElements();

        async function refreshDiscountRangeHints() {
            try {
                const params = new URLSearchParams();
                const fd = new FormData(form);
  for (const [k, v] of fd.entries()) {
     if (['descuentoMin', 'descuentoMax', 'pageNumber'].includes(k)) continue;
     params.append(k, v);
            }
  if (currentSearchTerm) params.append('searchTerm', currentSearchTerm);

    const resp = await fetch('/PaqueteServicio/DiscountRange?' + params.toString(), {
                    headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' }
      });

     let data;
     try { data = await resp.json(); } catch { data = null; }

        const descuentoMinEl = document.getElementById('descuentoMin');
        const descuentoMaxEl = document.getElementById('descuentoMax');
                const helpMin = document.getElementById('descuentoMin-help');
          const helpMax = document.getElementById('descuentoMax-help');

  if (data?.success) {
           const fmt = (n) => (typeof n === 'number'
         ? n.toFixed(0)
    : (n != null ? Math.round(parseFloat(n)) : ''));
   
               if (data.min != null) {
                     if (helpMin) helpMin.textContent = `Mín. disponible: ${fmt(data.min)}%`;
  descuentoMinEl?.setAttribute('min', data.min);
      } else {
   if (helpMin) helpMin.textContent = '';
  descuentoMinEl?.removeAttribute('min');
     }
           
        if (data.max != null) {
                if (helpMax) helpMax.textContent = `Máx. disponible: ${fmt(data.max)}%`;
               descuentoMaxEl?.setAttribute('max', data.max);
            } else {
     if (helpMax) helpMax.textContent = '';
            descuentoMaxEl?.removeAttribute('max');
         }
     } else {
   if (helpMin) helpMin.textContent = '';
           if (helpMax) helpMax.textContent = '';
             descuentoMinEl?.removeAttribute('min');
         descuentoMaxEl?.removeAttribute('max');
    }
     } catch (err) {
   console.error('Error refreshDiscountRangeHints:', err);
       // Fallback limpio
                const helpMin = document.getElementById('descuentoMin-help');
    const helpMax = document.getElementById('descuentoMax-help');
if (helpMin) helpMin.textContent = '';
      if (helpMax) helpMax.textContent = '';
                document.getElementById('descuentoMin')?.removeAttribute('min');
       document.getElementById('descuentoMax')?.removeAttribute('max');
    }
        }

        // Exponer para refrescos programados
        window.refreshDiscountRangeHints = refreshDiscountRangeHints;
     refreshDiscountRangeHints();

   // Watchers
        const watchedSelectors = [
            'input[name="estados"]',
     'input[name="tiposVehiculo"]',
  '#precioMin', '#precioMax',
            '#serviciosCantidad'
        ];
        watchedSelectors.forEach(sel => {
   document.querySelectorAll(sel).forEach(el => {
        el.addEventListener('change', refreshDiscountRangeHints);
 el.addEventListener('input', refreshDiscountRangeHints);
            });
        });
        const search = document.getElementById('simple-search');
        if (search) search.addEventListener('input', () => setTimeout(refreshDiscountRangeHints, 550));
    }

    /**
   * Programa un refresco de los hints de precio y descuento (debounced).
     */
    function schedulePriceHintsRefresh() {
   if (typeof window.refreshPriceRangeHints === 'function') {
    setTimeout(() => { try { window.refreshPriceRangeHints(); } catch (e) { console.error('Error refreshPriceRangeHints:', e); } }, 120);
     }
   if (typeof window.refreshDiscountRangeHints === 'function') {
            setTimeout(() => { try { window.refreshDiscountRangeHints(); } catch (e) { console.error('Error refreshDiscountRangeHints:', e); } }, 120);
        }
    }

    // ===================== Selector dinámico de servicios =====================

    /**
     * Carga servicios según tipo de vehículo (AJAX) y prepara dropdown filtrable.
     */
    window.loadServiciosPorTipoVehiculo = async function () {
        const tipoVehiculo = document.getElementById('TipoVehiculo')?.value;
        const cont = document.getElementById('servicio-selector-container');

        // Si no hay tipo seleccionado, ocultar y limpiar todo
        if (!tipoVehiculo) {
            cont?.classList.add('hidden');
            serviciosDisponibles = [];
            serviciosSeleccionados = [];
            ultimoTipoVehiculoSeleccionado = null;

            // Limpiar campos ocultos
            const hiddenIds = document.getElementById('PaqueteServiciosIdsData');
            if (hiddenIds) hiddenIds.value = '';
            const jsonIds = document.getElementById('ServiciosIdsJson');
            if (jsonIds) jsonIds.value = '[]';

            // Limpiar UI
            document.getElementById('servicio-search')?.setAttribute('value', '');
            document.getElementById('servicio-dropdown')?.classList.add('hidden');
            document.getElementById('servicios-error')?.classList.add('hidden');

            updateServiciosSeleccionadosList();
            updateResumen();
            return;
        }

        // VALIDACIÓN: Si cambió el tipo de vehículo Y ya había servicios seleccionados
        if (ultimoTipoVehiculoSeleccionado !== null &&
            ultimoTipoVehiculoSeleccionado !== tipoVehiculo &&
            serviciosSeleccionados.length > 0) {

            // Limpiar servicios seleccionados
            serviciosSeleccionados = [];

            // Limpiar campos ocultos
            const hiddenIds = document.getElementById('PaqueteServiciosIdsData');
            if (hiddenIds) hiddenIds.value = '';
            const jsonIds = document.getElementById('ServiciosIdsJson');
            if (jsonIds) jsonIds.value = '[]';

            // Limpiar UI
            const searchInput = document.getElementById('servicio-search');
            if (searchInput) searchInput.value = '';
            document.getElementById('servicio-dropdown')?.classList.add('hidden');
            document.getElementById('servicios-error')?.classList.add('hidden');

            updateServiciosSeleccionadosList();
            updateResumen();

            // Mostrar mensaje informativo
            showFormMessage('Se limpiaron los servicios seleccionados al cambiar el tipo de vehículo.', 'info');
        }

        // Cargar servicios del nuevo tipo
        try {
            const resp = await fetch(`/PaqueteServicio/ObtenerServiciosPorTipoVehiculo?tipoVehiculo=${encodeURIComponent(tipoVehiculo)}`);
            const data = await resp.json();

            if (data.success) {
                serviciosDisponibles = data.servicios;
                cont?.classList.remove('hidden');
                renderServiciosDropdown(serviciosDisponibles);
                // Actualizar el tipo de vehículo registrado
                ultimoTipoVehiculoSeleccionado = tipoVehiculo;
            } else {
                cont?.classList.add('hidden');
                showFormMessage('No hay servicios disponibles para el tipo seleccionado.', 'error');
                ultimoTipoVehiculoSeleccionado = tipoVehiculo;
            }
        } catch {
            cont?.classList.add('hidden');
            showFormMessage('Error al cargar servicios.', 'error');
        }
    };

    /**
     * Renderiza el dropdown de servicios, agrupado por tipo y filtrable.
     * @param {Array} servicios
     * @param {string} filterText
     */
    function renderServiciosDropdown(servicios, filterText = '') {
        const target = document.getElementById('servicio-dropdown-content');
        if (!target) return;

        if (!Array.isArray(servicios) || servicios.length === 0) {
            target.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No hay servicios disponibles</p>';
            return;
        }

        let lista = servicios;
        if // Filtering logic
  (filterText) {
            const lower = filterText.toLowerCase();
            lista = lista.filter(s =>
                (s.nombre && s.nombre.toLowerCase().includes(lower)) ||
                (s.tipo && s.tipo.toLowerCase().includes(lower))
            );
        }
        lista = lista.filter(s => !serviciosSeleccionados.some(sel => sel.id === s.id));

        if (lista.length === 0) {
            target.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No se encontraron servicios</p>';
            return;
        }

        const grupos = {};
        lista.forEach(s => (grupos[s.tipo] ||= []).push(s));

        let html = '';
        Object.keys(grupos).sort().forEach(tipo => {
            html += `<div class="mb-2">
                        <h6 class="text-xs font-semibold text-gray-700 dark:text-gray-300 px-2 py-1 bg-gray-100 dark:bg-gray-600">${tipo}</h6>
                        <div class="space-y-1">`;
            grupos[tipo].forEach(s => {
                const active = servicioSeleccionadoDropdown?.id === s.id ? 'bg-blue-100 dark:bg-blue-900' : '';
                html += `<div class="px-2 py-2 hover:bg-gray-100 dark:hover:bg-gray-600 cursor-pointer ${active}"
                             onclick="selectServicioFromDropdown('${s.id}')">
                             <div class="text-sm font-medium text-gray-900 dark:text-white">${escapeHtml(s.nombre)}</div>
                         </div>`;
            });
            html += '</div></div>';
        });

        target.innerHTML = html;
    }

    /**
     * Muestra el dropdown del selector de servicios.
     */
    window.showServicioDropdown = function () {
        const dropdown = document.getElementById('servicio-dropdown');
        const searchInput = document.getElementById('servicio-search');
        if (dropdown && serviciosDisponibles.length) {
            renderServiciosDropdown(serviciosDisponibles, searchInput?.value || '');
            dropdown.classList.remove('hidden');
        }
    };

    /**
     * Filtra el dropdown de servicios por texto.
     * @param {string} txt
     */
    window.filterServiciosDropdown = function (txt) {
        renderServiciosDropdown(serviciosDisponibles, txt);
        document.getElementById('servicio-dropdown')?.classList.remove('hidden');
    };

    /**
     * Selecciona un servicio desde el dropdown (pre-selección).
     * @param {string} id
     */
    window.selectServicioFromDropdown = function (id) {
        const s = serviciosDisponibles.find(x => x.id === id);
        if (!s) return;
        servicioSeleccionadoDropdown = s;
        renderServiciosDropdown(serviciosDisponibles, document.getElementById('servicio-search')?.value || '');
        const searchInput = document.getElementById('servicio-search');
        if (searchInput) searchInput.value = s.nombre;
    };

    /**
     * Agrega el servicio seleccionado a la lista del paquete.
     */
    window.agregarServicioSeleccionado = function () {
        if (!servicioSeleccionadoDropdown) {
            showFormMessage('Debe seleccionar un servicio del listado.', 'error');
            return;
        }
        const tipo = servicioSeleccionadoDropdown.tipo;
        if (serviciosSeleccionados.some(s => s.tipo === tipo)) {
            showFormMessage('Solo puede seleccionar un servicio de cada tipo.', 'error');
            return;
        }
        serviciosSeleccionados.push({
            id: servicioSeleccionadoDropdown.id,
            nombre: servicioSeleccionadoDropdown.nombre,
            tipo: servicioSeleccionadoDropdown.tipo,
            precio: servicioSeleccionadoDropdown.precio,
            tiempoEstimado: servicioSeleccionadoDropdown.tiempoEstimado
        });
        servicioSeleccionadoDropdown = null;
        const searchInput = document.getElementById('servicio-search');
        if (searchInput) searchInput.value = '';
        document.getElementById('servicio-dropdown')?.classList.add('hidden');
        updateServiciosSeleccionadosList();
        updateResumen();
        renderServiciosDropdown(serviciosDisponibles);
    };

    /**
     * Quita un servicio de la lista seleccionada del paquete.
     * @param {string} id
     */
    window.removerServicioSeleccionado = function (id) {
        serviciosSeleccionados = serviciosSeleccionados.filter(s => s.id !== id);
        updateServiciosSeleccionadosList();
        updateResumen();
        renderServiciosDropdown(serviciosDisponibles, document.getElementById('servicio-search')?.value || '');
    };

    /**
     * Renderiza la lista de servicios seleccionados.
     */
    function updateServiciosSeleccionadosList() {
        const container = document.getElementById('servicios-seleccionados-container');
        const list = document.getElementById('servicios-seleccionados-list');
        if (!(container && list)) return;

        if (serviciosSeleccionados.length === 0) {
            container.classList.add('hidden');
            list.innerHTML = '';
            return;
        }
        container.classList.remove('hidden');

        // Generar HTML con numeración y drag handles
        list.innerHTML = '<ul class="space-y-2" id="servicios-sortable-list">' +
            serviciosSeleccionados.map((s, index) => {
                // Formatear tiempo usando helper global (fallback simple)
                const tiempoFormateado = window.SiteModule?.formatTiempoSimple?.(s.tiempoEstimado)
                    || (s.tiempoEstimado + ' min');

                return `
     <li draggable="true" 
                data-servicio-id="${s.id}"
            data-index="${index}"
              class="servicio-item flex items-center gap-3 p-2 bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-600 hover:border-blue-300 dark:hover:border-blue-500 transition-colors cursor-move">
      
          <!-- Drag Handle Icon -->
      <div class="drag-handle flex-shrink-0 text-gray-400 hover:text-gray-600 dark:text-gray-500 dark:hover:text-gray-300" title="Arrastrar para reordenar">
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="w-5 h-5">
 <path d="M8 5a2 2 0 1 1-4 0 2 2 0 0 1 4 0Zm0 7a2 2 0 1 1-4 0 2 2 0 0 1 4 0Zm0 7a2 2 0 1 1-4 0 2 2 0 0 1 4 0Zm12-14a2 2 0 1 1-4 0 2 2 0 0 1 4 0Zm0 7a2 2 0 1 1-4 0 2 2 0 0 1 4 0Zm0 7a2 2 0 1 1-4 0 2 2 0 0 1 4 0Z"/>
 </svg>
    </div>

<!-- Número de orden -->
                <div class="flex-shrink-0 w-8 h-8 flex items-center justify-center bg-blue-100 dark:bg-blue-900 text-blue-800 dark:text-blue-200 rounded-full font-bold text-sm">
       ${index + 1}
     </div>

          <!-- Información del servicio -->
       <div class="flex-1 min-w-0">
    <div class="font-medium text-gray-900 dark:text-white truncate">${escapeHtml(s.nombre)}</div>
   <div class="text-sm text-gray-500 dark:text-gray-400">(${escapeHtml(s.tipo)})</div>
       </div>

         <!-- Precio y tiempo -->
           <div class="flex-shrink-0 text-right">
      <div class="text-sm font-medium text-gray-900 dark:text-white">$${s.precio.toFixed(2)}</div>
            <div class="text-xs text-gray-500 dark:text-gray-400">${tiempoFormateado}</div>
    </div>

          <!-- Botón eliminar (centrado y sin corte) -->
  <button type="button"
          onclick="event.stopPropagation(); removerServicioSeleccionado('${s.id}')"
          class="flex-shrink-0 w-8 h-8 flex items-center justify-center overflow-visible bg-transparent rounded-md border border-transparent hover:border-red-200 dark:hover:border-red-700 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300"
          title="Quitar servicio"
          aria-label="Quitar servicio"
          style="line-height:0;">
    <svg xmlns="http://www.w3.org/2000/svg" fill="currentColor" class="w-5 h-5" viewBox="0 0 24 24" aria-hidden="true">
      <path fill-rule="evenodd" d="M12 2.25c-5.385 0-9.75 4.365-9.75 9.75s4.365 9.75 9.75 9.75 9.75-4.365 9.75-9.75S17.385 2.25 12 2.25Zm-1.72 6.97a.75.75 0 1 0-1.06 1.06L10.94 12l-1.72 1.72a.75.75 0 1 0 1.06 1.06L12 13.06l1.72 1.72a.75.75 0 1 0 1.06-1.06L13.06 12l1.72-1.72a.75.75 0 1 0-1.06-1.06L12 10.94l-1.72-1.72Z" clip-rule="evenodd"/>
    </svg>
  </button>
</li>
        `;
            }).join('') +
            '</ul>' +
            '<p class="mt-2 text-xs text-gray-500 dark:text-gray-400 flex items-center gap-1">' +
            '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-4 h-4">' +
            '<path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd" />' +
            '</svg>' +
            'Arrastra los servicios para cambiar su orden de ejecución' +
            '</p>';

        // Configurar drag and drop
        setupDragAndDrop();
    }

    /**
    * Configura la funcionalidad de drag and drop para reordenar servicios.
    */
    function setupDragAndDrop() {
        const list = document.getElementById('servicios-sortable-list');
        if (!list) return;

        const items = document.querySelectorAll('.servicio-item');
        let draggedElement = null;
        let placeholder = null;

        items.forEach(item => {
            // Dragstart - cuando empieza el arrastre
            item.addEventListener('dragstart', function (e) {
                draggedElement = this;
                this.style.opacity = '0.4';
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('text/html', this.innerHTML);

                // Crear placeholder visual
                setTimeout(() => {
                    placeholder = document.createElement('li');
                    placeholder.className = 'servicio-placeholder h-16 border-2 border-dashed border-blue-400 dark:border-blue-600 rounded bg-blue-50 dark:bg-blue-900/20';

                    // Insertar placeholder después del elemento arrastrado
                    if (draggedElement.nextSibling) {
                        list.insertBefore(placeholder, draggedElement.nextSibling);
                    } else {
                        list.appendChild(placeholder);
                    }

                    // Ocultar el elemento original
                    draggedElement.style.display = 'none';
                }, 0);
            });

            // Dragend - cuando termina el arrastre
            item.addEventListener('dragend', function (e) {
                this.style.opacity = '1';
                this.style.display = 'flex';

                // Limpiar placeholder
                if (placeholder && placeholder.parentNode) {
                    placeholder.parentNode.removeChild(placeholder);
                }

                // Remover highlights
                document.querySelectorAll('.servicio-item').forEach(i => {
                    i.classList.remove('bg-blue-50', 'dark:bg-blue-900/20');
                });

                draggedElement = null;
                placeholder = null;
            });

            // Dragover - permitir drop
            item.addEventListener('dragover', function (e) {
                e.preventDefault();
                e.dataTransfer.dropEffect = 'move';

                if (draggedElement === this || !draggedElement || !placeholder) return;

                // Determinar si insertar antes o después basándonos en la posición del mouse
                const rect = this.getBoundingClientRect();
                const midpoint = rect.top + rect.height / 2;

                if (e.clientY < midpoint) {
                    // Insertar antes
                    list.insertBefore(placeholder, this);
                } else {
                    // Insertar después
                    if (this.nextSibling) {
                        list.insertBefore(placeholder, this.nextSibling);
                    } else {
                        list.appendChild(placeholder);
                    }
                }
            });

            // Drop - cuando se suelta
            item.addEventListener('drop', function (e) {
                e.preventDefault();
                e.stopPropagation();

                if (!draggedElement || !placeholder) return;

                // Insertar el elemento en la posición del placeholder
                if (placeholder.parentNode) {
                    placeholder.parentNode.insertBefore(draggedElement, placeholder);
                    placeholder.parentNode.removeChild(placeholder);
                }

                // Mostrar el elemento
                draggedElement.style.display = 'flex';

                // Reconstruir orden
                actualizarOrdenServicios();
            });
        });

        // **CRÍTICO: Eventos en el contenedor de la lista**
        list.addEventListener('dragover', function (e) {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';

            if (!draggedElement || !placeholder) return;

            // Si el mouse está sobre un espacio vacío, mover el placeholder
            const afterElement = getDragAfterElement(list, e.clientY);

            if (afterElement == null) {
                list.appendChild(placeholder);
            } else {
                list.insertBefore(placeholder, afterElement);
            }
        });

        list.addEventListener('drop', function (e) {
            e.preventDefault();
            e.stopPropagation();

            if (!draggedElement || !placeholder) return;

            // Insertar el elemento en la posición del placeholder
            if (placeholder.parentNode) {
                placeholder.parentNode.insertBefore(draggedElement, placeholder);
                placeholder.parentNode.removeChild(placeholder);
            }

            // Mostrar el elemento
            draggedElement.style.display = 'flex';

            // Reconstruir orden
            actualizarOrdenServicios();
        });

        /**
         * Determina el elemento después del cual se debe insertar el placeholder
         * basándose en la posición Y del mouse
         */
        function getDragAfterElement(container, y) {
            const draggableElements = [...container.querySelectorAll('.servicio-item:not(.opacity-40)')];

            return draggableElements.reduce((closest, child) => {
                const box = child.getBoundingClientRect();
                const offset = y - box.top - box.height / 2;

                if (offset < 0 && offset > closest.offset) {
                    return { offset: offset, element: child };
                } else {
                    return closest;
                }
            }, { offset: Number.NEGATIVE_INFINITY }).element;
        }

        /**
         * Reconstruye el array serviciosSeleccionados según el orden actual del DOM
         */
        function actualizarOrdenServicios() {
            const allItems = Array.from(list.children).filter(el => el.classList.contains('servicio-item'));
            const newOrder = allItems.map(el => {
                const id = el.getAttribute('data-servicio-id');
                return serviciosSeleccionados.find(s => s.id === id);
            }).filter(s => s !== undefined);

            serviciosSeleccionados = newOrder;

            // Re-renderizar con nuevo orden
            updateServiciosSeleccionadosList();
            updateResumen();
        }
    }

    /**
     * Dispara el recálculo de precio/tiempo (expuesto global).
     */
    window.calcularPrecioYTiempo = function () { updateResumen(); };

    /**
     * Calcula y actualiza precio total, final (con descuento) y tiempo total.
     */
    function updateResumen() {
        const descuento = parseFloat(document.getElementById('PorcentajeDescuento')?.value || 0);
      const precioTotal = serviciosSeleccionados.reduce((sum, s) => sum + s.precio, 0);
        const precioFinal = precioTotal - (precioTotal * (descuento / 100));
    const tiempoTotal = serviciosSeleccionados.reduce((sum, s) => sum + s.tiempoEstimado, 0);

      setText('precio-total-sin-descuento', '$' + precioTotal.toFixed(2));
        setText('precio-final', '$' + precioFinal.toFixed(2));
        
// NUEVO: Usar helper global para formatear tiempo
        const tiempoFormateado = window.SiteModule?.formatTiempo?.(tiempoTotal, true) 
            || (tiempoTotal + ' min');
        document.getElementById('tiempo-total').innerHTML = tiempoFormateado;

        setValue('Precio', precioFinal.toFixed(2));
        setValue('TiempoEstimado', tiempoTotal);
    }

    /**
     * Asigna texto a un elemento por id si existe.
     * @param {string} id
     * @param {string} text
     */
    function setText(id, text) {
        const el = document.getElementById(id);
        if (el) el.textContent = text;
    }

    /**
     * Asigna valor a un input por id si existe.
     * @param {string} id
     * @param {string|number} value
     */
    function setValue(id, value) {
        const el = document.getElementById(id);
        if (el) el.value = value;
    }

    // ===================== Formulario AJAX (crear/actualizar) =====================

    /**
     * Envía el formulario de paquete por AJAX y reemplaza el parcial.
     * @param {HTMLFormElement} form
     * @param {SubmitEvent} event
     */
    window.submitPaqueteAjax = function (form, event) {
        if (event) event.preventDefault();

        if (serviciosSeleccionados.length < 2) {
            showFormMessage('Debe seleccionar al menos 2 servicios.', 'error');
            document.getElementById('servicios-error')?.classList.remove('hidden');
            return false;
        }
        document.getElementById('servicios-error')?.classList.add('hidden');

        const serviciosIdsJson = document.getElementById('ServiciosIdsJson');
        if (serviciosIdsJson) serviciosIdsJson.value = JSON.stringify(serviciosSeleccionados.map(s => s.id));

        const formData = new FormData(form);
        fetch(form.action, { method: 'POST', body: formData, headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(resp => {
                const isValid = resp.headers.get('X-Form-Valid') === 'true';
                const msg = resp.headers.get('X-Form-Message');
                const title = resp.headers.get('X-Form-Title');
                return resp.text().then(html => ({ html, isValid, msg, title, ok: resp.ok, status: resp.status }));
            })
            .then(result => {
                const cont = document.getElementById('paquete-form-container');
                if (cont) cont.innerHTML = result.html;

                const titleSpan = document.getElementById('form-title');
                if (titleSpan) {
                    if (result.title && result.title.trim().length) {
                        titleSpan.textContent = result.title;
                    } else {
                        const isEdit = !!document.getElementById('Id')?.value;
                        titleSpan.textContent = isEdit ? 'Editando un Paquete de Servicios'
                            : 'Registrando un Paquete de Servicios';
                    }
                }

                // NUEVO: reinstalar handler de tipo de vehículo
                setupDescuentoStep();
                protegerTipoVehiculoEnEdicion();
                setupTipoVehiculoChangeHandler();
                initializeFormFromHidden();

                if (!result.ok) {
                    console.error('HTTP error submitPaqueteAjax:', result.status);
                    showFormMessage('Error al procesar la solicitud.', 'error');
                    return;
                }

                if (result.isValid) {
                    showFormMessage(result.msg || 'Operación exitosa.', 'success');
                    const searchEl = document.getElementById('simple-search');
                    if (searchEl) searchEl.value = '';
                    currentSearchTerm = '';
                    setHiddenValue('pageNumber', '1');
                    reloadPaqueteTable(1);

                    //setTimeout(() => {
                    //    document.getElementById('accordion-flush-body-1')?.classList.add('hidden');
                    //}, 1500);
                } else {
                    showFormMessage('Revise los errores del formulario.', 'error');
                }
            })
            .catch(err => {
                console.error('Network/parse error submitPaqueteAjax:', err);
                showFormMessage('Error al procesar la solicitud.', 'error');
            });

        return false;
    };

    // ===================== Edición (carga parcial del formulario) =====================

    /**
     * Carga el formulario parcial (edición) y reinstala hooks.
     * @param {string} id
     */
    window.editPaquete = function (id) {
        const url = '/PaqueteServicio/FormPartial?id=' + encodeURIComponent(id);
        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
          .then(async r => {
const srvTitle = r.headers.get('X-Form-Title');
       const html = await r.text();
      const cont = document.getElementById('paquete-form-container');
            if (cont) cont.innerHTML = html;

        const titleSpan = document.getElementById('form-title');
    if (titleSpan) {
 if (srvTitle && srvTitle.trim().length) {
         titleSpan.textContent = srvTitle;
         } else {
     const isEdit = !!document.getElementById('Id')?.value;
 titleSpan.textContent = isEdit ? 'Editando un Paquete de Servicios'
      : 'Registrando un Paquete de Servicios';
 }
    }

      // reinstalar handler de tipo de vehículo
   setupDescuentoStep();
        protegerTipoVehiculoEnEdicion();
setupTipoVehiculoChangeHandler();
   initializeFormFromHidden();

         const accBody = document.getElementById('accordion-flush-body-1');
        if (accBody) accBody.classList.remove('hidden');

            // NUEVO: Scroll automático hacia el formulario
    setTimeout(() => {
        const formContainer = document.getElementById('accordion-flush');
       if (formContainer) {
         formContainer.scrollIntoView({ behavior: 'smooth', block: 'start' });
          }
            }, 100);
        })
        .catch(err => console.error('Error cargando formulario de paquete:', err));
    };

    /**
     * Reconstruye la selección de servicios desde el hidden (modo edición).
     */
    function initializeFormFromHidden() {
        const hidden = document.getElementById('PaqueteServiciosIdsData');
        const raw = hidden?.value?.trim();
        const ids = raw ? raw.split(',').map(x => x.trim()).filter(x => x) : [];
        if (!ids.length) return;
        const tipoVehiculo = document.getElementById('TipoVehiculo')?.value;
        if (!tipoVehiculo) return;

        serviciosSeleccionados = [];
        loadServiciosPorTipoVehiculo().then(() => {
            ids.forEach(id => {
                const s = serviciosDisponibles.find(x => x.id === id);
                if (!s) return;
                serviciosSeleccionados.push({
                    id: s.id,
                    nombre: s.nombre,
                    tipo: s.tipo,
                    precio: s.precio,
                    tiempoEstimado: s.tiempoEstimado
                });
            });
            updateServiciosSeleccionadosList();
            updateResumen();
            const acc = document.getElementById('accordion-flush-body-1');
            if (acc && acc.classList.contains('hidden')) acc.classList.remove('hidden');
        });
    }

    // ===================== Activar / Desactivar (modal confirmación) =====================

    /**
     * Abre el modal de confirmación (Flowbite + íconos correctos).
     * @param {'desactivar'|'reactivar'} tipoAccion
     * @param {string} id
     * @param {string} nombre
     */
    window.openPaqueteConfirmModal = function (tipoAccion, id, nombre) {
        const modalId = 'paqueteConfirmModal';
        const modal = document.getElementById(modalId);
        const title = document.getElementById('paqueteConfirmTitle');
        const message = document.getElementById('paqueteConfirmMessage');
        const idInput = document.getElementById('paqueteConfirmId');
        const submitBtn = document.getElementById('paqueteConfirmSubmit');
        const form = document.getElementById('paqueteConfirmForm');
        const iconWrapper = document.getElementById('paqueteConfirmIconWrapper');
        const icon = document.getElementById('paqueteConfirmIcon');

        const esDesactivar = tipoAccion === 'desactivar';

        // Título y mensaje
        title.textContent = esDesactivar ? 'Desactivar Paquete' : 'Reactivar Paquete';
        message.innerHTML = esDesactivar
            ? '¿Confirma desactivar el paquete <strong>' + (window.SiteModule?.escapeHtml?.(nombre) || escapeHtml(nombre)) + '</strong>?'
            : '¿Confirma reactivar el paquete <strong>' + (window.SiteModule?.escapeHtml?.(nombre) || escapeHtml(nombre)) + '</strong>?';

        // Hidden id y acción
        idInput.value = id;
        form.action = esDesactivar ? '/PaqueteServicio/DeactivatePaquete' : '/PaqueteServicio/ReactivatePaquete';

        // Botón y estilos
        submitBtn.textContent = esDesactivar ? 'Desactivar' : 'Reactivar';
        submitBtn.className = esDesactivar
            ? 'py-2 px-3 text-sm font-medium text-center text-white bg-red-600 rounded-lg hover:bg-red-700 focus:ring-4 focus:outline-none focus:ring-red-300 dark:bg-red-500 dark:hover:bg-red-600 dark:focus:ring-red-900'
            : 'py-2 px-3 text-sm font-medium text-center text-white bg-green-600 rounded-lg hover:bg-green-700 focus:ring-4 focus:outline-none focus:ring-green-300 dark:bg-green-500 dark:hover:bg-green-600 dark:focus:ring-green-900';

        // Ícono (estilo Servicios)
        if (esDesactivar) {
            iconWrapper.className = 'w-12 h-12 rounded-full bg-red-100 dark:bg-red-900 p-2 flex items-center justify-center mx-auto mb-3.5';
            icon.setAttribute('fill', 'currentColor');
            icon.setAttribute('viewBox', '0 0 20 20');
            icon.setAttribute('class', 'w-8 h-8 text-red-600 dark:text-red-400');
            icon.innerHTML = `<path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-11.293a1 1 0 00-1.414-1.414L10 7.586 7.707 5.293a1 1 0 00-1.414 1.414L8.586 10l-2.293 2.293a1 1 0 001.414 1.414L10 12.414l2.293 2.293a1 1 0 001.414-1.414L13.06 12l2.293-2.293a.75.75 0 1 0-1.06-1.06L12 10.94l-1.72-1.72Z" clip-rule="evenodd"/>`;
        } else {
            iconWrapper.className = 'w-12 h-12 rounded-full bg-green-100 dark:bg-green-900 p-2 flex items-center justify-center mx-auto mb-3.5';
            icon.setAttribute('fill', 'currentColor');
            icon.setAttribute('viewBox', '0 0 24 24');
            icon.setAttribute('class', 'w-8 h-8 text-green-500 dark:text-green-400');
            icon.innerHTML = `<path fill-rule="evenodd" d="M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 4.365 9.75 9.75-4.365 9.75-9.75 9.75S2.25 17.385 2.25 12Zm13.36-1.814a.75.75 0 1 0-1.22-.872l-3.236 4.53L9.53 12.22a.75.75 0 0 0-1.06 1.06l2.25 2.25a.75.75 0 0 0 1.14-.094l3.75-5.25Z" clip-rule="evenodd"/>`;
        }

        // Mostrar con Flowbite (backdrop)
        abrirModal(modalId);
    };

    /**
     * Cierra el modal de confirmación de paquetes (usa Flowbite si disponible).
     */
    window.closePaqueteConfirmModal = function () {
        cerrarModal('paqueteConfirmModal');
    };

    /**
     * Envía el cambio de estado (activar/desactivar) vía AJAX.
     * @param {HTMLFormElement} form
     */
    window.submitPaqueteEstado = function (form) {
        const fd = new FormData(form);
        fetch(form.action, { method: 'POST', body: fd, headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => {
                if (!r.ok) throw new Error('Estado HTTP ' + r.status);
                const accion = form.action.includes('Deactivate') ? 'desactivado' : 'reactivado';
                showTableMessage('success', `Paquete ${accion} correctamente.`);
                closePaqueteConfirmModal();
                reloadPaqueteTable(window.getCurrentTablePage ? window.getCurrentTablePage() : 1);
            })
            .catch(err => {
                console.error('submitPaqueteEstado error:', err);
                showTableMessage('error', 'No se pudo completar la acción.');
                closePaqueteConfirmModal();
            });
        return false;
    };

    // ===================== Paso de descuento =====================

    /**
     * Obtiene el step de descuento desde config o dataset (mínimo 5).
     */
    function getDescuentoStep() {
        // 1. Intentar desde configuración global
        if (window.AppConfig?.descuentoStep) {
            let step = parseInt(window.AppConfig.descuentoStep, 10);
            if (!isNaN(step) && step >= 1) return Math.max(step, 5);
        }

        // 2. Intentar desde ViewBag (inyectado en el HTML)
        const stepFromViewBag = document.querySelector('[data-descuento-step]')?.dataset?.descuentoStep;
        if (stepFromViewBag) {
            let step = parseInt(stepFromViewBag, 10);
            if (!isNaN(step) && step >= 1) return Math.max(step, 5);
        }

        // 3. Intentar desde el input del formulario
        const inputStep = document.getElementById('PorcentajeDescuento')?.dataset?.step
            || document.getElementById('PorcentajeDescuento')?.getAttribute('step');
        if (inputStep) {
            let step = parseInt(inputStep, 10);
            if (!isNaN(step) && step >= 1) return Math.max(step, 5);
        }

        // 4. Fallback final: 5 (solo si nada más está definido)
        return 5;
    }

    /**
 * Aplica restricciones de mínimo/máximo en inputs de descuento.
  * Con step="any" en HTML, no hay validación de múltiplos.
 */
    function setupDescuentoStep() {
  const minDescuento = getDescuentoStep(); // Este es el MÍNIMO permitido

        /**
   * Valida que el valor esté dentro del rango permitido
         */
    const validate = (el) => {
      if (!el) return;
   let v = parseFloat(el.value);

       // Si está vacío o inválido, permitir (se validará en servidor)
    if (isNaN(v) || el.value.trim() === '') {
    return;
         }

        // Limitar al rango [minDescuento, 95]
   if (v < minDescuento) {
   el.value = String(minDescuento);
      } else if (v > 95) {
    el.value = '95';
    }
        };

        /**
   * Configura un input de descuento con atributos y listeners
   */
  const wire = (el) => {
if (!el) return;

    // Configurar solo atributos min/max (step="any" ya está en HTML)
       el.setAttribute('min', String(minDescuento));
       el.setAttribute('max', '95');

     // Evitar duplicar listeners
     if (el.dataset.stepSetup === 'true') return;

     // Validar en blur (cuando pierde foco)
        el.addEventListener('blur', () => {
       validate(el);

        // Si es el campo del formulario principal, recalcular resumen
     if (el.id === 'PorcentajeDescuento') {
      updateResumen();
     }
});

      // Para el campo del formulario principal, recalcular al cambiar
     if (el.id === 'PorcentajeDescuento') {
     el.addEventListener('input', () => {
      // Recalcular en vivo sin validar (permite escribir)
       updateResumen();
    });
       }

      el.dataset.stepSetup = 'true';
  };

     // Aplicar a todos los inputs de descuento
        wire(document.getElementById('PorcentajeDescuento'));
   wire(document.getElementById('descuentoMin'));
  wire(document.getElementById('descuentoMax'));
    }

    // ===================== Mensajería =====================

    /**
     * Muestra un mensaje inline en el formulario (auto-ocultable).
     * @param {'success'|'info'|'error'} type
     * @param {string} message
     */
    function showFormMessage(message, type) {
        const existing = document.getElementById('form-message-container');
        let container = existing;
        let textEl;
        if (!existing) {
            container = document.createElement('div');
            container.id = 'form-message-container';
            container.className = 'm-4';
            textEl = document.createElement('div');
            textEl.id = 'form-message-text';
            container.appendChild(textEl);
            (document.getElementById('paquete-form-container') || document.body).prepend(container);
        } else {
            textEl = document.getElementById('form-message-text');
        }

        textEl.textContent = message;
        container.className = `m-4 p-4 mb-4 text-sm rounded-lg ${type === 'success'
            ? 'text-green-800 bg-green-50 dark:bg-gray-800 dark:text-green-400'
            : type === 'info'
                ? 'text-blue-800 bg-blue-50 dark:bg-gray-800 dark:text-blue-400'
                : 'text-red-800 bg-red-50 dark:bg-gray-800 dark:text-red-400'
            }`;

        container.classList.remove('hidden');
        if (formMsgTimeout) clearTimeout(formMsgTimeout);
        formMsgTimeout = setTimeout(() => container.classList.add('hidden'), 5000);
    }

    /**
     * Oculta el mensaje del formulario (si existe).
     */
    function hideFormMessage() {
        document.getElementById('form-message-container')?.classList.add('hidden');
    }

    /**
     * Muestra un mensaje sobre la tabla con animación y autohide.
     * @param {'success'|'info'|'error'} type
     * @param {string} msg
     * @param {number} disappearMs
     */
    function showTableMessage(type, msg, disappearMs = 5000) {
        let container = document.getElementById('paquete-table-messages');
        if (!container) {
            container = document.createElement('div');
            container.id = 'paquete-table-messages';
            container.className = 'mb-4';
            const tableWrapper = document.getElementById('paquete-table-container');
            if (tableWrapper?.parentNode) {
                tableWrapper.parentNode.insertBefore(container, tableWrapper);
            } else {
                document.body.prepend(container);
            }
        }
        if (tableMsgTimeout) { clearTimeout(tableMsgTimeout); tableMsgTimeout = null; }

        const color =
            type === 'success' ? { bg: 'green-50', text: 'green-800', dark: 'green-400', border: 'green-300' } :
                type === 'info' ? { bg: 'blue-50', text: 'blue-800', dark: 'blue-400', border: 'blue-300' } :
                    { bg: 'red-50', text: 'red-800', dark: 'red-400', border: 'red-300' };

        container.innerHTML = `<div class="opacity-100 transition-opacity duration-700
            p-4 mb-4 text-sm rounded-lg border
            bg-${color.bg} text-${color.text} border-${color.border}
            dark:bg-gray-800 dark:text-${color.dark}">
            ${escapeHtml(msg)}
        </div>`;

        try { container.scrollIntoView({ behavior: 'smooth', block: 'start' }); } catch { }

        tableMsgTimeout = setTimeout(() => {
            const alertEl = container.firstElementChild;
            if (alertEl) {
                alertEl.classList.add('opacity-0');
                setTimeout(() => { try { alertEl.remove(); } catch { } }, 700);
            }
        }, disappearMs);
    }

    /**
     * Oculta mensajes del formulario cuando se edita algún campo del mismo.
     */
    function setupFormMessageHandler() {
        document.addEventListener('input', (e) => {
            if (e.target.closest('#paquete-form')) hideFormMessage();
        });
    }

    // ===================== Utilidades varias =====================

    /**
     * Cierra el dropdown de servicios si se hace click fuera.
     */
    function setupDropdownClickOutside() {
        document.addEventListener('click', (e) => {
            const dropdown = document.getElementById('servicio-dropdown');
            const searchInput = document.getElementById('servicio-search');
            if (dropdown && !dropdown.contains(e.target) && e.target !== searchInput) {
                dropdown.classList.add('hidden');
            }
        });
    };

    /**
     * Escapa HTML para prevenir XSS en inserciones de texto.
     * @param {string} text
     */
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
    function setupTipoVehiculoChangeHandler() {
        const tipoVehiculoSelect = document.getElementById('TipoVehiculo');
        if (!tipoVehiculoSelect) return;

        // NUEVO: Si está protegido (modo edición), no enganchar eventos
        if (tipoVehiculoSelect.dataset.protected === 'true') {
            return;
        }

        if (tipoVehiculoSelect.dataset.changeSetup === 'true') return;

        tipoVehiculoSelect.addEventListener('change', () => {
            try {
                window.loadServiciosPorTipoVehiculo();
            } catch (e) {
                console.error('Error al cargar servicios:', e);
            }
        });

        tipoVehiculoSelect.dataset.changeSetup = 'true';
    }
    /**
    * NUEVO: Protege el campo TipoVehiculo en modo edición
    */
    function protegerTipoVehiculoEnEdicion() {
        const tipoVehiculoSelect = document.getElementById('TipoVehiculo');
        const form = document.getElementById('paquete-form');

        if (!tipoVehiculoSelect || !form) return;

        const isEdit = form.dataset.edit === 'True' || form.dataset.edit === 'true';

        if (isEdit) {
            // 1. Deshabilitar el select
            tipoVehiculoSelect.disabled = true;

            // 2. Aplicar estilos visuales
            tipoVehiculoSelect.classList.add('cursor-not-allowed', 'opacity-60');

            // 3. Remover el evento onchange si existe
            tipoVehiculoSelect.onchange = null;
            tipoVehiculoSelect.removeAttribute('onchange');

            // 4. Prevenir cualquier intento de cambio mediante eventos
            ['change', 'input', 'click', 'mousedown', 'keydown'].forEach(eventType => {
                tipoVehiculoSelect.addEventListener(eventType, function (e) {
                    e.preventDefault();
                    e.stopPropagation();
                    e.stopImmediatePropagation();
                    return false;
                }, true);
            });

            // 5. Congelar la propiedad disabled
            try {
                Object.defineProperty(tipoVehiculoSelect, 'disabled', {
                    value: true,
                    writable: false,
                    configurable: false
                });
            } catch (e) {
                // Algunos navegadores no permiten esto, pero los eventos anteriores lo cubren
                console.log('No se pudo congelar la propiedad disabled, pero el select está protegido por eventos');
            }

            // 6. Marcar como protegido para evitar que setupTipoVehiculoChangeHandler lo modifique
            tipoVehiculoSelect.dataset.protected = 'true';
        }
    }
    // ===================== Flowbite modals (backdrop) =====================

    /**
     * Obtiene o crea una instancia Flowbite Modal para un elemento.
     * Compatible con Flowbite 2.x (getInstance/getOrCreateInstance).
     * @param {HTMLElement} modalEl
     */
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

    /**
     * Abre un modal por id, usando Flowbite si está disponible.
     * @param {string} modalId
     */
    function abrirModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        try {
            const inst = getFlowbiteModal(modal);
            if (inst && typeof inst.show === 'function') {
                inst.show(); // Flowbite crea y maneja el backdrop
                return;
            }
        } catch { /* no-op */ }

        // Fallback mínimo
        modal.classList.remove('hidden');
        modal.setAttribute('aria-hidden', 'false');
    }

    /**
     * Cierra un modal por id, intentando limpiar backdrop y restaurar scroll.
     * @param {string} modalId
     */
    function cerrarModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        let closed = false;

        // 1) API Flowbite
        try {
            const inst = getFlowbiteModal(modal);
            if (inst && typeof inst.hide === 'function') {
                inst.hide();
                closed = true;
            }
        } catch { /* no-op */ }

        // 2) Click en backdrop si existe
        try {
            const backdrop = document.querySelector('[modal-backdrop]');
            if (backdrop) {
                backdrop.click();
                closed = true;
            }
        } catch { /* no-op */ }

        // 3) Click en elementos con data-modal-hide="..."
        try {
            document.querySelectorAll(`[data-modal-hide="${modalId}"]`).forEach(btn => btn.click());
        } catch { /* no-op */ }

        // 4) Fallback final
        modal.classList.add('hidden');
        modal.setAttribute('aria-hidden', 'true');
        document.querySelectorAll('[modal-backdrop]').forEach(b => b.remove());
        document.body.classList.remove('overflow-hidden');
    }

})();