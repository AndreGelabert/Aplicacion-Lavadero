const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

for (const file of ['wwwroot/js/lavado.js', 'wwwroot/js/lavado-detalle.js']) {
    test(`${file}: inventory flows do not use native prompts or confirms`, () => {
        const source = fs.readFileSync(file, 'utf8');
        assert.doesNotMatch(source, /window\.(?:prompt|confirm)\s*\(/);
    });
}

test('detalle header reflows navigation and actions on narrow screens', () => {
    const source = fs.readFileSync('Views/Lavados/Detalle.cshtml', 'utf8');
    assert.match(source, /class="mb-6 flex flex-col[^\"]*sm:flex-row/);
    assert.match(source, /class="flex flex-wrap gap-2"/);
    assert.match(source, /class="flex min-w-0 flex-wrap items-center gap-4"/);
});

test('both navigation paths provide the inventory interaction modal', () => {
    for (const file of ['Views/Lavados/Index.cshtml', 'Views/Lavados/Detalle.cshtml']) {
        assert.match(fs.readFileSync(file, 'utf8'), /_InsumosInteractionModals/);
    }
});
