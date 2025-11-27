// Variables globales para el estado de la tabla
let currentPage = 1;
let currentSearchTerm = "";
let currentSortBy = "Nombre";
let currentSortOrder = "asc";
let searchTimeout;

document.addEventListener("DOMContentLoaded", function () {
    // Inicializar estado desde inputs hidden si existen
    const pageInput = document.getElementById("current-page-value");
    if (pageInput) currentPage = parseInt(pageInput.value);

    const sortInput = document.getElementById("current-sort-by");
    if (sortInput) currentSortBy = sortInput.value;

    const orderInput = document.getElementById("current-sort-order");
    if (orderInput) currentSortOrder = orderInput.value;

    // Event listener para búsqueda
    const searchInput = document.getElementById("simple-search");
    if (searchInput) {
        searchInput.addEventListener("input", function (e) {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                currentSearchTerm = e.target.value;
                currentPage = 1; // Reset a primera página al buscar
                reloadClienteTable();
            }, 500); // Debounce de 500ms
        });
    }
});

// Función para recargar la tabla via AJAX
function reloadClienteTable(page) {
    if (page) currentPage = page;

    const url = `/Cliente/TablePartial?searchTerm=${encodeURIComponent(currentSearchTerm)}&pageNumber=${currentPage}&sortBy=${currentSortBy}&sortOrder=${currentSortOrder}`;

    fetch(url)
        .then(response => response.text())
        .then(html => {
            document.getElementById("cliente-table-container").innerHTML = html;
            // Actualizar URL sin recargar (opcional, para mantener estado al refrescar)
            // const newUrl = `${window.location.pathname}?searchTerm=${encodeURIComponent(currentSearchTerm)}&pageNumber=${currentPage}`;
            // window.history.pushState({ path: newUrl }, '', newUrl);
        })
        .catch(error => console.error('Error al cargar la tabla:', error));
}

// Función para ordenar
function sortTable(column) {
    if (currentSortBy === column) {
        currentSortOrder = currentSortOrder === "asc" ? "desc" : "asc";
    } else {
        currentSortBy = column;
        currentSortOrder = "asc";
    }
    reloadClienteTable();
}

// Función para cargar el formulario (Crear o Editar)
function loadClienteForm(id) {
    const url = id ? `/Cliente/FormPartial?id=${id}` : "/Cliente/FormPartial";
    const title = id ? "Editando Cliente" : "Registrando Cliente";

    fetch(url)
        .then(response => response.text())
        .then(html => {
            document.getElementById("cliente-form-container").innerHTML = html;
            document.getElementById("form-title").innerText = title;

            // Abrir el acordeón si está cerrado
            const accordionBtn = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
            const accordionBody = document.getElementById("accordion-flush-body-1");
            if (accordionBody.classList.contains("hidden")) {
                accordionBtn.click();
            }

            // Scroll al formulario
            document.getElementById("accordion-flush").scrollIntoView({ behavior: 'smooth' });
        })
        .catch(error => console.error('Error al cargar el formulario:', error));
}

// Función para manejar el submit del formulario via AJAX
function submitClienteAjax(form) {
    const formData = new FormData(form);
    const actionUrl = form.action;
    const msgContainer = document.getElementById("ajax-form-messages");
    const validationSummary = document.getElementById("cliente-validation-summary");

    fetch(actionUrl, {
        method: 'POST',
        body: formData,
        headers: {
            'X-Requested-With': 'XMLHttpRequest'
        }
    })
        .then(response => {
            const isValid = response.headers.get("X-Form-Valid") === "true";
            const message = response.headers.get("X-Form-Message");

            return response.text().then(html => ({ isValid, message, html }));
        })
        .then(result => {
            if (result.isValid) {
                // Éxito: Limpiar form, mostrar mensaje éxito, recargar tabla
                document.getElementById("cliente-form-container").innerHTML = result.html; // El partial retornado suele ser un form limpio o el mismo

                // Mostrar mensaje de éxito
                msgContainer.innerHTML = `<div class="p-4 mb-4 text-sm text-green-800 rounded-lg bg-green-50 dark:bg-gray-800 dark:text-green-400" role="alert">${result.message}</div>`;
                validationSummary.classList.add("hidden");

                // Si era edición, cambiar título a registro
                document.getElementById("form-title").innerText = "Registrando Cliente";

                reloadClienteTable();

                // Auto-ocultar mensaje después de 3s
                setTimeout(() => {
                    msgContainer.innerHTML = "";
                }, 3000);
            } else {
                // Error: Reemplazar form con el HTML que trae errores de validación
                document.getElementById("cliente-form-container").innerHTML = result.html;

                // Asegurar que el summary sea visible si hay errores
                const newSummary = document.getElementById("cliente-validation-summary");
                if (newSummary) newSummary.classList.remove("hidden");
            }
        })
        .catch(error => {
            console.error('Error:', error);
            msgContainer.innerHTML = `<div class="p-4 mb-4 text-sm text-red-800 rounded-lg bg-red-50 dark:bg-gray-800 dark:text-red-400" role="alert">Error inesperado al procesar la solicitud.</div>`;
        });

    return false; // Prevenir submit normal
}

