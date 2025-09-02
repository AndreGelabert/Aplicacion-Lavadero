/**
 * Módulo para la gestión de personal
 * Maneja la edición de roles, reactivación y notificaciones
 */
const PersonalModule = {

    // Estado de edición y timeout para mensajes
    isEditing: false,
    messageTimeout: null,

    /**
     * Inicializa el módulo de personal
     */
    init() {
        this.setupRoleEditing();
        this.setupNotificationHandling();
        this.setupConfirmationModal();
    },

    /**
     * Configura la funcionalidad de edición de roles
     */
    setupRoleEditing() {
        document.addEventListener('click', (event) => {
            let isClickInside = false;
            const forms = document.querySelectorAll('form[id^="rol-form-"]');

            forms.forEach((form) => {
                if (form.contains(event.target) || event.target.closest('button[onclick^="toggleEdit"]')) {
                    isClickInside = true;
                }
            });

            if (!isClickInside && this.isEditing) {
                location.reload();
            }
        });
    },

    /**
     * Configura el modal de confirmación reutilizable
     */
    setupConfirmationModal() {
        // El modal se configura dinámicamente en openPersonalConfirmModal
    },

    /**
     * Abre modal de confirmación para cambio de estado
     * @param {string} tipoAccion - 'desactivar' o 'reactivar'
     * @param {string} id - ID del empleado
     * @param {string} nombre - Nombre del empleado
     */
    openPersonalConfirmModal(tipoAccion, id, nombre) {
        const modal = document.getElementById('personalConfirmModal');
        const title = document.getElementById('personalConfirmTitle');
        const msg = document.getElementById('personalConfirmMessage');
        const submitBtn = document.getElementById('personalConfirmSubmit');
        const form = document.getElementById('personalConfirmForm');
        const idInput = document.getElementById('personalConfirmId');
        const iconWrapper = document.getElementById('personalConfirmIconWrapper');
        const icon = document.getElementById('personalConfirmIcon');

        idInput.value = id;

        if (tipoAccion === 'desactivar') {
            title.textContent = 'Desactivar Empleado';
            msg.innerHTML = '¿Confirma desactivar al empleado <strong>' + this.escapeHtml(nombre) + '</strong>?';
            form.action = '/Personal/DeactivateEmployee';
            submitBtn.textContent = 'Desactivar';
            submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-red-600 rounded-lg hover:bg-red-700 focus:ring-4 focus:outline-none focus:ring-red-300 dark:bg-red-500 dark:hover:bg-red-600 dark:focus:ring-red-900';

            iconWrapper.className = 'w-12 h-12 rounded-full bg-red-100 dark:bg-red-900 p-2 flex items-center justify-center mx-auto mb-3.5';
            icon.setAttribute('fill', 'currentColor');
            icon.setAttribute('viewBox', '0 0 20 20');
            icon.setAttribute('class', 'w-8 h-8 text-red-600 dark:text-red-400');
            icon.innerHTML = `<path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-11.293a1 1 0 00-1.414-1.414L10 7.586 7.707 5.293a1 1 0 00-1.414 1.414L8.586 10l-2.293 2.293a1 1 0 001.414 1.414L10 12.414l2.293 2.293a1 1 0 001.414-1.414L11.414 10l2.293-2.293z" clip-rule="evenodd"/>`;
        } else {
            title.textContent = 'Reactivar Empleado';
            msg.innerHTML = '¿Confirma reactivar al empleado <strong>' + this.escapeHtml(nombre) + '</strong>?';
            form.action = '/Personal/ReactivateEmployee';
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
    closePersonalConfirmModal() {
        const modal = document.getElementById('personalConfirmModal');
        modal.classList.add('hidden');
    },

    /**
     * Cambia estado del empleado vía AJAX
     * @param {HTMLFormElement} form - Formulario con datos del empleado
     */
    submitPersonalEstado(form) {
        const fd = new FormData(form);
        fetch(form.action, {
            method: 'POST',
            body: fd,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        }).then(r => {
            if (!r.ok) throw new Error('Error estado');
            this.closePersonalConfirmModal();

            // Determinar el mensaje según la acción
            const isDeactivate = form.action.includes('DeactivateEmployee');
            const message = isDeactivate ? 'Empleado desactivado correctamente.' : 'Empleado reactivado correctamente.';

            // Mostrar notificación con el mismo formato que las otras
            this.showTableMessage('success', message);

            // Recargar la página después de un breve delay
            setTimeout(() => {
                location.reload();
            }, 1500);
        }).catch(e => {
            console.error(e);
            this.showTableMessage('error', 'Error procesando la operación.');
        });
        return false;
    },

    /**
     * Configura el manejo de notificaciones al inicio
     */
    setupNotificationHandling() {
        // Mostrar notificaciones de éxito/error del servidor si existen
        this.showServerNotifications();
    },

    /**
     * Muestra notificaciones del servidor basadas en TempData
     */
    showServerNotifications() {
        // Verificar si hay mensajes de éxito en TempData
        const successMessage = this.getMetaContent('success-message');
        const errorMessage = this.getMetaContent('error-message');

        if (successMessage) {
            this.showTableMessage('success', successMessage);
        } else if (errorMessage) {
            this.showTableMessage('error', errorMessage);
        }
    },

    /**
     * Obtiene contenido de meta tags
     * @param {string} name - Nombre del meta tag
     * @returns {string|null} Contenido del meta tag
     */
    getMetaContent(name) {
        const meta = document.querySelector(`meta[name="${name}"]`);
        return meta ? meta.getAttribute('content') : null;
    },

    /**
     * Alterna entre modo edición y visualización de roles
     * @param {string} id - ID del empleado
     */
    toggleEdit(id) {
        const rolText = document.getElementById('rol-text-' + id);
        const rolForm = document.getElementById('rol-form-' + id);
        if (!rolText || !rolForm) return;

        if (rolText.classList.contains('hidden')) {
            rolText.classList.remove('hidden');
            rolForm.classList.add('hidden');
            this.isEditing = false;
        } else {
            rolText.classList.add('hidden');
            rolForm.classList.remove('hidden');
            this.isEditing = true;
        }
    },

    /**
     * Envía el formulario de edición de rol con notificación
     * @param {string} id - ID del empleado
     */
    submitForm(id) {
        const form = document.getElementById('rol-form-' + id);
        if (!form) return;

        // Interceptar el envío para mostrar notificación
        const formData = new FormData(form);

        fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    this.showTableMessage('success', data.message || 'Rol actualizado correctamente.');
                    // Delay para mostrar el mensaje antes de recargar
                    setTimeout(() => {
                        location.reload();
                    }, 1500);
                } else {
                    this.showTableMessage('error', data.message || 'Error al actualizar el rol.');
                }
            })
            .catch(() => {
                this.showTableMessage('error', 'Error de comunicación con el servidor.');
            });
    },

    // =====================================
    // SISTEMA DE NOTIFICACIONES
    // =====================================

    /**
     * Muestra mensaje relacionado con la tabla
     * @param {'success'|'error'|'info'} type - Tipo de mensaje
     * @param {string} msg - Mensaje a mostrar
     * @param {number} disappearMs - Milisegundos antes de desaparecer
     */
    showTableMessage(type, msg, disappearMs = 5000) {
        // Buscar si ya existe un contenedor de mensajes de tabla
        let container = document.getElementById('personal-messages-container');

        if (!container) {
            // Crear el contenedor si no existe
            container = document.createElement('div');
            container.id = 'personal-messages-container';
            container.className = 'mb-4';

            // Insertar antes de la tabla (después del encabezado de tabla)
            const tableContainer = document.querySelector('.overflow-x-auto');
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

        container.innerHTML = `<div id="personal-inline-alert"
            class="personal-inline-alert opacity-100 transition-opacity duration-700
                   p-4 mb-4 text-sm rounded-lg border
                   bg-${color.bg} text-${color.text} border-${color.border}
                   dark:bg-gray-800 dark:text-${color.darkText}">
            ${this.escapeHtml(msg)}
        </div>`;

        const alertEl = document.getElementById('personal-inline-alert');
        if (!alertEl) return;

        // Hacer scroll hacia la notificación si no está visible
        alertEl.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

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

    /**
     * Escapa caracteres HTML para prevenir XSS
     * @param {string} str - Cadena a escapar
     * @returns {string} Cadena escapada
     */
    escapeHtml(str) {
        return str.replace(/[&<>"']/g, c => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[c]));
    },

    /**
     * Cierra manualmente las notificaciones
     */
    closeNotifications() {
        const alert = document.getElementById('personal-inline-alert');
        if (alert) {
            if (this.messageTimeout) clearTimeout(this.messageTimeout);
            alert.classList.add('opacity-0');
            setTimeout(() => { if (alert.parentElement) alert.remove(); }, 400);
        }
    }
};

// =====================================
// INICIALIZACIÓN DEL MÓDULO
// =====================================
document.addEventListener('DOMContentLoaded', () => {
    // Solo inicializar si estamos en la página de personal
    if (document.querySelector('form[id^="rol-form-"]') || document.querySelector('table')) {
        PersonalModule.init();
    }
});

// Exportar funciones globalmente para uso en onclick
window.toggleEdit = (id) => PersonalModule.toggleEdit(id);
window.submitForm = (id) => PersonalModule.submitForm(id);
window.openPersonalConfirmModal = (tipo, id, nombre) => PersonalModule.openPersonalConfirmModal(tipo, id, nombre);
window.closePersonalConfirmModal = () => PersonalModule.closePersonalConfirmModal();
window.submitPersonalEstado = (form) => PersonalModule.submitPersonalEstado(form);