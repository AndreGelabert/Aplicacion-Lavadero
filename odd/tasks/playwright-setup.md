# Reusable Playwright setup
Objective: project-local Playwright and Chromium with repeatable browser checks. Branch: feat/audit-change-details; preserve all prior audit changes.
Delivery: feature-branch-chain inherited; no publishing. Runner: pnpm test:e2e; node --test Firebase.Tests/*.test.cjs.

## Specs
S1. "Bueno, instala y configura Playwright asi ya queda para futuras pruebas". Install @playwright/test as dev dependency, project-local Chromium, reusable config/scripts, browser smoke tests and setup/run documentation. Keep generated browsers/reports/auth state ignored.
S2. Existing constraint: "Comparación primero; persistencia después (recomendado)". Do not alter audit persistence or application authentication to enable tests. Preserve existing app and persistent development Firebase data. Prefer disposable local runner; document environment requirements honestly.

## Tasks
T1 | S1-S2 | delegated configuration + parent install | done | Job8 installed pinned Playwright1.64.1/local Chromium. Config/scripts/docs/setup test and ignored artifacts verified. Job9 real Chromium5/5 passed. No commit yet.
T2 | S1-S2 | parent + independent verifier Risk item5 (dependencies/environment) | in_progress | Browser5/5 parent-observed, independent verifier found no blockers and confirmed occupied-port refusal. Verifier extra checks timed out/unavailable and are not passes. Parent Node regression and native authority inspection pending; commit pending.

## Log
L1. Original request: "Bueno, instala y configura Playwright asi ya queda para futuras pruebas".
L3. Installation job7 exited127: npm command not found. Diagnostic confirms node.exe exists at C:/Program Files/nodejs/node.exe, but npm.cmd not on PowerShell PATH and npm/pnpm/corepack modules not resolvable; pnpm/corepack/npx commands unavailable. No dependency install succeeded, browser download never started. No retries or external path reads performed. Need authorize local package-manager bootstrap or user provision before continuing.
L4. User: "Tengo instalado pnpm pero lo habia sacado del path por un problema, ya lo solucione asi que deberias poder acceder a el". Fresh Windows PATH resolves C:/Users/Andre/AppData/Local/pnpm/bin/pnpm.cmd version12.15.0; inherited shell PATH stale. Use refreshed PATH only in child process, do not modify persistent environment.
L5. Job8 exit0 installed pinned @playwright/test1.64.1 and local Chromium/headless shell. Worker mvzhh2x0-m-7zdn authored config/scripts,5 offline fixture browser tests loading actual auditoria.js,docs and setup test (1 passed). Parent job9 running real Chromium; no full authenticated/Razor integration claimed. Found .local/pnpm-store not ignored yet; fix before any commit.
L6. Job9 Chromium5/5 passed. Added ignore for local package caches. Verifier mvzhlzcu-n-i087 S1/S2 met, reproduced occupied port refusal; independent repeat runs timed out (not passes, no retry). pnpm lock coherent; pre-existing package-lock stale documented. ASSESS native unassessable due intended untracked; escalated item5 independent verification done. Next parent Node regression/authority inspect.
L2. Authorization includes dependency/browser install. Put browser artifacts under ignored .local/playwright-browsers; no global installs or external default browser cache writes. Existing .local Firebase tooling available. Current app guard only accepts persistent emulator ports8189/9099; existing isolated tests use8190/9199 with transport override. Do not weaken production guards; choose a safe self-contained browser fixture runner if full app isolation requires out-of-scope auth changes. Functional authenticated audit verification remains distinct from browser test infrastructure smoke proof.
