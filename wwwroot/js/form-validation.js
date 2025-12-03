/**
 * ================================================
 * FORM-VALIDATION.JS - VALIDACIÓN EN TIEMPO REAL DE FORMULARIOS
 * ================================================
 * Responsabilidades:
 *  - Validación en tiempo real de campos obligatorios
 *  - Mostrar/ocultar mensajes de error debajo de inputs
 *  - Estilos de error para campos inválidos
 */

(function () {
    'use strict';

    /**
     * Módulo de validación de formularios
     */
    const FormValidation = {
        /**
         * Inicializa la validación para un formulario específico
         * @param {string|HTMLFormElement} formSelector - Selector CSS o elemento del formulario
         * @param {Object} options - Opciones de configuración
         */
        init(formSelector, options = {}) {
            const form = typeof formSelector === 'string' 
                ? document.querySelector(formSelector) 
                : formSelector;
            
            if (!form) return;

            const config = {
                validateOnBlur: true,
                validateOnInput: true,
                ...options
            };

            this.setupValidationListeners(form, config);
        },

        /**
         * Configura los listeners de validación para todos los campos del formulario
         * @param {HTMLFormElement} form - Elemento del formulario
         * @param {Object} config - Configuración
         */
        setupValidationListeners(form, config) {
            const fields = form.querySelectorAll('input[required], select[required], textarea[required]');
            
            fields.forEach(field => {
                // Crear contenedor de error si no existe
                this.ensureErrorContainer(field);

                if (config.validateOnBlur) {
                    field.addEventListener('blur', () => this.validateField(field));
                }

                if (config.validateOnInput) {
                    field.addEventListener('input', () => this.validateField(field));
                }

                // Para selects, también validar en change
                if (field.tagName === 'SELECT') {
                    field.addEventListener('change', () => this.validateField(field));
                }
            });
        },

        /**
         * Asegura que existe un contenedor de error para el campo
         * @param {HTMLElement} field - Campo del formulario
         */
        ensureErrorContainer(field) {
            const fieldId = field.id || field.name;
            let errorContainer = document.getElementById(`${fieldId}-validation-error`);
            
            if (!errorContainer) {
                // Buscar si ya existe un span de error asp-validation-for
                const existingValidation = field.parentElement?.querySelector('[class*="text-red"]');
                if (existingValidation) {
                    errorContainer = existingValidation;
                    errorContainer.id = `${fieldId}-validation-error`;
                } else {
                    // Crear nuevo contenedor de error
                    errorContainer = document.createElement('span');
                    errorContainer.id = `${fieldId}-validation-error`;
                    errorContainer.className = 'text-red-600 text-xs mt-1 block validation-error-message';
                    errorContainer.style.display = 'none';
                    
                    // Insertar después del campo o del contenedor flex si existe
                    const parent = field.closest('.flex') || field;
                    if (parent.nextSibling) {
                        parent.parentNode.insertBefore(errorContainer, parent.nextSibling);
                    } else {
                        parent.parentNode.appendChild(errorContainer);
                    }
                }
            }
            
            return errorContainer;
        },

        /**
         * Valida un campo específico
         * @param {HTMLElement} field - Campo a validar
         * @returns {boolean} - true si es válido
         */
        validateField(field) {
            const value = field.value?.trim() || '';
            const fieldName = this.getFieldLabel(field);
            let isValid = true;
            let errorMessage = '';

            // Validación de campo vacío (requerido)
            if (field.hasAttribute('required') && !value) {
                isValid = false;
                errorMessage = `${fieldName} es obligatorio`;
            }

            // Validación de longitud mínima
            if (isValid && field.hasAttribute('minlength')) {
                const minLength = parseInt(field.getAttribute('minlength'));
                if (value.length > 0 && value.length < minLength) {
                    isValid = false;
                    errorMessage = `${fieldName} debe tener al menos ${minLength} caracteres`;
                }
            }

            // Validación de patrón
            if (isValid && field.hasAttribute('pattern') && value) {
                const pattern = new RegExp(field.getAttribute('pattern'));
                if (!pattern.test(value)) {
                    isValid = false;
                    errorMessage = field.getAttribute('title') || `${fieldName} tiene un formato inválido`;
                }
            }

            // Validación de email
            if (isValid && field.type === 'email' && value) {
                const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
                if (!emailRegex.test(value)) {
                    isValid = false;
                    errorMessage = 'Ingrese un correo electrónico válido';
                }
            }

            // Validación de número
            if (isValid && field.type === 'number' && value) {
                const numValue = parseFloat(value);
                
                if (field.hasAttribute('min')) {
                    const min = parseFloat(field.getAttribute('min'));
                    if (numValue < min) {
                        isValid = false;
                        errorMessage = `${fieldName} debe ser mayor o igual a ${min}`;
                    }
                }
                
                if (field.hasAttribute('max')) {
                    const max = parseFloat(field.getAttribute('max'));
                    if (numValue > max) {
                        isValid = false;
                        errorMessage = `${fieldName} debe ser menor o igual a ${max}`;
                    }
                }
            }

            // Validación de select (opción vacía)
            if (isValid && field.tagName === 'SELECT' && field.hasAttribute('required')) {
                if (!value || value === '') {
                    isValid = false;
                    errorMessage = `Seleccione ${fieldName.toLowerCase()}`;
                }
            }

            // Mostrar/ocultar error
            this.showFieldError(field, isValid ? '' : errorMessage);

            return isValid;
        },

        /**
         * Obtiene el label del campo para mensajes de error
         * @param {HTMLElement} field - Campo del formulario
         * @returns {string} - Nombre del campo
         */
        getFieldLabel(field) {
            // Buscar label asociado
            const label = document.querySelector(`label[for="${field.id}"]`);
            if (label) {
                // Obtener solo el texto, sin el asterisco
                let text = label.textContent.replace('*', '').trim();
                return text || field.name || 'Este campo';
            }
            
            // Usar placeholder como fallback
            if (field.placeholder) {
                return field.placeholder;
            }
            
            // Usar name como último recurso
            return field.name || 'Este campo';
        },

        /**
         * Muestra u oculta el mensaje de error de un campo
         * @param {HTMLElement} field - Campo del formulario
         * @param {string} message - Mensaje de error (vacío para limpiar)
         */
        showFieldError(field, message) {
            const fieldId = field.id || field.name;
            let errorContainer = document.getElementById(`${fieldId}-validation-error`);
            
            if (!errorContainer) {
                errorContainer = this.ensureErrorContainer(field);
            }

            if (message) {
                // Mostrar error
                errorContainer.textContent = message;
                errorContainer.style.display = 'block';
                errorContainer.classList.remove('hidden');
                
                // Añadir clases de error al campo
                field.classList.add('border-red-500', 'focus:border-red-500', 'focus:ring-red-500');
                field.classList.remove('border-gray-300', 'focus:border-primary-600', 'focus:ring-primary-600');
            } else {
                // Ocultar error
                errorContainer.textContent = '';
                errorContainer.style.display = 'none';
                errorContainer.classList.add('hidden');
                
                // Remover clases de error del campo
                field.classList.remove('border-red-500', 'focus:border-red-500', 'focus:ring-red-500');
                field.classList.add('border-gray-300', 'focus:border-primary-600', 'focus:ring-primary-600');
            }
        },

        /**
         * Valida todo el formulario
         * @param {string|HTMLFormElement} formSelector - Selector CSS o elemento del formulario
         * @returns {boolean} - true si todo el formulario es válido
         */
        validateForm(formSelector) {
            const form = typeof formSelector === 'string' 
                ? document.querySelector(formSelector) 
                : formSelector;
            
            if (!form) return false;

            const fields = form.querySelectorAll('input[required], select[required], textarea[required]');
            let isFormValid = true;

            fields.forEach(field => {
                const isFieldValid = this.validateField(field);
                if (!isFieldValid) {
                    isFormValid = false;
                }
            });

            return isFormValid;
        },

        /**
         * Limpia todos los errores de validación del formulario
         * @param {string|HTMLFormElement} formSelector - Selector CSS o elemento del formulario
         */
        clearErrors(formSelector) {
            const form = typeof formSelector === 'string' 
                ? document.querySelector(formSelector) 
                : formSelector;
            
            if (!form) return;

            const errorContainers = form.querySelectorAll('.validation-error-message, [id$="-validation-error"]');
            errorContainers.forEach(container => {
                container.textContent = '';
                container.style.display = 'none';
                container.classList.add('hidden');
            });

            const fields = form.querySelectorAll('.border-red-500');
            fields.forEach(field => {
                field.classList.remove('border-red-500', 'focus:border-red-500', 'focus:ring-red-500');
                field.classList.add('border-gray-300', 'focus:border-primary-600', 'focus:ring-primary-600');
            });
        }
    };

    // Exportar globalmente
    window.FormValidation = FormValidation;

    // Auto-inicialización para formularios con data-validate="true"
    document.addEventListener('DOMContentLoaded', () => {
        const forms = document.querySelectorAll('form[data-validate="true"]');
        forms.forEach(form => FormValidation.init(form));
    });
})();
