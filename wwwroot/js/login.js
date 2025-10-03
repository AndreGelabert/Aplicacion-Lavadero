/**
 * Módulo para la página de login
 * Maneja validaciones y comportamiento específico del formulario de autenticación
 */
const LoginModule = {

    /**
     * Inicializa el módulo de login
     */
    init() {
        this.setupPasswordToggle();
        this.setupFormValidation(); // Ahora incluye "Recordarme"
        this.loadRememberedEmail(); // Cargar email guardado al inicio
    },

    /**
     * Carga el email guardado si existe
     */
    loadRememberedEmail() {
        const rememberCheckbox = document.getElementById('rememberMe');
        const emailField = document.getElementById('email');

        if (rememberCheckbox && emailField) {
            const savedEmail = localStorage.getItem('rememberedEmail');
            if (savedEmail) {
                emailField.value = savedEmail;
                rememberCheckbox.checked = true;
            }
        }
    },

    /**
     * Configura validación del formulario de login
     * AHORA INCLUYE LA LÓGICA DE "RECORDARME"
     */
    setupFormValidation() {
        const loginForm = document.getElementById('loginForm');
        if (!loginForm) return;

        loginForm.addEventListener('submit', (e) => {
            const email = document.getElementById('email');
            const password = document.getElementById('password');
            const rememberCheckbox = document.getElementById('rememberMe');

            let hasErrors = false;

            // Validar email
            if (!email.value.trim()) {
                this.showFieldError(email, 'El email es obligatorio');
                hasErrors = true;
            } else if (!this.isValidEmail(email.value)) {
                this.showFieldError(email, 'Ingrese un email válido');
                hasErrors = true;
            } else {
                this.clearFieldError(email);
            }

            // Validar contraseña
            if (!password.value.trim()) {
                this.showFieldError(password, 'La contraseña es obligatoria');
                hasErrors = true;
            } else {
                this.clearFieldError(password);
            }

            // Si hay errores, prevenir envío
            if (hasErrors) {
                e.preventDefault();
                return;
            }

            // Guardar/eliminar email según checkbox (solo si no hay errores)
            if (rememberCheckbox && rememberCheckbox.checked) {
                localStorage.setItem('rememberedEmail', email.value);
            } else {
                localStorage.removeItem('rememberedEmail');
            }

            // Dejar que el formulario se envíe normalmente
        });
    },

    /**
     * Configura el toggle de mostrar/ocultar contraseña
     */
    setupPasswordToggle() {
        const toggleButton = document.getElementById('togglePassword');
        const passwordField = document.getElementById('password');

        if (toggleButton && passwordField) {
            toggleButton.addEventListener('click', () => {
                const type = passwordField.getAttribute('type') === 'password' ? 'text' : 'password';
                passwordField.setAttribute('type', type);

                const icon = toggleButton.querySelector('i');
                if (icon) {
                    icon.classList.toggle('fa-eye');
                    icon.classList.toggle('fa-eye-slash');
                }
            });
        }
    },

    /**
     * Valida formato de email
     * @param {string} email - Email a validar
     * @returns {boolean} Si el email es válido
     */
    isValidEmail(email) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return emailRegex.test(email);
    },

    /**
     * Muestra error en un campo específico
     * @param {HTMLElement} field - Campo con error
     * @param {string} message - Mensaje de error
     */
    showFieldError(field, message) {
        field.classList.add('border-red-500');

        let errorElement = field.parentNode.querySelector('.field-error');
        if (!errorElement) {
            errorElement = document.createElement('span');
            errorElement.className = 'field-error text-red-500 text-sm mt-1';
            field.parentNode.appendChild(errorElement);
        }
        errorElement.textContent = message;
    },

    /**
     * Limpia error de un campo específico
     * @param {HTMLElement} field - Campo a limpiar
     */
    clearFieldError(field) {
        field.classList.remove('border-red-500');

        const errorElement = field.parentNode.querySelector('.field-error');
        if (errorElement) {
            errorElement.remove();
        }
    }
};
/**
 * Cierra el modal de verificación de email
 */
function closeVerificationModal() {
    const modal = document.getElementById('verificationModal');
    if (modal) {
        modal.classList.add('hidden');
    }
}

/**
 * Muestra el modal de verificación de email
 */
function showVerificationModal(email) {
    const modal = document.getElementById('verificationModal');
    const emailSpan = modal.querySelector('.registration-email');

    if (emailSpan) {
        emailSpan.textContent = email;
    }

    if (modal) {
        modal.classList.remove('hidden');
    }
}

// Exportar funciones globalmente
window.closeVerificationModal = closeVerificationModal;
window.showVerificationModal = showVerificationModal;

// =====================================
// INICIALIZACIÓN DEL MÓDULO
// =====================================

// Al cargar la página, si hay un modal de verificación abierto, cerrar el de registro
document.addEventListener('DOMContentLoaded', () => {
    const verificationModal = document.getElementById('verificationModal');
    const registerModal = document.getElementById('registerModal');

    if (verificationModal && !verificationModal.classList.contains('hidden')) {
        // Cerrar el modal de registro si el de verificación está abierto
        if (registerModal) {
            registerModal.classList.add('hidden');
        }
    }

    // Inicializar el módulo de login
    if (document.getElementById('loginForm')) {
        LoginModule.init();
    }
});