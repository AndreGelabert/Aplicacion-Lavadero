window.etiquetaUnidadInsumo = function (unidad) {
    const etiquetas = { ml: 'Mililitro (mL)', l: 'Litro (L)', mg: 'Miligramo (mg)', g: 'Gramo (g)', kg: 'Kilogramo (kg)', unidad: 'Unidad (u)', u: 'Unidad (u)', unidades: 'Unidad (u)', litro: 'Litro (L)', litros: 'Litro (L)', lt: 'Litro (L)', mililitro: 'Mililitro (mL)', mililitros: 'Mililitro (mL)', cm3: 'Centímetro cúbico (cm³)', cc: 'Centímetro cúbico (cc)', miligramo: 'Miligramo (mg)', miligramos: 'Miligramo (mg)', gr: 'Gramo (g)', gramo: 'Gramo (g)', gramos: 'Gramo (g)', kilo: 'Kilogramo (kg)', kilogramo: 'Kilogramo (kg)', kilogramos: 'Kilogramo (kg)' };
    return etiquetas[(unidad || '').trim().toLowerCase()] || unidad;
};
window.actualizarUnidadesInsumo = function (selector) {
    const unidades = { Volumen: ['ml', 'l'], Masa: ['mg', 'g', 'kg'], Unidades: ['unidad'] };
    const select = selector.form.querySelector('select[name="UnidadMedida"]');
    const categoria = selector.selectedOptions[0]?.dataset.categoria;
    select.replaceChildren(new Option('Seleccionar unidad', ''));
    for (const unidad of unidades[categoria] || []) select.add(new Option(window.etiquetaUnidadInsumo(unidad), unidad));
};
