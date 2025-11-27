// Variables globales
let currentPage = 1;
let currentSearchTerm = "";
let currentTipoVehiculo = "";
let currentSortBy = "Patente";
let currentSortOrder = "asc";
let searchTimeout;

document.addEventListener("DOMContentLoaded", function () {
    // Inicializar estado
    const pageInput = document.getElementById("current-page-value");
    if (pageInput) currentPage = parseInt(pageInput.value);

    const sortInput = document.getElementById("current-sort-by");
    if (sortInput) currentSortBy = sortInput.value;

    const orderInput = document.getElementById("current-sort-order");
    if (orderInput) currentSortOrder = orderInput.value;

    // Search input
    const searchInput = document.getElementById("simple-search");
    if (searchInput) {
        searchInput.addEventListener("input", function (e) {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                currentSearchTerm = e.target.value;
                currentPage = 1;
                reloadVehiculoTable();
            }, 500);
        });
    }
});

function reloadVehiculoTable(page) {
    if (page) currentPage = page;

    // Obtener filtro de tipo de vehículo si existe
    // (Si está en el dropdown de filtros)
    const tipoRadio = document.querySelector('input[name="tipoVehiculo"]:checked');
    if (tipoRadio) currentTipoVehiculo = tipoRadio.value;

    const url = `/Vehiculo/TablePartial?searchTerm=${encodeURIComponent(currentSearchTerm)}&tipoVehiculo=${encodeURIComponent(currentTipoVehiculo)}&pageNumber=${currentPage}&sortBy=${currentSortBy}&sortOrder=${currentSortOrder}`;

    fetch(url)
        .then(response => response.text())
        .then(html => {
            document.getElementById("vehiculo-table-container").innerHTML = html;
        })
        .catch(error => console.error('Error al cargar tabla:', error));
}

function sortTable(column) {
    if (currentSortBy === column) {
        currentSortOrder = currentSortOrder === "asc" ? "desc" : "asc";
    } else {
        currentSortBy = column;
        currentSortOrder = "asc";
    }
    reloadVehiculoTable();
}

function loadVehiculoForm(id) {
    const url = id ? `/Vehiculo/FormPartial?id=${id}` : "/Vehiculo/FormPartial";
    const title = id ? "Editando Vehículo" : "Registrando Vehículo";

    fetch(url)
        .then(response => response.text())
        .then(html => {
            document.getElementById("vehiculo-form-container").innerHTML = html;
            document.getElementById("form-title").innerText = title;

            const accordionBtn = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
            const accordionBody = document.getElementById("accordion-flush-body-1");
            if (accordionBody.classList.contains("hidden")) {
                accordionBtn.click();
            }
            document.getElementById("accordion-flush").scrollIntoView({ behavior: 'smooth' });
        })
        .catch(error => console.error('Error al cargar formulario:', error));
}

function submitVehiculoAjax(form) {
    const formData = new FormData(form);
    const actionUrl = form.action;
    const msgContainer = document.getElementById("ajax-form-messages");
    const validationSummary = document.getElementById("vehiculo-validation-summary");

    fetch(actionUrl, {
        method: 'POST',
        body: formData,
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    })
        .then(response => {
            const isValid = response.headers.get("X-Form-Valid") === "true";
            const message = response.headers.get("X-Form-Message");
            return response.text().then(html => ({ isValid, message, html }));
        })
        .then(result => {
            if (result.isValid) {
                document.getElementById("vehiculo-form-container").innerHTML = result.html;
                msgContainer.innerHTML = `<div class="p-4 mb-4 text-sm text-green-800 rounded-lg bg-green-50 dark:bg-gray-800 dark:text-green-400" role="alert">${result.message}</div>`;
                if (validationSummary) validationSummary.classList.add("hidden");

                document.getElementById("form-title").innerText = "Registrando Vehículo";
                reloadVehiculoTable();

                setTimeout(() => { msgContainer.innerHTML = ""; }, 3000);
            } else {
                document.getElementById("vehiculo-form-container").innerHTML = result.html;
                const newSummary = document.getElementById("vehiculo-validation-summary");
                if (newSummary) newSummary.classList.remove("hidden");
            }
        })
        .catch(error => {
            console.error('Error:', error);
            msgContainer.innerHTML = `<div class="p-4 mb-4 text-sm text-red-800 rounded-lg bg-red-50 dark:bg-gray-800 dark:text-red-400" role="alert">Error inesperado.</div>`;
        });

    return false;
}

// Filtros
function clearVehiculoFilters() {
    currentTipoVehiculo = "";
    const radios = document.querySelectorAll('input[name="tipoVehiculo"]');
    radios.forEach(r => r.checked = false);
    reloadVehiculoTable(1);
}

document.getElementById("filterForm")?.addEventListener("submit", function (e) {
    e.preventDefault();
    reloadVehiculoTable(1);
    // Cerrar dropdown (opcional)
    const btn = document.getElementById("filterDropdownButton");
    if (btn) btn.click();
});

// Confirmación Eliminación
let vehiculoIdToDelete = null;
function openVehiculoConfirmModal(id) {
    vehiculoIdToDelete = id;
    document.getElementById("idVehiculoEliminar").value = id;
    const modal = document.getElementById("vehiculoConfirmModal");
    modal.classList.remove("hidden");
    modal.classList.add("flex");
}

document.getElementById("formEliminarVehiculo")?.addEventListener("submit", function (e) {
    e.preventDefault();
    const form = this;
    const formData = new FormData(form);

    fetch(form.action, { method: 'POST', body: formData })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                const modal = document.getElementById("vehiculoConfirmModal");
                modal.classList.add("hidden");
                modal.classList.remove("flex");
                reloadVehiculoTable();
                alert(data.message);
            } else {
                alert(data.message);
            }
        })
        .catch(error => console.error('Error:', error));
});

// Info Cliente Modal
function verInfoCliente(clienteId) {
    fetch(`/Vehiculo/GetClienteInfo?clienteId=${clienteId}`)
        .then(response => {
            if (!response.ok) throw new Error("No se pudo obtener info");
            return response.json();
        })
        .then(data => {
            document.getElementById("infoClienteNombre").innerText = data.nombreCompleto;
            document.getElementById("infoClienteDocumento").innerText = data.documento;
            document.getElementById("infoClienteTelefono").innerText = data.telefono;
            document.getElementById("infoClienteEmail").innerText = data.email;

            const modal = document.getElementById("clienteInfoModal");
            modal.classList.remove("hidden");
            modal.classList.add("flex");
        })
        .catch(error => console.error(error));
}

// Cerrar modales al hacer click fuera o en X (Lógica general)
document.querySelectorAll('[data-modal-hide]').forEach(btn => {
    btn.addEventListener('click', function () {
        const targetId = this.getAttribute('data-modal-hide');
        const modal = document.getElementById(targetId);
        if (modal) {
            modal.classList.add("hidden");
            modal.classList.remove("flex");
        }
    });
});
