# Exact frontend integration preparation

Run from the project root, with existing dependencies and browser cache, supplying full commit IDs:

```text
node scripts/prepare-frontend-integration-verification.cjs --base <40-hex-commit> --candidate <40-hex-commit>
```

The helper requires the exact 32-path changed boundary encoded in the script. It checks regular Git blob modes, individual sizes (2 MiB maximum), total payload (8 MiB maximum) and available system-temp capacity before creating a fresh scratch directory. It exports explicit paths using `git show` only, verifies every written file against the SHA-256 of its Git output, and records paths, sizes, hashes, commits, dependency junctions and executable command arrays in `manifest.json`. Dependencies and browser cache are junctions to the existing project, not copies; no installation occurs. Candidate inputs include the seven Node files, four browser fixtures, three differential tools, required server/configuration and finite Razor/JavaScript inputs. Baseline contains only the shared inputs for the differentials. No appsettings, credentials or workspace archive are exported.

The helper creates scratch under its process's `os.tmpdir()`. A child runtime may clean that directory when it settles: the L116 worker manifest subsequently returned ENOENT to its parent. A verifier needs an export prepared in the parent's retained runtime; the worker's successful preparation ran zero probes. Parent subsequently prepared `C:/Users/Andre/AppData/Local/Temp/gplrdwq2/frontend-integration-lR8yHV` and ran job28; its independently audited bounded results are recorded in L118 of `odd/tasks/frontend-consistency.md`. The earlier missing artifact was not recovered.

Execute the new manifest's `commands` in order, using each exact `cwd`, `env` and `argv`; each differential has its own initially empty report directory. The browser command starts the existing loopback fixture server, not the application. Preparation itself runs no probes, and does not establish behavior or independent verification. The checked historical classifier in the shared-presentation tool may require interpretation against the chosen base; a failing differential is not silently waived. Do not use a dirty worktree as the candidate. Retain the parent-owned scratch for verification and remove it afterward; exported junctions reference mutable existing dependencies. Hashes establish export consistency with Git output, not a defense against a malicious same-user process.
