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
        this.setupFormValidation();
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

            // Si hay errores, prevenir envío
            if (hasErrors) {
                e.preventDefault();
                return;
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
 * @param {string} email - Email del usuario
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
// FUNCIONES PARA MODALES
// =====================================

/**
 * Cierra el modal de recuperación de contraseña
 */
function closeForgotPasswordModal() {
    const modal = document.getElementById('forgotPasswordModal');
    const feedback = document.getElementById('forgotPasswordFeedback');
    const emailInput = document.getElementById('resetEmail');
    
    if (modal) modal.classList.add('hidden');
    if (feedback) {
        feedback.classList.add('hidden');
        feedback.textContent = '';
    }
    if (emailInput) emailInput.value = '';
}

/**
 * Envía el correo de recuperación de contraseña
 */
async function sendPasswordResetEmail() {
    const emailInput = document.getElementById('resetEmail');
    const feedback = document.getElementById('forgotPasswordFeedback');
    const email = emailInput?.value?.trim();
    
    if (!email) {
        if (feedback) {
            feedback.textContent = 'Por favor, ingrese un correo electrónico.';
            feedback.className = 'mt-4 text-sm text-red-600 dark:text-red-400 text-center';
            feedback.classList.remove('hidden');
        }
        return;
    }
    
    // Validar formato de email
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email)) {
        if (feedback) {
            feedback.textContent = 'Por favor, ingrese un correo electrónico válido.';
            feedback.className = 'mt-4 text-sm text-red-600 dark:text-red-400 text-center';
            feedback.classList.remove('hidden');
        }
        return;
    }
    
    if (feedback) {
        feedback.className = 'mt-4 text-sm text-gray-600 dark:text-gray-300 text-center';
        feedback.textContent = 'Enviando...';
        feedback.classList.remove('hidden');
    }
    
    try {
        const resp = await fetch('/Login/ForgotPassword', {
            method: 'POST',
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email: email })
        });
        
        const data = await resp.json();
        
        if (!resp.ok) {
            throw new Error(data.error || 'Error al enviar el correo');
        }
        
        if (feedback) {
            feedback.textContent = 'Correo de recuperación enviado. Revise su bandeja de entrada.';
            feedback.className = 'mt-4 text-sm text-green-600 dark:text-green-400 text-center';
        }
        
        // Cerrar modal después de 3 segundos
        setTimeout(() => {
            closeForgotPasswordModal();
        }, 3000);
    } catch (error) {
        if (feedback) {
            feedback.textContent = error.message || 'No se pudo enviar el correo de recuperación.';
            feedback.className = 'mt-4 text-sm text-red-600 dark:text-red-400 text-center';
        }
    }
}

/**
 * Reenvía el correo de verificación
 */
async function resendVerificationEmail() {
    const feedback = document.getElementById('verificationFeedback');
    if (feedback) {
        feedback.className = 'mt-4 text-sm text-gray-600 dark:text-gray-300';
        feedback.textContent = 'Reenviando...';
        feedback.classList.remove('hidden');
    }
    try {
        const resp = await fetch('/Login/ResendVerification', { 
            method: 'POST', 
            headers: { "Content-Type": "application/json" } 
        });
        if (!resp.ok) throw new Error();
        if (feedback) {
            feedback.textContent = 'Correo de verificación reenviado.';
            feedback.className = 'mt-4 text-sm text-green-600 dark:text-green-400';
        }
    } catch {
        if (feedback) {
            feedback.textContent = 'No se pudo reenviar el correo.';
            feedback.className = 'mt-4 text-sm text-red-600 dark:text-red-400';
        }
    }
}

// =====================================
// FUNCIONES PARA EL MODAL DE REGISTRO
// =====================================

/**
 * Cierra el modal de registro y limpia los campos
 */
function closeRegisterModal() {
    const modal = document.getElementById('registerModal');
    if (modal) {
        modal.classList.add('hidden');
    }
    
    // Limpiar todos los campos del formulario
    const form = document.getElementById('registerForm');
    if (form) {
        form.reset();
    }
    
    // Limpiar mensajes de error del servidor
    const errorMessages = document.querySelectorAll('#registerModal .text-red-500');
    errorMessages.forEach(msg => msg.remove());
    
    // Limpiar errores dinámicos de validación
    const dynamicErrors = document.querySelectorAll('#registerModal .nombre-completo-error, #registerModal .password-strength-error');
    dynamicErrors.forEach(err => err.remove());
    
    // Remover clases de error de los campos
    const errorFields = document.querySelectorAll('#registerModal .border-red-500');
    errorFields.forEach(field => field.classList.remove('border-red-500'));
    
    // Mostrar tooltips de ayuda si estaban ocultos
    const nombreCompletoHelp = document.getElementById('nombreCompletoHelp');
    if (nombreCompletoHelp) {
        nombreCompletoHelp.classList.remove('hidden');
    }
    
    const passwordHelp = document.getElementById('passwordHelp');
    if (passwordHelp) {
        passwordHelp.classList.remove('hidden');
    }
}

