# Dynamic form validation

Cliente, Vehículo, Servicio and PaqueteServicio reinitialize opted-in forms after their AJAX replacement hooks; existing business validators remain separate.
Repeated initialization binds new fields once and preserves existing server messages; hidden required fields remain subject to validation.
Synthetic submit coverage checks selections and rejection/network-error state, including Paquete's intentional hidden selection serialization.
Focused checks: `node --test Firebase.Tests/DynamicFormValidation.test.cjs Firebase.Tests/ModuleValidation.test.cjs`; browser fixtures: `tests/browser/dynamic-form-validation.spec.cjs`, `tests/browser/module-validation.spec.cjs`.
