/**
 * VEHICULO-API.JS - INTEGRACIÓN CON CARQUERY API
 * Soporta carga dinámica (modales AJAX)
 */

console.log('🚀🚀🚀 SCRIPT vehiculo-api.js CARGADO');

(function () {
    'use strict';

    console.log('✅ IIFE iniciado');

    let marcasCache = null;
    let coloresCache = null;
    let initialized = false;

    // ==================== FUNCIÓN DE INICIALIZACIÓN PRINCIPAL ====================
    function initVehiculoApiSelects() {
        console.log('🎯🎯🎯 initVehiculoApiSelects() LLAMADO');

        // Evitar inicialización múltiple
        if (initialized) {
            console.log('⚠️ Ya inicializado, saltando...');
            return;
        }

        const vehiculoForm = document.getElementById('vehiculo-form');
        console.log('📝 Formulario:', vehiculoForm);

        if (!vehiculoForm) {
            console.error('❌ No se encontró formulario vehiculo-form');
            return;
        }

        const editValue = vehiculoForm.dataset.edit;
        console.log('🔍 data-edit:', editValue, 'tipo:', typeof editValue);

        const isEdit = editValue === 'true' || editValue === 'True' || editValue === true;
        console.log('🔍 isEdit:', isEdit);

        if (isEdit) {
            console.log('⏸️ MODO EDICIÓN detectado, NO inicializando API');
            return;
        }

        console.log('✅ Modo creación detectado, inicializando API...');
        initialized = true;

        // Inicializar de forma asíncrona
        setTimeout(async () => {
            try {
                console.log('1️⃣ Bloqueando select de Marca (requiere Tipo de Vehículo)...');
                lockMarcaSelect();

                console.log('2️⃣ Cargando colores...');
                await loadColores();

                console.log('3️⃣ Configurando listeners...');
                setupTipoVehiculoChangeListener(); // NUEVO
                setupMarcaChangeListener();
                setupColorToggle();

                console.log('✅✅✅ Inicialización COMPLETADA');
            } catch (error) {
                console.error('💥 ERROR en inicialización:', error);
            }
        }, 100); // Pequeño delay para asegurar que el DOM esté listo
    }

    // ==================== AUTO-INICIALIZACIÓN ====================

    // Estrategia 1: DOMContentLoaded (para carga normal)
    if (document.readyState === 'loading') {
        console.log('📌 Esperando DOMContentLoaded...');
        document.addEventListener('DOMContentLoaded', function () {
            console.log('🎬 DOMContentLoaded disparado');
            initVehiculoApiSelects();
        });
    } else {
        // DOM ya está listo
        console.log('📌 DOM ya está listo, inicializando...');
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
                        console.log('🔍 Formulario detectado por observer!');
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
        console.log('👁️ Observer activado');
    }

    // ==================== BLOQUEAR SELECT DE MARCA ====================
    function lockMarcaSelect() {
        const marcaSelect = document.getElementById('Marca');
        if (!marcaSelect) return;

        marcaSelect.disabled = true;
        marcaSelect.innerHTML = '<option value="">Primero seleccione un tipo de vehículo</option>';
        console.log('🔒 Select de Marca bloqueado');
    }

    // ==================== LISTENER DE TIPO DE VEHÍCULO ====================
    function setupTipoVehiculoChangeListener() {
        console.log('🎧 setupTipoVehiculoChangeListener()');

        const tipoSelect = document.getElementById('TipoVehiculo');
        if (!tipoSelect) {
            console.error('❌ No se encontró select TipoVehiculo');
            return;
        }

        tipoSelect.addEventListener('change', async function () {
            const tipoVehiculo = this.value;
            console.log('🔄 Tipo de vehículo cambiado a:', tipoVehiculo);

            const marcaSelect = document.getElementById('Marca');
            const modeloSelect = document.getElementById('Modelo');

            if (!tipoVehiculo) {
                // Si no hay tipo seleccionado, bloquear marca
                lockMarcaSelect();
                resetModeloSelect();
                return;
            }

            // Cargar marcas para el tipo seleccionado
            await loadMarcasPorTipo(tipoVehiculo);
        });

        console.log('✅ Listener de TipoVehiculo registrado');
    }

    // ==================== CARGAR MARCAS POR TIPO ====================
    async function loadMarcasPorTipo(tipoVehiculo) {
        console.log('🚗 loadMarcasPorTipo() para:', tipoVehiculo);

        const marcaSelect = document.getElementById('Marca');
        const loadingText = document.getElementById('marca-loading');

        if (!marcaSelect) return;

        try {
            showLoading(marcaSelect, loadingText, `Cargando marcas de ${tipoVehiculo}...`);

            const url = `/Vehiculo/GetMarcasPorTipo?tipoVehiculo=${encodeURIComponent(tipoVehiculo)}`;
            console.log('📡 Fetch:', url);

            const response = await fetch(url, { 
                cache: 'no-cache',
                headers: { 'Accept': 'application/json' }
            });

            console.log('📡 Response:', response.status, response.ok);

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const marcas = await response.json();
            console.log('📦 Marcas recibidas:', marcas.length);

            if (!marcas || marcas.length === 0) {
                throw new Error('Sin marcas para este tipo');
            }

            marcasCache = marcas;
            renderMarcaOptions(marcas);
            hideLoading(loadingText);

            console.log(`✅ ${marcas.length} marcas cargadas para ${tipoVehiculo}`);

        } catch (error) {
            console.error('💥 Error al cargar marcas:', error);
            convertToTextInput(marcaSelect, 'Marca');
            hideLoading(loadingText);
        }
    }

    // ==================== CARGAR MARCAS (SIN FILTRO) ====================
    async function loadMarcas() {
        console.log('🔥🔥🔥 loadMarcas() INICIADO - SIN FILTRO');

        const marcaSelect = document.getElementById('Marca');
        const loadingText = document.getElementById('marca-loading');

        console.log('🔍 Marca select:', marcaSelect);
        console.log('🔍 Loading text:', loadingText);

        if (!marcaSelect) {
            console.error('❌ No se encontró select Marca');
            return;
        }

        try {
            console.log('⏳ Mostrando loading...');
            showLoading(marcaSelect, loadingText, 'Cargando marcas...');

            console.log('📡 Haciendo fetch a /Vehiculo/GetMarcas');
            const response = await fetch('/Vehiculo/GetMarcas', {
                cache: 'no-cache',
                headers: { 'Accept': 'application/json' }
            });

            console.log('📡 Response:', response.status, response.ok);

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const marcas = await response.json();
            console.log('📦 Marcas recibidas:', marcas.length);

            if (!marcas || marcas.length === 0) {
                throw new Error('Sin marcas');
            }

            marcasCache = marcas;
            localStorage.setItem('vehiculo_marcas_cache', JSON.stringify(marcas));
            localStorage.setItem('vehiculo_marcas_timestamp', Date.now().toString());

            console.log('🎨 Renderizando opciones...');
            renderMarcaOptions(marcas);

            console.log('🧹 Ocultando loading...');
            hideLoading(loadingText);

            console.log(`✅✅✅ ${marcas.length} marcas cargadas EXITOSAMENTE`);

        } catch (error) {
            console.error('💥💥💥 ERROR en loadMarcas:', error);

            // Fallback: convertir a input
            console.log('🔄 Usando fallback...');
            convertToTextInput(marcaSelect, 'Marca');
            hideLoading(loadingText);
        }
    }

    function renderMarcaOptions(marcas) {
        console.log('🎨 renderMarcaOptions() con', marcas.length, 'marcas');

        const marcaSelect = document.getElementById('Marca');
        if (!marcaSelect) {
            console.error('❌ No se encontró select en renderMarcaOptions');
            return;
        }

        marcaSelect.innerHTML = '<option value="">Seleccione una marca...</option>';

        marcas.forEach((marca, index) => {
            // Soportar tanto minúsculas (id, nombre) como mayúsculas (Id, Nombre)
            const id = marca.id || marca.Id;
            const nombre = marca.nombre || marca.Nombre;

            if (!id || !nombre) {
                console.warn('⚠️ Marca sin id o nombre:', marca);
                return; // Saltar esta marca
            }

            const option = document.createElement('option');
            option.value = id;
            option.textContent = nombre;
            marcaSelect.appendChild(option);

            if (index < 3) {
                console.log(`   ➕ ${id} - ${nombre}`);
            }
        });

        marcaSelect.disabled = false;
        console.log('✅ Select habilitado con', marcaSelect.options.length, 'opciones');
    }

    // ==================== CARGAR MODELOS ====================
    function setupMarcaChangeListener() {
        console.log('🎧 setupMarcaChangeListener()');

        const marcaSelect = document.getElementById('Marca');
        if (!marcaSelect) {
            console.error('❌ No se encontró select para listener');
            return;
        }

        marcaSelect.addEventListener('change', async function () {
            console.log('🔄 Marca cambiada a:', this.value);

            if (!this.value) {
                resetModeloSelect();
                return;
            }

            await loadModelos(this.value);
        });

        console.log('✅ Listener registrado');
    }

    async function loadModelos(marcaId) {
        console.log('🚗 loadModelos() para:', marcaId);

        const modeloSelect = document.getElementById('Modelo');
        if (!modeloSelect) return;

        modeloSelect.disabled = true;
        modeloSelect.innerHTML = '<option value="">Cargando modelos...</option>';

        try {
            const url = `/Vehiculo/GetModelos?marcaId=${encodeURIComponent(marcaId)}`;
            console.log('📡 Fetch:', url);

            const response = await fetch(url, { cache: 'no-cache' });
            console.log('📡 Response:', response.status);

            if (!response.ok) throw new Error(`HTTP ${response.status}`);

            const modelos = await response.json();
            console.log('📦 Modelos:', modelos.length);

            if (!modelos || modelos.length === 0) throw new Error('Sin modelos');

            renderModeloOptions(modelos);

        } catch (error) {
            console.error('💥 Error modelos:', error);
            convertToTextInput(modeloSelect, 'Modelo');
        }
    }

    function renderModeloOptions(modelos) {
        const modeloSelect = document.getElementById('Modelo');
        if (!modeloSelect) return;

        modeloSelect.innerHTML = '<option value="">Seleccione un modelo...</option>';

        modelos.forEach(modelo => {
            const option = document.createElement('option');
            option.value = modelo.nombre;
            option.textContent = modelo.nombre;
            modeloSelect.appendChild(option);
        });

        modeloSelect.disabled = false;
        console.log('✅', modelos.length, 'modelos renderizados');
    }

    function resetModeloSelect() {
        const modeloSelect = document.getElementById('Modelo');
        if (!modeloSelect) return;
        modeloSelect.innerHTML = '<option value="">Primero seleccione una marca</option>';
        modeloSelect.disabled = true;
    }

    // ==================== CARGAR COLORES ====================
    async function loadColores() {
        console.log('🎨 loadColores()');

        try {
            const response = await fetch('/Vehiculo/GetColores', { cache: 'no-cache' });
            if (!response.ok) throw new Error('Error colores');

            const colores = await response.json();
            console.log('📦 Colores:', colores.length);

            if (colores && colores.length > 0) {
                coloresCache = colores;
                renderColorOptions(colores);
            }
        } catch (error) {
            console.warn('⚠️ Error colores, usando fallback');
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

        console.log('✅', colores.length, 'colores renderizados');
    }

    // ==================== TOGGLE COLOR ====================
    function setupColorToggle() {
        console.log('🎨 setupColorToggle()');

        const toggle = document.getElementById('color-custom-toggle');
        const colorSelect = document.getElementById('Color');
        const colorInput = document.getElementById('ColorCustom');

        if (!toggle || !colorSelect || !colorInput) {
            console.warn('⚠️ Elementos toggle no encontrados');
            return;
        }

        toggle.addEventListener('change', function () {
            console.log('🔄 Toggle:', this.checked);

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

        console.log('✅ Toggle configurado');
    }

    // ==================== UTILIDADES ====================
    function convertToTextInput(selectElement, fieldName) {
        console.log('🔄 convertToTextInput:', fieldName);
        const input = document.createElement('input');
        input.type = 'text';
        input.id = selectElement.id;
        input.name = selectElement.name;
        input.className = selectElement.className;
        input.placeholder = `Ingrese ${fieldName.toLowerCase()} manualmente`;
        input.required = selectElement.hasAttribute('required');
        selectElement.parentNode.replaceChild(input, selectElement);
        console.log('✅ Convertido a input');
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

    console.log('✅ Módulo configurado');

})();

console.log('🏁 SCRIPT vehiculo-api.js TERMINADO');
