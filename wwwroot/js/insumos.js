(() => {
    const createModal = document.getElementById('insumo-form');
    const inventoryForm = createModal;
    const title = document.getElementById('form-title');
    const accordion = document.getElementById('accordion-flush-body-1');
    const accordionButton = document.querySelector('[data-accordion-target="#accordion-flush-body-1"]');
    function resetInventory() {
        if (!inventoryForm) return;
        inventoryForm.reset();
        inventoryForm.elements.crear.value = 'true';
        inventoryForm.elements.Id.value = '';
        const type = inventoryForm.querySelector('select[name="TipoId"]');
        const unit = inventoryForm.querySelector('select[name="UnidadMedida"]');
        const hiddenType = inventoryForm.querySelector('input[name="TipoId"]');
        const hiddenUnit = inventoryForm.querySelector('input[name="UnidadMedida"]');
        type.disabled = unit.disabled = false; hiddenType.disabled = hiddenUnit.disabled = true;
        inventoryForm.querySelectorAll('[data-open-tipos], [data-delete-tipo]').forEach(b => b.disabled = false);
        inventoryForm.querySelectorAll('[data-missing-provider]').forEach(el => el.remove());
        inventoryForm.querySelector('[type="submit"]').disabled = inventoryForm.dataset.canCreate !== 'true';
        title.textContent = 'Registrando un Insumo';
    }
    inventoryForm?.querySelector('[data-reset-insumo]')?.addEventListener('click', resetInventory);
    if (inventoryForm && inventoryForm.dataset.canCreate !== 'true') resetInventory();
    document.querySelectorAll('[data-edit-insumo]').forEach(button => button.addEventListener('click', () => {
        const record = JSON.parse(button.dataset.record);
        resetInventory();
        inventoryForm.elements.crear.value = 'false'; inventoryForm.elements.Id.value = record.Id;
        inventoryForm.elements.Nombre.value = record.Nombre;
        inventoryForm.elements.StockMinimo.value = record.StockMinimo;
        inventoryForm.elements.CantidadReabastecimiento.value = record.CantidadReabastecimiento;
        inventoryForm.elements.ReabastecimientoAutomatico.checked = record.ReabastecimientoAutomatico;
        inventoryForm.elements.Tipo.value = record.Tipo || '';
        const type = inventoryForm.querySelector('select[name="TipoId"]'), unit = inventoryForm.querySelector('select[name="UnidadMedida"]');
        const hiddenType = inventoryForm.querySelector('input[name="TipoId"]'), hiddenUnit = inventoryForm.querySelector('input[name="UnidadMedida"]');
        type.value = record.TipoId || ''; window.actualizarUnidadesInsumo(type); unit.value = record.UnidadMedida || '';
        hiddenType.value = record.TipoId || ''; hiddenUnit.value = record.UnidadMedida || '';
        type.disabled = unit.disabled = true; hiddenType.disabled = hiddenUnit.disabled = false;
        inventoryForm.querySelectorAll('[data-open-tipos], [data-delete-tipo]').forEach(b => b.disabled = true);
        const providers = inventoryForm.querySelector('#insumo-proveedores');
        for (const id of record.ProveedoresIds || []) {
            let checkbox = [...providers.querySelectorAll('input')].find(x => x.value === id);
            if (!checkbox) { const label = document.createElement('label'); label.dataset.missingProvider = 'true'; checkbox = document.createElement('input'); checkbox.type = 'checkbox'; checkbox.name = 'ProveedoresIds'; checkbox.value = id; label.append(checkbox, document.createTextNode(' Proveedor no disponible (' + id + ')')); providers.append(label); }
            checkbox.checked = true;
        }
        inventoryForm.querySelector('[type="submit"]').disabled = false;
        title.textContent = 'Editando un Insumo';
        if (accordion.classList.contains('hidden')) accordionButton.click();
        inventoryForm.scrollIntoView({ block: 'start' }); inventoryForm.elements.Nombre.focus();
    }));
    let dialogTrigger = null;
    function openDialog(id, trigger) { dialogTrigger = trigger; const modal = document.getElementById(id); modal.classList.remove('hidden'); modal.querySelector('input:not([type="hidden"]), button')?.focus(); }
    document.querySelectorAll('[data-ajustar-insumo]').forEach(button => button.addEventListener('click', () => { const modal = document.getElementById('insumoAjusteModal'); modal.querySelector('form').reset(); modal.querySelector('[name="insumoId"]').value = button.dataset.id; openDialog('insumoAjusteModal', button); }));
    document.querySelectorAll('[data-estado-insumo]').forEach(button => button.addEventListener('click', () => { const modal = document.getElementById('insumoConfirmModal'), deactivate = button.dataset.estado === 'Inactivo'; modal.querySelector('[name="id"]').value = button.dataset.id; modal.querySelector('[name="estado"]').value = button.dataset.estado; document.getElementById('insumoConfirmTitle').textContent = deactivate ? 'Desactivar Insumo' : 'Reactivar Insumo'; document.getElementById('insumoConfirmMessage').textContent = `¿Confirma ${deactivate ? 'desactivar' : 'reactivar'} el insumo ${button.dataset.nombre}?`; document.getElementById('insumoConfirmSubmit').textContent = deactivate ? 'Desactivar' : 'Reactivar'; openDialog('insumoConfirmModal', button); }));
    document.querySelectorAll('[data-close-dialog]').forEach(button => button.addEventListener('click', () => { button.closest('[role="dialog"]').classList.add('hidden'); dialogTrigger?.focus(); }));
    document.querySelectorAll('[data-sort-insumos]').forEach(button => button.addEventListener('click', () => {
        const table = button.closest('table'), body = table.tBodies[0];
        const ascending = button.dataset.order !== 'asc';
        const rows = [...body.rows];
        rows.sort((a, b) => (ascending ? 1 : -1) * a.cells[0].textContent.trim().localeCompare(b.cells[0].textContent.trim(), 'es', { sensitivity: 'base', numeric: true }));
        rows.forEach(row => body.appendChild(row));
        button.dataset.order = ascending ? 'asc' : 'desc';
        button.closest('th').setAttribute('aria-sort', ascending ? 'ascending' : 'descending');
    }));
    const typesModal = document.getElementById('tiposInsumoModal');
    document.querySelectorAll('[data-unidad]').forEach(el => { el.textContent = window.etiquetaUnidadInsumo(el.dataset.unidad); });
    document.querySelectorAll('[data-page]').forEach(link => {
        const url = new URL(window.location.href);
        url.searchParams.set('page', link.dataset.page);
        link.href = url.toString();
    });
    if (!createModal || !typesModal) return;

    const form = document.getElementById('tipoInsumoForm');
    const message = document.getElementById('tiposInsumoMensaje');
    const table = document.getElementById('tiposInsumoTabla');
    const createForm = createModal;
    const selector = createForm.querySelector('select[name="TipoId"]');
    const deleteDialog = document.getElementById('confirmarEliminarTipoInsumo');
    let deleteId = '', deleteTrigger = null;
    function closeDelete() { deleteDialog.classList.add('hidden'); deleteId = ''; deleteTrigger?.focus(); }
    deleteDialog.querySelector('[data-cancel-delete-tipo]').addEventListener('click', closeDelete);
    deleteDialog.querySelector('[data-confirm-delete-tipo]').addEventListener('click', async () => {
        const result = await mutate(typesModal.dataset.deleteUrl, { id: deleteId });
        if (result) closeDelete();
        else document.getElementById('confirmarEliminarTipoError').textContent = message.textContent;
    });
    const canEdit = typesModal.dataset.canEdit === 'true';
    let activeModal = null;
    let returnFocus = null;
    let previousType = '';
    let instances = new Map();

    function instance(el) {
        if (!instances.has(el)) {
            const modal = typeof window.Modal === 'function' ?
                (typeof window.Modal.getInstance === 'function' && window.Modal.getInstance(el)) || new window.Modal(el, { backdrop: 'static', closable: false }) : null;
            instances.set(el, modal);
        }
        return instances.get(el);
    }
    function show(el, focus) {
        const modal = instance(el);
        if (modal) modal.show(); else { el.classList.remove('hidden'); el.setAttribute('aria-hidden', 'false'); el.setAttribute('role', 'dialog'); }
        activeModal = el;
        (focus || el.querySelector('input:not([type="hidden"]), button'))?.focus();
    }
    function hide(el) {
        const modal = instance(el);
        if (modal) modal.hide(); else { el.classList.add('hidden'); el.setAttribute('aria-hidden', 'true'); }
        activeModal = null;
    }
    function closeTypes() { hide(typesModal); returnFocus?.focus(); }
    createModal.querySelector('[data-open-tipos]').addEventListener('click', e => {
        returnFocus = e.currentTarget;
        show(typesModal);
        refresh();
    });
    typesModal.querySelector('[data-close-tipos]').addEventListener('click', closeTypes);
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape' && activeModal) {
            e.preventDefault();
            if (activeModal === typesModal) closeTypes();
            else hide(activeModal);
        } else if (e.key === 'Tab' && activeModal) {
            const items = [...activeModal.querySelectorAll('button:not([disabled]), input:not([type="hidden"]):not([disabled]), select:not([disabled]), a[href]')].filter(el => el.offsetParent !== null);
            if (!items.length) return;
            const first = items[0], last = items[items.length - 1];
            if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
            else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
        }
    });
    function resetForm() { form.reset(); form.elements.crear.value = 'true'; form.elements.Id.value = ''; }
    typesModal.querySelector('[data-reset-tipo]').addEventListener('click', resetForm);
    function button(label, handler) {
        const b = document.createElement('button'); b.type = 'button'; b.textContent = label;
        b.className = 'app-button app-button--secondary'; b.addEventListener('click', handler); return b;
    }
    async function refresh(selectedId) {
        try {
            const response = await fetch(typesModal.dataset.listUrl, { headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error('No se pudieron cargar los tipos.');
            const types = await response.json();
            const selected = selectedId || selector.value;
            selector.replaceChildren(new Option('Seleccionar tipo', ''));
            table.replaceChildren();
            let hasTool = false;
            for (const type of types) {
                const protectedType = type.esProtegido || type.nombre.toLowerCase() === 'herramienta';
                hasTool ||= type.nombre.toLowerCase() === 'herramienta';
                if (type.estado === 'Activo') {
                    const option = new Option(type.nombre, type.id); option.dataset.categoria = type.categoria; selector.add(option);
                }
                const row = document.createElement('tr'); row.className = 'border-b dark:border-gray-700';
                for (const value of [type.nombre, type.categoria, window.etiquetaUnidadInsumo(type.unidadResumen), type.estado]) {
                    const cell = document.createElement('td'); cell.className = 'p-2'; cell.textContent = value; row.append(cell);
                }
                const actions = document.createElement('td'); actions.className = 'p-2 flex flex-wrap gap-1';
                if (protectedType) actions.textContent = 'Protegido';
                else if (canEdit) {
                    actions.append(button('Editar', () => {
                        form.elements.crear.value = 'false'; form.elements.Id.value = type.id;
                        form.elements.Nombre.value = type.nombre; form.elements.Categoria.value = type.categoria;
                        const unit = form.elements.UnidadResumen;
                        if (![...unit.options].some(o => o.value === type.unidadResumen)) unit.add(new Option(window.etiquetaUnidadInsumo(type.unidadResumen), type.unidadResumen));
                        unit.value = type.unidadResumen; form.elements.Nombre.focus();
                    }));
                    actions.append(button(type.estado === 'Activo' ? 'Desactivar' : 'Reactivar', () => mutate(typesModal.dataset.stateUrl, { id: type.id, estado: type.estado === 'Activo' ? 'Inactivo' : 'Activo' })));
                    actions.append(button('Eliminar', () => { if (window.confirm('¿Eliminar este tipo de insumo?')) mutate(typesModal.dataset.deleteUrl, { id: type.id }); }));
                }
                row.append(actions); table.append(row);
            }
            typesModal.querySelector('[data-init-tool]').hidden = hasTool;
            selector.value = selected;
            if (selector.value !== previousType) { window.actualizarUnidadesInsumo(selector); previousType = selector.value; }
        } catch (error) { message.textContent = error.message; }
    }
    selector.addEventListener('change', () => { previousType = selector.value; });
    createModal.querySelector('[data-delete-tipo]')?.addEventListener('click', e => {
        if (!selector.value) { window.alert('Seleccione un tipo de insumo.'); return; }
        deleteId = selector.value; deleteTrigger = e.currentTarget;
        document.getElementById('confirmarEliminarTipoMensaje').textContent = `¿Confirma eliminar el tipo de insumo ${selector.selectedOptions[0].textContent}?`;
        document.getElementById('confirmarEliminarTipoError').textContent = '';
        deleteDialog.classList.remove('hidden');
        deleteDialog.querySelector('[data-cancel-delete-tipo]').focus();
    });
    async function mutate(url, values) {
        const data = new FormData();
        data.set('__RequestVerificationToken', form.querySelector('[name="__RequestVerificationToken"]').value);
        for (const [key, value] of Object.entries(values)) data.set(key, value);
        try {
            const response = await fetch(url, { method: 'POST', body: data, headers: { Accept: 'application/json' } });
            const result = await response.json();
            if (!response.ok) throw new Error(result.mensaje || 'No se pudo completar la operación.');
            message.textContent = result.mensaje;
            resetForm();
            await refresh(result.id);
            return true;
        } catch (error) { message.textContent = error.message || 'No se pudo completar la operación.'; return false; }
    }
    form.addEventListener('submit', e => {
        e.preventDefault();
        if (!form.reportValidity()) return;
        mutate(typesModal.dataset.saveUrl, Object.fromEntries(new FormData(form)));
    });
    typesModal.querySelector('[data-init-tool]').addEventListener('click', () => mutate(typesModal.dataset.toolUrl, {}));
})();
