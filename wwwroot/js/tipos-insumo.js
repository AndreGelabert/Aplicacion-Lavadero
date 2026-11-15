window.actualizarUnidadesInsumo = function (selector) {
    const unidades = { Volumen: ['ml', 'l'], Masa: ['mg', 'g', 'kg'], Unidades: ['unidad'] };
    const select = selector.form.querySelector('[name="UnidadMedida"]');
    const categoria = selector.selectedOptions[0]?.dataset.categoria;
    select.replaceChildren(new Option('Seleccionar unidad', ''));
    for (const unidad of unidades[categoria] || []) select.add(new Option(unidad, unidad));
};
