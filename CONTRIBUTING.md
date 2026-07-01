# Contributing to SLT.Api

This repo is the public SLT CargoPay backend. This document covers commit messages, merge
requests, validation, and the house rules that matter here.

For architecture and code layout, start with [README.md](README.md), [CODEBASE_MAP.md](CODEBASE_MAP.md),
[CONTEXT.md](CONTEXT.md), and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

---

## Commit messages

Use **[Conventional Commits](https://www.conventionalcommits.org/)**. Keep messages terse and
human. No generated-by footers.

### Format

```text
<type>(<optional scope>): <description>

<optional body>

<optional footer>
```

Example:

```bash
git commit -m "feat(order): add locked invoice read-through"
```

### Subject line rules

| Part | Rule |
|------|------|
| **type** | Required — see [Types](#types) |
| **scope** | Optional — logical area, not an issue ID |
| **description** | Required — imperative, present tense (`add`, not `added`); lowercase first letter; no trailing period |
| **breaking change** | Append `!` before `:` and describe impact in a `BREAKING CHANGE:` footer |

Think: _"This commit will..."_

### Types

| Type | When to use |
|------|-------------|
| `feat` | New or changed API behavior, endpoint contract, event sync, auth behavior |
| `fix` | Bug fix for existing behavior |
| `refactor` | Code structure change with no behavior change |
| `perf` | Performance improvement worth calling out |
| `style` | Formatting/whitespace only |
| `test` | Tests only, reserved for when this repo gets a test project |
| `docs` | Documentation only |
| `build` | Project files, build tooling, package management |
| `ops` | CI/CD, Docker, deployment config |
| `chore` | Repo maintenance that does not fit above |

### Scopes

Prefer a module or layer:

| Scope | Typical use |
|-------|-------------|
| `api` | Controllers, pipeline, `Program.cs`, app host wiring |
| `services` | `SLT.Services` feature logic and DTOs |
| `domain` | Collections, repositories, Monjo indexes |
| `utilities` | Shared filters, middleware, Monjo framework |
| `order` | `_Order` module |
| `invoice` | invoice contract and invoice state work |
| `blockchain` | Nethereum, listeners, ABI, chain reads/writes |
| `stake` | `_Stake` module |
| `user` | `_User` / wallet auth |
| `ci` | CI docs and runner config |

Issue IDs belong in the body/footer when useful, not as the scope.

### Examples

```text
feat(order): add locked invoice read-through
feat(blockchain): mirror locked invoice events
fix(invoice): keep refunded invoices from completing orders
docs: add locked invoice frontend handover
build(api): add user secrets profile
ops(ci): document release build gate
chore: update ignored local planning output
```

---

## Merge requests

Use GitLab. This repo delivers through the permanent `pourya` branch into `main`.

1. Work on `pourya`; do not create a short-lived feature branch unless the maintainer asks for one.
2. Keep commits reviewable and grouped by logical concern.
3. Open the MR from `pourya` to `main`.
4. MR description should say what changed, why, and how it was verified.
5. Production deploy is manual after merge.

There is no test project and no CI safety net here. The Release build is the required gate.

---

## Local validation

Before pushing:

```bash
dotnet build SLT.Api/SLT.Api.csproj -c Release
```

Do not run `dotnet format`, csharpier, `just`, or package-manager commands unless the task
explicitly asks for them. Match neighboring style.

---

## Code conventions

Full detail lives in [CLAUDE.md](CLAUDE.md), [AGENTS.md](AGENTS.md), and
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

| Topic | Convention |
|-------|------------|
| Layering | `SLT.Api -> SLT.Services -> SLT.Domain -> SLT.Utilities` |
| Mongo wrapper | **Monjo** is intentional: `MonjoRepository<T>`, `MonjoSettings`, `MonjoCollectionName` |
| HTTP reads | Reads stay `POST` actions with body DTOs |
| DI | Autofac marker interfaces; no manual service registration for business types |
| Nullability | `<Nullable>disable</Nullable>`; guard manually, no `required` or NRT assumptions |
| Auth | Custom `Utilities.Filters.AuthorizeAttribute`, not ASP.NET's filter |
| Errors | Throw `BaseException` / typed exceptions; controllers return bare DTOs |
| Not found | Use `NotFoundException(ApiResultStatusCode.NotFound, "...")` for real HTTP 404 |
| Secrets | Do not commit real `appsettings` values; use user-secrets/env/host secrets |
| Blockchain | EVM only: BSC/BEP20 + Ethereum/ERC20 via Nethereum; no Tron/Bitcoin |

When adding backend behavior, follow the local N-tier path: collection/repository if needed,
service module, DTOs, then a thin controller action.

---

## References

- [Conventional Commits](https://www.conventionalcommits.org/)
- [docs/WORKFLOW.md](docs/WORKFLOW.md)
- [docs/SECURITY.md](docs/SECURITY.md)
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
