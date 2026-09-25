## Agent skills

### Issue tracker

Issues live as markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Canonical roles mapped 1:1 to `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.

### Git Commit Specification (Mandatory)

**Every change must be committed**, using Conventional Commits format. `test` and `feat` commits must be separated:

- **TDD Workflow**: First commit failing/passing tests (`test: <scope>: ...`), then commit the implementation (`feat: <scope>: ...`). Each is an independent commit.
- **Atomic Commits**: One commit per logical change. Only stage files directly related to the current change (`git add <files>`), avoiding sweeping changes or staging unrelated files.

Format:

```
<type>(<scope>): <subject>

<body>  # Optional
```

- `<type>`: `feat` (new feature) | `fix` (bug fix) | `refactor` (refactoring) | `test` (tests) | `docs` (documentation) | `chore` (maintenance/tooling) | `perf` (performance)
- `<scope>`: Affected package or subsystem: `core`, `parsers`, `ui`, `desktop`, `tests`, `harness`, `docs`
- `<subject>`: Imperative mood, lowercase start, max 50 characters, no trailing period.
- **Body**: English only. Do NOT use Chinese in commit messages.
- **No Trailers**: Prohibit automated tool attribution trailers (such as `Co-Authored-By: ...`).
- Examples:
  - `feat(core): implement SIMD search engine with reverse lookup`
  - `test(parsers): add test for xml dynamic schema discovery`
  - `fix(desktop): fix TreeDataGrid startup crash`
  - `docs: update AGENTS.md with monorepo package boundaries`

Never commit secrets, credentials, `.env` files, or local temporary artifacts (strictly adhere to `.gitignore`).

### Context7 MCP
When generating code, setting up configurations, exploring external library APIs, or checking migration paths, prioritize using **Context7 MCP** (`resolve-library-id`, `query-docs`).
