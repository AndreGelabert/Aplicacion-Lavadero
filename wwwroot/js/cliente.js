/**
 * ================================================
 * CLIENTE.JS - FUNCIONALIDAD DE LA PÁGINA DE CLIENTES
 * ================================================
 * Responsabilidades:
 *  - Búsqueda con debounce y ordenamiento de tabla
 *  - Filtros y recarga parcial (tabla y formulario)
 *  - Formulario AJAX crear/actualizar
 *  - Selector dinámico de vehículos (estilo paquetes)
 *  - Gestión de activación/desactivación (modal de confirmación)
 *  - Creación rápida de vehículos desde modal
 */

(function () {
    'use strict';

    // ===================== Estado interno =====================
    let currentPage = 1;
    let currentSearchTerm = "";
    let currentSortBy = "Nombre";
    let currentSortOrder = "asc";
    let searchTimeout;
    let clienteMsgTimeout = null;
    let tableMsgTimeout = null;

    // Variables para gestión de vehículos
    let vehiculosSeleccionados = [];
    let vehiculosDisponibles = [];
    let vehiculoSeleccionadoDropdown = null;
    let vehiculosTemporales = []; // Vehículos creados en memoria (aún no guardados en BD)

    // ===================== Inicialización del módulo =====================
    window.PageModules = window.PageModules || {};
    window.PageModules.clientes = { init: initializeClientesPage };

    document.addEventListener('DOMContentLoaded', () => {
        try { window.PageModules?.clientes?.init(); }
        catch { initializeClientesPage(); }
    });

    /**
     * Inicializa el comportamiento principal de la página de Clientes
     */
    function initializeClientesPage() {
        setupInitialState();
        setupSearchWithDebounce();
        setupFilterFormSubmit();
        setupModals();
        setupAccordionListener(); // NUEVO: Escuchar apertura del acordeón
        setupDocumentoValidation();// Validación dinámica de número de documento
        setupFormatoDocumentoValidation();
        checkEditMode();
    }

    // ===================== Validación dinámica de documento =====================

    let tiposDocumentoFormatos = {}; // Cache de formatos de tipos de documento
    let tiposVehiculoFormatos = {}; // Cache de formatos de tipos de vehículo
    /**
     * Configura la validación dinámica del número de documento basada en el tipo seleccionado.
     */
    async function setupDocumentoValidation() {
        const nombreInput = document.getElementById('Nombre');
        if (nombreInput && !nombreInput.dataset.validationSetup) {
            const allowedRegex = /^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]*$/;
            const minLength = 3;

            nombreInput.addEventListener('input', function () {
                // Filtrar caracteres no permitidos
                if (!allowedRegex.test(this.value)) {
                    this.value = this.value.replace(/[^a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]/g, '');
                }

                // Validar longitud mínima
                if (this.value.trim().length > 0 && this.value.trim().length < minLength) {
                    this.setCustomValidity(`El nombre debe tener al menos ${minLength} letras`);
                } else {
                    this.setCustomValidity('');
                }
            });

            nombreInput.dataset.validationSetup = 'true';
        }
        const apellidoInput = document.getElementById('Apellido');
        if (apellidoInput && !apellidoInput.dataset.validationSetup) {
            const allowedRegex = /^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]*$/;
            const minLength = 3;

            apellidoInput.addEventListener('input', function () {
                if (!allowedRegex.test(this.value)) {
                    this.value = this.value.replace(/[^a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]/g, '');
                }

                if (this.value.trim().length > 0 && this.value.trim().length < minLength) {
                    this.setCustomValidity(`El apellido debe tener al menos ${minLength} letras`);
                } else {
                    this.setCustomValidity('');
                }
            });

            apellidoInput.dataset.validationSetup = 'true';
        }
        const emailInput = document.getElementById('Email');
        if (emailInput && !emailInput.dataset.validationSetup) {
            emailInput.addEventListener('input', function () {
                const emailValue = this.value.trim();

                if (emailValue.length === 0) {
                    this.setCustomValidity('');
                    return;
                }

                // Validar que tenga @ y al menos 3 caracteres después
                const atIndex = emailValue.indexOf('@');
                if (atIndex === -1) {
                    this.setCustomValidity('El email debe contener @');
                } else {
                    const afterAt = emailValue.substring(atIndex + 1);
                    if (afterAt.length < 3) {
                        this.setCustomValidity('Debe haber al menos 3 caracteres después de @');
                    } else {
                        // Validación básica de formato de email
                        const emailRegex = /^[^\s@]+@[^\s@]{3,}\.[^\s@]+$/;
                        if (!emailRegex.test(emailValue)) {
                            this.setCustomValidity('Formato de email inválido');
                        } else {
                            this.setCustomValidity('');
                        }
                    }
                }
            });

            emailInput.addEventListener('blur', function () {
                if (this.value.trim().length > 0) {
                    this.dispatchEvent(new Event('input'));
                }
            });

            emailInput.dataset.validationSetup = 'true';
        }
        // Cargar formatos de tipos de documento
        await loadTiposDocumentoFormatos();

        // Agregar listener al cambio de tipo de documento
        const tipoDocSelect = document.getElementById('TipoDocumento');
        if (tipoDocSelect) {
            tipoDocSelect.addEventListener('change', function () {
                updateDocumentoFormatoHint(this.value);
                validateDocumentoNumero();
            });

            // Inicializar con el valor actual
            if (tipoDocSelect.value) {
                updateDocumentoFormatoHint(tipoDocSelect.value);
            }
        }

        // Agregar validación en tiempo real al campo de número de documento
        const numeroDocInput = document.getElementById('NumeroDocumento');
        if (numeroDocInput) {
            numeroDocInput.addEventListener('input', validateDocumentoNumero);
            numeroDocInput.addEventListener('blur', validateDocumentoNumero);
        }
    }
    /**
 * Configura la validación del campo de formato de documento en tiempo real
 */
    function setupFormatoDocumentoValidation() {
        const formatoInput = document.getElementById('formatoTipoDocumento');

        if (!formatoInput) return;

        formatoInput.addEventListener('input', function (e) {
            const valor = this.value;
            const cursorPos = this.selectionStart;

            // Filtrar solo caracteres válidos: n, l, ., -
            const valorFiltrado = valor
                .split('')
                .filter(char => /[nNlL.\-]/.test(char))
                .join('')
                .toLowerCase(); // Convertir a minúsculas

            if (valor !== valorFiltrado) {
                this.value = valorFiltrado;
                // Ajustar posición del cursor
                const diff = valor.length - valorFiltrado.length;
                this.setSelectionRange(cursorPos - diff, cursorPos - diff);
            }
        });

        formatoInput.addEventListener('blur', function () {
            const valor = this.value.trim();

            if (valor.length > 0 && valor.length < 3) {
                this.setCustomValidity('El formato debe tener al menos 3 caracteres');
                this.reportValidity();
            } else {
                this.setCustomValidity('');
            }
        });
    }
    /**
     * Carga los formatos de todos los tipos de documento desde el servidor.
     */
    async function loadTiposDocumentoFormatos() {
        try {
            const response = await fetch('/TipoDocumento/ObtenerTiposConFormatos');
            const tipos = await response.json();

            tiposDocumentoFormatos = {};
            tipos.forEach(t => {
                tiposDocumentoFormatos[t.nombre] = {
                    formato: t.formato,
                    regex: t.regex
                };
            });
        } catch (error) {
            console.error('Error al cargar formatos de tipos de documento:', error);
        }
    }
    /**
    * Carga los formatos de todos los tipos de vehículodesde el servidor.
    */
    async function loadTiposVehiculoFormatos() {
        try {
            const response = await fetch('/TipoVehiculo/ObtenerTiposConFormatos');
            const tipos = await response.json();

            tiposVehiculoFormatos = {};
            tipos.forEach(t => {
                tiposVehiculoFormatos[t.nombre] = {
                    formato: t.formato,
                    regex: t.regex
                };
            });
        } catch (error) {
            console.error('Error al cargar formatos de tipos de vehículo:', error);
        }
    }
    /**
     * Actualiza el mensaje de ayuda del formato del documento.
     */
    function updateDocumentoFormatoHint(tipoDocumento) {
        const hintElement = document.getElementById('documento-formato-hint');
        const numeroDocInput = document.getElementById('NumeroDocumento');

        if (!hintElement) return;

        if (!tipoDocumento) {
            hintElement.textContent = 'Seleccione un tipo de documento';
            if (numeroDocInput) {
                numeroDocInput.removeAttribute('pattern');
                numeroDocInput.placeholder = 'Ingrese número';
            }
            return;
        }

        const tipoInfo = tiposDocumentoFormatos[tipoDocumento];

        if (tipoInfo && tipoInfo.formato) {
            hintElement.textContent = `Formato: ${tipoInfo.formato}`;
            if (numeroDocInput) {
                numeroDocInput.placeholder = tipoInfo.formato;
            }
        } else {
            hintElement.textContent = 'Ingrese el número de documento';
            if (numeroDocInput) {
                numeroDocInput.placeholder = 'Ingrese número';
            }
        }
    }
    /**
 * Actualiza el mensaje de ayuda del formato de la patente.
 */
    function updatePatenteFormatoHint(tipoVehiculo) {
        const hintElement = document.getElementById('patente-formato-hint');
        const patenteInput = document.getElementById('Patente');

        if (!hintElement) return;

        if (!tipoVehiculo) {
            hintElement.textContent = 'Seleccione un tipo de vehículo';
            if (patenteInput) {
                patenteInput.removeAttribute('pattern');
                patenteInput.placeholder = 'Ingrese patente';
            }
            return;
        }

        const tipoInfo = tiposVehiculoFormatos[tipoVehiculo];

        if (tipoInfo && tipoInfo.formato) {
            hintElement.textContent = `Formato: ${tipoInfo.formato}`;
            if (patenteInput) {
                patenteInput.placeholder = tipoInfo.formato;
            }
        } else {
            hintElement.textContent = 'Ingrese la patente del vehículo';
            if (patenteInput) {
                patenteInput.placeholder = 'Ingrese patente';
            }
        }
    }
    /**
     * Valida el número de documento según el formato del tipo seleccionado.
     */
    function validateDocumentoNumero() {
        const tipoDocSelect = document.getElementById('TipoDocumento');
        const numeroDocInput = document.getElementById('NumeroDocumento');
        const errorSpan = document.getElementById('documento-validation-error');

        if (!tipoDocSelect || !numeroDocInput) return true;

        const tipoDocumento = tipoDocSelect.value;
        const numeroDoc = numeroDocInput.value;

        // Limpiar error previo
        if (errorSpan) {
            errorSpan.classList.add('hidden');
            errorSpan.textContent = '';
        }
        numeroDocInput.classList.remove('border-red-500');

        // Si no hay tipo seleccionado o número, no validar
        if (!tipoDocumento || !numeroDoc) return true;

        const tipoInfo = tiposDocumentoFormatos[tipoDocumento];

        // Si no hay formato definido, aceptar cualquier valor
        if (!tipoInfo || !tipoInfo.regex) return true;

        // Validar contra el regex
        try {
            const regex = new RegExp(tipoInfo.regex);
            if (!regex.test(numeroDoc)) {
                if (errorSpan) {
                    errorSpan.textContent = `El formato debe ser: ${tipoInfo.formato}`;
                    errorSpan.classList.remove('hidden');
                }
                numeroDocInput.classList.add('border-red-500');
                return false;
            }
        } catch (e) {
            console.error('Error en regex de tipo de documento:', e);
        }

        return true;
    }
    /**
 * Valida la patente según el formato del tipo de vehículo seleccionado.
 */
    function validatePatenteVehiculo() {
        const tipoVehiculoSelect = document.getElementById('TipoVehiculo');
        const patenteInput = document.getElementById('Patente');
        const errorSpan = document.getElementById('patente-validation-error');

        if (!tipoVehiculoSelect || !patenteInput) return true;

        const tipoVehiculo = tipoVehiculoSelect.value;
        const patente = patenteInput.value;

        // Limpiar error previo
        if (errorSpan) {
            errorSpan.classList.add('hidden');
            errorSpan.textContent = '';
        }
        patenteInput.classList.remove('border-red-500');

        // Si no hay tipo seleccionado o patente, no validar
        if (!tipoVehiculo || !patente) return true;

        const tipoInfo = tiposVehiculoFormatos[tipoVehiculo];

        // Si no hay formato definido, aceptar cualquier valor
        if (!tipoInfo || !tipoInfo.regex) return true;

        // Validar contra el regex
        try {
            const regex = new RegExp(tipoInfo.regex);
            if (!regex.test(patente)) {
                if (errorSpan) {
                    errorSpan.textContent = `El formato debe ser: ${tipoInfo.formato}`;
                    errorSpan.classList.remove('hidden');
                }
                patenteInput.classList.add('border-red-500');
                return false;
            }
        } catch (e) {
            console.error('Error en regex de tipo de vehículo:', e);
        }

        return true;
    }
    /**
     * Configura listener para cuando se abre el acordeón del formulario
     */
    function setupAccordionListener() {
        const accordionBtn = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
        const accordionBody = document.getElementById('accordion-flush-body-1');

        if (!accordionBtn || !accordionBody) {
            console.warn('? Elementos del acordeón no encontrados');
            return;
        }

        // Usar MutationObserver para detectar cuando el acordeón se abre
        const observer = new MutationObserver(async (mutations) => {
            for (const mutation of mutations) {
                if (mutation.type === 'attributes' && mutation.attributeName === 'class') {
                    const isHidden = accordionBody.classList.contains('hidden');

                    if (!isHidden) {
                        // El acordeón se acaba de abrir

                        // Esperar un poco para asegurar que el DOM esté completamente renderizado
                        await new Promise(resolve => setTimeout(resolve, 100));

                        // Verificar si ya se inicializó (para evitar duplicados)
                        if (vehiculosDisponibles.length === 0) {
                            await setupVehiculoSelector();
                        } else {

                        }
                    }
                }
            }
        });

        observer.observe(accordionBody, { attributes: true });

    }

    // ===================== Configuración inicial =====================

    function setupInitialState() {
        const pageInput = document.getElementById("current-page-value");
        if (pageInput) currentPage = parseInt(pageInput.value);

        const sortInput = document.getElementById("current-sort-by");
        if (sortInput) currentSortBy = sortInput.value;

        const orderInput = document.getElementById("current-sort-order");
        if (orderInput) currentSortOrder = orderInput.value;
    }

    async function checkEditMode() {
        const formTitle = document.getElementById('form-title');
        if (formTitle && formTitle.textContent.includes('Editando')) {
            const accordion = document.getElementById('accordion-flush-body-1');
            if (accordion) {
                accordion.classList.remove('hidden');

                // CRÍTICO: Inicializar selector de vehículos cuando se abre en modo edición
                await setupVehiculoSelector();
            }
        }
    }

    // ===================== Búsqueda (debounce) =====================

    function setupSearchWithDebounce() {
        const searchInput = document.getElementById("simple-search");
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
                // Limpiar estado y volver a la tabla base
                currentSearchTerm = '';
                currentPage = 1;
                reloadClienteTable();
                return;
            }

            // Debouncing
            searchTimeout = setTimeout(() => {
                performServerSearch(searchTerm);
            }, 500);
        });
    }


    // ===================== Ordenamiento tabla =====================

    window.sortTable = function (column) {
        if (currentSortBy === column) {
            currentSortOrder = currentSortOrder === "asc" ? "desc" : "asc";
        } else {
            currentSortBy = column;
            currentSortOrder = "asc";
        }
        reloadClienteTable();
    };

    // ===================== Filtros y recarga de tabla =====================

    function buildFilterParams() {
        const form = document.getElementById('filterForm');
        const params = new URLSearchParams();
        if (form) {
            const formData = new FormData(form);
            for (const [key, value] of formData.entries()) {
                params.append(key, value);
            }
        }
        return params;
    }

    function getCurrentTablePage() {
        return parseInt(document.getElementById('cliente-table-container')?.dataset.currentPage || '1');
    }

    function reloadClienteTable(page) {
        if (page) currentPage = page;

        const params = buildFilterParams();
        params.set('searchTerm', currentSearchTerm);
        params.set('pageNumber', currentPage.toString());
        params.set('sortBy', currentSortBy);
        params.set('sortOrder', currentSortOrder);

        const url = `/Cliente/SearchPartial?${params.toString()}`;

        fetch(url, {
            headers: { 'X-Requested-With': 'XMLHttpRequest', 'Cache-Control': 'no-cache' },
            cache: 'no-store'
        })
            .then(r => r.text())
            .then(html => {
                const container = document.getElementById("cliente-table-container");
                if (container) container.innerHTML = html;

                const cp = document.getElementById('current-page-value')?.value;
                if (cp && container) container.dataset.currentPage = cp;
            })
            .catch(error => {
                console.error('Error al cargar la tabla:', error);
                showTableMessage('error', 'Error al cargar los datos.');
            });
    }

    function setupFilterFormSubmit() {
        const form = document.getElementById('filterForm');
        if (!form || form.dataset.submitSetup === 'true') return;

        form.addEventListener('submit', (e) => {
            e.preventDefault();
            e.stopPropagation();

            const pg = form.querySelector('input[name="pageNumber"]');
            if (pg) pg.value = '1';

            const searchInput = document.getElementById('simple-search');
            if (searchInput) currentSearchTerm = searchInput.value.trim();

            // Cerrar el dropdown de filtros usando el botón de toggle
            const filterButton = document.getElementById('filterDropdownButton');
            if (filterButton) {
                filterButton.click();
            }

            reloadClienteTable(1);

            if (history.replaceState) history.replaceState(null, '', window.location.pathname);

            showTableMessage('info', 'Filtros aplicados.');
        });

        form.dataset.submitSetup = 'true';
    }

    window.clearClienteFilters = function () {
        const form = document.getElementById('filterForm');
        if (!form) return;

        try {
            if (typeof window.clearAllFilters === 'function') {
                window.clearAllFilters();
            } else {
                form.querySelectorAll('input[name="estados"][type="checkbox"]').forEach(cb => cb.checked = false);
                form.querySelector('input[name="estados"][value="Activo"]')?.setAttribute('checked', 'checked');
            }
        } catch { }

        form.querySelectorAll('input[type="text"]').forEach(inp => inp.value = '');
        form.querySelectorAll('input[type="number"]').forEach(inp => inp.value = '');
        form.querySelectorAll('select').forEach(sel => sel.selectedIndex = 0);

        const searchInput = document.getElementById('simple-search');
        if (searchInput) searchInput.value = '';
        currentSearchTerm = '';

        // Cerrar el dropdown de filtros usando el botón de toggle
        const filterButton = document.getElementById('filterDropdownButton');
        if (filterButton) {
            filterButton.click();
        }

        if (history.replaceState) history.replaceState({}, document.title, '/Cliente/Index');

        //window.CommonUtils?.setupDefaultFilterForm?.();

        reloadClienteTable(1);
        showTableMessage('info', 'Filtros restablecidos.');
    };

    // ===================== Formulario =====================

    window.loadClienteForm = async function (id) {
        // NUEVO: Limpiar vehículos temporales al cambiar/limpiar formulario
        vehiculosTemporales = [];

        const url = id ? `/Cliente/FormPartial?id=${id}` : "/Cliente/FormPartial";

        try {
            const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            const html = await response.text();

            document.getElementById("cliente-form-container").innerHTML = html;

            const isEdit = !!document.getElementById('Id')?.value;
            const titleSpan = document.getElementById('form-title');
            if (titleSpan) {
                titleSpan.textContent = isEdit ? 'Editando Cliente' : 'Registrando Cliente';
            }

            // CRÍTICO: Esperar a que setupVehiculoSelector termine
            await setupVehiculoSelector();

            // Reconfigurar validación de documento
            await setupDocumentoValidation();

            const accordionBtn = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
            const accordionBody = document.getElementById("accordion-flush-body-1");
            if (accordionBody?.classList.contains("hidden")) {
                accordionBtn?.click();
            }

            if (isEdit) {
                setTimeout(() => {
                    const formContainer = document.getElementById('accordion-flush');
                    if (formContainer) {
                        formContainer.scrollIntoView({ behavior: 'smooth', block: 'start' });
                    }
                }, 100);
            }
        } catch (error) {

        }
    };

    window.submitClienteAjax = async function (form, event) {
        // Prevenir el comportamiento por defecto
        if (event) {
            event.preventDefault();
            event.stopPropagation();
        }

        // Validar número de documento antes de enviar
        if (!validateDocumentoNumero()) {
            showFormMessage('error', 'El formato del número de documento no es válido.');
            return false;
        }

        // Prevenir doble envío
        const submitBtn = document.getElementById('submit-button');
        if (submitBtn && submitBtn.disabled) {
            return false;
        }

        // Validar que al menos haya un vehículo
        if (vehiculosSeleccionados.length === 0) {
            showFormMessage('error', 'Debe agregar al menos un vehículo para el cliente.');
            document.getElementById('vehiculos-error')?.classList.remove('hidden');
            return false;
        }

        document.getElementById('vehiculos-error')?.classList.add('hidden');

        // Deshabilitar botón de envío
        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.innerHTML = `
                <svg class="animate-spin h-5 w-5 mr-2 inline" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
                Guardando...
            `;
        }

        try {
            // NUEVO: Preparar datos de vehículos
            const vehiculosData = vehiculosSeleccionados.map(v => ({
                id: v.esTemporalNuevo ? null : v.id, // null para nuevos, ID para existentes
                patente: v.patente,
                marca: v.marca,
                modelo: v.modelo,
                color: v.color,
                tipoVehiculo: v.tipoVehiculo,
                esNuevo: v.esTemporalNuevo || false,
                esReasignacion: v.esReasignacion || false // Flag para reasignación
            }));

            // Crear FormData del formulario
            const formData = new FormData(form);

            // Agregar el JSON al FormData
            const vehiculosJson = JSON.stringify(vehiculosData);
            formData.set('VehiculosDataJson', vehiculosJson);

            const response = await fetch(form.action, {
                method: 'POST',
                body: formData,
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            const valid = response.headers.get('X-Form-Valid') === 'true';
            const msg = response.headers.get('X-Form-Message');
            const associationKeysHeader = response.headers.get('X-Association-Keys');
            const html = await response.text();

            document.getElementById('cliente-form-container').innerHTML = html;

            // CRÍTICO: Esperar setup de vehículos
            await setupVehiculoSelector();

            const isEdit = !!document.getElementById('Id')?.value;
            const titleSpan = document.getElementById('form-title');
            if (titleSpan) {
                titleSpan.textContent = isEdit ? 'Editando Cliente' : 'Registrando Cliente';
            }

            if (valid) {
                // Limpiar vehículos temporales si se guardó exitosamente
                vehiculosTemporales = [];
                vehiculosSeleccionados = [];
                vehiculosDisponibles = [];

                // Verificar si hay claves de asociación generadas
                if (associationKeysHeader) {
                    try {
                        const claves = JSON.parse(associationKeysHeader);
                        if (claves && claves.length > 0) {
                            mostrarModalClavesAsociacion(claves);
                        }
                    } catch (e) {
                        console.error('Error al parsear claves de asociación:', e);
                    }
                }

                showFormMessage('success', msg || 'Cliente guardado correctamente. Los vehículos han sido asignados.', 4000);
                reloadClienteTable(1);

                // Cerrar el acordeón
                setTimeout(() => {
                    const accordionBody = document.getElementById('accordion-flush-body-1');
                    const accordionBtn = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');

                    if (accordionBody && accordionBtn && !accordionBody.classList.contains('hidden')) {
                        accordionBtn.click(); // Simular click para cerrar correctamente
                    }
                }, 1500);
            } else {
                const summary = document.getElementById('cliente-validation-summary');
                if (summary && summary.textContent.trim().length > 0) {
                    summary.classList.remove('hidden');
                }
                showFormMessage('error', 'Revise los errores del formulario.', 8000);
            }
        } catch (e) {
            console.error('❌ Error al enviar formulario:', e);
            showFormMessage('error', 'Error de comunicación con el servidor.', 8000);
        } finally {
            // Re-habilitar botón de envío
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.innerHTML = `
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="w-10 h-8 text-white-500 dark:text-white-400">
                        <path fill-rule="evenodd" d="M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 4.365 9.75 9.75-4.365 9.75-9.75 9.75S2.25 17.385 2.25 12Zm13.36-1.814a.75.75 0 1 0-1.22-.872l-3.236 4.53L9.53 12.22a.75.75 0 0 0-1.06 1.06l2.25 2.25a.75.75 0 0 0 1.14-.094l3.75-5.25Z" clip-rule="evenodd" />
                    </svg>
                    ${document.getElementById('Id')?.value ? 'Guardar' : 'Registrar'}
                `;
            }
        }

        return false;
    };

    /**
     * Muestra un modal con las claves de asociación generadas para los vehículos nuevos
     */
    function mostrarModalClavesAsociacion(claves) {
        // Eliminar modal anterior si existe
        const existingModal = document.getElementById("claves-asociacion-modal");
        if (existingModal) {
            existingModal.remove();
        }

        const clavesHtml = claves.map(clave => {
            const [patente, codigo] = clave.split(': ');
            return `
                <div class="p-3 bg-gray-50 dark:bg-gray-700 rounded-lg border border-gray-200 dark:border-gray-600 flex justify-between items-center">
                    <div>
                        <p class="text-sm font-medium text-gray-900 dark:text-white">${escapeHtml(patente)}</p>
                        <p class="text-xs text-gray-500 dark:text-gray-400">Clave de asociación</p>
                    </div>
                    <div class="flex items-center gap-2">
                        <code class="px-3 py-1 bg-blue-100 dark:bg-blue-900 text-blue-800 dark:text-blue-200 rounded font-mono text-lg font-bold">
                            ${escapeHtml(codigo)}
                        </code>
                        <button type="button" onclick="copiarClave('${escapeHtml(codigo)}')" 
                                class="p-2 text-gray-500 hover:text-blue-600 dark:text-gray-400 dark:hover:text-blue-400"
                                title="Copiar clave">
                            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10m0 0l3-3m-3 3l3 3"></path>
                            </svg>
                        </button>
                    </div>
                </div>
            `;
        }).join('');

        const modalHtml = `
            <div id="claves-asociacion-modal" tabindex="-1" aria-hidden="true" class="hidden overflow-y-auto overflow-x-hidden fixed top-0 right-0 left-0 z-50 justify-center items-center w-full md:inset-0 h-[calc(100%-1rem)] max-h-full">
                <div class="relative p-4 w-full max-w-md max-h-full">
                    <div class="relative bg-white rounded-lg shadow dark:bg-gray-800">
                        <div class="flex items-center justify-between p-4 md:p-5 border-b rounded-t dark:border-gray-600 bg-green-50 dark:bg-green-900/20">
                            <div class="flex items-center gap-2">
                                <div class="w-10 h-10 rounded-full bg-green-100 dark:bg-green-900 flex items-center justify-center">
                                    <svg class="w-6 h-6 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z"></path>
                                    </svg>
                                </div>
                                <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
                                    Claves de Asociación
                                </h3>
                            </div>
                            <button type="button" onclick="cerrarModalClavesAsociacion()" class="text-gray-400 bg-transparent hover:bg-gray-200 hover:text-gray-900 rounded-lg text-sm w-8 h-8 ms-auto inline-flex justify-center items-center dark:hover:bg-gray-600 dark:hover:text-white">
                                <svg class="w-3 h-3" aria-hidden="true" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 14 14">
                                    <path stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="m1 1 6 6m0 0 6 6M7 7l6-6M7 7l-6 6"/>
                                </svg>
                            </button>
                        </div>
                        <div class="p-4 md:p-5">
                            <div class="mb-4 p-3 bg-yellow-50 border border-yellow-200 rounded-lg dark:bg-yellow-900/20 dark:border-yellow-800">
                                <p class="text-sm text-yellow-800 dark:text-yellow-300">
                                    <svg class="w-4 h-4 inline mr-1" fill="currentColor" viewBox="0 0 20 20">
                                        <path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clip-rule="evenodd"/>
                                    </svg>
                                    <strong>¡Importante!</strong> Guarde estas claves. Serán necesarias para que otros clientes puedan asociarse a estos vehículos.
                                </p>
                            </div>
                            <div class="space-y-3">
                                ${clavesHtml}
                            </div>
                        </div>
                        <div class="p-4 md:p-5 border-t dark:border-gray-600 flex justify-end">
                            <button type="button" onclick="cerrarModalClavesAsociacion()" 
                                    class="text-white bg-blue-600 hover:bg-blue-700 focus:ring-4 focus:outline-none focus:ring-blue-300 font-medium rounded-lg text-sm px-5 py-2.5 dark:bg-blue-500 dark:hover:bg-blue-600">
                                Entendido
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', modalHtml);

        const modalEl = document.getElementById('claves-asociacion-modal');

        if (typeof Modal !== 'undefined') {
            const modal = new Modal(modalEl, {
                placement: 'center',
                backdrop: 'static',
                closable: false
            });
            modal.show();
            window._clavesAsociacionModal = modal;
        } else {
            modalEl.classList.remove('hidden');
            modalEl.classList.add('flex');
        }
    }

    /**
     * Cierra el modal de claves de asociación
     */
    window.cerrarModalClavesAsociacion = function () {
        if (window._clavesAsociacionModal && typeof window._clavesAsociacionModal.hide === 'function') {
            window._clavesAsociacionModal.hide();
            setTimeout(() => {
                const modalEl = document.getElementById('claves-asociacion-modal');
                if (modalEl) modalEl.remove();
                window._clavesAsociacionModal = null;
            }, 300);
        } else {
            const modalEl = document.getElementById("claves-asociacion-modal");
            if (modalEl) {
                modalEl.classList.add('hidden');
                modalEl.classList.remove('flex');
                setTimeout(() => modalEl.remove(), 300);
            }
        }
    };

    /**
     * Copia una clave al portapapeles
     */
    window.copiarClave = async function (clave) {
        try {
            await navigator.clipboard.writeText(clave);
            showFormMessage('success', `Clave ${clave} copiada al portapapeles.`, 2000);
        } catch (e) {
            console.error('Error al copiar:', e);
            // Fallback para navegadores antiguos
            const textArea = document.createElement('textarea');
            textArea.value = clave;
            document.body.appendChild(textArea);
            textArea.select();
            document.execCommand('copy');
            document.body.removeChild(textArea);
            showFormMessage('success', `Clave ${clave} copiada al portapapeles.`, 2000);
        }
    };

    // ===================== Selector de Vehículos (Estilo Paquetes) =====================

    async function setupVehiculoSelector() {
        await loadVehiculosDisponibles();

        const searchInput = document.getElementById('vehiculo-search');
        if (searchInput) {
            searchInput.addEventListener('input', function () {
                filterVehiculosDropdown(this.value);
            });

            searchInput.addEventListener('focus', function () {
                showVehiculoDropdown();
            });
        }

        setupDropdownClickOutside();
    }

    async function loadVehiculosDisponibles() {
        try {
            const clienteId = document.getElementById('Id')?.value;

            if (clienteId) {
                const respCliente = await fetch(`/Cliente/GetVehiculosCliente?clienteId=${clienteId}`);
                const dataCliente = await respCliente.json();

                const vehiculosCliente = dataCliente.success ? (dataCliente.vehiculos || []) : [];
                vehiculosDisponibles = [...vehiculosCliente, ...vehiculosTemporales];

                const hiddenIds = document.getElementById('VehiculosIdsData')?.value;
                if (hiddenIds) {
                    const ids = hiddenIds.split(',').map(x => x.trim()).filter(x => x);
                    vehiculosSeleccionados = vehiculosDisponibles.filter(v => ids.includes(v.id));
                } else {
                    vehiculosSeleccionados = [];
                }
            } else {
                vehiculosDisponibles = [...vehiculosTemporales];
                vehiculosSeleccionados = [];
            }

            updateVehiculosSeleccionadosList();

        } catch (error) {
            console.error('Error al cargar vehículos:', error);
            vehiculosDisponibles = [];
            vehiculosSeleccionados = [];
        }
    }

    function renderVehiculosDropdown(vehiculos, filterText = '') {
        const target = document.getElementById('vehiculo-dropdown-content');
        if (!target) return;

        if (!Array.isArray(vehiculos) || vehiculos.length === 0) {
            target.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No hay vehículos disponibles. Use "Nuevo Vehículo" para crear uno.</p>';
            return;
        }

        let lista = vehiculos;

        if (filterText && filterText.trim()) {
            const lower = filterText.toLowerCase();
            lista = lista.filter(v =>
                (v.patente && v.patente.toLowerCase().includes(lower)) ||
                (v.marca && v.marca.toLowerCase().includes(lower)) ||
                (v.modelo && v.modelo.toLowerCase().includes(lower))
            );
        }

        lista = lista.filter(v => !vehiculosSeleccionados.some(sel => sel.id === v.id));

        if (lista.length === 0) {
            target.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No se encontraron vehículos con ese criterio</p>';
            return;
        }

        let html = '';
        lista.forEach(v => {
            const active = vehiculoSeleccionadoDropdown?.id === v.id ? 'bg-blue-100 dark:bg-blue-900' : '';
            html += `<div class="px-2 py-2 hover:bg-gray-100 dark:hover:bg-gray-600 cursor-pointer ${active}"
                         onclick="selectVehiculoFromDropdown('${v.id}')">
                         <div class="text-sm font-medium text-gray-900 dark:text-white">${escapeHtml(v.patente)}</div>
                         <div class="text-xs text-gray-500 dark:text-gray-400">${escapeHtml(v.marca)} ${escapeHtml(v.modelo)} - ${escapeHtml(v.color)} (${escapeHtml(v.tipoVehiculo)})</div>
                     </div>`;
        });

        target.innerHTML = html;
    }

    window.showVehiculoDropdown = function () {
        const dropdown = document.getElementById('vehiculo-dropdown');
        const searchInput = document.getElementById('vehiculo-search');

        if (!dropdown) return;

        const inputValue = searchInput?.value?.trim() || '';

        if (vehiculosDisponibles.length > 0) {
            renderVehiculosDropdown(vehiculosDisponibles, inputValue);
            dropdown.classList.remove('hidden');
        } else {
            const target = document.getElementById('vehiculo-dropdown-content');
            if (target) {
                target.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No hay vehículos disponibles. Cree uno nuevo usando el botón "Nuevo Vehículo".</p>';
            }
            dropdown.classList.remove('hidden');
        }
    };

    window.filterVehiculosDropdown = function (txt) {
        const dropdown = document.getElementById('vehiculo-dropdown');
        if (!dropdown) return;

        const searchText = txt?.trim() || '';

        if (searchText === '') {
            if (vehiculosDisponibles.length > 0) {
                renderVehiculosDropdown(vehiculosDisponibles, '');
                dropdown.classList.remove('hidden');
            } else {
                const target = document.getElementById('vehiculo-dropdown-content');
                if (target) {
                    target.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No hay vehículos disponibles.</p>';
                }
                dropdown.classList.remove('hidden');
            }
            return;
        }

        renderVehiculosDropdown(vehiculosDisponibles, searchText);
        dropdown.classList.remove('hidden');
    };

    window.selectVehiculoFromDropdown = function (id) {
        const v = vehiculosDisponibles.find(x => x.id === id);
        if (!v) return;
        vehiculoSeleccionadoDropdown = v;
        renderVehiculosDropdown(vehiculosDisponibles, document.getElementById('vehiculo-search')?.value || '');
        const searchInput = document.getElementById('vehiculo-search');
        if (searchInput) searchInput.value = v.patente;
    };

    /**
     * Agrega el vehículo seleccionado a la lista del cliente.
     */
    window.agregarVehiculoSeleccionado = function () {
        if (!vehiculoSeleccionadoDropdown) {
            showFormMessage('error', 'Debe seleccionar un vehículo del listado.');
            return;
        }

        // Verificar si ya está en la lista
        if (vehiculosSeleccionados.some(v => v.id === vehiculoSeleccionadoDropdown.id)) {
            showFormMessage('error', 'Este vehículo ya está en la lista.');
            return;
        }

        vehiculosSeleccionados.push({
            id: vehiculoSeleccionadoDropdown.id,
            patente: vehiculoSeleccionadoDropdown.patente,
            marca: vehiculoSeleccionadoDropdown.marca,
            modelo: vehiculoSeleccionadoDropdown.modelo,
            color: vehiculoSeleccionadoDropdown.color,
            tipoVehiculo: vehiculoSeleccionadoDropdown.tipoVehiculo
        });

        vehiculoSeleccionadoDropdown = null;
        const searchInput = document.getElementById('vehiculo-search');
        if (searchInput) searchInput.value = '';
        document.getElementById('vehiculo-dropdown')?.classList.add('hidden');
        updateVehiculosSeleccionadosList();
        renderVehiculosDropdown(vehiculosDisponibles);
    };

    window.removerVehiculoSeleccionado = function (id) {
        // Remover de seleccionados
        vehiculosSeleccionados = vehiculosSeleccionados.filter(v => v.id !== id);

        // NUEVO: Si es un vehículo temporal, lo devolvemos al dropdown
        const vehiculoRemovido = vehiculosTemporales.find(v => v.id === id);
        if (vehiculoRemovido) {
            // Ya está en vehiculosTemporales, solo actualizar disponibles
            if (!vehiculosDisponibles.some(v => v.id === id)) {
                vehiculosDisponibles.push(vehiculoRemovido);
            }
        }

        updateVehiculosSeleccionadosList();
        renderVehiculosDropdown(vehiculosDisponibles, document.getElementById('vehiculo-search')?.value || '');
    };

    function updateVehiculosSeleccionadosList() {
        const container = document.getElementById('vehiculos-seleccionados-container');
        const list = document.getElementById('vehiculos-seleccionados-list');
        if (!(container && list)) return;

        if (vehiculosSeleccionados.length === 0) {
            container.classList.add('hidden');
            list.innerHTML = '';
            return;
        }

        container.classList.remove('hidden');

        list.innerHTML = '<ul class="space-y-2" id="vehiculos-sortable-list">' +
            vehiculosSeleccionados.map((v, index) => {
                return `
                    <li draggable="true" 
                        data-vehiculo-id="${v.id}"
                        data-index="${index}"
                        class="vehiculo-item flex items-center gap-3 p-2 bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-600 hover:border-blue-300 dark:hover:border-blue-500 transition-colors cursor-move">
                        
                        <!-- Drag Handle Icon -->
                        <div class="drag-handle flex-shrink-0 text-gray-400 hover:text-gray-600 dark:text-gray-500 dark:hover:text-gray-300" title="Arrastrar para reordenar">
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="w-5 h-5">
                                <path d="M8 5a2 2 0 1 1-4 0 2 2 0 0 1 4 0Zm0 7a2 2 0 1 1-4 0 2 2 0 0 1 4 0Zm0 7a2 2 0 1 1-4 0 2 2 0 0 1 4 0Zm12-14a2 2 0 1 1-4 0 2 2 0 0 1 4 0Zm0 7a2 2 0 1 1-4 0 2 2 0 0 1 4 0Zm0 7a2 2 0 1 1-4 0 2 2 0 0 1 4 0Z"/>
                            </svg>
                        </div>

                        <!-- Número de orden -->
                        <div class="flex-shrink-0 w-8 h-8 flex items-center justify-center bg-blue-100 dark:bg-blue-900 text-blue-800 dark:text-blue-200 rounded-full font-bold text-sm">
                            ${index + 1}
                        </div>

                        <!-- Información del vehículo -->
                        <div class="flex-1 min-w-0">
                            <div class="font-medium text-gray-900 dark:text-white truncate">${escapeHtml(v.patente)}</div>
                            <div class="text-sm text-gray-500 dark:text-gray-400">${escapeHtml(v.marca)} ${escapeHtml(v.modelo)} - ${escapeHtml(v.color)} (${escapeHtml(v.tipoVehiculo)})</div>
                        </div>

                        <!-- Botón eliminar -->
                        <button type="button"
                                onclick="event.stopPropagation(); removerVehiculoSeleccionado('${v.id}')"
                                class="flex-shrink-0 w-8 h-8 flex items-center justify-center overflow-visible bg-transparent rounded-md border border-transparent hover:border-red-200 dark:hover:border-red-700 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300"
                                title="Quitar vehículo"
                                aria-label="Quitar vehículo"
                                style="line-height:0;">
                            <svg xmlns="http://www.w3.org/2000/svg" fill="currentColor" class="w-5 h-5" viewBox="0 0 24 24" aria-hidden="true">
                                <path fill-rule="evenodd" d="M12 2.25c-5.385 0-9.75 4.365-9.75 9.75s4.365 9.75 9.75 9.75 9.75-4.365 9.75-9.75S17.385 2.25 12 2.25Zm-1.72 6.97a.75.75 0 1 0-1.06 1.06L10.94 12l-1.72 1.72a.75.75 0 1 0 1.06 1.06L12 13.06l1.72 1.72a.75.75 0 1 0 1.06-1.06L13.06 12l1.72-1.72a.75.75 0 1 0-1.06-1.06L12 10.94l-1.72-1.72Z" clip-rule="evenodd"/>
                            </svg>
                        </button>
                    </li>
                `;
            }).join('') +
            '</ul>' +
            '<p class="mt-2 text-xs text-gray-500 dark:text-gray-400 flex items-center gap-1">' +
            '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="w-4 h-4">' +
            '<path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd" />' +
            '</svg>' +
            'Arrastra los vehículos para cambiar su orden. El primero será el vehículo principal.' +
            '</p>';

        // Configurar drag and drop
        setupVehiculoDragAndDrop();
    }

    function setupDropdownClickOutside() {
        document.addEventListener('click', (e) => {
            const dropdown = document.getElementById('vehiculo-dropdown');
            const searchInput = document.getElementById('vehiculo-search');
            const addBtn = document.getElementById('add-vehiculo-btn');
            const agregarBtn = document.getElementById('btn-agregar-vehiculo');

            if (dropdown && !dropdown.contains(e.target) &&
                e.target !== searchInput && e.target !== addBtn && e.target !== agregarBtn) {
                dropdown.classList.add('hidden');
            }
        });
    }

    /**
     * Configura la funcionalidad de drag and drop para reordenar vehículos.
     */
    function setupVehiculoDragAndDrop() {
        const list = document.getElementById('vehiculos-sortable-list');
        if (!list) return;

        const items = document.querySelectorAll('.vehiculo-item');
        let draggedElement = null;
        let placeholder = null;

        items.forEach(item => {
            // Dragstart
            item.addEventListener('dragstart', function (e) {
                draggedElement = this;
                this.style.opacity = '0.4';
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('text/html', this.innerHTML);

                setTimeout(() => {
                    placeholder = document.createElement('li');
                    placeholder.className = 'vehiculo-placeholder h-16 border-2 border-dashed border-blue-400 dark:border-blue-600 rounded bg-blue-50 dark:bg-blue-900/20';

                    if (draggedElement.nextSibling) {
                        list.insertBefore(placeholder, draggedElement.nextSibling);
                    } else {
                        list.appendChild(placeholder);
                    }

                    draggedElement.style.display = 'none';
                }, 0);
            });

            // Dragend
            item.addEventListener('dragend', function (e) {
                this.style.opacity = '1';
                this.style.display = 'flex';

                if (placeholder && placeholder.parentNode) {
                    placeholder.parentNode.removeChild(placeholder);
                }

                document.querySelectorAll('.vehiculo-item').forEach(i => {
                    i.classList.remove('bg-blue-50', 'dark:bg-blue-900/20');
                });

                draggedElement = null;
                placeholder = null;
            });

            // Dragover
            item.addEventListener('dragover', function (e) {
                e.preventDefault();
                e.dataTransfer.dropEffect = 'move';

                if (draggedElement === this || !draggedElement || !placeholder) return;

                const rect = this.getBoundingClientRect();
                const midpoint = rect.top + rect.height / 2;

                if (e.clientY < midpoint) {
                    list.insertBefore(placeholder, this);
                } else {
                    if (this.nextSibling) {
                        list.insertBefore(placeholder, this.nextSibling);
                    } else {
                        list.appendChild(placeholder);
                    }
                }
            });

            // Drop
            item.addEventListener('drop', function (e) {
                e.preventDefault();
                e.stopPropagation();

                if (!draggedElement || !placeholder) return;

                if (placeholder.parentNode) {
                    placeholder.parentNode.insertBefore(draggedElement, placeholder);
                    placeholder.parentNode.removeChild(placeholder);
                }

                draggedElement.style.display = 'flex';
                actualizarOrdenVehiculos();
            });
        });

        // Eventos en el contenedor
        list.addEventListener('dragover', function (e) {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';

            if (!draggedElement || !placeholder) return;

            const afterElement = getDragAfterElement(list, e.clientY);

            if (afterElement == null) {
                list.appendChild(placeholder);
            } else {
                list.insertBefore(placeholder, afterElement);
            }
        });

        list.addEventListener('drop', function (e) {
            e.preventDefault();
            e.stopPropagation();

            if (!draggedElement || !placeholder) return;

            if (placeholder.parentNode) {
                placeholder.parentNode.insertBefore(draggedElement, placeholder);
                placeholder.parentNode.removeChild(placeholder);
            }

            draggedElement.style.display = 'flex';
            actualizarOrdenVehiculos();
        });

        function getDragAfterElement(container, y) {
            const draggableElements = [...container.querySelectorAll('.vehiculo-item:not(.opacity-40)')];

            return draggableElements.reduce((closest, child) => {
                const box = child.getBoundingClientRect();
                const offset = y - box.top - box.height / 2;

                if (offset < 0 && offset > closest.offset) {
                    return { offset: offset, element: child };
                } else {
                    return closest;
                }
            }, { offset: Number.NEGATIVE_INFINITY }).element;
        }

        function actualizarOrdenVehiculos() {
            const allItems = Array.from(list.children).filter(el => el.classList.contains('vehiculo-item'));
            const newOrder = allItems.map(el => {
                const id = el.getAttribute('data-vehiculo-id');
                return vehiculosSeleccionados.find(v => v.id === id);
            }).filter(v => v !== undefined);

            vehiculosSeleccionados = newOrder;
            updateVehiculosSeleccionadosList();
        }
    }

    // ===================== Modal de Creación Rápida de Vehículo =====================

    // Variable para rastrear el modo actual del modal
    let modoAsociacion = false;

    window.openQuickCreateVehiculoModal = async function () {
        try {
            // Resetear modo
            modoAsociacion = false;
            
            // Primero cargar los formatos
            await loadTiposVehiculoFormatos();

            const response = await fetch('/Vehiculo/FormPartial');
            const html = await response.text();

            // Eliminar modal anterior si existe
            const existingModal = document.getElementById("quick-create-modal");
            if (existingModal) {
                existingModal.remove();
            }
            const existingBackdrop = document.querySelector('[modal-backdrop]');
            if (existingBackdrop) {
                existingBackdrop.remove();
            }

            // Crear estructura de modal Flowbite completa con toggle de modo
            const modalHtml = `
            <div id="quick-create-modal" tabindex="-1" aria-hidden="true" class="hidden overflow-y-auto overflow-x-hidden fixed top-0 right-0 left-0 z-50 justify-center items-center w-full md:inset-0 h-[calc(100%-1rem)] max-h-full">
                <div class="relative p-4 w-full max-w-3xl max-h-full">
                    <div class="relative bg-white rounded-lg shadow dark:bg-gray-800">
                        <div class="flex items-center justify-between p-4 md:p-5 border-b rounded-t dark:border-gray-600">
                            <h3 id="modal-title" class="text-xl font-semibold text-gray-900 dark:text-white">
                                Agregar Vehículo
                            </h3>
                            <button type="button" onclick="closeQuickCreateModal()" class="text-gray-400 bg-transparent hover:bg-gray-200 hover:text-gray-900 rounded-lg text-sm w-8 h-8 ms-auto inline-flex justify-center items-center dark:hover:bg-gray-600 dark:hover:text-white">
                                <svg class="w-3 h-3" aria-hidden="true" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 14 14">
                                    <path stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="m1 1 6 6m0 0 6 6M7 7l6-6M7 7l-6 6"/>
                                </svg>
                                <span class="sr-only">Cerrar modal</span>
                            </button>
                        </div>
                        
                        <!-- Toggle de Modo -->
                        <div class="p-4 md:p-5 pb-2 border-b dark:border-gray-600">
                            <div class="flex items-center justify-center gap-4">
                                <span id="label-nuevo" class="text-sm font-medium text-blue-600 dark:text-blue-400">Registrar Nuevo</span>
                                <label class="relative inline-flex items-center cursor-pointer">
                                    <input type="checkbox" id="toggle-modo-vehiculo" class="sr-only peer" onchange="toggleModoVehiculo(this.checked)">
                                    <div class="w-11 h-6 bg-blue-600 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-blue-300 dark:peer-focus:ring-blue-800 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-green-600"></div>
                                </label>
                                <span id="label-asociar" class="text-sm font-medium text-gray-400 dark:text-gray-500">Asociar Existente</span>
                            </div>
                            <p id="modo-descripcion" class="text-center text-xs text-gray-500 dark:text-gray-400 mt-2">
                                Registre un vehículo nuevo. Se generará una clave de asociación para compartirlo.
                            </p>
                        </div>
                        
                        <div class="p-4 md:p-5 space-y-4 max-h-[calc(100vh-280px)] overflow-y-auto">
                            <div id="quick-vehiculo-messages" class="mb-4"></div>
                            
                            <!-- Contenido para MODO NUEVO (formulario estándar) -->
                            <div id="modo-nuevo-content">
                                <div id="quick-vehiculo-form-content">
                                    ${html}
                                </div>
                            </div>
                            
                            <!-- Contenido para MODO ASOCIACIÓN -->
                            <div id="modo-asociacion-content" class="hidden">
                                <form id="asociacion-form" class="space-y-4">
                                    <div class="relative">
                                        <label for="asociacion-patente-input" class="block mb-2 text-sm font-medium text-gray-900 dark:text-white">
                                            Patente del Vehículo <span class="text-red-600">*</span>
                                        </label>
                                        <input type="text" id="asociacion-patente-input" 
                                               class="bg-gray-50 border border-gray-300 text-gray-900 text-sm rounded-lg focus:ring-blue-500 focus:border-blue-500 block w-full p-2.5 dark:bg-gray-700 dark:border-gray-600 dark:placeholder-gray-400 dark:text-white uppercase"
                                               placeholder="Escriba para filtrar patentes..."
                                               autocomplete="off"
                                               oninput="filtrarPatentesAsociacion(this.value)"
                                               onfocus="mostrarDropdownPatentes()">
                                        <input type="hidden" id="asociacion-patente" value="">
                                        <div id="patentes-dropdown" class="hidden absolute z-50 w-full bg-white border border-gray-300 rounded-lg shadow-lg dark:bg-gray-700 dark:border-gray-600 max-h-48 overflow-y-auto mt-1">
                                            <!-- Las opciones se llenan dinámicamente -->
                                        </div>
                                        <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">
                                            Escriba para filtrar y seleccione el vehículo
                                        </p>
                                    </div>
                                    
                                    <!-- Info del vehículo seleccionado -->
                                    <div id="info-vehiculo-asociacion" class="hidden p-3 bg-gray-50 dark:bg-gray-700 rounded-lg">
                                        <p class="text-sm font-medium text-gray-900 dark:text-white mb-2">Datos del Vehículo:</p>
                                        <div id="info-vehiculo-datos" class="text-sm text-gray-500 dark:text-gray-400"></div>
                                    </div>
                                    
                                    <div>
                                        <label for="clave-asociacion" class="block mb-2 text-sm font-medium text-gray-900 dark:text-white">
                                            Clave de Asociación <span class="text-red-600">*</span>
                                        </label>
                                        <input type="text" id="clave-asociacion" 
                                               class="bg-gray-50 border border-gray-300 text-gray-900 text-sm rounded-lg focus:ring-blue-500 focus:border-blue-500 block w-full p-2.5 dark:bg-gray-700 dark:border-gray-600 dark:placeholder-gray-400 dark:text-white uppercase"
                                               placeholder="XXXX-XXXX"
                                               maxlength="9"
                                               oninput="formatearClaveAsociacion(this)">
                                        <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">
                                            Ingrese la clave que le proporcionó el dueño actual del vehículo
                                        </p>
                                    </div>
                                    
                                    <!-- Resultado de validación -->
                                    <div id="validacion-clave-resultado" class="hidden"></div>
                                    
                                    <div class="flex justify-end gap-2 pt-4">
                                        <button type="button" onclick="validarClaveAsociacion()" 
                                                class="text-white bg-blue-600 hover:bg-blue-700 focus:ring-4 focus:outline-none focus:ring-blue-300 font-medium rounded-lg text-sm px-4 py-2.5 dark:bg-blue-500 dark:hover:bg-blue-600">
                                            <svg class="w-4 h-4 inline mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                                            </svg>
                                            Validar y Agregar
                                        </button>
                                        <button type="button" onclick="closeQuickCreateModal()" 
                                                class="text-gray-500 bg-white hover:bg-gray-100 focus:ring-4 focus:outline-none focus:ring-gray-300 rounded-lg border border-gray-200 text-sm font-medium px-4 py-2.5 dark:bg-gray-700 dark:text-gray-300 dark:border-gray-500 dark:hover:bg-gray-600">
                                            Cancelar
                                        </button>
                                    </div>
                                </form>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;

            document.body.insertAdjacentHTML('beforeend', modalHtml);

            const modalEl = document.getElementById('quick-create-modal');

            if (typeof Modal !== 'undefined') {
                const modalOptions = {
                    placement: 'center',
                    backdrop: 'static',
                    closable: false,
                    onHide: () => { document.body.style.overflow = ''; },
                    onShow: () => { document.body.style.overflow = 'hidden'; }
                };

                const modal = new Modal(modalEl, modalOptions);
                modal.show();
                window._quickVehiculoModal = modal;
                // ============================================================
                console.log('🚗 Cargando vehiculo-api.js manualmente...');

                // Verificar si el formulario NO está en modo edición
                const vehiculoForm = modalEl.querySelector('#vehiculo-form');
                const isEditMode = vehiculoForm?.dataset.edit === 'true' || vehiculoForm?.dataset.edit === 'True';

                console.log('📝 Modo edición:', isEditMode);

                if (!isEditMode) {
                    // Verificar si el script ya existe
                    const existingScript = document.querySelector('script[src*="vehiculo-api.js"]');
                    if (existingScript) {
                        console.log('?? Script ya existe, REMOVIENDO y recargando...');
                        // REMOVER el script viejo
                        existingScript.remove();

                        // Cargar script fresco
                        const script = document.createElement('script');
                        script.src = '/js/vehiculo-api.js?v=' + new Date().getTime();
                        script.onload = function () {
                            console.log('? Script RECARGADO, inicializando...');
                            setTimeout(() => {
                                if (window.initVehiculoApiSelects) {
                                    console.log('?? Llamando initVehiculoApiSelects()');
                                    window.initVehiculoApiSelects();
                                } else {
                                    console.error('? initVehiculoApiSelects TODAVÍA no encontrado');
                                    console.log('window.VehiculoApi:', window.VehiculoApi);
                                    console.log('window.initVehiculoApiSelects:', window.initVehiculoApiSelects);
                                }
                            }, 300);
                        };
                        script.onerror = function () {
                            console.error('?? Error al RECARGAR vehiculo-api.js');
                        };
                        document.head.appendChild(script);
                    } else {
                        console.log('?? Cargando script por primera vez...');
                        // Si no existe, cargarlo
                        const script = document.createElement('script');
                        script.src = '/js/vehiculo-api.js?v=' + new Date().getTime();
                        script.onload = function () {
                            console.log('? Script cargado, inicializando...');
                            setTimeout(() => {
                                if (window.initVehiculoApiSelects) {
                                    console.log('?? Llamando initVehiculoApiSelects()');
                                    window.initVehiculoApiSelects();
                                } else {
                                    console.error('? initVehiculoApiSelects no encontrado');
                                }
                            }, 300);
                        };
                        script.onerror = function () {
                            console.error('?? Error al cargar vehiculo-api.js');
                        };
                        document.head.appendChild(script);
                    }
                } else {
                    console.log('?? Modo edición, no se carga API');
                }
                // ============================================================
            } else {
                modalEl.classList.remove('hidden');
                modalEl.classList.add('flex');
                document.body.style.overflow = 'hidden';

                const backdrop = document.createElement('div');
                backdrop.setAttribute('modal-backdrop', '');
                backdrop.className = 'bg-gray-900/50 dark:bg-gray-900/80 fixed inset-0 z-40';
                document.body.appendChild(backdrop);
            }

            const messagesContainer = document.getElementById('quick-vehiculo-messages');
            if (messagesContainer) {
                messagesContainer.innerHTML = '';
            }

            const form = modalEl.querySelector("form");
            if (form) {
                form.onsubmit = function (e) {
                    e.preventDefault();
                    submitQuickVehiculo(this);
                    return false;
                };

                const cancelBtn = form.querySelector("#clear-button");
                if (cancelBtn) {
                    cancelBtn.onclick = function (e) {
                        e.preventDefault();
                        closeQuickCreateModal();
                        return false;
                    };
                }

                const tipoVehiculoSelect = modalEl.querySelector('#TipoVehiculo');
                if (tipoVehiculoSelect) {
                    tipoVehiculoSelect.addEventListener('change', function () {
                        updatePatenteFormatoHint(this.value);
                        validatePatenteVehiculo();
                    });

                    if (tipoVehiculoSelect.value) {
                        updatePatenteFormatoHint(tipoVehiculoSelect.value);
                    }
                }

                const patenteInput = modalEl.querySelector('#Patente');
                if (patenteInput) {
                    patenteInput.addEventListener('input', function () {
                        validatePatenteVehiculo();
                        const start = this.selectionStart;
                        const end = this.selectionEnd;
                        this.value = this.value.toUpperCase();
                        this.setSelectionRange(start, end);
                    });
                    patenteInput.addEventListener('blur', validatePatenteVehiculo);
                }
            }
        } catch (error) {
            console.error('Error al cargar form vehiculo:', error);
        }
    };

    window.closeQuickCreateModal = function () {
        // Limpiar mensajes del modal
        const messagesContainer = document.getElementById('quick-vehiculo-messages');
        if (messagesContainer) {
            messagesContainer.innerHTML = '';
        }

        // Intentar cerrar con Flowbite
        if (window._quickVehiculoModal && typeof window._quickVehiculoModal.hide === 'function') {
            window._quickVehiculoModal.hide();

            // Limpiar después de cerrar
            setTimeout(() => {
                const modalEl = document.getElementById('quick-create-modal');
                if (modalEl) modalEl.remove();
                window._quickVehiculoModal = null;
            }, 300);
        } else {
            // Fallback
            const modalEl = document.getElementById("quick-create-modal");
            if (modalEl) {
                modalEl.classList.add('hidden');
                modalEl.classList.remove('flex');
                setTimeout(() => modalEl.remove(), 300);
            }

            const backdrop = document.querySelector('[modal-backdrop]');
            if (backdrop) backdrop.remove();
        }

        document.body.style.overflow = '';
    };

    // ===================== Funciones de Toggle y Asociación =====================
    
    /**
     * Alterna entre modo "Nuevo Vehículo" y "Asociar Existente"
     */
    window.toggleModoVehiculo = async function (esAsociacion) {
        modoAsociacion = esAsociacion;
        
        const modoNuevoContent = document.getElementById('modo-nuevo-content');
        const modoAsociacionContent = document.getElementById('modo-asociacion-content');
        const modalTitle = document.getElementById('modal-title');
        const labelNuevo = document.getElementById('label-nuevo');
        const labelAsociar = document.getElementById('label-asociar');
        const modoDescripcion = document.getElementById('modo-descripcion');
        
        if (esAsociacion) {
            // Cambiar a modo asociación
            modoNuevoContent?.classList.add('hidden');
            modoAsociacionContent?.classList.remove('hidden');
            if (modalTitle) modalTitle.textContent = 'Asociar Vehículo Existente';
            if (labelNuevo) {
                labelNuevo.classList.remove('text-blue-600', 'dark:text-blue-400');
                labelNuevo.classList.add('text-gray-400', 'dark:text-gray-500');
            }
            if (labelAsociar) {
                labelAsociar.classList.remove('text-gray-400', 'dark:text-gray-500');
                labelAsociar.classList.add('text-green-600', 'dark:text-green-400');
            }
            if (modoDescripcion) {
                modoDescripcion.textContent = 'Seleccione un vehículo e ingrese la clave de asociación que le proporcionó el dueño.';
            }
            
            // Cargar vehículos disponibles para asociación
            await cargarVehiculosParaAsociacion();
        } else {
            // Cambiar a modo nuevo
            modoNuevoContent?.classList.remove('hidden');
            modoAsociacionContent?.classList.add('hidden');
            if (modalTitle) modalTitle.textContent = 'Registrar Nuevo Vehículo';
            if (labelNuevo) {
                labelNuevo.classList.add('text-blue-600', 'dark:text-blue-400');
                labelNuevo.classList.remove('text-gray-400', 'dark:text-gray-500');
            }
            if (labelAsociar) {
                labelAsociar.classList.add('text-gray-400', 'dark:text-gray-500');
                labelAsociar.classList.remove('text-green-600', 'dark:text-green-400');
            }
            if (modoDescripcion) {
                modoDescripcion.textContent = 'Registre un vehículo nuevo. Se generará una clave de asociación para compartirlo.';
            }
        }
        
        // Limpiar mensajes
        const messagesContainer = document.getElementById('quick-vehiculo-messages');
        if (messagesContainer) messagesContainer.innerHTML = '';
    };

    /**
     * Carga los vehículos disponibles para asociación
     */
    let vehiculosParaAsociacion = []; // Cache global de vehículos
    
    async function cargarVehiculosParaAsociacion() {
        const input = document.getElementById('asociacion-patente-input');
        const dropdown = document.getElementById('patentes-dropdown');
        
        if (!input || !dropdown) return;
        
        input.value = '';
        input.placeholder = 'Cargando vehículos...';
        
        try {
            const response = await fetch('/Vehiculo/GetVehiculosParaAsociacion');
            const vehiculos = await response.json();
            
            if (!vehiculos || vehiculos.length === 0) {
                vehiculosParaAsociacion = [];
                input.placeholder = 'No hay vehículos disponibles para asociar';
                return;
            }
            
            // Filtrar vehículos que ya están en la lista del cliente actual
            vehiculosParaAsociacion = vehiculos.filter(v => 
                !vehiculosSeleccionados.some(sel => sel.id === v.id)
            );
            
            if (vehiculosParaAsociacion.length === 0) {
                input.placeholder = 'No hay más vehículos disponibles para asociar';
                return;
            }
            
            input.placeholder = 'Escriba para filtrar patentes...';
            renderDropdownPatentes(vehiculosParaAsociacion);
        } catch (error) {
            console.error('Error al cargar vehículos para asociación:', error);
            input.placeholder = 'Error al cargar vehículos';
            vehiculosParaAsociacion = [];
        }
    }

    /**
     * Renderiza las opciones del dropdown de patentes
     */
    function renderDropdownPatentes(vehiculos) {
        const dropdown = document.getElementById('patentes-dropdown');
        if (!dropdown) return;
        
        if (!vehiculos || vehiculos.length === 0) {
            dropdown.innerHTML = '<div class="p-3 text-sm text-gray-500 dark:text-gray-400">No se encontraron vehículos</div>';
            return;
        }
        
        dropdown.innerHTML = vehiculos.map(v => `
            <div class="patente-option p-2.5 cursor-pointer hover:bg-blue-100 dark:hover:bg-blue-900 border-b border-gray-100 dark:border-gray-600 last:border-b-0"
                 data-patente="${escapeHtml(v.patente)}"
                 data-vehiculo='${JSON.stringify(v).replace(/'/g, "&apos;")}'
                 onclick="seleccionarPatenteAsociacion(this)">
                <div class="flex justify-between items-center">
                    <span class="font-medium text-gray-900 dark:text-white">${escapeHtml(v.patente)}</span>
                    <span class="text-xs text-gray-500 dark:text-gray-400">${escapeHtml(v.tipoVehiculo)}</span>
                </div>
                <div class="text-sm text-gray-500 dark:text-gray-400">
                    ${escapeHtml(v.marca)} ${escapeHtml(v.modelo)} - ${escapeHtml(v.color)}
                </div>
            </div>
        `).join('');
    }

    /**
     * Muestra el dropdown de patentes
     */
    window.mostrarDropdownPatentes = function() {
        const dropdown = document.getElementById('patentes-dropdown');
        if (dropdown && vehiculosParaAsociacion.length > 0) {
            dropdown.classList.remove('hidden');
        }
    };

    /**
     * Oculta el dropdown de patentes
     */
    function ocultarDropdownPatentes() {
        const dropdown = document.getElementById('patentes-dropdown');
        if (dropdown) {
            // Usar un pequeño delay para permitir que el click en las opciones se registre primero
            setTimeout(() => dropdown.classList.add('hidden'), 200);
        }
    }

    /**
     * Filtra las patentes según el texto ingresado
     */
    window.filtrarPatentesAsociacion = function(texto) {
        const dropdown = document.getElementById('patentes-dropdown');
        if (!dropdown) return;
        
        dropdown.classList.remove('hidden');
        
        if (!texto || texto.trim() === '') {
            renderDropdownPatentes(vehiculosParaAsociacion);
            return;
        }
        
        const textoLower = texto.toLowerCase().trim();
        const vehiculosFiltrados = vehiculosParaAsociacion.filter(v => 
            v.patente.toLowerCase().includes(textoLower) ||
            v.marca.toLowerCase().includes(textoLower) ||
            v.modelo.toLowerCase().includes(textoLower)
        );
        
        renderDropdownPatentes(vehiculosFiltrados);
    };

    /**
     * Selecciona una patente del dropdown
     */
    window.seleccionarPatenteAsociacion = function(element) {
        const patente = element.dataset.patente;
        const vehiculoData = JSON.parse(element.dataset.vehiculo.replace(/&apos;/g, "'"));
        
        // Actualizar el input visible y el hidden
        const input = document.getElementById('asociacion-patente-input');
        const hiddenInput = document.getElementById('asociacion-patente');
        
        if (input) input.value = patente;
        if (hiddenInput) hiddenInput.value = patente;
        
        // Ocultar dropdown
        const dropdown = document.getElementById('patentes-dropdown');
        if (dropdown) dropdown.classList.add('hidden');
        
        // Mostrar info del vehículo
        mostrarInfoVehiculoAsociacion(vehiculoData);
    };

    /**
     * Muestra la información del vehículo seleccionado
     */
    function mostrarInfoVehiculoAsociacion(vehiculo) {
        const infoContainer = document.getElementById('info-vehiculo-asociacion');
        const infoDatos = document.getElementById('info-vehiculo-datos');
        
        if (!infoContainer || !infoDatos || !vehiculo) {
            infoContainer?.classList.add('hidden');
            return;
        }
        
        infoDatos.innerHTML = `
            <p><strong>Patente:</strong> ${escapeHtml(vehiculo.patente)}</p>
            <p><strong>Tipo:</strong> ${escapeHtml(vehiculo.tipoVehiculo)}</p>
            <p><strong>Marca/Modelo:</strong> ${escapeHtml(vehiculo.marca)} ${escapeHtml(vehiculo.modelo)}</p>
            <p><strong>Color:</strong> ${escapeHtml(vehiculo.color)}</p>
        `;
        infoContainer.classList.remove('hidden');
    }

    /**
     * Maneja el cambio de selección de patente para asociación (legacy, mantener por compatibilidad)
     */
    window.onPatenteAsociacionChange = function (patente) {
        const infoContainer = document.getElementById('info-vehiculo-asociacion');
        
        if (!patente) {
            infoContainer?.classList.add('hidden');
            return;
        }
        
        // Buscar el vehículo en la cache
        const vehiculo = vehiculosParaAsociacion.find(v => v.patente === patente);
        if (vehiculo) {
            mostrarInfoVehiculoAsociacion(vehiculo);
        } else {
            infoContainer?.classList.add('hidden');
        }
    };

    /**
     * Formatea la clave de asociación mientras se escribe (XXXX-XXXX)
     */
    window.formatearClaveAsociacion = function (input) {
        let valor = input.value.toUpperCase().replace(/[^A-Z0-9]/g, '');
        
        if (valor.length > 4) {
            valor = valor.slice(0, 4) + '-' + valor.slice(4, 8);
        }
        
        input.value = valor;
    };

    /**
     * Valida la clave de asociación y agrega el vehículo
     */
    window.validarClaveAsociacion = async function () {
        const patenteHidden = document.getElementById('asociacion-patente');
        const claveInput = document.getElementById('clave-asociacion');
        const resultadoDiv = document.getElementById('validacion-clave-resultado');
        
        const patente = patenteHidden?.value?.trim();
        const clave = claveInput?.value?.trim();
        
        if (!patente) {
            showQuickVehiculoMessage('error', 'Debe seleccionar un vehículo.', 5000);
            return;
        }
        
        if (!clave || clave.length < 9) {
            showQuickVehiculoMessage('error', 'Ingrese la clave de asociación completa (formato: XXXX-XXXX).', 5000);
            return;
        }
        
        // Verificar que no esté ya en la lista
        if (vehiculosSeleccionados.some(v => v.patente?.toUpperCase() === patente.toUpperCase())) {
            showQuickVehiculoMessage('error', 'Este vehículo ya está en la lista del cliente.', 5000);
            return;
        }
        
        try {
            const response = await fetch('/Vehiculo/ValidarClaveAsociacion', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    patente: patente,
                    claveAsociacion: clave
                })
            });
            
            const data = await response.json();
            
            if (!data.valida) {
                if (resultadoDiv) {
                    resultadoDiv.innerHTML = `
                        <div class="p-3 bg-red-50 border border-red-200 rounded-lg dark:bg-red-900/20 dark:border-red-800">
                            <p class="text-sm text-red-800 dark:text-red-300">
                                <svg class="w-4 h-4 inline mr-1" fill="currentColor" viewBox="0 0 20 20">
                                    <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd"/>
                                </svg>
                                ${escapeHtml(data.error || 'Clave inválida')}
                            </p>
                        </div>
                    `;
                    resultadoDiv.classList.remove('hidden');
                }
                return;
            }
            
            // Clave válida - agregar vehículo a la lista
            const vehiculoAsociado = {
                id: data.vehiculo.id,
                patente: data.vehiculo.patente,
                marca: data.vehiculo.marca,
                modelo: data.vehiculo.modelo,
                color: data.vehiculo.color,
                tipoVehiculo: data.vehiculo.tipoVehiculo,
                esTemporalNuevo: false,
                esAsociacion: true, // Flag especial para indicar asociación
                claveAsociacion: clave
            };
            
            vehiculosSeleccionados.push(vehiculoAsociado);
            
            // Cerrar modal
            closeQuickCreateModal();
            
            // Actualizar UI
            updateVehiculosSeleccionadosList();
            
            showFormMessage('success', `Vehículo ${vehiculoAsociado.patente} asociado correctamente. Guarde el cliente para confirmar.`, 4000);
            
        } catch (error) {
            console.error('Error al validar clave:', error);
            showQuickVehiculoMessage('error', 'Error de comunicación. Intente nuevamente.', 5000);
        }
    };

    async function submitQuickVehiculo(form) {
        // Prevenir el envío al servidor
        event.preventDefault();

        // Validar el formulario usando el API de validación del navegador
        if (!form.checkValidity()) {
            form.reportValidity();
            return false;
        }

        // Capturar datos del formulario (patente en mayúsculas)
        const patente = form.querySelector('#Patente')?.value?.trim().toUpperCase();
        const marca = form.querySelector('#Marca')?.value?.trim();
        const modelo = form.querySelector('#Modelo')?.value?.trim();
        const color = form.querySelector('#Color')?.value?.trim();
        const tipoVehiculo = form.querySelector('#TipoVehiculo')?.value;

        // Validaciones básicas
        if (!patente || !marca || !modelo || !color || !tipoVehiculo) {
            showQuickVehiculoMessage('error', 'Todos los campos son obligatorios.', 5000);
            return false;
        }
        // Validar patente según el tipo de vehículo
        if (!validatePatenteVehiculo()) {
            showQuickVehiculoMessage('error', 'El formato de la patente no es válido.', 5000);
            return false;
        }
        // Verificar que no exista ya en los vehículos temporales o seleccionados
        const patenteExiste = [...vehiculosTemporales, ...vehiculosSeleccionados].some(v =>
            v.patente && v.patente.toLowerCase() === patente.toLowerCase()
        );

        if (patenteExiste) {
            showQuickVehiculoMessage('error', 'Ya existe un vehículo con esta patente en la lista.', 5000);
            return false;
        }

        // NUEVO: Verificar si existe un vehículo con esta patente
        try {
            const verificarUrl = `/Vehiculo/VerificarVehiculoSinDueno?patente=${encodeURIComponent(patente)}&marca=${encodeURIComponent(marca)}&modelo=${encodeURIComponent(modelo)}`;
            const respuesta = await fetch(verificarUrl);
            const data = await respuesta.json();

            if (data.existe && data.vehiculo) {
                // Existe vehículo inactivo sin dueño ? Mostrar modal de reasignación
                mostrarModalReasignacion(data.vehiculo, color, tipoVehiculo);
                return false;
            }

            if (data.existe === false && data.error) {
                // Existe un vehículo activo o con dueño ? Bloquear
                showQuickVehiculoMessage('error', data.error, 8000);
                return false;
            }
        } catch (error) {
            console.error('Error al verificar vehículo:', error);
            showQuickVehiculoMessage('error', 'Error al verificar el vehículo. Intente nuevamente.', 5000);
        }

        // Crear vehículo temporal (solo en memoria)
        const vehiculoTemporal = {
            id: 'temp_' + Date.now(), // ID temporal único
            patente: patente,
            marca: marca,
            modelo: modelo,
            color: color,
            tipoVehiculo: tipoVehiculo,
            estado: 'Activo',
            esTemporalNuevo: true // Flag para identificar que es nuevo y no viene de BD
        };

        // Agregar a las listas
        vehiculosTemporales.push(vehiculoTemporal);
        vehiculosSeleccionados.push(vehiculoTemporal);

        // Cerrar modal
        closeQuickCreateModal();

        // Actualizar UI
        updateVehiculosSeleccionadosList();

        showFormMessage('success', `Vehículo ${patente} agregado. Guarde el cliente para registrarlo.`, 4000);

        return false;
    }

    /**
     * Muestra modal de confirmación para reasignar vehículo existente
     */
    function mostrarModalReasignacion(vehiculoExistente, nuevoColor, nuevoTipo) {
        // Eliminar modal anterior si existe
        const existingModal = document.getElementById("reasignar-vehiculo-modal");
        if (existingModal) {
            existingModal.remove();
        }

        const colorCambio = vehiculoExistente.color !== nuevoColor;
        const tipoCambio = vehiculoExistente.tipoVehiculo !== nuevoTipo;

        const cambiosHTML = colorCambio || tipoCambio ? `
            <div class="mt-3 p-3 bg-blue-50 border border-blue-200 rounded-lg dark:bg-blue-900/20 dark:border-blue-800">
                <p class="text-sm font-medium text-blue-800 dark:text-blue-300 mb-2">
                    ?? Se detectaron cambios que se aplicarán:
                </p>
                <ul class="text-xs text-blue-700 dark:text-blue-400 list-disc list-inside space-y-1">
                    ${colorCambio ? `<li>Color: <span class="font-semibold">${escapeHtml(vehiculoExistente.color)}</span> ? <span class="font-semibold">${escapeHtml(nuevoColor)}</span></li>` : ''}
                    ${tipoCambio ? `<li>Tipo: <span class="font-semibold">${escapeHtml(vehiculoExistente.tipoVehiculo)}</span> ? <span class="font-semibold">${escapeHtml(nuevoTipo)}</span></li>` : ''}
                </ul>
            </div>
        ` : '';

        const modalHtml = `
            <div id="reasignar-vehiculo-modal" tabindex="-1" aria-hidden="true" class="hidden overflow-y-auto overflow-x-hidden fixed top-0 right-0 left-0 z-50 justify-center items-center w-full md:inset-0 h-[calc(100%-1rem)] max-h-full">
                <div class="relative p-4 w-full max-w-md max-h-full">
                    <div class="relative bg-white rounded-lg shadow dark:bg-gray-800">
                        <button type="button" onclick="closeReasignarModal()" class="absolute top-3 right-2.5 text-gray-400 bg-transparent hover:bg-gray-200 hover:text-gray-900 rounded-lg text-sm w-8 h-8 ml-auto inline-flex justify-center items-center dark:hover:bg-gray-600 dark:hover:text-white">
                            <svg class="w-3 h-3" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 14 14">
                                <path stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="m1 1 6 6m0 0 6 6M7 7l6-6M7 7l-6 6"/>
                            </svg>
                            <span class="sr-only">Cerrar</span>
                        </button>
                        <div class="p-6 text-center">
                            <div class="w-12 h-12 rounded-full bg-yellow-100 dark:bg-yellow-900 p-2 flex items-center justify-center mx-auto mb-3.5">
                                <svg class="w-8 h-8 text-yellow-600 dark:text-yellow-400" fill="currentColor" viewBox="0 0 20 20">
                                    <path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clip-rule="evenodd"/>
                                </svg>
                            </div>
                            <h3 class="mb-2 text-lg font-normal text-gray-900 dark:text-white">Vehículo Ya Registrado</h3>
                            <div class="mb-4 text-sm text-gray-500 dark:text-gray-400">
                                <p class="mb-2">Este vehículo ya está registrado en el sistema pero <strong>no tiene dueño asignado</strong>.</p>
                                <div class="p-3 bg-gray-50 dark:bg-gray-700 rounded-lg text-left mb-3">
                                    <p class="font-semibold text-gray-900 dark:text-white mb-1">?? Datos actuales:</p>
                                    <ul class="text-xs space-y-1">
                                        <li>• Patente: <span class="font-semibold">${escapeHtml(vehiculoExistente.patente)}</span></li>
                                        <li>• Marca/Modelo: <span class="font-semibold">${escapeHtml(vehiculoExistente.marca)} ${escapeHtml(vehiculoExistente.modelo)}</span></li>
                                        <li>• Color: <span class="font-semibold">${escapeHtml(vehiculoExistente.color)}</span></li>
                                        <li>• Tipo: <span class="font-semibold">${escapeHtml(vehiculoExistente.tipoVehiculo)}</span></li>
                                        <li>• Estado: <span class="font-semibold text-red-600">Inactivo</span></li>
                                    </ul>
                                </div>
                                ${cambiosHTML}
                                <p class="mt-3 font-medium">¿Desea reasignarlo a este cliente?</p>
                            </div>
                            <div class="flex justify-center items-center space-x-4">
                                <button type="button" onclick="closeReasignarModal()" class="py-2 px-4 text-sm font-medium text-gray-500 bg-white rounded-lg border border-gray-200 hover:bg-gray-100 focus:ring-4 focus:outline-none focus:ring-gray-300 dark:bg-gray-700 dark:text-gray-300 dark:border-gray-500 dark:hover:text-white dark:hover:bg-gray-600">
                                    No, cancelar
                                </button>
                                <button type="button" onclick="confirmarReasignacion('${vehiculoExistente.id}', '${escapeHtml(nuevoColor)}', '${escapeHtml(nuevoTipo)}')" class="py-2 px-4 text-sm font-medium text-center text-white bg-blue-600 rounded-lg hover:bg-blue-700 focus:ring-4 focus:outline-none focus:ring-blue-300 dark:bg-blue-500 dark:hover:bg-blue-600 dark:focus:ring-blue-900">
                                    Sí, reasignar
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', modalHtml);

        const modalEl = document.getElementById('reasignar-vehiculo-modal');

        if (typeof Modal !== 'undefined') {
            const modalOptions = {
                placement: 'center',
                backdrop: 'static',
                closable: false
            };
            const modal = new Modal(modalEl, modalOptions);
            modal.show();
            window._reasignarVehiculoModal = modal;
        } else {
            modalEl.classList.remove('hidden');
            modalEl.classList.add('flex');
        }
    }

    window.closeReasignarModal = function () {
        if (window._reasignarVehiculoModal && typeof window._reasignarVehiculoModal.hide === 'function') {
            window._reasignarVehiculoModal.hide();
            setTimeout(() => {
                const modalEl = document.getElementById('reasignar-vehiculo-modal');
                if (modalEl) modalEl.remove();
                window._reasignarVehiculoModal = null;
            }, 300);
        } else {
            const modalEl = document.getElementById("reasignar-vehiculo-modal");
            if (modalEl) {
                modalEl.classList.add('hidden');
                modalEl.classList.remove('flex');
                setTimeout(() => modalEl.remove(), 300);
            }
        }
    };

    window.confirmarReasignacion = async function (vehiculoId, nuevoColor, nuevoTipo) {
        try {
            // Obtener el vehículo completo del servidor
            const respuesta = await fetch(`/Vehiculo/FormPartial?id=${vehiculoId}`);
            const vehiculoHTML = await respuesta.text();

            // Parsear para extraer datos (alternativa: hacer un endpoint específico)
            const respVehiculo = await fetch(`/Vehiculo/VerificarVehiculoSinDueno?patente=${encodeURIComponent('')}&marca=&modelo=`);

            // En su lugar, vamos a usar los datos que ya tenemos y hacer la petición correcta
            const vehiculoData = await fetch(`/Cliente/GetVehiculosCliente?clienteId=${vehiculoId}`);

            // Método más directo: agregar el vehículo existente con los nuevos datos
            const vehiculoReasignado = {
                id: vehiculoId, // ID del vehículo existente
                patente: '', // Se llenará desde el modal original
                marca: '',
                modelo: '',
                color: nuevoColor,
                tipoVehiculo: nuevoTipo,
                esTemporalNuevo: false, // NO es nuevo, es reasignación
                esReasignacion: true // Flag especial para indicar reasignación
            };

            // Obtener datos del formulario que se quedó abierto
            const quickModal = document.getElementById('quick-create-modal');
            if (quickModal) {
                const form = quickModal.querySelector('form');
                if (form) {
                    vehiculoReasignado.patente = form.querySelector('#Patente')?.value?.trim().toUpperCase() || '';
                    vehiculoReasignado.marca = form.querySelector('#Marca')?.value?.trim() || '';
                    vehiculoReasignado.modelo = form.querySelector('#Modelo')?.value?.trim() || '';
                }
            }

            // Agregar a las listas
            vehiculosSeleccionados.push(vehiculoReasignado);

            // Si estaba en disponibles, removerlo
            vehiculosDisponibles = vehiculosDisponibles.filter(v => v.id !== vehiculoId);

            // Cerrar ambos modales
            closeReasignarModal();
            closeQuickCreateModal();

            // Actualizar UI
            updateVehiculosSeleccionadosList();

            showFormMessage('success', `Vehículo ${vehiculoReasignado.patente} reasignado. Guarde el cliente para confirmar.`, 4000);
        } catch (error) {
            console.error('Error al reasignar vehículo:', error);
            showFormMessage('error', 'Error al procesar la reasignación.', 5000);
        }
    };

    // ===================== Modales =====================

    function setupModals() {
        setupTipoDocumentoModal();
        setupDeleteModal();
    }

    function setupTipoDocumentoModal() {
        // Setup crear
        const formCrear = document.getElementById("formCrearTipoDocumento");
        if (formCrear && formCrear.dataset.setup !== 'true') {
            formCrear.addEventListener("submit", async function (e) {
                e.preventDefault();
                e.stopPropagation();
                await handleCrearTipoDocumento(this);
            });
            formCrear.dataset.setup = 'true';
        }
    }

    /**
     * Confirma y elimina el tipo de documento seleccionado (con modal)
     */
    window.confirmarEliminarTipoDocumento = async function () {
        const selectTipoDoc = document.getElementById('TipoDocumento');
        if (!selectTipoDoc) return;

        const tipoSeleccionado = selectTipoDoc.value?.trim();
        if (!tipoSeleccionado) {
            showFormMessage('error', 'Debe seleccionar un tipo de documento del listado.');
            cerrarModal('eliminarTipoDocumentoModal');
            return;
        }

        try {
            // Verificar si está en uso
            const checkResponse = await fetch(`/TipoDocumento/VerificarEnUso?nombre=${encodeURIComponent(tipoSeleccionado)}`);
            const checkData = await checkResponse.json();

            if (checkData.enUso) {
                cerrarModal('eliminarTipoDocumentoModal');
                showFormMessage('error', `No se puede eliminar el tipo de documento "${tipoSeleccionado}" porque está siendo usado por ${checkData.cantidad} cliente(s).`);
                return;
            }
            const formData = new FormData();
            formData.append('nombreTipo', tipoSeleccionado);

            // Obtener token antiforgery
            const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
            if (tokenInput) {
                formData.append('__RequestVerificationToken', tokenInput.value);
            }

            const response = await fetch('/TipoDocumento/EliminarTipoDocumento', {
                method: 'POST',
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' },
                body: formData
            });

            const { ok, data } = await parseJsonSafe(response);
            const success = data?.success ?? ok;
            const message = data?.message ?? (success ? 'Tipo de documento eliminado correctamente.' : 'No se pudo eliminar el tipo de documento.');

            cerrarModal('eliminarTipoDocumentoModal');

            if (success) {
                if (data?.tipos) {
                    actualizarDropdownTipos('TipoDocumento', data.tipos, null);
                }
                showFormMessage('success', message);
            } else {
                showFormMessage('error', message);
            }
        } catch (error) {
            console.error('Error:', error);
            cerrarModal('eliminarTipoDocumentoModal');
            showFormMessage('error', 'Error al eliminar el tipo de documento.');
        }
    };

    async function handleCrearTipoDocumento(form) {
        const nombreTipo = document.getElementById('nombreTipoDocumento')?.value?.trim();
        const formato = document.getElementById('formatoTipoDocumento')?.value?.trim();

        if (!nombreTipo) {
            showTipoDocumentoModalMessage('error', 'El nombre del tipo de documento es obligatorio.');
            return;
        }

        if (!formato) {
            showTipoDocumentoModalMessage('error', 'El formato del documento es obligatorio.');
            return;
        }

        if (formato.length < 3) {
            showTipoDocumentoModalMessage('error', 'El formato debe tener al menos 3 caracteres.');
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
            const message = data?.message ?? (success ? 'Tipo de documento creado.' : 'No se pudo crear el tipo de documento.');

            if (success) {
                if (data?.tipos) actualizarDropdownTipos('TipoDocumento', data.tipos, nombreTipo);
                cerrarModal('tipoDocumentoModal');
                form.reset();
                showTableMessage('success', message);
                // Recargar formatos
                await loadTiposDocumentoFormatos();
            } else {
                showTipoDocumentoModalMessage('error', message);
            }
        } catch (error) {
            console.error('Error:', error);
            showTipoDocumentoModalMessage('error', 'Error al crear el tipo de documento.');
        }
    }

    function actualizarDropdownTipos(selectId, tipos, valorSeleccionado) {
        const select = document.getElementById(selectId);
        if (!select) return;

        // Mantener la opción "Seleccione..."
        select.innerHTML = '<option value="">Seleccione...</option>';

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

    function setupDeleteModal() {
        const form = document.getElementById("formEliminarCliente");
        if (!form || form.dataset.setup === 'true') return;

        form.addEventListener("submit", function (e) {
            e.preventDefault();
            e.stopPropagation();

            const formData = new FormData(this);

            fetch(this.action, {
                method: 'POST',
                body: formData,
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
                .then(response => response.json())
                .then(data => {
                    cerrarModal('clienteConfirmModal');
                    if (data.success) {
                        showTableMessage('success', data.message);
                        reloadClienteTable(getCurrentTablePage());
                    } else {
                        showTableMessage('error', data.message);
                    }
                })
                .catch(error => {
                    console.error('Error:', error);
                    cerrarModal('clienteConfirmModal');
                    showTableMessage('error', 'Error al procesar la solicitud.');
                });
        });

        form.dataset.setup = 'true';
    }

    window.openClienteConfirmModal = async function (tipoAccion, id, nombre) {
        const modalId = 'clienteConfirmModal';
        const modal = document.getElementById(modalId);
        const title = document.getElementById('clienteConfirmTitle');
        const message = document.getElementById('clienteConfirmMessage');
        const idInput = document.getElementById('clienteConfirmId') || document.getElementById('idClienteEliminar');
        const submitBtn = document.getElementById('clienteConfirmSubmit');
        const form = document.getElementById('clienteConfirmForm') || document.getElementById('formEliminarCliente');
        const iconWrapper = document.getElementById('clienteConfirmIconWrapper');
        const icon = document.getElementById('clienteConfirmIcon');

        const esDesactivar = tipoAccion === 'desactivar';

        if (title) title.textContent = esDesactivar ? 'Desactivar Cliente' : 'Reactivar Cliente';

        if (message) {
            if (esDesactivar) {
                // Obtener vehículos del cliente para mostrar advertencia
                try {
                    const resp = await fetch(`/Cliente/GetVehiculosCliente?clienteId=${id}`);
                    const data = await resp.json();

                    let mensajeVehiculos = '';
                    if (data.success && data.vehiculos && data.vehiculos.length > 0) {
                        const vehiculosActivos = data.vehiculos.filter(v => v.estado === 'Activo');
                        if (vehiculosActivos.length > 0) {
                            mensajeVehiculos = `<div class="mt-3 p-3 bg-yellow-50 border border-yellow-200 rounded-lg dark:bg-yellow-900/20 dark:border-yellow-800">
                                <p class="text-sm font-medium text-yellow-800 dark:text-yellow-300 mb-2">
                                    ?? Advertencia: Esta acción también desactivará ${vehiculosActivos.length} vehículo${vehiculosActivos.length > 1 ? 's' : ''} asociado${vehiculosActivos.length > 1 ? 's' : ''}:
                                </p>
                                <ul class="text-xs text-yellow-700 dark:text-yellow-400 list-disc list-inside space-y-1">
                                    ${vehiculosActivos.map(v => `<li>${escapeHtml(v.patente)} - ${escapeHtml(v.marca)} ${escapeHtml(v.modelo)}</li>`).join('')}
                                </ul>
                            </div>`;
                        }
                    }

                    message.innerHTML = `
                        <p class="mb-2">¿Confirma desactivar el cliente <strong>${escapeHtml(nombre)}</strong>?</p>
                        ${mensajeVehiculos}
                    `;
                } catch (error) {
                    message.innerHTML = '¿Confirma desactivar el cliente <strong>' + escapeHtml(nombre) + '</strong>?';
                }
            } else {
                // Reactivación: mostrar advertencia de vehículos que se reactivarán
                try {
                    const resp = await fetch(`/Cliente/GetVehiculosCliente?clienteId=${id}`);
                    const data = await resp.json();

                    let mensajeVehiculos = '';
                    if (data.success && data.vehiculos && data.vehiculos.length > 0) {
                        const vehiculosInactivos = data.vehiculos.filter(v => v.estado === 'Inactivo');
                        if (vehiculosInactivos.length > 0) {
                            mensajeVehiculos = `<div class="mt-3 p-3 bg-green-50 border border-green-200 rounded-lg dark:bg-green-900/20 dark:border-green-800">
                                <p class="text-sm font-medium text-green-800 dark:text-green-300 mb-2">
                                    ?? Esta acción también reactivará ${vehiculosInactivos.length} vehículo${vehiculosInactivos.length > 1 ? 's' : ''} asociado${vehiculosInactivos.length > 1 ? 's' : ''}:
                                </p>
                                <ul class="text-xs text-green-700 dark:text-green-400 list-disc list-inside space-y-1">
                                    ${vehiculosInactivos.map(v => `<li>${escapeHtml(v.patente)} - ${escapeHtml(v.marca)} ${escapeHtml(v.modelo)}</li>`).join('')}
                                </ul>
                            </div>`;
                        }
                    }

                    message.innerHTML = `
                        <p class="mb-2">¿Confirma reactivar el cliente <strong>${escapeHtml(nombre)}</strong>?</p>
                        ${mensajeVehiculos}
                    `;
                } catch (error) {
                    message.innerHTML = '¿Confirma reactivar el cliente <strong>' + escapeHtml(nombre) + '</strong>?';
                }
            }
        }

        if (idInput) idInput.value = id;

        if (form) {
            form.action = esDesactivar ? '/Cliente/DeactivateCliente' : '/Cliente/ReactivateCliente';
        }

        if (submitBtn) {
            submitBtn.textContent = esDesactivar ? 'Desactivar' : 'Reactivar';
            submitBtn.className = esDesactivar
                ? 'py-2 px-3 text-sm font-medium text-center text-white bg-red-600 rounded-lg hover:bg-red-700 focus:ring-4 focus:outline-none focus:ring-red-300 dark:bg-red-500 dark:hover:bg-red-600 dark:focus:ring-red-900'
                : 'py-2 px-3 text-sm font-medium text-center text-white bg-green-600 rounded-lg hover:bg-green-700 focus:ring-4 focus:outline-none focus:ring-green-300 dark:bg-green-500 dark:hover:bg-green-600 dark:focus:ring-green-900';
        }

        if (iconWrapper && icon) {
            if (esDesactivar) {
                iconWrapper.className = 'w-12 h-12 rounded-full bg-red-100 dark:bg-red-900 p-2 flex items-center justify-center mx-auto mb-3.5';
                icon.setAttribute('fill', 'currentColor');
                icon.setAttribute('viewBox', '0 0 20 20');
                icon.setAttribute('class', 'w-8 h-8 text-red-600 dark:text-red-400');
                icon.innerHTML = `<path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-11.293a1 1 0 00-1.414-1.414L10 7.586 7.707 5.293a1 1 0 00-1.414 1.414L8.586 10l-2.293 2.293a.75.75 0 001.414 1.414L10 11.414l1.72 1.72a.75.75 0 001.414-1.414L13.06 10l2.293-2.293a.75.75 0 1 0-1.06-1.06L12 10.94l-1.72-1.72Z" clip-rule="evenodd"/>`;
            } else {
                iconWrapper.className = 'w-12 h-12 rounded-full bg-green-100 dark:bg-green-900 p-2 flex items-center justify-center mx-auto mb-3.5';
                icon.setAttribute('fill', 'currentColor');
                icon.setAttribute('viewBox', '0 0 24 24');
                icon.setAttribute('class', 'w-8 h-8 text-green-500 dark:text-green-400');
                icon.innerHTML = `<path fill-rule="evenodd" d="M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 4.365 9.75 9.75-4.365 9.75-9.75 9.75S2.25 17.385 2.25 12Zm13.36-1.814a.75.75 0 1 0-1.22-.872l-3.236 4.53L9.53 12.22a.75.75 0 0 0-1.06 1.06l2.25 2.25a.75.75 0 0 0 1.14-.094l3.75-5.25Z" clip-rule="evenodd"/>`;
            }
        }

        abrirModal(modalId);
    };

    window.closeClienteConfirmModal = function () {
        cerrarModal('clienteConfirmModal');
    };

    window.submitClienteEstado = function (form) {
        const fd = new FormData(form);
        fetch(form.action, {
            method: 'POST',
            body: fd,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(r => {
                if (!r.ok) throw new Error('Estado HTTP ' + r.status);
                return r.json();
            })
            .then(data => {
                closeClienteConfirmModal();
                if (data.success) {
                    const accion = form.action.includes('Deactivate') ? 'desactivado' : 'reactivado';
                    showTableMessage('success', `Cliente ${accion} correctamente.`);
                } else {
                    showTableMessage('error', data.message || 'No se pudo completar la acción.');
                }
                reloadClienteTable(getCurrentTablePage());
            })
            .catch(err => {
                console.error('submitClienteEstado error:', err);
                closeClienteConfirmModal();
                showTableMessage('error', 'No se pudo completar la acción.');
            });
        return false;
    };

    // Ver vehículos del cliente
    window.verVehiculos = async function (clienteId) {
        try {
            const resp = await fetch(`/Cliente/GetVehiculosCliente?clienteId=${clienteId}`);
            const data = await resp.json();

            if (data.success && data.vehiculos) {
                mostrarModalVehiculos(data.vehiculos);
            } else {
                showTableMessage('info', 'No se pudieron cargar los vehículos del cliente.');
            }
        } catch (error) {
            console.error('Error:', error);
            showTableMessage('error', 'Error al cargar los vehículos.');
        }
    };

    function mostrarModalVehiculos(vehiculos) {
        // Eliminar modal anterior si existe
        const existingModal = document.getElementById("ver-vehiculos-modal");
        if (existingModal) {
            existingModal.remove();
        }

        // Construir el HTML del modal
        let vehiculosHtml = '';
        if (vehiculos.length === 0) {
            vehiculosHtml = '<p class="text-center text-gray-500 dark:text-gray-400 py-4">Este cliente no tiene vehículos asociados.</p>';
        } else {
            vehiculos.forEach(v => {
                const estadoClass = v.estado === 'Activo'
                    ? 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300'
                    : 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-300';

                vehiculosHtml += `
                    <div class="p-3 bg-gray-50 dark:bg-gray-700 rounded-lg border border-gray-200 dark:border-gray-600">
                        <div class="flex justify-between items-center">
                            <div>
                                <div class="font-medium text-gray-900 dark:text-white">${escapeHtml(v.patente)}</div>
                                <div class="text-sm text-gray-500 dark:text-gray-400">${escapeHtml(v.tipoVehiculo)} - ${escapeHtml(v.marca)} ${escapeHtml(v.modelo)}</div>
                                <div class="text-xs text-gray-400 dark:text-gray-500">Color: ${escapeHtml(v.color)}</div>
                            </div>
                            <span class="px-2 py-1 text-xs font-medium ${estadoClass} rounded">${escapeHtml(v.estado)}</span>
                        </div>
                    </div>
                `;
            });
        }

        // Crear estructura de modal Flowbite
        const modalHtml = `
            <div id="ver-vehiculos-modal" tabindex="-1" aria-hidden="true" class="hidden overflow-y-auto overflow-x-hidden fixed top-0 right-0 left-0 z-50 justify-center items-center w-full md:inset-0 h-[calc(100%-1rem)] max-h-full">
                <div class="relative p-4 w-full max-w-2xl max-h-full">
                    <div class="relative bg-white rounded-lg shadow dark:bg-gray-800">
                        <div class="flex items-center justify-between p-4 md:p-5 border-b rounded-t dark:border-gray-600">
                            <h3 class="text-xl font-semibold text-gray-900 dark:text-white">
                                Vehículos del Cliente
                            </h3>
                            <button type="button" onclick="closeVerVehiculosModal()" class="text-gray-400 bg-transparent hover:bg-gray-200 hover:text-gray-900 rounded-lg text-sm w-8 h-8 ms-auto inline-flex justify-center items-center dark:hover:bg-gray-600 dark:hover:text-white">
                                <svg class="w-3 h-3" aria-hidden="true" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 14 14">
                                    <path stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="m1 1 6 6m0 0 6 6M7 7l6-6M7 7l-6 6"/>
                                </svg>
                                <span class="sr-only">Cerrar modal</span>
                            </button>
                        </div>
                        <div class="p-4 md:p-5 space-y-2">
                            ${vehiculosHtml}
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Insertar en el DOM
        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // Obtener el modal y crear instancia Flowbite
        const modalEl = document.getElementById('ver-vehiculos-modal');

        // Configurar Flowbite Modal
        if (typeof Modal !== 'undefined') {
            const modalOptions = {
                placement: 'center',
                backdrop: 'dynamic',
                closable: true,
                onHide: () => {
                    document.body.style.overflow = '';
                    // Limpiar después de cerrar
                    setTimeout(() => {
                        const modalToRemove = document.getElementById('ver-vehiculos-modal');
                        if (modalToRemove) modalToRemove.remove();
                    }, 300);
                },
                onShow: () => {
                    document.body.style.overflow = 'hidden';
                }
            };

            const modal = new Modal(modalEl, modalOptions);
            modal.show();

            // Guardar referencia para cerrar después
            window._verVehiculosModal = modal;
        } else {
            // Fallback sin Flowbite
            modalEl.classList.remove('hidden');
            modalEl.classList.add('flex');
            document.body.style.overflow = 'hidden';
        }
    }

    window.closeVerVehiculosModal = function () {
        // Intentar cerrar con Flowbite
        if (window._verVehiculosModal && typeof window._verVehiculosModal.hide === 'function') {
            window._verVehiculosModal.hide();
            window._verVehiculosModal = null;
        } else {
            // Fallback
            const modal = document.getElementById("ver-vehiculos-modal");
            if (modal) {
                modal.classList.add('hidden');
                modal.classList.remove('flex');
                setTimeout(() => modal.remove(), 300);
            }
            document.body.style.overflow = '';
        }
    };

    // ===================== Mensajería =====================

    function showFormMessage(type, message, disappearMs = 5000) {
        const container = document.getElementById('ajax-form-messages');
        if (!container) return;

        if (clienteMsgTimeout) {
            clearTimeout(clienteMsgTimeout);
            clienteMsgTimeout = null;
        }

        const color = type === 'success'
            ? { bg: 'green-50', text: 'green-800', darkText: 'green-400', border: 'green-300' }
            : type === 'info'
                ? { bg: 'blue-50', text: 'blue-800', darkText: 'blue-400', border: 'blue-300' }
                : { bg: 'red-50', text: 'red-800', darkText: 'red-400', border: 'red-300' };

        container.innerHTML = `<div class="opacity-100 transition-opacity duration-700
            p-4 mb-4 text-sm rounded-lg border
            bg-${color.bg} text-${color.text} border-${color.border}
            dark:bg-gray-800 dark:text-${color.darkText}">
            ${escapeHtml(message)}
        </div>`;

        clienteMsgTimeout = setTimeout(() => {
            const alertEl = container.firstElementChild;
            if (alertEl) {
                alertEl.classList.add('opacity-0');
                setTimeout(() => { try { alertEl.remove(); } catch { } }, 700);
            }
        }, disappearMs);
    }

    function showTableMessage(type, msg, disappearMs = 5000) {
        let container = document.getElementById('table-messages-container');

        if (!container) {
            container = document.createElement('div');
            container.id = 'table-messages-container';
            container.className = 'mb-4';

            const tableContainer = document.getElementById('cliente-table-container');
            if (tableContainer?.parentNode) {
                tableContainer.parentNode.insertBefore(container, tableContainer);
            } else {
                document.body.prepend(container);
            }
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

        container.innerHTML = `<div class="opacity-100 transition-opacity duration-700
            p-4 mb-4 text-sm rounded-lg border
            bg-${color.bg} text-${color.text} border-${color.border}
            dark:bg-gray-800 dark:text-${color.darkText}">
            ${escapeHtml(msg)}
        </div>`;

        try { container.scrollIntoView({ behavior: 'smooth', block: 'start' }); } catch { }

        tableMsgTimeout = setTimeout(() => {
            const alertEl = container.firstElementChild;
            if (alertEl) {
                alertEl.classList.add('opacity-0');
                setTimeout(() => {
                    try {
                        alertEl.remove();
                        if (container.children.length === 0) {
                            container.style.display = 'none';
                        }
                    } catch { }
                }, 750);
            }
        }, disappearMs);
    }

    // ===================== Búsqueda en servidor =====================

    function performServerSearch(searchTerm = '') {
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
        params.set('searchTerm', searchTerm);
        params.set('pageNumber', '1');
        params.set('sortBy', currentSortBy);
        params.set('sortOrder', currentSortOrder);

        const url = `/Cliente/SearchPartial?${params.toString()}`;

        fetch(url, {
            headers: { 'X-Requested-With': 'XMLHttpRequest', 'Cache-Control': 'no-cache' },
            cache: 'no-store'
        })
            .then(r => r.text())
            .then(html => {
                const cont = document.getElementById('cliente-table-container');
                if (cont) {
                    cont.innerHTML = html;
                    const cp = document.getElementById('current-page-value')?.value;
                    if (cp) cont.dataset.currentPage = cp;
                }
            })
            .catch(e => {
                console.error('Error en búsqueda:', e);
                showTableMessage('error', 'Error al realizar la búsqueda.');
            });
    }

    // ===================== Utilidades =====================

    /**
     * Muestra un mensaje dentro del modal de creación rápida de vehículo
     */
    function showQuickVehiculoMessage(type, message, disappearMs = 5000) {
        const container = document.getElementById('quick-vehiculo-messages');
        if (!container) {
            // Si no existe el contenedor, usar la notificación estándar
            showFormMessage(type, message, disappearMs);
            return;
        }

        const color = type === 'success'
            ? { bg: 'green-50', text: 'green-800', darkText: 'green-400', border: 'green-300', icon: 'M10 .5a9.5 9.5 0 1 0 9.5 9.5A9.51 9.51 0 0 0 10 .5ZM9.5 4a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3ZM12 15H8a1 1 0 0 1 0-2h1v-3H8a1 1 0 0 1 0-2h2a1 1 0 0 1 1 1v4h1a1 1 0 0 1 0 2Z' }
            : type === 'info'
                ? { bg: 'blue-50', text: 'blue-800', darkText: 'blue-400', border: 'blue-300', icon: 'M10 .5a9.5 9.5 0 1 0 9.5 9.5A9.51 9.51 0 0 0 10 .5ZM9.5 4a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3ZM12 15H8a1 1 0 0 1 0-2h1v-3H8a1 1 0 0 1 0-2h2a1 1 0 0 1 1 1v4h1a1 1 0 0 1 0 2Z' }
                : { bg: 'red-50', text: 'red-800', darkText: 'red-400', border: 'red-300', icon: 'M10 .5a9.5 9.5 0 1 0 9.5 9.5A9.51 9.51 0 0 0 10 .5Zm3.707 8.207-4 4a1 1 0 0 1-1.414 0l-2-2a1 1 0 0 1 1.414-1.414L9 10.586l3.293-3.293a1 1 0 0 1 1.414 1.414Z' };

        container.innerHTML = `
            <div class="flex items-center p-4 mb-4 text-sm rounded-lg border bg-${color.bg} text-${color.text} border-${color.border} dark:bg-gray-800 dark:text-${color.darkText}" role="alert">
                <svg class="flex-shrink-0 inline w-4 h-4 me-3" aria-hidden="true" xmlns="http://www.w3.org/2000/svg" fill="currentColor" viewBox="0 0 20 20">
                    <path d="${color.icon}"/>
                </svg>
                <span class="sr-only">${type === 'error' ? 'Error' : type === 'success' ? 'Success' : 'Info'}</span>
                <div class="flex-1">${escapeHtml(message)}</div>
            </div>
        `;

        // Auto-ocultar después del tiempo especificado
        if (disappearMs > 0) {
            setTimeout(() => {
                const alertEl = container.firstElementChild;
                if (alertEl) {
                    alertEl.classList.add('opacity-0', 'transition-opacity', 'duration-700');
                    setTimeout(() => {
                        try {
                            container.innerHTML = '';
                        } catch { }
                    }, 700);
                }
            }, disappearMs);
        }
    }
    /**
 * Muestra un mensaje dentro del modal de tipo documento
 */
    function showTipoDocumentoModalMessage(type, message, disappearMs = 5000) {
        const container = document.getElementById('tipo-documento-modal-messages');
        if (!container) {
            showTableMessage(type, message, disappearMs);
            return;
        }

        const color = type === 'success'
            ? { bg: 'green-50', text: 'green-800', darkText: 'green-400', border: 'green-300', icon: 'M10 .5a9.5 9.5 0 1 0 9.5 9.5A9.51 9.51 0 0 0 10 .5ZM9.5 4a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3ZM12 15H8a1 1 0 0 1 0-2h1v-3H8a1 1 0 0 1 0-2h2a1 1 0 0 1 1 1v4h1a1 1 0 0 1 0 2Z' }
            : type === 'info'
                ? { bg: 'blue-50', text: 'blue-800', darkText: 'blue-400', border: 'blue-300', icon: 'M10 .5a9.5 9.5 0 1 0 9.5 9.5A9.51 9.51 0 0 0 10 .5ZM9.5 4a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3ZM12 15H8a1 1 0 0 1 0-2h1v-3H8a1 1 0 0 1 0-2h2a1 1 0 0 1 1 1v4h1a1 1 0 0 1 0 2Z' }
                : { bg: 'red-50', text: 'red-800', darkText: 'red-400', border: 'red-300', icon: 'M10 .5a9.5 9.5 0 1 0 9.5 9.5A9.51 9.51 0 0 0 10 .5Zm3.707 8.207-4 4a1 1 0 0 1-1.414 0l-2-2a1 1 0 0 1 1.414-1.414L9 10.586l3.293-3.293a1 1 0 0 1 1.414 1.414Z' };

        container.innerHTML = `
        <div class="flex items-center p-4 mb-4 text-sm rounded-lg border bg-${color.bg} text-${color.text} border-${color.border} dark:bg-gray-800 dark:text-${color.darkText}" role="alert">
            <svg class="flex-shrink-0 inline w-4 h-4 me-3" aria-hidden="true" xmlns="http://www.w3.org/2000/svg" fill="currentColor" viewBox="0 0 20 20">
                <path d="${color.icon}"/>
            </svg>
            <span class="sr-only">${type === 'error' ? 'Error' : type === 'success' ? 'Success' : 'Info'}</span>
            <div class="flex-1">${escapeHtml(message)}</div>
        </div>
    `;

        if (disappearMs > 0) {
            setTimeout(() => {
                const alertEl = container.firstElementChild;
                if (alertEl) {
                    alertEl.classList.add('opacity-0', 'transition-opacity', 'duration-700');
                    setTimeout(() => {
                        try { container.innerHTML = ''; } catch { }
                    }, 700);
                }
            }, disappearMs);
        }
    }
    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

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

    function getFlowbiteModal(modalEl) {
        if (!modalEl || typeof window !== 'object' || typeof window.Modal === 'undefined') return null;
        const opts = { backdrop: 'static', closable: false };
        if (typeof Modal.getInstance === 'function') {
            const existing = Modal.getInstance(modalEl);
            if (existing) return existing;
        }
        if (typeof Modal.getOrCreateInstance === 'function') {
            return Modal.getOrCreateInstance(modalEl, opts);
        }
        try { return new Modal(modalEl, opts); } catch { return null; }
    }

    function abrirModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        try {
            const inst = getFlowbiteModal(modal);
            if (inst && typeof inst.show === 'function') {
                inst.show();
                return;
            }
        } catch { }

        modal.classList.remove('hidden');
        modal.setAttribute('aria-hidden', 'false');
    }

    function cerrarModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        try {
            const inst = getFlowbiteModal(modal);
            if (inst && typeof inst.hide === 'function') {
                inst.hide();
            }
        } catch { }

        modal.classList.add('hidden');
        modal.setAttribute('aria-hidden', 'true');
        document.querySelectorAll('[modal-backdrop]').forEach(b => b.remove());
        document.body.classList.remove('overflow-hidden');
    }
    /**
    * Cierra el modal de tipo documento y limpia campos
    */
    window.closeTipoDocumentoModal = function () {
        // Limpiar campos
        const nombreInput = document.getElementById('nombreTipoDocumento');
        const formatoInput = document.getElementById('formatoTipoDocumento');
        if (nombreInput) nombreInput.value = '';
        if (formatoInput) formatoInput.value = '';

        // Limpiar mensajes
        const messagesContainer = document.getElementById('tipo-documento-modal-messages');
        if (messagesContainer) messagesContainer.innerHTML = '';
    };
})();