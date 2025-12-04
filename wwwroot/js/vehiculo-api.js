/**
 * VEHICULO-API.JS - INTEGRACIÓN CON CARQUERY API
 * Soporta carga dinámica (modales AJAX) con dropdowns personalizados
 */

(function () {
    'use strict';

    let marcasCache = null;
    let modelosCache = null;
    let coloresCache = null;
    let initialized = false;
    let marcaSeleccionadaValida = null;
    let modeloSeleccionadoValido = null;

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
                setupModeloChangeListener();
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
        marcasCache = marcas;
        const marcaDropdown = document.getElementById('marca-dropdown');
        const marcaInput = document.getElementById('Marca');
        if (!marcaDropdown) return;

        marcaDropdown.innerHTML = '';

        if (!marcas || marcas.length === 0) {
            marcaDropdown.innerHTML = '<div class="p-2 text-sm text-gray-500 dark:text-gray-400">No hay marcas disponibles</div>';
            return;
        }

        marcas.forEach((marca) => {
            const id = marca.id || marca.Id;
            const nombre = marca.nombre || marca.Nombre;

            if (!id || !nombre) return;

            const div = document.createElement('div');
            div.className = 'px-3 py-2 cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 text-sm text-gray-900 dark:text-white';
            div.textContent = nombre;
            div.dataset.marcaId = id;
            div.dataset.marcaNombre = nombre;

            div.addEventListener('click', function () {
                selectMarca(id, nombre);
            });

            marcaDropdown.appendChild(div);
        });

        // Posicionar el dropdown
        positionDropdown(marcaInput, marcaDropdown);
    }

    // ==================== LISTENER DE MARCA ====================
    function setupMarcaChangeListener() {
        const marcaInput = document.getElementById('Marca');
        const marcaDropdown = document.getElementById('marca-dropdown');

        if (!marcaInput || !marcaDropdown) return;

        // Input - mostrar dropdown y filtrar
        marcaInput.addEventListener('input', function () {
            const valor = this.value.trim();

            // Limpiar validación custom
            this.setCustomValidity('');

            // Resetear marca válida si se modifica
            if (marcaSeleccionadaValida && marcaSeleccionadaValida.nombre !== valor) {
                marcaSeleccionadaValida = null;
                resetModeloSelect();
            }

            if (valor.length > 0 && marcasCache) {
                filterMarcas(valor);
            } else if (marcasCache) {
                renderMarcaOptions(marcasCache);
                marcaDropdown.classList.remove('hidden');
            } else {
                marcaDropdown.classList.add('hidden');
            }
        });

        // Focus - mostrar todas las opciones
        marcaInput.addEventListener('focus', function () {
            if (marcasCache && marcasCache.length > 0) {
                renderMarcaOptions(marcasCache);
                positionDropdown(marcaInput, marcaDropdown);
                marcaDropdown.classList.remove('hidden');
            }
        });

        // Blur - validar y ocultar dropdown
        marcaInput.addEventListener('blur', function () {
            // Pequeño delay para permitir clicks en el dropdown
            setTimeout(() => {
                validateMarca();
                marcaDropdown.classList.add('hidden');
            }, 200);
        });

        // Validación al enviar formulario
        const form = marcaInput.closest('form');
        if (form) {
            form.addEventListener('submit', function (e) {
                if (!validateMarca()) {
                    e.preventDefault();
                    marcaInput.reportValidity();
                }
            });
        }
    }

    // ==================== LISTENER DE MODELO ====================
    function setupModeloChangeListener() {
        const modeloInput = document.getElementById('Modelo');
        const modeloDropdown = document.getElementById('modelo-dropdown');

        if (!modeloInput || !modeloDropdown) return;

        // Input - mostrar dropdown y filtrar
        modeloInput.addEventListener('input', function () {
            const valor = this.value.trim();

            // Limpiar validación custom
            this.setCustomValidity('');

            // Resetear modelo válido si se modifica
            if (modeloSeleccionadoValido !== valor) {
                modeloSeleccionadoValido = null;
            }

            if (valor.length > 0 && modelosCache) {
                filterModelos(valor);
            } else if (modelosCache) {
                renderModeloOptions(modelosCache);
                modeloDropdown.classList.remove('hidden');
            } else {
                modeloDropdown.classList.add('hidden');
            }
        });

        // Focus - mostrar todas las opciones
        modeloInput.addEventListener('focus', function () {
            if (modelosCache && modelosCache.length > 0) {
                renderModeloOptions(modelosCache);
                positionDropdown(modeloInput, modeloDropdown);
                modeloDropdown.classList.remove('hidden');
            }
        });

        // Blur - validar y ocultar dropdown
        modeloInput.addEventListener('blur', function () {
            // Pequeño delay para permitir clicks en el dropdown
            setTimeout(() => {
                validateModelo();
                modeloDropdown.classList.add('hidden');
            }, 200);
        });

        // Validación al enviar formulario
        const form = modeloInput.closest('form');
        if (form) {
            form.addEventListener('submit', function (e) {
                if (!validateModelo()) {
                    e.preventDefault();
                    modeloInput.reportValidity();
                }
            });
        }
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
        modelosCache = modelos;
        const modeloDropdown = document.getElementById('modelo-dropdown');
        const modeloInput = document.getElementById('Modelo');
        if (!modeloDropdown) return;

        modeloDropdown.innerHTML = '';

        if (!modelos || modelos.length === 0) {
            modeloDropdown.innerHTML = '<div class="p-2 text-sm text-gray-500 dark:text-gray-400">No hay modelos disponibles</div>';
            return;
        }

        modelos.forEach(modelo => {
            const nombre = modelo.nombre || modelo.Nombre;
            if (!nombre) return;

            const div = document.createElement('div');
            div.className = 'px-3 py-2 cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 text-sm text-gray-900 dark:text-white';
            div.textContent = nombre;
            div.dataset.modeloNombre = nombre;

            div.addEventListener('click', function () {
                selectModelo(nombre);
            });

            modeloDropdown.appendChild(div);
        });

        if (modeloInput) {
            modeloInput.disabled = false;
            modeloInput.placeholder = 'Seleccione o escriba un modelo...';
        }

        // Posicionar el dropdown
        positionDropdown(modeloInput, modeloDropdown);
    }

    function resetModeloSelect() {
        const modeloDropdown = document.getElementById('modelo-dropdown');
        if (modeloDropdown) {
            modeloDropdown.innerHTML = '';
            modeloDropdown.classList.add('hidden');
        }
        const modeloInput = document.getElementById('Modelo');
        if (modeloInput) {
            modeloInput.value = '';
            modeloInput.disabled = true;
            modeloInput.placeholder = 'Primero seleccione una marca...';
        }
        modeloSeleccionadoValido = null;
        modelosCache = null;
    }

    // ==================== POSICIONAR DROPDOWN ====================
    function positionDropdown(inputElement, dropdownElement) {
        if (!inputElement || !dropdownElement) return;

        const rect = inputElement.getBoundingClientRect();

        // Posicionar justo debajo del input
        dropdownElement.style.top = `${rect.bottom + window.scrollY}px`;
        dropdownElement.style.left = `${rect.left + window.scrollX}px`;
        dropdownElement.style.width = `${rect.width}px`;
    }

    // ==================== SELECCIÓN DE MARCA ====================
    function selectMarca(id, nombre) {
        const marcaInput = document.getElementById('Marca');
        const marcaDropdown = document.getElementById('marca-dropdown');

        if (marcaInput) {
            marcaInput.value = nombre;
            marcaSeleccionadaValida = { id, nombre };
        }

        if (marcaDropdown) {
            marcaDropdown.classList.add('hidden');
        }

        // Cargar modelos automáticamente
        loadModelos(id);
    }

    // ==================== SELECCIÓN DE MODELO ====================
    function selectModelo(nombre) {
        const modeloInput = document.getElementById('Modelo');
        const modeloDropdown = document.getElementById('modelo-dropdown');

        if (modeloInput) {
            modeloInput.value = nombre;
            modeloSeleccionadoValido = nombre;
        }

        if (modeloDropdown) {
            modeloDropdown.classList.add('hidden');
        }
    }

    // ==================== FILTRADO DE MARCA ====================
    function filterMarcas(searchText) {
        const marcaDropdown = document.getElementById('marca-dropdown');
        if (!marcaDropdown || !marcasCache) return;

        const filtered = searchText.trim() === ''
            ? marcasCache
            : marcasCache.filter(m => {
                const nombre = (m.nombre || m.Nombre || '').toLowerCase();
                return nombre.includes(searchText.toLowerCase());
            });

        marcaDropdown.innerHTML = '';

        if (filtered.length === 0) {
            marcaDropdown.innerHTML = '<div class="p-2 text-sm text-gray-500 dark:text-gray-400">No se encontraron coincidencias</div>';
        } else {
            filtered.forEach(marca => {
                const id = marca.id || marca.Id;
                const nombre = marca.nombre || marca.Nombre;
                if (!id || !nombre) return;

                const div = document.createElement('div');
                div.className = 'px-3 py-2 cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 text-sm text-gray-900 dark:text-white';
                div.textContent = nombre;
                div.dataset.marcaId = id;
                div.dataset.marcaNombre = nombre;

                div.addEventListener('click', function () {
                    selectMarca(id, nombre);
                });

                marcaDropdown.appendChild(div);
            });
        }

        // Posicionar antes de mostrar
        const marcaInput = document.getElementById('Marca');
        positionDropdown(marcaInput, marcaDropdown);

        marcaDropdown.classList.remove('hidden');
    }

    // ==================== FILTRADO DE MODELO ====================
    function filterModelos(searchText) {
        const modeloDropdown = document.getElementById('modelo-dropdown');
        if (!modeloDropdown || !modelosCache) return;

        const filtered = searchText.trim() === ''
            ? modelosCache
            : modelosCache.filter(m => {
                const nombre = (m.nombre || m.Nombre || '').toLowerCase();
                return nombre.includes(searchText.toLowerCase());
            });

        modeloDropdown.innerHTML = '';

        if (filtered.length === 0) {
            modeloDropdown.innerHTML = '<div class="p-2 text-sm text-gray-500 dark:text-gray-400">No se encontraron coincidencias</div>';
        } else {
            filtered.forEach(modelo => {
                const nombre = modelo.nombre || modelo.Nombre;
                if (!nombre) return;

                const div = document.createElement('div');
                div.className = 'px-3 py-2 cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 text-sm text-gray-900 dark:text-white';
                div.textContent = nombre;
                div.dataset.modeloNombre = nombre;

                div.addEventListener('click', function () {
                    selectModelo(nombre);
                });

                modeloDropdown.appendChild(div);
            });
        }

        // Posicionar antes de mostrar
        const modeloInput = document.getElementById('Modelo');
        positionDropdown(modeloInput, modeloDropdown);

        modeloDropdown.classList.remove('hidden');
    }

    // ==================== VALIDACIÓN DE MARCA ====================
    function validateMarca() {
        const marcaInput = document.getElementById('Marca');
        if (!marcaInput) return true;

        const valorActual = marcaInput.value.trim();

        if (!valorActual) {
            marcaSeleccionadaValida = null;
            return true;
        }

        // Verificar si el valor coincide con la marca seleccionada válida
        if (marcaSeleccionadaValida && marcaSeleccionadaValida.nombre === valorActual) {
            return true;
        }

        // Verificar si existe en el cache
        if (marcasCache) {
            const marcaEncontrada = marcasCache.find(m =>
                (m.nombre || m.Nombre) === valorActual
            );

            if (marcaEncontrada) {
                marcaSeleccionadaValida = {
                    id: marcaEncontrada.id || marcaEncontrada.Id,
                    nombre: marcaEncontrada.nombre || marcaEncontrada.Nombre
                };
                return true;
            }
        }

        // No es válida
        marcaInput.setCustomValidity('Debe seleccionar una marca de la lista');
        return false;
    }

    // ==================== VALIDACIÓN DE MODELO ====================
    function validateModelo() {
        const modeloInput = document.getElementById('Modelo');
        if (!modeloInput) return true;

        const valorActual = modeloInput.value.trim();

        if (!valorActual) {
            modeloSeleccionadoValido = null;
            return true;
        }

        // Verificar si el valor coincide con el modelo seleccionado válido
        if (modeloSeleccionadoValido === valorActual) {
            return true;
        }

        // Verificar si existe en el cache
        if (modelosCache) {
            const modeloEncontrado = modelosCache.find(m =>
                (m.nombre || m.Nombre) === valorActual
            );

            if (modeloEncontrado) {
                modeloSeleccionadoValido = modeloEncontrado.nombre || modeloEncontrado.Nombre;
                return true;
            }
        }

        // No es válido
        modeloInput.setCustomValidity('Debe seleccionar un modelo de la lista');
        return false;
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
                colorSelect.style.display = 'none';
                colorSelect.removeAttribute('required');
                colorSelect.removeAttribute('name');
                colorInput.style.display = 'block';
                colorInput.setAttribute('required', 'required');
                colorInput.setAttribute('name', 'Color');
                colorInput.focus();
            } else {
                colorInput.style.display = 'none';
                colorInput.removeAttribute('required');
                colorInput.removeAttribute('name');
                colorSelect.style.display = 'block';
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

    // ==================== CERRAR DROPDOWNS AL CLICK FUERA ====================
    document.addEventListener('click', function (e) {
        const marcaInput = document.getElementById('Marca');
        const marcaDropdown = document.getElementById('marca-dropdown');
        const modeloInput = document.getElementById('Modelo');
        const modeloDropdown = document.getElementById('modelo-dropdown');

        // Cerrar dropdown de marca si click está fuera
        if (marcaInput && marcaDropdown &&
            !marcaInput.contains(e.target) &&
            !marcaDropdown.contains(e.target)) {
            marcaDropdown.classList.add('hidden');
        }

        // Cerrar dropdown de modelo si click está fuera
        if (modeloInput && modeloDropdown &&
            !modeloInput.contains(e.target) &&
            !modeloDropdown.contains(e.target)) {
            modeloDropdown.classList.add('hidden');
        }
    });

    // Exponer funciones globalmente
    window.VehiculoApi = {
        init: initVehiculoApiSelects,
        loadMarcas,
        loadModelos,
        loadColores,
        resetInitialized: () => { initialized = false; }
    };

})();