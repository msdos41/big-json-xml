## Agent skills

### Issue tracker

Issues live as markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Canonical roles mapped 1:1 to `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.

### Architecture decisions (ADR)

- Read the relevant ADRs in `docs/adr/` before changing behaviour.
- Record a new design decision as a new numbered ADR (`docs/adr/NNNN-<slug>.md`) in the existing format: a `#` title stating the decision, then short paragraphs on what holds and what was rejected.
- To reverse a decision, write a new ADR that supersedes the old one. Do not rewrite the old ADR.

### Branching & merging

- Commit on a feature branch, open a pull request, and wait for CI to pass.
- Never push directly to `main`.
- Merging, tagging, and releasing are decided only by the repository owner.
- Pull requests are merged with rebase merge to keep the history linear.

### Build & test

- `dotnet test BigJsonXml.slnx` must pass locally before pushing.
- Desktop tests need Windows. On Linux, at least build them with `-p:EnableWindowsTargeting=true`.
- Timing-limited tests (e.g. `LargeScanTests`) are sensitive to machine load. Report a timing failure; do not loosen its limit.
- Never commit generated large test data.

### Releases

- The first section of `CHANGELOG.md` must stay a dated `## [X.Y.Z] - YYYY-MM-DD` heading that matches `<Version>` in `src/BigJsonXml.Desktop/BigJsonXml.Desktop.csproj`.
- Do not change these or create tags unless the owner asks. See `docs/releasing.md`.

### Git Commit Specification (Mandatory)

**Every change must be committed**, using Conventional Commits format. Test commits and implementation commits must be separated:

- **TDD Workflow**: First commit the tests (`test(<scope>): ...`), then commit the implementation as `feat(<scope>): ...`, `fix(<scope>): ...`, or `perf(<scope>): ...` as appropriate. Each is an independent commit.
- **Atomic Commits**: One commit per logical change. Only stage files directly related to the current change (`git add <files>`), avoiding sweeping changes or staging unrelated files.

Format:

```
<type>(<scope>): <subject>

<body>  # Optional
```

- `<type>`: `feat` (new feature) | `fix` (bug fix) | `refactor` (refactoring) | `test` (tests) | `docs` (documentation) | `chore` (maintenance/tooling) | `perf` (performance)
- `<scope>`: Affected subsystem: `core` (`src/BigJsonXml.Core`), `parsers` (the XML and JSON parsers in core), `desktop` (`src/BigJsonXml.Desktop`), `tests` (shared test infrastructure). Omit the scope for repository-wide `docs` and `chore` commits.
- `<subject>`: Imperative mood, lowercase start, max 50 characters, no trailing period.
- **Language**: The whole commit message, subject and body, is in English only. Do NOT use Chinese in commit messages.
- **No Trailers**: Prohibit automated tool attribution trailers (such as `Co-Authored-By: ...`).
- Examples:
  - `fix(core): publish a hit once the scan can open it`
  - `test(desktop): pin changelog rules on two versions`
  - `perf(parsers): skip whitespace runs with one span search`
  - `docs: add the release tag procedure`

Never commit secrets, credentials, `.env` files, or local temporary artifacts (strictly adhere to `.gitignore`).

### Context7 MCP

When available, use **Context7 MCP** (`resolve-library-id`, `query-docs`) when generating code, setting up configurations, exploring external library APIs, or checking migration paths.
