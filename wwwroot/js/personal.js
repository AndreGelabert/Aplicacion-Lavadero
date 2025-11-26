/* ================================================
 * PERSONAL.JS - FUNCIONALIDAD PÁGINA PERSONAL
 * ================================================
 */

(function () {
    'use strict';

    // ===================== Estado interno =====================
    let tableMsgTimeout = null;
    let searchTimeout = null;
    let currentSearchTerm = '';
    let isEditing = false;

    // ===================== Inicialización del módulo =====================
    window.PageModules = window.PageModules || {};
    window.PageModules.personal = { init: initializePersonalPage };

    document.addEventListener('DOMContentLoaded', () => {
        console.log('[Personal] Inicializando módulo...');
        try {
            window.PageModules?.personal?.init();
        } catch (e) {
            console.error('[Personal] Error en init:', e);
            initializePersonalPage();
        }
    });

    function initializePersonalPage() {
        console.log('[Personal] Setup iniciado');

        // Limpieza agresiva
        cleanupOldNotifications();

        setupSearchWithDebounce();
        setupFilterFormSubmit();
        setupClearFiltersButton();
        setupNotificationHandling?.();
        setupRoleEditing();
        window.CommonUtils?.setupDefaultFilterForm();

        // Limpiar notificaciones al salir de la página
        setupPageUnloadCleanup();

        console.log('[Personal] Setup completado');
    }

    /* ---------------------------------------------------------
     * LIMPIEZA DE NOTIFICACIONES
     * --------------------------------------------------------- */

    function cleanupOldNotifications() {
        const container = document.getElementById('personal-messages-container');
        if (container) {
            container.remove();
        }

        const orphanAlert = document.getElementById('personal-inline-alert');
        if (orphanAlert) {
            orphanAlert.remove();
        }

        document.querySelectorAll('.personal-inline-alert').forEach(el => {
            el.remove();
        });

        sessionStorage.setItem('personal-notifications-cleaned', Date.now().toString());

        if (tableMsgTimeout) {
            clearTimeout(tableMsgTimeout);
            tableMsgTimeout = null;
        }
    }

    function setupPageUnloadCleanup() {
        window.addEventListener('beforeunload', () => {
            cleanupOldNotifications();
        });

        document.addEventListener('visibilitychange', () => {
            if (document.hidden) {
                cleanupOldNotifications();
            }
        });

        window.addEventListener('pagehide', () => {
            cleanupOldNotifications();
        });

        window.addEventListener('blur', () => {
            setTimeout(() => cleanupOldNotifications(), 500);
        });
    }

    /* ---------------------------------------------------------
     * BÚSQUEDA (DEBOUNCE)
     * --------------------------------------------------------- */

    function setupSearchWithDebounce() {
        const searchInput = document.getElementById('simple-search');
        if (!searchInput) return;

        const cloned = searchInput.cloneNode(true);
        searchInput.parentNode.replaceChild(cloned, searchInput);
        currentSearchTerm = cloned.value?.trim() || '';

        cloned.addEventListener('input', function () {
            const term = this.value.trim();
            if (searchTimeout) clearTimeout(searchTimeout);

            if (term === '') {
                currentSearchTerm = '';
                reloadPersonalTable(1);
                return;
            }

            searchTimeout = setTimeout(() => {
                performServerSearch(term);
            }, 500);
        });
    }

    function performServerSearch(searchTerm) {
        currentSearchTerm = searchTerm;

        const params = buildFilterParams();
        const { sortBy, sortOrder } = getCurrentSort();
        params.set('searchTerm', searchTerm);
        params.set('pageNumber', '1');
        params.set('sortBy', sortBy);
        params.set('sortOrder', sortOrder);

        loadTablePartial('/Personal/SearchPartial?' + params.toString());
    }

    /* ---------------------------------------------------------
     * ORDENAMIENTO TABLA
     * --------------------------------------------------------- */

    function getCurrentSort() {
        return {
            sortBy: document.getElementById('current-sort-by')?.value || 'NombreCompleto',
            sortOrder: document.getElementById('current-sort-order')?.value || 'asc'
        };
    }

    window.sortTable = function (sortBy) {
        const currentSortBy = document.getElementById('current-sort-by')?.value || 'NombreCompleto';
        const currentSortOrder = document.getElementById('current-sort-order')?.value || 'asc';

        const newOrder = (currentSortBy === sortBy)
            ? (currentSortOrder === 'asc' ? 'desc' : 'asc')
            : 'asc';

        setHiddenValue('current-sort-by', sortBy);
        setHiddenValue('current-sort-order', newOrder);
        reloadPersonalTable(1);
    };

    /* ---------------------------------------------------------
     * FILTROS Y RECARGA TABLA
     * --------------------------------------------------------- */

    function buildFilterParams() {
        const form = document.getElementById('filterForm');
        const params = new URLSearchParams();
        if (!form) return params;

        const fd = new FormData(form);
        for (const [k, v] of fd.entries()) params.append(k, v);

        return params;
    }

    function reloadPersonalTable(page, callback) {
        const params = buildFilterParams();
        params.set('pageNumber', String(page));

        const { sortBy, sortOrder } = getCurrentSort();
        params.set('sortBy', sortBy);
        params.set('sortOrder', sortOrder);

        let url;
        if (currentSearchTerm && currentSearchTerm.trim().length > 0) {
            params.set('searchTerm', currentSearchTerm.trim());
            url = '/Personal/SearchPartial?' + params.toString();
        } else {
            url = '/Personal/TablePartial?' + params.toString();
        }

        loadTablePartial(url, callback);
    }

    function loadTablePartial(url, callback) {
        const antiCache = (url.includes('?') ? '&' : '?') + '_=' + Date.now();

        fetch(url + antiCache, {
            headers: {
                'X-Requested-With': 'XMLHttpRequest',
                'Cache-Control': 'no-cache'
            },
            cache: 'no-store'
        })
            .then(r => r.text())
            .then(html => {
                const container = document.getElementById('personal-table-container');
                if (container) container.innerHTML = html;

                const cp = document.getElementById('current-page-value')?.value;
                if (cp && container) container.dataset.currentPage = cp;

                window.CommonUtils?.setupDefaultFilterForm?.();
                if (typeof initDropdowns === 'function') initDropdowns();

                if (typeof callback === 'function') callback();
            })
            .catch(err => {
                console.error('loadTablePartial error:', err);
                showTableMessage('error', 'Error al cargar los datos.');
            });
    }

    window.changePage = (page) => reloadPersonalTable(page);

    window.getCurrentTablePage = function () {
        return parseInt(document.getElementById('personal-table-container')?.dataset.currentPage || '1');
    };

    /* ---------------------------------------------------------
     * FILTROS
     * --------------------------------------------------------- */

    function setupFilterFormSubmit() {
        const form = document.getElementById('filterForm');
        if (!form || form.dataset.submitSetup === 'true') return;

        console.log('[Personal] Configurando submit de filtros');

        form.addEventListener('submit', (e) => {
            e.preventDefault();
            e.stopPropagation();

            //   Prevenir doble submit
            if (form.dataset.submitting === 'true') {
                console.log('[Personal] Filtros ya en proceso, omitiendo');
                return;
            }

            console.log('[Personal] Filtros submit triggered');

            form.dataset.submitting = 'true';

            const pg = form.querySelector('input[name="pageNumber"]');
            if (pg) pg.value = '1';

            const searchInput = document.getElementById('simple-search');
            if (searchInput) currentSearchTerm = searchInput.value.trim();

            const dropdown = document.getElementById('filterDropdown');
            if (dropdown) dropdown.classList.add('hidden');

            reloadPersonalTable(1, () => {
                showTableMessage('info', 'Filtros aplicados.');
                form.dataset.submitting = 'false'; //   Permitir nuevos filtros
            });

            if (history.replaceState) history.replaceState(null, '', window.location.pathname);
        });

        form.dataset.submitSetup = 'true';
        console.log('[Personal] Submit de filtros configurado');
    }

    function setupClearFiltersButton() {
        const btn =
            document.getElementById('clearFiltersBtn') ||
            document.querySelector('[data-action="clear-filters"]') ||
            document.querySelector('[data-clear="filters"]');

        if (!btn || btn.dataset.setup === 'true') return;

        btn.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            clearPersonalFilters();
        });

        btn.dataset.setup = 'true';
    }

    window.clearPersonalFilters = function () {
        console.log('[Personal] Limpiando filtros');

        const form = document.getElementById('filterForm');
        if (!form) return;

        //   Prevenir doble ejecución
        if (form.dataset.clearing === 'true') {
            console.log('[Personal] Ya se están limpiando filtros, omitiendo');
            return;
        }

        form.dataset.clearing = 'true';

        try {
            if (typeof window.clearAllFilters === 'function') {
                window.clearAllFilters();
            } else {
                form.querySelectorAll('input[name="estados"][type="checkbox"]').forEach(cb => cb.checked = false);
                form.querySelector('input[name="estados"][value="Activo"]')?.setAttribute('checked', 'checked');
            }
        } catch { }

        form.querySelectorAll('input[name="roles"][type="checkbox"]').forEach(cb => cb.checked = false);

        const searchInput = document.getElementById('simple-search');
        if (searchInput) searchInput.value = '';

        currentSearchTerm = '';

        if (searchTimeout) {
            clearTimeout(searchTimeout);
            searchTimeout = null;
        }

        setHiddenValue('pageNumber', '1');
        setHiddenValue('current-sort-by', 'NombreCompleto');
        setHiddenValue('current-sort-order', 'asc');

        if (history.replaceState) history.replaceState({}, document.title, '/Personal/Index');

        const dropdown = document.getElementById('filterDropdown');
        if (dropdown) dropdown.classList.add('hidden');

        window.CommonUtils?.setupDefaultFilterForm?.();
        if (typeof initDropdowns === 'function') initDropdowns();

        reloadPersonalTable(1, () => {
            showTableMessage('info', 'Filtros restablecidos.');
            form.dataset.clearing = 'false'; //   Permitir nueva limpieza
        });
    };

    function setHiddenValue(id, value) {
        const el = document.getElementById(id);
        if (el) el.value = value;
    }

    /* ---------------------------------------------------------
     * EDICIÓN DE ROLES
     * --------------------------------------------------------- */

    let roleEditingSetup = false; //   Flag para evitar múltiples registros

    function setupRoleEditing() {
        //   Solo configurar una vez
        if (roleEditingSetup) {
            console.log('[Personal] setupRoleEditing ya configurado, omitiendo');
            return;
        }

        console.log('[Personal] Configurando edición de roles');

        document.addEventListener('click', (event) => {
            let isClickInside = false;
            const forms = document.querySelectorAll('form[id^="rol-form-"]');

            forms.forEach((form) => {
                if (form.contains(event.target) || event.target.closest('button[onclick^="toggleEdit"]')) {
                    isClickInside = true;
                }
            });

            if (!isClickInside && isEditing) {
                reloadPersonalTable(getCurrentTablePage());
                isEditing = false;
            }
        });

        roleEditingSetup = true;
    }

    window.toggleEdit = function (id) {
        const rolText = document.getElementById('rol-text-' + id);
        const rolForm = document.getElementById('rol-form-' + id);
        if (!rolText || !rolForm) return;

        if (rolText.classList.contains('hidden')) {
            rolText.classList.remove('hidden');
            rolForm.classList.add('hidden');
            isEditing = false;
        } else {
            rolText.classList.add('hidden');
            rolForm.classList.remove('hidden');
            isEditing = true;
        }
    };

    window.submitForm = function (id) {
        const form = document.getElementById('rol-form-' + id);
        if (!form) return;

        //   Prevenir doble submit
        if (form.dataset.submitting === 'true') {
            console.log('[Personal] Formulario ya en proceso de envío, omitiendo');
            return;
        }

        form.dataset.submitting = 'true';

        const formData = new FormData(form);

        showTableMessage('info', 'Actualizando rol...', 3000);

        fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(response => response.json())
            .then(data => {
                form.dataset.submitting = 'false'; //   Permitir nuevos envíos

                if (data.success) {
                    showTableMessage('success', data.message || 'Rol actualizado correctamente.');
                    setTimeout(() => {
                        reloadPersonalTable(getCurrentTablePage());
                        isEditing = false;
                    }, 800);
                } else {
                    showTableMessage('error', data.message || 'Error al actualizar el rol.');
                }
            })
            .catch(() => {
                form.dataset.submitting = 'false'; //   Permitir reintentos
                showTableMessage('error', 'Error de comunicación con el servidor.');
            });
    };

    /* ---------------------------------------------------------
     * MODALES
     * --------------------------------------------------------- */

    function abrirModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        modal.classList.remove('hidden');
        modal.classList.add('flex');
        modal.setAttribute('aria-hidden', 'false');
        document.body.style.overflow = 'hidden';
    }

    function cerrarModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        modal.classList.add('hidden');
        modal.classList.remove('flex');
        modal.setAttribute('aria-hidden', 'true');
        document.body.style.overflow = '';
    }

    window.openPersonalConfirmModal = function (tipoAccion, id, nombre) {
        console.log('[Personal] Abriendo modal:', tipoAccion, id, nombre);

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
            msg.innerHTML = '¿Confirma desactivar al empleado <strong>' + escapeHtml(nombre) + '</strong>?';
            form.action = '/Personal/DeactivateEmployee';

            submitBtn.textContent = 'Desactivar';
            submitBtn.className =
                'py-2 px-3 text-sm font-medium text-center text-white bg-red-600 rounded-lg ' +
                'hover:bg-red-700 focus:ring-4 focus:outline-none focus:ring-red-300 ' +
                'dark:bg-red-500 dark:hover:bg-red-600 dark:focus:ring-red-900';

            iconWrapper.className =
                'w-12 h-12 rounded-full bg-red-100 dark:bg-red-900 p-2 flex items-center justify-center mx-auto mb-3.5';

            icon.setAttribute('fill', 'currentColor');
            icon.setAttribute('viewBox', '0 0 20 20');
            icon.setAttribute('class', 'w-8 h-8 text-red-600 dark:text-red-400');
            icon.innerHTML =
                `<path fill-rule="evenodd"
                        d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-11.293a1 
                        1 0 00-1.414-1.414L10 7.586 7.707 5.293a1 
                        1 0 00-1.414 1.414L8.586 10l-2.293 
                        2.293a1 1 0 001.414 1.414L10 
                        12.414l2.293 2.293a1 1 0 001.414-1.414L11.414 
                        10l2.293-2.293z"
                        clip-rule="evenodd"/>`;
        } else {
            title.textContent = 'Reactivar Empleado';
            msg.innerHTML = '¿Confirma reactivar al empleado <strong>' + escapeHtml(nombre) + '</strong>?';
            form.action = '/Personal/ReactivateEmployee';

            submitBtn.textContent = 'Reactivar';
            submitBtn.className =
                'py-2 px-3 text-sm font-medium text-center text-white bg-green-600 rounded-lg ' +
                'hover:bg-green-700 focus:ring-4 focus:outline-none focus:ring-green-300 ' +
                'dark:bg-green-500 dark:hover:bg-green-600 dark:focus:ring-green-900';

            iconWrapper.className =
                'w-12 h-12 rounded-full bg-green-100 dark:bg-green-900 p-2 flex items-center justify-center mx-auto mb-3.5';

            icon.setAttribute('fill', 'currentColor');
            icon.setAttribute('viewBox', '0 0 24 24');
            icon.setAttribute('class', 'w-8 h-8 text-green-500 dark:text-green-400');

            icon.innerHTML =
                `<path fill-rule="evenodd"
                        d="M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 
                        4.365 9.75 9.75-4.365 9.75-9.75 
                        9.75S2.25 17.385 
                        2.25 12Zm13.36-1.814a.75.75 0 1 0-1.22-.872l-3.236 
                        4.53L9.53 12.22a.75.75 0 0 0-1.06 
                        1.06l2.25 2.25a.75.75 0 0 0 
                        1.14-.094l3.75-5.25Z"
                        clip-rule="evenodd"/>`;
        }

        abrirModal('personalConfirmModal');
    };

    window.closePersonalConfirmModal = function () {
        cerrarModal('personalConfirmModal');
    };

    window.submitPersonalEstado = function (form) {
        console.log('[Personal] Submit estado, action:', form.action);

        //   Prevenir doble submit
        if (form.dataset.submitting === 'true') {
            console.log('[Personal] Formulario ya en proceso de envío, omitiendo');
            return false;
        }

        form.dataset.submitting = 'true';

        const formData = new FormData(form);

        fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(r => {
                console.log('[Personal] Response status:', r.status, r.ok);
                if (!r.ok) throw new Error('Error estado');

                window.closePersonalConfirmModal();

                const isDeactivate = form.action.includes('DeactivateEmployee');
                const message = isDeactivate
                    ? 'Empleado desactivado correctamente.'
                    : 'Empleado reactivado correctamente.';

                showTableMessage('success', message);

                const pg = getCurrentTablePage();

                setTimeout(() => {
                    reloadPersonalTable(pg);
                    form.dataset.submitting = 'false'; //  Permitir nuevos envíos
                }, 800);
            })
            .catch(err => {
                console.error('[Personal] Error en submitPersonalEstado:', err);
                form.dataset.submitting = 'false'; //   Permitir reintentos
                window.closePersonalConfirmModal();
                showTableMessage('error', 'Error al procesar la solicitud.');
            });

        return false;
    };

    /* ---------------------------------------------------------
     * NOTIFICACIONES
     * --------------------------------------------------------- */

    function setupNotificationHandling() {
        showServerNotifications();
    }

    function showServerNotifications() {
    const successMeta = document.querySelector('meta[name="success-message"]');
        const errorMeta = document.querySelector('meta[name="error-message"]');

        const successMessage = successMeta?.getAttribute('content');
   const errorMessage = errorMeta?.getAttribute('content');

        if (successMessage) {
    showTableMessage('success', successMessage);
     //   Eliminar meta tag después de mostrar
            successMeta?.remove();
        } else if (errorMessage) {
            showTableMessage('error', errorMessage);
//   Eliminar meta tag después de mostrar
       errorMeta?.remove();
        }
    }

    function getMetaContent(name) {
        const meta = document.querySelector(`meta[name="${name}"]`);
        return meta ? meta.getAttribute('content') : null;
    }

    function showTableMessage(type, msg, disappearMs = 5000) {
        console.log('[Personal] showTableMessage called:', type, msg);

        const lastClean = sessionStorage.getItem('personal-notifications-cleaned');
        if (lastClean) {
            const timeSinceClean = Date.now() - parseInt(lastClean);
            if (timeSinceClean < 100) {
                console.log('[Personal] Limpieza reciente detectada, esperando...');
                setTimeout(() => showTableMessageInternal(type, msg, disappearMs), 150);
                return;
            }
        }

        showTableMessageInternal(type, msg, disappearMs);
    }

    function showTableMessageInternal(type, msg, disappearMs) {
        console.log('[Personal] showTableMessageInternal:', type, msg);

        let container = document.getElementById('personal-messages-container');

        if (!container) {
            console.log('[Personal] Creando contenedor de mensajes');

            container = document.createElement('div');
            container.id = 'personal-messages-container';
            container.className = 'mb-4 px-4';

            const tableWrapper = document.getElementById('personal-table-container');

            if (tableWrapper?.parentNode) {
                tableWrapper.parentNode.insertBefore(container, tableWrapper);
            } else {
                const section = document.querySelector('section');
                if (section) {
                    const header = section.querySelector('.p-2.flex.justify-between');
                    if (header && header.nextSibling) {
                        section.insertBefore(container, header.nextSibling);
                    } else {
                        section.insertBefore(container, section.firstChild);
                    }
                } else {
                    document.body.prepend(container);
                }
            }
        }

        container.style.display = 'block';

        if (tableMsgTimeout) {
            clearTimeout(tableMsgTimeout);
            tableMsgTimeout = null;
        }

        const color =
            type === 'success'
                ? { bg: 'green-50', text: 'green-800', darkText: 'green-400', border: 'green-300' }
                : type === 'info'
                    ? { bg: 'blue-50', text: 'blue-800', darkText: 'blue-400', border: 'blue-300' }
                    : { bg: 'red-50', text: 'red-800', darkText: 'red-400', border: 'red-300' };

        container.innerHTML = `
            <div id="personal-inline-alert"
                class="personal-inline-alert opacity-100 transition-opacity duration-700
                    p-4 mb-4 text-sm rounded-lg border
                    bg-${color.bg} text-${color.text} border-${color.border}
                    dark:bg-gray-800 dark:text-${color.darkText}">
                ${escapeHtml(msg)}
            </div>`;

        const alertEl = document.getElementById('personal-inline-alert');
        if (!alertEl) return;

        try {
            alertEl.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        } catch { }

        tableMsgTimeout = setTimeout(() => {
            alertEl.classList.add('opacity-0');
            setTimeout(() => {
                if (alertEl && alertEl.parentElement) {
                    alertEl.remove();

                    const cont = document.getElementById('personal-messages-container');
                    if (cont && cont.children.length === 0) {
                        cont.remove();
                    }
                }
            }, 750);
        }, disappearMs);

        sessionStorage.setItem('personal-notification-active', 'true');

        setTimeout(() => {
            sessionStorage.removeItem('personal-notification-active');
        }, disappearMs + 800);
    }

    function escapeHtml(str) {
        if (!str) return '';
        return String(str).replace(/[&<>"']/g, c => ({
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#39;'
        }[c]));
    }

})();
