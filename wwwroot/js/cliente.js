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
    let vehiculoSeleccionadoDropdown = null;

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
        setupAccordionListener(); // NUEVO: Escuchar apertura del acordeón
        checkEditMode();
        window.CommonUtils?.setupDefaultFilterForm();
    }
    
    /**
     * Configura listener para cuando se abre el acordeón del formulario
     */
    function setupAccordionListener() {
        const accordionBtn = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
        const accordionBody = document.getElementById('accordion-flush-body-1');
        
        if (!accordionBtn || !accordionBody) {
            console.warn('⚠ Elementos del acordeón no encontrados');
            return;
        }
        
        // Usar MutationObserver para detectar cuando el acordeón se abre
        const observer = new MutationObserver(async (mutations) => {
            for (const mutation of mutations) {
                if (mutation.type === 'attributes' && mutation.attributeName === 'class') {
                    const isHidden = accordionBody.classList.contains('hidden');
                    
                    if (!isHidden) {
                        // El acordeón se acaba de abrir
                        console.log('🎯 Acordeón abierto - Inicializando vehículos...');
                        
                        // Esperar un poco para asegurar que el DOM esté completamente renderizado
                        await new Promise(resolve => setTimeout(resolve, 100));
                        
                        // Verificar si ya se inicializó (para evitar duplicados)
                        if (vehiculosDisponibles.length === 0) {
                            await setupVehiculoSelector();
                        } else {
                            console.log('✓ Selector de vehículos ya inicializado');
                        }
                    }
                }
            }
        });
        
        observer.observe(accordionBody, { attributes: true });
        console.log('✓ Observer del acordeón configurado');
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

    async function checkEditMode() {
        const formTitle = document.getElementById('form-title');
        if (formTitle && formTitle.textContent.includes('Editando')) {
            const accordion = document.getElementById('accordion-flush-body-1');
            if (accordion) {
                accordion.classList.remove('hidden');
                
                // CRÍTICO: Inicializar selector de vehículos cuando se abre en modo edición
                console.log('🔧 Inicializando selector de vehículos en modo edición...');
                await setupVehiculoSelector();
            }
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

    window.loadClienteForm = async function (id) {
        const url = id ? `/Cliente/FormPartial?id=${id}` : "/Cliente/FormPartial";

        try {
            const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            const html = await response.text();
            
            document.getElementById("cliente-form-container").innerHTML = html;

            const isEdit = !!document.getElementById('Id')?.value;
            const titleSpan = document.getElementById('form-title');
            if (titleSpan) {
                titleSpan.textContent = isEdit ? 'Editando Cliente' : 'Registrando Cliente';
            }

            // CRÍTICO: Esperar a que setupVehiculoSelector termine
            console.log('📋 Formulario cargado, iniciando setup de vehículos...');
            await setupVehiculoSelector();
            console.log('✅ Setup de vehículos completado');

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
        } catch (error) {
            console.error('❌ Error al cargar el formulario:', error);
        }
    };

    window.submitClienteAjax = function (form) {
        // Validar que al menos haya un vehículo
        if (vehiculosSeleccionados.length === 0) {
            showFormMessage('error', 'Debe seleccionar al menos un vehículo para el cliente.');
            document.getElementById('vehiculos-error')?.classList.remove('hidden');
            return false;
        }

        document.getElementById('vehiculos-error')?.classList.add('hidden');

        const formData = new FormData(form);

        // Agregar vehículos seleccionados (eliminar duplicados primero)
        formData.delete('VehiculosIds');
        vehiculosSeleccionados.forEach(v => {
            formData.append('VehiculosIds', v.id);
        });

        console.log('Enviando vehículos:', vehiculosSeleccionados.map(v => v.id));

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
            .then(async result => {
                document.getElementById('cliente-form-container').innerHTML = result.html;
                
                // CRÍTICO: Esperar setup de vehículos
                await setupVehiculoSelector();

                const isEdit = !!document.getElementById('Id')?.value;
                const titleSpan = document.getElementById('form-title');
                if (titleSpan) {
                    titleSpan.textContent = isEdit ? 'Editando Cliente' : 'Registrando Cliente';
                }

                if (result.valid) {
                    showFormMessage('success', result.msg || 'Cliente guardado correctamente. Los vehículos han sido asignados.', 4000);
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
                console.error('Error al enviar formulario:', e);
                showFormMessage('error', 'Error de comunicación con el servidor.', 8000);
            });

        return false;
    };

    // ===================== Selector de Vehículos (Estilo Paquetes) =====================

    async function setupVehiculoSelector() {
        console.log('🔧 setupVehiculoSelector iniciado');
        
        // IMPORTANTE: Esperar a que se carguen los vehículos ANTES de continuar
        await loadVehiculosDisponibles();
        
        console.log('🔧 Configurando event listeners...');

        const searchInput = document.getElementById('vehiculo-search');
        if (searchInput) {
            searchInput.addEventListener('input', function () {
                filterVehiculosDropdown(this.value);
            });

            searchInput.addEventListener('focus', function () {
                showVehiculoDropdown();
            });
            console.log('✓ Event listeners configurados');
        } else {
            console.error('❌ vehiculo-search input no encontrado');
        }

        setupDropdownClickOutside();
        console.log('✅ setupVehiculoSelector completado');
    }

    async function loadVehiculosDisponibles() {
        console.log('🔄 loadVehiculosDisponibles iniciado');
        
        try {
            // Obtener ID del cliente si estamos editando
            const clienteId = document.getElementById('Id')?.value;
            console.log('Cliente ID:', clienteId || '(nuevo cliente)');

            // MODO EDICIÓN: Cargar los vehículos del cliente actual + disponibles
            if (clienteId) {
                console.log('📝 Modo EDICIÓN');
                
                // Primero obtener vehículos del cliente
                const respCliente = await fetch(`/Cliente/GetVehiculosCliente?clienteId=${clienteId}`);
                const dataCliente = await respCliente.json();
                console.log('Respuesta GetVehiculosCliente:', dataCliente);
                
                // Luego obtener vehículos disponibles (sin dueño)
                const respDisponibles = await fetch('/Cliente/ObtenerVehiculosDisponibles');
                const dataDisponibles = await respDisponibles.json();
                console.log('Respuesta ObtenerVehiculosDisponibles:', dataDisponibles);

                // Combinar ambos (vehículos del cliente + disponibles)
                const vehiculosCliente = dataCliente.success ? (dataCliente.vehiculos || []) : [];
                const vehiculosLibres = dataDisponibles.success ? (dataDisponibles.vehiculos || []) : [];
                
                vehiculosDisponibles = [...vehiculosCliente, ...vehiculosLibres];
                console.log('✓ Total vehículos disponibles:', vehiculosDisponibles.length, 
                           '(Cliente:', vehiculosCliente.length, '+ Libres:', vehiculosLibres.length, ')');

                // Marcar como seleccionados los que ya son del cliente
                const hiddenIds = document.getElementById('VehiculosIdsData')?.value;
                if (hiddenIds) {
                    const ids = hiddenIds.split(',').map(x => x.trim()).filter(x => x);
                    vehiculosSeleccionados = vehiculosDisponibles.filter(v => ids.includes(v.id));
                    console.log('✓ Vehículos pre-seleccionados:', vehiculosSeleccionados.length);
                } else {
                    vehiculosSeleccionados = [];
                }
            } 
            // MODO CREACIÓN: Solo mostrar vehículos sin dueño
            else {
                console.log('✨ Modo CREACIÓN');
                
                const resp = await fetch('/Cliente/ObtenerVehiculosDisponibles');
                const data = await resp.json();
                console.log('Respuesta ObtenerVehiculosDisponibles:', data);

                if (data.success) {
                    vehiculosDisponibles = data.vehiculos || [];
                    vehiculosSeleccionados = [];
                    console.log('✓ Vehículos disponibles:', vehiculosDisponibles.length);
                    
                    // Log detallado de cada vehículo
                    if (vehiculosDisponibles.length > 0) {
                        console.table(vehiculosDisponibles.map(v => ({
                            Patente: v.patente,
                            Marca: v.marca,
                            Modelo: v.modelo,
                            Estado: v.estado
                        })));
                    } else {
                        console.warn('⚠ No hay vehículos sin dueño. Cree uno nuevo.');
                    }
                } else {
                    vehiculosDisponibles = [];
                    vehiculosSeleccionados = [];
                    console.error('❌ Error en respuesta del servidor:', data);
                }
            }

            updateVehiculosSeleccionadosList();
            console.log('✅ loadVehiculosDisponibles completado');
            
        } catch (error) {
            console.error('❌ Error al cargar vehículos:', error);
            vehiculosDisponibles = [];
            vehiculosSeleccionados = [];
        }
    }

    function renderVehiculosDropdown(vehiculos, filterText = '') {
        const target = document.getElementById('vehiculo-dropdown-content');
        if (!target) {
            console.error('❌ vehiculo-dropdown-content no encontrado');
            return;
        }

        console.log('renderVehiculosDropdown - Total vehículos:', vehiculos?.length, 'Filtro:', filterText);

        if (!Array.isArray(vehiculos) || vehiculos.length === 0) {
            target.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No hay vehículos disponibles. Use "Nuevo Vehículo" para crear uno.</p>';
            console.log('⚠ Array vacío o no válido');
            return;
        }

        let lista = vehiculos;
        
        // Aplicar filtro de texto
        if (filterText && filterText.trim()) {
            const lower = filterText.toLowerCase();
            lista = lista.filter(v =>
                (v.patente && v.patente.toLowerCase().includes(lower)) ||
                (v.marca && v.marca.toLowerCase().includes(lower)) ||
                (v.modelo && v.modelo.toLowerCase().includes(lower))
            );
            console.log('Después de filtrar por texto:', lista.length);
        }

        // Excluir ya seleccionados
        const listaOriginal = lista.length;
        lista = lista.filter(v => !vehiculosSeleccionados.some(sel => sel.id === v.id));
        console.log('Después de excluir seleccionados:', lista.length, '(de', listaOriginal, ')');

        if (lista.length === 0) {
            target.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No se encontraron vehículos con ese criterio</p>';
            console.log('⚠ Sin resultados tras filtros');
            return;
        }

        let html = '';
        lista.forEach(v => {
            const active = vehiculoSeleccionadoDropdown?.id === v.id ? 'bg-blue-100 dark:bg-blue-900' : '';
            html += `<div class="px-2 py-2 hover:bg-gray-100 dark:hover:bg-gray-600 cursor-pointer ${active}"
                         onclick="selectVehiculoFromDropdown('${v.id}')">
                         <div class="text-sm font-medium text-gray-900 dark:text-white">${escapeHtml(v.patente)}</div>
                         <div class="text-xs text-gray-500 dark:text-gray-400">${escapeHtml(v.marca)} ${escapeHtml(v.modelo)} - ${escapeHtml(v.color)} (${escapeHtml(v.tipoVehiculo)})</div>
                     </div>`;
        });

        target.innerHTML = html;
        console.log('✓ Dropdown renderizado con', lista.length, 'vehículos');
    }

    window.showVehiculoDropdown = function () {
        const dropdown = document.getElementById('vehiculo-dropdown');
        const searchInput = document.getElementById('vehiculo-search');
        
        if (!dropdown) {
            console.warn('Dropdown de vehículos no encontrado');
            return;
        }

        const inputValue = searchInput?.value?.trim() || '';
        
        console.log('showVehiculoDropdown - Vehículos disponibles:', vehiculosDisponibles.length, 'Filtro:', inputValue);

        // Mostrar SIEMPRE que haya vehículos disponibles
        if (vehiculosDisponibles.length > 0) {
            renderVehiculosDropdown(vehiculosDisponibles, inputValue);
            dropdown.classList.remove('hidden');
            console.log('✓ Dropdown mostrado');
        } else {
            // Si no hay vehículos, mostrar mensaje
            const target = document.getElementById('vehiculo-dropdown-content');
            if (target) {
                target.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No hay vehículos disponibles. Cree uno nuevo usando el botón "Nuevo Vehículo".</p>';
            }
            dropdown.classList.remove('hidden');
            console.log('⚠ No hay vehículos disponibles');
        }
    };

    window.filterVehiculosDropdown = function (txt) {
        const dropdown = document.getElementById('vehiculo-dropdown');
        if (!dropdown) return;

        const searchText = txt?.trim() || '';
        
        console.log('filterVehiculosDropdown - Texto:', searchText, 'Vehículos:', vehiculosDisponibles.length);

        // Si no hay texto, mostrar todos
        if (searchText === '') {
            if (vehiculosDisponibles.length > 0) {
                renderVehiculosDropdown(vehiculosDisponibles, '');
                dropdown.classList.remove('hidden');
            } else {
                const target = document.getElementById('vehiculo-dropdown-content');
                if (target) {
                    target.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No hay vehículos disponibles.</p>';
                }
                dropdown.classList.remove('hidden');
            }
            return;
        }

        // Filtrar por texto
        renderVehiculosDropdown(vehiculosDisponibles, searchText);
        dropdown.classList.remove('hidden');
    };

    window.selectVehiculoFromDropdown = function (id) {
        const v = vehiculosDisponibles.find(x => x.id === id);
        if (!v) return;
        vehiculoSeleccionadoDropdown = v;
        renderVehiculosDropdown(vehiculosDisponibles, document.getElementById('vehiculo-search')?.value || '');
        const searchInput = document.getElementById('vehiculo-search');
        if (searchInput) searchInput.value = v.patente;
    };

    /**
     * Agrega el vehículo seleccionado a la lista del cliente.
     */
    window.agregarVehiculoSeleccionado = function () {
        if (!vehiculoSeleccionadoDropdown) {
            showFormMessage('error', 'Debe seleccionar un vehículo del listado.');
            return;
        }

        // Verificar si ya está en la lista
        if (vehiculosSeleccionados.some(v => v.id === vehiculoSeleccionadoDropdown.id)) {
            showFormMessage('error', 'Este vehículo ya está en la lista.');
            return;
        }

        vehiculosSeleccionados.push({
            id: vehiculoSeleccionadoDropdown.id,
            patente: vehiculoSeleccionadoDropdown.patente,
            marca: vehiculoSeleccionadoDropdown.marca,
            modelo: vehiculoSeleccionadoDropdown.modelo,
            color: vehiculoSeleccionadoDropdown.color,
            tipoVehiculo: vehiculoSeleccionadoDropdown.tipoVehiculo
        });

        vehiculoSeleccionadoDropdown = null;
        const searchInput = document.getElementById('vehiculo-search');
        if (searchInput) searchInput.value = '';
        document.getElementById('vehiculo-dropdown')?.classList.add('hidden');
        updateVehiculosSeleccionadosList();
        renderVehiculosDropdown(vehiculosDisponibles);
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

        list.innerHTML = '<ul class="space-y-2" id="vehiculos-sortable-list">' +
            vehiculosSeleccionados.map((v, index) => {
                return `
                    <li draggable="true" 
                        data-vehiculo-id="${v.id}"
                        data-index="${index}"
                        class="vehiculo-item flex items-center gap-3 p-2 bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-600 hover:border-blue-300 dark:hover:border-blue-500 transition-colors cursor-move">
                        
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

                        <!-- Información del vehículo -->
                        <div class="flex-1 min-w-0">
                            <div class="font-medium text-gray-900 dark:text-white truncate">${escapeHtml(v.patente)}</div>
                            <div class="text-sm text-gray-500 dark:text-gray-400">${escapeHtml(v.marca)} ${escapeHtml(v.modelo)} - ${escapeHtml(v.color)} (${escapeHtml(v.tipoVehiculo)})</div>
                        </div>

                        <!-- Botón eliminar -->
                        <button type="button"
                                onclick="event.stopPropagation(); removerVehiculoSeleccionado('${v.id}')"
                                class="flex-shrink-0 w-8 h-8 flex items-center justify-center overflow-visible bg-transparent rounded-md border border-transparent hover:border-red-200 dark:hover:border-red-700 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300"
                                title="Quitar vehículo"
                                aria-label="Quitar vehículo"
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
            'Arrastra los vehículos para cambiar su orden. El primero será el vehículo principal.' +
            '</p>';

        // Configurar drag and drop
        setupVehiculoDragAndDrop();
    }

    function setupDropdownClickOutside() {
        document.addEventListener('click', (e) => {
            const dropdown = document.getElementById('vehiculo-dropdown');
            const searchInput = document.getElementById('vehiculo-search');
            const addBtn = document.getElementById('add-vehiculo-btn');
            const agregarBtn = document.getElementById('btn-agregar-vehiculo');

            if (dropdown && !dropdown.contains(e.target) &&
                e.target !== searchInput && e.target !== addBtn && e.target !== agregarBtn) {
                dropdown.classList.add('hidden');
            }
        });
    }

    /**
     * Configura la funcionalidad de drag and drop para reordenar vehículos.
     */
    function setupVehiculoDragAndDrop() {
        const list = document.getElementById('vehiculos-sortable-list');
        if (!list) return;

        const items = document.querySelectorAll('.vehiculo-item');
        let draggedElement = null;
        let placeholder = null;

        items.forEach(item => {
            // Dragstart
            item.addEventListener('dragstart', function (e) {
                draggedElement = this;
                this.style.opacity = '0.4';
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('text/html', this.innerHTML);

                setTimeout(() => {
                    placeholder = document.createElement('li');
                    placeholder.className = 'vehiculo-placeholder h-16 border-2 border-dashed border-blue-400 dark:border-blue-600 rounded bg-blue-50 dark:bg-blue-900/20';

                    if (draggedElement.nextSibling) {
                        list.insertBefore(placeholder, draggedElement.nextSibling);
                    } else {
                        list.appendChild(placeholder);
                    }

                    draggedElement.style.display = 'none';
                }, 0);
            });

            // Dragend
            item.addEventListener('dragend', function (e) {
                this.style.opacity = '1';
                this.style.display = 'flex';

                if (placeholder && placeholder.parentNode) {
                    placeholder.parentNode.removeChild(placeholder);
                }

                document.querySelectorAll('.vehiculo-item').forEach(i => {
                    i.classList.remove('bg-blue-50', 'dark:bg-blue-900/20');
                });

                draggedElement = null;
                placeholder = null;
            });

            // Dragover
            item.addEventListener('dragover', function (e) {
                e.preventDefault();
                e.dataTransfer.dropEffect = 'move';

                if (draggedElement === this || !draggedElement || !placeholder) return;

                const rect = this.getBoundingClientRect();
                const midpoint = rect.top + rect.height / 2;

                if (e.clientY < midpoint) {
                    list.insertBefore(placeholder, this);
                } else {
                    if (this.nextSibling) {
                        list.insertBefore(placeholder, this.nextSibling);
                    } else {
                        list.appendChild(placeholder);
                    }
                }
            });

            // Drop
            item.addEventListener('drop', function (e) {
                e.preventDefault();
                e.stopPropagation();

                if (!draggedElement || !placeholder) return;

                if (placeholder.parentNode) {
                    placeholder.parentNode.insertBefore(draggedElement, placeholder);
                    placeholder.parentNode.removeChild(placeholder);
                }

                draggedElement.style.display = 'flex';
                actualizarOrdenVehiculos();
            });
        });

        // Eventos en el contenedor
        list.addEventListener('dragover', function (e) {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';

            if (!draggedElement || !placeholder) return;

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

            if (placeholder.parentNode) {
                placeholder.parentNode.insertBefore(draggedElement, placeholder);
                placeholder.parentNode.removeChild(placeholder);
            }

            draggedElement.style.display = 'flex';
            actualizarOrdenVehiculos();
        });

        function getDragAfterElement(container, y) {
            const draggableElements = [...container.querySelectorAll('.vehiculo-item:not(.opacity-40)')];

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

        function actualizarOrdenVehiculos() {
            const allItems = Array.from(list.children).filter(el => el.classList.contains('vehiculo-item'));
            const newOrder = allItems.map(el => {
                const id = el.getAttribute('data-vehiculo-id');
                return vehiculosSeleccionados.find(v => v.id === id);
            }).filter(v => v !== undefined);

            vehiculosSeleccionados = newOrder;
            updateVehiculosSeleccionadosList();
        }
    }

    // ===================== Modal de Creación Rápida de Vehículo =====================

    window.openQuickCreateVehiculoModal = function () {
        fetch('/Vehiculo/FormPartial')
            .then(response => response.text())
            .then(html => {
                // Eliminar modal anterior si existe
                const existingModal = document.getElementById("quick-create-modal");
                if (existingModal) {
                    existingModal.remove();
                }
                const existingBackdrop = document.querySelector('[modal-backdrop]');
                if (existingBackdrop) {
                    existingBackdrop.remove();
                }

                // Crear estructura de modal Flowbite completa
                const modalHtml = `
                    <div id="quick-create-modal" tabindex="-1" aria-hidden="true" class="hidden overflow-y-auto overflow-x-hidden fixed top-0 right-0 left-0 z-50 justify-center items-center w-full md:inset-0 h-[calc(100%-1rem)] max-h-full">
                        <div class="relative p-4 w-full max-w-3xl max-h-full">
                            <div class="relative bg-white rounded-lg shadow dark:bg-gray-800">
                                <div class="flex items-center justify-between p-4 md:p-5 border-b rounded-t dark:border-gray-600">
                                    <h3 class="text-xl font-semibold text-gray-900 dark:text-white">
                                        Registrar Nuevo Vehículo
                                    </h3>
                                    <button type="button" onclick="closeQuickCreateModal()" class="text-gray-400 bg-transparent hover:bg-gray-200 hover:text-gray-900 rounded-lg text-sm w-8 h-8 ms-auto inline-flex justify-center items-center dark:hover:bg-gray-600 dark:hover:text-white">
                                        <svg class="w-3 h-3" aria-hidden="true" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 14 14">
                                            <path stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="m1 1 6 6m0 0 6 6M7 7l6-6M7 7l-6 6"/>
                                        </svg>
                                        <span class="sr-only">Cerrar modal</span>
                                    </button>
                                </div>
                                <div class="p-4 md:p-5 space-y-4 max-h-[calc(100vh-200px)] overflow-y-auto">
                                    <div id="quick-vehiculo-form-content">
                                        ${html}
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                `;

                // Insertar en el DOM
                document.body.insertAdjacentHTML('beforeend', modalHtml);

                // Obtener el modal y crear instancia Flowbite
                const modalEl = document.getElementById('quick-create-modal');
                
                // Configurar Flowbite Modal
                if (typeof Modal !== 'undefined') {
                    const modalOptions = {
                        placement: 'center',
                        backdrop: 'static', // NO se cierra al clickear fuera
                        closable: false, // NO se cierra con ESC
                        onHide: () => {
                            document.body.style.overflow = '';
                        },
                        onShow: () => {
                            document.body.style.overflow = 'hidden';
                        }
                    };

                    const modal = new Modal(modalEl, modalOptions);
                    modal.show();

                    // Guardar referencia para cerrar después
                    window._quickVehiculoModal = modal;
                } else {
                    // Fallback sin Flowbite
                    modalEl.classList.remove('hidden');
                    modalEl.classList.add('flex');
                    document.body.style.overflow = 'hidden';
                    
                    // Crear backdrop manual
                    const backdrop = document.createElement('div');
                    backdrop.setAttribute('modal-backdrop', '');
                    backdrop.className = 'bg-gray-900/50 dark:bg-gray-900/80 fixed inset-0 z-40';
                    document.body.appendChild(backdrop);
                }

                // Configurar el formulario
                const form = modalEl.querySelector("form");
                if (form) {
                    form.onsubmit = function (e) {
                        e.preventDefault();
                        submitQuickVehiculo(this);
                        return false;
                    };

                    const cancelBtn = form.querySelector("#clear-button");
                    if (cancelBtn) {
                        cancelBtn.onclick = function (e) {
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
        // Intentar cerrar con Flowbite
        if (window._quickVehiculoModal && typeof window._quickVehiculoModal.hide === 'function') {
            window._quickVehiculoModal.hide();
            
            // Limpiar después de cerrar
            setTimeout(() => {
                const modalEl = document.getElementById('quick-create-modal');
                if (modalEl) modalEl.remove();
                window._quickVehiculoModal = null;
            }, 300);
        } else {
            // Fallback
            const modalEl = document.getElementById("quick-create-modal");
            if (modalEl) {
                modalEl.classList.add('hidden');
                modalEl.classList.remove('flex');
                setTimeout(() => modalEl.remove(), 300);
            }
            
            const backdrop = document.querySelector('[modal-backdrop]');
            if (backdrop) backdrop.remove();
        }
        
        document.body.style.overflow = '';
    };

    function submitQuickVehiculo(form) {
        const formData = new FormData(form);

        // Capturar datos antes de enviar
        const patente = form.querySelector('#Patente')?.value;
        const marca = form.querySelector('#Marca')?.value;
        const modelo = form.querySelector('#Modelo')?.value;
        const color = form.querySelector('#Color')?.value;
        const tipoVehiculo = form.querySelector('#TipoVehiculo')?.value;

        console.log('Enviando vehículo:', { patente, marca, modelo, color, tipoVehiculo });

        fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(response => {
                const isValid = response.headers.get("X-Form-Valid") === "true";
                const message = response.headers.get("X-Form-Message");
                console.log('Respuesta servidor - isValid:', isValid, 'message:', message);
                return response.text().then(html => ({ isValid, html, message, status: response.status }));
            })
            .then(async result => {
                if (result.isValid && result.status === 200) {
                    console.log('✓ Vehículo creado exitosamente');
                    
                    // IMPORTANTE: Cerrar el modal PRIMERO
                    closeQuickCreateModal();

                    // Esperar un poco para que se complete la creación en el servidor
                    await new Promise(resolve => setTimeout(resolve, 800));

                    console.log('Recargando lista de vehículos disponibles...');

                    // Recargar vehículos disponibles
                    const respDisponibles = await fetch('/Cliente/ObtenerVehiculosDisponibles');
                    const dataDisponibles = await respDisponibles.json();

                    console.log('Respuesta ObtenerVehiculosDisponibles:', dataDisponibles);

                    if (dataDisponibles.success && dataDisponibles.vehiculos) {
                        vehiculosDisponibles = dataDisponibles.vehiculos;
                        console.log('Vehículos disponibles actualizados:', vehiculosDisponibles.length);
                        
                        // Buscar el vehículo recién creado por patente
                        const nuevoVehiculo = vehiculosDisponibles.find(v => 
                            v.patente && v.patente.toLowerCase() === patente.toLowerCase()
                        );

                        console.log('Vehículo encontrado:', nuevoVehiculo);

                        if (nuevoVehiculo) {
                            // Agregarlo automáticamente a la lista
                            if (!vehiculosSeleccionados.some(v => v.id === nuevoVehiculo.id)) {
                                vehiculosSeleccionados.push(nuevoVehiculo);
                                updateVehiculosSeleccionadosList();
                                console.log('✓ Vehículo agregado a la lista de seleccionados');
                            }
                            showFormMessage('success', `Vehículo ${patente} registrado y agregado correctamente.`, 4000);
                        } else {
                            console.warn('⚠ No se encontró el vehículo recién creado en la lista');
                            showFormMessage('info', 'Vehículo registrado. Búsquelo en la lista para agregarlo.', 5000);
                        }
                    } else {
                        console.error('Error al obtener vehículos:', dataDisponibles);
                        showFormMessage('warning', 'Vehículo registrado. Recargue la página para verlo.', 5000);
                    }
                } else {
                    console.log('✗ Formulario con errores de validación');
                    // Mostrar errores en el modal (NO cerrar)
                    const content = document.getElementById("quick-vehiculo-form-content");
                    if (content) {
                        content.innerHTML = result.html;

                        const newForm = content.querySelector("form");
                        if (newForm) {
                            newForm.onsubmit = function (e) {
                                e.preventDefault();
                                submitQuickVehiculo(this);
                                return false;
                            };

                            const cancelBtn = newForm.querySelector("#clear-button");
                            if (cancelBtn) {
                                cancelBtn.onclick = function (e) {
                                    e.preventDefault();
                                    closeQuickCreateModal();
                                    return false;
                                };
                            }
                        }
                    }
                }
            })
            .catch(error => {
                console.error('Error de red:', error);
                showFormMessage('error', 'Error al registrar el vehículo.', 5000);
            });
    }

    // ===================== Modales =====================

    function setupModals() {
        setupTipoDocumentoModal();
        setupDeleteModal();
    }

    function setupTipoDocumentoModal() {
        // Setup crear
        const formCrear = document.getElementById("formCrearTipoDocumento");
        if (formCrear && formCrear.dataset.setup !== 'true') {
            formCrear.addEventListener("submit", async function (e) {
                e.preventDefault();
                e.stopPropagation();
                await handleCrearTipoDocumento(this);
            });
            formCrear.dataset.setup = 'true';
        }
    }

    /**
     * Confirma y elimina el tipo de documento seleccionado (con modal)
     */
    window.confirmarEliminarTipoDocumento = async function () {
        const selectTipoDoc = document.getElementById('TipoDocumento');
        if (!selectTipoDoc) return;

        const tipoSeleccionado = selectTipoDoc.value?.trim();
        if (!tipoSeleccionado) {
            showFormMessage('error', 'Debe seleccionar un tipo de documento del listado.');
            cerrarModal('eliminarTipoDocumentoModal');
            return;
        }

        try {
            const formData = new FormData();
            formData.append('nombreTipo', tipoSeleccionado);

            // Obtener token antiforgery
            const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
            if (tokenInput) {
                formData.append('__RequestVerificationToken', tokenInput.value);
            }

            const response = await fetch('/TipoDocumento/EliminarTipoDocumento', {
                method: 'POST',
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' },
                body: formData
            });

            const { ok, data } = await parseJsonSafe(response);
            const success = data?.success ?? ok;
            const message = data?.message ?? (success ? 'Tipo de documento eliminado correctamente.' : 'No se pudo eliminar el tipo de documento.');

            cerrarModal('eliminarTipoDocumentoModal');

            if (success) {
                if (data?.tipos) {
                    actualizarDropdownTipos('TipoDocumento', data.tipos, null);
                }
                showFormMessage('success', message);
            } else {
                showFormMessage('error', message);
            }
        } catch (error) {
            console.error('Error:', error);
            cerrarModal('eliminarTipoDocumentoModal');
            showFormMessage('error', 'Error al eliminar el tipo de documento.');
        }
    };

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

        // Mantener la opción "Seleccione..."
        select.innerHTML = '<option value="">Seleccione...</option>';

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