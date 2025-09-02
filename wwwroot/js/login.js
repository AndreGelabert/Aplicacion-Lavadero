/**
 * Módulo para la página de login
 * Maneja validaciones y comportamiento específico del formulario de autenticación
 */
const LoginModule = {

    /**
     * Inicializa el módulo de login
     */
    init() {
        this.setupFormValidation();
        this.setupPasswordToggle();
        this.setupRememberMe();
    },

    /**
     * Configura validación del formulario de login
     */
    setupFormValidation() {
        const loginForm = document.getElementById('loginForm');
        if (!loginForm) return;

        loginForm.addEventListener('submit', (e) => {
            const email = document.getElementById('email');
            const password = document.getElementById('password');

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

            if (hasErrors) {
                e.preventDefault();
            }
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
     * Configura funcionalidad de "Recordarme"
     */
    setupRememberMe() {
        const rememberCheckbox = document.getElementById('rememberMe');
        const emailField = document.getElementById('email');

        if (rememberCheckbox && emailField) {
            // Cargar email guardado si existe
            const savedEmail = localStorage.getItem('rememberedEmail');
            if (savedEmail) {
                emailField.value = savedEmail;
                rememberCheckbox.checked = true;
            }

            // Guardar/eliminar email según checkbox
            const loginForm = document.getElementById('loginForm');
            if (loginForm) {
                loginForm.addEventListener('submit', () => {
                    if (rememberCheckbox.checked) {
                        localStorage.setItem('rememberedEmail', emailField.value);
                    } else {
                        localStorage.removeItem('rememberedEmail');
                    }
                });
            }
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

// =====================================
// INICIALIZACIÓN DEL MÓDULO
// =====================================
document.addEventListener('DOMContentLoaded', () => {
    // Solo inicializar si estamos en la página de login
    if (document.getElementById('loginForm')) {
        LoginModule.init();
    }
});