/**
 * Valida el formato del nombre completo
 * Permite uno o más nombres seguidos de uno o más apellidos
 * Cada palabra debe tener mínimo 2 letras
 */
function validateNombreCompleto(input) {
    const nombreCompleto = input.value;
    const nombreCompletoHelp = document.getElementById('nombreCompletoHelp');
    
    // RegEx mejorado: permite múltiples nombres y apellidos
    // Mínimo 2 letras por palabra, al menos 2 palabras en total
    const nombreCompletoRegex = /^[a-zA-ZáéíóúÁÉÍÓÚñÑ]{2,}(\s+[a-zA-ZáéíóúÁÉÍÓÚñÑ]{2,})+$/;
    
    // Limpiar errores previos
    let existingError = input.parentNode.querySelector('.nombre-completo-error');
    if (existingError) {
        existingError.remove();
    }
    
    if (nombreCompleto.length > 0 && !nombreCompletoRegex.test(nombreCompleto)) {
        // Ocultar tooltip de ayuda
        if (nombreCompletoHelp) {
            nombreCompletoHelp.classList.add('hidden');
        }
        
        // Crear mensaje de error específico
        const errorMsg = document.createElement('span');
        errorMsg.className = 'nombre-completo-error text-red-500 text-xs mt-1 block';
        
        // Analizar qué falta
        const palabras = nombreCompleto.trim().split(/\s+/);
        
        if (palabras.length < 2) {
            errorMsg.textContent = 'Debe ingresar al menos nombre y apellido';
        } else {
            // Verificar longitud de cada palabra
            const palabrasCortas = palabras.filter(p => p.length < 2);
            if (palabrasCortas.length > 0) {
                errorMsg.textContent = 'Cada palabra debe tener al menos 2 letras';
            } else {
                // Verificar que solo contenga letras válidas
                errorMsg.textContent = 'Solo se permiten letras (sin números ni caracteres especiales)';
            }
        }
        
        input.parentNode.appendChild(errorMsg);
        input.classList.add('border-red-500');
    } else {
        // Mostrar tooltip de ayuda si el nombre es válido o está vacío
        if (nombreCompletoHelp) {
            nombreCompletoHelp.classList.remove('hidden');
        }
        input.classList.remove('border-red-500');
    }
}

/**
 * Valida la fortaleza de la contraseña según los requisitos de Firebase
 */
function validatePasswordStrength(input) {
    const password = input.value;
    const passwordHelp = document.getElementById('passwordHelp');
    
    // Requisitos: mínimo 6 caracteres, al menos una mayúscula, una minúscula y un número
    const minLength = password.length >= 6;
    const hasUpperCase = /[A-Z]/.test(password);
    const hasLowerCase = /[a-z]/.test(password);
    const hasNumber = /[0-9]/.test(password);
    
    const isValid = minLength && hasUpperCase && hasLowerCase && hasNumber;
    
    // Limpiar errores previos
    let existingError = input.parentNode.parentNode.querySelector('.password-strength-error');
    if (existingError) {
        existingError.remove();
    }
    
    if (password.length > 0 && !isValid) {
        // Ocultar tooltip de ayuda
        if (passwordHelp) {
            passwordHelp.classList.add('hidden');
        }
        
        // Crear mensaje de error específico
        const errorMsg = document.createElement('span');
        errorMsg.className = 'password-strength-error text-red-500 text-xs mt-1 block';
        
        const missingReqs = [];
        if (!minLength) missingReqs.push('al menos 6 caracteres');
        if (!hasUpperCase) missingReqs.push('una mayúscula');
        if (!hasLowerCase) missingReqs.push('una minúscula');
        if (!hasNumber) missingReqs.push('un número');
        
        errorMsg.textContent = `La contraseña debe contener ${missingReqs.join(', ')}`;
        
        input.parentNode.parentNode.appendChild(errorMsg);
        input.classList.add('border-red-500');
    } else {
        // Mostrar tooltip de ayuda si la contraseña es válida o está vacía
        if (passwordHelp) {
            passwordHelp.classList.remove('hidden');
        }
        input.classList.remove('border-red-500');
    }
    
    // Validar coincidencia si el campo de confirmación tiene contenido
    validatePasswordMatch();
}

