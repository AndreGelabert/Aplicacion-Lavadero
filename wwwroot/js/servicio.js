/**
 * ================================================
 * SERVICIO.JS - FUNCIONALIDAD DE LA PÁGINA DE SERVICIOS
 * ================================================
 */

(function () {
    'use strict';

    let servicioMsgTimeout = null;
    let tableMsgTimeout = null;
    let searchTimeout = null;
    let currentSearchTerm = '';

    // =====================================
    // INICIALIZACIÓN DEL MÓDULO
    // =====================================
    window.PageModules = window.PageModules || {};
    window.PageModules.servicios = {
        init: initializeServiciosPage
    };

    /**
     * Inicializa la funcionalidad específica de la página de Servicios
     */
    function initializeServiciosPage() {
        setupFormValidation();
        setupModals();
        setupAccordion();
        setupDescriptionAutoGrow();
        setupFormMessageHandler();
        setupSearchWithDebounce();
        window.CommonUtils?.setupDefaultFilterForm();
        checkEditMode(); // Verificar si estamos en modo edición
    }

    // IMPORTANTE: asegurar que init se ejecute siempre
    // Evita que los formularios hagan submit tradicional (recarga),
    // engancha eventos de los modales (abrir/cerrar) y AJAX de crear/eliminar tipos.
    document.addEventListener('DOMContentLoaded', () => {
        try {
            window.PageModules?.servicios?.init();
        } catch (e) {
            // fallback por si se renombra el módulo
            initializeServiciosPage();
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
     * Configura autoGrow para el campo descripción
     */
    function setupDescriptionAutoGrow() {
        const descripcionField = document.getElementById('Descripcion');
        if (descripcionField) {
            window.SiteModule?.autoGrow(descripcionField);
        }
    }

    /**
     * Configura el manejo de mensajes del formulario
     */
    function setupFormMessageHandler() {
        document.addEventListener('input', (e) => {
            if (e.target.closest('#servicio-form')) {
                hideServicioMessage();
            }
        });
    }

    // =====================================
    // ACORDEONES
    // =====================================
    /**
     * Configura el comportamiento de acordeones
     */
    function setupAccordion() {
        const hayErrores = document.getElementById('error-alert') ||
            document.getElementById('success-alert');
        const servicioFormErrors = document.getElementById('servicio-form')?.querySelector('.validation-message');
        const formularioEdicion = document.getElementById('form-title')?.textContent.includes('Editando');

        if (hayErrores || servicioFormErrors || formularioEdicion) {
            openAccordion();
        }
    }

    /**
     * Abre el acordeón del formulario
     */
    function openAccordion() {
        const accordion = document.getElementById('accordion-flush-body-1');
        if (accordion && accordion.classList.contains('hidden')) {
            accordion.classList.remove('hidden');

            const accordionButton = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
            if (accordionButton) {
                accordionButton.setAttribute('aria-expanded', 'true');
                const icon = accordionButton.querySelector('[data-accordion-icon]');
                if (icon) icon.classList.add('rotate-180');
            }
        }
    }

    // =====================================
    // BUSQUEDA DE LA TABLA
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

        // Inicializar estado con el valor actual del input (si vino del servidor)
        currentSearchTerm = newSearchInput.value?.trim() || '';

        newSearchInput.addEventListener('input', function () {
            const searchTerm = this.value.trim();

            if (searchTimeout) clearTimeout(searchTimeout);

            if (searchTerm === '') {
                // limpiar estado y volver a la tabla base
                currentSearchTerm = '';
                reloadServicioTable(1);
                return;
            }

            // Debouncing
            searchTimeout = setTimeout(() => {
                performServerSearch(searchTerm);
            }, 500);
        });
    }

    /**
     * Realiza búsqueda en el servidor
     */
    function performServerSearch(searchTerm) {
        // Persistir búsqueda activa
        currentSearchTerm = searchTerm;

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

        const url = `/Servicio/SearchPartial?${params.toString()}`;

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const cont = document.getElementById('servicio-table-container');
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
    // ORDENAMIENTO DE TABLA
    // =====================================
    /**
     * Maneja el ordenamiento de la tabla
     * @param {string} sortBy - Campo por el cual ordenar
     */
    function sortTable(sortBy) {
        // Obtener valores actuales
        const currentSortBy = document.getElementById('current-sort-by')?.value || 'Nombre';
        const currentSortOrder = document.getElementById('current-sort-order')?.value || 'asc';

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
        reloadServicioTable(1);
    }

    /**
     * Obtiene los parámetros de ordenamiento actuales
     */
    function getCurrentSort() {
        const sortByInput = document.getElementById('current-sort-by');
        const sortOrderInput = document.getElementById('current-sort-order');

        return {
            sortBy: sortByInput?.value || 'Nombre',
            sortOrder: sortOrderInput?.value || 'asc'
        };
    }

    // =====================================
    // RECARGA DE TABLA CON ORDENAMIENTO
    // =====================================
    /**
     * Recarga la tabla de servicios con filtros y ordenamiento actuales
     * @param {number} page - Número de página
     */
    function reloadServicioTable(page) {
        // Obtener filtros actuales del formulario
        const filterForm = document.getElementById('filterForm');
        const params = new URLSearchParams();

        if (filterForm) {
            const formData = new FormData(filterForm);
            for (const [key, value] of formData.entries()) {
                params.append(key, value);
            }
        }

        // Orden actual
        const currentSort = getCurrentSort();
        params.set('pageNumber', page.toString());
        params.set('sortBy', currentSort.sortBy);
        params.set('sortOrder', currentSort.sortOrder);

        // Si hay búsqueda activa, mantener paginación y ordenamiento dentro del contexto de búsqueda
        let url;
        if (currentSearchTerm && currentSearchTerm.trim().length > 0) {
            params.set('searchTerm', currentSearchTerm.trim());
            url = `/Servicio/SearchPartial?${params.toString()}`;
        } else {
            url = `/Servicio/TablePartial?${params.toString()}`;
        }

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                const cont = document.getElementById('servicio-table-container');
                cont.innerHTML = html;
                const cp = document.getElementById('current-page-value')?.value;
                if (cp) cont.dataset.currentPage = cp;
            })
            .catch(e => {
                showTableMessage('error', 'Error cargando la tabla.');
            });
    }

    /**
     * Obtiene la página actual de la tabla
     */
    function getCurrentTablePage() {
        return parseInt(document.getElementById('servicio-table-container')?.dataset.currentPage || '1');
    }

    // =====================================
    // VALIDACIÓN DE FORMULARIOS
    // =====================================
    /**
     * Configura la validación del formulario de servicios
     */
    function setupFormValidation() {
        const servicioForm = document.getElementById('servicio-form');
        if (!servicioForm) return;

        setupFieldValidation();

        if (!servicioForm.hasAttribute('data-validation-setup')) {
            servicioForm.addEventListener('submit', validateFormOnSubmit);
            servicioForm.setAttribute('data-validation-setup', 'true');
        }
    }

    /**
     * Configura validación de campos individuales
     */
    function setupFieldValidation() {
        setupNombreValidation();
        setupPrecioValidation();
        setupTiempoEstimadoValidation();
    }

    /**
     * Configura validación del campo Nombre
     */
    function setupNombreValidation() {
        const nombreInput = document.getElementById('Nombre');
        if (!nombreInput) return;

        nombreInput.addEventListener('input', function () {
            const regex = /^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]*$/;
            if (!regex.test(this.value)) {
                this.classList.add('border-red-500');
                this.value = this.value.replace(/[^a-zA-ZáéíóúÁÉÍÓÚñÑ\s]/g, '');
            } else {
                this.classList.remove('border-red-500');
            }
        });
    }

    /**
     * Configura validación del campo Precio
     */
    function setupPrecioValidation() {
        const precioInput = document.getElementById('Precio');
        if (!precioInput) return;

        precioInput.addEventListener('input', function () {
            const value = parseFloat(this.value);
            if (isNaN(value) || value < 0) {
                this.classList.add('border-red-500');
                if (value < 0) {
                    this.value = Math.abs(value);
                }
            } else {
                this.classList.remove('border-red-500');
            }
        });

        precioInput.addEventListener('blur', function () {
            const value = parseFloat(this.value);
            if (isNaN(value) || value < 0) {
                this.value = '0';
                this.classList.remove('border-red-500');
            }
        });
    }

    /**
     * Configura validación del campo Tiempo Estimado
     */
    function setupTiempoEstimadoValidation() {
        const tiempoEstimadoInput = document.getElementById('TiempoEstimado');
        if (!tiempoEstimadoInput) return;

        tiempoEstimadoInput.addEventListener('input', function () {
            const value = parseInt(this.value);
            if (isNaN(value) || value <= 0) {
                this.classList.add('border-red-500');
                if (value <= 0) {
                    this.value = 1;
                    this.classList.remove('border-red-500');
                }
            } else {
                this.classList.remove('border-red-500');
            }
        });

        tiempoEstimadoInput.addEventListener('blur', function () {
            const value = parseInt(this.value);
            if (isNaN(value) || value <= 0) {
                this.value = '1';
                this.classList.remove('border-red-500');
            }
        });
    }

    /**
     * Valida el formulario al enviarlo
     */
    function validateFormOnSubmit(e) {
        const servicioForm = this;
        const requiredFields = servicioForm.querySelectorAll('[required]');
        let allValid = true;

        // Verificar campos requeridos
        requiredFields.forEach(field => {
            if (!field.value.trim()) {
                allValid = false;
                field.classList.add('border-red-500');
            } else {
                field.classList.remove('border-red-500');
            }
        });

        if (!allValid) {
            e.preventDefault();
            showFormError("Por favor, complete todos los campos obligatorios.");
            return;
        }

        // Validaciones específicas
        const validationErrors = performSpecificValidations();
        if (validationErrors.length > 0) {
            e.preventDefault();
            showFormError(validationErrors.join('<br>'));
        }
    }

    /**
     * Realiza validaciones específicas de los campos
     */
    function performSpecificValidations() {
        const errors = [];
        const nombreInput = document.getElementById('Nombre');
        const precioInput = document.getElementById('Precio');
        const tiempoEstimadoInput = document.getElementById('TiempoEstimado');

        // Validar nombre
        const nombreValue = nombreInput?.value.trim();
        const nombreRegex = /^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$/;
        if (nombreValue && (!nombreRegex.test(nombreValue) || nombreValue === '')) {
            nombreInput.classList.add('border-red-500');
            errors.push("El nombre solo puede contener letras y espacios");
        } else if (nombreInput) {
            nombreInput.classList.remove('border-red-500');
        }

        // Validar precio
        const precioValue = parseFloat(precioInput?.value);
        if (precioInput && (isNaN(precioValue) || precioValue < 0)) {
            precioInput.classList.add('border-red-500');
            errors.push("El precio debe ser igual o mayor a 0");
        } else if (precioInput) {
            precioInput.classList.remove('border-red-500');
        }

        // Validar tiempo estimado
        const tiempoEstimadoValue = parseInt(tiempoEstimadoInput?.value);
        if (tiempoEstimadoInput && (isNaN(tiempoEstimadoValue) || tiempoEstimadoValue <= 0)) {
            tiempoEstimadoInput.classList.add('border-red-500');
            errors.push("El tiempo estimado debe ser mayor a 0");
        } else if (tiempoEstimadoInput) {
            tiempoEstimadoInput.classList.remove('border-red-500');
        }

        return errors;
    }

    /**
     * Muestra error en el formulario
     */
    function showFormError(message) {
        openAccordion();

        let errorDiv = document.getElementById('form-error-message');
        if (!errorDiv) {
            const servicioForm = document.getElementById('servicio-form');
            errorDiv = document.createElement('div');
            errorDiv.id = 'form-error-message';
            errorDiv.className = 'p-4 mb-4 text-sm text-red-800 rounded-lg bg-red-50 dark:bg-gray-800 dark:text-red-400';
            servicioForm.appendChild(errorDiv);
        }

        errorDiv.innerHTML = '<span class="font-medium">¡Error!</span> ' + message;
    }

    // =====================================
    // GESTIÓN DE MODALES
    // =====================================
    /**
     * Configura todos los modales de la página
     */
    function setupModals() {
        setupServiceTypeModals();
        setupVehicleTypeModals();

        // Inicializar Flowbite modals para compatibilidad cross-browser
        if (typeof initModals === 'function') {
            initModals();
        }

        // Asegurar cierre robusto en X y Cancelar
        addModalCloseHandlers('defaultModal');
        addModalCloseHandlers('eliminarTipoModal');
        addModalCloseHandlers('tipoVehiculoModal');
        addModalCloseHandlers('eliminarTipoVehiculoModal');
    }

    function addModalCloseHandlers(modalId) {
        const closeBtns = document.querySelectorAll(`[data-modal-hide="${modalId}"]`);
        closeBtns.forEach(btn => {
            if (btn.hasAttribute('data-close-setup')) return;
            btn.addEventListener('click', (e) => {
                // Asegura cierre incluso si Flowbite no reacciona
                e.preventDefault();
                e.stopPropagation();
                cerrarModal(modalId);
            });
            btn.setAttribute('data-close-setup', 'true');
        });
    }

    /**
     * Configura modales de tipos de servicio
     */
    function setupServiceTypeModals() {
        // Configurar formulario de crear tipo de servicio
        const formCrearTipo = document.getElementById('formCrearTipoServicio');
        if (formCrearTipo && !formCrearTipo.hasAttribute('data-setup')) {
            formCrearTipo.addEventListener('submit', async function (e) {
                e.preventDefault();
                e.stopPropagation();
                await handleCrearTipoServicio(this);
            });
            formCrearTipo.setAttribute('data-setup', 'true');
        }

        // Configurar formulario de eliminar tipo de servicio
        const formEliminarTipo = document.getElementById('formEliminarTipo');
        if (formEliminarTipo && !formEliminarTipo.hasAttribute('data-setup')) {
            formEliminarTipo.addEventListener('submit', async function (e) {
                e.preventDefault();
                e.stopPropagation();
                await handleEliminarTipoServicio(this);
            });
            formEliminarTipo.setAttribute('data-setup', 'true');
        }

        // Configurar botones que abren modal de eliminar (para llenar el input hidden)
        const btnsEliminarTipo = document.querySelectorAll('[data-modal-toggle="eliminarTipoModal"]');
        btnsEliminarTipo.forEach(btn => {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();

                const tipoSeleccionado = document.getElementById('Tipo')?.value;
                const eliminInput = document.getElementById('nombreTipoEliminar');
                if (eliminInput) eliminInput.value = tipoSeleccionado;

                abrirModal('eliminarTipoModal');
            });
        });

        // Configurar botones de abrir modal de crear
        const btnsCrearTipo = document.querySelectorAll('[data-modal-toggle="defaultModal"]');
        btnsCrearTipo.forEach(btn => {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();

                abrirModal('defaultModal');
            });
        });
    }

    /**
     * Configura modales de tipos de vehículo
     */
    function setupVehicleTypeModals() {
        // Configurar formulario de crear tipo de vehículo
        const formCrearTipoVehiculo = document.getElementById('formCrearTipoVehiculo');
        if (formCrearTipoVehiculo && !formCrearTipoVehiculo.hasAttribute('data-setup')) {
            formCrearTipoVehiculo.addEventListener('submit', async function (e) {
                e.preventDefault();
                e.stopPropagation();
                await handleCrearTipoVehiculo(this);
            });
            formCrearTipoVehiculo.setAttribute('data-setup', 'true');
        }

        // Configurar formulario de eliminar tipo de vehículo
        const formEliminarTipoVehiculo = document.getElementById('formEliminarTipoVehiculo');
        if (formEliminarTipoVehiculo && !formEliminarTipoVehiculo.hasAttribute('data-setup')) {
            formEliminarTipoVehiculo.addEventListener('submit', async function (e) {
                e.preventDefault();
                e.stopPropagation();
                await handleEliminarTipoVehiculo(this);
            });
            formEliminarTipoVehiculo.setAttribute('data-setup', 'true');
        }

        // Configurar botones que abren modal de eliminar vehículo
        const btnsEliminarTipoVehiculo = document.querySelectorAll('[data-modal-toggle="eliminarTipoVehiculoModal"]');
        btnsEliminarTipoVehiculo.forEach(btn => {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();

                const tipoSeleccionado = document.getElementById('TipoVehiculo')?.value;
                const eliminInput = document.getElementById('nombreTipoVehiculoEliminar');
                if (eliminInput) eliminInput.value = tipoSeleccionado;

                abrirModal('eliminarTipoVehiculoModal');
            });
        });

        // Configurar botones de abrir modal de crear vehículo
        const btnsCrearTipoVehiculo = document.querySelectorAll('[data-modal-toggle="tipoVehiculoModal"]');
        btnsCrearTipoVehiculo.forEach(btn => {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();

                abrirModal('tipoVehiculoModal');
            });
        });
    }

    /**
     * Maneja la creación de tipo de servicio vía AJAX
     */
    async function handleCrearTipoServicio(form) {
        const nombreTipo = document.getElementById('nombreTipo')?.value?.trim();
        if (!nombreTipo) {
            showTableMessage('error', 'El nombre del tipo de servicio es obligatorio.');
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
            const message = data?.message ?? (success ? 'Tipo de servicio creado.' : 'No se pudo crear el tipo de servicio.');

            if (success) {
                if (data?.tipos) actualizarDropdownTipos('Tipo', data.tipos, nombreTipo);
                cerrarModal('defaultModal');
                form.reset();
                showTableMessage('success', message);
            } else {
                showTableMessage('error', message);
            }
        } catch (error) {
            console.error('Error:', error);
            showTableMessage('error', 'Error al crear el tipo de servicio.');
        }
    }

    /**
     * Maneja la creación de tipo de vehículo vía AJAX
     */
    async function handleCrearTipoVehiculo(form) {
        const nombreTipo = document.getElementById('nombreTipoVehiculo')?.value?.trim();
        if (!nombreTipo) {
            showTableMessage('error', 'El nombre del tipo de vehículo es obligatorio.');
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
            const message = data?.message ?? (success ? 'Tipo de vehículo creado.' : 'No se pudo crear el tipo de vehículo.');

            if (success) {
                if (data?.tipos) actualizarDropdownTipos('TipoVehiculo', data.tipos, nombreTipo);
                cerrarModal('tipoVehiculoModal');
                form.reset();
                showTableMessage('success', message);
            } else {
                showTableMessage('error', message);
            }
        } catch (error) {
            console.error('Error:', error);
            showTableMessage('error', 'Error al crear el tipo de vehículo.');
        }
    }

    /**
     * Maneja la eliminación de tipo de servicio vía AJAX
     */
    async function handleEliminarTipoServicio(form) {
        const nombreTipo = document.getElementById('nombreTipoEliminar')?.value?.trim();
        if (!nombreTipo) {
            showTableMessage('error', 'Debe seleccionar un tipo de servicio.');
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
            const message = data?.message ?? (success ? 'Tipo de servicio eliminado.' : 'No se pudo eliminar el tipo de servicio.');

            if (success) {
                if (data?.tipos?.length) actualizarDropdownTipos('Tipo', data.tipos);
                cerrarModal('eliminarTipoModal');
                form.reset();
                showTableMessage('success', message);
            } else {
                cerrarModal('eliminarTipoModal');
                showTableMessage('error', message);
            }
        } catch (error) {
            console.error('Error:', error);
            cerrarModal('eliminarTipoModal');
            showTableMessage('error', 'Error al eliminar el tipo de servicio.');
        }
    }

    /**
     * Maneja la eliminación de tipo de vehículo vía AJAX
     */
    async function handleEliminarTipoVehiculo(form) {
        const nombreTipo = document.getElementById('nombreTipoVehiculoEliminar')?.value?.trim();
        if (!nombreTipo) {
            showTableMessage('error', 'Debe seleccionar un tipo de vehículo.');
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
            const message = data?.message ?? (success ? 'Tipo de vehículo eliminado.' : 'No se pudo eliminar el tipo de vehículo.');

            if (success) {
                if (data?.tipos?.length) actualizarDropdownTipos('TipoVehiculo', data.tipos);
                cerrarModal('eliminarTipoVehiculoModal');
                form.reset();
                showTableMessage('success', message);
            } else {
                cerrarModal('eliminarTipoVehiculoModal');
                showTableMessage('error', message);
            }
        } catch (error) {
            console.error('Error:', error);
            cerrarModal('eliminarTipoVehiculoModal');
            showTableMessage('error', 'Error al eliminar el tipo de vehículo.');
        }
    }

    /**
     * Actualiza un dropdown con nuevos tipos
     */
    function actualizarDropdownTipos(selectId, tipos, valorSeleccionado) {
        const select = document.getElementById(selectId);
        if (!select) return;

        // Limpiar opciones actuales
        select.innerHTML = '';

        // Agregar nuevas opciones
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

    // Helper: obtiene o crea la instancia Flowbite del modal
    function getFlowbiteModal(modalEl) {
        if (!modalEl || typeof window !== 'object' || typeof window.Modal === 'undefined') return null;

        const opts = { backdrop: 'dynamic', closable: true };

        // Flowbite 2.x expone getInstance y getOrCreateInstance
        if (typeof Modal.getInstance === 'function') {
            const existing = Modal.getInstance(modalEl);
            if (existing) return existing;
        }
        if (typeof Modal.getOrCreateInstance === 'function') {
            return Modal.getOrCreateInstance(modalEl, opts);
        }
        // Fallback
        try {
            return new Modal(modalEl, opts);
        } catch {
            return null;
        }
    }
    function abrirModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        try {
            const inst = getFlowbiteModal(modal);
            if (inst && typeof inst.show === 'function') {
                inst.show(); // Flowbite crea y gestiona su backdrop
                return;
            }
        } catch { /* no-op */ }

        // Fallback minimal
        modal.classList.remove('hidden');
        modal.setAttribute('aria-hidden', 'false');
    }
    /**
     * Cierra un modal garantizando que se quite el backdrop y se restaure el scroll
     */
    function cerrarModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        let closed = false;

        // 1) API de Flowbite
        try {
            const inst = getFlowbiteModal(modal);
            if (inst && typeof inst.hide === 'function') {
                inst.hide();
                closed = true;
            }
        } catch { /* no-op */ }

        // 2) Intentar cerrar haciendo click sobre el backdrop de Flowbite (si existe)
        try {
            const backdrop = document.querySelector('[modal-backdrop]');
            if (backdrop) {
                backdrop.click(); // Flowbite interpreta el click y oculta el modal
                closed = true;
            }
        } catch { /* no-op */ }

        // 3) Click sintético en cualquier botón con data-modal-hide="..."
        try {
            document.querySelectorAll(`[data-modal-hide="${modalId}"]`).forEach(btn => {
                btn.click();
                closed = true;
            });
        } catch { /* no-op */ }

        // 4) Fallback final: forzar hidden y limpiar overlays
        modal.classList.add('hidden');
        modal.setAttribute('aria-hidden', 'true');

        // Limpieza de overlays y scroll
        document.querySelectorAll('[modal-backdrop]').forEach(b => b.remove());
        document.body.classList.remove('overflow-hidden');
    }
    // 4) NUEVO: helper para parsear JSON de forma segura (evita "primer intento fallido" si el servidor no devuelve JSON)
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

    // =====================================
    // AJAX Y MENSAJES
    // =====================================
    /**
     * Construye HTML para alertas del servicio
     */
    function buildServicioAlert(type, msg) {
        const colorMap = {
            'success': { bg: 'green-50', text: 'green-800', darkText: 'green-400', border: 'green-300' },
            'info': { bg: 'blue-50', text: 'blue-800', darkText: 'blue-400', border: 'blue-300' },
            'error': { bg: 'red-50', text: 'red-800', darkText: 'red-400', border: 'red-300' }
        };

        const color = colorMap[type] || colorMap.error;

        return `<div id="servicio-inline-alert"
                    class="servicio-inline-alert opacity-100 transition-opacity duration-700
                           p-4 mb-4 text-sm rounded-lg border
                           bg-${color.bg} text-${color.text} border-${color.border}
                           dark:bg-gray-800 dark:text-${color.darkText}">
                    ${msg}
                </div>`;
    }

    /**
     * Muestra un mensaje en el formulario con auto-ocultado
     */
    function showServicioMessage(type, msg, disappearMs = 5000) {
        const container = document.getElementById('ajax-form-messages');
        if (!container) return;

        // Cancelar timeout previo
        if (servicioMsgTimeout) {
            clearTimeout(servicioMsgTimeout);
            servicioMsgTimeout = null;
        }

        container.innerHTML = buildServicioAlert(type, msg);
        const alertEl = document.getElementById('servicio-inline-alert');
        if (!alertEl) return;

        // Programar auto-ocultado
        servicioMsgTimeout = setTimeout(() => {
            alertEl.classList.add('opacity-0');
            setTimeout(() => {
                if (alertEl.parentElement) alertEl.remove();
            }, 750);
        }, disappearMs);
    }

    /**
     * Oculta el mensaje del servicio inmediatamente
     */
    function hideServicioMessage() {
        const alertEl = document.getElementById('servicio-inline-alert');
        if (alertEl) {
            if (servicioMsgTimeout) {
                clearTimeout(servicioMsgTimeout);
                servicioMsgTimeout = null;
            }
            alertEl.classList.add('opacity-0');
            setTimeout(() => {
                if (alertEl.parentElement) alertEl.remove();
            }, 400);
        }
    }

    /**
     * Muestra mensaje relacionado con la tabla
     */
    function showTableMessage(type, msg, disappearMs = 5000) {
        let container = document.getElementById('table-messages-container');

        if (!container) {
            container = document.createElement('div');
            container.id = 'table-messages-container';
            container.className = 'mb-4';

            const tableContainer = document.getElementById('servicio-table-container');
            tableContainer.parentNode.insertBefore(container, tableContainer);
        }

        // Si quedó oculto por un mensaje anterior, volver a mostrarlo
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

        // Asegurar visibilidad para el usuario
        try { alertEl.scrollIntoView({ behavior: 'smooth', block: 'start' }); } catch {}

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

    // =====================================
    // FUNCIONES AUXILIARES PRIVADAS
    // =====================================

    /**
     * Limpia modal de tipo de servicio
     */
    function limpiarModalTipoServicio() {
        const nombreTipo = document.getElementById('nombreTipo');
        if (nombreTipo) nombreTipo.value = '';
    }

    /**
     * Limpia modal de tipo de vehículo
     */
    function limpiarModalTipoVehiculo() {
        const nombreTipoVehiculo = document.getElementById('nombreTipoVehiculo');
        if (nombreTipoVehiculo) nombreTipoVehiculo.value = '';
    }

    // =====================================
    // FUNCIONES GLOBALES (llamadas desde HTML)
    // =====================================

    /**
     * Limpia todos los campos del formulario
     */
    window.clearForm = function () {
        const fieldActions = [
            { id: 'Id', action: field => field.value = '' },
            { id: 'Nombre', action: field => field.value = '' },
            { id: 'Precio', action: field => field.value = '' },
            { id: 'Tipo', action: field => field.selectedIndex = 0 },
            { id: 'TipoVehiculo', action: field => field.selectedIndex = 0 },
            { id: 'TiempoEstimado', action: field => field.value = '' },
            { id: 'Descripcion', action: field => field.value = '' }
        ];

        fieldActions.forEach(item => {
            const field = document.getElementById(item.id);
            if (field) item.action(field);
        });
    };

    /**
     * Carga formulario de servicio via AJAX
     */
    window.loadServicioForm = function (id) {
        const url = '/Servicio/FormPartial' + (id ? ('?id=' + encodeURIComponent(id)) : '');

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                document.getElementById('servicio-form-container').innerHTML = html;
                setupFormValidation();

                const desc = document.getElementById('Descripcion');
                if (desc) window.SiteModule?.autoGrow(desc);

                // Actualizar título
                const isEdit = !!document.getElementById('Id')?.value;
                const titleSpan = document.getElementById('form-title');
                if (titleSpan) {
                    titleSpan.textContent = isEdit ? 'Editando un Servicio' : 'Registrando un Servicio';
                }

                // Abrir acordeón
                const accordionBody = document.getElementById('accordion-flush-body-1');
                if (accordionBody && accordionBody.classList.contains('hidden')) {
                    accordionBody.classList.remove('hidden');
                }
            })
            .catch(e => console.error('Error cargando formulario:', e));
    };

    /**
     * Envía formulario de servicio via AJAX
     */
    window.submitServicioAjax = function (form) {
        const formData = new FormData(form);

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
            .then(result => {
                document.getElementById('servicio-form-container').innerHTML = result.html;
                setupFormValidation();

                const desc = document.getElementById('Descripcion');
                if (desc) window.SiteModule?.autoGrow(desc);

                const isEdit = !!document.getElementById('Id')?.value;
                const titleSpan = document.getElementById('form-title');
                if (titleSpan) {
                    titleSpan.textContent = isEdit ? 'Editando un Servicio' : 'Registrando un Servicio';
                }

                if (result.valid) {
                    showServicioMessage('success', result.msg || 'Operación exitosa.', 4000);
                    reloadServicioTable(1);
                } else {
                    const summary = document.getElementById('servicio-validation-summary');
                    if (summary && summary.textContent.trim().length > 0) {
                        summary.classList.remove('hidden');
                    }
                    showServicioMessage('error', 'Revise los errores del formulario.', 8000);
                }
            })
            .catch(e => {
                showServicioMessage('error', 'Error de comunicación con el servidor.', 8000);
            });

        return false;
    };

    /**
     * Abre modal de confirmación para servicios
     */
    window.openServicioConfirmModal = function (tipoAccion, id, nombre) {
        const modal = document.getElementById('servicioConfirmModal');
        const title = document.getElementById('servicioConfirmTitle');
        const msg = document.getElementById('servicioConfirmMessage');
        const submitBtn = document.getElementById('servicioConfirmSubmit');
        const form = document.getElementById('servicioConfirmForm');
        const idInput = document.getElementById('servicioConfirmId');
        const iconWrapper = document.getElementById('servicioConfirmIconWrapper');
        const icon = document.getElementById('servicioConfirmIcon');

        idInput.value = id;

        if (tipoAccion === 'desactivar') {
            configureDeactivateModal(title, msg, form, submitBtn, iconWrapper, icon, nombre);
        } else {
            configureReactivateModal(title, msg, form, submitBtn, iconWrapper, icon, nombre);
        }

        modal.classList.remove('hidden');
    };

    /**
     * Cierra modal de confirmación
     */
    window.closeServicioConfirmModal = function () {
        const modal = document.getElementById('servicioConfirmModal');
        modal.classList.add('hidden');
    };

    /**
     * Envía cambio de estado de servicio
     */
    window.submitServicioEstado = function (form) {
        const formData = new FormData(form);

        fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(r => {
                if (!r.ok) throw new Error('Error estado');
                window.closeServicioConfirmModal();

                const isDeactivate = form.action.includes('DeactivateServicio');
                const message = isDeactivate ? 'Servicio desactivado correctamente.' : 'Servicio reactivado correctamente.';

                showTableMessage('success', message);
                reloadServicioTable(getCurrentTablePage());
            })
            .catch(e => {
                showTableMessage('error', 'Error procesando la operación.');
            });

        return false;
    };

    // =====================================
    // FUNCIONES GLOBALES DE ORDENAMIENTO
    // =====================================
    window.sortTable = function (sortBy) {
        sortTable(sortBy);
    };

    window.reloadServicioTable = function (page) {
        reloadServicioTable(page);
    };

    window.getCurrentTablePage = function () {
        return getCurrentTablePage();
    };

    // =====================================
    // CONFIGURAR MODALES (AUXILIARES)
    // =====================================
    function configureDeactivateModal(title, msg, form, submitBtn, iconWrapper, icon, nombre) {
        title.textContent = 'Desactivar Servicio';
        msg.innerHTML = '¿Confirma desactivar el servicio <strong>' + (window.SiteModule?.escapeHtml(nombre) || nombre) + '</strong>?';
        form.action = '/Servicio/DeactivateServicio';
        submitBtn.textContent = 'Desactivar';
        submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-red-600 rounded-lg hover:bg-red-700 focus:ring-4 focus:outline-none focus:ring-red-300 dark:bg-red-500 dark:hover:bg-red-600 dark:focus:ring-red-900';

        iconWrapper.className = 'w-12 h-12 rounded-full bg-red-100 dark:bg-red-900 p-2 flex items-center justify-center mx-auto mb-3.5';
        icon.setAttribute('fill', 'currentColor');
        icon.setAttribute('viewBox', '0 0 20 20');
        icon.setAttribute('class', 'w-8 h-8 text-red-600 dark:text-red-400');
        icon.innerHTML = `<path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-11.293a1 1 0 00-1.414-1.414L10 7.586 7.707 5.293a1 1 0 00-1.414 1.414L8.586 10l-2.293 2.293a1 1 0 001.414 1.414L10 12.414l2.293 2.293a1 1 0 001.414-1.414L11.414 10l2.293-2.293z" clip-rule="evenodd"/>`;
    }

    function configureReactivateModal(title, msg, form, submitBtn, iconWrapper, icon, nombre) {
        title.textContent = 'Reactivar Servicio';
        msg.innerHTML = '¿Confirma reactivar el servicio <strong>' + (window.SiteModule?.escapeHtml(nombre) || nombre) + '</strong>?';
        form.action = '/Servicio/ReactivateServicio';
        submitBtn.textContent = 'Reactivar';
        submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-green-600 rounded-lg hover:bg-green-700 focus:ring-4 focus:outline-none focus:ring-green-300 dark:bg-green-500 dark:hover:bg-green-600 dark:focus:ring-green-900';

        iconWrapper.className = 'w-12 h-12 rounded-full bg-green-100 dark:bg-green-900 p-2 flex items-center justify-center mx-auto mb-3.5';
        icon.setAttribute('fill', 'currentColor');
        icon.setAttribute('viewBox', '0 0 24 24');
        icon.setAttribute('class', 'w-8 h-8 text-green-500 dark:text-green-400');
        icon.innerHTML = `<path fill-rule="evenodd" d="M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 4.365 9.75 9.75-4.365 9.75-9.75 9.75S2.25 17.385 2.25 12Zm13.36-1.814a.75.75 0 1 0-1.22-.872l-3.236 4.53L9.53 12.22a.75.75 0 0 0-1.06 1.06l2.25 2.25a.75.75 0 0 0 1.14-.094l3.75-5.25Z" clip-rule="evenodd"/>`;
    }

    // Exportar funciones globalmente
    window.limpiarModalTipoServicio = limpiarModalTipoServicio;
    window.limpiarModalTipoVehiculo = limpiarModalTipoVehiculo;
    window.cerrarModal = cerrarModal;

    // =====================================
    // GESTIÓN DE ETAPAS
    // =====================================
    
    /**
     * Variable global para almacenar las etapas en memoria
     */
    let etapasEnMemoria = [];

    /**
     * Abre el modal de gestión de etapas
     */
    window.openEtapasModal = function() {
        // Cargar etapas existentes desde el input hidden
        const etapasJson = document.getElementById('etapas-json')?.value || '[]';
        try {
            etapasEnMemoria = JSON.parse(etapasJson);
        } catch (e) {
            etapasEnMemoria = [];
        }
        
        // Renderizar lista de etapas
        renderEtapasList();
        
        // Mostrar modal
        const modal = document.getElementById('etapasModal');
        if (modal) {
            modal.classList.remove('hidden');
        }
        
        // Configurar el listener para Enter en el input
        const inputNombre = document.getElementById('nuevaEtapaNombre');
        if (inputNombre) {
            inputNombre.addEventListener('keypress', handleEtapaInputKeyPress);
            inputNombre.focus();
        }
    };

    /**
     * Cierra el modal de gestión de etapas y guarda los cambios
     */
    window.closeEtapasModal = function() {
        // Guardar etapas en el input hidden
        const etapasJson = JSON.stringify(etapasEnMemoria);
        const hiddenInput = document.getElementById('etapas-json');
        if (hiddenInput) {
            hiddenInput.value = etapasJson;
        }
        
        // Actualizar contador
        const countSpan = document.getElementById('etapas-count');
        if (countSpan) {
            countSpan.textContent = etapasEnMemoria.length;
        }
        
        // Cerrar modal
        const modal = document.getElementById('etapasModal');
        if (modal) {
            modal.classList.add('hidden');
        }
        
        // Limpiar input
        const inputNombre = document.getElementById('nuevaEtapaNombre');
        if (inputNombre) {
            inputNombre.value = '';
        }
    };

    /**
     * Agrega una nueva etapa a la lista
     */
    window.agregarEtapa = function() {
        const inputNombre = document.getElementById('nuevaEtapaNombre');
        if (!inputNombre) return;
        
        const nombre = inputNombre.value.trim();
        if (!nombre) {
            alert('Por favor, ingrese el nombre de la etapa');
            return;
        }
        
        // Crear nueva etapa
        const nuevaEtapa = {
            Id: 'etapa-' + Date.now() + '-' + Math.random().toString(36).substr(2, 9),
            Nombre: nombre,
            Estado: 'Pendiente'
        };
        
        // Agregar a la lista en memoria
        etapasEnMemoria.push(nuevaEtapa);
        
        // Re-renderizar lista
        renderEtapasList();
        
        // Limpiar input
        inputNombre.value = '';
        inputNombre.focus();
    };

    /**
     * Elimina una etapa de la lista
     */
    window.eliminarEtapa = function(etapaId) {
        etapasEnMemoria = etapasEnMemoria.filter(e => e.Id !== etapaId);
        renderEtapasList();
    };

    /**
     * Renderiza la lista de etapas en el modal
     */
    function renderEtapasList() {
        const container = document.getElementById('etapas-list');
        if (!container) return;
        
        if (etapasEnMemoria.length === 0) {
            container.innerHTML = `
                <div class="text-center text-gray-500 dark:text-gray-400 py-8">
                    No hay etapas agregadas. Agregue una etapa usando el campo de arriba.
                </div>
            `;
            return;
        }
        
        container.innerHTML = etapasEnMemoria.map((etapa, index) => `
            <div class="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700 rounded-lg border border-gray-200 dark:border-gray-600">
                <div class="flex items-center gap-3">
                    <span class="text-sm font-medium text-gray-700 dark:text-gray-300">${index + 1}.</span>
                    <span class="text-sm text-gray-900 dark:text-white">${escapeHtml(etapa.Nombre)}</span>
                </div>
                <button type="button" onclick="eliminarEtapa('${etapa.Id}')" 
                        class="text-red-600 hover:text-red-700 dark:text-red-400 dark:hover:text-red-300">
                    <svg class="w-5 h-5" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12"></path>
                    </svg>
                </button>
            </div>
        `).join('');
    }

    /**
     * Escapa HTML para prevenir XSS
     */
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Maneja la tecla Enter en el input de nueva etapa
     */
    function handleEtapaInputKeyPress(event) {
        if (event.key === 'Enter') {
            event.preventDefault();
            agregarEtapa();
        }
    }

})();