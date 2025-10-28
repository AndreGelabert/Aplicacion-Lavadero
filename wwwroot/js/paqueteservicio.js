/**
 * ================================================
 * PAQUETESERVICIO.JS - FUNCIONALIDAD DE LA PÁGINA DE PAQUETES DE SERVICIOS
 * ================================================
 */

(function () {
    'use strict';

    let paqueteMsgTimeout = null;
    let tableMsgTimeout = null;
    let searchTimeout = null;
    let currentSearchTerm = '';
    let serviciosDisponibles = [];
    let serviciosSeleccionados = [];
    let servicioSeleccionadoDropdown = null; // Servicio actualmente seleccionado en el dropdown

    // =====================================
    // INICIALIZACIÓN DEL MÓDULO
    // =====================================
    window.PageModules = window.PageModules || {};
    window.PageModules.paquetes = {
        init: initializePaquetesPage
    };

    /**
     * Inicializa la funcionalidad específica de la página de Paquetes
     */
    function initializePaquetesPage() {
        setupFormMessageHandler();
        setupSearchWithDebounce();
        window.CommonUtils?.setupDefaultFilterForm();
        checkEditMode();
        initializeForm();
        setupDropdownClickOutside();
    }

    // IMPORTANTE: asegurar que init se ejecute siempre
    document.addEventListener('DOMContentLoaded', () => {
        try {
            window.PageModules?.paquetes?.init();
        } catch (e) {
            initializePaquetesPage();
        }
    });

    // =====================================
    // CONFIGURACIÓN INICIAL
    // =====================================
    /**
     * Verifica si estamos editando para abrir el acordeón
     */
    function checkEditMode() {
        const formTitle = document.getElementById('form-title');
        if (formTitle && formTitle.textContent.includes('Editando')) {
            const accordion = document.getElementById('accordion-flush-body-1');
            if (accordion) accordion.classList.remove('hidden');
        }
    }

    /**
     * Inicializa el formulario y carga datos si está en modo edición
     */
    function initializeForm() {
        if (window.paqueteEditData && window.paqueteEditData.serviciosIds) {
            const tipoVehiculo = document.getElementById('TipoVehiculo')?.value;
            if (tipoVehiculo) {
                loadServiciosPorTipoVehiculo().then(() => {
                    // Cargar servicios seleccionados en modo edición
                    window.paqueteEditData.serviciosIds.forEach(id => {
                        const servicio = serviciosDisponibles.find(s => s.id === id);
                        if (servicio) {
                            serviciosSeleccionados.push({
                                id: servicio.id,
                                nombre: servicio.nombre,
                                tipo: servicio.tipo,
                                precio: servicio.precio,
                                tiempoEstimado: servicio.tiempoEstimado
                            });
                        }
                    });
                    updateServiciosSeleccionadosList();
                    updateResumen();
                });
            }
        }
    }

    /**
     * Configura el manejo de mensajes del formulario
     */
    function setupFormMessageHandler() {
        document.addEventListener('input', (e) => {
            if (e.target.closest('#paquete-form')) {
                hidePaqueteMessage();
            }
        });
    }

    /**
     * Configura cierre del dropdown al hacer clic fuera
     */
    function setupDropdownClickOutside() {
        document.addEventListener('click', (e) => {
            const dropdown = document.getElementById('servicio-dropdown');
            const searchInput = document.getElementById('servicio-search');
            
            if (dropdown && !dropdown.contains(e.target) && e.target !== searchInput) {
                dropdown.classList.add('hidden');
            }
        });
    }

    // =====================================
    // BÚSQUEDA DE LA TABLA
    // =====================================
    /**
     * Configura la búsqueda con debouncing
     */
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
                reloadPaqueteTable(1);
                return;
            }

            searchTimeout = setTimeout(() => {
                performServerSearch(searchTerm);
            }, 500);
        });
    }

    /**
     * Realiza búsqueda en el servidor
     */
    function performServerSearch(searchTerm) {
        currentSearchTerm = searchTerm;
        const estados = getSelectedCheckboxValues('estados');
        const tiposVehiculo = getSelectedCheckboxValues('tiposVehiculo');
        const sortBy = document.getElementById('current-sort-by')?.value || 'Nombre';
        const sortOrder = document.getElementById('current-sort-order')?.value || 'asc';

        const url = buildSearchUrl(searchTerm, estados, tiposVehiculo, sortBy, sortOrder);
        loadTablePartial(url);
    }

    /**
     * Construye URL de búsqueda
     */
    function buildSearchUrl(searchTerm, estados, tiposVehiculo, sortBy, sortOrder) {
        const params = new URLSearchParams();
        params.append('searchTerm', searchTerm);
        params.append('pageNumber', '1');
        params.append('pageSize', '10');
        params.append('sortBy', sortBy);
        params.append('sortOrder', sortOrder);

        estados.forEach(e => params.append('estados', e));
        tiposVehiculo.forEach(tv => params.append('tiposVehiculo', tv));

        return '/PaqueteServicio/SearchPartial?' + params.toString();
    }

    /**
     * Recarga la tabla de paquetes
     */
    function reloadPaqueteTable(pageNumber) {
        const estados = getSelectedCheckboxValues('estados');
        const tiposVehiculo = getSelectedCheckboxValues('tiposVehiculo');
        const sortBy = document.getElementById('current-sort-by')?.value || 'Nombre';
        const sortOrder = document.getElementById('current-sort-order')?.value || 'asc';

        const params = new URLSearchParams();
        params.append('pageNumber', pageNumber);
        params.append('pageSize', '10');
        params.append('sortBy', sortBy);
        params.append('sortOrder', sortOrder);

        estados.forEach(e => params.append('estados', e));
        tiposVehiculo.forEach(tv => params.append('tiposVehiculo', tv));

        const url = '/PaqueteServicio/TablePartial?' + params.toString();
        loadTablePartial(url);
    }

    /**
     * Carga parcial de tabla
     */
    function loadTablePartial(url) {
        fetch(url)
            .then(response => response.text())
            .then(html => {
                const container = document.getElementById('paquete-table-container');
                if (container) {
                    container.innerHTML = html;
                }
            })
            .catch(error => {
                console.error('Error cargando tabla:', error);
                showTableMessage('Error al cargar los datos', 'error');
            });
    }

    /**
     * Obtiene valores seleccionados de checkboxes
     */
    function getSelectedCheckboxValues(name) {
        return Array.from(document.querySelectorAll(`input[name="${name}"]:checked`))
            .map(cb => cb.value);
    }

    // =====================================
    // GESTIÓN DE SERVICIOS
    // =====================================
    /**
     * Carga servicios por tipo de vehículo
     */
    window.loadServiciosPorTipoVehiculo = async function () {
        const tipoVehiculo = document.getElementById('TipoVehiculo')?.value;
        const container = document.getElementById('servicio-selector-container');

        if (!tipoVehiculo) {
            container.classList.add('hidden');
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
                container.classList.remove('hidden');
                renderServiciosDropdown(serviciosDisponibles);
            } else {
                container.classList.add('hidden');
                showPaqueteMessage(data.message || 'No hay servicios disponibles', 'error');
            }
        } catch (error) {
            console.error('Error cargando servicios:', error);
            container.classList.add('hidden');
            showPaqueteMessage('Error al cargar servicios', 'error');
        }
    };

    /**
     * Renderiza los servicios en el dropdown agrupados por tipo
     */
    function renderServiciosDropdown(servicios, filterText = '') {
        const dropdownContent = document.getElementById('servicio-dropdown-content');
        
        if (!servicios || servicios.length === 0) {
            dropdownContent.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No hay servicios disponibles</p>';
            return;
        }

        // Filtrar servicios por texto de búsqueda
        let serviciosFiltrados = servicios;
        if (filterText) {
            const searchLower = filterText.toLowerCase();
            serviciosFiltrados = servicios.filter(s => 
                s.nombre.toLowerCase().includes(searchLower) ||
                s.tipo.toLowerCase().includes(searchLower)
            );
        }

        // Filtrar servicios ya seleccionados
        serviciosFiltrados = serviciosFiltrados.filter(s => 
            !serviciosSeleccionados.some(sel => sel.id === s.id)
        );

        if (serviciosFiltrados.length === 0) {
            dropdownContent.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No se encontraron servicios</p>';
            return;
        }

        // Agrupar por tipo
        const serviciosPorTipo = {};
        serviciosFiltrados.forEach(s => {
            if (!serviciosPorTipo[s.tipo]) {
                serviciosPorTipo[s.tipo] = [];
            }
            serviciosPorTipo[s.tipo].push(s);
        });

        // Renderizar agrupados
        let html = '';
        Object.keys(serviciosPorTipo).sort().forEach(tipo => {
            html += `<div class="mb-2">
                <h6 class="text-xs font-semibold text-gray-700 dark:text-gray-300 px-2 py-1 bg-gray-100 dark:bg-gray-600">${tipo}</h6>
                <div class="space-y-1">`;
            
            serviciosPorTipo[tipo].forEach(servicio => {
                const isSelected = servicioSeleccionadoDropdown?.id === servicio.id;
                html += `
                    <div class="px-2 py-2 hover:bg-gray-100 dark:hover:bg-gray-600 cursor-pointer ${isSelected ? 'bg-blue-100 dark:bg-blue-900' : ''}"
                         onclick="selectServicioFromDropdown('${servicio.id}')">
                        <div class="text-sm font-medium text-gray-900 dark:text-white">${servicio.nombre}</div>
                    </div>`;
            });
            
            html += '</div></div>';
        });

        dropdownContent.innerHTML = html;
    }

    /**
     * Muestra el dropdown de servicios
     */
    window.showServicioDropdown = function () {
        const dropdown = document.getElementById('servicio-dropdown');
        const searchInput = document.getElementById('servicio-search');
        
        if (serviciosDisponibles.length > 0) {
            renderServiciosDropdown(serviciosDisponibles, searchInput.value);
            dropdown.classList.remove('hidden');
        }
    };

    /**
     * Filtra servicios en el dropdown según el texto de búsqueda
     */
    window.filterServiciosDropdown = function (searchText) {
        renderServiciosDropdown(serviciosDisponibles, searchText);
        const dropdown = document.getElementById('servicio-dropdown');
        if (!dropdown.classList.contains('hidden')) {
            // El dropdown ya está visible, no hacer nada
        } else {
            dropdown.classList.remove('hidden');
        }
    };

    /**
     * Selecciona un servicio del dropdown
     */
    window.selectServicioFromDropdown = function (servicioId) {
        const servicio = serviciosDisponibles.find(s => s.id === servicioId);
        if (servicio) {
            servicioSeleccionadoDropdown = servicio;
            
            // Actualizar visualmente la selección
            renderServiciosDropdown(serviciosDisponibles, document.getElementById('servicio-search').value);
            
            // Actualizar el input de búsqueda con el nombre del servicio
            document.getElementById('servicio-search').value = servicio.nombre;
        }
    };

    /**
     * Agrega el servicio seleccionado a la lista
     */
    window.agregarServicioSeleccionado = function () {
        if (!servicioSeleccionadoDropdown) {
            showPaqueteMessage('Debe seleccionar un servicio del listado', 'error');
            return;
        }

        const tipo = servicioSeleccionadoDropdown.tipo;
        
        // Verificar si ya hay un servicio de este tipo
        const tipoYaSeleccionado = serviciosSeleccionados.some(s => s.tipo === tipo);
        if (tipoYaSeleccionado) {
            showPaqueteMessage('Solo puede seleccionar un servicio de cada tipo', 'error');
            return;
        }

        // Agregar servicio
        serviciosSeleccionados.push({
            id: servicioSeleccionadoDropdown.id,
            nombre: servicioSeleccionadoDropdown.nombre,
            tipo: servicioSeleccionadoDropdown.tipo,
            precio: servicioSeleccionadoDropdown.precio,
            tiempoEstimado: servicioSeleccionadoDropdown.tiempoEstimado
        });

        // Limpiar selección
        servicioSeleccionadoDropdown = null;
        document.getElementById('servicio-search').value = '';
        document.getElementById('servicio-dropdown').classList.add('hidden');

        // Actualizar UI
        updateServiciosSeleccionadosList();
        updateResumen();
        renderServiciosDropdown(serviciosDisponibles, '');
    };

    /**
     * Remueve un servicio de la lista de seleccionados
     */
    window.removerServicioSeleccionado = function (servicioId) {
        serviciosSeleccionados = serviciosSeleccionados.filter(s => s.id !== servicioId);
        updateServiciosSeleccionadosList();
        updateResumen();
        renderServiciosDropdown(serviciosDisponibles, document.getElementById('servicio-search')?.value || '');
    };

    /**
     * Actualiza la lista de servicios seleccionados
     */
    function updateServiciosSeleccionadosList() {
        const container = document.getElementById('servicios-seleccionados-container');
        const list = document.getElementById('servicios-seleccionados-list');

        if (serviciosSeleccionados.length === 0) {
            container.classList.add('hidden');
            return;
        }

        container.classList.remove('hidden');

        let html = '<ul class="space-y-2">';
        serviciosSeleccionados.forEach(servicio => {
            html += `
                <li class="flex justify-between items-center p-2 bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-600">
                    <div>
                        <span class="font-medium text-gray-900 dark:text-white">${servicio.nombre}</span>
                        <span class="text-sm text-gray-500 dark:text-gray-400 ml-2">(${servicio.tipo})</span>
                    </div>
                    <div class="flex items-center gap-3">
                        <div class="text-right">
                            <div class="text-sm font-medium text-gray-900 dark:text-white">$${servicio.precio.toFixed(2)}</div>
                            <div class="text-xs text-gray-500 dark:text-gray-400">${servicio.tiempoEstimado} min</div>
                        </div>
                        <button type="button" 
                                onclick="removerServicioSeleccionado('${servicio.id}')"
                                class="p-1 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300"
                                title="Quitar servicio">
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="w-5 h-5">
                                <path fill-rule="evenodd" d="M12 2.25c-5.385 0-9.75 4.365-9.75 9.75s4.365 9.75 9.75 9.75 9.75-4.365 9.75-9.75S17.385 2.25 12 2.25Zm-1.72 6.97a.75.75 0 1 0-1.06 1.06L10.94 12l-1.72 1.72a.75.75 0 1 0 1.06 1.06L12 13.06l1.72 1.72a.75.75 0 1 0 1.06-1.06L13.06 12l1.72-1.72a.75.75 0 1 0-1.06-1.06L12 10.94l-1.72-1.72Z" clip-rule="evenodd" />
                            </svg>
                        </button>
                    </div>
                </li>`;
        });
        html += '</ul>';

        list.innerHTML = html;
    }

    /**
     * Calcula precio y tiempo
     */
    window.calcularPrecioYTiempo = function () {
        updateResumen();
    };

    /**
     * Actualiza el resumen del paquete
     */
    function updateResumen() {
        const descuento = parseFloat(document.getElementById('PorcentajeDescuento')?.value || 0);

        const precioTotal = serviciosSeleccionados.reduce((sum, s) => sum + s.precio, 0);
        const descuentoMonto = precioTotal * (descuento / 100);
        const precioFinal = precioTotal - descuentoMonto;
        const tiempoTotal = serviciosSeleccionados.reduce((sum, s) => sum + s.tiempoEstimado, 0);

        document.getElementById('precio-total-sin-descuento').textContent = '$' + precioTotal.toFixed(2);
        document.getElementById('precio-final').textContent = '$' + precioFinal.toFixed(2);
        document.getElementById('tiempo-total').textContent = tiempoTotal + ' min';

        document.getElementById('Precio').value = precioFinal.toFixed(2);
        document.getElementById('TiempoEstimado').value = tiempoTotal;
    }

    // =====================================
    // FORMULARIO AJAX
    // =====================================
    /**
     * Envía el formulario vía AJAX
     */
    window.submitPaqueteAjax = function (form, event) {
        if (event) {
            event.preventDefault();
        }

        // Validar servicios seleccionados
        if (serviciosSeleccionados.length < 2) {
            showPaqueteMessage('Debe seleccionar al menos 2 servicios para crear un paquete', 'error');
            document.getElementById('servicios-error')?.classList.remove('hidden');
            return false;
        }

        document.getElementById('servicios-error')?.classList.add('hidden');

        // Preparar IDs de servicios
        const serviciosIds = serviciosSeleccionados.map(s => s.id);
        document.getElementById('ServiciosIdsJson').value = JSON.stringify(serviciosIds);

        const formData = new FormData(form);

        fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        })
            .then(response => {
                const isValid = response.headers.get('X-Form-Valid') === 'true';
                const message = response.headers.get('X-Form-Message');

                return response.text().then(html => ({
                    html: html,
                    isValid: isValid,
                    message: message
                }));
            })
            .then(data => {
                if (data.isValid) {
                    showPaqueteMessage(data.message || 'Operación exitosa', 'success');
                    limpiarFormularioPaquete();
                    reloadPaqueteTable(1);

                    // Cerrar acordeón después de 2 segundos
                    setTimeout(() => {
                        const accordion = document.getElementById('accordion-flush-body-1');
                        if (accordion) accordion.classList.add('hidden');
                    }, 2000);
                } else {
                    // Reemplazar formulario con errores
                    document.getElementById('paquete-form-container').innerHTML = data.html;
                    initializeForm();
                }
            })
            .catch(error => {
                console.error('Error:', error);
                showPaqueteMessage('Error al procesar la solicitud', 'error');
            });

        return false;
    };

    /**
     * Limpia el formulario
     */
    window.limpiarFormularioPaquete = function () {
        const form = document.getElementById('paquete-form');
        if (!form) return;

        const isEdit = form.dataset.edit === 'True';

        if (isEdit) {
            window.location.href = '/PaqueteServicio/Index';
        } else {
            form.reset();
            serviciosSeleccionados = [];
            serviciosDisponibles = [];
            servicioSeleccionadoDropdown = null;
            updateServiciosSeleccionadosList();
            updateResumen();
            document.getElementById('servicio-selector-container')?.classList.add('hidden');
            document.getElementById('servicio-search').value = '';
            document.getElementById('servicio-dropdown')?.classList.add('hidden');
            hidePaqueteMessage();
        }
    };

    /**
     * Edita un paquete
     */
    window.editPaquete = function (id) {
        window.location.href = `/PaqueteServicio/Index?editId=${id}`;
    };

    // =====================================
    // MODAL DE CONFIRMACIÓN
    // =====================================
    /**
     * Abre modal de confirmación para paquetes
     */
    window.openPaqueteConfirmModal = function (tipoAccion, id, nombre) {
        const modal = document.getElementById('paqueteConfirmModal');
        const title = document.getElementById('paqueteConfirmTitle');
        const msg = document.getElementById('paqueteConfirmMessage');
        const submitBtn = document.getElementById('paqueteConfirmSubmit');
        const form = document.getElementById('paqueteConfirmForm');
        const idInput = document.getElementById('paqueteConfirmId');
        const iconWrapper = document.getElementById('paqueteConfirmIconWrapper');
        const icon = document.getElementById('paqueteConfirmIcon');

        idInput.value = id;

        if (tipoAccion === 'desactivar') {
            title.textContent = 'Desactivar Paquete';
            msg.textContent = `¿Está seguro que desea desactivar el paquete "${nombre}"?`;
            form.action = '/PaqueteServicio/DeactivatePaquete';
            submitBtn.textContent = 'Sí, desactivar';
            submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-red-600 rounded-lg hover:bg-red-700 focus:ring-4 focus:outline-none focus:ring-red-300 dark:bg-red-500 dark:hover:bg-red-600 dark:focus:ring-red-900';
            iconWrapper.className = 'w-12 h-12 rounded-full bg-red-100 dark:bg-red-900 p-2 flex items-center justify-center mx-auto mb-3.5';
            icon.className = 'w-8 h-8 text-red-500 dark:text-red-400';
        } else {
            title.textContent = 'Reactivar Paquete';
            msg.textContent = `¿Está seguro que desea reactivar el paquete "${nombre}"?`;
            form.action = '/PaqueteServicio/ReactivatePaquete';
            submitBtn.textContent = 'Sí, reactivar';
            submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-green-600 rounded-lg hover:bg-green-700 focus:ring-4 focus:outline-none focus:ring-green-300 dark:bg-green-500 dark:hover:bg-green-600 dark:focus:ring-green-900';
            iconWrapper.className = 'w-12 h-12 rounded-full bg-green-100 dark:bg-green-900 p-2 flex items-center justify-center mx-auto mb-3.5';
            icon.className = 'w-8 h-8 text-green-500 dark:text-green-400';
        }

        modal.classList.remove('hidden');
    };

    /**
     * Cierra modal de confirmación
     */
    window.closePaqueteConfirmModal = function () {
        const modal = document.getElementById('paqueteConfirmModal');
        modal.classList.add('hidden');
    };

    /**
     * Envía cambio de estado de paquete
     */
    window.submitPaqueteEstado = function (form) {
        const formData = new FormData(form);

        fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(r => {
                if (!r.ok) throw new Error('Error estado');
                window.closePaqueteConfirmModal();

                const isDeactivate = form.action.includes('DeactivatePaquete');
                const message = isDeactivate ? 'Paquete desactivado correctamente.' : 'Paquete reactivado correctamente.';

                showTableMessage(message, 'success');
                reloadPaqueteTable(1);
            })
            .catch(e => {
                showTableMessage('Error procesando la operación.', 'error');
            });

        return false;
    };

    // =====================================
    // FILTROS
    // =====================================
    /**
     * Limpia todos los filtros
     */
    window.clearAllFilters = function () {
        const filterForm = document.getElementById('filterForm');
        if (filterForm) {
            const checkboxes = filterForm.querySelectorAll('input[type="checkbox"]');
            checkboxes.forEach(cb => cb.checked = false);
        }
    };

    // =====================================
    // PAGINACIÓN Y ORDENAMIENTO
    // =====================================
    /**
     * Cambia de página
     */
    window.changePage = function (pageNumber) {
        if (currentSearchTerm) {
            const estados = getSelectedCheckboxValues('estados');
            const tiposVehiculo = getSelectedCheckboxValues('tiposVehiculo');
            const sortBy = document.getElementById('current-sort-by')?.value || 'Nombre';
            const sortOrder = document.getElementById('current-sort-order')?.value || 'asc';

            const params = new URLSearchParams();
            params.append('searchTerm', currentSearchTerm);
            params.append('pageNumber', pageNumber);
            params.append('pageSize', '10');
            params.append('sortBy', sortBy);
            params.append('sortOrder', sortOrder);

            estados.forEach(e => params.append('estados', e));
            tiposVehiculo.forEach(tv => params.append('tiposVehiculo', tv));

            const url = '/PaqueteServicio/SearchPartial?' + params.toString();
            loadTablePartial(url);
        } else {
            reloadPaqueteTable(pageNumber);
        }
    };

    /**
     * Ordena la tabla
     */
    window.sortTable = function (sortBy) {
        const currentSortBy = document.getElementById('current-sort-by')?.value;
        const currentSortOrder = document.getElementById('current-sort-order')?.value || 'asc';

        let newSortOrder = 'asc';
        if (currentSortBy === sortBy) {
            newSortOrder = currentSortOrder === 'asc' ? 'desc' : 'asc';
        }

        const estados = getSelectedCheckboxValues('estados');
        const tiposVehiculo = getSelectedCheckboxValues('tiposVehiculo');

        const params = new URLSearchParams();
        params.append('pageNumber', '1');
        params.append('pageSize', '10');
        params.append('sortBy', sortBy);
        params.append('sortOrder', newSortOrder);

        estados.forEach(e => params.append('estados', e));
        tiposVehiculo.forEach(tv => params.append('tiposVehiculo', tv));

        if (currentSearchTerm) {
            params.append('searchTerm', currentSearchTerm);
            const url = '/PaqueteServicio/SearchPartial?' + params.toString();
            loadTablePartial(url);
        } else {
            const url = '/PaqueteServicio/TablePartial?' + params.toString();
            loadTablePartial(url);
        }
    };

    // =====================================
    // MENSAJES
    // =====================================
    /**
     * Muestra mensaje del formulario
     */
    function showPaqueteMessage(message, type) {
        const container = document.getElementById('form-message-container');
        const text = document.getElementById('form-message-text');

        if (!container || !text) return;

        text.textContent = message;
        container.className = `m-4 p-4 mb-4 text-sm rounded-lg ${type === 'success' ? 'text-green-800 bg-green-50 dark:bg-gray-800 dark:text-green-400' : 'text-red-800 bg-red-50 dark:bg-gray-800 dark:text-red-400'}`;
        container.classList.remove('hidden');

        if (paqueteMsgTimeout) clearTimeout(paqueteMsgTimeout);
        paqueteMsgTimeout = setTimeout(() => {
            container.classList.add('hidden');
        }, 5000);
    }

    /**
     * Oculta mensaje del formulario
     */
    function hidePaqueteMessage() {
        const container = document.getElementById('form-message-container');
        if (container) container.classList.add('hidden');
    }

    /**
     * Muestra mensaje de tabla
     */
    function showTableMessage(message, type) {
        const container = document.getElementById('table-message-container');
        const text = document.getElementById('table-message-text');

        if (!container || !text) return;

        text.textContent = message;
        container.className = `p-4 mb-4 mx-4 text-sm rounded-lg ${type === 'success' ? 'text-green-800 bg-green-50 dark:bg-gray-800 dark:text-green-400' : 'text-red-800 bg-red-50 dark:bg-gray-800 dark:text-red-400'}`;
        container.classList.remove('hidden');

        if (tableMsgTimeout) clearTimeout(tableMsgTimeout);
        tableMsgTimeout = setTimeout(() => {
            container.classList.add('hidden');
        }, 5000);
    }

})();
