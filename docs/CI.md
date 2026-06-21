# CI / CD

GitLab pipeline defined in [`.gitlab-ci.yml`](../.gitlab-ci.yml). It gates every MR into `main`
on a Release build + a format check (GitLab shared runners), and deploys to the production box on
demand via a **self-hosted runner on that box**. **Zero secret migration** — secrets stay on the
server in `$DEPLOY_ENV_FILE`; no registry, no SSH key in CI.

## Pipeline shape

```
stages: build → quality → deploy
```

| Job | Stage | Runner | Trigger | Blocking? |
|-----|-------|--------|---------|-----------|
| `build` | build | shared (`sdk:8.0`) | MR + `main` | yes |
| `format` | quality | shared (`sdk:8.0`) | MR + `main` | **no** (`allow_failure: true`) — advisory for now |
| `deploy` | deploy | self-hosted `sltapi-prod` | `main` only, `when: manual` | n/a (▶ click) |

`workflow:rules` restricts pipelines to **merge-request events** and the **`main`** branch — this
prevents the common GitLab .NET pitfall of a duplicate *branch* pipeline running alongside every
MR pipeline.

### `build`
`dotnet restore` + `dotnet build SLT.Api/SLT.Api.csproj -c Release`. Builds the `.csproj`
(not the `.slnx`, which the `sdk:8.0` image's CLI doesn't open). NuGet packages are cached.

### `format`
`dotnet tool restore` (pins **csharpier `1.2.6`** via [`.config/dotnet-tools.json`](../.config/dotnet-tools.json))
then `dotnet csharpier check .`. The repo has never been csharpier-formatted, so the check
currently reports drift — hence `allow_failure: true` (advisory). [`.csharpierignore`](../.csharpierignore)
keeps it off `bin/`, `obj/`, `publish/`.

**Graduation to blocking:** land one dedicated commit running `dotnet csharpier format .`
(whitespace/layout only; csharpier does *not* rename namespaces, so the intentional `Utilities.*`
residue stays untouched), then delete `allow_failure: true` from the `format` job.

### `deploy`
Mirrors `deploy.sh` and runs on the prod box, so the locally-built image is reachable by compose:

```
dotnet publish → docker build -t slt.api . → docker-compose down → up -d → ps
```

Uses **`docker-compose` (v1)** to match `deploy.sh`. **No `--build`**: the single
`api.sltcargopay.com` service uses `image: slt.api` (no `build:` directive), so the explicit
`docker build -t slt.api .` is the only image build. Secrets are read from `$DEPLOY_ENV_FILE` via
`docker-compose --env-file` (the compose file substitutes `$var` placeholders — Mongo, JWT,
BlockChain, BlockchainWebSocket, ApplicationPool) — the runner checks out to a fresh dir, so
compose's auto-load of a `.env` in CWD (how `deploy.sh` works today) won't fire; the explicit
`--env-file` replaces it.

## Supporting config

- [`Directory.Build.props`](../Directory.Build.props) — built-in .NET 8 Roslyn analyzers
  (`AnalysisMode=Recommended`) across all 4 projects as **warnings** (`TreatWarningsAsErrors=false`,
  because the codebase is `<Nullable>disable</Nullable>` with intentional house anti-patterns).
- [`Directory.Packages.props`](../Directory.Packages.props) — NuGet **Central Package Management**:
  every package version lives here; the `.csproj` files carry version-less `<PackageReference>`s.
  Only `SLT.Services` + `SLT.Utilities` carry package refs (the host + Domain are project-refs only).

## One-time server setup (manual, on the prod box)

1. `gitlab-runner register` → tag **`sltapi-prod`**, **shell** executor. Shell is required: the
   no-registry design (`docker build` locally + compose `image: slt.api`) only works if the image
   lands on the **host** daemon. A docker executor builds inside a container and the image never
   reaches the host → deploy fails (unless explicitly bound to the host docker socket).
2. `usermod -aG docker gitlab-runner` so the runner user can drive Docker.
3. Set **`DEPLOY_ENV_FILE`** to the real secret-file path (default `/srv/slt.api/.env`) in
   **Settings → CI/CD → Variables**, or fix the default. ⚠️ Wrong path → compose injects **empty
   strings** into every secret and brings up a broken container with only warnings — verify first.
   This service holds a blockchain hot-wallet private key, so a silent-empty-secret start is dangerous.
4. Ensure **shared runners** are enabled so `build`/`format` have a runner; the tagged
   `sltapi-prod` runner serves only `deploy`. Register that runner to **refuse untagged jobs**
   (the default) so the untagged `build`/`format` jobs always land on shared runners, never the box.
5. **Compose project cutover (one-time).** `docker-compose.yml` pins a global `container_name`
   (`api.sltcargopay.com`). The CI deploy runs from the runner's checkout dir, so its compose
   *project name* differs from the manual `deploy.sh`'s — the first CI `up` would hit
   `Conflict: container name "api.sltcargopay.com" already in use`. Mitigations (do one): (a) set
   **`COMPOSE_PROJECT_NAME`** (default `sltapi`) to match the project the running container was
   created under, **or** run `docker-compose down` on the box once before the first CI deploy. This
   box also hosts `slt.manage` — keep the two project names distinct.

> This repo has **no sidecar** and **no `networks:` block** — a single dotnet service on the default
> bridge network. There is nothing to define there.

## Verifying a change

No test runner; building is the only automated validation. Locally:

```bash
dotnet build SLT.Api/SLT.Api.csproj -c Release
dotnet tool restore && dotnet csharpier check .    # reports drift until a reformat commit lands
```
