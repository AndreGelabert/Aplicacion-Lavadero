# Login validation

Login opts into shared validation with immediate input checks and persistent required errors; invalid submission is prevented while valid submission remains native.
Recovery feedback includes dark-theme error styling; existing login and recovery flows remain separate.
Focused checks: `node --test Firebase.Tests/LoginValidation.test.cjs`; browser fixture: `tests/browser/login-validation.spec.cjs`.