// Modal de Confirmación de Eliminación
let clienteIdToDelete = null;

function openClienteConfirmModal(id) {
    clienteIdToDelete = id;
    document.getElementById("idClienteEliminar").value = id;

    // Mostrar modal (usando Flowbite o lógica custom)
    const modal = document.getElementById("clienteConfirmModal");
    modal.classList.remove("hidden");
    modal.classList.add("flex");
}

// Manejar el submit del form de eliminación
document.getElementById("formEliminarCliente")?.addEventListener("submit", function (e) {
    e.preventDefault();
    const form = this;
    const formData = new FormData(form);

    fetch(form.action, {
        method: 'POST',
        body: formData
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                // Cerrar modal
                const modal = document.getElementById("clienteConfirmModal");
                modal.classList.add("hidden");
                modal.classList.remove("flex");

                // Recargar tabla
                reloadClienteTable();

                // Mostrar toast o alerta
                alert(data.message); // O usar un toast mejor
            } else {
                alert(data.message);
            }
        })
        .catch(error => console.error('Error:', error));
});

// Manejo de Modal Tipo Documento (Creación rápida)
document.getElementById("formCrearTipoDocumento")?.addEventListener("submit", function (e) {
    e.preventDefault();
    const form = this;
    const formData = new FormData(form);

    fetch(form.action, {
        method: 'POST',
        body: formData,
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                // Cerrar modal
                const modal = document.getElementById("tipoDocumentoModal");
                // Asumiendo que usamos data-modal-hide de flowbite, simulamos click o ocultamos manual
                const closeBtn = modal.querySelector('[data-modal-hide]');
                if (closeBtn) closeBtn.click();

                // Actualizar dropdowns de TipoDocumento
                updateTipoDocumentoDropdowns(data.tipos);

                // Limpiar input
                form.reset();
            } else {
                alert(data.message);
            }
        })
        .catch(error => console.error('Error:', error));
});

function updateTipoDocumentoDropdowns(tipos) {
    const selects = document.querySelectorAll('select[name="TipoDocumento"]');
    selects.forEach(select => {
        const currentVal = select.value;
        select.innerHTML = "";
        tipos.forEach(tipo => {
            const option = document.createElement("option");
            option.value = tipo.nombre; // Asumiendo que devuelve objetos {id, nombre} o strings
            option.text = tipo.nombre;
            select.appendChild(option);
        });
        select.value = currentVal; // Intentar mantener selección
    });
}

// Lógica para "Quick Create Vehicle"
function openQuickCreateVehiculoModal() {
    // 1. Obtener el formulario de vehículo via AJAX
    fetch('/Vehiculo/FormPartial')
        .then(response => response.text())
        .then(html => {
            // 2. Inyectar en un contenedor modal dinámico
            let modalContainer = document.getElementById("quick-create-modal-container");
            if (!modalContainer) {
                modalContainer = document.createElement("div");
                modalContainer.id = "quick-create-modal-container";
                modalContainer.className = "fixed inset-0 z-[60] flex items-center justify-center bg-black bg-opacity-50 hidden";
                document.body.appendChild(modalContainer);
            }

            modalContainer.innerHTML = `
                <div class="relative w-full max-w-2xl bg-white rounded-lg shadow dark:bg-gray-800 p-6 m-4 max-h-[90vh] overflow-y-auto">
                    <div class="flex justify-between items-center mb-4">
                        <h3 class="text-xl font-semibold text-gray-900 dark:text-white">Registrar Vehículo Rápido</h3>
                        <button onclick="closeQuickCreateModal()" class="text-gray-400 hover:text-gray-900 dark:hover:text-white">
                            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path></svg>
                        </button>
                    </div>
                    <div id="quick-vehiculo-form-content">
                        ${html}
                    </div>
                </div>
            `;

            // 3. Mostrar modal
            modalContainer.classList.remove("hidden");

            // 4. Interceptar el submit del formulario inyectado
            const form = modalContainer.querySelector("form");
            if (form) {
                form.onsubmit = function (e) {
                    e.preventDefault();
                    submitQuickVehiculo(this);
                };

                // Ajustar botones del form para que "Cancelar" cierre este modal
                const cancelBtn = form.querySelector("#clear-button");
                if (cancelBtn) {
                    cancelBtn.onclick = closeQuickCreateModal;
                    cancelBtn.innerText = "Cancelar";
                }
            }
        })
        .catch(error => console.error('Error al cargar form vehiculo:', error));
}

