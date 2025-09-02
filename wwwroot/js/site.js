// =====================================
// MÓDULO PRINCIPAL - FUNCIONALIDAD COMPARTIDA
// =====================================

/**
 * Módulo principal que contiene funcionalidades compartidas entre páginas
 * Incluye: alertas, filtros, tablas, acordeones y utilidades generales
 */
const SiteModule = {

    // =====================================
    // INICIALIZACIÓN GLOBAL
    // =====================================

    /**
     * Inicializa el módulo principal al cargar el DOM
     */
    init() {
        this.setupGlobalAlerts();
        this.setupGlobalFilters();
        this.setupTableFilters();
        this.setupFilterDropdowns();
    },

    // =====================================
    // GESTIÓN DE ALERTAS GLOBALES
    // =====================================

    /**
     * Configura el auto-ocultado de alertas globales
     */
    setupGlobalAlerts() {
        setTimeout(() => {
            const errorAlert = document.getElementById('error-alert');
            const successAlert = document.getElementById('success-alert');
            if (errorAlert) errorAlert.style.display = 'none';
            if (successAlert) successAlert.style.display = 'none';
        }, 5000);
    },

    // =====================================
    // FILTROS GLOBALES
    // =====================================

    /**
     * Inicializa formularios de filtrado comunes
     */
    setupGlobalFilters() {
        const filterForm = document.getElementById('filterForm');
        if (filterForm) {
            filterForm.addEventListener('submit', (e) => {
                const estadosCheckboxes = Array.from(filterForm.querySelectorAll('input[name="estados"]:checked'));

                if (estadosCheckboxes.length === 0) {
                    e.preventDefault();
                    const activoCheckbox = filterForm.querySelector('input[name="estados"][value="Activo"]');
                    if (activoCheckbox) {
                        activoCheckbox.checked = true;
                        filterForm.submit();
                    }
                }
            });
        }
    },

    /**
     * Limpia todos los filtros y marca "Activo" por defecto
     */
    clearAllFilters() {
        const form = document.getElementById('filterForm');
        if (form) {
            const checkboxes = form.querySelectorAll('input[type="checkbox"]');
            checkboxes.forEach(cb => cb.checked = false);

            const activoCheckbox = form.querySelector('input[value="Activo"]');
            if (activoCheckbox) {
                activoCheckbox.checked = true;
            }
        }
    },

    // =====================================
    // FILTRADO DE TABLAS
    // =====================================

    /**
     * Configura el filtrado en tiempo real de tablas
     */
    setupTableFilters() {
        const searchInput = document.getElementById('simple-search');
        if (searchInput) {
            searchInput.addEventListener('input', this.filterTable);
        }
    },

    /**
     * Filtra las filas de una tabla según el texto de búsqueda
     */
    filterTable() {
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
    },

    // =====================================
    // DROPDOWNS DE FILTROS
    // =====================================

    /**
     * Configura dropdowns de filtros con posicionamiento inteligente
     */
    setupFilterDropdowns() {
        const filterButton = document.getElementById('filterDropdownButton');
        const filterDropdown = document.getElementById('filterDropdown');

        if (filterButton && filterDropdown) {
            filterButton.addEventListener('click', (e) => {
                e.stopPropagation();
                this.toggleFilterDropdown(filterButton, filterDropdown);
            });

            document.addEventListener('click', (event) => {
                if (!filterButton.contains(event.target) && !filterDropdown.contains(event.target)) {
                    filterDropdown.classList.add('hidden');
                }
            });

            window.addEventListener('resize', () => {
                if (!filterDropdown.classList.contains('hidden')) {
                    filterButton.click();
                    filterButton.click();
                }
            });
        }
    },

    /**
     * Maneja la lógica de mostrar/ocultar dropdown con posicionamiento
     * @param {HTMLElement} button - Botón del dropdown
     * @param {HTMLElement} dropdown - Elemento dropdown
     */
    toggleFilterDropdown(button, dropdown) {
        const isHidden = dropdown.classList.contains('hidden');
        if (!isHidden) {
            dropdown.classList.add('hidden');
            return;
        }

        const buttonRect = button.getBoundingClientRect();
        const viewportWidth = window.innerWidth;
        const viewportHeight = window.innerHeight;

        dropdown.classList.remove('hidden');
        dropdown.style.visibility = 'hidden';
        dropdown.style.position = 'fixed';

        const dropdownRect = dropdown.getBoundingClientRect();
        const dropdownWidth = dropdownRect.width;
        const dropdownHeight = dropdownRect.height;

        let left = Math.max(10, Math.min(buttonRect.left, viewportWidth - dropdownWidth - 10));

        const spaceBelow = viewportHeight - buttonRect.bottom;
        const spaceAbove = buttonRect.top;

        let top, maxHeight = 'none';

        if (spaceBelow >= dropdownHeight + 10) {
            top = buttonRect.bottom + 5;
        } else if (spaceAbove >= dropdownHeight + 10) {
            top = buttonRect.top - dropdownHeight - 5;
        } else {
            if (spaceBelow > spaceAbove) {
                top = buttonRect.bottom + 5;
                maxHeight = (spaceBelow - 15) + 'px';
            } else {
                top = 10;
                maxHeight = (spaceAbove - 15) + 'px';
            }
        }

        Object.assign(dropdown.style, {
            left: left + 'px',
            top: top + 'px',
            zIndex: '1000',
            maxHeight,
            visibility: 'visible'
        });

        this.updateDropdownIcon(button, spaceBelow < dropdownHeight + 10 && spaceAbove >= dropdownHeight + 10);
    },

    /**
     * Actualiza el ícono del botón dropdown
     * @param {HTMLElement} button - Botón del dropdown
     * @param {boolean} isAbove - Si el dropdown se muestra arriba
     */
    updateDropdownIcon(button, isAbove) {
        const icon = button?.querySelector('svg:last-child');
        if (icon) {
            icon.style.transform = isAbove ? 'rotate(180deg)' : 'rotate(0deg)';
            icon.style.transition = 'transform 0.2s ease';
        }
    },

    // =====================================
    // ACORDEONES
    // =====================================

    /**
     * Configura acordeones con condiciones específicas
     * @param {string} accordionId - ID del acordeón
     * @param {Function} shouldOpen - Función que determina si debe abrirse
     */
    setupAccordion(accordionId, shouldOpen) {
        if (shouldOpen()) {
            const accordion = document.getElementById(accordionId);
            if (accordion && accordion.classList.contains('hidden')) {
                accordion.classList.remove('hidden');

                const accordionButton = document.querySelector(`[data-accordion-target="#${accordionId}"]`);
                if (accordionButton) {
                    accordionButton.setAttribute('aria-expanded', 'true');
                    const icon = accordionButton.querySelector('[data-accordion-icon]');
                    if (icon) icon.classList.add('rotate-180');
                }
            }
        }
    },

    // =====================================
    // UTILIDADES GENERALES
    // =====================================

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
     * Hace crecer automáticamente un textarea según su contenido
     * @param {HTMLElement} element - Elemento textarea
     */
    autoGrow(element) {
        element.style.height = '2.5rem';
        element.style.height = (element.scrollHeight) + 'px';
    }
};

// =====================================
// INICIALIZACIÓN GLOBAL
// =====================================
document.addEventListener('DOMContentLoaded', () => {
    SiteModule.init();
});

// Exportar para uso global
window.SiteModule = SiteModule;