/**
 * Valida que las contraseñas coincidan
 */
function validatePasswordMatch() {
    const password = document.getElementById('modalPassword');
    const confirmPassword = document.getElementById('confirmPassword');
    const confirmPasswordError = document.getElementById('confirmPasswordError');
    
    if (!password || !confirmPassword || !confirmPasswordError) return;
    
    if (confirmPassword.value.length > 0) {
        if (password.value !== confirmPassword.value) {
            confirmPasswordError.classList.remove('hidden');
            confirmPassword.classList.add('border-red-500');
        } else {
            confirmPasswordError.classList.add('hidden');
            confirmPassword.classList.remove('border-red-500');
        }
    } else {
        confirmPasswordError.classList.add('hidden');
        confirmPassword.classList.remove('border-red-500');
    }
}

/**
 * Toggle de visibilidad de contraseña
 */
function togglePasswordVisibility(passwordFieldId, iconId) {
    var passwordField = document.getElementById(passwordFieldId);
    var icon = document.getElementById(iconId);
    if (passwordField.type === "password") {
        passwordField.type = "text";
        icon.innerHTML = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.875 18.825A10.05 10.05 0 0112 19c-5.523 0-10-4.477-10-10S6.477 0 12 0c2.21 0 4.26.72 5.875 1.925M15 12a3 3 0 11-6 0 3 3 0 016 0z" />';
    } else {
        passwordField.type = "password";
        icon.innerHTML = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" /><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />';
    }
}

// Exportar funciones globalmente
window.closeForgotPasswordModal = closeForgotPasswordModal;
window.sendPasswordResetEmail = sendPasswordResetEmail;
window.resendVerificationEmail = resendVerificationEmail;
window.closeRegisterModal = closeRegisterModal;
window.validateNombreCompleto = validateNombreCompleto;
window.validatePasswordStrength = validatePasswordStrength;
window.validatePasswordMatch = validatePasswordMatch;
window.togglePasswordVisibility = togglePasswordVisibility;

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
    
    // Validar formulario de registro antes de enviar
    const registerForm = document.getElementById('registerForm');
    if (registerForm) {
        registerForm.addEventListener('submit', function(e) {
            const nombreCompleto = document.getElementById('nombreCompleto')?.value;
            const nombreCompletoInput = document.getElementById('nombreCompleto');
            const password = document.getElementById('modalPassword')?.value;
            const passwordInput = document.getElementById('modalPassword');
            const confirmPassword = document.getElementById('confirmPassword')?.value;
            const confirmPasswordInput = document.getElementById('confirmPassword');
            
            let hasErrors = false;
            
            // Validar nombre completo (mínimo 2 letras por palabra, al menos 2 palabras)
            const nombreCompletoRegex = /^[a-zA-ZáéíóúÁÉÍÓÚñÑ]{2,}(\s+[a-zA-ZáéíóúÁÉÍÓÚñÑ]{2,})+$/;
            if (nombreCompleto && !nombreCompletoRegex.test(nombreCompleto)) {
                // Trigger la validación visual del campo
                if (nombreCompletoInput) {
                    validateNombreCompleto(nombreCompletoInput);
                }
                hasErrors = true;
            }
            
            // Validar requisitos de contraseña
            if (password) {
                const minLength = password.length >= 6;
                const hasUpperCase = /[A-Z]/.test(password);
                const hasLowerCase = /[a-z]/.test(password);
                const hasNumber = /[0-9]/.test(password);
                
                const isValid = minLength && hasUpperCase && hasLowerCase && hasNumber;
                
                if (!isValid) {
                    // Trigger la validación visual del campo
                    if (passwordInput) {
                        validatePasswordStrength(passwordInput);
                    }
                    hasErrors = true;
                }
            }
            
            // Validar que las contraseñas coincidan
            if (password && confirmPassword && password !== confirmPassword) {
                // Trigger la validación visual
                validatePasswordMatch();
                hasErrors = true;
            }
            
            // Si hay errores, prevenir el envío del formulario
            if (hasErrors) {
                e.preventDefault();
                return false;
            }
        });
    }
});