/**
 * ================================================
 * CONFIGURACION.JS - FUNCIONALIDAD DE HORARIOS DEL LAVADERO
 * ================================================
 */

(function () {
    'use strict';

    // Estado global del modal
    let modoEdicion = 'todos'; // 'todos', 'rango', 'individual'
    let diasSeleccionados = [];
    let diaIndividual = null;

    // Cache para guardar horarios previos antes de cerrar
    let horariosAnteriores = {};

    // =====================================
    // INICIALIZACIÓN DEL MÓDULO
    // =====================================
    window.PageModules = window.PageModules || {};
    window.PageModules.configuracion = {
        init: initializeConfiguracionPage
    };

    /**
     * Inicializa la funcionalidad específica de la página de Configuración
     */
    function initializeConfiguracionPage() {
        inicializarCacheHorarios();
        inicializarCheckboxesDias();
        setupModals();
    }

    // Inicializar cuando el DOM esté listo
    document.addEventListener('DOMContentLoaded', () => {
        try {
            window.PageModules?.configuracion?.init();
        } catch (e) {
            initializeConfiguracionPage();
        }
    });

    // =====================================
    // CACHE DE HORARIOS
    // =====================================
    /**
     * Inicializa el cache con los horarios actuales
     */
    function inicializarCacheHorarios() {
        const dias = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
        dias.forEach(dia => {
            const inputHidden = document.getElementById(`horario-${dia}`);
            if (inputHidden && inputHidden.value !== 'CERRADO') {
                horariosAnteriores[dia] = inputHidden.value;
            }
        });
    }

    /**
     * Guarda el horario actual antes de cerrarlo
     * @param {string} dia - Día de la semana
     * @param {string} horario - Horario a guardar
     */
    function guardarHorarioAnterior(dia, horario) {
        if (horario !== 'CERRADO') {
            horariosAnteriores[dia] = horario;
        }
    }

    /**
     * Obtiene el horario anterior de un día o el del día anterior si no existe
     * @param {string} dia - Día de la semana
     * @returns {string} Horario anterior o por defecto
     */
    function obtenerHorarioAnteriorOPrevio(dia) {
        // Intentar obtener el horario anterior guardado
        if (horariosAnteriores[dia]) {
            return horariosAnteriores[dia];
        }

        // Si no existe, buscar el día anterior con horario
        const dias = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
        const indexActual = dias.indexOf(dia);

        // Buscar hacia atrás
        for (let i = indexActual - 1; i >= 0; i--) {
            const horarioPrevio = horariosAnteriores[dias[i]];
            if (horarioPrevio && horarioPrevio !== 'CERRADO') {
                return horarioPrevio;
            }
        }

        // Si no encontró hacia atrás, buscar hacia adelante
        for (let i = indexActual + 1; i < dias.length; i++) {
            const horarioPrevio = horariosAnteriores[dias[i]];
            if (horarioPrevio && horarioPrevio !== 'CERRADO') {
                return horarioPrevio;
            }
        }

        // Por defecto, retornar un horario estándar
        return '09:00-18:00';
    }

    // =====================================
    // CHECKBOXES DE DÍAS
    // =====================================
    /**
     * Inicializa los checkboxes de días de la semana con actualización en tiempo real
     */
    function inicializarCheckboxesDias() {
        const checkboxes = document.querySelectorAll('.dia-checkbox');
        checkboxes.forEach(checkbox => {
            // Remover listeners anteriores
            const newCheckbox = checkbox.cloneNode(true);
            checkbox.parentNode.replaceChild(newCheckbox, checkbox);

            newCheckbox.addEventListener('change', function () {
                const dia = this.dataset.dia;
                const estaAbierto = this.checked;
                const inputHidden = document.getElementById(`horario-${dia}`);

                if (!estaAbierto) {
                    // Guardar el horario actual antes de cerrarlo
                    const horarioActual = inputHidden?.value;
                    if (horarioActual && horarioActual !== 'CERRADO') {
                        guardarHorarioAnterior(dia, horarioActual);
                    }

                    // Actualizar a CERRADO
                    actualizarHorarioHidden(dia, 'CERRADO');
                    actualizarResumenHorario(dia, 'CERRADO');
                } else {
                    // Restaurar el horario anterior o copiar del día previo
                    const horarioRestaurado = obtenerHorarioAnteriorOPrevio(dia);
                    actualizarHorarioHidden(dia, horarioRestaurado);
                    actualizarResumenHorario(dia, horarioRestaurado);
                }
            });
        });
    }

    /**
     * Actualiza el input hidden de un día
     * @param {string} dia - Día de la semana
     * @param {string} horario - Horario a actualizar
     */
    function actualizarHorarioHidden(dia, horario) {
        const inputHidden = document.getElementById(`horario-${dia}`);
        if (inputHidden) {
            inputHidden.value = horario;
        }
    }

    // =====================================
    // GESTIÓN DE MODALES
    // =====================================
    /**
     * Configura todos los modales y eventos
     */
    function setupModals() {
        // Configurar botones que abren modales (usando delegación de eventos)
        document.addEventListener('click', (e) => {
            // Botón "Editar todos los horarios"
            if (e.target.closest('[onclick*="abrirModalHorarios(\'todos\')"]')) {
                e.preventDefault();
                e.stopPropagation();
                abrirModalHorarios('todos');
                return;
            }

            // Botón "Editar rango de días"
            if (e.target.closest('[onclick*="abrirModalHorarios(\'rango\')"]')) {
                e.preventDefault();
                e.stopPropagation();
                abrirModalHorarios('rango');
                return;
            }

            // Botón de editar día individual
            const btnIndividual = e.target.closest('[onclick*="abrirModalHorarios(\'individual\'"]');
            if (btnIndividual) {
                e.preventDefault();
                e.stopPropagation();
                const match = btnIndividual.getAttribute('onclick').match(/abrirModalHorarios\('individual',\s*'([^']+)'\)/);
                if (match) {
                    abrirModalHorarios('individual', match[1]);
                }
                return;
            }
        });

        // Inicializar Flowbite modals
        if (typeof initModals === 'function') {
            initModals();
        }

        // Configurar botones de cierre
        addModalCloseHandlers('modalHorarios');
    }

    function addModalCloseHandlers(modalId) {
        const closeBtns = document.querySelectorAll(`[data-modal-hide="${modalId}"], [onclick*="cerrarModalHorarios"]`);
        closeBtns.forEach(btn => {
            if (btn.hasAttribute('data-close-setup')) return;

            // Remover onclick inline
            btn.removeAttribute('onclick');

            btn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                cerrarModal(modalId);
            });
            btn.setAttribute('data-close-setup', 'true');
        });
    }

    /**
     * Abre el modal de edición de horarios
     * @param {string} modo - Modo de edición ('todos', 'rango', 'individual')
     * @param {string} dia - Día individual (opcional)
     */
    function abrirModalHorarios(modo, dia = null) {
        modoEdicion = modo;
        diaIndividual = dia;
        diasSeleccionados = [];

        const modal = document.getElementById('modalHorarios');
        if (!modal) return;

        const selectorRango = document.getElementById('selector-dias-rango');
        const selectorModal = document.getElementById('selector-dias-modal');
        const tituloModal = modal.querySelector('h3');
        const checkCerrado = document.getElementById('cerrado');

        // Configurar según el modo
        switch (modo) {
            case 'todos':
                tituloModal.textContent = 'Editar todos los horarios';
                selectorRango.classList.add('hidden');
                selectorModal.classList.remove('hidden');

                // OCULTAR checkbox de cerrado en modo "todos"
                if (checkCerrado) {
                    checkCerrado.closest('.flex')?.classList.add('hidden');
                }

                // Seleccionar todos los días
                const todosBotones = document.querySelectorAll('.dia-modal-btn');
                todosBotones.forEach(btn => {
                    btn.classList.add('seleccionado');
                    diasSeleccionados.push(btn.dataset.dia);
                });

                // Cargar horarios comunes si existen
                cargarHorariosComunesOVacio();
                break;

            case 'rango':
                tituloModal.textContent = 'Editar rango de días';
                selectorRango.classList.remove('hidden');
                selectorModal.classList.add('hidden');

                if (checkCerrado) {
                    checkCerrado.closest('.flex')?.classList.remove('hidden');
                }

                // ✅ NUEVO: Cargar horarios del rango inicial
                const diaInicio = document.getElementById('dia-inicio');
                const diaFin = document.getElementById('dia-fin');
                if (diaInicio && diaFin) {
                    cargarHorariosRango(diaInicio.value, diaFin.value);
                }
                break;

            case 'individual':
                tituloModal.textContent = `Editar horario de ${dia}`;
                selectorRango.classList.add('hidden');
                selectorModal.classList.add('hidden');

                // MOSTRAR checkbox de cerrado en modo individual
                if (checkCerrado) {
                    checkCerrado.closest('.flex')?.classList.remove('hidden');
                }

                diasSeleccionados = [dia];
                cargarHorarioActual(dia);
                break;
        }

        // Configurar eventos de botones del modal
        configurarEventosModal();

        // Abrir modal usando Flowbite
        try {
            const inst = getFlowbiteModal(modal);
            if (inst && typeof inst.show === 'function') {
                inst.show();
                return;
            }
        } catch (e) {
            console.log('Flowbite no disponible, usando fallback');
        }

        // Fallback
        modal.classList.remove('hidden');
        modal.setAttribute('aria-hidden', 'false');
    }

    /**
     * Carga horarios comunes de todos los días o muestra --:-- si no coinciden
     */
    function cargarHorariosComunesOVacio() {
        const dias = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
        const horarios = [];

        // Obtener todos los horarios actuales
        dias.forEach(dia => {
            const inputHidden = document.getElementById(`horario-${dia}`);
            if (inputHidden) {
                horarios.push(inputHidden.value);
            }
        });

        // Verificar si todos los horarios son iguales
        const primerHorario = horarios[0];
        const todosSonIguales = horarios.every(h => h === primerHorario);

        const contenedor = document.getElementById('contenedor-horarios');
        if (!contenedor) return;

        if (todosSonIguales && primerHorario !== 'CERRADO') {
            // Si todos son iguales, cargar ese horario
            contenedor.innerHTML = '';

            const rangos = primerHorario.split(',');
            rangos.forEach((rango) => {
                const [apertura, cierre] = rango.trim().split('-');
                agregarSlotHorario(contenedor, apertura, cierre);
            });
        } else {
            // Si no son iguales, mostrar --:-- como placeholder
            contenedor.innerHTML = '';
            agregarSlotHorario(contenedor, '', '');
        }

        actualizarEstadoBotonesEliminar();
    }

    /**
     * Agrega un slot de horario al contenedor
     * @param {HTMLElement} contenedor - Contenedor de horarios
     * @param {string} apertura - Hora de apertura
     * @param {string} cierre - Hora de cierre
     */
    function agregarSlotHorario(contenedor, apertura, cierre) {
        const nuevoSlot = document.createElement('div');
        nuevoSlot.className = 'horario-slot flex gap-3 items-end mb-3';

        const aperturaValue = apertura || '';
        const cierreValue = cierre || '';
        const placeholderApertura = apertura ? '' : '--:--';
        const placeholderCierre = cierre ? '' : '--:--';

        nuevoSlot.innerHTML = `
            <div class="flex-1">
                <label class="block mb-1.5 text-xs font-medium text-gray-700 dark:text-gray-300">Apertura</label>
                <input type="time" class="horario-apertura bg-gray-50 border border-gray-300 text-gray-900 text-sm rounded-lg focus:ring-blue-500 focus:border-blue-500 block w-full p-2.5 dark:bg-gray-700 dark:border-gray-600 dark:text-white" value="${aperturaValue}" placeholder="${placeholderApertura}">
            </div>
            <div class="flex-1">
                <label class="block mb-1.5 text-xs font-medium text-gray-700 dark:text-gray-300">Cierre</label>
                <input type="time" class="horario-cierre bg-gray-50 border border-gray-300 text-gray-900 text-sm rounded-lg focus:ring-blue-500 focus:border-blue-500 block w-full p-2.5 dark:bg-gray-700 dark:border-gray-600 dark:text-white" value="${cierreValue}" placeholder="${placeholderCierre}">
            </div>
            <button type="button" class="btn-eliminar-horario text-red-600 hover:text-red-800 hover:bg-red-50 dark:text-red-400 dark:hover:text-red-300 dark:hover:bg-red-900/20 p-2.5 rounded-lg transition-colores">
                <svg class="w-5 h-5" fill="currentColor" viewBox="0 0 20 20">
                    <path fill-rule="evenodd" d="M9 2a1 1 0 00-.894.553L7.382 4H4a1 1 0 000 2v10a2 2 0 002 2h8a2 2 0 002-2V6a1 1 0 100-2h-3.382l-.724-1.447A1 1 0 0011 2H9zM7 8a1 1 0 012 0v6a1 1 0 11-2 0V8zm5-1a1 1 0 00-1 1v6a1 1 0 102 0V8a1 1 0 00-1-1z" clip-rule="evenodd"></path>
                </svg>
            </button>
        `;

        const btnEliminar = nuevoSlot.querySelector('.btn-eliminar-horario');
        btnEliminar.addEventListener('click', function (e) {
            e.preventDefault();
            eliminarHorario(this);
        });

        contenedor.appendChild(nuevoSlot);
    }

    /**
     * Configura los eventos dentro del modal
     */
    function configurarEventosModal() {
        // Botones de días del modal
        const botonesDias = document.querySelectorAll('.dia-modal-btn');
        botonesDias.forEach(boton => {
            // Remover listeners anteriores
            const newBoton = boton.cloneNode(true);
            boton.parentNode.replaceChild(newBoton, boton);

            newBoton.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();

                if (modoEdicion === 'todos') return;

                const dia = this.dataset.dia;
                this.classList.toggle('seleccionado');

                if (this.classList.contains('seleccionado')) {
                    if (!diasSeleccionados.includes(dia)) {
                        diasSeleccionados.push(dia);
                    }
                } else {
                    diasSeleccionados = diasSeleccionados.filter(d => d !== dia);
                }
            });
        });

        // ✅ NUEVO: Selectores de rango (día inicio y día fin)
        const diaInicio = document.getElementById('dia-inicio');
        const diaFin = document.getElementById('dia-fin');

        if (diaInicio && diaFin && modoEdicion === 'rango') {
            // Remover listeners anteriores
            const newDiaInicio = diaInicio.cloneNode(true);
            const newDiaFin = diaFin.cloneNode(true);

            // Copiar todas las opciones seleccionadas
            Array.from(diaInicio.options).forEach((option, index) => {
                newDiaInicio.options[index].selected = option.selected;
            });
            Array.from(diaFin.options).forEach((option, index) => {
                newDiaFin.options[index].selected = option.selected;
            });

            diaInicio.parentNode.replaceChild(newDiaInicio, diaInicio);
            diaFin.parentNode.replaceChild(newDiaFin, diaFin);

            // Agregar eventos para recargar horarios cuando cambie el rango
            newDiaInicio.addEventListener('change', function () {
                cargarHorariosRango(this.value, newDiaFin.value);
            });

            newDiaFin.addEventListener('change', function () {
                cargarHorariosRango(newDiaInicio.value, this.value);
            });
        }

        // Checkbox de cerrado
        const checkCerrado = document.getElementById('cerrado');
        const contenedorHorarios = document.getElementById('contenedor-horarios');

        if (checkCerrado) {
            // Remover listener anterior
            const newCheck = checkCerrado.cloneNode(true);
            checkCerrado.parentNode.replaceChild(newCheck, checkCerrado);

            newCheck.addEventListener('change', function () {
                if (this.checked) {
                    contenedorHorarios.classList.add('opacity-50', 'pointer-events-none');
                } else {
                    contenedorHorarios.classList.remove('opacity-50', 'pointer-events-none');
                }
            });
        }

        // Botón agregar horario
        const btnAgregar = document.querySelector('[onclick*="agregarHorario"]');
        if (btnAgregar) {
            const newBtn = btnAgregar.cloneNode(true);
            btnAgregar.parentNode.replaceChild(newBtn, btnAgregar);
            newBtn.removeAttribute('onclick');

            newBtn.addEventListener('click', (e) => {
                e.preventDefault();
                agregarHorario();
            });
        }

        // Botón guardar
        const btnGuardar = document.querySelector('[onclick*="guardarHorarios"]');
        if (btnGuardar) {
            const newBtn = btnGuardar.cloneNode(true);
            btnGuardar.parentNode.replaceChild(newBtn, btnGuardar);
            newBtn.removeAttribute('onclick');

            newBtn.addEventListener('click', (e) => {
                e.preventDefault();
                guardarHorarios();
            });
        }
    }

    /**
     * Cierra el modal de horarios
     * @param {string} modalId - ID del modal
     */
    function cerrarModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        let closed = false;

        // Intentar cerrar con Flowbite
        try {
            const inst = getFlowbiteModal(modal);
            if (inst && typeof inst.hide === 'function') {
                inst.hide();
                closed = true;
            }
        } catch (e) {
            console.log('Error cerrando con Flowbite:', e);
        }

        // Intentar cerrar backdrop
        try {
            const backdrop = document.querySelector('[modal-backdrop]');
            if (backdrop) {
                backdrop.click();
                closed = true;
            }
        } catch (e) {
            console.log('Error con backdrop:', e);
        }

        // Fallback
        modal.classList.add('hidden');
        modal.setAttribute('aria-hidden', 'true');

        // Limpiar estado
        diasSeleccionados = [];
        diaIndividual = null;
        limpiarContenedorHorarios();

        // Limpieza de overlays y scroll
        document.querySelectorAll('[modal-backdrop]').forEach(b => b.remove());
        document.body.classList.remove('overflow-hidden');
    }

    // Helper: obtiene la instancia Flowbite del modal
    function getFlowbiteModal(modalEl) {
        if (!modalEl || typeof window !== 'object' || typeof window.Modal === 'undefined') return null;

        // 🔒 NUEVO: backdrop 'static' para que NO se cierre clickeando fuera
        const opts = { backdrop: 'static', closable: false };

        if (typeof Modal.getInstance === 'function') {
            const existing = Modal.getInstance(modalEl);
            if (existing) return existing;
        }
        if (typeof Modal.getOrCreateInstance === 'function') {
            return Modal.getOrCreateInstance(modalEl, opts);
        }
        try {
            return new Modal(modalEl, opts);
        } catch {
            return null;
        }
    }

    // =====================================
    // GESTIÓN DE HORARIOS
    // =====================================
    /**
     * Agrega un nuevo slot de horario
     */
    function agregarHorario() {
        const contenedor = document.getElementById('contenedor-horarios');
        if (!contenedor) return;

        agregarSlotHorario(contenedor, '09:00', '18:00');
        actualizarEstadoBotonesEliminar();
    }

    /**
     * Elimina un slot de horario
     * @param {HTMLElement} boton - Botón de eliminar
     */
    function eliminarHorario(boton) {
        const slot = boton.closest('.horario-slot');
        if (slot) {
            slot.remove();
            actualizarEstadoBotonesEliminar();
        }
    }

    /**
     * Actualiza el estado de los botones de eliminar
     */
    function actualizarEstadoBotonesEliminar() {
        const contenedor = document.getElementById('contenedor-horarios');
        if (!contenedor) return;

        const slots = contenedor.querySelectorAll('.horario-slot');
        const botones = contenedor.querySelectorAll('.btn-eliminar-horario');

        botones.forEach(boton => {
            if (slots.length === 1) {
                boton.disabled = true;
                boton.classList.add('opacity-50', 'cursor-not-allowed');
            } else {
                boton.disabled = false;
                boton.classList.remove('opacity-50', 'cursor-not-allowed');
            }
        });
    }

    /**
     * Limpia el contenedor de horarios
     */
    function limpiarContenedorHorarios() {
        const contenedor = document.getElementById('contenedor-horarios');
        if (!contenedor) return;

        contenedor.innerHTML = '';
        agregarSlotHorario(contenedor, '09:00', '18:00');

        // Resetear checkbox de cerrado
        const checkCerrado = document.getElementById('cerrado');
        if (checkCerrado) {
            checkCerrado.checked = false;
        }

        contenedor.classList.remove('opacity-50', 'pointer-events-none');
    }

    /**
     * Carga el horario actual de un día específico
     * @param {string} dia - Día de la semana
     */
    function cargarHorarioActual(dia) {
        const inputHidden = document.getElementById(`horario-${dia}`);
        if (!inputHidden) return;

        const horarioActual = inputHidden.value;
        const contenedor = document.getElementById('contenedor-horarios');
        const checkCerrado = document.getElementById('cerrado');

        if (horarioActual === 'CERRADO') {
            if (checkCerrado) checkCerrado.checked = true;
            contenedor.classList.add('opacity-50', 'pointer-events-none');
        } else {
            // Parsear horarios existentes
            contenedor.innerHTML = '';

            const rangos = horarioActual.split(',');
            rangos.forEach((rango) => {
                const [apertura, cierre] = rango.trim().split('-');
                agregarSlotHorario(contenedor, apertura, cierre);
            });

            actualizarEstadoBotonesEliminar();
        }
    }

    /**
     * Guarda los horarios editados
     */
    function guardarHorarios() {
        let diasAActualizar = [];

        // Determinar qué días actualizar según el modo
        if (modoEdicion === 'rango') {
            const diaInicio = document.getElementById('dia-inicio')?.value;
            const diaFin = document.getElementById('dia-fin')?.value;
            if (!diaInicio || !diaFin) {
                alert('Por favor, seleccione el rango de días.');
                return;
            }
            diasAActualizar = obtenerDiasEnRango(diaInicio, diaFin);
        } else {
            diasAActualizar = diasSeleccionados;
        }

        if (diasAActualizar.length === 0) {
            alert('Por favor, seleccione al menos un día.');
            return;
        }

        // Obtener el horario configurado
        let horario = '';

        const checkCerrado = document.getElementById('cerrado');
        if (checkCerrado && checkCerrado.checked) {
            horario = 'CERRADO';
        } else {
            // Construir el horario desde los slots
            const slots = document.querySelectorAll('.horario-slot');
            const rangos = [];

            let errorValidacion = false;
            slots.forEach(slot => {
                const apertura = slot.querySelector('.horario-apertura')?.value;
                const cierre = slot.querySelector('.horario-cierre')?.value;

                if (apertura && cierre) {
                    // Validar que cierre sea después de apertura
                    if (cierre <= apertura) {
                        alert('La hora de cierre debe ser posterior a la hora de apertura.');
                        errorValidacion = true;
                        return;
                    }
                    rangos.push(`${apertura}-${cierre}`);
                }
            });

            if (errorValidacion) return;

            if (rangos.length === 0) {
                alert('Por favor, configure al menos un horario.');
                return;
            }

            horario = rangos.join(',');
        }

        // Actualizar todos los días seleccionados
        diasAActualizar.forEach(dia => {
            // Guardar en cache antes de actualizar
            if (horario !== 'CERRADO') {
                guardarHorarioAnterior(dia, horario);
            }

            // Actualizar el hidden input
            actualizarHorarioHidden(dia, horario);

            // Actualizar el checkbox
            const checkbox = document.querySelector(`.dia-checkbox[data-dia="${dia}"]`);
            if (checkbox) {
                checkbox.checked = horario !== 'CERRADO';
            }

            // Actualizar el resumen
            actualizarResumenHorario(dia, horario);
        });

        cerrarModal('modalHorarios');
    }

    /**
     * Obtiene un array de días entre dos días de la semana
     * @param {string} diaInicio - Día de inicio
     * @param {string} diaFin - Día de fin
     * @returns {string[]} Array de días
     */
    function obtenerDiasEnRango(diaInicio, diaFin) {
        const diasSemana = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
        const indexInicio = diasSemana.indexOf(diaInicio);
        const indexFin = diasSemana.indexOf(diaFin);

        if (indexInicio === -1 || indexFin === -1) return [];

        const resultado = [];
        if (indexInicio <= indexFin) {
            for (let i = indexInicio; i <= indexFin; i++) {
                resultado.push(diasSemana[i]);
            }
        } else {
            // Caso especial: el rango cruza la semana (ej: Sábado a Lunes)
            for (let i = indexInicio; i < diasSemana.length; i++) {
                resultado.push(diasSemana[i]);
            }
            for (let i = 0; i <= indexFin; i++) {
                resultado.push(diasSemana[i]);
            }
        }

        return resultado;
    }

    /**
     * Actualiza el resumen visual de horarios
     * @param {string} dia - Día de la semana
     * @param {string} horario - Horario a mostrar
     */
    function actualizarResumenHorario(dia, horario) {
        // Buscar el div del resumen que contiene este día
        const resumenContainer = document.getElementById('resumen-horarios');
        if (!resumenContainer) return;

        const todosLosDivs = resumenContainer.querySelectorAll('.flex.items-center');
        todosLosDivs.forEach(divHorario => {
            const nombreDia = divHorario.querySelector('span:first-child')?.textContent.trim();
            if (nombreDia === dia) {
                const spanHorario = divHorario.querySelector('span:nth-child(2)');
                if (spanHorario) {
                    spanHorario.textContent = horario;

                    // Actualizar clases según el estado
                    if (horario === 'CERRADO') {
                        spanHorario.classList.add('text-red-600', 'dark:text-red-400', 'font-medium');
                        spanHorario.classList.remove('text-gray-600', 'dark:text-gray-400');
                    } else {
                        spanHorario.classList.remove('text-red-600', 'dark:text-red-400', 'font-medium');
                        spanHorario.classList.add('text-gray-600', 'dark:text-gray-400');
                    }
                }
            }
        });
    }

    /**
     * Carga horarios del rango seleccionado
     * @param {string} diaInicio - Día de inicio
     * @param {string} diaFin - Día de fin
     */
    function cargarHorariosRango(diaInicio, diaFin) {
        const diasDelRango = obtenerDiasEnRango(diaInicio, diaFin);
        if (diasDelRango.length === 0) return;

        const horarios = [];

        // Obtener horarios del rango
        diasDelRango.forEach(dia => {
            const inputHidden = document.getElementById(`horario-${dia}`);
            if (inputHidden) {
                horarios.push(inputHidden.value);
            }
        });

        // Verificar si todos los horarios del rango son iguales
        const primerHorario = horarios[0];
        const todosSonIguales = horarios.every(h => h === primerHorario);

        const contenedor = document.getElementById('contenedor-horarios');
        if (!contenedor) return;

        if (todosSonIguales && primerHorario !== 'CERRADO') {
            // Si todos son iguales, cargar ese horario
            contenedor.innerHTML = '';

            const rangos = primerHorario.split(',');
            rangos.forEach((rango) => {
                const [apertura, cierre] = rango.trim().split('-');
                agregarSlotHorario(contenedor, apertura, cierre);
            });
        } else {
            // Si no son iguales o están cerrados, mostrar --:--
            contenedor.innerHTML = '';
            agregarSlotHorario(contenedor, '', '');
        }

        actualizarEstadoBotonesEliminar();
    }
    // =====================================
    // FUNCIONES GLOBALES (compatibilidad)
    // =====================================
    window.abrirModalHorarios = abrirModalHorarios;
    window.cerrarModalHorarios = () => cerrarModal('modalHorarios');
    window.agregarHorario = agregarHorario;
    window.guardarHorarios = guardarHorarios;

})();
