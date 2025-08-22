// =====================================
// INICIALIZACIÓN PRINCIPAL
// =====================================
document.addEventListener('DOMContentLoaded', function () {
    // Inicialización general
    initializeByPage();
    initializeFilterForm();
    setupFormValidation();
    setupModals();
    setupAccordion();

    // Auto-ocultar alertas después de 5 segundos
    setTimeout(function () {
        const errorAlert = document.getElementById('error-alert');
        const successAlert = document.getElementById('success-alert');
        if (errorAlert) errorAlert.style.display = 'none';
        if (successAlert) successAlert.style.display = 'none';
    }, 5000);

    // Configurar autoGrow para el campo de descripción
    const descripcionField = document.getElementById('Descripcion');
    if (descripcionField) {
        autoGrow(descripcionField);
    }

    // Configurar el evento click del botón de filtro
    setupFilterDropdown();
});

// =====================================
// INICIALIZACIÓN DE COMPONENTES POR PÁGINA
// =====================================
function initializeByPage() {
    // Para la página de Personal
    if (document.querySelector('form[id^="rol-form-"]')) {
        initializePersonalPage();
    }

    // Para la página de Servicios
    if (document.getElementById('servicio-form')) {
        initializeServiciosPage();
    }

    // Funcionalidad común de filtrado de tablas
    if (document.querySelector('table')) {
        setupTableFilter();
    }
}

// =====================================
// FILTROS Y TABLAS
// =====================================
function initializeFilterForm() {
    const filterForm = document.getElementById('filterForm');
    if (filterForm) {
        filterForm.addEventListener('submit', function (e) {
            // Verificar checkboxes de estados
            const estadosCheckboxes = Array.from(this.querySelectorAll('input[name="estados"]:checked'));

            // Si no hay checkboxes de estados marcados, forzar "Activo"
            if (estadosCheckboxes.length === 0) {
                e.preventDefault();
                const activoCheckbox = this.querySelector('input[name="estados"][value="Activo"]');
                if (activoCheckbox) {
                    activoCheckbox.checked = true;
                    this.submit();
                }
            }
        });
    }
}

function setupTableFilter() {
    const searchInput = document.getElementById('simple-search');
    if (searchInput) {
        searchInput.addEventListener('input', filterTable);
    }
}

function filterTable() {
    const input = document.getElementById("simple-search");
    if (!input) return;

    const filter = input.value.toUpperCase();
    const table = document.querySelector("table");
    if (!table) return;

    const rows = table.getElementsByTagName("tr");

    for (let i = 1; i < rows.length; i++) {
        rows[i].style.display = "none";
        const cells = rows[i].getElementsByTagName("td");
        for (let j = 0; j < cells.length; j++) {
            if (cells[j]) {
                const txtValue = cells[j].textContent || cells[j].innerText;
                if (txtValue.toUpperCase().indexOf(filter) > -1) {
                    rows[i].style.display = "";
                    break;
                }
            }
        }
    }
}

function setupFilterDropdown() {
    const filterButton = document.getElementById('filterDropdownButton');
    const filterDropdown = document.getElementById('filterDropdown');

    if (filterButton && filterDropdown) {
        filterButton.addEventListener('click', function () {
            // Calcular la posición del dropdown basada en el botón
            const buttonRect = filterButton.getBoundingClientRect();
            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;

            // Establecer la posición del dropdown para que se muestre debajo del botón
            filterDropdown.style.position = 'fixed';
            filterDropdown.style.top = (buttonRect.bottom + scrollTop) + 'px';
            filterDropdown.style.left = buttonRect.left + 'px';
            filterDropdown.style.maxHeight = '80vh'; // Altura máxima para que quepa en la pantalla
            filterDropdown.style.overflowY = 'auto'; // Permitir scroll si el contenido es demasiado largo
        });
    }
}

// =====================================
// GESTIÓN DE PERSONAL
// =====================================
let isEditing = false;

