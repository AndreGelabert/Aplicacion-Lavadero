# Shared form validation core

`FormValidation.init(form, options)` opts a form in; `data-validate="true"` forms initialize on DOM ready. `validateForm(form)` validates required fields and returns a boolean.
Defaults validate on blur and input, with a 300 ms input debounce; required-message errors auto-hide after 3000 ms if still unchanged and empty.
Options `inputDebounceMs: 0` and `requiredErrorTimeoutMs: 0` enable immediate input validation and persistent required errors respectively.
Required markers are normalized for light/dark themes. Reinitializing adds newly inserted required fields without duplicating existing listeners; existing fields keep their first initialization options.
Validation includes required fields inside hidden ancestors; server validation messages survive initialization.

Focused Node check: `node --test Firebase.Tests/FormValidation.test.cjs`.
Browser coverage: `tests/browser/form-validation.spec.cjs`.
