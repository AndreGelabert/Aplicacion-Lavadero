// Funciones de utilidad generales
document.addEventListener('DOMContentLoaded', function () {
    // Inicializar funcionalidades basadas en la presencia de elementos específicos
    initializeByPage();

    // Inicializar el formulario de filtro (común en varias páginas)
    initializeFilterForm();

    // Inicializar autoGrow para el campo de descripción si existe
    const descripcionField = document.getElementById('Descripcion');
    if (descripcionField) {
        autoGrow(descripcionField);
    }
});

// Inicializa las funcionalidades específicas de cada página
function initializeByPage() {
    // Para la página de Personal
    if (document.querySelector('form[id^="rol-form-"]')) {
        initializePersonalPage();
    }

    // Para la página de Servicios
    if (document.getElementById('servicio-form')) {
        initializeServiciosPage();
    }

    // Funcionalidad común de filtrado de tablas (si existe la tabla)
    if (document.querySelector('table')) {
        setupTableFilter();
    }
}

// Inicializa el formulario de filtro común
function initializeFilterForm() {
    const filterForm = document.getElementById('filterForm');
    if (filterForm) {
        filterForm.addEventListener('submit', function (e) {
            const checkboxes = Array.from(this.querySelectorAll('input[name="estados"]:checked'));

            // Si no hay checkboxes marcados, forzar "Activo"
            if (checkboxes.length === 0) {
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

// Función para filtrar tablas por texto
function setupTableFilter() {
    const searchInput = document.getElementById('simple-search');
    if (searchInput) {
        searchInput.addEventListener('input', filterTable);
    }
}

function filterTable() {
    var input, filter, table, tr, td, i, j, txtValue;
    input = document.getElementById("simple-search");
    if (!input) return;

    filter = input.value.toUpperCase();
    table = document.querySelector("table");
    if (!table) return;

    tr = table.getElementsByTagName("tr");

    for (i = 1; i < tr.length; i++) {
        tr[i].style.display = "none";
        td = tr[i].getElementsByTagName("td");
        for (j = 0; j < td.length; j++) {
            if (td[j]) {
                txtValue = td[j].textContent || td[j].innerText;
                if (txtValue.toUpperCase().indexOf(filter) > -1) {
                    tr[i].style.display = "";
                    break;
                }
            }
        }
    }
}

// Funcionalidades específicas para la página de Personal
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

var isEditing = false;
function setupRoleEditing() {
    // Configurar detección de clics fuera de los formularios de edición
    document.addEventListener('click', function (event) {
        var isClickInside = false;
        var forms = document.querySelectorAll('form[id^="rol-form-"]');
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
    var rolText = document.getElementById('rol-text-' + id);
    var rolForm = document.getElementById('rol-form-' + id);
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
    var form = document.getElementById('rol-form-' + id);
    if (form) form.submit();
}

// Funcionalidades específicas para la página de Servicios
function initializeServiciosPage() {
    // Verificar si estamos editando para abrir el acordeón
    if (document.getElementById('form-title').textContent.includes('Editando')) {
        var accordion = document.getElementById('accordion-flush-body-1');
        if (accordion) accordion.classList.remove('hidden');
    }
}
function autoGrow(element) {
    element.style.height = '2.5rem'; // Altura inicial
    element.style.height = (element.scrollHeight) + 'px'; // Ajustar a contenido
}
function clearForm() {
    const idField = document.getElementById('Id');
    const nombreField = document.getElementById('Nombre');
    const precioField = document.getElementById('Precio');
    const tipoField = document.getElementById('Tipo');
    const tipoVehiculoField = document.getElementById('TipoVehiculo');
    const tiempoEstimadoField = document.getElementById('TiempoEstimado');
    const descripcionField = document.getElementById('Descripcion');

    if (idField) idField.value = '';
    if (nombreField) nombreField.value = '';
    if (precioField) precioField.value = '';
    if (tipoField) tipoField.selectedIndex = 0;
    if (tipoVehiculoField) tipoVehiculoField.selectedIndex = 0;
    if (tiempoEstimadoField) tiempoEstimadoField.value = '';
    if (descripcionField) descripcionField.value = '';
}

function initializeFilterForm() {
    const filterForm = document.getElementById('filterForm');
    if (filterForm) {
        filterForm.addEventListener('submit', function (e) {
            // Verificar checkboxes de estados
            const estadosCheckboxes = Array.from(this.querySelectorAll('input[name="estados"]:checked'));

            // Si no hay checkboxes de estados marcados, forzar "Activo"
            if (estadosCheckboxes.length === 0) {
                const activoCheckbox = this.querySelector('input[name="estados"][value="Activo"]');
                if (activoCheckbox) {
                    activoCheckbox.checked = true;
                }
            }

            // No se necesita preventDefault() ni manual submit ya que queremos que el formulario se envíe normalmente
        });
    }
}
// Función para limpiar y cerrar el modal de tipo de servicio
function limpiarModalTipoServicio() {
    document.getElementById('nombreTipo').value = '';
    // Cerrar el modal
    document.querySelector('[data-modal-toggle="defaultModal"]').click();
}

// Funciones para gestionar el modal de eliminación de tipo de servicio
document.addEventListener('DOMContentLoaded', function () {
    // Setup para el formulario de eliminación de tipo
    const btnEliminarTipo = document.querySelector('[data-modal-toggle="eliminarTipoModal"]');
    if (btnEliminarTipo) {
        btnEliminarTipo.addEventListener('click', function () {
            const tipoSeleccionado = document.getElementById('Tipo').value;
            document.getElementById('nombreTipoEliminar').value = tipoSeleccionado;
        });
    }

    // Auto-ocultar alertas después de 5 segundos
    setTimeout(function () {
        const errorAlert = document.getElementById('error-alert');
        const successAlert = document.getElementById('success-alert');
        if (errorAlert) errorAlert.style.display = 'none';
        if (successAlert) successAlert.style.display = 'none';
    }, 5000);

    // Asegurar que el botón de cancelar del modal de tipo servicio funcione
    const cancelarTipoBtn = document.querySelector('#defaultModal button[type="button"]');
    if (cancelarTipoBtn) {
        cancelarTipoBtn.addEventListener('click', function () {
            limpiarModalTipoServicio();
        });
    }
});
// Función para limpiar y cerrar el modal de tipo de vehículo
function limpiarModalTipoVehiculo() {
    document.getElementById('nombreTipoVehiculo').value = '';
    // Cerrar el modal
    document.querySelector('[data-modal-toggle="tipoVehiculoModal"]').click();
}

// Funciones para gestionar el modal de eliminación de tipo de vehículo
document.addEventListener('DOMContentLoaded', function () {
    // Setup para el formulario de eliminación de tipo de vehículo
    const btnEliminarTipoVehiculo = document.querySelector('[data-modal-toggle="eliminarTipoVehiculoModal"]');
    if (btnEliminarTipoVehiculo) {
        btnEliminarTipoVehiculo.addEventListener('click', function () {
            const tipoSeleccionado = document.getElementById('TipoVehiculo').value;
            document.getElementById('nombreTipoVehiculoEliminar').value = tipoSeleccionado;
        });
    }

    // Asegurar que el botón de cancelar del modal de tipo de vehículo funcione
    const cancelarTipoVehiculoBtn = document.querySelector('#tipoVehiculoModal button[type="button"]');
    if (cancelarTipoVehiculoBtn) {
        cancelarTipoVehiculoBtn.addEventListener('click', function () {
            limpiarModalTipoVehiculo();
        });
    }
});
// Función para asegurar que el acordeón se abra cuando hay errores de validación
document.addEventListener('DOMContentLoaded', function () {
    // Verificar si hay errores en el modelo (ModelState)
    const servicioForm = document.getElementById('servicio-form');
    if (servicioForm && servicioForm.querySelector('.validation-message')) {
        // Abrir el acordeón si hay errores de validación
        const accordion = document.getElementById('accordion-flush-body-1');
        if (accordion && accordion.classList.contains('hidden')) {
            accordion.classList.remove('hidden');
        }
    }
});
// Monitorear el envío del formulario de servicio
document.addEventListener('DOMContentLoaded', function () {
    const servicioForm = document.getElementById('servicio-form');
    if (servicioForm) {
        servicioForm.addEventListener('submit', function (e) {
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
                // Abrir el acordeón si está cerrado
                const accordion = document.getElementById('accordion-flush-body-1');
                if (accordion && accordion.classList.contains('hidden')) {
                    accordion.classList.remove('hidden');
                }

                // Mostrar mensaje de error
                let errorDiv = document.getElementById('form-error-message');
                if (!errorDiv) {
                    errorDiv = document.createElement('div');
                    errorDiv.id = 'form-error-message';
                    errorDiv.className = 'p-4 mb-4 text-sm text-red-800 rounded-lg bg-red-50 dark:bg-gray-800 dark:text-red-400';
                    errorDiv.innerHTML = '<span class="font-medium">¡Error!</span> Por favor, complete todos los campos obligatorios.';
                    servicioForm.appendChild(errorDiv);
                }
            }
        });
    }
});
// Agregar al final del archivo
document.addEventListener('DOMContentLoaded', function () {
    // Verificar si hay mensajes de error o éxito
    if (document.getElementById('error-alert') || document.getElementById('success-alert')) {
        const accordion = document.getElementById('accordion-flush-body-1');
        if (accordion && accordion.classList.contains('hidden')) {
            accordion.classList.remove('hidden');
        }
    }
});

// Agregar estas funciones para la validación del formulario de servicios
document.addEventListener('DOMContentLoaded', function() {
    const nombreInput = document.getElementById('Nombre');
    const precioInput = document.getElementById('Precio');
    const tiempoEstimadoInput = document.getElementById('TiempoEstimado');
    
    if (nombreInput) {
        nombreInput.addEventListener('input', function() {
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
    
    if (precioInput) {
        precioInput.addEventListener('input', function() {
            const value = parseFloat(this.value);
            if (isNaN(value) || value < 0) {
                this.classList.add('border-red-500');
                // Si es negativo, convertirlo a positivo
                if (value < 0) {
                    this.value = Math.abs(value);
                }
            } else {
                this.classList.remove('border-red-500');
            }
        });
        
        // También validar al perder el foco
        precioInput.addEventListener('blur', function() {
            const value = parseFloat(this.value);
            if (isNaN(value) || value < 0) {
                this.value = '0';
                this.classList.remove('border-red-500');
            }
        });
    }
    
    if (tiempoEstimadoInput) {
        tiempoEstimadoInput.addEventListener('input', function() {
            const value = parseInt(this.value);
            if (isNaN(value) || value <= 0) {
                this.classList.add('border-red-500');
                // Si es negativo o cero, convertirlo a 1
                if (value <= 0) {
                    this.value = 1;
                    this.classList.remove('border-red-500');
                }
            } else {
                this.classList.remove('border-red-500');
            }
        });
        
        // También validar al perder el foco
        tiempoEstimadoInput.addEventListener('blur', function() {
            const value = parseInt(this.value);
            if (isNaN(value) || value <= 0) {
                this.value = '1';
                this.classList.remove('border-red-500');
            }
        });
    }
    
    // Validar el formulario antes de enviarlo
    const servicioForm = document.getElementById('servicio-form');
    if (servicioForm) {
        servicioForm.addEventListener('submit', function(e) {
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
                
                // Abrir el acordeón si está cerrado
                const accordion = document.getElementById('accordion-flush-body-1');
                if (accordion && accordion.classList.contains('hidden')) {
                    accordion.classList.remove('hidden');
                }
                
                // Mostrar mensaje de error
                let errorDiv = document.getElementById('form-error-message');
                if (!errorDiv) {
                    errorDiv = document.createElement('div');
                    errorDiv.id = 'form-error-message';
                    errorDiv.className = 'p-4 mb-4 text-sm text-red-800 rounded-lg bg-red-50 dark:bg-gray-800 dark:text-red-400';
                    errorDiv.innerHTML = '<span class="font-medium">¡Error!</span> ' + errorMessages.join('<br>');
                    servicioForm.appendChild(errorDiv);
                } else {
                    errorDiv.innerHTML = '<span class="font-medium">¡Error!</span> ' + errorMessages.join('<br>');
                }
            }
        });
    }
});