function initializePersonalPage() {
    // Configurar toggleEdit y escucha de clics fuera de los formularios
    setupRoleEditing();

    // Configurar formularios de reactivación
    document.querySelectorAll('form[asp-action="ReactivateEmployee"]').forEach(form => {
        form.addEventListener('submit', function (e) {
            e.preventDefault();

            fetch(this.action, {
                method: 'POST',
                body: new FormData(this)
            }).then(response => {
                if (response.ok) {
                    location.reload(); // Recargar para ver cambios
                }
            });
        });
    });
}

function setupRoleEditing() {
    // Configurar detección de clics fuera de los formularios de edición
    document.addEventListener('click', function (event) {
        let isClickInside = false;
        const forms = document.querySelectorAll('form[id^="rol-form-"]');

        forms.forEach(function (form) {
            if (form.contains(event.target) || event.target.closest('button[onclick^="toggleEdit"]')) {
                isClickInside = true;
            }
        });

        if (!isClickInside && isEditing) {
            location.reload(); // Refrescar la página para cancelar la edición
        }
    });
}

function toggleEdit(id) {
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
}

function submitForm(id) {
    const form = document.getElementById('rol-form-' + id);
    if (form) form.submit();
}

// =====================================
// GESTIÓN DE SERVICIOS
// =====================================
function initializeServiciosPage() {
    // Verificar si estamos editando para abrir el acordeón
    if (document.getElementById('form-title').textContent.includes('Editando')) {
        const accordion = document.getElementById('accordion-flush-body-1');
        if (accordion) accordion.classList.remove('hidden');
    }
}

