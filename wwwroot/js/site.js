//JavaScript para la página de Personal
var isEditing = false;
function toggleEdit(id) {
    var rolText = document.getElementById('rol-text-' + id);
    var rolForm = document.getElementById('rol-form-' + id);
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
    form.submit();
}

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
document.addEventListener('DOMContentLoaded', function () {
    const filterForm = document.getElementById('filterForm');
    const filterDropdown = document.getElementById('filterDropdown');

    filterForm.addEventListener('submit', function () {
        filterDropdown.classList.add('hidden'); // Cerrar el dropdown
    });
});
// Manejar envío del formulario de reactivación
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