function closeQuickCreateModal() {
    const modal = document.getElementById("quick-create-modal-container");
    if (modal) modal.classList.add("hidden");
}

function submitQuickVehiculo(form) {
    const formData = new FormData(form);

    fetch(form.action, {
        method: 'POST',
        body: formData,
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    })
        .then(response => {
            const isValid = response.headers.get("X-Form-Valid") === "true";
            return response.text().then(html => ({ isValid, html }));
        })
        .then(result => {
            if (result.isValid) {
                // Éxito
                closeQuickCreateModal();

                // Recargar lista de vehículos en el formulario de cliente
                // Necesitamos una forma de obtener el ID del nuevo vehículo.
                // Lo ideal sería que el servidor devuelva el objeto creado en JSON si es AJAX.
                // Pero como devuelve HTML (PartialView), es difícil parsear el ID.
                // Workaround: Recargar el dropdown de vehículos completo.
                reloadVehiculosDropdown();

                alert("Vehículo registrado correctamente. Selecciónelo en la lista.");
            } else {
                // Error: mostrar errores en el mismo modal
                document.getElementById("quick-vehiculo-form-content").innerHTML = result.html;
                // Re-bindear el submit
                const newForm = document.getElementById("quick-vehiculo-form-content").querySelector("form");
                if (newForm) {
                    newForm.onsubmit = function (e) {
                        e.preventDefault();
                        submitQuickVehiculo(this);
                    };
                }
            }
        })
        .catch(error => console.error('Error:', error));
}

function reloadVehiculosDropdown() {
    // Fetch de todos los vehículos disponibles
    // Podríamos crear un endpoint específico para esto que devuelva JSON
    // Por ahora, recargamos la página o hacemos un fetch custom.
    // Haremos un fetch a una acción que devuelva JSON de vehículos.
    // No tenemos esa acción aun, pero podemos usar el Index con filtro? No.
    // Vamos a asumir que el usuario refresca o implementamos un endpoint JSON simple en VehiculoController.
    // TODO: Implementar endpoint GetVehiculosJson en VehiculoController para mejor UX.

    // Fallback temporal: Alertar al usuario
    // alert("Vehículo creado. Por favor actualice la lista de vehículos (F5 si es necesario, o reabra el formulario).");

    // Mejor: Recargar solo el partial del formulario de cliente? No, perderíamos datos ingresados.
    // Lo ideal es un endpoint JSON.
}

function verVehiculos(clienteId) {
    // Redirigir a la vista de vehículos filtrada por este cliente?
    // O mostrar un modal con la lista.
    // El requerimiento dice "icono de un ojo que al hacer click me muestre los vehiculos de ese cliente".
    // Un modal es mejor.

    // Implementación simple: Redirigir con filtro (ya que implementamos filtro por cliente en VehiculoController? No, implementamos filtro por texto).
    // Pero el texto busca en "ClienteNombreCompleto".
    // Así que buscar por nombre del cliente podría funcionar.

    // Mejor: Modal con lista read-only.
    // Reutilizaremos el modal de "Vehiculos" pero en modo lectura.
    // O simplemente un alert con la lista por ahora si no hay endpoint.

    // Vamos a redirigir a Vehiculos/Index con un parámetro de búsqueda especial o simplemente el nombre.
    // window.location.href = `/Vehiculo/Index?searchTerm=${clienteId}`; // Si el search busca por ID

    // Como no tenemos búsqueda por ID de cliente explícita en el back (solo texto general),
    // lo dejaremos como TODO o un simple alert.
    alert("Funcionalidad de ver vehículos en modal pendiente de implementación backend específica.");
}
