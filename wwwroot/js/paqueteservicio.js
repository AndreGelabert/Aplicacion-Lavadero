    /**
     * ================================================
     * PAQUETESERVICIO.JS - Página Paquetes de Servicios
     * ================================================
     * - Filtros: sólo aplican con submit (Aplicar).
     * - Búsqueda: debounce (500ms).
     * - Limpiar: usa clearAllFilters() (site.js) para estados y limpia el resto local.
     * - Mensajes: contenedores autogenerados y robustos.
     * - Hints de precio: recalculan al iniciar, aplicar, limpiar, búsqueda y tras recargar la tabla.
     */

    (function () {
        'use strict';

        let paqueteMsgTimeout = null;
        let tableMsgTimeout = null;
        let searchTimeout = null;
        let currentSearchTerm = '';

        let serviciosDisponibles = [];
        let serviciosSeleccionados = [];
        let servicioSeleccionadoDropdown = null;

        window.PageModules = window.PageModules || {};
        window.PageModules.paquetes = { init: initializePaquetesPage };

        document.addEventListener('DOMContentLoaded', () => {
            try { window.PageModules?.paquetes?.init(); } catch { initializePaquetesPage(); }
        });

        function initializePaquetesPage() {
            setupFormMessageHandler();
            setupSearchWithDebounce();
            window.CommonUtils?.setupDefaultFilterForm();
            setupDynamicPriceHints();
            setupFilterFormSubmit();
            setupClearFiltersButton();
            checkEditMode();
            initializeForm();
            setupDropdownClickOutside();
        }

        // ===================== Aplicar filtros =====================
        function setupFilterFormSubmit() {
            const form = document.getElementById('filterForm');
            if (!form || form.dataset.submitSetup === 'true') return;

            form.addEventListener('submit', (e) => {
                e.preventDefault();
                e.stopPropagation();

                const pg = form.querySelector('input[name="pageNumber"]');
                if (pg) pg.value = '1';

                const searchInput = document.getElementById('simple-search');
                currentSearchTerm = searchInput?.value.trim() || '';

                document.getElementById('filterDropdown')?.classList.add('hidden');
                if (history.replaceState) history.replaceState(null, '', '/PaqueteServicio/Index');

                reloadPaqueteTable(1);
                showTableMessage('info', 'Filtros aplicados.');

                if (typeof window.refreshPriceRangeHints === 'function') {
                    setTimeout(() => { try { window.refreshPriceRangeHints(); } catch { } }, 150);
                }
            });

            form.dataset.submitSetup = 'true';
        }

        // ===================== Limpiar filtros =====================
        function setupClearFiltersButton() {
            // Soporta diferentes selectores por seguridad
            const btn = document.getElementById('clearFiltersBtn')
                || document.querySelector('[data-action="clear-filters"]')
                || document.querySelector('[data-clear="filters"]');
            if (!btn || btn.dataset.setup === 'true') return;

            btn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                e.stopImmediatePropagation();
                clearPaqueteFilters();
            });

            btn.dataset.setup = 'true';
        }

        function clearPaqueteFilters() {
            const form = document.getElementById('filterForm');
            if (!form) return;

            // 1) Estados con clearAllFilters (o fallback)
            try {
                if (typeof window.clearAllFilters === 'function') {
                    window.clearAllFilters();
                } else {
                    form.querySelectorAll('input[name="estados"][type="checkbox"]').forEach(cb => cb.checked = false);
                    form.querySelector('input[name="estados"][value="Activo"]')?.setAttribute('checked', 'checked');
                }
            } catch { /* no-op */ }

            // 2) Búsqueda
            const searchInput = document.getElementById('simple-search');
            if (searchInput) searchInput.value = '';
            currentSearchTerm = '';
            if (searchTimeout) { clearTimeout(searchTimeout); searchTimeout = null; }

            // 3) Campos propios
            form.querySelectorAll('input[type="number"]').forEach(n => {
                n.value = '';
                n.removeAttribute('min');
                n.removeAttribute('max');
            });
            form.querySelectorAll('select').forEach(sel => sel.selectedIndex = 0);

            // 4) Paginación/orden
            setHiddenValue('pageNumber', '1');
            setHiddenValue('current-sort-by', 'Nombre');
            setHiddenValue('current-sort-order', 'asc');

            // 5) URL limpia
            if (history.replaceState) history.replaceState({}, document.title, '/PaqueteServicio/Index');

            // 6) Hints visuales
            ensureHelpElements();
            document.getElementById('precioMin-help').textContent = '';
            document.getElementById('precioMax-help').textContent = '';
            document.getElementById('precioMin')?.removeAttribute('min');
            document.getElementById('precioMax')?.removeAttribute('max');

            // 7) Cerrar dropdown
            document.getElementById('filterDropdown')?.classList.add('hidden');

            // 8) Recargar
            reloadPaqueteTable(1);

            // 9) Recalcular hints
            if (typeof window.refreshPriceRangeHints === 'function') {
                setTimeout(() => { try { window.refreshPriceRangeHints(); } catch { } }, 150);
            }

            showTableMessage('info', 'Filtros restablecidos.');
        }

        // ===================== Búsqueda (debounce) =================
        function setupSearchWithDebounce() {
            const original = document.getElementById('simple-search');
            if (!original) return;
            const searchInput = original.cloneNode(true);
            original.parentNode.replaceChild(searchInput, original);

            currentSearchTerm = searchInput.value?.trim() || '';

            searchInput.addEventListener('input', function () {
                const term = this.value.trim();
                if (searchTimeout) clearTimeout(searchTimeout);

                if (term === '') {
                    currentSearchTerm = '';
                    reloadPaqueteTable(1);
                    // refrescar hints al volver a estado base
                    if (typeof window.refreshPriceRangeHints === 'function') {
                        setTimeout(() => { try { window.refreshPriceRangeHints(); } catch { } }, 120);
                    }
                    return;
                }

                searchTimeout = setTimeout(() => {
                    currentSearchTerm = term;
                    reloadPaqueteTable(1);
                    if (typeof window.refreshPriceRangeHints === 'function') {
                        setTimeout(() => { try { window.refreshPriceRangeHints(); } catch { } }, 120);
                    }
                }, 500);
            });
        }

        // ===================== Reload tabla ========================
        function buildBaseParams() {
            const form = document.getElementById('filterForm');
            const params = new URLSearchParams();

            if (form) {
                const fd = new FormData(form);
                for (const [k, v] of fd.entries()) params.append(k, v);
            }

            params.set('sortBy', document.getElementById('current-sort-by')?.value || 'Nombre');
            params.set('sortOrder', document.getElementById('current-sort-order')?.value || 'asc');

            return params;
        }

        function reloadPaqueteTable(pageNumber) {
            const params = buildBaseParams();
            params.set('pageNumber', String(pageNumber));

            let url;
            if (currentSearchTerm) {
                params.set('searchTerm', currentSearchTerm);
                url = '/PaqueteServicio/SearchPartial?' + params.toString();
            } else {
                url = '/PaqueteServicio/TablePartial?' + params.toString();
            }
            loadTablePartial(url);
        }

        function loadTablePartial(url) {
            fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                .then(r => r.text())
                .then(html => {
                    const container = document.getElementById('paquete-table-container');
                    if (container) container.innerHTML = html;

                    const currentPageHidden = document.getElementById('current-page-value')?.value;
                    if (currentPageHidden && container) container.dataset.currentPage = currentPageHidden;

                    // Recalcular hints tras pintar
                    if (typeof window.refreshPriceRangeHints === 'function') {
                        setTimeout(() => { try { window.refreshPriceRangeHints(); } catch { } }, 80);
                    }
                })
                .catch((err) => {
                    console.error('loadTablePartial error:', err);
                    showTableMessage('error', 'Error al cargar los datos');
                });
        }

        function setHiddenValue(id, value) {
            const el = document.getElementById(id);
            if (el) el.value = value;
        }

        window.changePage = (page) => reloadPaqueteTable(page);
        window.sortTable = function (sortBy) {
            const currentSortBy = document.getElementById('current-sort-by')?.value;
            const currentSortOrder = document.getElementById('current-sort-order')?.value || 'asc';
            const newOrder = (currentSortBy === sortBy) ? (currentSortOrder === 'asc' ? 'desc' : 'asc') : 'asc';
            setHiddenValue('current-sort-by', sortBy);
            setHiddenValue('current-sort-order', newOrder);
            reloadPaqueteTable(1);
        };

        // ===================== Hints de precio =====================
        function setupDynamicPriceHints() {
            const form = document.getElementById('filterForm');
            if (!form) return;

            ensureHelpElements();

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

                    const resp = await fetch('/PaqueteServicio/PriceRange?' + params.toString(), {
                        headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' }
                    });

                    let data;
                    try { data = await resp.json(); }
                    catch { data = null; }

                    if (data?.success) {
                        const fmt = (n) => (typeof n === 'number' ? n.toFixed(2) : (n != null ? parseFloat(n).toFixed(2) : ''));
                        helpMin.textContent = (data.min != null) ? `Mín. permitido: $${fmt(data.min)}` : '';
                        helpMax.textContent = (data.max != null) ? `Máx. permitido: $${fmt(data.max)}` : '';

                        if (precioMinEl && data.min != null) precioMinEl.setAttribute('min', data.min); else precioMinEl?.removeAttribute('min');
                        if (precioMaxEl && data.max != null) precioMaxEl.setAttribute('max', data.max); else precioMaxEl?.removeAttribute('max');
                    } else {
                        helpMin.textContent = '';
                        helpMax.textContent = '';
                        precioMinEl?.removeAttribute('min');
                        precioMaxEl?.removeAttribute('max');
                    }
                } catch (err) {
                    console.debug('PriceRange hints error:', err);
                    helpMin.textContent = '';
                    helpMax.textContent = '';
                    precioMinEl?.removeAttribute('min');
                    precioMaxEl?.removeAttribute('max');
                }
            }

            // Exponer global
            window.refreshPriceRangeHints = refreshPriceRangeHints;

            // Inicial
            refreshPriceRangeHints();

            // Campos relevantes
            const watched = [
                'input[name="estados"]',
                'input[name="tiposVehiculo"]',
                '#descuentoMin', '#descuentoMax',
                '#serviciosCantidad'
            ];
            watched.forEach(sel =>
                document.querySelectorAll(sel).forEach(el => {
                    el.addEventListener('change', refreshPriceRangeHints);
                    el.addEventListener('input', refreshPriceRangeHints);
                })
            );

            // Búsqueda: refrescar tras debounce aproximado
            const search = document.getElementById('simple-search');
            if (search) search.addEventListener('input', () => setTimeout(refreshPriceRangeHints, 550));
        }

        // Garantiza la existencia de los elementos de ayuda bajo los inputs de precio
        function ensureHelpElements() {
            const min = document.getElementById('precioMin');
            const max = document.getElementById('precioMax');
            if (min && !document.getElementById('precioMin-help')) {
                const help = document.createElement('p');
                help.id = 'precioMin-help';
                help.className = 'mt-1 text-xs text-gray-500 dark:text-gray-400';
                min.parentNode.insertBefore(help, min.nextSibling);
            }
            if (max && !document.getElementById('precioMax-help')) {
                const help = document.createElement('p');
                help.id = 'precioMax-help';
                help.className = 'mt-1 text-xs text-gray-500 dark:text-gray-400';
                max.parentNode.insertBefore(help, max.nextSibling);
            }
        }

        // ===================== UI =====================
        function checkEditMode() {
            const formTitle = document.getElementById('form-title');
            if (formTitle?.textContent.includes('Editando')) {
                document.getElementById('accordion-flush-body-1')?.classList.remove('hidden');
            }
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

        // ===================== Selector de servicios =====================
        window.loadServiciosPorTipoVehiculo = async function () {
            const tipoVehiculo = document.getElementById('TipoVehiculo')?.value;
            const cont = document.getElementById('servicio-selector-container');

            if (!tipoVehiculo) {
                cont?.classList.add('hidden');
                serviciosDisponibles = [];
                serviciosSeleccionados = [];
                updateResumen();
                return;
            }
            try {
                const resp = await fetch(`/PaqueteServicio/ObtenerServiciosPorTipoVehiculo?tipoVehiculo=${encodeURIComponent(tipoVehiculo)}`);
                const data = await resp.json();
                if (data.success) {
                    serviciosDisponibles = data.servicios;
                    cont?.classList.remove('hidden');
                    renderServiciosDropdown(serviciosDisponibles);
                } else {
                    cont?.classList.add('hidden');
                    showPaqueteMessage(data.message || 'No hay servicios disponibles', 'error');
                }
            } catch {
                cont?.classList.add('hidden');
                showPaqueteMessage('Error al cargar servicios', 'error');
            }
        };

        function renderServiciosDropdown(servicios, filterText = '') {
            const target = document.getElementById('servicio-dropdown-content');
            if (!target) return;

            if (!Array.isArray(servicios) || servicios.length === 0) {
                target.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No hay servicios disponibles</p>';
                return;
            }

            let lista = servicios;
            if (filterText) {
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
                    html += `<div class="px-2 py-2 hover:bg-gray-100 dark:hoverbg-gray-600 cursor-pointer ${active}" onclick="selectServicioFromDropdown('${s.id}')">
                            <div class="text-sm font-medium text-gray-900 dark:text-white">${s.nombre}</div>
                         </div>`;
                });
                html += '</div></div>';
            });

            target.innerHTML = html;
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
            if (!(container && list)) return;

            if (serviciosSeleccionados.length === 0) {
                container.classList.add('hidden');
                list.innerHTML = '';
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

        function setText(id, text) { const el = document.getElementById(id); if (el) el.textContent = text; }
        function setValue(id, value) { const el = document.getElementById(id); if (el) el.value = value; }

        // ===================== Form AJAX =====================
        window.submitPaqueteAjax = function (form, event) {
            if (event) event.preventDefault();

            if (serviciosSeleccionados.length < 2) {
                showPaqueteMessage('Debe seleccionar al menos 2 servicios', 'error');
                document.getElementById('servicios-error')?.classList.remove('hidden');
                return false;
            }
            document.getElementById('servicios-error')?.classList.add('hidden');

            const serviciosIdsJson = document.getElementById('ServiciosIdsJson');
            if (serviciosIdsJson) serviciosIdsJson.value = JSON.stringify(serviciosSeleccionados.map(s => s.id));

            const formData = new FormData(form);
            fetch(form.action, {
                method: 'POST',
                body: formData,
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
                .then(resp => {
                    const isValid = resp.headers.get('X-Form-Valid') === 'true';
                    const message = resp.headers.get('X-Form-Message');
                    return resp.text().then(html => ({ isValid, message, html }));
                })
                .then(result => {
                    if (result.isValid) {
                        showPaqueteMessage(result.message || 'Operación exitosa', 'success');
                        limpiarFormularioPaquete();
                        reloadPaqueteTable(1);
                        setTimeout(() => {
                            document.getElementById('accordion-flush-body-1')?.classList.add('hidden');
                        }, 1800);
                    } else {
                        document.getElementById('paquete-form-container').innerHTML = result.html;
                        initializeForm();
                    }
                })
                .catch(() => showPaqueteMessage('Error al procesar la solicitud', 'error'));

            return false;
        };

        window.limpiarFormularioPaquete = function () {
            const form = document.getElementById('paquete-form');
            if (!form) return;
            if (form.dataset.edit === 'True') {
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

        // ===================== Modales =====================
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
            const el = document.getElementById(id);
            if (!el) return;
            const inst = getFlowbiteModal(el);
            if (inst?.show) inst.show();
            else {
                el.classList.remove('hidden');
                el.setAttribute('aria-hidden', 'false');
            }
        }

        function cerrarModal(id) {
            const el = document.getElementById(id);
            if (!el) return;
            let closed = false;
            try {
                const inst = getFlowbiteModal(el);
                if (inst?.hide) { inst.hide(); closed = true; }
            } catch { }
            if (!closed) {
                el.classList.add('hidden');
                el.setAttribute('aria-hidden', 'true');
                document.querySelectorAll('[modal-backdrop]')?.forEach(b => b.remove());
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
                    showTableMessage('success', msg);
                    reloadPaqueteTable(1);
                })
                .catch(() => showTableMessage('error', 'Error procesando la operación.'));
            return false;
        };

        // ===================== Mensajería =====================
        function showPaqueteMessage(message, type) {
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
                const formWrap = document.getElementById('paquete-form-container') || document.body;
                formWrap.prepend(container);
            } else {
                textEl = document.getElementById('form-message-text');
            }

            textEl.textContent = message;
            container.className = `m-4 p-4 mb-4 text-sm rounded-lg ${type === 'success'
                ? 'text-green-800 bg-green-50 dark:bg-gray-800 dark:text-green-400'
                : type === 'info'
                    ? 'text-blue-800 bg-blue-50 dark:bg-gray-800 dark:text-blue-400'
                    : 'text-red-800 bg-red-50 dark:bg-gray-800 dark:text-red-400'}`;

            container.classList.remove('hidden');

            if (paqueteMsgTimeout) clearTimeout(paqueteMsgTimeout);
            paqueteMsgTimeout = setTimeout(() => container.classList.add('hidden'), 5000);
        }

        function hidePaqueteMessage() {
            document.getElementById('form-message-container')?.classList.add('hidden');
        }

        function showTableMessage(typeOrMsg, msgOrType, disappearMs = 5000) {
            // Normaliza llamadas en ambos órdenes
            let type = typeOrMsg, msg = msgOrType;
            const allowed = ['success', 'info', 'error'];
            if (!allowed.includes(typeOrMsg) && allowed.includes(msgOrType)) {
                type = msgOrType;
                msg = typeOrMsg;
            }

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
                ${msg}
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

        // ===================== Inicial (modo edición) =====================
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

    })();