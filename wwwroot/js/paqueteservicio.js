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
                    // Marcar servicios seleccionados
                    window.paqueteEditData.serviciosIds.forEach(id => {
                        const checkbox = document.querySelector(`input[name="servicio-${id}"]`);
                        if (checkbox) {
                            checkbox.checked = true;
                            onServicioCheckboxChange({ target: checkbox });
                        }
                    });
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
        const container = document.getElementById('servicios-checkboxes');

        if (!tipoVehiculo) {
            container.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400">Seleccione un tipo de vehículo</p>';
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
                renderServiciosCheckboxes();
            } else {
                container.innerHTML = `<p class="text-sm text-red-500">${data.message}</p>`;
            }
        } catch (error) {
            console.error('Error cargando servicios:', error);
            container.innerHTML = '<p class="text-sm text-red-500">Error al cargar servicios</p>';
        }
    };

    /**
     * Renderiza los checkboxes de servicios
     */
    function renderServiciosCheckboxes() {
        const container = document.getElementById('servicios-checkboxes');

        if (!serviciosDisponibles || serviciosDisponibles.length === 0) {
            container.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400">No hay servicios disponibles</p>';
            return;
        }

        // Agrupar por tipo
        const serviciosPorTipo = {};
        serviciosDisponibles.forEach(s => {
            if (!serviciosPorTipo[s.tipo]) {
                serviciosPorTipo[s.tipo] = [];
            }
            serviciosPorTipo[s.tipo].push(s);
        });

        let html = '';
        Object.keys(serviciosPorTipo).forEach(tipo => {
            html += `<div class="col-span-2 mb-2">
                <h4 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">${tipo}</h4>
                <div class="space-y-1">`;

            serviciosPorTipo[tipo].forEach(servicio => {
                const isSelected = serviciosSeleccionados.some(s => s.id === servicio.id);
                html += `
                    <label class="flex items-center p-2 rounded hover:bg-gray-100 dark:hover:bg-gray-600 cursor-pointer">
                        <input type="checkbox" name="servicio-${servicio.id}" 
                               value="${servicio.id}" 
                               data-tipo="${servicio.tipo}"
                               data-precio="${servicio.precio}"
                               data-tiempo="${servicio.tiempoEstimado}"
                               data-nombre="${servicio.nombre}"
                               ${isSelected ? 'checked' : ''}
                               onchange="onServicioCheckboxChange(event)"
                               class="w-4 h-4 text-blue-600 bg-gray-100 border-gray-300 rounded focus:ring-blue-500 dark:focus:ring-blue-600 dark:ring-offset-gray-700 dark:focus:ring-offset-gray-700 focus:ring-2 dark:bg-gray-600 dark:border-gray-500">
                        <span class="ml-2 text-sm text-gray-900 dark:text-gray-100">
                            ${servicio.nombre} - $${servicio.precio.toFixed(2)} - ${servicio.tiempoEstimado} min
                        </span>
                    </label>`;
            });

            html += '</div></div>';
        });

        container.innerHTML = html;
    }

    /**
     * Maneja el cambio de checkbox de servicio
     */
    window.onServicioCheckboxChange = function (event) {
        const checkbox = event.target;
        const servicioId = checkbox.value;
        const tipo = checkbox.dataset.tipo;

        if (checkbox.checked) {
            // Verificar si ya hay un servicio de este tipo
            const tipoYaSeleccionado = serviciosSeleccionados.some(s => s.tipo === tipo);
            if (tipoYaSeleccionado) {
                checkbox.checked = false;
                showPaqueteMessage('Solo puede seleccionar un servicio de cada tipo', 'error');
                return;
            }

            // Agregar servicio
            serviciosSeleccionados.push({
                id: servicioId,
                nombre: checkbox.dataset.nombre,
                tipo: tipo,
                precio: parseFloat(checkbox.dataset.precio),
                tiempoEstimado: parseInt(checkbox.dataset.tiempo)
            });
        } else {
            // Remover servicio
            serviciosSeleccionados = serviciosSeleccionados.filter(s => s.id !== servicioId);
        }

        updateServiciosSeleccionadosList();
        updateResumen();
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
                    <div class="text-right">
                        <div class="text-sm font-medium text-gray-900 dark:text-white">$${servicio.precio.toFixed(2)}</div>
                        <div class="text-xs text-gray-500 dark:text-gray-400">${servicio.tiempoEstimado} min</div>
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
            updateServiciosSeleccionadosList();
            updateResumen();
            document.getElementById('servicios-checkboxes').innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400">Seleccione un tipo de vehículo</p>';
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
