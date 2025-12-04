/**
 * ================================================
 * CLIENTE-VALIDATION-INTEGRATION.JS
 * ================================================
 * Integra FormValidation con las validaciones personalizadas de cliente
 */

(function () {
    'use strict';

    // Bandera para evitar inicialización múltiple
    let initialized = false;

    // Esperar a que DOMContentLoaded y que cliente.js estén listos
    document.addEventListener('DOMContentLoaded', function () {
        // Esperar un poco para asegurar que cliente.js se haya inicializado
        setTimeout(initializeClienteFormValidation, 300);
    });

    function initializeClienteFormValidation() {
        // Evitar inicialización múltiple
        if (initialized) {
            console.log('ClienteValidation: Ya inicializado, saltando...');
            return;
        }

        const form = document.getElementById('cliente-form');
        
        if (!form) {
            console.log('ClienteValidation: Formulario no encontrado');
            return;
        }

        if (!window.FormValidation) {
            console.warn('ClienteValidation: FormValidation no disponible');
            return;
        }

        console.log('ClienteValidation: Inicializando validación del formulario');

        // Marcar como inicializado
        initialized = true;

        // Inicializar FormValidation
        window.FormValidation.init(form, {
            validateOnBlur: true,
            validateOnInput: true,
            showAsterisk: true
        });

        // Registrar validador personalizado para NumeroDocumento
        // Usa la función validateDocumentoNumero que ya existe en cliente.js
        if (typeof window.validateDocumentoNumero === 'function') {
            window.FormValidation.registerCustomValidator('NumeroDocumento', function() {
                const isValid = window.validateDocumentoNumero();
                const numeroDocInput = document.getElementById('NumeroDocumento');
                const errorSpan = document.getElementById('documento-validation-error');
                
                let message = '';
                if (!isValid && errorSpan && !errorSpan.classList.contains('hidden')) {
                    message = errorSpan.textContent;
                }
                
                return {
                    isValid: isValid,
                    message: message || 'El formato del número de documento no es válido'
                };
            });
            console.log('ClienteValidation: Validador de NumeroDocumento registrado');
        }

        // Hacer disponible globalmente la validación del formulario
        window.validateClienteForm = function() {
            return window.FormValidation.validateForm(form);
        };

        console.log('ClienteValidation: Inicialización completada');
    }

})();
