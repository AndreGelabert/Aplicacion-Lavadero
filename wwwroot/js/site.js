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