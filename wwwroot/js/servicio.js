/**
 * Módulo para la gestión de servicios
 * Maneja CRUD, validaciones, AJAX, modales y tablas
 */
const ServicioModule = {

    // Timeout para mensajes
    messageTimeout: null,

    /**
     * Inicializa el módulo de servicios
     */
    init() {
        this.setupAccordion();
        this.setupFormValidation();
        this.setupModals();
        this.setupDescriptionField();
        this.setupMessageHandling();
    },

    // =====================================
    // ACORDEÓN
    // =====================================

    /**
     * Configura el acordeón del formulario
     */
    setupAccordion() {
        SiteModule.setupAccordion('accordion-flush-body-1', () => {
            const hayErrores = document.getElementById('error-alert') || document.getElementById('success-alert');
            const servicioFormErrors = document.getElementById('servicio-form')?.querySelector('.validation-message');
            const formularioEdicion = document.getElementById('form-title')?.textContent.includes('Editando');

            return hayErrores || servicioFormErrors || formularioEdicion;
        });
    },

    // =====================================
    // VALIDACIÓN DE FORMULARIOS
    // =====================================

    /**
     * Configura la validación del formulario de servicios
     */
    setupFormValidation() {
        const servicioForm = document.getElementById('servicio-form');
        if (!servicioForm) return;

        this.setupFieldValidation();

        if (!servicioForm.hasAttribute('data-validation-setup')) {
            servicioForm.addEventListener('submit', this.validateFormOnSubmit.bind(this));
            servicioForm.setAttribute('data-validation-setup', 'true');
        }
    },

    /**
     * Configura validación de campos individuales
     */
    setupFieldValidation() {
        const nombreInput = document.getElementById('Nombre');
        const precioInput = document.getElementById('Precio');
        const tiempoEstimadoInput = document.getElementById('TiempoEstimado');

        if (nombreInput) {
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

        if (precioInput) {
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

        if (tiempoEstimadoInput) {
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
    },

    /**
     * Valida el formulario al enviarlo
     * @param {Event} e - Evento de envío
     */
    validateFormOnSubmit(e) {
        const servicioForm = e.target;
        const requiredFields = servicioForm.querySelectorAll('[required]');
        let allValid = true;

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
            this.showFormError("Por favor, complete todos los campos obligatorios.");
            return;
        }

        // Validaciones específicas
        const nombre = document.getElementById('Nombre').value.trim();
        const precio = parseFloat(document.getElementById('Precio').value);
        const tiempoEstimado = parseInt(document.getElementById('TiempoEstimado').value);

        let hasErrors = false;
        let errorMessages = [];

        if (!/^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$/.test(nombre) || nombre === '') {
            document.getElementById('Nombre').classList.add('border-red-500');
            errorMessages.push("El nombre solo puede contener letras y espacios");
            hasErrors = true;
        }

        if (isNaN(precio) || precio < 0) {
            document.getElementById('Precio').classList.add('border-red-500');
            errorMessages.push("El precio debe ser igual o mayor a 0");
            hasErrors = true;
        }

        if (isNaN(tiempoEstimado) || tiempoEstimado <= 0) {
            document.getElementById('TiempoEstimado').classList.add('border-red-500');
            errorMessages.push("El tiempo estimado debe ser mayor a 0");
            hasErrors = true;
        }

        if (hasErrors) {
            e.preventDefault();
            this.showFormError(errorMessages.join('<br>'));
        }
    },

    /**
     * Muestra errores del formulario
     * @param {string} message - Mensaje de error
     */
    showFormError(message) {
        const accordion = document.getElementById('accordion-flush-body-1');
        if (accordion && accordion.classList.contains('hidden')) {
            accordion.classList.remove('hidden');
        }

        let errorDiv = document.getElementById('form-error-message');
        if (!errorDiv) {
            const servicioForm = document.getElementById('servicio-form');
            errorDiv = document.createElement('div');
            errorDiv.id = 'form-error-message';
            errorDiv.className = 'p-4 mb-4 text-sm text-red-800 rounded-lg bg-red-50 dark:bg-gray-800 dark:text-red-400';
            servicioForm.appendChild(errorDiv);
        }

        errorDiv.innerHTML = '<span class="font-medium">¡Error!</span> ' + message;
    },

    // =====================================
    // GESTIÓN DE MODALES
    // =====================================

    /**
     * Configura todos los modales de la página
     */
    setupModals() {
        this.setupServiceTypeModals();
        this.setupVehicleTypeModals();
        this.setupConfirmationModal();
    },

    /**
     * Configura modales de tipos de servicio
     */
    setupServiceTypeModals() {
        const btnEliminarTipo = document.querySelector('[data-modal-toggle="eliminarTipoModal"]');
        if (btnEliminarTipo) {
            btnEliminarTipo.addEventListener('click', () => {
                const tipoSeleccionado = document.getElementById('Tipo').value;
                document.getElementById('nombreTipoEliminar').value = tipoSeleccionado;
            });
        }

        const cancelarTipoBtn = document.querySelector('#defaultModal button[type="button"]');
        if (cancelarTipoBtn) {
            cancelarTipoBtn.addEventListener('click', this.limpiarModalTipoServicio);
        }
    },

    /**
     * Configura modales de tipos de vehículo
     */
    setupVehicleTypeModals() {
        const btnEliminarTipoVehiculo = document.querySelector('[data-modal-toggle="eliminarTipoVehiculoModal"]');
        if (btnEliminarTipoVehiculo) {
            btnEliminarTipoVehiculo.addEventListener('click', () => {
                const tipoSeleccionado = document.getElementById('TipoVehiculo').value;
                document.getElementById('nombreTipoVehiculoEliminar').value = tipoSeleccionado;
            });
        }

        const cancelarTipoVehiculoBtn = document.querySelector('#tipoVehiculoModal button[type="button"]');
        if (cancelarTipoVehiculoBtn) {
            cancelarTipoVehiculoBtn.addEventListener('click', this.limpiarModalTipoVehiculo);
        }
    },

    /**
     * Configura el modal de confirmación reutilizable
     */
    setupConfirmationModal() {
        // El modal se configura dinámicamente en openServicioConfirmModal
    },

    /**
     * Limpia y cierra el modal de tipo de servicio
     */
    limpiarModalTipoServicio() {
        document.getElementById('nombreTipo').value = '';
        document.querySelector('[data-modal-toggle="defaultModal"]').click();
    },

    /**
     * Limpia y cierra el modal de tipo de vehículo
     */
    limpiarModalTipoVehiculo() {
        document.getElementById('nombreTipoVehiculo').value = '';
        document.querySelector('[data-modal-toggle="tipoVehiculoModal"]').click();
    },

    /**
     * Abre modal de confirmación para cambio de estado
     * @param {string} tipoAccion - 'desactivar' o 'reactivar'
     * @param {string} id - ID del servicio
     * @param {string} nombre - Nombre del servicio
     */
    openServicioConfirmModal(tipoAccion, id, nombre) {
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
            title.textContent = 'Desactivar Servicio';
            msg.innerHTML = '¿Confirma desactivar el servicio <strong>' + SiteModule.escapeHtml(nombre) + '</strong>?';
            form.action = '/Servicio/DeactivateServicio';
            submitBtn.textContent = 'Desactivar';
            submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-red-600 rounded-lg hover:bg-red-700 focus:ring-4 focus:outline-none focus:ring-red-300 dark:bg-red-500 dark:hover:bg-red-600 dark:focus:ring-red-900';

            iconWrapper.className = 'w-12 h-12 rounded-full bg-red-100 dark:bg-red-900 p-2 flex items-center justify-center mx-auto mb-3.5';
            icon.setAttribute('fill', 'currentColor');
            icon.setAttribute('viewBox', '0 0 20 20');
            icon.setAttribute('class', 'w-8 h-8 text-red-600 dark:text-red-400');
            icon.innerHTML = `<path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-11.293a1 1 0 00-1.414-1.414L10 7.586 7.707 5.293a1 1 0 00-1.414 1.414L8.586 10l-2.293 2.293a1 1 0 001.414 1.414L10 12.414l2.293 2.293a1 1 0 001.414-1.414L11.414 10l2.293-2.293z" clip-rule="evenodd"/>`;
        } else {
            title.textContent = 'Reactivar Servicio';
            msg.innerHTML = '¿Confirma reactivar el servicio <strong>' + SiteModule.escapeHtml(nombre) + '</strong>?';
            form.action = '/Servicio/ReactivateServicio';
            submitBtn.textContent = 'Reactivar';
            submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-green-600 rounded-lg hover:bg-green-700 focus:ring-4 focus:outline-none focus:ring-green-300 dark:bg-green-500 dark:hover:bg-green-600 dark:focus:ring-green-900';

            iconWrapper.className = 'w-12 h-12 rounded-full bg-green-100 dark:bg-green-900 p-2 flex items-center justify-center mx-auto mb-3.5';
            icon.setAttribute('fill', 'currentColor');
            icon.setAttribute('viewBox', '0 0 24 24');
            icon.setAttribute('class', 'w-8 h-8 text-green-500 dark:text-green-400');
            icon.innerHTML = `<path fill-rule="evenodd" d="M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 4.365 9.75 9.75-4.365 9.75-9.75 9.75S2.25 17.385 2.25 12Zm13.36-1.814a.75.75 0 1 0-1.22-.872l-3.236 4.53L9.53 12.22a.75.75 0 0 0-1.06 1.06l2.25 2.25a.75.75 0 0 0 1.14-.094l3.75-5.25Z" clip-rule="evenodd"/>`;
        }

        modal.classList.remove('hidden');
    },

    /**
     * Cierra el modal de confirmación
     */
    closeServicioConfirmModal() {
        const modal = document.getElementById('servicioConfirmModal');
        modal.classList.add('hidden');
    },

    // =====================================
    // OPERACIONES AJAX
    // =====================================

    /**
     * Carga formulario vía AJAX
     * @param {string} id - ID del servicio (opcional para nuevo)
     */
    loadServicioForm(id) {
        const url = '/Servicio/FormPartial' + (id ? ('?id=' + encodeURIComponent(id)) : '');
        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => {
                document.getElementById('servicio-form-container').innerHTML = html;
                this.setupFormValidation();

                const desc = document.getElementById('Descripcion');
                if (desc) SiteModule.autoGrow(desc);

                const isEdit = !!document.getElementById('Id')?.value;
                const titleSpan = document.getElementById('form-title');
                if (titleSpan) titleSpan.textContent = isEdit ? 'Editando un Servicio' : 'Registrando un Servicio';

                const accordionBody = document.getElementById('accordion-flush-body-1');
                if (accordionBody && accordionBody.classList.contains('hidden')) {
                    accordionBody.classList.remove('hidden');
                }

                // NUEVO: Scroll automático hacia el formulario
                this.scrollToForm();
            })
            .catch(e => console.error('Error cargando formulario:', e));
    },

    /**
     * Realiza scroll suave hacia el formulario de edición
     */
    scrollToForm() {
        // Buscar el elemento del acordeón o el formulario
        const formContainer = document.getElementById('accordion-flush-body-1') || 
                             document.getElementById('servicio-form-container') ||
                             document.getElementById('servicio-form');
        
        if (formContainer) {
            // Usar scrollIntoView con comportamiento suave
            formContainer.scrollIntoView({
                behavior: 'smooth',
                block: 'start',
                inline: 'nearest'
            });
            
            // Alternativa: si scrollIntoView no funciona bien, usar un offset manual
            // const elementTop = formContainer.getBoundingClientRect().top + window.pageYOffset;
            // const offset = 80; // Espacio desde el top para que no quede pegado
            // window.scrollTo({
            //     top: elementTop - offset,
            //     behavior: 'smooth'
            // });
        }
    },

    /**
     * Envía formulario vía AJAX
     * @param {HTMLFormElement} form - Formulario a enviar
     */
    submitServicioAjax(form) {
        const fd = new FormData(form);
        fetch(form.action, {
            method: 'POST',
            body: fd,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(resp => {
                const valid = resp.headers.get('X-Form-Valid') === 'true';
                const msg = resp.headers.get('X-Form-Message');
                return resp.text().then(html => ({ html, valid, msg }));
            })
            .then(r => {
                document.getElementById('servicio-form-container').innerHTML = r.html;
                this.setupFormValidation();

                const desc = document.getElementById('Descripcion');
                if (desc) SiteModule.autoGrow(desc);

                const isEdit = !!document.getElementById('Id')?.value;
                const titleSpan = document.getElementById('form-title');
                if (titleSpan) titleSpan.textContent = isEdit ? 'Editando un Servicio' : 'Registrando un Servicio';

                if (r.valid) {
                    this.showServicioMessage('success', r.msg || 'Operación exitosa.', 4000);
                    this.reloadServicioTable(1);
                } else {
                    const summary = document.getElementById('servicio-validation-summary');
                    if (summary && summary.textContent.trim().length > 0) {
                        summary.classList.remove('hidden');
                    }
                    this.showServicioMessage('error', 'Revise los errores del formulario.', 8000);
                }
            })
            .catch(e => {
                console.error('Error enviando formulario:', e);
                this.showServicioMessage('error', 'Error de comunicación con el servidor.', 8000);
            });
        return false;
    },

    /**
     * Cambia estado del servicio vía AJAX
     * @param {HTMLFormElement} form - Formulario con datos del servicio
     */
    submitServicioEstado(form) {
        const fd = new FormData(form);
        fetch(form.action, {
            method: 'POST',
            body: fd,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        }).then(r => {
            if (!r.ok) throw new Error('Error estado');
            this.closeServicioConfirmModal();

            // Determinar el mensaje según la acción
            const isDeactivate = form.action.includes('DeactivateServicio');
            const message = isDeactivate ? 'Servicio desactivado correctamente.' : 'Servicio reactivado correctamente.';

            // Mostrar notificación con el mismo formato que las otras
            this.showTableMessage('success', message);

            this.reloadServicioTable(this.getCurrentTablePage());
        }).catch(e => {
            console.error(e);
            this.showTableMessage('error', 'Error procesando la operación.');
        });
        return false;
    },

    /**
     * Recarga la tabla de servicios con filtros actuales
     * @param {number} page - Número de página
     */
    reloadServicioTable(page) {
        // Obtener filtros actuales del formulario
        const filterForm = document.getElementById('filterForm');
        const params = new URLSearchParams();

        if (filterForm) {
            const formData = new FormData(filterForm);
            for (const [key, value] of formData.entries()) {
                params.append(key, value);
            }
        }

        // Asegurar que se incluya el número de página
        params.set('pageNumber', page.toString());

        const url = `/Servicio/TablePartial?${params.toString()}`;
        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => {
                return r.text();
            })
            .then(html => {
                const cont = document.getElementById('servicio-table-container');
                cont.innerHTML = html;
                const cp = document.getElementById('current-page-value')?.value;
                if (cp) cont.dataset.currentPage = cp;
            })
            .catch(e => {
                this.showTableMessage('error', 'Error cargando la tabla.');
            });
    },

    /**
     * Obtiene la página actual de la tabla
     * @returns {number} Número de página actual
     */
    getCurrentTablePage() {
        return parseInt(document.getElementById('servicio-table-container')?.dataset.currentPage || '1');
    },

    // =====================================
    // MENSAJES Y ALERTAS
    // =====================================

    /**
     * Configura el manejo de mensajes del formulario
     */
    setupMessageHandling() {
        document.addEventListener('input', (e) => {
            if (e.target.closest('#servicio-form')) {
                const alert = document.getElementById('servicio-inline-alert');
                if (alert) {
                    if (this.messageTimeout) clearTimeout(this.messageTimeout);
                    alert.classList.add('opacity-0');
                    setTimeout(() => { if (alert.parentElement) alert.remove(); }, 400);
                }
            }
        });
    },

    /**
     * Muestra mensaje en el formulario
     * @param {'success'|'error'|'info'} type - Tipo de mensaje
     * @param {string} msg - Mensaje a mostrar
     * @param {number} disappearMs - Milisegundos antes de desaparecer
     */
    showServicioMessage(type, msg, disappearMs = 5000) {
        const container = document.getElementById('ajax-form-messages');
        if (!container) return;

        if (this.messageTimeout) {
            clearTimeout(this.messageTimeout);
            this.messageTimeout = null;
        }

        const color = type === 'success'
            ? { bg: 'green-50', text: 'green-800', darkText: 'green-400', border: 'green-300' }
            : type === 'info'
                ? { bg: 'blue-50', text: 'blue-800', darkText: 'blue-400', border: 'blue-300' }
                : { bg: 'red-50', text: 'red-800', darkText: 'red-400', border: 'red-300' };

        container.innerHTML = `<div id="servicio-inline-alert"
            class="servicio-inline-alert opacity-100 transition-opacity duration-700
                   p-4 mb-4 text-sm rounded-lg border
                   bg-${color.bg} text-${color.text} border-${color.border}
                   dark:bg-gray-800 dark:text-${color.darkText}">
            ${msg}
        </div>`;

        const alertEl = document.getElementById('servicio-inline-alert');
        if (!alertEl) return;

        this.messageTimeout = setTimeout(() => {
            alertEl.classList.add('opacity-0');
            setTimeout(() => {
                if (alertEl.parentElement) alertEl.remove();
            }, 750);
        }, disappearMs);
    },

    /**
     * Muestra mensaje relacionado con la tabla (activar/desactivar servicios)
     * @param {'success'|'error'|'info'} type - Tipo de mensaje
     * @param {string} msg - Mensaje a mostrar
     * @param {number} disappearMs - Milisegundos antes de desaparecer
     */
    showTableMessage(type, msg, disappearMs = 5000) {
        // Buscar si ya existe un contenedor de mensajes de tabla
        let container = document.getElementById('table-messages-container');

        if (!container) {
            // Crear el contenedor si no existe
            container = document.createElement('div');
            container.id = 'table-messages-container';
            container.className = 'mb-4';

            // Insertar antes de la tabla
            const tableContainer = document.getElementById('servicio-table-container');
            tableContainer.parentNode.insertBefore(container, tableContainer);
        }

        if (this.messageTimeout) {
            clearTimeout(this.messageTimeout);
            this.messageTimeout = null;
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

        this.messageTimeout = setTimeout(() => {
            alertEl.classList.add('opacity-0');
            setTimeout(() => {
                if (alertEl.parentElement) {
                    alertEl.remove();
                    // Si el contenedor queda vacío, ocultarlo
                    if (container.children.length === 0) {
                        container.style.display = 'none';
                    }
                }
            }, 750);
        }, disappearMs);
    },

    // =====================================
    // UTILIDADES
    // =====================================

    /**
     * Configura el campo de descripción
     */
    setupDescriptionField() {
        const descripcionField = document.getElementById('Descripcion');
        if (descripcionField) {
            SiteModule.autoGrow(descripcionField);
        }
    },

    /**
    * Realiza scroll hacia el tope de la página para asegurar que el formulario sea completamente visible
    */
    scrollToForm() {
        // Scroll directo al tope de la página para asegurar visibilidad completa del formulario
        window.scrollTo({
            top: 0,
            behavior: 'smooth'
        });
    },

    /**
     * Limpia todos los campos del formulario
     */
    clearForm() {
        const fields = [
            { id: 'Id', action: field => field.value = '' },
            { id: 'Nombre', action: field => field.value = '' },
            { id: 'Precio', action: field => field.value = '' },
            { id: 'Tipo', action: field => field.selectedIndex = 0 },
            { id: 'TipoVehiculo', action: field => field.selectedIndex = 0 },
            { id: 'TiempoEstimado', action: field => field.value = '' },
            { id: 'Descripcion', action: field => field.value = '' }
        ];

        fields.forEach(item => {
            const field = document.getElementById(item.id);
            if (field) item.action(field);
        });
    }
};

// =====================================
// INICIALIZACIÓN DEL MÓDULO
// =====================================
document.addEventListener('DOMContentLoaded', () => {
    // Solo inicializar si estamos en la página de servicios
    if (document.getElementById('servicio-form') || document.getElementById('servicio-table-container')) {
        ServicioModule.init();
    }
});

// Exportar funciones globalmente para uso en las vistas
window.loadServicioForm = (id) => ServicioModule.loadServicioForm(id);
window.submitServicioAjax = (form) => ServicioModule.submitServicioAjax(form);
window.submitServicioEstado = (form) => ServicioModule.submitServicioEstado(form);
window.openServicioConfirmModal = (tipo, id, nombre) => ServicioModule.openServicioConfirmModal(tipo, id, nombre);
window.closeServicioConfirmModal = () => ServicioModule.closeServicioConfirmModal();
window.clearForm = () => ServicioModule.clearForm();
window.clearAllFilters = () => SiteModule.clearAllFilters();
window.reloadServicioTable = (page) => ServicioModule.reloadServicioTable(page);