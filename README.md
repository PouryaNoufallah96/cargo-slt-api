# SLT API

Public/trading backend for **SLT CargoPay** — a crypto invoice/order + staking platform.
Wallets create token-denominated orders and invoices, pay them on-chain, and stake tokens for
time-locked monthly profit. The API mirrors on-chain events into MongoDB and serves read/report
endpoints. Customers authenticate by **Web3 wallet signature** (no passwords).

Chains are dual EVM: **BSC / BEP20 (ChainId 56)** and **Ethereum / ERC20**, via Nethereum.
There is **no Tron and no Bitcoin** in this service.

## Architecture (N-tier)

Classic layered .NET Web API — **not** Clean Architecture / DDD / CQRS / EF. Four projects with a
strict one-way dependency chain `Api → Services → Domain → Utilities`:

| Project | Role |
|---------|------|
| `SLT.Api` | HTTP host: `Program.cs`, versioned thin controllers (`Controllers/V1/`), middleware pipeline, app-local DI glue. |
| `SLT.Services` | Business logic in `_`-prefixed feature modules (`_Order`, `_User`, `_Stake`, …); Nethereum blockchain integration. |
| `SLT.Domain` | MongoDB persistence: `Collections/` (entities) + `Repositories/` (one per collection). |
| `SLT.Utilities` | Cross-cutting framework: the **Monjo** MongoDB wrapper, `ApiResult` envelope, exception hierarchy, middleware/filters, JWE auth, custom attributes. |

See **`docs/ARCHITECTURE.md`** for the diagram, middleware pipeline, request lifecycle, and an
honest architecture assessment; **`CODEBASE_MAP.md`** for where things live and how to add them;
**[CONTEXT.md](CONTEXT.md)** for domain and framework terms.

## Tech stack

- **.NET 8** (`net8.0`, C# 12; `<Nullable>disable</Nullable>` — nullable reference types are OFF).
- **MongoDB** via the custom **Monjo** wrapper over `MongoDB.Driver` 2.28.0 (no EF, no `DbContext`).
- **Autofac** DI by marker interfaces (`IScopedDependency`, `ISingletonDependency`, …).
- **Encrypted JWT (JWE)** auth; custom `[Authorize]` filter (not ASP.NET's); per-app HMAC signature gate.
- **Nethereum 5.0.0** (Web3, Contracts, Signer) for BSC + Ethereum; `CryptoPrice` for price feeds.
- **SignalR** hubs (`/hubs/prices`, `/hubs/NotifyWallet`); **Sentry** error reporting.

## Service modules

| Module | Purpose |
|--------|---------|
| `_Order` | Create quick/multi-step pending orders + invoices; list/detail/report; sync paid invoices; cleanup. |
| `_User` | Wallet nonce → signature → JWT auth; user fetch + stats. |
| `_Stake` | Create stake, history/detail, wallet stats, activate from chain events, cleanup. |
| `_Withdrawal` | Sync stake-withdrawal and profit-withdrawn chain events into the DB. |
| `_Price` | Fetch token prices; feed `PriceHub` + cached `PriceStorage`. |
| `_BlockChain` | EVM read/write via Nethereum (balances, on-chain invoice ops, stake profit preview), `_MultiCallService`, `_BlockChainWebSocket` listeners. |
| `_TransactionLog` | Persist invoice/deposit/profit/withdraw chain logs; track last-checked block per network; feed `WalletNotifyHub`. |
| `_Log` | Capture app + request logs; hard-delete cleanup (`RealDeleteManyAsync`). |

## Build, run, deploy

```bash
# Build (CI runs this on every MR; csharpier check is advisory, no tests)
dotnet build SLT.Api/SLT.Api.csproj -c Release        # or: dotnet build SLT.Api.slnx

# Run locally (needs a reachable Mongo at MonjoSettings.ConnectionString)
dotnet run --project SLT.Api/SLT.Api.csproj

# Publish
dotnet publish SLT.Api/SLT.Api.csproj -c Release -o publish

# Deploy: build + publish → docker build → docker-compose up → tail logs
./deploy.sh
```

`deploy.sh` produces container `api.sltcargopay.com`, bound to `127.0.0.1:3009`. `Dockerfile`
uses `mcr.microsoft.com/dotnet/aspnet:8.0` and runs the pre-published `SLT.Api.dll`.
`docker-compose.yml` injects secrets from host environment variables.

CI runs on GitLab (see [docs/CI.md](docs/CI.md)): every MR into `main` gets a Release build + an advisory csharpier check, and merges to `main` expose a manual deploy job on the self-hosted `sltapi-prod` runner. Package versions are centralized (`Directory.Packages.props`) and Roslyn analyzers run as warnings. There is still **no test project**.

## Security

This service holds a blockchain hot-wallet private key and the symmetric keys that mint/decrypt
every JWT. **Production secrets are currently committed to git** — see **`docs/SECURITY.md`** for
the full findings and remediation (rotate + externalize + `git rm --cached`). Read it before
touching config or auth.

## AI tooling

Project-local Claude Code config lives in `.claude/` (rules, commands, hooks, skill symlinks) and
`.agents/skills/`. Start from the root **`CLAUDE.md`**; see **`.claude/README.md`** for the layer
layout.
