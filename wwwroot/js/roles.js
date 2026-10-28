/**
 * ================================================
 * ROLES.JS - FUNCIONALIDAD PÁGINA ROLES Y PERMISOS
 * ================================================
 * Responsabilidades:
 *  - Búsqueda con debounce, filtro por estado y ordenamiento.
 *  - Recarga parcial de la tabla con paginación.
 *  - Apertura del formulario inline de creación/edición y envío AJAX.
 *  - Selección masiva de permisos y notificaciones tipo toast.
 *
 * Solo se exponen las funciones invocadas desde el HTML.
 */

(function () {
    'use strict';

    // ===================== Estado interno =====================
    let searchTimeout = null;
    let tableRequestSeq = 0;
    let formRequestSeq = 0;
    let rolFormIsEditing = false;

    // ===================== Inicialización del módulo =====================
    window.PageModules = window.PageModules || {};
    window.PageModules.roles = { init: initRolesPage };

    document.addEventListener('DOMContentLoaded', () => {
        try { window.PageModules?.roles?.init(); }
        catch (error) { console.error('roles init error:', error); }
    });

    /**
     * Inicializa el comportamiento principal de la página de Roles.
     */
    function initRolesPage() {
        setupSearchWithDebounce();
        setupFilters();
        setupPermissionDelegation();
        setupDeleteConfirmation();
        setupInlineFormAccordion();
        syncSortSelect();
    }

    /**
     * Carga el formulario de creación al abrir el acordeón vacío.
     */
    function setupInlineFormAccordion() {
        const { button } = getRolFormParts();
        if (!button) return;

        button.addEventListener('click', () => {
            const { body, container } = getRolFormParts();
            if (!body || !container || body.classList.contains('hidden')) return;
            if (container.innerHTML.trim()) return;

            if (document.getElementById('btn-nuevo-rol')) {
                window.openRolForm();
            } else {
                container.innerHTML = '<p class="p-4 text-sm text-gray-500 dark:text-gray-400">Seleccione «Editar» en un rol de la tabla para modificar sus datos.</p>';
            }
        });
    }

    // ===================== Búsqueda (debounce) =====================

    /**
     * Configura la búsqueda de roles con debouncing.
     */
    function setupSearchWithDebounce() {
        const searchInput = document.getElementById('rol-search');
        if (!searchInput) return;

        // Clonar para eliminar handlers previos si el módulo se inicializa dos veces.
        const cloned = searchInput.cloneNode(true);
        searchInput.parentNode.replaceChild(cloned, searchInput);

        cloned.addEventListener('input', function () {
            const term = this.value.trim();

            if (searchTimeout) {
                clearTimeout(searchTimeout);
                searchTimeout = null;
            }

            if (term === '') {
                reloadRolTable(1);
                return;
            }

            searchTimeout = setTimeout(() => {
                searchTimeout = null;
                reloadRolTable(1);
            }, 400);
        });
    }

    // ===================== Filtros y orden =====================

    /**
     * Configura los selectores de estado y ordenamiento.
     */
    function setupFilters() {
        document.getElementById('rol-estado')?.addEventListener('change', () => reloadRolTable(1));

        document.getElementById('rol-sort')?.addEventListener('change', function () {
            const [sortBy, sortOrder] = (this.value || '').split('|');
            if (!sortBy || !sortOrder) return;

            setHiddenValue('current-sort-by', sortBy);
            setHiddenValue('current-sort-order', sortOrder);
            reloadRolTable(1);
        });
    }

    /**
     * Sincroniza el selector de orden con los valores actuales de la tabla.
     */
    function syncSortSelect() {
        const select = document.getElementById('rol-sort');
        if (!select) return;

        const sortBy = document.getElementById('current-sort-by')?.value || 'Nombre';
        const sortOrder = document.getElementById('current-sort-order')?.value || 'asc';
        const value = `${sortBy}|${sortOrder}`;
        const option = Array.from(select.options).find(opt => opt.value === value);
        if (option) select.value = value;
    }

    /**
     * Construye los parámetros de consulta de la tabla.
     * @returns {URLSearchParams}
     */
    function buildRolParams() {
        const params = new URLSearchParams();
        params.set('searchTerm', document.getElementById('rol-search')?.value.trim() || '');
        params.set('estado', document.getElementById('rol-estado')?.value || '');
        params.set('sortBy', document.getElementById('current-sort-by')?.value || 'Nombre');
        params.set('sortOrder', document.getElementById('current-sort-order')?.value || 'asc');
        return params;
    }

    // ===================== Recarga de la tabla =====================

    /**
     * Recarga la tabla de roles considerando búsqueda, filtros, orden y página.
     * @param {number} page
     */
    function reloadRolTable(page) {
        const params = buildRolParams();
        const searchTerm = params.get('searchTerm');
        params.set('pageNumber', String(page || 1));
        params.set('pageSize', '10');

        const url = (searchTerm ? '/Rol/SearchPartial?' : '/Rol/TablePartial?') + params.toString();
        return loadTablePartial(url);
    }

    /**
     * Carga la tabla parcial vía fetch y reemplaza el contenedor.
     * @param {string} url
     */
    function loadTablePartial(url) {
        const requestSeq = ++tableRequestSeq;
        const antiCache = (url.includes('?') ? '&' : '?') + '_=' + Date.now();

        return fetch(url + antiCache, {
            headers: { 'X-Requested-With': 'XMLHttpRequest', 'Cache-Control': 'no-cache' },
            cache: 'no-store'
        })
            .then(response => {
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                return response.text();
            })
            .then(html => {
                if (requestSeq !== tableRequestSeq) return;

                const container = document.getElementById('rol-table-container');
                if (!container) return;

                container.innerHTML = html;
                const currentPage = document.getElementById('current-page-value')?.value;
                if (currentPage) container.dataset.currentPage = currentPage;
                syncSortSelect();
            })
            .catch(error => {
                if (requestSeq !== tableRequestSeq) return;
                console.error('loadTablePartial error:', error);
                showRolToast('Error al cargar los roles.', 'error');
            });
    }

    /**
     * Cambia de página la tabla.
     * @param {number} page
     */
    window.changeRolPage = function (page) {
        reloadRolTable(page);
    };

    /**
     * Ordena la tabla por la columna indicada, alternando la dirección.
     * @param {string} sortBy
     */
    window.sortRolTable = function (sortBy) {
        const currentSortBy = document.getElementById('current-sort-by')?.value || 'Nombre';
        const currentSortOrder = document.getElementById('current-sort-order')?.value || 'asc';
        const newOrder = currentSortBy === sortBy
            ? (currentSortOrder === 'asc' ? 'desc' : 'asc')
            : 'asc';

        setHiddenValue('current-sort-by', sortBy);
        setHiddenValue('current-sort-order', newOrder);
        reloadRolTable(1);
    };

    /**
     * Asigna el valor de un input hidden si existe.
     * @param {string} id
     * @param {string} value
     */
    function setHiddenValue(id, value) {
        const element = document.getElementById(id);
        if (element) element.value = value;
    }

    // ===================== Formulario inline crear / editar =====================

    /**
     * Obtiene los elementos del acordeón y del contenedor del formulario.
     * @returns {{button: HTMLElement|null, body: HTMLElement|null, title: HTMLElement|null, container: HTMLElement|null}}
     */
    function getRolFormParts() {
        return {
            button: document.querySelector('[data-accordion-target="#accordion-flush-body-1"]'),
            body: document.getElementById('accordion-flush-body-1'),
            title: document.getElementById('rol-form-title'),
            container: document.getElementById('rol-form-container')
        };
    }

    /**
     * Abre el acordeón del formulario si está cerrado.
     */
    function expandRolForm() {
        const { button, body } = getRolFormParts();
        if (button && body && body.classList.contains('hidden')) {
            button.click();
        }
    }

    /**
     * Cierra el acordeón del formulario si está abierto.
     */
    function collapseRolForm() {
        const { button, body } = getRolFormParts();
        if (button && body && !body.classList.contains('hidden')) {
            button.click();
        }
    }

    /**
     * Abre el formulario inline y carga el rol solicitado.
     * @param {string} [id] ID del rol a editar; vacío para crear.
     */
    window.openRolForm = function (id) {
        const { title, container } = getRolFormParts();
        if (!title || !container) return;

        const editando = Boolean(id);
        rolFormIsEditing = editando;
        const requestSeq = ++formRequestSeq;

        title.textContent = editando ? 'Modificar Rol' : 'Nuevo Rol';
        container.innerHTML = '';
        expandRolForm();

        const url = editando
            ? '/Rol/FormPartial?id=' + encodeURIComponent(id)
            : '/Rol/FormPartial';

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(response => {
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                return response.text();
            })
            .then(html => {
                if (requestSeq !== formRequestSeq) return;

                container.innerHTML = html;
                // El contenido cambia mientras el acordeón sigue abierto; volver a
                // alternarlo permite que Flowbite recalcule el alto del panel.
                const { body } = getRolFormParts();
                if (body && !body.classList.contains('hidden')) {
                    collapseRolForm();
                    expandRolForm();
                }
                updateSelectAllState();
                document.getElementById('Nombre')?.focus();
                container.scrollIntoView({ behavior: 'smooth', block: 'start' });
            })
            .catch(error => {
                if (requestSeq !== formRequestSeq) return;
                console.error('openRolForm error:', error);
                window.closeRolForm();
                showRolToast('No se pudo cargar el formulario del rol.', 'error');
            });
    };

    /**
     * Cierra el formulario inline y limpia su contenido.
     */
    window.closeRolForm = function () {
        formRequestSeq++;
        rolFormIsEditing = false;
        const { container } = getRolFormParts();
        if (container) container.innerHTML = '';
        collapseRolForm();
    };

    /**
     * Cancela la edición volviendo al formulario de registro.
     */
    window.cancelRolForm = function () {
        if (rolFormIsEditing) {
            window.openRolForm();
            return;
        }

        window.closeRolForm();
    };

    /**
     * Muestra un modal agregando la clase flex.
     * @param {HTMLElement} modal
     */
    function showModal(modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
        modal.setAttribute('aria-hidden', 'false');
    }

    /**
     * Oculta un modal quitando la clase flex.
     * @param {HTMLElement} modal
     */
    function hideModal(modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
        modal.setAttribute('aria-hidden', 'true');
    }

    // ===================== Envío AJAX del formulario =====================

    /**
     * Envía el formulario de rol por AJAX.
     * @param {HTMLFormElement} form
     * @param {SubmitEvent} event
     * @returns {boolean}
     */
    window.submitRolAjax = function (form, event) {
        if (event) event.preventDefault();
        if (!form) return false;

        clearFormErrors(form);

        const submitButton = form.querySelector('[type="submit"]');
        if (submitButton) submitButton.disabled = true;

        fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            credentials: 'same-origin'
        })
            .then(async response => {
                const data = await response.json().catch(() => null);
                return { response, data };
            })
            .then(({ response, data }) => {
                if (!response.ok || !data) {
                    showFormMessage('Error al procesar la solicitud.', 'error');
                    return;
                }

                if (data.success) {
                    window.closeRolForm();
                    showRolToast(data.message || 'Operación exitosa.', 'success');

                    const searchInput = document.getElementById('rol-search');
                    if (searchInput) searchInput.value = '';

                    reloadRolTable(1);
                    return;
                }

                showFormMessage(data.message || 'Revise los errores del formulario.', 'error');
                applyFormErrors(form, data.errors);
            })
            .catch(error => {
                console.error('submitRolAjax error:', error);
                showFormMessage('Error al procesar la solicitud.', 'error');
            })
            .finally(() => {
                if (submitButton && document.body.contains(submitButton)) {
                    submitButton.disabled = false;
                }
            });

        return false;
    };

    /**
     * Limpia los mensajes y estilos de error del formulario.
     * @param {HTMLFormElement} form
     */
    function clearFormErrors(form) {
        form.querySelectorAll('[data-valmsg-for]').forEach(span => {
            span.textContent = '';
        });

        form.querySelectorAll('.border-red-500').forEach(element => {
            element.classList.remove('border-red-500', 'focus:border-red-500', 'focus:ring-red-500');
        });

        const message = document.getElementById('rol-form-message');
        if (message) {
            message.textContent = '';
            message.classList.add('hidden');
            message.classList.remove('bg-red-100', 'text-red-800', 'bg-green-100', 'text-green-800');
        }
    }

    /**
     * Muestra el mensaje global del formulario.
     * @param {string} text
     * @param {string} type
     */
    function showFormMessage(text, type) {
        const message = document.getElementById('rol-form-message');
        if (!message) return;

        message.textContent = text;
        message.classList.remove('hidden', 'bg-red-100', 'text-red-800', 'bg-green-100', 'text-green-800');

        if (type === 'success') {
            message.classList.add('bg-green-100', 'text-green-800');
        } else {
            message.classList.add('bg-red-100', 'text-red-800');
        }
    }

    /**
     * Aplica los errores devueltos por el servidor a los campos del formulario.
     * @param {HTMLFormElement} form
     * @param {Object<string, string>} errors
     */
    function applyFormErrors(form, errors) {
        if (!errors) return;

        Object.entries(errors).forEach(([field, errorMessage]) => {
            const span = form.querySelector(`[data-valmsg-for="${field}"]`);
            if (span) span.textContent = errorMessage;

            const input = form.querySelector(`[name="${field}"]`);
            if (input && input.type !== 'checkbox' && input.type !== 'hidden') {
                input.classList.add('border-red-500', 'focus:border-red-500', 'focus:ring-red-500');
            }
        });
    }

    // ===================== Permisos =====================

    /**
     * Obtiene los checkboxes de permisos habilitados.
     * @returns {HTMLInputElement[]}
     */
    function getPermissionBoxes() {
        return Array.from(
            document.querySelectorAll('#rol-form input[type="checkbox"][name="Permisos"]:not(:disabled)')
        );
    }

    /**
     * Marca o desmarca todos los permisos habilitados.
     * @param {boolean} checked
     */
    window.seleccionarTodosPermisos = function (checked) {
        getPermissionBoxes().forEach(checkbox => { checkbox.checked = checked; });
        updateSelectAllState();
    };

    /**
     * Actualiza el estado del checkbox "Seleccionar todos".
     */
    function updateSelectAllState() {
        const selectAll = document.getElementById('perm-select-all');
        if (!selectAll) return;

        const boxes = getPermissionBoxes();
        const checkedCount = boxes.filter(checkbox => checkbox.checked).length;

        selectAll.checked = boxes.length > 0 && checkedCount === boxes.length;
        selectAll.indeterminate = checkedCount > 0 && checkedCount < boxes.length;
    }

    /**
     * Escucha cambios individuales de permisos para sincronizar el selector global.
     */
    function setupPermissionDelegation() {
        document.addEventListener('change', event => {
            const target = event.target;
            if (!(target instanceof HTMLInputElement)) return;
            if (target.name !== 'Permisos' || target.disabled) return;
            updateSelectAllState();
        });
    }

    // ===================== Notificaciones =====================

    /**
     * Muestra una notificación toast.
     * @param {string} message
     * @param {string} type "success" | "error"
     */
    function showRolToast(message, type) {
        const container = document.getElementById('rol-toast-container');
        const template = document.getElementById('rol-toast-template');
        if (!container || !template) return;

        const toast = template.content.firstElementChild.cloneNode(true);
        const text = toast.querySelector('[data-toast-text]');
        if (text) text.textContent = message;

        if (type === 'success') {
            toast.classList.add('text-green-800', 'bg-green-100', 'dark:bg-green-800', 'dark:text-green-200');
        } else {
            toast.classList.add('text-red-800', 'bg-red-100', 'dark:bg-red-800', 'dark:text-red-200');
        }

        container.appendChild(toast);
        setTimeout(() => toast.remove(), 4000);
    }

    window.showRolToast = showRolToast;

    // ===================== Eliminación =====================

    /**
     * Abre el modal de confirmación de eliminación o muestra el bloqueo correspondiente.
     * @param {HTMLButtonElement} button
     */
    window.openRolDeleteModal = function (button) {
        if (!button) return;

        const id = button.dataset.id || '';
        const nombre = button.dataset.nombre || '';
        const protegido = button.dataset.protegido === 'true';
        const usuarios = parseInt(button.dataset.usuarios || '0', 10) || 0;

        if (protegido) {
            showRolBlockModal('Acción no permitida: Los roles predeterminados del sistema no pueden ser eliminados');
            return;
        }

        if (usuarios > 0) {
            showRolBlockModal(
                'No es posible eliminar el rol: Existen ' + usuarios +
                ' empleados que tienen este rol asignado. Reasígnelos a otro rol antes de continuar'
            );
            return;
        }

        const idInput = document.getElementById('rol-delete-id');
        const text = document.getElementById('rol-delete-text');
        const modal = document.getElementById('rol-delete-modal');

        if (idInput) idInput.value = id;
        if (text) text.textContent = `¿Está seguro de que desea eliminar el rol ${nombre}? Esta operación es irreversible.`;
        if (modal) showModal(modal);
    };

    /**
     * Cierra el modal de confirmación de eliminación.
     */
    window.closeRolDeleteModal = function () {
        const modal = document.getElementById('rol-delete-modal');
        if (modal) hideModal(modal);

        const idInput = document.getElementById('rol-delete-id');
        if (idInput) idInput.value = '';
    };

    /**
     * Muestra el modal de bloqueo con el mensaje indicado.
     * @param {string} mensaje
     */
    window.showRolBlockModal = function (mensaje) {
        const text = document.getElementById('rol-block-message');
        const modal = document.getElementById('rol-block-modal');

        if (text) text.textContent = mensaje || '';
        if (modal) showModal(modal);
    };

    /**
     * Cierra el modal de bloqueo.
     */
    window.closeRolBlockModal = function () {
        const modal = document.getElementById('rol-block-modal');
        if (modal) hideModal(modal);
    };

    /**
     * Envía la confirmación de eliminación por AJAX.
     */
    function setupDeleteConfirmation() {
        const confirmButton = document.getElementById('rol-delete-confirm-btn');
        const form = document.getElementById('rol-delete-form');

        if (form) {
            form.addEventListener('submit', event => event.preventDefault());
        }

        if (!confirmButton || !form) return;

        confirmButton.addEventListener('click', event => {
            event.preventDefault();
            if (confirmButton.disabled) return;

            confirmButton.disabled = true;

            fetch('/Rol/EliminarRolAjax', {
                method: 'POST',
                body: new FormData(form),
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin'
            })
                .then(async response => {
                    const data = await response.json().catch(() => null);
                    return { response, data };
                })
                .then(({ response, data }) => {
                    if (!response.ok || !data) {
                        closeRolDeleteModal();
                        showRolToast('Error al eliminar el rol.', 'error');
                        return;
                    }

                    if (data.success) {
                        const container = document.getElementById('rol-table-container');
                        const currentPage = parseInt(container?.dataset.currentPage || '1', 10) || 1;

                        closeRolDeleteModal();
                        showRolToast(data.message || 'Rol eliminado exitosamente.', 'success');

                        reloadRolTable(currentPage).then(() => {
                            const emptyRow = document.querySelector('#rol-table-container tbody tr td[colspan="6"]');
                            if (emptyRow && currentPage > 1) {
                                reloadRolTable(1);
                            }
                        });
                        return;
                    }

                    closeRolDeleteModal();

                    const message = data.message || 'No se pudo eliminar el rol.';
                    const bloqueado = message.startsWith('No es posible eliminar el rol') ||
                        message.startsWith('Acción no permitida');

                    if (bloqueado) {
                        showRolBlockModal(message);
                    } else {
                        showRolToast(message, 'error');
                    }
                })
                .catch(error => {
                    console.error('setupDeleteConfirmation error:', error);
                    closeRolDeleteModal();
                    showRolToast('Error al eliminar el rol.', 'error');
                })
                .finally(() => {
                    confirmButton.disabled = false;
                });
        });
    }

})();