function clearForm() {
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

function autoGrow(element) {
    element.style.height = '2.5rem'; // Altura inicial
    element.style.height = (element.scrollHeight) + 'px'; // Ajustar a contenido
}

// =====================================
// GESTIÓN DE MODALES
// =====================================
function setupModals() {
    // Configurar modales de tipo de servicio
    setupServiceTypeModals();

    // Configurar modales de tipo de vehículo
    setupVehicleTypeModals();
}

function setupServiceTypeModals() {
    // Botón para eliminar tipo de servicio
    const btnEliminarTipo = document.querySelector('[data-modal-toggle="eliminarTipoModal"]');
    if (btnEliminarTipo) {
        btnEliminarTipo.addEventListener('click', function () {
            const tipoSeleccionado = document.getElementById('Tipo').value;
            document.getElementById('nombreTipoEliminar').value = tipoSeleccionado;
        });
    }

    // Botón para cancelar en el modal de tipo de servicio
    const cancelarTipoBtn = document.querySelector('#defaultModal button[type="button"]');
    if (cancelarTipoBtn) {
        cancelarTipoBtn.addEventListener('click', function () {
            limpiarModalTipoServicio();
        });
    }
}

function setupVehicleTypeModals() {
    // Botón para eliminar tipo de vehículo
    const btnEliminarTipoVehiculo = document.querySelector('[data-modal-toggle="eliminarTipoVehiculoModal"]');
    if (btnEliminarTipoVehiculo) {
        btnEliminarTipoVehiculo.addEventListener('click', function () {
            const tipoSeleccionado = document.getElementById('TipoVehiculo').value;
            document.getElementById('nombreTipoVehiculoEliminar').value = tipoSeleccionado;
        });
    }

    // Botón para cancelar en el modal de tipo de vehículo
    const cancelarTipoVehiculoBtn = document.querySelector('#tipoVehiculoModal button[type="button"]');
    if (cancelarTipoVehiculoBtn) {
        cancelarTipoVehiculoBtn.addEventListener('click', function () {
            limpiarModalTipoVehiculo();
        });
    }
}

function limpiarModalTipoServicio() {
    document.getElementById('nombreTipo').value = '';
    // Cerrar el modal
    document.querySelector('[data-modal-toggle="defaultModal"]').click();
}

function limpiarModalTipoVehiculo() {
    document.getElementById('nombreTipoVehiculo').value = '';
    // Cerrar el modal
    document.querySelector('[data-modal-toggle="tipoVehiculoModal"]').click();
}

// =====================================
// ACORDEONES Y UI
// =====================================
function setupAccordion() {
    // Solo abrir el acordeón si hay errores o mensajes
    const hayErrores = document.getElementById('error-alert') || document.getElementById('success-alert');
    const servicioFormErrors = document.getElementById('servicio-form')?.querySelector('.validation-message');
    const formularioEdicion = document.getElementById('form-title')?.textContent.includes('Editando');

    if (hayErrores || servicioFormErrors || formularioEdicion) {
        const accordion = document.getElementById('accordion-flush-body-1');
        if (accordion && accordion.classList.contains('hidden')) {
            accordion.classList.remove('hidden');
            // También actualizar el aria-expanded del botón
            const accordionButton = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
            if (accordionButton) {
                accordionButton.setAttribute('aria-expanded', 'true');
                // Girar la flecha
                const icon = accordionButton.querySelector('[data-accordion-icon]');
                if (icon) icon.classList.add('rotate-180');
            }
        }
    }
    // Acordeón se mantendrá cerrado si no se cumple ninguna condición
}
// =====================================
// VALIDACIÓN DE FORMULARIOS
// =====================================
function setupFormValidation() {
    const servicioForm = document.getElementById('servicio-form');
    if (!servicioForm) return;

    // Configurar validación de campos individuales
    setupFieldValidation();

    // Validación en envío del formulario
    servicioForm.addEventListener('submit', validateFormOnSubmit);
}

function setupFieldValidation() {
    const nombreInput = document.getElementById('Nombre');
    const precioInput = document.getElementById('Precio');
    const tiempoEstimadoInput = document.getElementById('TiempoEstimado');

    // Validación del campo Nombre
    if (nombreInput) {
        nombreInput.addEventListener('input', function () {
            const regex = /^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]*$/;
            if (!regex.test(this.value)) {
                this.classList.add('border-red-500');
                // Eliminar caracteres no permitidos
                this.value = this.value.replace(/[^a-zA-ZáéíóúÁÉÍÓÚñÑ\s]/g, '');
            } else {
                this.classList.remove('border-red-500');
            }
        });
    }

    // Validación del campo Precio
    if (precioInput) {
        precioInput.addEventListener('input', function () {
            const value = parseFloat(this.value);
            if (isNaN(value) || value < 0) {
                this.classList.add('border-red-500');
                // Convertir valores negativos a positivos
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

    // Validación del campo Tiempo Estimado
    if (tiempoEstimadoInput) {
        tiempoEstimadoInput.addEventListener('input', function () {
            const value = parseInt(this.value);
            if (isNaN(value) || value <= 0) {
                this.classList.add('border-red-500');
                // Valores negativos o cero se convierten a 1
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
}

function validateFormOnSubmit(e) {
    const nombreInput = document.getElementById('Nombre');
    const precioInput = document.getElementById('Precio');
    const tiempoEstimadoInput = document.getElementById('TiempoEstimado');
    const servicioForm = this;

    // Verificar que todos los campos required estén llenos
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
        showFormError("Por favor, complete todos los campos obligatorios.");
        return;
    }

    // Validaciones específicas de los campos
    const nombreValue = nombreInput.value.trim();
    const precioValue = parseFloat(precioInput.value);
    const tiempoEstimadoValue = parseInt(tiempoEstimadoInput.value);

    let hasErrors = false;
    let errorMessages = [];

    // Validar nombre (solo letras y espacios)
    const nombreRegex = /^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$/;
    if (!nombreRegex.test(nombreValue) || nombreValue === '') {
        nombreInput.classList.add('border-red-500');
        errorMessages.push("El nombre solo puede contener letras y espacios");
        hasErrors = true;
    } else {
        nombreInput.classList.remove('border-red-500');
    }

    // Validar precio (no negativo)
    if (isNaN(precioValue) || precioValue < 0) {
        precioInput.classList.add('border-red-500');
        errorMessages.push("El precio debe ser igual o mayor a 0");
        hasErrors = true;
    } else {
        precioInput.classList.remove('border-red-500');
    }

    // Validar tiempo estimado (mayor a 0)
    if (isNaN(tiempoEstimadoValue) || tiempoEstimadoValue <= 0) {
        tiempoEstimadoInput.classList.add('border-red-500');
        errorMessages.push("El tiempo estimado debe ser mayor a 0");
        hasErrors = true;
    } else {
        tiempoEstimadoInput.classList.remove('border-red-500');
    }

    // Si hay errores, prevenir envío y mostrar mensajes
    if (hasErrors) {
        e.preventDefault();
        showFormError(errorMessages.join('<br>'));
    }
}

function showFormError(message) {
    // Abrir el acordeón si está cerrado
    const accordion = document.getElementById('accordion-flush-body-1');
    if (accordion && accordion.classList.contains('hidden')) {
        accordion.classList.remove('hidden');
    }

    // Mostrar mensaje de error
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
// ===== AJAX Servicios (form + tabla) =====
function loadServicioForm(id) {
    const url = '/Servicio/FormPartial' + (id ? ('?id=' + encodeURIComponent(id)) : '');
    fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(r => r.text())
        .then(html => {
            document.getElementById('servicio-form-container').innerHTML = html;
            setupFormValidation();
            const desc = document.getElementById('Descripcion');
            if (desc) autoGrow(desc);
            // Abrir acordeón si estaba cerrado
            const accordionBody = document.getElementById('accordion-flush-body-1');
            if (accordionBody && accordionBody.classList.contains('hidden')) {
                accordionBody.classList.remove('hidden');
            }
        })
        .catch(e => console.error('Error cargando formulario:', e));
}

function reloadServicioTable(page) {
    // Tomar filtros actuales desde DOM (solo si quieres ampliar en siguiente paso)
    const url = '/Servicio/TablePartial?pageNumber=' + page;
    fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(r => r.text())
        .then(html => {
            document.getElementById('servicio-table-container').innerHTML = html;
        })
        .catch(e => console.error('Error cargando tabla:', e));
}

function submitServicioAjax(form) {
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
            setupFormValidation();
            const desc = document.getElementById('Descripcion');
            if (desc) autoGrow(desc);
            if (r.valid) {
                showToast(r.msg || 'Operación exitosa', false);
                reloadServicioTable(1);
            } else {
                showToast('Revise los errores del formulario', true);
            }
        })
        .catch(e => console.error('Error enviando formulario:', e));
    return false;
}

// Cambiar estado (desactivar/reactivar) sin recargar toda la página
function submitEstadoServicio(form) {
    const fd = new FormData(form);
    fetch(form.action, {
        method: 'POST',
        body: fd,
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    }).then(() => reloadServicioTable(getCurrentTablePage()));
    return false;
}

function showToast(msg, isError) {
    let el = document.getElementById('toast-msg');
    if (!el) {
        el = document.createElement('div');
        el.id = 'toast-msg';
        document.body.appendChild(el);
    }
    el.className = 'fixed top-4 right-4 z-50 px-4 py-2 rounded shadow text-sm font-medium ' + (isError ? 'bg-red-600 text-white' : 'bg-green-600 text-white');
    el.textContent = msg;
    setTimeout(() => el.remove(), 4000);
}
function reloadServicioTable(page) {
    fetch('/Servicio/TablePartial?pageNumber=' + page, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(r => r.text())
        .then(html => {
            const cont = document.getElementById('servicio-table-container');
            cont.innerHTML = html;
            const cp = document.getElementById('current-page-value')?.value;
            if (cp) cont.dataset.currentPage = cp;
        })
        .catch(e => console.error('Error cargando tabla:', e));
}

function getCurrentTablePage() {
    return parseInt(document.getElementById('servicio-table-container')?.dataset.currentPage || '1');
}
// ================== Modal reutilizable Servicios ==================
function openServicioConfirmModal(tipoAccion, id, nombre) {
    const modal = document.getElementById('servicioConfirmModal');
    const title = document.getElementById('servicioConfirmTitle');
    const msg = document.getElementById('servicioConfirmMessage');
    const submitBtn = document.getElementById('servicioConfirmSubmit');
    const form = document.getElementById('servicioConfirmForm');
    const idInput = document.getElementById('servicioConfirmId');

    // Iconos y wrapper
    const iconWrapper = document.getElementById('servicioConfirmIconWrapper');
    const icon = document.getElementById('servicioConfirmIcon');

    idInput.value = id;

    if (tipoAccion === 'desactivar') {
        // Texto
        title.textContent = 'Desactivar Servicio';
        msg.innerHTML = '¿Confirma desactivar el servicio <strong>' + escapeHtml(nombre) + '</strong>?';
        form.action = '/Servicio/DeactivateServicio';
        submitBtn.textContent = 'Desactivar';

        // Estilos botón
        submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-red-600 rounded-lg hover:bg-red-700 focus:ring-4 focus:outline-none focus:ring-red-300 dark:bg-red-500 dark:hover:bg-red-600 dark:focus:ring-red-900';

        // Icono rojo ❌
        iconWrapper.className = 'w-12 h-12 rounded-full bg-red-100 dark:bg-red-900 p-2 flex items-center justify-center mx-auto mb-3.5';
        icon.setAttribute('fill', 'currentColor');
        icon.setAttribute('viewBox', '0 0 20 20');
        icon.setAttribute('class', 'w-8 h-8 text-red-600 dark:text-red-400');
        icon.innerHTML = `<path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-11.293a1 1 0 
        00-1.414-1.414L10 7.586 7.707 5.293a1 1 0 
        00-1.414 1.414L8.586 10l-2.293 2.293a1 1 0 
        001.414 1.414L10 12.414l2.293 2.293a1 1 0 
        001.414-1.414L11.414 10l2.293-2.293z" clip-rule="evenodd"/>`;

    } else { // reactivar
        // Texto
        title.textContent = 'Reactivar Servicio';
        msg.innerHTML = '¿Confirma reactivar el servicio <strong>' + escapeHtml(nombre) + '</strong>?';
        form.action = '/Servicio/ReactivateServicio';
        submitBtn.textContent = 'Reactivar';

        // Estilos botón
        submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-green-600 rounded-lg hover:bg-green-700 focus:ring-4 focus:outline-none focus:ring-green-300 dark:bg-green-500 dark:hover:bg-green-600 dark:focus:ring-green-900';

        // Icono verde ✅
        iconWrapper.className = 'w-12 h-12 rounded-full bg-green-100 dark:bg-green-900 p-2 flex items-center justify-center mx-auto mb-3.5';
        icon.setAttribute('fill', 'currentColor');
        icon.setAttribute('viewBox', '0 0 24 24');
        icon.setAttribute('class', 'w-8 h-8 text-green-500 dark:text-green-400');
        icon.innerHTML = `<path fill-rule="evenodd" d="M2.25 12c0-5.385 
        4.365-9.75 9.75-9.75s9.75 4.365 
        9.75 9.75-4.365 9.75-9.75 
        9.75S2.25 17.385 2.25 
        12Zm13.36-1.814a.75.75 0 1 0-1.22-.872l-3.236 
        4.53L9.53 12.22a.75.75 0 0 0-1.06 
        1.06l2.25 2.25a.75.75 0 0 0 
        1.14-.094l3.75-5.25Z" clip-rule="evenodd"/>`;
    }

    modal.classList.remove('hidden');
}

function closeServicioConfirmModal() {
    const modal = document.getElementById('servicioConfirmModal');
    modal.classList.add('hidden');
}


function submitServicioEstado(form) {
    const fd = new FormData(form);
    fetch(form.action, {
        method: 'POST',
        body: fd,
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    }).then(r => {
        if (!r.ok) throw new Error('Error estado');
        closeServicioConfirmModal();
        showToast('Operación realizada', false);
        reloadServicioTable(getCurrentTablePage());
    }).catch(e => {
        console.error(e);
        showToast('Error procesando la operación', true);
    });
    return false;
}

function escapeHtml(str) {
    return str.replace(/[&<>"']/g, c => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
}