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
    // BÚSQUEDA DE CLIENTES
    // =====================================
    function setupClienteSearch() {
        const searchInput = document.getElementById('clienteSearch');
        const resultadosDiv = document.getElementById('clienteResultados');

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
                buscarClientes(term);
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

    async function buscarClientes(term) {
        const resultadosDiv = document.getElementById('clienteResultados');

        try {
            const response = await fetch(`/Lavados/ObtenerClientes?search=${encodeURIComponent(term)}`);
            const clientes = await response.json();

            if (clientes.length === 0) {
                resultadosDiv.innerHTML = '<div class="p-3 text-gray-500 dark:text-gray-400">No se encontraron clientes</div>';
            } else {
                resultadosDiv.innerHTML = clientes.map(c => `
                    <div class="p-3 hover:bg-gray-100 dark:hover:bg-gray-600 cursor-pointer border-b dark:border-gray-600 last:border-b-0"
                         onclick="seleccionarCliente('${c.id}', '${escapeHtml(c.nombre)}')">
                        <div class="font-medium text-gray-900 dark:text-white">${escapeHtml(c.nombre)}</div>
                        <div class="text-sm text-gray-500 dark:text-gray-400">${escapeHtml(c.documento)}</div>
                    </div>
                `).join('');
            }

            resultadosDiv.classList.remove('hidden');
        } catch (e) {
            console.error('Error buscando clientes:', e);
            resultadosDiv.innerHTML = '<div class="p-3 text-red-500">Error al buscar clientes</div>';
            resultadosDiv.classList.remove('hidden');
        }
    }

    window.seleccionarCliente = function (id, nombre) {
        document.getElementById('clienteId').value = id;
        document.getElementById('clienteSearch').value = nombre;
        document.getElementById('clienteResultados').classList.add('hidden');
        document.getElementById('clienteSeleccionado').classList.remove('hidden');
        document.getElementById('clienteNombre').textContent = nombre;

        window.lavadoData.clienteId = id;
        window.lavadoData.clienteNombre = nombre;

        cargarVehiculosCliente(id);
    };

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
                         onclick="toggleVehiculo('${v.id}', '${v.tipoVehiculo}')">
                        <div class="flex items-center gap-3">
                            <input type="checkbox" class="vehiculo-checkbox w-5 h-5 text-blue-600 rounded focus:ring-blue-500" 
                                   id="vehiculo-${v.id}" data-vehiculo-id="${v.id}">
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

    window.toggleVehiculo = async function (vehiculoId, tipoVehiculo) {
        const checkbox = document.getElementById(`vehiculo-${vehiculoId}`);
        const card = checkbox.closest('.vehiculo-card');

        checkbox.checked = !checkbox.checked;

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
                    <label class="block mb-2 text-sm font-medium text-gray-900 dark:text-white">Paquetes de Servicios</label>
                    <select onchange="agregarPaquete('${vehiculoId}', this.value)" 
                            class="bg-gray-50 border border-gray-300 text-gray-900 text-sm rounded-lg focus:ring-primary-500 focus:border-primary-500 block w-full p-2.5 dark:bg-gray-700 dark:border-gray-600 dark:text-white">
                        <option value="">Seleccionar paquete...</option>
                        ${paquetes.map(p => `<option value="${p.id}">${escapeHtml(p.nombre)} - ${formatCurrency(p.precio)} (${p.descuento}% desc.)</option>`).join('')}
                    </select>
                </div>
                ` : ''}
                
                <div class="mb-4">
                    <label class="block mb-2 text-sm font-medium text-gray-900 dark:text-white">Servicios Individuales</label>
                    <select onchange="agregarServicio('${vehiculoId}', this.value)"
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
                        paqueteNombre: paquete.nombre
                    });
                }
            }
        });

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

        renderizarServiciosVehiculo(vehiculoId);
        actualizarResumen();
        actualizarBotonSubmit();

        // Reset select
        event.target.value = '';
    };

    window.eliminarServicioDeVehiculo = function (vehiculoId, servicioId) {
        window.lavadoData.serviciosPorVehiculo[vehiculoId] = window.lavadoData.serviciosPorVehiculo[vehiculoId].filter(s => s.id !== servicioId);
        renderizarServiciosVehiculo(vehiculoId);
        actualizarResumen();
        actualizarBotonSubmit();
    };

    window.moverServicio = function (vehiculoId, servicioId, direccion) {
        const servicios = window.lavadoData.serviciosPorVehiculo[vehiculoId];
        const index = servicios.findIndex(s => s.id === servicioId);

        if (direccion === 'up' && index > 0) {
            [servicios[index], servicios[index - 1]] = [servicios[index - 1], servicios[index]];
        } else if (direccion === 'down' && index < servicios.length - 1) {
            [servicios[index], servicios[index + 1]] = [servicios[index + 1], servicios[index]];
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

        listaDiv.innerHTML = servicios.map((s, index) => `
            <div class="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700 rounded-lg" draggable="true">
                <div class="flex items-center gap-3">
                    <span class="text-gray-400 dark:text-gray-500 font-medium">${index + 1}.</span>
                    <div>
                        <div class="font-medium text-gray-900 dark:text-white">${escapeHtml(s.nombre)}</div>
                        <div class="text-sm text-gray-500 dark:text-gray-400">${escapeHtml(s.tipo)} • ${s.tiempoEstimado} min • ${formatCurrency(s.precio)}</div>
                        ${s.paqueteNombre ? `<div class="text-xs text-blue-600 dark:text-blue-400">Paquete: ${escapeHtml(s.paqueteNombre)}</div>` : ''}
                    </div>
                </div>
                <div class="flex items-center gap-1">
                    <button type="button" onclick="moverServicio('${vehiculoId}', '${s.id}', 'up')" 
                            class="p-1 text-gray-400 hover:text-gray-600 ${index === 0 ? 'opacity-50 cursor-not-allowed' : ''}"
                            ${index === 0 ? 'disabled' : ''}>
                        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 15l7-7 7 7"></path>
                        </svg>
                    </button>
                    <button type="button" onclick="moverServicio('${vehiculoId}', '${s.id}', 'down')"
                            class="p-1 text-gray-400 hover:text-gray-600 ${index === servicios.length - 1 ? 'opacity-50 cursor-not-allowed' : ''}"
                            ${index === servicios.length - 1 ? 'disabled' : ''}>
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
        `).join('');
    }

    window.actualizarResumen = function () {
        let precioTotal = 0;
        let tiempoTotal = 0;

        Object.values(window.lavadoData.serviciosPorVehiculo).forEach(servicios => {
            servicios.forEach(s => {
                precioTotal += s.precio;
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
        const tieneCliente = !!window.lavadoData.clienteId;
        const tieneServicios = Object.values(window.lavadoData.serviciosPorVehiculo).some(s => s.length > 0);

        submitBtn.disabled = !(tieneCliente && tieneServicios);
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

            const requestData = {
                clienteId: window.lavadoData.clienteId,
                vehiculosServicios: vehiculosServicios,
                cantidadEmpleados: parseInt(document.getElementById('cantidadEmpleados')?.value || 1),
                descuento: parseFloat(document.getElementById('descuento')?.value || 0),
                notas: document.getElementById('notas')?.value || null
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
            clienteId: null,
            clienteNombre: null,
            vehiculos: [],
            vehiculosSeleccionados: [],
            serviciosPorVehiculo: {},
            serviciosDisponibles: {},
            paquetesDisponibles: {}
        };

        document.getElementById('clienteSearch').value = '';
        document.getElementById('clienteId').value = '';
        document.getElementById('clienteSeleccionado').classList.add('hidden');
        document.getElementById('vehiculosSection').style.display = 'none';
        document.getElementById('vehiculosList').innerHTML = '';
        document.getElementById('serviciosSection').style.display = 'none';
        document.getElementById('serviciosPorVehiculo').innerHTML = '';
        document.getElementById('resumenSection').style.display = 'none';
        document.getElementById('descuento').value = '0';
        document.getElementById('notas').value = '';
        document.getElementById('cantidadEmpleados').value = '1';
        document.getElementById('submit-button').disabled = true;
    };

    // =====================================
    // MODALES
    // =====================================
    window.verDetalleLavado = async function (id) {
        try {
            const response = await fetch(`/Lavados/DetailPartial?id=${id}`, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            const html = await response.text();

            document.getElementById('detalleModalContent').innerHTML = html;
            abrirModal('detalleModal');
        } catch (e) {
            console.error('Error cargando detalle:', e);
            showTableMessage('error', 'Error al cargar el detalle del lavado.');
        }
    };

    window.abrirModalPago = function (lavadoId, precioTotal, pagado) {
        document.getElementById('pagoLavadoId').value = lavadoId;
        document.getElementById('pagoTotal').textContent = formatCurrency(precioTotal);
        document.getElementById('pagoPagado').textContent = formatCurrency(pagado);
        document.getElementById('pagoRestante').textContent = formatCurrency(precioTotal - pagado);
        document.getElementById('montoInput').value = (precioTotal - pagado).toFixed(2);
        document.getElementById('notasPago').value = '';

        abrirModal('pagoModal');
    };

    window.abrirModalCancelar = function (lavadoId, servicioId, etapaId, tipo) {
        document.getElementById('cancelarLavadoId').value = lavadoId;
        document.getElementById('cancelarServicioId').value = servicioId || '';
        document.getElementById('cancelarEtapaId').value = etapaId || '';
        document.getElementById('cancelarTipo').value = tipo;
        document.getElementById('motivoCancelacion').value = '';

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
                    alert('Debe ingresar un motivo de cancelación.');
                    return;
                }

                let url;
                const formData = new FormData();
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
                    cerrarModal('detalleModal');

                    if (result.success) {
                        showTableMessage('success', result.message);
                        reloadLavadoTable(getCurrentTablePage());
                    } else {
                        showTableMessage('error', result.message);
                    }
                } catch (e) {
                    showTableMessage('error', 'Error al cancelar.');
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
            filterForm.querySelectorAll('input[type="checkbox"]').forEach(cb => {
                cb.checked = cb.value === 'Pendiente' || cb.value === 'EnProceso';
            });
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

        modal.classList.remove('hidden');
        modal.setAttribute('aria-hidden', 'false');
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
        document.querySelectorAll('[modal-backdrop]').forEach(b => b.remove());
        document.body.classList.remove('overflow-hidden');
    }

    window.cerrarModal = cerrarModal;
    window.abrirModal = abrirModal;

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

})();
