/**
 * ================================================
 * PAQUETESERVICIO.JS - Funcionalidad página Paquetes de Servicios
 * ================================================
 * Objetivos del refactor:
 *  - Arreglar limpieza completa de filtros (URL, inputs, búsqueda, ayudas de precio).
 *  - Evitar auto-submit / recarga provocada por otros scripts al limpiar.
 *  - Reducir código duplicado (unificar construcción de parámetros y carga parcial).
 */

(function () {
    'use strict';

    // ===================== Estado interno =====================
    let paqueteMsgTimeout = null;
    let tableMsgTimeout = null;
    let searchTimeout = null;
    let currentSearchTerm = '';
    let serviciosDisponibles = [];
    let serviciosSeleccionados = [];
    let servicioSeleccionadoDropdown = null;

    // Flags limpieza
    let isClearingFilters = false;
    let originalFormSubmit = null;

    // ===================== Exposición módulo ==================
    window.PageModules = window.PageModules || {};
    window.PageModules.paquetes = { init: initializePaquetesPage };

    document.addEventListener('DOMContentLoaded', () => {
        try { window.PageModules?.paquetes?.init(); }
        catch { initializePaquetesPage(); }
    });

    // ===================== Inicialización ======================
    function initializePaquetesPage() {
        setupFormMessageHandler();
        setupSearchWithDebounce();
        window.CommonUtils?.setupDefaultFilterForm();
        setupDynamicPriceHints();
        setupServiciosCantidadListener();
        attachFilterFormGuards();
        setupClearFiltersButton();
        checkEditMode();
        initializeForm();
        setupDropdownClickOutside();
    }

    // ===================== Guardas de formulario filtros =======
    function attachFilterFormGuards() {
        const form = document.getElementById('filterForm');
        if (!form || form.hasAttribute('data-guards')) return;

        // Bloquear submit mientras se limpia (captura)
        form.addEventListener('submit', (e) => {
            if (isClearingFilters) {
                e.preventDefault();
                e.stopImmediatePropagation();
                return false;
            }
        }, true);

        // Evitar efectos secundarios de listeners externos durante limpieza
        ['change', 'input'].forEach(evt => {
            form.addEventListener(evt, (e) => {
                if (isClearingFilters) {
                    e.stopImmediatePropagation();
                    e.stopPropagation();
                }
            }, true);
        });

        form.setAttribute('data-guards', 'true');
    }

    function temporarilyDisableProgrammaticSubmit(ms = 1500) {
        if (!originalFormSubmit) originalFormSubmit = HTMLFormElement.prototype.submit;
        HTMLFormElement.prototype.submit = function () {
            if (isClearingFilters && this.id === 'filterForm') return;
            return originalFormSubmit.apply(this, arguments);
        };
        setTimeout(() => { HTMLFormElement.prototype.submit = originalFormSubmit; }, ms);
    }
    // ===================== Limpieza filtros ====================
    function setupClearFiltersButton() {
        const clearBtn = document.getElementById('clearFiltersBtn');
        if (!clearBtn || clearBtn.hasAttribute('data-setup')) return;

        clearBtn.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation(); // evita handlers globales (site.js)
            clearPaqueteFilters();        // usar función local, no window.clearAllFilters
        });

        clearBtn.setAttribute('data-setup', 'true');
    }

    // Limpia los filtros
    function clearPaqueteFilters() {
        const form = document.getElementById('filterForm');
        if (!form) return;

        isClearingFilters = true;
        if (searchTimeout) { clearTimeout(searchTimeout); searchTimeout = null; }

        // 1) Reset búsqueda (fuera del form)
        const searchInput = document.getElementById('simple-search');
        if (searchInput) searchInput.value = '';
        currentSearchTerm = '';

        // 2) Reset del formulario (y limpiar min/max de numéricos)
        form.reset();
        form.querySelectorAll('input[type="number"]').forEach(n => {
            n.value = '';
            n.removeAttribute('min');
            n.removeAttribute('max');
        });

        // 3) Selects a índice 0
        form.querySelectorAll('select').forEach(sel => { sel.selectedIndex = 0; });

        // 4) Estado por defecto: solo “Activo” (ajusta si quieres ambos)
        const activo = form.querySelector('input[name="estados"][value="Activo"]');
        const inactivo = form.querySelector('input[name="estados"][value="Inactivo"]');
        if (activo) activo.checked = true;
        if (inactivo) inactivo.checked = false;

        // 5) Reset paginación y orden
        const setHiddenValue = (id, val) => { const el = document.getElementById(id); if (el) el.value = val; };
        setHiddenValue('pageNumber', '1');
        setHiddenValue('current-sort-by', 'Nombre');
        setHiddenValue('current-sort-order', 'asc');

        // 6) Reset URL (sin recargar)
        if (history?.replaceState) history.replaceState({}, document.title, '/PaqueteServicio/Index');

        // 7) Limpiar ayudas de precio
        const helpMin = document.getElementById('precioMin-help');
        const helpMax = document.getElementById('precioMax-help');
        if (helpMin) helpMin.textContent = '';
        if (helpMax) helpMax.textContent = '';
        document.getElementById('precioMin')?.removeAttribute('min');
        document.getElementById('precioMax')?.removeAttribute('max');

        // 8) Recargar tabla limpia (AJAX)
        const params = new URLSearchParams();
        params.set('pageNumber', '1');
        params.set('sortBy', 'Nombre');
        params.set('sortOrder', 'asc');
        if (activo?.checked) params.append('estados', 'Activo');

        fetch('/PaqueteServicio/TablePartial?' + params.toString(), { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const container = document.getElementById('paquete-table-container');
                if (container) container.innerHTML = html;
            })
            .catch(() => {
                const container = document.getElementById('table-message-container');
                const text = document.getElementById('table-message-text');
                if (container && text) {
                    text.textContent = 'Error al cargar los datos';
                    container.className = 'p-4 mb-4 mx-4 text-sm rounded-lg text-red-800 bg-red-50 dark:bg-gray-800 dark:text-red-400';
                    container.classList.remove('hidden');
                }
            });

        // 9) Refrescar hints de precio después de limpiar
        if (typeof window.refreshPriceRangeHints === 'function') {
            setTimeout(() => { try { window.refreshPriceRangeHints(); } catch { } }, 150);
        }

        setTimeout(() => { isClearingFilters = false; }, 600);
        // Mensaje informativo
        showMessage('info', 'Filtros reestablecidos.');
    }

    function setHiddenValue(id, value) {
        const el = document.getElementById(id);
        if (el) el.value = value;
    }

    // ===================== Búsqueda Debounce ===================
    function setupSearchWithDebounce() {
        const original = document.getElementById('simple-search');
        if (!original) return;
        const searchInput = original.cloneNode(true);
        original.parentNode.replaceChild(searchInput, original);

        currentSearchTerm = searchInput.value?.trim() || '';

        searchInput.addEventListener('input', function () {
            const searchTerm = this.value.trim();
            if (searchTimeout) clearTimeout(searchTimeout);

            if (searchTerm === '') {
                currentSearchTerm = '';
                reloadPaqueteTable(1);
                return;
            }

            searchTimeout = setTimeout(() => {
                currentSearchTerm = searchTerm;
                const params = buildBaseParams();
                params.set('searchTerm', searchTerm);
                params.set('pageNumber', '1');
                loadTablePartial('/PaqueteServicio/SearchPartial?' + params.toString());
            }, 500);
        });
    }

    // ===================== Construcción parámetros =============
    function buildBaseParams() {
        const form = document.getElementById('filterForm');
        const params = new URLSearchParams();
        if (form) {
            const fd = new FormData(form);
            for (const [k, v] of fd.entries()) params.append(k, v);
        }
        // Orden
        const sortBy = document.getElementById('current-sort-by')?.value || 'Nombre';
        const sortOrder = document.getElementById('current-sort-order')?.value || 'asc';
        params.set('sortBy', sortBy);
        params.set('sortOrder', sortOrder);
        return params;
    }

    // ===================== Recarga tabla =======================
    function reloadPaqueteTable(pageNumber) {
        const params = buildBaseParams();
        params.set('pageNumber', String(pageNumber));
        if (currentSearchTerm) {
            params.set('searchTerm', currentSearchTerm);
            loadTablePartial('/PaqueteServicio/SearchPartial?' + params.toString());
        } else {
            loadTablePartial('/PaqueteServicio/TablePartial?' + params.toString());
        }
    }

    function loadTablePartial(url) {
        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const container = document.getElementById('paquete-table-container');
                if (container) container.innerHTML = html;
            })
            .catch(() => showTableMessage('Error al cargar los datos', 'error'));
    }

    // ===================== Orden y paginación ==================
    window.changePage = (page) => reloadPaqueteTable(page);

    window.sortTable = function (sortBy) {
        const currentSortBy = document.getElementById('current-sort-by')?.value;
        const currentSortOrder = document.getElementById('current-sort-order')?.value || 'asc';
        let newOrder = 'asc';
        if (currentSortBy === sortBy) newOrder = currentSortOrder === 'asc' ? 'desc' : 'asc';
        setHiddenValue('current-sort-by', sortBy);
        setHiddenValue('current-sort-order', newOrder);
        reloadPaqueteTable(1);
    };

    // ===================== Ayudas dinámicas precio =============
    function setupDynamicPriceHints() {
        const form = document.getElementById('filterForm');
        if (!form) return;

        const precioMinEl = document.getElementById('precioMin');
        const precioMaxEl = document.getElementById('precioMax');
        const helpMin = document.getElementById('precioMin-help');
        const helpMax = document.getElementById('precioMax-help');

        async function refreshPriceRangeHints() {
            try {
                const params = new URLSearchParams();
                const fd = new FormData(form);
                for (const [k, v] of fd.entries()) {
                    if (['precioMin', 'precioMax', 'pageNumber'].includes(k)) continue;
                    params.append(k, v);
                }
                if (currentSearchTerm) params.append('searchTerm', currentSearchTerm);

                const url = '/PaqueteServicio/PriceRange?' + params.toString();
                const resp = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                const data = await resp.json();
                if (data?.success) {
                    const fmt = n => (typeof n === 'number' ? n.toFixed(2) : (n != null ? parseFloat(n).toFixed(2) : ''));
                    if (helpMin) helpMin.textContent = (data.min != null) ? `Mín. permitido: $${fmt(data.min)}` : '';
                    if (helpMax) helpMax.textContent = (data.max != null) ? `Máx. permitido: $${fmt(data.max)}` : '';
                    if (precioMinEl && data.min != null) precioMinEl.setAttribute('min', parseFloat(data.min)); else precioMinEl?.removeAttribute('min');
                    if (precioMaxEl && data.max != null) precioMaxEl.setAttribute('max', parseFloat(data.max)); else precioMaxEl?.removeAttribute('max');
                } else {
                    if (helpMin) helpMin.textContent = '';
                    if (helpMax) helpMax.textContent = '';
                    precioMinEl?.removeAttribute('min');
                    precioMaxEl?.removeAttribute('max');
                }
            } catch {
                if (helpMin) helpMin.textContent = '';
                if (helpMax) helpMax.textContent = '';
                precioMinEl?.removeAttribute('min');
                precioMaxEl?.removeAttribute('max');
            }
        }

        // Exponer global para reutilizar tras limpiar
        window.refreshPriceRangeHints = refreshPriceRangeHints;

        refreshPriceRangeHints();

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
        if (search) {
            search.addEventListener('input', () => {
                setTimeout(refreshPriceRangeHints, 550);
            });
        }
    }

    // ===================== Listener cantidad servicios =========
    function setupServiciosCantidadListener() {
        const sel = document.getElementById('serviciosCantidad');
        if (sel) sel.addEventListener('change', () => reloadPaqueteTable(1));
    }

    // ===================== Utilidades interfaz principal =======
    function checkEditMode() {
        const formTitle = document.getElementById('form-title');
        if (formTitle && formTitle.textContent.includes('Editando')) {
            document.getElementById('accordion-flush-body-1')?.classList.remove('hidden');
        }
    }

    function initializeForm() {
        if (!(window.paqueteEditData?.serviciosIds?.length)) return;
        const tipoVehiculo = document.getElementById('TipoVehiculo')?.value;
        if (!tipoVehiculo) return;

        loadServiciosPorTipoVehiculo().then(() => {
            window.paqueteEditData.serviciosIds.forEach(id => {
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
        });
    }

    function setupFormMessageHandler() {
        document.addEventListener('input', (e) => {
            if (e.target.closest('#paquete-form')) hidePaqueteMessage();
        });
    }

    function setupDropdownClickOutside() {
        document.addEventListener('click', (e) => {
            const dropdown = document.getElementById('servicio-dropdown');
            const searchInput = document.getElementById('servicio-search');
            if (dropdown && !dropdown.contains(e.target) && e.target !== searchInput) {
                dropdown.classList.add('hidden');
            }
        });
    }

    // ===================== Servicios (selector) ================
    window.loadServiciosPorTipoVehiculo = async function () {
        const tipoVehiculo = document.getElementById('TipoVehiculo')?.value;
        const container = document.getElementById('servicio-selector-container');

        if (!tipoVehiculo) {
            container?.classList.add('hidden');
            serviciosDisponibles = [];
            serviciosSeleccionados = [];
            updateResumen();
            return;
        }

        try {
            const response = await fetch(`/PaqueteServicio/ObtenerServiciosPorTipoVehiculo?tipoVehiculo=${encodeURIComponent(tipoVehiculo)}`);
            const data = await response.json();
            if (data.success) {
                serviciosDisponibles = data.servicios;
                container?.classList.remove('hidden');
                renderServiciosDropdown(serviciosDisponibles);
            } else {
                container?.classList.add('hidden');
                showPaqueteMessage(data.message || 'No hay servicios disponibles', 'error');
            }
        } catch {
            container?.classList.add('hidden');
            showPaqueteMessage('Error al cargar servicios', 'error');
        }
    };

    function renderServiciosDropdown(servicios, filterText = '') {
        const dropdownContent = document.getElementById('servicio-dropdown-content');
        if (!dropdownContent) return;

        if (!servicios || !servicios.length) {
            dropdownContent.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No hay servicios disponibles</p>';
            return;
        }

        let filtrados = servicios;
        if (filterText) {
            const lower = filterText.toLowerCase();
            filtrados = filtrados.filter(s =>
                (s.nombre && s.nombre.toLowerCase().includes(lower)) ||
                (s.tipo && s.tipo.toLowerCase().includes(lower))
            );
        }

        filtrados = filtrados.filter(s => !serviciosSeleccionados.some(sel => sel.id === s.id));

        if (!filtrados.length) {
            dropdownContent.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No se encontraron servicios</p>';
            return;
        }

        const grupos = {};
        filtrados.forEach(s => { (grupos[s.tipo] ||= []).push(s); });

        let html = '';
        Object.keys(grupos).sort().forEach(tipo => {
            html += `<div class="mb-2">
                <h6 class="text-xs font-semibold text-gray-700 dark:text-gray-300 px-2 py-1 bg-gray-100 dark:bg-gray-600">${tipo}</h6>
                <div class="space-y-1">`;
            grupos[tipo].forEach(s => {
                const active = servicioSeleccionadoDropdown?.id === s.id ? 'bg-blue-100 dark:bg-blue-900' : '';
                html += `<div class="px-2 py-2 hover:bg-gray-100 dark:hover:bg-gray-600 cursor-pointer ${active}" onclick="selectServicioFromDropdown('${s.id}')">
                            <div class="text-sm font-medium text-gray-900 dark:text-white">${s.nombre}</div>
                         </div>`;
            });
            html += '</div></div>';
        });

        dropdownContent.innerHTML = html;
    }

    window.showServicioDropdown = function () {
        const dropdown = document.getElementById('servicio-dropdown');
        const searchInput = document.getElementById('servicio-search');
        if (dropdown && serviciosDisponibles.length) {
            renderServiciosDropdown(serviciosDisponibles, searchInput?.value || '');
            dropdown.classList.remove('hidden');
        }
    };

    window.filterServiciosDropdown = function (txt) {
        renderServiciosDropdown(serviciosDisponibles, txt);
        document.getElementById('servicio-dropdown')?.classList.remove('hidden');
    };

    window.selectServicioFromDropdown = function (id) {
        const s = serviciosDisponibles.find(x => x.id === id);
        if (!s) return;
        servicioSeleccionadoDropdown = s;
        renderServiciosDropdown(serviciosDisponibles, document.getElementById('servicio-search')?.value || '');
        const searchInput = document.getElementById('servicio-search');
        if (searchInput) searchInput.value = s.nombre;
    };

    window.agregarServicioSeleccionado = function () {
        if (!servicioSeleccionadoDropdown) {
            showPaqueteMessage('Debe seleccionar un servicio del listado', 'error');
            return;
        }
        const tipo = servicioSeleccionadoDropdown.tipo;
        if (serviciosSeleccionados.some(s => s.tipo === tipo)) {
            showPaqueteMessage('Solo puede seleccionar un servicio de cada tipo', 'error');
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

    window.removerServicioSeleccionado = function (id) {
        serviciosSeleccionados = serviciosSeleccionados.filter(s => s.id !== id);
        updateServiciosSeleccionadosList();
        updateResumen();
        renderServiciosDropdown(serviciosDisponibles, document.getElementById('servicio-search')?.value || '');
    };

    function updateServiciosSeleccionadosList() {
        const container = document.getElementById('servicios-seleccionados-container');
        const list = document.getElementById('servicios-seleccionados-list');
        if (!container || !list) return;

        if (!serviciosSeleccionados.length) {
            container.classList.add('hidden');
            return;
        }
        container.classList.remove('hidden');

        list.innerHTML = '<ul class="space-y-2">' + serviciosSeleccionados.map(s => `
            <li class="flex justify-between items-center p-2 bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-600">
                <div>
                    <span class="font-medium text-gray-900 dark:text-white">${s.nombre}</span>
                    <span class="text-sm text-gray-500 dark:text-gray-400 ml-2">(${s.tipo})</span>
                </div>
                <div class="flex items-center gap-3">
                    <div class="text-right">
                        <div class="text-sm font-medium text-gray-900 dark:text-white">$${s.precio.toFixed(2)}</div>
                        <div class="text-xs text-gray-500 dark:text-gray-400">${s.tiempoEstimado} min</div>
                    </div>
                    <button type="button" onclick="removerServicioSeleccionado('${s.id}')" class="p-1 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300" title="Quitar servicio">
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="w-5 h-5">
                            <path fill-rule="evenodd" d="M12 2.25c-5.385 0-9.75 4.365-9.75 9.75s4.365 9.75 9.75 9.75 9.75-4.365 9.75-9.75S17.385 2.25 12 2.25Zm-1.72 6.97a.75.75 0 1 0-1.06 1.06L10.94 12l-1.72 1.72a.75.75 0 1 0 1.06 1.06L12 13.06l1.72 1.72a.75.75 0 1 0 1.06-1.06L13.06 12l1.72-1.72a.75.75 0 1 0-1.06-1.06L12 10.94l-1.72-1.72Z" clip-rule="evenodd" />
                        </svg>
                    </button>
                </div>
            </li>`).join('') + '</ul>';
    }

    window.calcularPrecioYTiempo = function () { updateResumen(); };

    function updateResumen() {
        const descuento = parseFloat(document.getElementById('PorcentajeDescuento')?.value || 0);
        const precioTotal = serviciosSeleccionados.reduce((sum, s) => sum + s.precio, 0);
        const descuentoMonto = precioTotal * (descuento / 100);
        const precioFinal = precioTotal - descuentoMonto;
        const tiempoTotal = serviciosSeleccionados.reduce((sum, s) => sum + s.tiempoEstimado, 0);

        setText('precio-total-sin-descuento', '$' + precioTotal.toFixed(2));
        setText('precio-final', '$' + precioFinal.toFixed(2));
        setText('tiempo-total', tiempoTotal + ' min');

        setValue('Precio', precioFinal.toFixed(2));
        setValue('TiempoEstimado', tiempoTotal);
    }

    function setText(id, text) {
        const el = document.getElementById(id);
        if (el) el.textContent = text;
    }
    function setValue(id, value) {
        const el = document.getElementById(id);
        if (el) el.value = value;
    }

    // ===================== Formulario AJAX =====================
    window.submitPaqueteAjax = function (form, event) {
        if (event) event.preventDefault();

        if (serviciosSeleccionados.length < 2) {
            showPaqueteMessage('Debe seleccionar al menos 2 servicios', 'error');
            document.getElementById('servicios-error')?.classList.remove('hidden');
            return false;
        }
        document.getElementById('servicios-error')?.classList.add('hidden');

        // IDs servicios
        const serviciosIdsJson = document.getElementById('ServiciosIdsJson');
        if (serviciosIdsJson) serviciosIdsJson.value = JSON.stringify(serviciosSeleccionados.map(s => s.id));

        const formData = new FormData(form);

        fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(response => {
                const isValid = response.headers.get('X-Form-Valid') === 'true';
                const message = response.headers.get('X-Form-Message');
                return response.text().then(html => ({ isValid, message, html }));
            })
            .then(data => {
                if (data.isValid) {
                    showPaqueteMessage(data.message || 'Operación exitosa', 'success');
                    limpiarFormularioPaquete();
                    reloadPaqueteTable(1);
                    setTimeout(() => {
                        document.getElementById('accordion-flush-body-1')?.classList.add('hidden');
                    }, 2000);
                } else {
                    document.getElementById('paquete-form-container').innerHTML = data.html;
                    initializeForm();
                }
            })
            .catch(() => showPaqueteMessage('Error al procesar la solicitud', 'error'));

        return false;
    };

    window.limpiarFormularioPaquete = function () {
        const form = document.getElementById('paquete-form');
        if (!form) return;
        const isEdit = form.dataset.edit === 'True';
        if (isEdit) {
            window.location.href = '/PaqueteServicio/Index';
            return;
        }
        form.reset();
        serviciosSeleccionados = [];
        serviciosDisponibles = [];
        servicioSeleccionadoDropdown = null;
        updateServiciosSeleccionadosList();
        updateResumen();
        resetServiceDropdown();
        hidePaqueteMessage();
    };

    function resetServiceDropdown() {
        document.getElementById('servicio-selector-container')?.classList.add('hidden');
        const searchInput = document.getElementById('servicio-search');
        if (searchInput) searchInput.value = '';
        document.getElementById('servicio-dropdown')?.classList.add('hidden');
    }

    window.editPaquete = (id) => window.location.href = `/PaqueteServicio/Index?editId=${id}`;

    // ===================== Modal confirmación ==================
    function getFlowbiteModal(modalEl) {
        if (!modalEl || typeof Modal === 'undefined') return null;
        try {
            if (typeof Modal.getInstance === 'function') {
                const inst = Modal.getInstance(modalEl);
                if (inst) return inst;
            }
            if (typeof Modal.getOrCreateInstance === 'function')
                return Modal.getOrCreateInstance(modalEl, { backdrop: 'dynamic', closable: true });
            return new Modal(modalEl, { backdrop: 'dynamic', closable: true });
        } catch { return null; }
    }

    function abrirModal(id) {
        const m = document.getElementById(id);
        if (!m) return;
        const inst = getFlowbiteModal(m);
        if (inst?.show) { inst.show(); return; }
        m.classList.remove('hidden');
        m.setAttribute('aria-hidden', 'false');
    }

    function cerrarModal(id) {
        const m = document.getElementById(id);
        if (!m) return;
        let closed = false;
        try {
            const inst = getFlowbiteModal(m);
            if (inst?.hide) { inst.hide(); closed = true; }
        } catch { }
        try {
            document.querySelector('[modal-backdrop]')?.click();
            closed = true;
        } catch { }
        if (!closed) {
            m.classList.add('hidden');
            m.setAttribute('aria-hidden', 'true');
            document.querySelectorAll('[modal-backdrop]').forEach(b => b.remove());
            document.body.classList.remove('overflow-hidden');
        }
    }

    window.openPaqueteConfirmModal = function (tipoAccion, id, nombre) {
        const title = document.getElementById('paqueteConfirmTitle');
        const msg = document.getElementById('paqueteConfirmMessage');
        const submitBtn = document.getElementById('paqueteConfirmSubmit');
        const form = document.getElementById('paqueteConfirmForm');
        const idInput = document.getElementById('paqueteConfirmId');
        const iconWrapper = document.getElementById('paqueteConfirmIconWrapper');
        const icon = document.getElementById('paqueteConfirmIcon');
        if (!(title && msg && submitBtn && form && idInput && iconWrapper && icon)) return;

        idInput.value = id;

        if (tipoAccion === 'desactivar') {
            title.textContent = 'Desactivar Paquete';
            msg.innerHTML = `¿Confirma desactivar el paquete <strong>${(window.SiteModule?.escapeHtml?.(nombre) || nombre)}</strong>?`;
            form.action = '/PaqueteServicio/DeactivatePaquete';
            submitBtn.textContent = 'Desactivar';
            submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-red-600 rounded-lg hover:bg-red-700 focus:ring-4 focus:outline-none focus:ring-red-300 dark:bg-red-500 dark:hover:bg-red-600 dark:focus:ring-red-900';
            iconWrapper.className = 'w-12 h-12 rounded-full bg-red-100 dark:bg-red-900 p-2 flex items-center justify-center mx-auto mb-3.5';
            icon.className = 'w-8 h-8 text-red-600 dark:text-red-400';
            icon.innerHTML = '<path fill-rule="evenodd" d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm3.707-11.293a1 1 0 0 0-1.414-1.414L10 7.586 8.707 6.293a1 1 0 0 0-1.414 1.414L8.586 9l-1.293 1.293a1 1 0 1 0 1.414 1.414L10 10.414l1.293 1.293a1 1 0 0 0 1.414-1.414L11.414 9l1.293-1.293Z" clip-rule="evenodd" />';
        } else {
            title.textContent = 'Reactivar Paquete';
            msg.innerHTML = `¿Confirma reactivar el paquete <strong>${(window.SiteModule?.escapeHtml?.(nombre) || nombre)}</strong>?`;
            form.action = '/PaqueteServicio/ReactivatePaquete';
            submitBtn.textContent = 'Reactivar';
            submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-green-600 rounded-lg hover:bg-green-700 focus:ring-4 focus:outline-none focus:ring-green-300 dark:bg-green-500 dark:hover:bg-green-600 dark:focus:ring-green-900';
            iconWrapper.className = 'w-12 h-12 rounded-full bg-green-100 dark:bg-green-900 p-2 flex items-center justify-center mx-auto mb-3.5';
            icon.className = 'w-8 h-8 text-green-500 dark:text-green-400';
            icon.innerHTML = '<path fill-rule="evenodd" d="M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 4.365 9.75 9.75-4.365 9.75-9.75 9.75S2.25 17.385 2.25 12Zm13.36-1.814a.75.75 0 0 0-1.22-.872l-3.236 4.53-1.624-1.624a.75.75 0 0 0-1.06 1.06l2.252 2.25a.75.75 0 0 0 1.14-.094l3.75-5.25Z" clip-rule="evenodd" />';
        }

        abrirModal('paqueteConfirmModal');
    };

    window.closePaqueteConfirmModal = () => cerrarModal('paqueteConfirmModal');

    window.submitPaqueteEstado = function (form) {
        const fd = new FormData(form);
        fetch(form.action, { method: 'POST', body: fd, headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => {
                if (!r.ok) throw new Error('Estado');
                window.closePaqueteConfirmModal();
                const msg = form.action.includes('DeactivatePaquete')
                    ? 'Paquete desactivado correctamente.'
                    : 'Paquete reactivado correctamente.';
                showTableMessage(msg, 'success');
                reloadPaqueteTable(1);
            })
            .catch(() => showTableMessage('Error procesando la operación.', 'error'));
        return false;
    };

    // ===================== Mensajes ============================
    function showPaqueteMessage(message, type) {
        const container = document.getElementById('form-message-container');
        const text = document.getElementById('form-message-text');
        if (!container || !text) return;
        text.textContent = message;
        container.className = `m-4 p-4 mb-4 text-sm rounded-lg ${type === 'success'
            ? 'text-green-800 bg-green-50 dark:bg-gray-800 dark:text-green-400'
            : 'text-red-800 bg-red-50 dark:bg-gray-800 dark:text-red-400'}`;
        container.classList.remove('hidden');
        if (paqueteMsgTimeout) clearTimeout(paqueteMsgTimeout);
        paqueteMsgTimeout = setTimeout(() => container.classList.add('hidden'), 5000);
    }

    function hidePaqueteMessage() {
        document.getElementById('form-message-container')?.classList.add('hidden');
    }

    function showTableMessage(message, type) {
        const container = document.getElementById('table-message-container');
        const text = document.getElementById('table-message-text');
        if (!container || !text) return;
        text.textContent = message;
        container.className = `p-4 mb-4 mx-4 text-sm rounded-lg ${type === 'success'
            ? 'text-green-800 bg-green-50 dark:bg-gray-800 dark:text-green-400'
            : 'text-red-800 bg-red-50 dark:bg-gray-800 dark:text-red-400'}`;
        container.classList.remove('hidden');
        if (tableMsgTimeout) clearTimeout(tableMsgTimeout);
        tableMsgTimeout = setTimeout(() => container.classList.add('hidden'), 5000);
    }

})();