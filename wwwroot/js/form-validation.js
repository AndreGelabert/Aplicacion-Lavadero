/**
 * ================================================
 * FORM-VALIDATION.JS - VALIDACIÓN EN TIEMPO REAL DE FORMULARIOS
 * ================================================
 * Responsabilidades:
 *  - Validación en tiempo real de campos obligatorios
 *  - Mostrar/ocultar mensajes de error debajo de inputs
 *  - Estilos de error para campos inválidos
 *  - Integración con validaciones personalizadas
 */

(function () {
    'use strict';

    /**
     * Módulo de validación de formularios
     */
    const FormValidation = {
        // Almacén de validadores personalizados
        customValidators: {},

        /**
         * Registra un validador personalizado
         * @param {string} fieldId - ID del campo
         * @param {Function} validator - Función que retorna {isValid: boolean, message: string}
         */
        registerCustomValidator(fieldId, validator) {
            this.customValidators[fieldId] = validator;
        },

        /**
         * Inicializa la validación para un formulario específico
         * @param {string|HTMLFormElement} formSelector - Selector CSS o elemento del formulario
         * @param {Object} options - Opciones de configuración
         */
        init(formSelector, options = {}) {
            const form = typeof formSelector === 'string' 
                ? document.querySelector(formSelector) 
                : formSelector;
            
            if (!form) {
                console.warn('FormValidation: Formulario no encontrado', formSelector);
                return;
            }

            // Verificar si ya fue inicializado
            if (form.dataset.formValidationInitialized === 'true') {
                console.log('FormValidation: Formulario ya inicializado, saltando...', form.id || form.className);
                return;
            }

            // Marcar como inicializado
            form.dataset.formValidationInitialized = 'true';

            const config = {
                validateOnBlur: true,
                validateOnInput: true,
                showAsterisk: true,
                ...options
            };

            this.setupValidationListeners(form, config);
            
            // Agregar asteriscos rojos a campos obligatorios
            if (config.showAsterisk) {
                this.addRequiredAsterisks(form);
            }

            console.log('FormValidation inicializado para:', form.id || form.className);
        },

        /**
         * Agrega asteriscos rojos a labels de campos obligatorios
         * @param {HTMLFormElement} form - Elemento del formulario
         */
        addRequiredAsterisks(form) {
            const requiredFields = form.querySelectorAll('input[required], select[required], textarea[required]');
            
            requiredFields.forEach(field => {
                const label = field.labels ? field.labels[0] : document.querySelector(`label[for="${field.id}"]`);
                
                if (label) {
                    // Verificar si ya tiene asterisco (rojo o en texto)
                    const hasRedAsterisk = label.querySelector('.text-red-600, .text-red-500');
                    const hasTextAsterisk = label.textContent.includes('*');
                    
                    if (!hasRedAsterisk && !hasTextAsterisk) {
                        // Agregar asterisco rojo
                        const asterisk = document.createElement('span');
                        asterisk.className = 'text-red-600 ml-1';
                        asterisk.textContent = '*';
                        label.appendChild(asterisk);
                    } else if (hasTextAsterisk && !hasRedAsterisk) {
                        // Reemplazar asterisco de texto por uno rojo
                        label.innerHTML = label.innerHTML.replace(/\*/, '<span class="text-red-600 ml-1">*</span>');
                    }
                }
            });
        },

        /**
         * Configura los listeners de validación para todos los campos del formulario
         * @param {HTMLFormElement} form - Elemento del formulario
         * @param {Object} config - Configuración
         */
        setupValidationListeners(form, config) {
            const fields = form.querySelectorAll('input[required], select[required], textarea[required]');
            
            fields.forEach(field => {
                // Verificar si ya se inicializó para evitar duplicados
                if (field.dataset.formValidationInitialized === 'true') {
                    return;
                }
                
                // Marcar como inicializado
                field.dataset.formValidationInitialized = 'true';
                
                // Crear contenedor de error si no existe
                this.ensureErrorContainer(field);

                if (config.validateOnBlur) {
                    field.addEventListener('blur', () => this.validateField(field));
                }

                if (config.validateOnInput) {
                    field.addEventListener('input', () => {
                        // Pequeño debounce para mejorar rendimiento
                        clearTimeout(field._validationTimeout);
                        field._validationTimeout = setTimeout(() => {
                            this.validateField(field);
                        }, 300);
                    });
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
            
            // Si ya existe, retornarlo
            if (errorContainer) {
                return errorContainer;
            }
            
            // Buscar si ya existe un span de error asp-validation-for
            const parentElement = field.parentElement;
            if (parentElement) {
                const existingValidation = parentElement.querySelector(`span[data-valmsg-for="${field.name}"]`);
                if (existingValidation) {
                    existingValidation.id = `${fieldId}-validation-error`;
                    existingValidation.classList.add('validation-error-message');
                    return existingValidation;
                }
            }
            
            // Crear nuevo contenedor de error
            errorContainer = document.createElement('span');
            errorContainer.id = `${fieldId}-validation-error`;
            errorContainer.className = 'text-red-600 text-xs mt-1 block validation-error-message hidden';
            
            // Insertar DESPUÉS del campo (abajo)
            if (parentElement) {
                // Buscar el label del campo para insertar después de él si está después del input
                const label = parentElement.querySelector(`label[for="${field.id}"]`);
                
                // Si el label está después del input, insertar después del label
                if (label && field.compareDocumentPosition(label) === Node.DOCUMENT_POSITION_FOLLOWING) {
                    if (label.nextSibling) {
                        parentElement.insertBefore(errorContainer, label.nextSibling);
                    } else {
                        parentElement.appendChild(errorContainer);
                    }
                } else {
                    // Insertar después del input directamente
                    if (field.nextSibling) {
                        parentElement.insertBefore(errorContainer, field.nextSibling);
                    } else {
                        parentElement.appendChild(errorContainer);
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
            // Primero verificar si hay un validador personalizado
            if (this.customValidators[field.id]) {
                const result = this.customValidators[field.id]();
                this.showFieldError(field, result.isValid ? '' : result.message);
                return result.isValid;
            }

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
            const label = field.labels ? field.labels[0] : document.querySelector(`label[for="${field.id}"]`);
            if (label) {
                // Obtener solo el texto, sin el asterisco
                let text = label.textContent.replace(/\*/g, '').trim();
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

            // Limpiar timeout anterior si existe
            if (errorContainer._hideTimeout) {
                clearTimeout(errorContainer._hideTimeout);
                delete errorContainer._hideTimeout;
            }

            if (message) {
                // Mostrar error solo si el mensaje cambió (evita re-renders innecesarios)
                if (errorContainer.textContent !== message) {
                    errorContainer.textContent = message;
                }
                errorContainer.classList.remove('hidden');
                
                // Añadir clases de error al campo
                field.classList.add('border-red-500', 'focus:border-red-500', 'focus:ring-red-500');
                field.classList.remove('border-gray-300', 'focus:border-primary-600', 'focus:ring-primary-600');

                // Auto-ocultar SOLO mensajes de "obligatorio" después de 3 segundos
                const isRequiredMessage = message.toLowerCase().includes('obligatorio') || 
                                         message.toLowerCase().includes('requerido') ||
                                         message.toLowerCase().includes('seleccione');
                
                if (isRequiredMessage) {
                    errorContainer._hideTimeout = setTimeout(() => {
                        // Solo ocultar si el campo sigue vacío y el mensaje no cambió
                        const currentValue = field.value?.trim() || '';
                        if (!currentValue && errorContainer.textContent === message) {
                            errorContainer.textContent = '';
                            errorContainer.classList.add('hidden');
                            
                            // Remover clases de error del campo
                            field.classList.remove('border-red-500', 'focus:border-red-500', 'focus:ring-red-500');
                            field.classList.add('border-gray-300', 'focus:border-primary-600', 'focus:ring-primary-600');
                        }
                    }, 3000); // 3 segundos
                }
            } else {
                // Ocultar error
                errorContainer.textContent = '';
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
        console.log(`FormValidation: Encontrados ${forms.length} formularios con data-validate="true"`);
        forms.forEach(form => FormValidation.init(form));
    });
})();
