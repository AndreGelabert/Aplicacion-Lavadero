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
            this.dataset.catalogueManual = 'false';
            this.dataset.marcaId = '';

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
            this.dataset.catalogueManual = 'false';

            // Resetear modelo válido si se modifica
            if (modeloSeleccionadoValido !== valor) {
                modeloSeleccionadoValido = null;
            }

            if (valor.length > 0 && modelosCache) {
                filterModelos(valor);
            } else if (modelosCache) {
                renderModeloOptions(modelosCache);
                modeloDropdown.classList.remove('hidden');
            } else if (isClientQuickVehicleForm()) {
                filterModelos(valor);
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
            } else if (isClientQuickVehicleForm()) {
                filterModelos(modeloInput.value.trim());
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

        if (modeloInput && isClientQuickVehicleForm() && (!modelos || modelos.length === 0)) {
            modeloInput.disabled = false;
            modeloInput.placeholder = 'Escriba un modelo para buscar o proponer...';
        }

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
            modeloInput.dataset.catalogueManual = 'false';
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
            marcaInput.dataset.marcaId = id;
            marcaInput.dataset.catalogueManual = 'false';
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
            modeloInput.dataset.catalogueManual = 'false';
        }

        if (modeloDropdown) {
            modeloDropdown.classList.add('hidden');
        }
    }

    // ==================== FILTRADO DE MARCA ====================
    function filterMarcas(searchText) {
        const marcaDropdown = document.getElementById('marca-dropdown');
        if (!marcaDropdown) return;
        const candidates = marcasCache || [];
        const query = searchText.trim();
        const clientQuickForm = isClientQuickVehicleForm();

        const filtered = query === ''
            ? candidates
            : candidates.filter(m => {
                const name = m.nombre || m.Nombre || '';
                return clientQuickForm
                    ? catalogueNameKey(name).includes(catalogueNameKey(query))
                    : name.toLowerCase().includes(query.toLowerCase());
            });

        marcaDropdown.innerHTML = '';

        const exactMatch = query && candidates.some(m => {
            const name = m.nombre || m.Nombre || '';
            return clientQuickForm ? catalogueNameKey(name) === catalogueNameKey(query) : name === query;
        });
        const suggestions = clientQuickForm && filtered.length === 0 && query
            ? findSimilarCatalogueNames(query, candidates.map(m => ({ id: m.id || m.Id, name: m.nombre || m.Nombre })))
            : [];

        if (filtered.length === 0 && suggestions.length === 0) {
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
            suggestions.forEach(marca => appendMarcaOption(marcaDropdown, marca, true));
        }
        if (query && !exactMatch && clientQuickForm)
            appendManualCatalogueOption(marcaDropdown, query, 'marca', () => confirmManualMarca(query));

        // Posicionar antes de mostrar
        const marcaInput = document.getElementById('Marca');
        positionDropdown(marcaInput, marcaDropdown);

        marcaDropdown.classList.remove('hidden');
    }

    // ==================== FILTRADO DE MODELO ====================
    function filterModelos(searchText) {
        const modeloDropdown = document.getElementById('modelo-dropdown');
        if (!modeloDropdown) return;
        const candidates = modelosCache || [];
        const query = searchText.trim();
        const clientQuickForm = isClientQuickVehicleForm();

        const filtered = query === ''
            ? candidates
            : candidates.filter(m => {
                const name = m.nombre || m.Nombre || '';
                return clientQuickForm
                    ? catalogueNameKey(name).includes(catalogueNameKey(query))
                    : name.toLowerCase().includes(query.toLowerCase());
            });

        modeloDropdown.innerHTML = '';

        const exactMatch = query && candidates.some(m => {
            const name = m.nombre || m.Nombre || '';
            return clientQuickForm ? catalogueNameKey(name) === catalogueNameKey(query) : name === query;
        });
        const suggestions = clientQuickForm && filtered.length === 0 && query
            ? findSimilarCatalogueNames(query, candidates.map(m => ({ id: '', name: m.nombre || m.Nombre })))
            : [];

        if (filtered.length === 0 && suggestions.length === 0) {
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
            suggestions.forEach(modelo => appendModeloOption(modeloDropdown, modelo.name, true));
        }
        if (query && !exactMatch && clientQuickForm)
            appendManualCatalogueOption(modeloDropdown, query, 'modelo', () => confirmManualModelo(query));

        // Posicionar antes de mostrar
        const modeloInput = document.getElementById('Modelo');
        positionDropdown(modeloInput, modeloDropdown);

        modeloDropdown.classList.remove('hidden');
    }

    function isClientQuickVehicleForm() {
        return Boolean(document.querySelector('#quick-create-modal #vehiculo-form'));
    }

    function appendMarcaOption(dropdown, marca, isSuggestion = false) {
        const id = marca.id || marca.Id || '';
        const nombre = marca.name || marca.nombre || marca.Nombre || '';
        if (!id || !nombre) return;
        const option = document.createElement('div');
        option.className = 'px-3 py-2 cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 text-sm text-gray-900 dark:text-white';
        option.textContent = isSuggestion ? `¿Quisiste decir “${nombre}”?` : nombre;
        option.addEventListener('click', () => selectMarca(id, nombre));
        dropdown.appendChild(option);
    }

    function appendModeloOption(dropdown, nombre, isSuggestion = false) {
        if (!nombre) return;
        const option = document.createElement('div');
        option.className = 'px-3 py-2 cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 text-sm text-gray-900 dark:text-white';
        option.textContent = isSuggestion ? `¿Quisiste decir “${nombre}”?` : nombre;
        option.addEventListener('click', () => selectModelo(nombre));
        dropdown.appendChild(option);
    }

    function appendCatalogueMessage(dropdown, message) {
        const item = document.createElement('div');
        item.className = 'p-2 text-sm text-gray-500 dark:text-gray-400';
        item.textContent = message;
        dropdown.appendChild(item);
    }

    function appendManualCatalogueOption(dropdown, value, kind, onSelect) {
        const item = document.createElement('button');
        item.type = 'button';
        item.className = 'block w-full border-t border-gray-200 dark:border-gray-600 px-3 py-2 text-left text-sm font-medium text-blue-700 hover:bg-blue-50 dark:text-blue-300 dark:hover:bg-gray-600';
        item.textContent = value.length <= 100
            ? `Ninguna coincide: usar “${value}” como ${kind} nueva`
            : 'El valor no puede superar los 100 caracteres';
        item.disabled = value.length > 100;
        item.addEventListener('click', onSelect);
        dropdown.appendChild(item);
    }

    function confirmManualMarca(value) {
        const input = document.getElementById('Marca');
        if (!input || input.value.trim() !== value || !value || value.length > 100) return;
        input.dataset.catalogueManual = 'true';
        input.dataset.marcaId = '';
        marcaSeleccionadaValida = null;
        input.setCustomValidity('');
        const dropdown = document.getElementById('marca-dropdown');
        dropdown?.classList.add('hidden');

        resetModeloSelect();
        modelosCache = [];
        const modeloInput = document.getElementById('Modelo');
        if (modeloInput) {
            modeloInput.disabled = false;
            modeloInput.placeholder = 'Seleccione o escriba un modelo...';
        }
    }

    function confirmManualModelo(value) {
        const input = document.getElementById('Modelo');
        if (!input || input.value.trim() !== value || !value || value.length > 100) return;
        input.dataset.catalogueManual = 'true';
        input.setCustomValidity('');
        document.getElementById('modelo-dropdown')?.classList.add('hidden');
    }

    function catalogueNameKey(value) {
        return (value || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '')
            .replace(/[^a-z0-9]/gi, '').toUpperCase();
    }

    function findSimilarCatalogueNames(input, candidates) {
        const left = catalogueNameKey(input);
        if (left.length < 3) return [];
        const digits = left.replace(/\D/g, '');
        const seen = new Set();
        return candidates.filter(item => item.name && catalogueNameKey(item.name).length >= 3 &&
                catalogueNameKey(item.name).replace(/\D/g, '') === digits)
            .map(item => {
                const right = catalogueNameKey(item.name);
                return { ...item, distance: catalogueEditDistance(left, right), length: Math.max(left.length, right.length) };
            })
            .filter(item => item.distance > 0 && item.distance <= Math.min(3, Math.floor(item.length / 4)))
            .sort((a, b) => (a.distance / a.length) - (b.distance / b.length) || a.name.localeCompare(b.name))
            .filter(item => {
                const key = item.name.toLowerCase();
                if (seen.has(key)) return false;
                seen.add(key);
                return true;
            })
            .slice(0, 5);
    }

    function catalogueEditDistance(left, right) {
        const distance = Array.from({ length: left.length + 1 }, () => Array(right.length + 1).fill(0));
        for (let i = 0; i <= left.length; i++) distance[i][0] = i;
        for (let j = 0; j <= right.length; j++) distance[0][j] = j;
        for (let i = 1; i <= left.length; i++) {
            for (let j = 1; j <= right.length; j++) {
                const cost = left[i - 1] === right[j - 1] ? 0 : 1;
                distance[i][j] = Math.min(distance[i - 1][j] + 1, distance[i][j - 1] + 1,
                    distance[i - 1][j - 1] + cost);
                if (i > 1 && j > 1 && left[i - 1] === right[j - 2] && left[i - 2] === right[j - 1])
                    distance[i][j] = Math.min(distance[i][j], distance[i - 2][j - 2] + 1);
            }
        }
        return distance[left.length][right.length];
    }

    // ==================== VALIDACIÓN DE MARCA ====================
    function validateMarca() {
        const marcaInput = document.getElementById('Marca');
        if (!marcaInput) return true;

        const valorActual = marcaInput.value.trim();

        if (isClientQuickVehicleForm() && marcaInput.dataset.catalogueManual === 'true' && valorActual.length <= 100)
            return true;

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
            const marcaEncontrada = marcasCache.find(m => {
                const nombre = m.nombre || m.Nombre || '';
                return isClientQuickVehicleForm()
                    ? catalogueNameKey(nombre) === catalogueNameKey(valorActual)
                    : nombre === valorActual;
            });

            if (marcaEncontrada) {
                const nombre = marcaEncontrada.nombre || marcaEncontrada.Nombre;
                marcaSeleccionadaValida = {
                    id: marcaEncontrada.id || marcaEncontrada.Id,
                    nombre
                };
                marcaInput.value = nombre;
                marcaInput.dataset.marcaId = marcaSeleccionadaValida.id;
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

        if (isClientQuickVehicleForm() && modeloInput.dataset.catalogueManual === 'true' && valorActual.length <= 100)
            return true;

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
            const modeloEncontrado = modelosCache.find(m => {
                const nombre = m.nombre || m.Nombre || '';
                return isClientQuickVehicleForm()
                    ? catalogueNameKey(nombre) === catalogueNameKey(valorActual)
                    : nombre === valorActual;
            });

            if (modeloEncontrado) {
                modeloSeleccionadoValido = modeloEncontrado.nombre || modeloEncontrado.Nombre;
                modeloInput.value = modeloSeleccionadoValido;
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
                renderColorOptions();
            }
        } catch (error) {
            console.warn('?? Error colores, usando fallback');
            renderColorOptions();
        }
    }

    function renderColorOptions(colorForzado = null) {
        const colorSelect = document.getElementById('Color');
        const colorInput = document.getElementById('ColorCustom');
        const toggle = document.getElementById('color-custom-toggle');
        
        if (!colorSelect) {
            console.warn('⚠️ No se encontró el select de colores');
            return;
        }
        // CRÍTICO: Leer el valor inicial del data-attribute (viene del servidor)
        const dataValue = colorSelect.getAttribute('data-initial-value');
        // Usar color forzado si se proporciona (para AJAX), sino leer del data-attribute
        const colorActualDelServidor = colorForzado || dataValue?.trim() || colorInput?.value?.trim() || '';
        colorSelect.innerHTML = '<option value="">Seleccione un color...</option>';

        // Usar coloresCache en lugar de colores
        if (coloresCache && coloresCache.length > 0) {
            coloresCache.forEach(color => {
                const option = document.createElement('option');
                option.value = color;
                option.textContent = color;
                colorSelect.appendChild(option);
            });
        } else {
            console.warn('⚠️ No hay colores cacheados, usando fallback');
            ['Blanco', 'Negro', 'Gris', 'Plata', 'Rojo', 'Azul'].forEach(color => {
                const option = document.createElement('option');
                option.value = color;
                option.textContent = color;
                colorSelect.appendChild(option);
            });
        }

        // CRÍTICO: Si hay un color del servidor, procesarlo
        if (colorActualDelServidor && colorActualDelServidor.length > 0) {
            // Buscar si el color actual existe en la lista de colores oficiales
            const esColorOficial = Array.from(colorSelect.options).some(
                opt => opt.value === colorActualDelServidor
            );         
            if (esColorOficial) {
                // Es un color oficial, seleccionarlo en el dropdown
                colorSelect.value = colorActualDelServidor;
            } else {
                // Es un color personalizado, activar el toggle
                if (toggle && colorInput) {
                    toggle.checked = true;
                    // Asignar el valor al input personalizado
                    colorInput.value = colorActualDelServidor;
                    // Mostrar input personalizado
                    colorSelect.style.display = 'none';
                    colorSelect.removeAttribute('required');
                    colorSelect.removeAttribute('name');
                    colorInput.style.display = 'block';
                    colorInput.setAttribute('required', 'required');
                    colorInput.setAttribute('name', 'Color');
                }
            }
        }
    }

    // ==================== TOGGLE COLOR ====================
    function setupColorToggle() {
        const toggle = document.getElementById('color-custom-toggle');
        const colorSelect = document.getElementById('Color');
        const colorInput = document.getElementById('ColorCustom');
        toggle.addEventListener('change', function () {
            if (this.checked) {
                // Activar modo personalizado
                colorSelect.style.display = 'none';
                colorSelect.removeAttribute('required');
                colorSelect.removeAttribute('name');
                colorInput.style.display = 'block';
                colorInput.setAttribute('required', 'required');
                colorInput.setAttribute('name', 'Color');
                // Si había un valor seleccionado en el dropdown, transferirlo
                if (colorSelect.value) {
                    colorInput.value = colorSelect.value;
                }
                colorInput.focus();
            } else {
                // Volver a modo dropdown
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

    // ==================== INICIALIZACIÓN DE COLOR DESPUÉS DE AJAX ====================
    function initColorAfterAjax(colorDelServidor) {
        // Simular que tenemos el color del servidor
        renderColorOptions(colorDelServidor);
    }

    // ==================== EXPONER FUNCIONES ====================
    window.VehiculoApi = {
        init: initVehiculoApiSelects,
        loadMarcas,
        loadModelos,
        loadColores,
        resetInitialized: () => { initialized = false; },
        initColor: initColorAfterAjax,
        coloresCache: coloresCache  // Exponer para verificar si está cargado
    };

})();
