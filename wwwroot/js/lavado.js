/**
 * ================================================
 * LAVADO.JS - FUNCIONALIDAD DE LA PÁGINA DE LAVADOS
 * ================================================
 */

(function () {
    'use strict';

    let searchTimeout = null;
    let currentSearchTerm = '';
    let tableMsgTimeout = null;

    // =====================================
    // INICIALIZACIÓN DEL MÓDULO
    // =====================================
    window.PageModules = window.PageModules || {};
    window.PageModules.lavados = {
        init: initializeLavadosPage
    };

    /**
     * Inicializa la funcionalidad específica de la página de Lavados
     */
    function initializeLavadosPage() {
        setupSearchWithDebounce();
        setupFormSubmit();
        setupClienteSearch();
        setupFilterFormSubmit();
        initFilterTooltips(); // ✅ NUEVO: Inicializar validación de filtros
        window.CommonUtils?.setupDefaultFilterForm();
    }

    document.addEventListener('DOMContentLoaded', () => {
        try {
            window.PageModules?.lavados?.init();
        } catch (e) {
            initializeLavadosPage();
        }
    });

    // =====================================
    // BÚSQUEDA CON DEBOUNCE
    // =====================================
    function setupSearchWithDebounce() {
        const searchInput = document.getElementById('simple-search');
        if (!searchInput) return;

        const newSearchInput = searchInput.cloneNode(true);
        searchInput.parentNode.replaceChild(newSearchInput, searchInput);

        currentSearchTerm = newSearchInput.value?.trim() || '';

        newSearchInput.addEventListener('input', function () {
            const searchTerm = this.value.trim();

            if (searchTimeout) clearTimeout(searchTimeout);

            if (searchTerm === '') {
                currentSearchTerm = '';
                reloadLavadoTable(1);
                return;
            }

            searchTimeout = setTimeout(() => {
                performServerSearch(searchTerm);
            }, 500);
        });
    }

    function performServerSearch(searchTerm = '') {
        currentSearchTerm = searchTerm;

        const filterForm = document.getElementById('filterForm');
        const params = new URLSearchParams();

        if (filterForm) {
            const formData = new FormData(filterForm);
            for (const [key, value] of formData.entries()) {
                params.append(key, value);
            }
        }

        const currentSort = getCurrentSort();
        params.set('searchTerm', searchTerm);
        params.set('pageNumber', '1');
        params.set('sortBy', currentSort.sortBy);
        params.set('sortOrder', currentSort.sortOrder);

        const url = `/Lavados/SearchPartial?${params.toString()}`;

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const cont = document.getElementById('lavado-table-container');
                cont.innerHTML = html;
                const cp = document.getElementById('current-page-value')?.value;
                if (cp) cont.dataset.currentPage = cp;
            })
            .catch(e => {
                console.error('Error en búsqueda:', e);
                showTableMessage('error', 'Error al realizar la búsqueda.');
            });
    }

    // =====================================
    // ORDENAMIENTO Y PAGINACIÓN
    // =====================================
    function getCurrentSort() {
        const sortByInput = document.getElementById('current-sort-by');
        const sortOrderInput = document.getElementById('current-sort-order');

        return {
            sortBy: sortByInput?.value || 'FechaCreacion',
            sortOrder: sortOrderInput?.value || 'desc'
        };
    }

    window.sortTable = function (sortBy) {
        const currentSortBy = document.getElementById('current-sort-by')?.value || 'FechaCreacion';
        const currentSortOrder = document.getElementById('current-sort-order')?.value || 'desc';

        let newSortOrder = 'asc';
        if (currentSortBy === sortBy) {
            newSortOrder = currentSortOrder === 'asc' ? 'desc' : 'asc';
        }

        const sortByInput = document.getElementById('current-sort-by');
        const sortOrderInput = document.getElementById('current-sort-order');

        if (sortByInput) sortByInput.value = sortBy;
        if (sortOrderInput) sortOrderInput.value = newSortOrder;

        reloadLavadoTable(1);
    };

    window.reloadLavadoTable = function (page) {
        const filterForm = document.getElementById('filterForm');
        const params = new URLSearchParams();

        if (filterForm) {
            const formData = new FormData(filterForm);
            for (const [key, value] of formData.entries()) {
                params.append(key, value);
            }
        }

        const currentSort = getCurrentSort();
        params.set('pageNumber', page.toString());
        params.set('sortBy', currentSort.sortBy);
        params.set('sortOrder', currentSort.sortOrder);

        let url;
        if (currentSearchTerm && currentSearchTerm.trim().length > 0) {
            params.set('searchTerm', currentSearchTerm.trim());
            url = `/Lavados/SearchPartial?${params.toString()}`;
        } else {
            url = `/Lavados/TablePartial?${params.toString()}`;
        }

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const cont = document.getElementById('lavado-table-container');
                cont.innerHTML = html;
                const cp = document.getElementById('current-page-value')?.value;
                if (cp) cont.dataset.currentPage = cp;
                window.CommonUtils?.setupDefaultFilterForm?.();
                if (typeof initModals === 'function') { initModals(); }
                if (typeof initDropdowns === 'function') { initDropdowns(); }
            })
            .catch(e => {
                showTableMessage('error', 'Error cargando la tabla.');
            });
    };

    // =====================================
    // BÚSQUEDA DE VEHÍCULOS POR PATENTE
    // =====================================
    function setupClienteSearch() {
        setupPatenteSearch();
    }

    function setupPatenteSearch() {
        const searchInput = document.getElementById('patenteSearch');
        const resultadosDiv = document.getElementById('patenteResultados');

        if (!searchInput || !resultadosDiv) return;

        let debounceTimer;

        searchInput.addEventListener('input', function () {
            const term = this.value.trim();

            clearTimeout(debounceTimer);

            if (term.length < 2) {
                resultadosDiv.classList.add('hidden');
                return;
            }

            debounceTimer = setTimeout(() => {
                buscarVehiculos(term);
            }, 300);
        });

        searchInput.addEventListener('focus', function () {
            if (this.value.trim().length >= 2) {
                resultadosDiv.classList.remove('hidden');
            }
        });

        document.addEventListener('click', function (e) {
            if (!searchInput.contains(e.target) && !resultadosDiv.contains(e.target)) {
                resultadosDiv.classList.add('hidden');
            }
        });
    }

    async function buscarVehiculos(term) {
        const resultadosDiv = document.getElementById('patenteResultados');

        try {
            const response = await fetch(`/Lavados/BuscarVehiculosPorPatente?search=${encodeURIComponent(term)}`);
            const vehiculos = await response.json();

            if (vehiculos.length === 0) {
                resultadosDiv.innerHTML = '<div class="p-3 text-gray-500 dark:text-gray-400">No se encontraron vehículos</div>';
            } else {
                resultadosDiv.innerHTML = vehiculos.map(v => `
                    <div class="p-3 hover:bg-gray-100 dark:hover:bg-gray-600 cursor-pointer border-b dark:border-gray-600 last:border-b-0"
                         onclick="seleccionarVehiculo('${v.id}', '${escapeHtml(v.patente)}', '${escapeHtml(v.tipoVehiculo)}', '${escapeHtml(v.marca)}', '${escapeHtml(v.modelo)}', '${escapeHtml(v.color)}', '${escapeHtml(v.clienteNombre || '')}')">
                        <div class="font-medium text-gray-900 dark:text-white">${escapeHtml(v.patente)}</div>
                        <div class="text-sm text-gray-500 dark:text-gray-400">${escapeHtml(v.marca)} ${escapeHtml(v.modelo)} - ${escapeHtml(v.color)}</div>
                        <div class="text-xs text-gray-400 dark:text-gray-500">${escapeHtml(v.tipoVehiculo)} ${v.clienteNombre ? '• ' + escapeHtml(v.clienteNombre) : ''}</div>
                    </div>
                `).join('');
            }

            resultadosDiv.classList.remove('hidden');
        } catch (e) {
            console.error('Error buscando vehículos:', e);
            resultadosDiv.innerHTML = '<div class="p-3 text-red-500">Error al buscar vehículos</div>';
            resultadosDiv.classList.remove('hidden');
        }
    }

    window.seleccionarVehiculo = async function (id, patente, tipoVehiculo, marca, modelo, color, clienteNombre) {
        // Si ya había un vehículo seleccionado, limpiar datos anteriores
        if (window.lavadoData.vehiculoId && window.lavadoData.vehiculoId !== id) {
            window.lavadoData.vehiculosSeleccionados.forEach(vehiculoId => {
                eliminarServiciosVehiculo(vehiculoId);
            });
            window.lavadoData.vehiculosSeleccionados = [];
            window.lavadoData.serviciosPorVehiculo = {};
            window.lavadoData.serviciosDisponibles = {};
            window.lavadoData.paquetesDisponibles = {};
        }

        document.getElementById('vehiculoId').value = id;
        document.getElementById('patenteSearch').value = patente;
        document.getElementById('patenteResultados').classList.add('hidden');
        document.getElementById('vehiculoSeleccionado').classList.remove('hidden');
        document.getElementById('vehiculoNombre').textContent = `${patente} - ${marca} ${modelo}`;

        window.lavadoData.vehiculoId = id;
        window.lavadoData.vehiculoPatente = patente;
        window.lavadoData.tipoVehiculo = tipoVehiculo;

        // Store the vehicle in the vehiculos array for later reference
        window.lavadoData.vehiculos = [{
            id: id,
            patente: patente,
            tipoVehiculo: tipoVehiculo,
            marca: marca,
            modelo: modelo,
            color: color
        }];

        // Mostrar información del vehículo
        const infoSection = document.getElementById('infoVehiculoSection');
        const infoDiv = document.getElementById('infoVehiculo');
        if (infoSection && infoDiv) {
            infoDiv.innerHTML = `
                <p class="text-sm text-gray-600 dark:text-gray-400"><strong>Patente:</strong> ${escapeHtml(patente)}</p>
                <p class="text-sm text-gray-600 dark:text-gray-400"><strong>Vehículo:</strong> ${escapeHtml(marca)} ${escapeHtml(modelo)} (${escapeHtml(color)})</p>
                <p class="text-sm text-gray-600 dark:text-gray-400"><strong>Tipo:</strong> ${escapeHtml(tipoVehiculo)}</p>
            `;
            infoSection.style.display = 'block';
        }

        // Cargar clientes asociados al vehículo
        await cargarClientesVehiculo(id);

        // Cargar empleados según el tipo de vehículo
        await cargarEmpleadosPorTipoVehiculo(tipoVehiculo);

        // Agregar el vehículo a la lista de seleccionados y cargar sus servicios
        window.lavadoData.vehiculosSeleccionados = [id];
        await cargarServiciosParaVehiculo(id, tipoVehiculo);
    };

    async function cargarClientesVehiculo(vehiculoId) {
        try {
            const response = await fetch(`/Lavados/ObtenerClientesVehiculo?vehiculoId=${vehiculoId}`);
            const clientes = await response.json();

            window.lavadoData.clientesAsociados = clientes;

            const clientesSection = document.getElementById('clientesAsociadosSection');
            const clienteTrajoSelect = document.getElementById('clienteTrajoSelect');
            const clienteRetiraSelect = document.getElementById('clienteRetiraSelect');
            const clienteIdInput = document.getElementById('clienteId');

            if (clientes.length > 0) {
                // Establecer el primer cliente como el principal
                clienteIdInput.value = clientes[0].id;
                window.lavadoData.clienteId = clientes[0].id;
                window.lavadoData.clienteNombre = clientes[0].nombre;

                // Si hay más de un cliente, mostrar los desplegables
                if (clientes.length > 1) {
                    const options = clientes.map(c =>
                        `<option value="${c.id}">${escapeHtml(c.nombre)} (${escapeHtml(c.documento)})</option>`
                    ).join('');

                    clienteTrajoSelect.innerHTML = options;
                    clienteRetiraSelect.innerHTML = options;
                    clientesSection.style.display = 'block';
                } else {
                    // Un solo cliente, no se necesitan los desplegables
                    clientesSection.style.display = 'none';
                }
            } else {
                clientesSection.style.display = 'none';
                showTableMessage('error', 'El vehículo no tiene clientes asociados activos.');
            }
        } catch (e) {
            console.error('Error cargando clientes del vehículo:', e);
            showTableMessage('error', 'Error al cargar los clientes del vehículo.');
        }
    }

    async function cargarEmpleadosPorTipoVehiculo(tipoVehiculo) {
        try {
            // Obtener la cantidad de empleados requeridos para este tipo de vehículo
            const response = await fetch(`/Lavados/ObtenerEmpleadosPorTipoVehiculo?tipoVehiculo=${encodeURIComponent(tipoVehiculo)}`);
            const data = await response.json();

            window.lavadoData.cantidadEmpleadosPorTipo = data.cantidadEmpleadosRequeridos || 1;

            // Obtener información general de empleados disponibles
            const empleadosResponse = await fetch('/Lavados/ObtenerEmpleadosDisponibles');
            const empleadosData = await empleadosResponse.json();

            window.lavadoData.empleadosInfo = {
                totalActivos: empleadosData.totalActivos,
                totalDisponibles: empleadosData.totalDisponibles,
                empleadosMaximosPorLavado: empleadosData.empleadosMaximosPorLavado
            };

            // Mostrar información al usuario
            const infoDiv = document.getElementById('empleadosInfo');
            if (infoDiv) {
                infoDiv.innerHTML = `
                    <div class="text-xs text-gray-600 dark:text-gray-400 mt-1">
                        <p>Empleados requeridos para ${escapeHtml(tipoVehiculo)}: <span class="font-medium text-blue-600 dark:text-blue-400">${window.lavadoData.cantidadEmpleadosPorTipo}</span></p>
                        <p>Empleados disponibles: <span class="font-medium">${empleadosData.totalDisponibles}</span> de ${empleadosData.totalActivos} activos</p>
                    </div>
                `;
            }
        } catch (e) {
            console.error('Error cargando info de empleados:', e);
        }
    }

    // Mantener la función original por si se necesita
    window.seleccionarCliente = function (id, nombre) {
        // Si ya había un cliente seleccionado, limpiar datos anteriores
        if (window.lavadoData.clienteId && window.lavadoData.clienteId !== id) {
            // Limpiar vehículos seleccionados
            window.lavadoData.vehiculosSeleccionados.forEach(vehiculoId => {
                eliminarServiciosVehiculo(vehiculoId);
            });
            window.lavadoData.vehiculosSeleccionados = [];
            window.lavadoData.serviciosPorVehiculo = {};
            window.lavadoData.serviciosDisponibles = {};
            window.lavadoData.paquetesDisponibles = {};
        }

        window.lavadoData.clienteId = id;
        window.lavadoData.clienteNombre = nombre;
    };

    async function cargarInfoEmpleados() {
        try {
            const response = await fetch('/Lavados/ObtenerEmpleadosDisponibles');
            const data = await response.json();

            window.lavadoData.empleadosInfo = {
                totalActivos: data.totalActivos,
                totalDisponibles: data.totalDisponibles,
                empleadosMaximosPorLavado: data.empleadosMaximosPorLavado
            };

            // Actualizar el input de cantidad de empleados
            const cantidadEmpleadosInput = document.getElementById('cantidadEmpleados');
            if (cantidadEmpleadosInput) {
                const maxEmpleados = Math.min(
                    data.totalDisponibles,
                    data.empleadosMaximosPorLavado
                );

                cantidadEmpleadosInput.max = maxEmpleados;
                cantidadEmpleadosInput.min = 1;

                if (parseInt(cantidadEmpleadosInput.value) > maxEmpleados) {
                    cantidadEmpleadosInput.value = maxEmpleados;
                }

                // Agregar validación en tiempo real
                cantidadEmpleadosInput.addEventListener('input', validarCantidadEmpleados);
                cantidadEmpleadosInput.addEventListener('change', validarCantidadEmpleados);
            }

            // Mostrar información al usuario
            const infoDiv = document.getElementById('empleadosInfo');
            if (infoDiv) {
                infoDiv.innerHTML = `
                    <div class="text-xs text-gray-600 dark:text-gray-400 mt-1">
                        <p>Empleados disponibles: <span class="font-medium text-blue-600 dark:text-blue-400">${data.totalDisponibles}</span> de ${data.totalActivos} activos</p>
                        <p>Máximo por lavado: <span class="font-medium">${data.empleadosMaximosPorLavado}</span></p>
                    </div>
                `;
            }
        } catch (e) {
            console.error('Error cargando info de empleados:', e);
        }
    }

    function validarCantidadEmpleados() {
        const input = document.getElementById('cantidadEmpleados');
        if (!input || !window.lavadoData.empleadosInfo) return;

        const valor = parseInt(input.value);
        const maxEmpleados = Math.min(
            window.lavadoData.empleadosInfo.totalDisponibles,
            window.lavadoData.empleadosInfo.empleadosMaximosPorLavado
        );

        if (valor > maxEmpleados) {
            input.value = maxEmpleados;
            showTableMessage('error', `Solo hay ${window.lavadoData.empleadosInfo.totalDisponibles} empleados disponibles. Máximo permitido por lavado: ${window.lavadoData.empleadosInfo.empleadosMaximosPorLavado}`);
        }

        if (valor < 1) {
            input.value = 1;
        }
    }

    async function cargarVehiculosCliente(clienteId) {
        const vehiculosSection = document.getElementById('vehiculosSection');
        const vehiculosList = document.getElementById('vehiculosList');

        try {
            const response = await fetch(`/Lavados/ObtenerVehiculosCliente?clienteId=${clienteId}`);
            const vehiculos = await response.json();

            window.lavadoData.vehiculos = vehiculos;

            if (vehiculos.length === 0) {
                vehiculosList.innerHTML = '<div class="col-span-full text-center text-gray-500 dark:text-gray-400 py-4">El cliente no tiene vehículos registrados</div>';
            } else {
                vehiculosList.innerHTML = vehiculos.map(v => `
                    <div class="vehiculo-card p-4 bg-gray-50 dark:bg-gray-700 rounded-lg border-2 border-transparent hover:border-blue-500 cursor-pointer transition-all"
                         data-vehiculo-id="${v.id}" data-tipo-vehiculo="${v.tipoVehiculo}"
                         onclick="toggleVehiculo('${v.id}', '${v.tipoVehiculo}', event)">
                        <div class="flex items-center gap-3">
                            <input type="checkbox" class="vehiculo-checkbox w-5 h-5 text-blue-600 rounded focus:ring-blue-500" 
                                   id="vehiculo-${v.id}" data-vehiculo-id="${v.id}"
                                   onchange="handleCheckboxChange('${v.id}', '${v.tipoVehiculo}')" onclick="event.stopPropagation()">
                            <div>
                                <div class="font-medium text-gray-900 dark:text-white">${escapeHtml(v.patente)}</div>
                                <div class="text-sm text-gray-500 dark:text-gray-400">${escapeHtml(v.marca)} ${escapeHtml(v.modelo)}</div>
                                <div class="text-xs text-gray-400 dark:text-gray-500">${escapeHtml(v.tipoVehiculo)} • ${escapeHtml(v.color)}</div>
                            </div>
                        </div>
                    </div>
                `).join('');
            }

            vehiculosSection.style.display = 'block';
        } catch (e) {
            console.error('Error cargando vehículos:', e);
            showTableMessage('error', 'Error al cargar los vehículos del cliente.');
        }
    }

    // Función separada para manejar el cambio del checkbox
    window.handleCheckboxChange = async function (vehiculoId, tipoVehiculo) {
        const checkbox = document.getElementById(`vehiculo-${vehiculoId}`);
        const card = checkbox.closest('.vehiculo-card');

        if (checkbox.checked) {
            card.classList.add('border-blue-500', 'bg-blue-50', 'dark:bg-blue-900/30');
            window.lavadoData.vehiculosSeleccionados.push(vehiculoId);
            await cargarServiciosParaVehiculo(vehiculoId, tipoVehiculo);
        } else {
            card.classList.remove('border-blue-500', 'bg-blue-50', 'dark:bg-blue-900/30');
            window.lavadoData.vehiculosSeleccionados = window.lavadoData.vehiculosSeleccionados.filter(v => v !== vehiculoId);
            eliminarServiciosVehiculo(vehiculoId);
        }

        actualizarResumen();
        actualizarBotonSubmit();
    };

    window.toggleVehiculo = async function (vehiculoId, tipoVehiculo, event) {
        // Si el click fue en el checkbox, no hacer nada (ya se maneja en handleCheckboxChange)
        if (event && event.target.type === 'checkbox') {
            return;
        }

        const checkbox = document.getElementById(`vehiculo-${vehiculoId}`);

        // Si el click no fue en el checkbox, hacer toggle manualmente
        checkbox.checked = !checkbox.checked;

        // Disparar el cambio manualmente
        await handleCheckboxChange(vehiculoId, tipoVehiculo);
    };

    async function cargarServiciosParaVehiculo(vehiculoId, tipoVehiculo) {
        const serviciosSection = document.getElementById('serviciosSection');
        const serviciosPorVehiculo = document.getElementById('serviciosPorVehiculo');

        try {
            const [serviciosResp, paquetesResp] = await Promise.all([
                fetch(`/Lavados/ObtenerServiciosPorTipoVehiculo?tipoVehiculo=${encodeURIComponent(tipoVehiculo)}`),
                fetch(`/Lavados/ObtenerPaquetesPorTipoVehiculo?tipoVehiculo=${encodeURIComponent(tipoVehiculo)}`)
            ]);

            const servicios = await serviciosResp.json();
            const paquetes = await paquetesResp.json();

            window.lavadoData.serviciosDisponibles[vehiculoId] = servicios;
            window.lavadoData.paquetesDisponibles[vehiculoId] = paquetes;
            window.lavadoData.serviciosPorVehiculo[vehiculoId] = [];

            const vehiculo = window.lavadoData.vehiculos.find(v => v.id === vehiculoId);
            const vehiculoNombre = vehiculo ? `${vehiculo.patente} - ${vehiculo.marca} ${vehiculo.modelo}` : vehiculoId;

            const vehiculoDiv = document.createElement('div');
            vehiculoDiv.id = `servicios-vehiculo-${vehiculoId}`;
            vehiculoDiv.className = 'mb-6 p-4 bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-600';
            vehiculoDiv.innerHTML = `
                <h4 class="text-md font-medium text-gray-900 dark:text-white mb-4">${escapeHtml(vehiculoNombre)}</h4>
                
                ${paquetes.length > 0 ? `
                <div class="mb-4">
                    <label class="block mb-2 text-sm font-medium text-gray-900 dark:text-white">Paquete de Servicios (solo uno)</label>
                    <select id="paquete-select-${vehiculoId}" onchange="agregarPaquete('${vehiculoId}', this.value)" 
                            class="bg-gray-50 border border-gray-300 text-gray-900 text-sm rounded-lg focus:ring-primary-500 focus:border-primary-500 block w-full p-2.5 dark:bg-gray-700 dark:border-gray-600 dark:text-white">
                        <option value="">Seleccionar paquete...</option>
                        ${paquetes.map(p => {
                const precioOriginal = p.precioOriginal || 0;
                const precioConDescuento = precioOriginal - (precioOriginal * p.descuento / 100);
                return `<option value="${p.id}" data-precio="${precioOriginal}">${escapeHtml(p.nombre)} - ${formatCurrency(precioOriginal)} (${p.descuento}% desc.) = ${formatCurrency(precioConDescuento)}</option>`;
            }).join('')}
                    </select>
                </div>
                ` : ''}
                
                <div class="mb-4">
                    <label class="block mb-2 text-sm font-medium text-gray-900 dark:text-white">Servicios Individuales</label>
                    <select id="servicios-select-${vehiculoId}" onchange="agregarServicio('${vehiculoId}', this.value)"
                            class="bg-gray-50 border border-gray-300 text-gray-900 text-sm rounded-lg focus:ring-primary-500 focus:border-primary-500 block w-full p-2.5 dark:bg-gray-700 dark:border-gray-600 dark:text-white">
                        <option value="">Agregar servicio...</option>
                        ${servicios.map(s => `<option value="${s.id}" data-tipo="${s.tipo}">${escapeHtml(s.nombre)} - ${formatCurrency(s.precio)} (${s.tiempoEstimado} min)</option>`).join('')}
                    </select>
                </div>
                
                <div id="servicios-lista-${vehiculoId}" class="space-y-2">
                    <p class="text-gray-500 dark:text-gray-400 text-sm">No hay servicios seleccionados</p>
                </div>
            `;

            serviciosPorVehiculo.appendChild(vehiculoDiv);
            serviciosSection.style.display = 'block';
            document.getElementById('resumenSection').style.display = 'block';
        } catch (e) {
            console.error('Error cargando servicios:', e);
            showTableMessage('error', 'Error al cargar los servicios disponibles.');
        }
    }

    function eliminarServiciosVehiculo(vehiculoId) {
        const vehiculoDiv = document.getElementById(`servicios-vehiculo-${vehiculoId}`);
        if (vehiculoDiv) {
            vehiculoDiv.remove();
        }

        delete window.lavadoData.serviciosPorVehiculo[vehiculoId];
        delete window.lavadoData.serviciosDisponibles[vehiculoId];
        delete window.lavadoData.paquetesDisponibles[vehiculoId];

        if (window.lavadoData.vehiculosSeleccionados.length === 0) {
            document.getElementById('serviciosSection').style.display = 'none';
            document.getElementById('resumenSection').style.display = 'none';
        }
    }

    window.agregarPaquete = function (vehiculoId, paqueteId) {
        if (!paqueteId) return;

        const paquete = window.lavadoData.paquetesDisponibles[vehiculoId]?.find(p => p.id === paqueteId);
        if (!paquete) return;

        // Verificar si ya hay un paquete seleccionado
        const paqueteExistente = window.lavadoData.serviciosPorVehiculo[vehiculoId]?.find(s => s.paqueteId);
        if (paqueteExistente) {
            showTableMessage('error', 'Solo se puede seleccionar un paquete por lavado. Elimine el paquete actual primero.');
            event.target.value = '';
            return;
        }

        // Usar el precio original del paquete (suma de servicios) y aplicar descuento
        const precioOriginalPaquete = paquete.precioOriginal || 0;
        const precioPaqueteConDescuento = precioOriginalPaquete - (precioOriginalPaquete * paquete.descuento / 100);

        // Agregar todos los servicios del paquete
        paquete.servicios.forEach(s => {
            // Verificar si ya existe un servicio del mismo tipo
            const servicioExistente = window.lavadoData.serviciosPorVehiculo[vehiculoId]?.find(srv => srv.tipo === s.tipo);
            if (!servicioExistente) {
                const servicioCompleto = window.lavadoData.serviciosDisponibles[vehiculoId]?.find(serv => serv.id === s.id);
                if (servicioCompleto) {
                    window.lavadoData.serviciosPorVehiculo[vehiculoId].push({
                        ...servicioCompleto,
                        paqueteId: paquete.id,
                        paqueteNombre: paquete.nombre,
                        precioPaquete: precioPaqueteConDescuento // Guardar precio CON descuento del paquete
                    });
                }
            }
        });

        // Deshabilitar el select de paquetes
        const select = document.getElementById(`paquete-select-${vehiculoId}`);
        if (select) select.disabled = true;

        // Actualizar select de servicios individuales para ocultar los ya agregados
        actualizarSelectServicios(vehiculoId);

        renderizarServiciosVehiculo(vehiculoId);
        actualizarResumen();
        actualizarBotonSubmit();

        // Reset select
        event.target.value = '';
    };

    window.agregarServicio = function (vehiculoId, servicioId) {
        if (!servicioId) return;

        const servicio = window.lavadoData.serviciosDisponibles[vehiculoId]?.find(s => s.id === servicioId);
        if (!servicio) return;

        // Verificar si ya existe un servicio del mismo tipo
        const servicioExistente = window.lavadoData.serviciosPorVehiculo[vehiculoId]?.find(s => s.tipo === servicio.tipo);
        if (servicioExistente) {
            showTableMessage('error', `Ya existe un servicio del tipo "${servicio.tipo}" para este vehículo.`);
            event.target.value = '';
            return;
        }

        window.lavadoData.serviciosPorVehiculo[vehiculoId].push(servicio);

        // Actualizar select de servicios
        actualizarSelectServicios(vehiculoId);

        renderizarServiciosVehiculo(vehiculoId);
        actualizarResumen();
        actualizarBotonSubmit();

        // Reset select
        event.target.value = '';
    };

    window.eliminarServicioDeVehiculo = function (vehiculoId, servicioId) {
        const servicios = window.lavadoData.serviciosPorVehiculo[vehiculoId];
        const servicio = servicios.find(s => s.id === servicioId);

        // Si el servicio pertenece a un paquete, eliminar todos los servicios del paquete
        if (servicio && servicio.paqueteId) {
            const paqueteId = servicio.paqueteId;
            window.lavadoData.serviciosPorVehiculo[vehiculoId] = servicios.filter(s => s.paqueteId !== paqueteId);

            // Rehabilitar el select de paquetes
            const select = document.getElementById(`paquete-select-${vehiculoId}`);
            if (select) {
                select.disabled = false;
                select.value = '';
            }

            showTableMessage('info', 'Se eliminó el paquete completo.');
        } else {
            // Eliminar solo el servicio individual
            window.lavadoData.serviciosPorVehiculo[vehiculoId] = servicios.filter(s => s.id !== servicioId);
        }

        // Actualizar select de servicios
        actualizarSelectServicios(vehiculoId);

        renderizarServiciosVehiculo(vehiculoId);
        actualizarResumen();
        actualizarBotonSubmit();
    };

    window.moverServicio = function (vehiculoId, servicioId, direccion) {
        const servicios = window.lavadoData.serviciosPorVehiculo[vehiculoId];
        const index = servicios.findIndex(s => s.id === servicioId);
        const servicio = servicios[index];

        // Si el servicio pertenece a un paquete, mover todo el paquete
        if (servicio.paqueteId) {
            const paqueteId = servicio.paqueteId;
            const serviciosPaquete = servicios.filter(s => s.paqueteId === paqueteId);
            const serviciosOtros = servicios.filter(s => s.paqueteId !== paqueteId);

            // Encontrar la posición del primer servicio del paquete
            const primerIndexPaquete = servicios.findIndex(s => s.paqueteId === paqueteId);

            if (direccion === 'up' && primerIndexPaquete > 0) {
                // Encontrar el servicio o paquete anterior
                let indexAnterior = primerIndexPaquete - 1;
                const servicioAnterior = servicios[indexAnterior];

                if (servicioAnterior.paqueteId) {
                    // El anterior es un paquete, mover todo el paquete actual antes de ese paquete
                    const serviciosPaqueteAnterior = servicios.filter(s => s.paqueteId === servicioAnterior.paqueteId);
                    indexAnterior = servicios.findIndex(s => s.paqueteId === servicioAnterior.paqueteId);

                    // Reorganizar: otros servicios antes del anterior paquete + paquete actual + paquete anterior + resto
                    const antesAnteriorPaquete = servicios.slice(0, indexAnterior);
                    const despuesPaquetes = servicios.slice(indexAnterior + serviciosPaqueteAnterior.length + serviciosPaquete.length);
                    window.lavadoData.serviciosPorVehiculo[vehiculoId] = [
                        ...antesAnteriorPaquete,
                        ...serviciosPaquete,
                        ...serviciosPaqueteAnterior,
                        ...despuesPaquetes
                    ];
                } else {
                    // El anterior es un servicio individual, intercambiar
                    const antesPaquete = servicios.slice(0, indexAnterior);
                    const despuesPaquete = servicios.slice(primerIndexPaquete + serviciosPaquete.length);
                    window.lavadoData.serviciosPorVehiculo[vehiculoId] = [
                        ...antesPaquete,
                        ...serviciosPaquete,
                        servicioAnterior,
                        ...despuesPaquete
                    ];
                }
            } else if (direccion === 'down' && primerIndexPaquete + serviciosPaquete.length < servicios.length) {
                // Encontrar el servicio o paquete siguiente
                let indexSiguiente = primerIndexPaquete + serviciosPaquete.length;
                const servicioSiguiente = servicios[indexSiguiente];

                if (servicioSiguiente.paqueteId && servicioSiguiente.paqueteId !== paqueteId) {
                    // El siguiente es un paquete diferente
                    const serviciosPaqueteSiguiente = servicios.filter(s => s.paqueteId === servicioSiguiente.paqueteId);

                    // Reorganizar
                    const antesPaquete = servicios.slice(0, primerIndexPaquete);
                    const despuesPaquetes = servicios.slice(indexSiguiente + serviciosPaqueteSiguiente.length);
                    window.lavadoData.serviciosPorVehiculo[vehiculoId] = [
                        ...antesPaquete,
                        ...serviciosPaqueteSiguiente,
                        ...serviciosPaquete,
                        ...despuesPaquetes
                    ];
                } else if (!servicioSiguiente.paqueteId) {
                    // El siguiente es un servicio individual
                    const antesPaquete = servicios.slice(0, primerIndexPaquete);
                    const despuesSiguiente = servicios.slice(indexSiguiente + 1);
                    window.lavadoData.serviciosPorVehiculo[vehiculoId] = [
                        ...antesPaquete,
                        servicioSiguiente,
                        ...serviciosPaquete,
                        ...despuesSiguiente
                    ];
                }
            }
        } else {
            // Servicio individual, comportamiento normal
            if (direccion === 'up' && index > 0) {
                const servicioAnterior = servicios[index - 1];

                // Si el anterior es parte de un paquete, mover el servicio individual antes de TODO el paquete
                if (servicioAnterior.paqueteId) {
                    const paqueteIdAnterior = servicioAnterior.paqueteId;
                    const serviciosPaqueteAnterior = servicios.filter(s => s.paqueteId === paqueteIdAnterior);
                    const primerIndexPaquete = servicios.findIndex(s => s.paqueteId === paqueteIdAnterior);

                    // Reorganizar: antes del paquete + servicio individual + paquete + después
                    const antesPaquete = servicios.slice(0, primerIndexPaquete);
                    const despuesServicio = servicios.slice(index + 1);
                    window.lavadoData.serviciosPorVehiculo[vehiculoId] = [
                        ...antesPaquete,
                        servicio,
                        ...serviciosPaqueteAnterior,
                        ...despuesServicio
                    ];
                } else {
                    [servicios[index], servicios[index - 1]] = [servicios[index - 1], servicios[index]];
                }
            } else if (direccion === 'down' && index < servicios.length - 1) {
                const servicioSiguiente = servicios[index + 1];

                // Si el siguiente es parte de un paquete, mover el servicio individual después de TODO el paquete
                if (servicioSiguiente.paqueteId) {
                    const paqueteIdSiguiente = servicioSiguiente.paqueteId;
                    const serviciosPaqueteSiguiente = servicios.filter(s => s.paqueteId === paqueteIdSiguiente);
                    const primerIndexPaquete = servicios.findIndex(s => s.paqueteId === paqueteIdSiguiente);
                    const ultimoIndexPaquete = primerIndexPaquete + serviciosPaqueteSiguiente.length - 1;

                    // Reorganizar: antes del servicio + paquete + servicio individual + después
                    const antesServicio = servicios.slice(0, index);
                    const despuesPaquete = servicios.slice(ultimoIndexPaquete + 1);
                    window.lavadoData.serviciosPorVehiculo[vehiculoId] = [
                        ...antesServicio,
                        ...serviciosPaqueteSiguiente,
                        servicio,
                        ...despuesPaquete
                    ];
                } else {
                    [servicios[index], servicios[index + 1]] = [servicios[index + 1], servicios[index]];
                }
            }
        }

        renderizarServiciosVehiculo(vehiculoId);
    };

    function renderizarServiciosVehiculo(vehiculoId) {
        const listaDiv = document.getElementById(`servicios-lista-${vehiculoId}`);
        const servicios = window.lavadoData.serviciosPorVehiculo[vehiculoId] || [];

        if (servicios.length === 0) {
            listaDiv.innerHTML = '<p class="text-gray-500 dark:text-gray-400 text-sm">No hay servicios seleccionados</p>';
            return;
        }

        // Agrupar servicios por paquete
        const grupos = [];
        let i = 0;
        while (i < servicios.length) {
            const servicio = servicios[i];
            if (servicio.paqueteId) {
                // Encontrar todos los servicios del mismo paquete
                const serviciosPaquete = servicios.filter(s => s.paqueteId === servicio.paqueteId);
                grupos.push({
                    tipo: 'paquete',
                    paqueteId: servicio.paqueteId,
                    paqueteNombre: servicio.paqueteNombre,
                    servicios: serviciosPaquete,
                    indiceInicial: i
                });
                i += serviciosPaquete.length;
            } else {
                grupos.push({
                    tipo: 'individual',
                    servicio: servicio,
                    indice: i
                });
                i++;
            }
        }

        listaDiv.innerHTML = grupos.map((grupo, grupoIndex) => {
            if (grupo.tipo === 'paquete') {
                const primerServicio = grupo.servicios[0];
                const esUltimoGrupo = grupoIndex === grupos.length - 1;
                const esPrimerGrupo = grupoIndex === 0;

                return `
                    <div class="border-2 border-blue-200 dark:border-blue-700 rounded-lg p-2 bg-blue-50 dark:bg-blue-900/20">
                        <div class="flex items-center justify-between mb-2">
                            <span class="text-xs font-semibold text-blue-600 dark:text-blue-400">📦 Paquete: ${escapeHtml(grupo.paqueteNombre)}</span>
                            <div class="flex items-center gap-1">
                                <button type="button" onclick="moverServicio('${vehiculoId}', '${primerServicio.id}', 'up')" 
                                        class="p-1 text-gray-400 hover:text-gray-600 ${esPrimerGrupo ? 'opacity-50 cursor-not-allowed' : ''}"
                                        ${esPrimerGrupo ? 'disabled' : ''}>
                                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 15l7-7 7 7"></path>
                                    </svg>
                                </button>
                                <button type="button" onclick="moverServicio('${vehiculoId}', '${primerServicio.id}', 'down')"
                                        class="p-1 text-gray-400 hover:text-gray-600 ${esUltimoGrupo ? 'opacity-50 cursor-not-allowed' : ''}"
                                        ${esUltimoGrupo ? 'disabled' : ''}>
                                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path>
                                    </svg>
                                </button>
                                <button type="button" onclick="eliminarServicioDeVehiculo('${vehiculoId}', '${primerServicio.id}')"
                                        class="p-1 text-red-400 hover:text-red-600">
                                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                                    </svg>
                                </button>
                            </div>
                        </div>
                        ${grupo.servicios.map((s, idx) => `
                            <div class="flex items-center gap-3 p-2 bg-white dark:bg-gray-800 rounded ${idx > 0 ? 'mt-1' : ''}">
                                <span class="text-gray-400 dark:text-gray-500 font-medium text-sm">${grupo.indiceInicial + idx + 1}.</span>
                                <div class="flex-1">
                                    <div class="font-medium text-gray-900 dark:text-white text-sm">${escapeHtml(s.nombre)}</div>
                                    <div class="text-xs text-gray-500 dark:text-gray-400">${escapeHtml(s.tipo)} • ${s.tiempoEstimado} min • ${formatCurrency(s.precio)}</div>
                                </div>
                            </div>
                        `).join('')}
                    </div>
                `;
            } else {
                const esUltimo = grupoIndex === grupos.length - 1;
                const esPrimero = grupoIndex === 0;
                const s = grupo.servicio;

                return `
                    <div class="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700 rounded-lg">
                        <div class="flex items-center gap-3">
                            <span class="text-gray-400 dark:text-gray-500 font-medium">${grupo.indice + 1}.</span>
                            <div>
                                <div class="font-medium text-gray-900 dark:text-white">${escapeHtml(s.nombre)}</div>
                                <div class="text-sm text-gray-500 dark:text-gray-400">${escapeHtml(s.tipo)} • ${s.tiempoEstimado} min • ${formatCurrency(s.precio)}</div>
                            </div>
                        </div>
                        <div class="flex items-center gap-1">
                            <button type="button" onclick="moverServicio('${vehiculoId}', '${s.id}', 'up')" 
                                    class="p-1 text-gray-400 hover:text-gray-600 ${esPrimero ? 'opacity-50 cursor-not-allowed' : ''}"
                                    ${esPrimero ? 'disabled' : ''}>
                                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 15l7-7 7 7"></path>
                                </svg>
                            </button>
                            <button type="button" onclick="moverServicio('${vehiculoId}', '${s.id}', 'down')"
                                    class="p-1 text-gray-400 hover:text-gray-600 ${esUltimo ? 'opacity-50 cursor-not-allowed' : ''}"
                                    ${esUltimo ? 'disabled' : ''}>
                                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path>
                                </svg>
                            </button>
                            <button type="button" onclick="eliminarServicioDeVehiculo('${vehiculoId}', '${s.id}')"
                                    class="p-1 text-red-400 hover:text-red-600">
                                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                                </svg>
                            </button>
                        </div>
                    </div>
                `;
            }
        }).join('');
    }

    window.actualizarResumen = function () {
        let precioTotal = 0;
        let tiempoTotal = 0;
        const paquetesContados = new Set();

        Object.values(window.lavadoData.serviciosPorVehiculo).forEach(servicios => {
            servicios.forEach(s => {
                // Si el servicio pertenece a un paquete
                if (s.paqueteId) {
                    // Solo contar el precio del paquete una vez
                    if (!paquetesContados.has(s.paqueteId)) {
                        precioTotal += s.precioPaquete || s.precio;
                        paquetesContados.add(s.paqueteId);
                    }
                } else {
                    // Servicio individual
                    precioTotal += s.precio;
                }
                tiempoTotal += s.tiempoEstimado;
            });
        });

        const descuento = parseFloat(document.getElementById('descuento')?.value || 0);
        const precioConDescuento = precioTotal - (precioTotal * descuento / 100);

        document.getElementById('precioOriginal').textContent = descuento > 0 ? formatCurrency(precioTotal) : '';
        document.getElementById('precioFinal').textContent = formatCurrency(precioConDescuento);
        document.getElementById('tiempoTotal').textContent = tiempoTotal > 60 ? `${(tiempoTotal / 60).toFixed(1)} hs` : `${tiempoTotal} min`;
    };

    function actualizarBotonSubmit() {
        const submitBtn = document.getElementById('submit-button');
        const tieneVehiculo = !!window.lavadoData.vehiculoId;
        const tieneCliente = !!window.lavadoData.clienteId;
        const tieneServicios = Object.values(window.lavadoData.serviciosPorVehiculo).some(s => s.length > 0);

        submitBtn.disabled = !(tieneVehiculo && tieneCliente && tieneServicios);
    }

    // =====================================
    // ENVÍO DEL FORMULARIO
    // =====================================
    function setupFormSubmit() {
        const form = document.getElementById('lavado-form');
        if (!form) return;

        form.addEventListener('submit', async function (e) {
            e.preventDefault();

            const vehiculosServicios = [];

            for (const vehiculoId of window.lavadoData.vehiculosSeleccionados) {
                const servicios = window.lavadoData.serviciosPorVehiculo[vehiculoId] || [];
                if (servicios.length > 0) {
                    vehiculosServicios.push({
                        vehiculoId: vehiculoId,
                        servicios: servicios.map(s => ({
                            servicioId: s.id,
                            paqueteId: s.paqueteId || null,
                            paqueteNombre: s.paqueteNombre || null
                        }))
                    });
                }
            }

            // Obtener los clientes que trajeron y retiraran el vehículo
            const clienteTrajoSelect = document.getElementById('clienteTrajoSelect');
            const clienteRetiraSelect = document.getElementById('clienteRetiraSelect');

            const requestData = {
                clienteId: window.lavadoData.clienteId,
                vehiculosServicios: vehiculosServicios,
                descuento: parseFloat(document.getElementById('descuento')?.value || 0),
                notas: document.getElementById('notas')?.value || null,
                clienteTrajoId: clienteTrajoSelect?.value || null,
                clienteRetiraId: clienteRetiraSelect?.value || null
            };

            try {
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

                const response = await fetch('/Lavados/CrearLavadoAjax', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify(requestData)
                });

                const result = await response.json();

                if (result.success) {
                    showTableMessage('success', result.message);
                    limpiarFormularioLavado();
                    reloadLavadoTable(1);

                    // Cerrar acordeón
                    const accordionBody = document.getElementById('accordion-flush-body-1');
                    if (accordionBody) accordionBody.classList.add('hidden');
                } else {
                    showTableMessage('error', result.message);
                }
            } catch (e) {
                console.error('Error al crear lavado:', e);
                showTableMessage('error', 'Error al crear el lavado.');
            }
        });
    }

    window.limpiarFormularioLavado = function () {
        window.lavadoData = {
            vehiculoId: null,
            vehiculoPatente: null,
            tipoVehiculo: null,
            clienteId: null,
            clienteNombre: null,
            clientesAsociados: [],
            vehiculos: [],
            vehiculosSeleccionados: [],
            serviciosPorVehiculo: {},
            serviciosDisponibles: {},
            paquetesDisponibles: {},
            empleadosInfo: null,
            cantidadEmpleadosPorTipo: 1
        };

        const patenteSearch = document.getElementById('patenteSearch');
        if (patenteSearch) patenteSearch.value = '';

        const vehiculoIdInput = document.getElementById('vehiculoId');
        if (vehiculoIdInput) vehiculoIdInput.value = '';

        const clienteIdInput = document.getElementById('clienteId');
        if (clienteIdInput) clienteIdInput.value = '';

        const vehiculoSeleccionado = document.getElementById('vehiculoSeleccionado');
        if (vehiculoSeleccionado) vehiculoSeleccionado.classList.add('hidden');

        const infoVehiculoSection = document.getElementById('infoVehiculoSection');
        if (infoVehiculoSection) infoVehiculoSection.style.display = 'none';

        const clientesAsociadosSection = document.getElementById('clientesAsociadosSection');
        if (clientesAsociadosSection) clientesAsociadosSection.style.display = 'none';

        const serviciosSection = document.getElementById('serviciosSection');
        if (serviciosSection) serviciosSection.style.display = 'none';

        const serviciosPorVehiculo = document.getElementById('serviciosPorVehiculo');
        if (serviciosPorVehiculo) serviciosPorVehiculo.innerHTML = '';

        const resumenSection = document.getElementById('resumenSection');
        if (resumenSection) resumenSection.style.display = 'none';

        const descuento = document.getElementById('descuento');
        if (descuento) descuento.value = '0';

        const notas = document.getElementById('notas');
        if (notas) notas.value = '';

        const submitButton = document.getElementById('submit-button');
        if (submitButton) submitButton.disabled = true;

        const infoDiv = document.getElementById('empleadosInfo');
        if (infoDiv) {
            infoDiv.innerHTML = '';
        }
    };

    // =====================================
    // MODALES
    // =====================================
    window.verDetalleLavado = function (id) {
        // Redirigir a la vista completa de detalle
        window.location.href = `/Lavados/Detalle?id=${id}`;
    };

    window.abrirModalPago = function (lavadoId, precioTotal, pagado) {
        document.getElementById('pagoLavadoId').value = lavadoId;
        document.getElementById('pagoTotal').textContent = formatCurrency(precioTotal);
        document.getElementById('pagoPagado').textContent = formatCurrency(pagado);

        const restante = precioTotal - pagado;
        document.getElementById('pagoRestante').textContent = formatCurrency(restante);

        // Configurar el input para no permitir más del restante
        const montoInput = document.getElementById('montoInput');
        montoInput.value = restante.toFixed(2);
        montoInput.max = restante.toFixed(2);
        montoInput.dataset.restante = restante.toFixed(2);

        document.getElementById('notasPago').value = '';

        abrirModal('pagoModal');
    };

    window.validarMontoPago = function () {
        const montoInput = document.getElementById('montoInput');
        const montoError = document.getElementById('montoError');
        const restante = parseFloat(montoInput.dataset.restante || 0);
        const monto = parseFloat(montoInput.value || 0);

        if (monto > restante) {
            montoInput.value = restante.toFixed(2);
            montoError.classList.remove('hidden');
            setTimeout(() => montoError.classList.add('hidden'), 3000);
        }
    };

    window.abrirModalCancelar = function (lavadoId, servicioId, etapaId, tipo) {
        document.getElementById('cancelarLavadoId').value = lavadoId;
        document.getElementById('cancelarServicioId').value = servicioId || '';
        document.getElementById('cancelarEtapaId').value = etapaId || '';
        document.getElementById('cancelarTipo').value = tipo;
        document.getElementById('motivoCancelacion').value = '';

        // Configurar la acción del formulario según el tipo
        const form = document.getElementById('formCancelar');
        if (form) {
            if (tipo === 'lavado') {
                form.action = '/Lavados/CancelarLavado';
            } else if (tipo === 'servicio') {
                form.action = '/Lavados/CancelarServicio';
            } else if (tipo === 'etapa') {
                form.action = '/Lavados/CancelarEtapa';
            }
        }

        abrirModal('cancelarModal');
    };

    window.abrirModalFinalizar = function (lavadoId, servicioId, etapaId, tipo, nombre) {
        document.getElementById('finalizarLavadoId').value = lavadoId;
        document.getElementById('finalizarServicioId').value = servicioId || '';
        document.getElementById('finalizarEtapaId').value = etapaId || '';
        document.getElementById('finalizarTipo').value = tipo;

        const title = tipo === 'lavado' ? 'Finalizar Lavado' :
            tipo === 'servicio' ? 'Finalizar Servicio' : 'Finalizar Etapa';
        const message = tipo === 'lavado' ? '¿Está seguro de finalizar este lavado?' :
            tipo === 'servicio' ? `¿Está seguro de finalizar el servicio "${nombre}"?` :
                `¿Está seguro de finalizar la etapa "${nombre}"?`;

        document.getElementById('finalizarModalTitle').textContent = title;
        document.getElementById('finalizarModalMessage').textContent = message;

        abrirModal('finalizarModal');
    };

    // Funciones para acciones desde el detalle
    window.iniciarServicioDesdeDetalle = async function (lavadoId, servicioId) {
        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const formData = new FormData();
            formData.append('lavadoId', lavadoId);
            formData.append('servicioId', servicioId);

            const response = await fetch('/Lavados/IniciarServicio', {
                method: 'POST',
                headers: { 'RequestVerificationToken': token },
                body: formData
            });

            const result = await response.json();
            if (result.success) {
                showTableMessage('success', result.message);
                verDetalleLavado(lavadoId);
                reloadLavadoTable(getCurrentTablePage());
            } else {
                showTableMessage('error', result.message);
            }
        } catch (e) {
            showTableMessage('error', 'Error al iniciar el servicio.');
        }
    };

    window.iniciarEtapaDesdeDetalle = async function (lavadoId, servicioId, etapaId) {
        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const formData = new FormData();
            formData.append('lavadoId', lavadoId);
            formData.append('servicioId', servicioId);
            formData.append('etapaId', etapaId);

            const response = await fetch('/Lavados/IniciarEtapa', {
                method: 'POST',
                headers: { 'RequestVerificationToken': token },
                body: formData
            });

            const result = await response.json();
            if (result.success) {
                showTableMessage('success', result.message);
                verDetalleLavado(lavadoId);
                reloadLavadoTable(getCurrentTablePage());
            } else {
                showTableMessage('error', result.message);
            }
        } catch (e) {
            showTableMessage('error', 'Error al iniciar la etapa.');
        }
    };

    // =====================================
    // ENVÍO DE FORMULARIOS DE MODALES
    // =====================================
    document.addEventListener('DOMContentLoaded', function () {
        // Formulario de pago
        const formPago = document.getElementById('formPago');
        if (formPago) {
            formPago.addEventListener('submit', async function (e) {
                e.preventDefault();

                const formData = new FormData(this);

                try {
                    const response = await fetch('/Lavados/RegistrarPago', {
                        method: 'POST',
                        body: formData
                    });

                    const result = await response.json();

                    cerrarModal('pagoModal');

                    if (result.success) {
                        showTableMessage('success', result.message);
                        reloadLavadoTable(getCurrentTablePage());
                    } else {
                        showTableMessage('error', result.message);
                    }
                } catch (e) {
                    showTableMessage('error', 'Error al registrar el pago.');
                }
            });
        }

        // Formulario de cancelar
        const formCancelar = document.getElementById('formCancelar');
        if (formCancelar) {
            formCancelar.addEventListener('submit', async function (e) {
                e.preventDefault();

                const tipo = document.getElementById('cancelarTipo').value;
                const lavadoId = document.getElementById('cancelarLavadoId').value;
                const servicioId = document.getElementById('cancelarServicioId').value;
                const etapaId = document.getElementById('cancelarEtapaId').value;
                const motivo = document.getElementById('motivoCancelacion').value;

                if (!motivo.trim()) {
                    showTableMessage('error', 'Debe ingresar un motivo de cancelación.');
                    return;
                }

                if (!lavadoId) {
                    showTableMessage('error', 'ID de lavado no válido.');
                    return;
                }

                let url;
                const formData = new FormData();

                // Obtener token antiforgery
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                if (token) {
                    formData.append('__RequestVerificationToken', token);
                }

                formData.append('motivo', motivo);

                if (tipo === 'lavado') {
                    url = '/Lavados/CancelarLavado';
                    formData.append('id', lavadoId);
                } else if (tipo === 'servicio') {
                    url = '/Lavados/CancelarServicio';
                    formData.append('lavadoId', lavadoId);
                    formData.append('servicioId', servicioId);
                } else {
                    url = '/Lavados/CancelarEtapa';
                    formData.append('lavadoId', lavadoId);
                    formData.append('servicioId', servicioId);
                    formData.append('etapaId', etapaId);
                }

                try {
                    const response = await fetch(url, {
                        method: 'POST',
                        body: formData
                    });

                    const result = await response.json();

                    cerrarModal('cancelarModal');

                    if (result.success) {
                        showTableMessage('success', result.message);
                        reloadLavadoTable(getCurrentTablePage());
                    } else {
                        showTableMessage('error', result.message);
                    }
                } catch (e) {
                    console.error('Error al cancelar:', e);
                    showTableMessage('error', 'Error al cancelar: ' + e.message);
                }
            });
        }

        // Formulario de finalizar
        const formFinalizar = document.getElementById('formFinalizar');
        if (formFinalizar) {
            formFinalizar.addEventListener('submit', async function (e) {
                e.preventDefault();

                const tipo = document.getElementById('finalizarTipo').value;
                const lavadoId = document.getElementById('finalizarLavadoId').value;
                const servicioId = document.getElementById('finalizarServicioId').value;
                const etapaId = document.getElementById('finalizarEtapaId').value;

                let url;
                const formData = new FormData();

                if (tipo === 'lavado') {
                    url = '/Lavados/FinalizarLavado';
                    formData.append('id', lavadoId);
                } else if (tipo === 'servicio') {
                    url = '/Lavados/FinalizarServicio';
                    formData.append('lavadoId', lavadoId);
                    formData.append('servicioId', servicioId);
                } else {
                    url = '/Lavados/FinalizarEtapa';
                    formData.append('lavadoId', lavadoId);
                    formData.append('servicioId', servicioId);
                    formData.append('etapaId', etapaId);
                }

                try {
                    const response = await fetch(url, {
                        method: 'POST',
                        body: formData
                    });

                    const result = await response.json();

                    cerrarModal('finalizarModal');
                    cerrarModal('detalleModal');

                    if (result.success) {
                        showTableMessage('success', result.message);
                        reloadLavadoTable(getCurrentTablePage());
                    } else {
                        showTableMessage('error', result.message);
                    }
                } catch (e) {
                    showTableMessage('error', 'Error al finalizar.');
                }
            });
        }
    });

    // =====================================
    // FILTROS
    // =====================================
    function setupFilterFormSubmit() {
        const form = document.getElementById('filterForm');
        if (!form || form.dataset.submitSetup === 'true') return;

        form.addEventListener('submit', function (e) {
            e.preventDefault();

            // Validar campos de precio antes de enviar
            const precioDesde = document.getElementById('precioDesde');
            const precioHasta = document.getElementById('precioHasta');

            let isValid = true;

            // Validar precioDesde
            if (precioDesde && precioDesde.value.trim() !== '') {
                const value = parseFloat(precioDesde.value);
                const min = precioDesde.hasAttribute('min') ? parseFloat(precioDesde.getAttribute('min')) : 0;
                const max = precioDesde.hasAttribute('max') ? parseFloat(precioDesde.getAttribute('max')) : null;

                if (isNaN(value) || value < 0) {
                    precioDesde.setCustomValidity('Ingrese un precio válido');
                    isValid = false;
                } else if (value < min) {
                    precioDesde.setCustomValidity(`El valor debe ser mayor de o igual a ${min.toFixed(2)}`);
                    isValid = false;
                } else if (max !== null && value > max) {
                    precioDesde.setCustomValidity(`El valor debe ser menor de o igual a ${max.toFixed(2)}`);
                    isValid = false;
                } else {
                    precioDesde.setCustomValidity('');
                }
            } else if (precioDesde) {
                precioDesde.setCustomValidity('');
            }

            // Validar precioHasta
            if (precioHasta && precioHasta.value.trim() !== '') {
                const value = parseFloat(precioHasta.value);
                const min = precioHasta.hasAttribute('min') ? parseFloat(precioHasta.getAttribute('min')) : 0;
                const max = precioHasta.hasAttribute('max') ? parseFloat(precioHasta.getAttribute('max')) : null;

                if (isNaN(value) || value < 0) {
                    precioHasta.setCustomValidity('Ingrese un precio válido');
                    isValid = false;
                } else if (value < min) {
                    precioHasta.setCustomValidity(`El valor debe ser mayor de o igual a ${min.toFixed(2)}`);
                    isValid = false;
                } else if (max !== null && value > max) {
                    precioHasta.setCustomValidity(`El valor debe ser menor de o igual a ${max.toFixed(2)}`);
                    isValid = false;
                } else {
                    precioHasta.setCustomValidity('');
                }
            } else if (precioHasta) {
                precioHasta.setCustomValidity('');
            }

            // Validar que precioDesde <= precioHasta
            if (precioDesde && precioHasta &&
                precioDesde.value.trim() !== '' && precioHasta.value.trim() !== '') {
                const desde = parseFloat(precioDesde.value);
                const hasta = parseFloat(precioHasta.value);

                if (!isNaN(desde) && !isNaN(hasta) && desde > hasta) {
                    precioDesde.setCustomValidity('El precio mínimo no puede ser mayor que el precio máximo');
                    isValid = false;
                }
            }

            // Usar checkValidity() para validar el formulario completo
            if (!form.checkValidity() || !isValid) {
                form.reportValidity();
                return false;
            }

            // Si todo está bien, aplicar filtros
            reloadLavadoTable(1);

            const dd = document.getElementById('filterDropdown');
            if (dd) dd.classList.add('hidden');

            showTableMessage('info', 'Filtros aplicados.');
        });

        form.dataset.submitSetup = 'true';
    }

    window.clearLavadoFilters = function () {
        const filterForm = document.getElementById('filterForm');

        if (filterForm) {
            // Marcar TODOS los checkboxes de estado por defecto
            filterForm.querySelectorAll('input[type="checkbox"]').forEach(cb => {
                cb.checked = true;
            });

            // Limpiar campos de fecha
            const fechaDesde = document.getElementById('fechaDesde');
            const fechaHasta = document.getElementById('fechaHasta');
            if (fechaDesde) fechaDesde.value = '';
            if (fechaHasta) fechaHasta.value = '';

            // Limpiar campos de precio
            const precioDesde = document.getElementById('precioDesde');
            const precioHasta = document.getElementById('precioHasta');
            if (precioDesde) precioDesde.value = '';
            if (precioHasta) precioHasta.value = '';
        }

        const searchInput = document.getElementById('simple-search');
        if (searchInput) searchInput.value = '';
        currentSearchTerm = '';

        const filterDropdown = document.getElementById('filterDropdown');
        if (filterDropdown) filterDropdown.classList.add('hidden');

        reloadLavadoTable(1);
        showTableMessage('info', 'Filtros restablecidos.');
    };

    // =====================================
    // UTILIDADES
    // =====================================
    function getCurrentTablePage() {
        return parseInt(document.getElementById('lavado-table-container')?.dataset.currentPage || '1');
    }

    function showTableMessage(type, msg, disappearMs = 5000) {
        let container = document.getElementById('table-messages-container');

        if (!container) {
            container = document.createElement('div');
            container.id = 'table-messages-container';
            container.className = 'mb-4';

            const tableContainer = document.getElementById('lavado-table-container');
            tableContainer.parentNode.insertBefore(container, tableContainer);
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

        container.innerHTML = `<div id="table-inline-alert"
            class="table-inline-alert opacity-100 transition-opacity duration-700
                   p-4 mb-4 text-sm rounded-lg border
                   bg-${color.bg} text-${color.text} border-${color.border}
                   dark:bg-gray-800 dark:text-${color.darkText}">
            ${msg}
        </div>`;

        const alertEl = document.getElementById('table-inline-alert');
        if (!alertEl) return;

        tableMsgTimeout = setTimeout(() => {
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

    function actualizarSelectServicios(vehiculoId) {
        const select = document.getElementById(`servicios-select-${vehiculoId}`);
        if (!select) return;

        const serviciosAgregados = window.lavadoData.serviciosPorVehiculo[vehiculoId] || [];
        const tiposAgregados = serviciosAgregados.map(s => s.tipo);
        const idsAgregados = serviciosAgregados.map(s => s.id);

        // Obtener todas las opciones excepto la primera (placeholder)
        const opciones = Array.from(select.options).slice(1);

        opciones.forEach(option => {
            const servicioId = option.value;
            const servicioTipo = option.getAttribute('data-tipo');

            // Ocultar si el servicio ya está agregado o si su tipo ya está agregado
            if (idsAgregados.includes(servicioId) || tiposAgregados.includes(servicioTipo)) {
                option.style.display = 'none';
                option.disabled = true;
            } else {
                option.style.display = '';
                option.disabled = false;
            }
        });
    }

    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function formatCurrency(value) {
        return new Intl.NumberFormat('es-AR', {
            style: 'currency',
            currency: 'ARS'
        }).format(value);
    }

    function abrirModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        try {
            if (typeof Modal !== 'undefined') {
                const inst = Modal.getOrCreateInstance(modal);
                if (inst) {
                    inst.show();
                    return;
                }
            }
        } catch { }

        // Crear backdrop si no existe
        let backdrop = document.getElementById('modal-backdrop');
        if (!backdrop) {
            backdrop = document.createElement('div');
            backdrop.id = 'modal-backdrop';
            backdrop.className = 'fixed inset-0 bg-gray-900 bg-opacity-50 z-40';
            backdrop.style.backgroundColor = 'rgba(17, 24, 39, 0.5)';
            document.body.appendChild(backdrop);
        }

        modal.classList.remove('hidden');
        modal.classList.add('z-50');
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('overflow-hidden');
    }

    function cerrarModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        try {
            if (typeof Modal !== 'undefined') {
                const inst = Modal.getInstance(modal);
                if (inst) {
                    inst.hide();
                    return;
                }
            }
        } catch { }

        modal.classList.add('hidden');
        modal.setAttribute('aria-hidden', 'true');

        // Remover backdrop
        const backdrop = document.getElementById('modal-backdrop');
        if (backdrop) {
            backdrop.remove();
        }

        document.querySelectorAll('[modal-backdrop]').forEach(b => b.remove());
        document.body.classList.remove('overflow-hidden');
    }

    window.cerrarModal = cerrarModal;
    window.abrirModal = abrirModal;

    // =====================================
    // SETUP DE BOTONES DE CIERRE DE MODALES
    // =====================================
    document.addEventListener('DOMContentLoaded', function () {
        // Manejar botones con data-modal-hide
        document.querySelectorAll('[data-modal-hide]').forEach(button => {
            button.addEventListener('click', function () {
                const modalId = this.getAttribute('data-modal-hide');
                cerrarModal(modalId);
            });
        });
    });

    // =====================================
    // MONITOREO DE TIEMPO
    // =====================================
    let tiempoIntervalId = null;
    let configTiempos = null;

    async function cargarConfiguracionTiempos() {
        try {
            const response = await fetch('/Lavados/ObtenerConfiguracion');
            configTiempos = await response.json();
        } catch {
            configTiempos = {
                tiempoNotificacionMinutos: 15,
                tiempoToleranciaMinutos: 15,
                intervaloPreguntas: 5
            };
        }
    }

    function iniciarMonitoreoTiempo() {
        if (tiempoIntervalId) return;

        cargarConfiguracionTiempos();

        tiempoIntervalId = setInterval(() => {
            verificarTiemposLavados();
        }, 60000); // Verificar cada minuto
    }

    function verificarTiemposLavados() {
        if (!configTiempos) return;

        const filas = document.querySelectorAll('[data-lavado-id][data-tiempo-inicio]');

        filas.forEach(fila => {
            const tiempoInicioStr = fila.dataset.tiempoInicio;
            const tiempoEstimado = parseInt(fila.dataset.tiempoEstimado) || 0;

            if (!tiempoInicioStr || !tiempoEstimado) return;

            const tiempoInicio = new Date(tiempoInicioStr);
            const ahora = new Date();
            const minutosTranscurridos = (ahora - tiempoInicio) / 60000;
            const minutosRestantes = tiempoEstimado - minutosTranscurridos;

            // Notificación cuando faltan X minutos
            if (minutosRestantes > 0 && minutosRestantes <= configTiempos.tiempoNotificacionMinutos) {
                console.log(`Lavado ${fila.dataset.lavadoId}: Faltan ${Math.round(minutosRestantes)} minutos`);
            }

            // Tiempo excedido
            if (minutosRestantes < -configTiempos.tiempoToleranciaMinutos) {
                console.log(`Lavado ${fila.dataset.lavadoId}: Tiempo excedido`);
            }
        });
    }

    window.cerrarModalTiempoExcedido = function (terminado) {
        cerrarModal('tiempoExcedidoModal');

        if (terminado) {
            // Aquí se podría abrir el modal de finalización
        }
    };

    // Iniciar monitoreo cuando la página carga (con protección contra múltiples llamadas)
    if (!window._lavadoMonitoreoInicializado) {
        window._lavadoMonitoreoInicializado = true;
        document.addEventListener('DOMContentLoaded', iniciarMonitoreoTiempo);
    }

    // =====================================
    // TOOLTIPS Y VALIDACIÓN DINÁMICA PARA FILTROS
    // =====================================
    function initFilterTooltips() {
        // Tooltips y validación para campos de precio
        const precioDesde = document.getElementById('precioDesde');
        const precioHasta = document.getElementById('precioHasta');

        if (precioDesde) {
            // Prevenir entrada de caracteres no numéricos (excepto punto decimal)
            precioDesde.addEventListener('keypress', function (e) {
                const char = String.fromCharCode(e.which);
                if (!/[\d.]/.test(char)) {
                    e.preventDefault();
                }
                // Solo permitir un punto decimal
                if (char === '.' && this.value.includes('.')) {
                    e.preventDefault();
                }
            });
        }

        if (precioHasta) {
            // Prevenir entrada de caracteres no numéricos (excepto punto decimal)
            precioHasta.addEventListener('keypress', function (e) {
                const char = String.fromCharCode(e.which);
                if (!/[\d.]/.test(char)) {
                    e.preventDefault();
                }
                // Solo permitir un punto decimal
                if (char === '.' && this.value.includes('.')) {
                    e.preventDefault();
                }
            });
        }
    }

    // Nota: initFilterTooltips() se llama desde initializeLavadosPage()

})();
