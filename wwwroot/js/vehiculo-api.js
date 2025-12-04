/**
 * VEHICULO-API.JS - INTEGRACIÓN CON CARQUERY API
 * Soporta carga dinámica (modales AJAX)
 */



(function () {
    'use strict';

    

    let marcasCache = null;
    let coloresCache = null;
    let initialized = false;

    // ==================== FUNCIÓN DE INICIALIZACIÓN PRINCIPAL ====================
    function initVehiculoApiSelects() {
        

        // Evitar inicialización múltiple
        if (initialized) {
            
            return;
        }

        const vehiculoForm = document.getElementById('vehiculo-form');
        

        if (!vehiculoForm) {
            console.error('? No se encontró formulario vehiculo-form');
            return;
        }

        const editValue = vehiculoForm.dataset.edit;
        

        const isEdit = editValue === 'true' || editValue === 'True' || editValue === true;
        

        if (isEdit) {
            
            return;
        }

        
        initialized = true;

        // Inicializar de forma asíncrona
        setTimeout(async () => {
            try {
                
                // Inicializar estado
                lockMarcaSelect();
                resetModeloSelect();

                // Configurar listeners
                setupTipoVehiculoChangeListener();
                setupMarcaChangeListener();
                setupColorToggle();

                // Cargar datos iniciales
                await loadMarcas();
                await loadColores();

                
            } catch (error) {
                console.error('?? ERROR en inicialización:', error);
            }
        }, 100); // Pequeño delay para asegurar que el DOM esté listo
    }

    // ==================== AUTO-INICIALIZACIÓN ====================

    // Estrategia 1: DOMContentLoaded (para carga normal)
    if (document.readyState === 'loading') {
        
        document.addEventListener('DOMContentLoaded', function () {
            
            initVehiculoApiSelects();
        });
    } else {
        // DOM ya está listo
        
        initVehiculoApiSelects();
    }

    // Estrategia 2: Exponer globalmente para llamadas manuales
    window.initVehiculoApiSelects = initVehiculoApiSelects;

    // Estrategia 3: Observador de mutaciones para detectar cuando se añade el formulario
    const observer = new MutationObserver(function (mutations) {
        mutations.forEach(function (mutation) {
            mutation.addedNodes.forEach(function (node) {
                if (node.nodeType === 1) { // Element node
                    // Verificar si es el formulario o lo contiene
                    if (node.id === 'vehiculo-form' || node.querySelector && node.querySelector('#vehiculo-form')) {
                        
                        initVehiculoApiSelects();
                    }
                }
            });
        });
    });

    // Observar cambios en el body
    if (document.body) {
        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
        
    }

    // ==================== BLOQUEAR SELECT DE MARCA ====================
    function lockMarcaSelect() {
        const marcaInput = document.getElementById('Marca');
        if (!marcaInput) return;

        marcaInput.disabled = true;
        marcaInput.placeholder = 'Primero seleccione un tipo de vehículo';
    }

    // ==================== LISTENER DE TIPO DE VEHÍCULO ====================
    function setupTipoVehiculoChangeListener() {
        const tipoSelect = document.getElementById('TipoVehiculo');
        if (!tipoSelect) {
            console.error('? No se encontró select TipoVehiculo');
            return;
        }

        tipoSelect.addEventListener('change', async function () {
            const tipoVehiculo = this.value?.trim();
            if (!tipoVehiculo) {
                lockMarcaSelect();
                return;
            }

            // Deshabilitar temporalmente mientras carga
            lockMarcaSelect();

            await loadMarcasPorTipo(tipoVehiculo);

            // Habilitar marca después de cargar
            const marcaInput = document.getElementById('Marca');
            if (marcaInput) {
                marcaInput.disabled = false;
                marcaInput.placeholder = 'Seleccione o escriba una marca...';
            }
        });

        
    }

    // ==================== CARGAR MARCAS POR TIPO ====================
    async function loadMarcasPorTipo(tipoVehiculo) {
        

        const marcaInput = document.getElementById('Marca');
        const loadingText = document.getElementById('marca-loading');

        if (!marcaInput) return;

        try {
            

            const url = `/Vehiculo/GetMarcasPorTipo?tipoVehiculo=${encodeURIComponent(tipoVehiculo)}`;
            

            const response = await fetch(url, { 
                cache: 'no-cache',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (!response.ok) throw new Error('Error marcas por tipo');

            const marcas = await response.json();
            

            if (marcas && marcas.length > 0) {
                renderMarcaOptions(marcas);
            } else {
                renderMarcaOptions([]);
            }
        } catch (error) {
            
            renderMarcaOptions([]);
        } finally {
            
        }
    }

    // ==================== CARGAR MARCAS (SIN FILTRO) ====================
    async function loadMarcas() {
        

        const marcaInput = document.getElementById('Marca');
        const loadingText = document.getElementById('marca-loading');

        

        if (!marcaInput) {
            
            return;
        }

        try {
            

            const response = await fetch('/Vehiculo/GetMarcas', { cache: 'no-cache' });
            if (!response.ok) throw new Error('Error marcas');

            const marcas = await response.json();
            

            if (marcas && marcas.length > 0) {
                marcasCache = marcas;
                renderMarcaOptions(marcas);
            }
        } catch (error) {
            
            renderMarcaOptions(['Toyota', 'Ford', 'Chevrolet', 'Honda', 'Nissan']);
        } finally {
            
        }
    }

    function renderMarcaOptions(marcas) {
        

        const marcaDatalist = document.getElementById('marcas-datalist');
        if (!marcaDatalist) {
            
            return;
        }

        marcaDatalist.innerHTML = '';

        marcas.forEach((marca, index) => {
            // Soportar tanto minúsculas (id, nombre) como mayúsculas (Id, Nombre)
            const id = marca.id || marca.Id;
            const nombre = marca.nombre || marca.Nombre;

            if (!id || !nombre) {
                
                return; // Saltar esta marca
            }

            const option = document.createElement('option');
            option.value = nombre;
            marcaDatalist.appendChild(option);

            
        });

        
    }

    // ==================== CARGAR MODELOS ====================
    function setupMarcaChangeListener() {
        

        const marcaInput = document.getElementById('Marca');
        if (!marcaInput) {
            
            return;
        }

        marcaInput.addEventListener('change', async function () {
            

            const marcaNombre = this.value.trim();
            if (!marcaNombre) {
                resetModeloSelect();
                return;
            }

            // Buscar el ID de la marca por nombre
            const marca = marcasCache.find(m => (m.nombre || m.Nombre) === marcaNombre);
            if (!marca) {
                
                resetModeloSelect();
                return;
            }

            const marcaId = marca.id || marca.Id;
            if (!marcaId) {
                
                resetModeloSelect();
                return;
            }

            await loadModelos(marcaId);
        });

        
    }

    async function loadModelos(marcaId) {
        

        const modeloInput = document.getElementById('Modelo');
        if (!modeloInput) return;

        modeloInput.disabled = true;
        modeloInput.placeholder = 'Cargando modelos...';

        try {
            const url = `/Vehiculo/GetModelos?marcaId=${encodeURIComponent(marcaId)}`;
            

            const response = await fetch(url, { cache: 'no-cache' });
            if (!response.ok) throw new Error('Error modelos');

            const modelos = await response.json();
            if (!modelos || modelos.length === 0) throw new Error('Sin modelos');
            

            renderModeloOptions(modelos);
        } catch (error) {
            
            renderModeloOptions([]);
        }
    }

    function renderModeloOptions(modelos) {
        const modeloDatalist = document.getElementById('modelos-datalist');
        if (!modeloDatalist) return;

        modeloDatalist.innerHTML = '';

        modelos.forEach(modelo => {
            const option = document.createElement('option');
            option.value = modelo.nombre;
            modeloDatalist.appendChild(option);
        });

        const modeloInput = document.getElementById('Modelo');
        if (modeloInput) {
            modeloInput.disabled = false;
            modeloInput.placeholder = 'Seleccione o escriba un modelo...';
        }
        
    }

    function resetModeloSelect() {
        const modeloDatalist = document.getElementById('modelos-datalist');
        if (modeloDatalist) modeloDatalist.innerHTML = '';
        const modeloInput = document.getElementById('Modelo');
        if (modeloInput) {
            modeloInput.value = '';
            modeloInput.disabled = true;
        }
    }

    // ==================== CARGAR COLORES ====================
    async function loadColores() {
        

        try {
            const response = await fetch('/Vehiculo/GetColores', { cache: 'no-cache' });
            if (!response.ok) throw new Error('Error colores');

            const colores = await response.json();
            

            if (colores && colores.length > 0) {
                coloresCache = colores;
                renderColorOptions(colores);
            }
        } catch (error) {
            console.warn('?? Error colores, usando fallback');
            renderColorOptions(['Blanco', 'Negro', 'Gris', 'Plata', 'Rojo', 'Azul']);
        }
    }

    function renderColorOptions(colores) {
        const colorSelect = document.getElementById('Color');
        if (!colorSelect) return;

        colorSelect.innerHTML = '<option value="">Seleccione un color...</option>';

        colores.forEach(color => {
            const option = document.createElement('option');
            option.value = color;
            option.textContent = color;
            colorSelect.appendChild(option);
        });

        
    }

    // ==================== TOGGLE COLOR ====================
    function setupColorToggle() {
        

        const toggle = document.getElementById('color-custom-toggle');
        const colorSelect = document.getElementById('Color');
        const colorInput = document.getElementById('ColorCustom');

        if (!toggle || !colorSelect || !colorInput) {
            console.warn('?? Elementos toggle no encontrados');
            return;
        }

        toggle.addEventListener('change', function () {
            

            if (this.checked) {
                colorSelect.classList.add('hidden');
                colorSelect.removeAttribute('required');
                colorSelect.removeAttribute('name');
                colorInput.classList.remove('hidden');
                colorInput.setAttribute('required', 'required');
                colorInput.setAttribute('name', 'Color');
                colorInput.focus();
            } else {
                colorInput.classList.add('hidden');
                colorInput.removeAttribute('required');
                colorInput.removeAttribute('name');
                colorSelect.classList.remove('hidden');
                colorSelect.setAttribute('required', 'required');
                colorSelect.setAttribute('name', 'Color');
            }
        });

        
    }

    // ==================== UTILIDADES ====================
    function convertToTextInput(selectElement, fieldName) {
        
        const input = document.createElement('input');
        input.type = 'text';
        input.id = selectElement.id;
        input.name = selectElement.name;
        input.className = selectElement.className;
        input.placeholder = `Ingrese ${fieldName.toLowerCase()} manualmente`;
        input.required = selectElement.hasAttribute('required');
        selectElement.parentNode.replaceChild(input, selectElement);
        
    }

    function showLoading(selectElement, loadingElement, message) {
        selectElement.disabled = true;
        selectElement.innerHTML = `<option value="">${message}</option>`;
        if (loadingElement) loadingElement.textContent = message;
    }

    function hideLoading(loadingElement) {
        if (loadingElement) loadingElement.textContent = '';
    }

    // Exponer funciones globalmente
    window.VehiculoApi = {
        init: initVehiculoApiSelects,
        loadMarcas,
        loadModelos,
        loadColores,
        resetInitialized: () => { initialized = false; }
    };

    

})();