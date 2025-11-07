/**
 * ================================================
 * PAQUETESERVICIO.JS - FUNCIONALIDAD DE LA PÁGINA DE PAQUETES DE SERVICIOS
 * ================================================
 */

(function () {
 'use strict';

 let paqueteMsgTimeout = null;
 let tableMsgTimeout = null;
 let searchTimeout = null;
 let currentSearchTerm = '';
 let serviciosDisponibles = [];
 let serviciosSeleccionados = [];
 let servicioSeleccionadoDropdown = null;

 // =====================================
 // INICIALIZACIÓN DEL MÓDULO
 // =====================================
 window.PageModules = window.PageModules || {};
 window.PageModules.paquetes = {
 init: initializePaquetesPage
 };

 /**
 * Inicializa la funcionalidad específica de la página de Paquetes
 */
 function initializePaquetesPage() {
 setupFormMessageHandler();
 setupSearchWithDebounce();
 window.CommonUtils?.setupDefaultFilterForm();
 setupDynamicPriceHints();
 setupServiciosCantidadListener();
 checkEditMode();
 initializeForm();
 setupDropdownClickOutside();
 }

 // IMPORTANTE: asegurar que init se ejecute siempre
 document.addEventListener('DOMContentLoaded', () => {
 try {
 window.PageModules?.paquetes?.init();
 } catch (e) {
 initializePaquetesPage();
 }
 });

 // Nuevos listeners para selects de cantidad servicios
 function setupServiciosCantidadListener() {
 const sel = document.getElementById('serviciosCantidad');
 if (sel) {
 sel.addEventListener('change', () => reloadPaqueteTable(1));
 }
 }

 // =====================================
 // AYUDAS DINÁMICAS PARA PRECIO
 // =====================================
 function setupDynamicPriceHints() {
 const form = document.getElementById('filterForm');
 if (!form) return;

 const precioMinEl = document.getElementById('precioMin');
 const precioMaxEl = document.getElementById('precioMax');
 const helpMin = document.getElementById('precioMin-help');
 const helpMax = document.getElementById('precioMax-help');

 async function refreshPriceRangeHints() {
 try {
 const params = new URLSearchParams();
 // Copiar filtros excepto precio
 const fd = new FormData(form);
 for (const [k, v] of fd.entries()) {
 if (k === 'precioMin' || k === 'precioMax' || k === 'pageNumber') continue;
 params.append(k, v);
 }
 // incluir searchTerm si existe
 if (currentSearchTerm) params.append('searchTerm', currentSearchTerm);

 const url = `/PaqueteServicio/PriceRange?${params.toString()}`;
 const resp = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
 const data = await resp.json();
 if (data?.success) {
 const fmt = (n) => typeof n === 'number' ? n.toFixed(2) : (typeof n === 'string' && n ? parseFloat(n).toFixed(2) : null);
 if (helpMin) helpMin.textContent = (data.min != null) ? `Mín. permitido: $${fmt(data.min)}` : '';
 if (helpMax) helpMax.textContent = (data.max != null) ? `Máx. permitido: $${fmt(data.max)}` : '';
 // También setear atributos min/max como sugerencia (no bloquea intervalo abierto, ya que se puede limpiar un campo)
 if (precioMinEl && data.min != null) precioMinEl.setAttribute('min', parseFloat(data.min)); else precioMinEl?.removeAttribute('min');
 if (precioMaxEl && data.max != null) precioMaxEl.setAttribute('max', parseFloat(data.max)); else precioMaxEl?.removeAttribute('max');
 } else {
 if (helpMin) helpMin.textContent = '';
 if (helpMax) helpMax.textContent = '';
 precioMinEl?.removeAttribute('min');
 precioMaxEl?.removeAttribute('max');
 }
 } catch {
 if (helpMin) helpMin.textContent = '';
 if (helpMax) helpMax.textContent = '';
 precioMinEl?.removeAttribute('min');
 precioMaxEl?.removeAttribute('max');
 }
 }

 // Disparar al abrir la página y cuando cambien filtros que afectan al rango
 refreshPriceRangeHints();

 const watchedSelectors = [
 'input[name="estados"]',
 'input[name="tiposVehiculo"]',
 '#descuentoMin', '#descuentoMax',
 '#serviciosCantidad'
 ];

 watchedSelectors.forEach(sel => {
 document.querySelectorAll(sel).forEach(el => {
 el.addEventListener('change', () => refreshPriceRangeHints());
 el.addEventListener('input', () => refreshPriceRangeHints());
 });
 });

 // También actualizar cuando cambia el término de búsqueda
 const search = document.getElementById('simple-search');
 if (search) {
 search.addEventListener('input', () => {
 // Se usará debounced search, pero las ayudas pueden ir actualizándose
 setTimeout(refreshPriceRangeHints,550);
 });
 }
 }

 // =====================================
 // CONFIGURACIÓN INICIAL
 // =====================================
 /**
 * Verifica si estamos editando para abrir el acordeón
 */
 function checkEditMode() {
 const formTitle = document.getElementById('form-title');
 if (formTitle && formTitle.textContent.includes('Editando')) {
 const accordion = document.getElementById('accordion-flush-body-1');
 if (accordion) accordion.classList.remove('hidden');
 }
 }

 /**
 * Inicializa el formulario y carga datos si está en modo edición
 */
 function initializeForm() {
 if (window.paqueteEditData && window.paqueteEditData.serviciosIds) {
 const tipoVehiculo = document.getElementById('TipoVehiculo')?.value;
 if (tipoVehiculo) {
 loadServiciosPorTipoVehiculo().then(() => {
 // Cargar servicios seleccionados en modo edición
 window.paqueteEditData.serviciosIds.forEach(id => {
 const servicio = serviciosDisponibles.find(s => s.id === id);
 if (servicio) {
 serviciosSeleccionados.push({
 id: servicio.id,
 nombre: servicio.nombre,
 tipo: servicio.tipo,
 precio: servicio.precio,
 tiempoEstimado: servicio.tiempoEstimado
 });
 }
 });
 updateServiciosSeleccionadosList();
 updateResumen();
 });
 }
 }
 }

 /**
 * Configura el manejo de mensajes del formulario
 */
 function setupFormMessageHandler() {
 document.addEventListener('input', (e) => {
 if (e.target.closest('#paquete-form')) {
 hidePaqueteMessage();
 }
 });
 }

 /**
 * Configura cierre del dropdown al hacer clic fuera
 */
 function setupDropdownClickOutside() {
 document.addEventListener('click', (e) => {
 const dropdown = document.getElementById('servicio-dropdown');
 const searchInput = document.getElementById('servicio-search');
 
 if (dropdown && !dropdown.contains(e.target) && e.target !== searchInput) {
 dropdown.classList.add('hidden');
 }
 });
 }

 // =====================================
 // BÚSQUEDA DE LA TABLA
 // =====================================
 /**
 * Configura la búsqueda con debouncing
 */
 function setupSearchWithDebounce() {
 const searchInput = document.getElementById('simple-search');
 if (!searchInput) return;

 const newSearchInput = searchInput.cloneNode(true);
 searchInput.parentNode.replaceChild(newSearchInput, searchInput);

 currentSearchTerm = newSearchInput.value?.trim() || '';

 newSearchInput.addEventListener('input', function () {
 const searchTerm = this.value.trim();

 if (searchTimeout) clearTimeout(searchTimeout);

 if (searchTerm === '') {
 currentSearchTerm = '';
 reloadPaqueteTable(1);
 return;
 }

 searchTimeout = setTimeout(() => {
 performServerSearch(searchTerm);
 },500);
 });
 }

 /**
 * Serializa el formulario de filtros a URLSearchParams
 */
 function buildParamsFromFilterForm() {
 const filterForm = document.getElementById('filterForm');
 const params = new URLSearchParams();
 if (filterForm) {
 const formData = new FormData(filterForm);
 for (const [key, value] of formData.entries()) {
 params.append(key, value);
 }
 }
 return params;
 }

 /**
 * Obtiene los parámetros de orden currente
 */
 function getCurrentSort() {
 return {
 sortBy: document.getElementById('current-sort-by')?.value || 'Nombre',
 sortOrder: document.getElementById('current-sort-order')?.value || 'asc'
 };
 }

 /**
 * Realiza búsqueda en el servidor
 */
 function performServerSearch(searchTerm) {
 currentSearchTerm = searchTerm;

 const params = buildParamsFromFilterForm();
 const currentSort = getCurrentSort();
 params.set('searchTerm', searchTerm);
 params.set('pageNumber', '1');
 params.set('sortBy', currentSort.sortBy);
 params.set('sortOrder', currentSort.sortOrder);

 const url = '/PaqueteServicio/SearchPartial?' + params.toString();
 loadTablePartial(url);
 }

 /**
 * Recarga la tabla de paquetes
 */
 function reloadPaqueteTable(pageNumber) {
 const params = buildParamsFromFilterForm();
 const currentSort = getCurrentSort();
 params.set('pageNumber', String(pageNumber));
 params.set('sortBy', currentSort.sortBy);
 params.set('sortOrder', currentSort.sortOrder);

 let url;
 if (currentSearchTerm) {
 params.set('searchTerm', currentSearchTerm);
 url = '/PaqueteServicio/SearchPartial?' + params.toString();
 } else {
 url = '/PaqueteServicio/TablePartial?' + params.toString();
 }
 loadTablePartial(url);
 }

 /**
 * Carga parcial de tabla
 */
 function loadTablePartial(url) {
 fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
 .then(response => response.text())
 .then(html => {
 const container = document.getElementById('paquete-table-container');
 if (container) container.innerHTML = html;
 })
 .catch(() => {
 showTableMessage('Error al cargar los datos', 'error');
 });
 }

 // =====================================
 // GESTIÓN DE SERVICIOS
 // =====================================
 /**
 * Carga servicios por tipo de vehículo
 */
 window.loadServiciosPorTipoVehiculo = async function () {
 const tipoVehiculo = document.getElementById('TipoVehiculo')?.value;
 const container = document.getElementById('servicio-selector-container');

 if (!tipoVehiculo) {
 container.classList.add('hidden');
 serviciosDisponibles = [];
 serviciosSeleccionados = [];
 updateResumen();
 return;
 }

 try {
 const response = await fetch(`/PaqueteServicio/ObtenerServiciosPorTipoVehiculo?tipoVehiculo=${encodeURIComponent(tipoVehiculo)}`);
 const data = await response.json();

 if (data.success) {
 serviciosDisponibles = data.servicios;
 container.classList.remove('hidden');
 renderServiciosDropdown(serviciosDisponibles);
 } else {
 container.classList.add('hidden');
 showPaqueteMessage(data.message || 'No hay servicios disponibles', 'error');
 }
 } catch (error) {
 console.error('Error cargando servicios:', error);
 container.classList.add('hidden');
 showPaqueteMessage('Error al cargar servicios', 'error');
 }
 };

 /**
 * Renderiza los servicios en el dropdown agrupados por tipo
 */
 function renderServiciosDropdown(servicios, filterText = '') {
 const dropdownContent = document.getElementById('servicio-dropdown-content');
 
 if (!servicios || servicios.length ===0) {
 dropdownContent.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No hay servicios disponibles</p>';
 return;
 }

 // Filtrar servicios por texto de búsqueda
 let serviciosFiltrados = servicios;
 if (filterText) {
 const searchLower = filterText.toLowerCase();
 serviciosFiltrados = servicios.filter(s => 
 (s.nombre && s.nombre.toLowerCase().includes(searchLower)) ||
 (s.tipo && s.tipo.toLowerCase().includes(searchLower))
 );
 }

 // Filtrar servicios ya seleccionados
 serviciosFiltrados = serviciosFiltrados.filter(s => 
 !serviciosSeleccionados.some(sel => sel.id === s.id)
 );

 if (serviciosFiltrados.length ===0) {
 dropdownContent.innerHTML = '<p class="text-sm text-gray-500 dark:text-gray-400 p-2">No se encontraron servicios</p>';
 return;
 }

 // Agrupar por tipo
 const serviciosPorTipo = {};
 serviciosFiltrados.forEach(s => {
 if (!serviciosPorTipo[s.tipo]) {
 serviciosPorTipo[s.tipo] = [];
 }
 serviciosPorTipo[s.tipo].push(s);
 });

 // Renderizar agrupados
 let html = '';
 Object.keys(serviciosPorTipo).sort().forEach(tipo => {
 html += `<div class="mb-2">
 <h6 class="text-xs font-semibold text-gray-700 dark:text-gray-300 px-2 py-1 bg-gray-100 dark:bg-gray-600">${tipo}</h6>
 <div class="space-y-1">`;
 
 serviciosPorTipo[tipo].forEach(servicio => {
 const isSelected = servicioSeleccionadoDropdown?.id === servicio.id;
 html += `
 <div class="px-2 py-2 hover:bg-gray-100 dark:hover:bg-gray-600 cursor-pointer ${isSelected ? 'bg-blue-100 dark:bg-blue-900' : ''}"
 onclick="selectServicioFromDropdown('${servicio.id}')">
 <div class="text-sm font-medium text-gray-900 dark:text-white">${servicio.nombre}</div>
 </div>`;
 });
 
 html += '</div></div>';
 });

 dropdownContent.innerHTML = html;
 }

 /**
 * Muestra el dropdown de servicios
 */
 window.showServicioDropdown = function () {
 const dropdown = document.getElementById('servicio-dropdown');
 const searchInput = document.getElementById('servicio-search');
 
 if (serviciosDisponibles.length >0) {
 renderServiciosDropdown(serviciosDisponibles, searchInput.value);
 dropdown.classList.remove('hidden');
 }
 };

 /**
 * Filtra servicios en el dropdown según el texto de búsqueda
 */
 window.filterServiciosDropdown = function (searchText) {
 renderServiciosDropdown(serviciosDisponibles, searchText);
 const dropdown = document.getElementById('servicio-dropdown');
 if (!dropdown.classList.contains('hidden')) {
 // El dropdown ya está visible, no hacer nada
 } else {
 dropdown.classList.remove('hidden');
 }
 };

 /**
 * Selecciona un servicio del dropdown
 */
 window.selectServicioFromDropdown = function (servicioId) {
 const servicio = serviciosDisponibles.find(s => s.id === servicioId);
 if (servicio) {
 servicioSeleccionadoDropdown = servicio;
 
 // Actualizar visualmente la selección
 renderServiciosDropdown(serviciosDisponibles, document.getElementById('servicio-search').value);
 
 // Actualizar el input de búsqueda con el nombre del servicio
 document.getElementById('servicio-search').value = servicio.nombre;
 }
 };

 /**
 * Agrega el servicio seleccionado a la lista
 */
 window.agregarServicioSeleccionado = function () {
 if (!servicioSeleccionadoDropdown) {
 showPaqueteMessage('Debe seleccionar un servicio del listado', 'error');
 return;
 }

 const tipo = servicioSeleccionadoDropdown.tipo;
 
 // Verificar si ya hay un servicio de este tipo
 const tipoYaSeleccionado = serviciosSeleccionados.some(s => s.tipo === tipo);
 if (tipoYaSeleccionado) {
 showPaqueteMessage('Solo puede seleccionar un servicio de cada tipo', 'error');
 return;
 }

 // Agregar servicio
 serviciosSeleccionados.push({
 id: servicioSeleccionadoDropdown.id,
 nombre: servicioSeleccionadoDropdown.nombre,
 tipo: servicioSeleccionadoDropdown.tipo,
 precio: servicioSeleccionadoDropdown.precio,
 tiempoEstimado: servicioSeleccionadoDropdown.tiempoEstimado
 });

 // Limpiar selección
 servicioSeleccionadoDropdown = null;
 document.getElementById('servicio-search').value = '';
 document.getElementById('servicio-dropdown').classList.add('hidden');

 // Actualizar UI
 updateServiciosSeleccionadosList();
 updateResumen();
 renderServiciosDropdown(serviciosDisponibles, '');
 };

 /**
 * Remueve un servicio de la lista de seleccionados
 */
 window.removerServicioSeleccionado = function (servicioId) {
 serviciosSeleccionados = serviciosSeleccionados.filter(s => s.id !== servicioId);
 updateServiciosSeleccionadosList();
 updateResumen();
 const searchInput = document.getElementById('servicio-search');
 renderServiciosDropdown(serviciosDisponibles, searchInput?.value || '');
 };

 /**
 * Actualiza la lista de servicios seleccionados
 */
 function updateServiciosSeleccionadosList() {
 const container = document.getElementById('servicios-seleccionados-container');
 const list = document.getElementById('servicios-seleccionados-list');

 if (serviciosSeleccionados.length ===0) {
 container?.classList.add('hidden');
 return;
 }

 container?.classList.remove('hidden');

 let html = '<ul class="space-y-2">';
 serviciosSeleccionados.forEach(servicio => {
 html += `
 <li class="flex justify-between items-center p-2 bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-600">
 <div>
 <span class="font-medium text-gray-900 dark:text-white">${servicio.nombre}</span>
 <span class="text-sm text-gray-500 dark:text-gray-400 ml-2">(${servicio.tipo})</span>
 </div>
 <div class="flex items-center gap-3">
 <div class="text-right">
 <div class="text-sm font-medium text-gray-900 dark:text-white">$${servicio.precio.toFixed(2)}</div>
 <div class="text-xs text-gray-500 dark:text-gray-400">${servicio.tiempoEstimado} min</div>
 </div>
 <button type="button" 
 onclick="removerServicioSeleccionado('${servicio.id}')"
 class="p-1 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300"
 title="Quitar servicio">
 <svg xmlns="http://www.w3.org/2000/svg" viewBox="002424" fill="currentColor" class="w-5 h-5">
 <path fill-rule="evenodd" d="M122.25c-5.3850-9.754.365-9.759.75s4.3659.759.759.759.75-4.3659.75-9.75S17.3852.25122.25Zm-1.726.97a.75.75010-1.061.06L10.9412l-1.721.72a.75.750101.061.06L1213.06l1.721.72a.75.750101.06-1.06L13.0612l1.72-1.72a.75.75010-1.06-1.06L1210.94l-1.72-1.72Z" clip-rule="evenodd" />
 </svg>
 </button>
 </div>
 </li>`;
 });
 html += '</ul>';

 if (list) list.innerHTML = html;
 }

 /**
 * Calcula precio y tiempo
 */
 window.calcularPrecioYTiempo = function () {
 updateResumen();
 };

 /**
 * Actualiza el resumen del paquete
 */
 function updateResumen() {
 const descuento = parseFloat(document.getElementById('PorcentajeDescuento')?.value ||0);

 const precioTotal = serviciosSeleccionados.reduce((sum, s) => sum + s.precio,0);
 const descuentoMonto = precioTotal * (descuento /100);
 const precioFinal = precioTotal - descuentoMonto;
 const tiempoTotal = serviciosSeleccionados.reduce((sum, s) => sum + s.tiempoEstimado,0);

 const precioTotalSinEl = document.getElementById('precio-total-sin-descuento');
 const precioFinalEl = document.getElementById('precio-final');
 const tiempoTotalEl = document.getElementById('tiempo-total');
 const precioInput = document.getElementById('Precio');
 const tiempoInput = document.getElementById('TiempoEstimado');

 if (precioTotalSinEl) precioTotalSinEl.textContent = '$' + precioTotal.toFixed(2);
 if (precioFinalEl) precioFinalEl.textContent = '$' + precioFinal.toFixed(2);
 if (tiempoTotalEl) tiempoTotalEl.textContent = tiempoTotal + ' min';

 if (precioInput) precioInput.value = precioFinal.toFixed(2);
 if (tiempoInput) tiempoInput.value = tiempoTotal;
 }

 // =====================================
 // FORMULARIO AJAX
 // =====================================
 /**
 * Envía el formulario vía AJAX
 */
 window.submitPaqueteAjax = function (form, event) {
 if (event) {
 event.preventDefault();
 }

 // Validar servicios seleccionados
 if (serviciosSeleccionados.length <2) {
 showPaqueteMessage('Debe seleccionar al menos2 servicios para crear un paquete', 'error');
 document.getElementById('servicios-error')?.classList.remove('hidden');
 return false;
 }

 document.getElementById('servicios-error')?.classList.add('hidden');

 // Preparar IDs de servicios
 const serviciosIds = serviciosSeleccionados.map(s => s.id);
 const serviciosIdsJson = document.getElementById('ServiciosIdsJson');
 if (serviciosIdsJson) serviciosIdsJson.value = JSON.stringify(serviciosIds);

 const formData = new FormData(form);

 fetch(form.action, {
 method: 'POST',
 body: formData,
 headers: {
 'X-Requested-With': 'XMLHttpRequest'
 }
 })
 .then(response => {
 const isValid = response.headers.get('X-Form-Valid') === 'true';
 const message = response.headers.get('X-Form-Message');

 return response.text().then(html => ({
 html: html,
 isValid: isValid,
 message: message
 }));
 })
 .then(data => {
 if (data.isValid) {
 showPaqueteMessage(data.message || 'Operación exitosa', 'success');
 limpiarFormularioPaquete();
 reloadPaqueteTable(1);

 // Cerrar acordeón después de2 segundos
 setTimeout(() => {
 const accordion = document.getElementById('accordion-flush-body-1');
 if (accordion) accordion.classList.add('hidden');
 },2000);
 } else {
 // Reemplazar formulario con errores
 document.getElementById('paquete-form-container').innerHTML = data.html;
 initializeForm();
 }
 })
 .catch(error => {
 console.error('Error:', error);
 showPaqueteMessage('Error al procesar la solicitud', 'error');
 });

 return false;
 };

 /**
 * Limpia el formulario
 */
 window.limpiarFormularioPaquete = function () {
 const form = document.getElementById('paquete-form');
 if (!form) return;

 const isEdit = form.dataset.edit === 'True';

 if (isEdit) {
 window.location.href = '/PaqueteServicio/Index';
 } else {
 form.reset();
 serviciosSeleccionados = [];
 serviciosDisponibles = [];
 servicioSeleccionadoDropdown = null;
 updateServiciosSeleccionadosList();
 updateResumen();
 resetServiceDropdown();
 hidePaqueteMessage();
 }
 };

 /**
 * Resetea el dropdown de servicios
 */
 function resetServiceDropdown() {
 const selectorContainer = document.getElementById('servicio-selector-container');
 const searchInput = document.getElementById('servicio-search');
 const dropdown = document.getElementById('servicio-dropdown');
 
 if (selectorContainer) selectorContainer.classList.add('hidden');
 if (searchInput) searchInput.value = '';
 if (dropdown) dropdown.classList.add('hidden');
 }

 /**
 * Edita un paquete
 */
 window.editPaquete = function (id) {
 window.location.href = `/PaqueteServicio/Index?editId=${id}`;
 };

 // =====================================
 // UTILIDADES DE MODAL (compatibles con Flowbite)
 // =====================================
 function getFlowbiteModal(modalEl) {
 if (!modalEl || typeof window !== 'object' || typeof window.Modal === 'undefined') return null;
 const opts = { backdrop: 'dynamic', closable: true };
 try {
 if (typeof Modal?.getInstance === 'function') {
 const existing = Modal.getInstance(modalEl);
 if (existing) return existing;
 }
 if (typeof Modal?.getOrCreateInstance === 'function') {
 return Modal.getOrCreateInstance(modalEl, opts);
 }
 return new Modal(modalEl, opts);
 } catch { return null; }
 }
 function abrirModal(id) {
 const m = document.getElementById(id);
 if (!m) return;
 try {
 const inst = getFlowbiteModal(m);
 if (inst?.show) { inst.show(); return; }
 } catch { }
 m.classList.remove('hidden');
 m.setAttribute('aria-hidden', 'false');
 }
 function cerrarModal(id) {
 const m = document.getElementById(id);
 if (!m) return;
 let closed = false;
 try {
 const inst = getFlowbiteModal(m);
 if (inst?.hide) { inst.hide(); closed = true; }
 } catch { }
 try {
 const backdrop = document.querySelector('[modal-backdrop]');
 if (backdrop) { backdrop.click(); closed = true; }
 } catch { }
 if (!closed) {
 m.classList.add('hidden');
 m.setAttribute('aria-hidden', 'true');
 document.querySelectorAll('[modal-backdrop]').forEach(b => b.remove());
 document.body.classList.remove('overflow-hidden');
 }
 }
 // =====================================
 // MODAL DE CONFIRMACIÓN
 // =====================================
 /**
 * Abre modal de confirmación para paquetes
 */
 window.openPaqueteConfirmModal = function (tipoAccion, id, nombre) {
 const title = document.getElementById('paqueteConfirmTitle');
 const msg = document.getElementById('paqueteConfirmMessage');
 const submitBtn = document.getElementById('paqueteConfirmSubmit');
 const form = document.getElementById('paqueteConfirmForm');
 const idInput = document.getElementById('paqueteConfirmId');
 const iconWrapper = document.getElementById('paqueteConfirmIconWrapper');
 const icon = document.getElementById('paqueteConfirmIcon');

 if (!(form && idInput && submitBtn && title && msg && iconWrapper && icon)) return;
 idInput.value = id;

 if (tipoAccion === 'desactivar') {
 title.textContent = 'Desactivar Paquete';
 msg.innerHTML = '¿Confirma desactivar el paquete <strong>' + (window.SiteModule?.escapeHtml?.(nombre) || nombre) + '</strong>?';
 form.action = '/PaqueteServicio/DeactivatePaquete';
 submitBtn.textContent = 'Desactivar';
 submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-red-600 rounded-lg hover:bg-red-700 focus:ring-4 focus:outline-none focus:ring-red-300 dark:bg-red-500 dark:hover:bg-red-600 dark:focus:ring-red-900';
 iconWrapper.className = 'w-12 h-12 rounded-full bg-red-100 dark:bg-red-900 p-2 flex items-center justify-center mx-auto mb-3.5';
 icon.setAttribute('fill', 'currentColor');
 icon.setAttribute('viewBox', '002020');
 icon.setAttribute('class', 'w-8 h-8 text-red-600 dark:text-red-400');
 icon.innerHTML = `<path fill-rule="evenodd" d="M1018a880100-1688000016zm3.707-11.293a11000-1.414-1.414L107.5867.7075.293a11000-1.4141.414L8.58610l-2.2932.293a110001.4141.414L1012.414l2.2932.293a110001.414-1.414L11.41410l2.293-2.293z" clip-rule="evenodd"/>`;
 } else {
 title.textContent = 'Reactivar Paquete';
 msg.innerHTML = '¿Confirma reactivar el paquete <strong>' + (window.SiteModule?.escapeHtml?.(nombre) || nombre) + '</strong>?';
 form.action = '/PaqueteServicio/ReactivatePaquete';
 submitBtn.textContent = 'Reactivar';
 submitBtn.className = 'py-2 px-3 text-sm font-medium text-center text-white bg-green-600 rounded-lg hover:bg-green-700 focus:ring-4 focus:outline-none focus:ring-green-300 dark:bg-green-500 dark:hover:bg-green-600 dark:focus:ring-green-900';
 iconWrapper.className = 'w-12 h-12 rounded-full bg-green-100 dark:bg-green-900 p-2 flex items-center justify-center mx-auto mb-3.5';
 icon.setAttribute('fill', 'currentColor');
 icon.setAttribute('viewBox', '002424');
 icon.setAttribute('class', 'w-8 h-8 text-green-500 dark:text-green-400');
 icon.innerHTML = `<path fill-rule="evenodd" d="M2.2512c0-5.3854.365-9.759.75-9.75s9.754.3659.759.75-4.3659.75-9.759.75S2.2517.3852.2512Zm13.36-1.814a.75.75010-1.22-.872l-3.2364.53L9.5312.22a.75.75000-1.061.06l2.252.25a.75.750001.14-.094l3.75-5.25Z" clip-rule="evenodd"/>`;
 }

 abrirModal('paqueteConfirmModal');
 };

 /**
 * Cierra modal de confirmación
 */
 window.closePaqueteConfirmModal = function () {
 cerrarModal('paqueteConfirmModal');
 };

 /**
 * Envía cambio de estado de paquete
 */
 window.submitPaqueteEstado = function (form) {
 const formData = new FormData(form);

 fetch(form.action, {
 method: 'POST',
 body: formData,
 headers: { 'X-Requested-With': 'XMLHttpRequest' }
 })
 .then(r => {
 if (!r.ok) throw new Error('Error estado');
 window.closePaqueteConfirmModal();

 const isDeactivate = form.action.includes('DeactivatePaquete');
 const message = isDeactivate ? 'Paquete desactivado correctamente.' : 'Paquete reactivado correctamente.';

 showTableMessage(message, 'success');
 reloadPaqueteTable(1);
 })
 .catch(e => {
 showTableMessage('Error procesando la operación.', 'error');
 });

 return false;
 };

 // =====================================
 // FILTROS
 // =====================================
 /**
 * Limpia todos los filtros
 */
 window.clearAllFilters = function () {
 const filterForm = document.getElementById('filterForm');
 if (filterForm) {
 const checkboxes = filterForm.querySelectorAll('input[type="checkbox"]');
 checkboxes.forEach(cb => cb.checked = false);

 const numbers = filterForm.querySelectorAll('input[type="number"]');
 numbers.forEach(n => n.value = '');
 }
 };

 // =====================================
 // PAGINACIÓN Y ORDENAMIENTO
 // =====================================
 /**
 * Cambia de página
 */
 window.changePage = function (pageNumber) {
 reloadPaqueteTable(pageNumber);
 };

 /**
 * Ordena la tabla
 */
 window.sortTable = function (sortBy) {
 const currentSortBy = document.getElementById('current-sort-by')?.value;
 const currentSortOrder = document.getElementById('current-sort-order')?.value || 'asc';

 let newSortOrder = 'asc';
 if (currentSortBy === sortBy) {
 newSortOrder = currentSortOrder === 'asc' ? 'desc' : 'asc';
 }

 // Actualizar inputs ocultos
 const sortByInput = document.getElementById('current-sort-by');
 const sortOrderInput = document.getElementById('current-sort-order');
 if (sortByInput) sortByInput.value = sortBy;
 if (sortOrderInput) sortOrderInput.value = newSortOrder;

 reloadPaqueteTable(1);
 };

 // =====================================
 // MENSAJES
 // =====================================
 /**
 * Muestra mensaje del formulario
 */
 function showPaqueteMessage(message, type) {
 const container = document.getElementById('form-message-container');
 const text = document.getElementById('form-message-text');

 if (!container || !text) return;

 text.textContent = message;
 container.className = `m-4 p-4 mb-4 text-sm rounded-lg ${type === 'success' ? 'text-green-800 bg-green-50 dark:bg-gray-800 dark:text-green-400' : 'text-red-800 bg-red-50 dark:bg-gray-800 dark:text-red-400'}`;
 container.classList.remove('hidden');

 if (paqueteMsgTimeout) clearTimeout(paqueteMsgTimeout);
 paqueteMsgTimeout = setTimeout(() => {
 container.classList.add('hidden');
 },5000);
 }

 /**
 * Oculta mensaje del formulario
 */
 function hidePaqueteMessage() {
 const container = document.getElementById('form-message-container');
 if (container) container.classList.add('hidden');
 }

 /**
 * Muestra mensaje de tabla
 */
 function showTableMessage(message, type) {
 const container = document.getElementById('table-message-container');
 const text = document.getElementById('table-message-text');

 if (!container || !text) return;

 text.textContent = message;
 container.className = `p-4 mb-4 mx-4 text-sm rounded-lg ${type === 'success' ? 'text-green-800 bg-green-50 dark:bg-gray-800 dark:text-green-400' : 'text-red-800 bg-red-50 dark:bg-gray-800 dark:text-red-400'}`;
 container.classList.remove('hidden');

 if (tableMsgTimeout) clearTimeout(tableMsgTimeout);
 tableMsgTimeout = setTimeout(() => {
 container.classList.add('hidden');
 },5000);
 }

})();
