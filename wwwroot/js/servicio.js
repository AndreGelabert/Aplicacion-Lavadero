/**
 * ================================================
 * SERVICIO.JS - FUNCIONALIDAD DE LA PÁGINA DE SERVICIOS
 * ================================================
 */

(function () {
    'use strict';

    let servicioMsgTimeout = null;

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
        window.CommonUtils?.setupDefaultFilterForm();

        // Verificar si estamos en modo edición
        checkEditMode();
    }

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

        // Obtener ordenamiento actual
        const currentSort = getCurrentSort();
        // Asegurar que se incluyan todos los parámetros
        params.set('pageNumber', page.toString());
        params.set('sortBy', currentSort.sortBy);
        params.set('sortOrder', currentSort.sortOrder);
        const url = `/Servicio/TablePartial?${params.toString()}`;
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
        let errorMessages = [];

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
    }

    /**
     * Configura modales de tipos de servicio
     */
    function setupServiceTypeModals() {
        const btnEliminarTipo = document.querySelector('[data-modal-toggle="eliminarTipoModal"]');
        if (btnEliminarTipo) {
            btnEliminarTipo.addEventListener('click', function () {
                const tipoSeleccionado = document.getElementById('Tipo')?.value;
                const eliminInput = document.getElementById('nombreTipoEliminar');
                if (eliminInput) eliminInput.value = tipoSeleccionado;
            });
        }

        const cancelarTipoBtn = document.querySelector('#defaultModal button[type="button"]');
        if (cancelarTipoBtn) {
            cancelarTipoBtn.addEventListener('click', limpiarModalTipoServicio);
        }
    }

    /**
     * Configura modales de tipos de vehículo
     */
    function setupVehicleTypeModals() {
        const btnEliminarTipoVehiculo = document.querySelector('[data-modal-toggle="eliminarTipoVehiculoModal"]');
        if (btnEliminarTipoVehiculo) {
            btnEliminarTipoVehiculo.addEventListener('click', function () {
                const tipoSeleccionado = document.getElementById('TipoVehiculo')?.value;
                const eliminInput = document.getElementById('nombreTipoVehiculoEliminar');
                if (eliminInput) eliminInput.value = tipoSeleccionado;
            });
        }

        const cancelarTipoVehiculoBtn = document.querySelector('#tipoVehiculoModal button[type="button"]');
        if (cancelarTipoVehiculoBtn) {
            cancelarTipoVehiculoBtn.addEventListener('click', limpiarModalTipoVehiculo);
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

        if (servicioMsgTimeout) {
            clearTimeout(servicioMsgTimeout);
            servicioMsgTimeout = null;
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

        servicioMsgTimeout = setTimeout(() => {
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

        const toggleBtn = document.querySelector('[data-modal-toggle="defaultModal"]');
        if (toggleBtn) toggleBtn.click();
    }

    /**
     * Limpia modal de tipo de vehículo
     */
    function limpiarModalTipoVehiculo() {
        const nombreTipoVehiculo = document.getElementById('nombreTipoVehiculo');
        if (nombreTipoVehiculo) nombreTipoVehiculo.value = '';

        const toggleBtn = document.querySelector('[data-modal-toggle="tipoVehiculoModal"]');
        if (toggleBtn) toggleBtn.click();
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

})();