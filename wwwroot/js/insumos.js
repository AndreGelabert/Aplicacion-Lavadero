(() => {
    const createModal = document.getElementById('crearInsumoModal');
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
    const createForm = createModal.querySelector('form');
    const selector = createForm.querySelector('[name="TipoId"]');
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
    function closeTypes() { hide(typesModal); show(createModal, returnFocus); }
    document.querySelector('[data-open-insumo]').addEventListener('click', e => { returnFocus = e.currentTarget; show(createModal); });
    document.querySelector('[data-close-insumo]').addEventListener('click', () => { hide(createModal); document.querySelector('[data-open-insumo]').focus(); });
    createModal.querySelector('[data-open-tipos]').addEventListener('click', e => {
        returnFocus = e.currentTarget;
        hide(createModal);
        show(typesModal);
        refresh();
    });
    typesModal.querySelector('[data-close-tipos]').addEventListener('click', closeTypes);
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape' && activeModal) {
            e.preventDefault();
            if (activeModal === typesModal) closeTypes();
            else { hide(createModal); document.querySelector('[data-open-insumo]').focus(); }
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
        } catch (error) { message.textContent = error.message || 'No se pudo completar la operación.'; }
    }
    form.addEventListener('submit', e => {
        e.preventDefault();
        if (!form.reportValidity()) return;
        mutate(typesModal.dataset.saveUrl, Object.fromEntries(new FormData(form)));
    });
    typesModal.querySelector('[data-init-tool]').addEventListener('click', () => mutate(typesModal.dataset.toolUrl, {}));
})();
