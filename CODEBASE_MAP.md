# Codebase Map — SLT API

Where code lives, and which file to edit to add a thing. Read this before exploring.

## Projects (dependency direction: `Api → Services → Domain → Utilities`)

| Project | SDK | Role |
|---------|-----|------|
| `SLT.Api` | `Microsoft.NET.Sdk.Web` | HTTP host: `Program.cs`, `Controllers/V1/`, app-local DI + bootstrap glue under `Utilities/`. |
| `SLT.Services` | `Microsoft.NET.Sdk` | Business logic in `_`-prefixed feature modules; blockchain (Nethereum) integration. |
| `SLT.Domain` | `Microsoft.NET.Sdk` | Mongo `Collections/` (entities) + `Repositories/` (one per collection). |
| `SLT.Utilities` | `Microsoft.NET.Sdk` | Cross-cutting framework: Monjo wrapper, ApiResult, exceptions, middleware, filters, JWT/signature, attributes. Namespace root is `Utilities.*` (no `SLT.` prefix). |

Solution file: `SLT.Api.slnx` (XML solution format).

## Service modules (`SLT.Services/_<Module>/`)

`_Order`, `_User`, `_Stake`, `_Withdrawal`, `_Price`, `_BlockChain`, `_TransactionLog`, `_Log`.
Each: `IXService.cs` + `XService.cs` + a `DTOs/{Updates,Results,Settings,Storages}` tree, and
optionally `_Hub`/`_BackgroundServices` helpers. Only `_Order`, `_Stake`, `_User` have controllers;
the rest are internal (hubs, background sync, helpers).

## Collections (`SLT.Domain/Collections/`)

`Order`, `Invoice`, `Stake`, `Withdrawal`, `User`, `Log`, `RequestLog`, `TransactionLog`.

## Controllers (`SLT.Api/Controllers/V1/`)

`OrderController`, `StakeController`, `UserController`. (Note: `UserController` declares the stale
`CoinHalls.Api.Controllers.V1` namespace — see CLAUDE.md.)

### Locked invoice (Conditional Payment)

Optional fields on **existing** create DTOs; existing invoice detail returns optional live locked state.

| Piece | Location |
|-------|----------|
| Create | `CreatePendingQuickOrderAsync` / `CreatePendingMultiStepOrderAsync` — optional `isLocked`, `lockDurationMonths`, `thirdPartyApprover` on `CreateQuickInvoiceUpdate` / `CreateMultiStepOrderUpdate` |
| Detail | `GetInvoiceDetailAsync` handles normal invoices unchanged and locked invoices with optional live lock fields |
| Approval | `GetApprovalListAsync`, `GetApprovalDetailAsync`, `GetApprovalReportAsync` in `_Order` / `OrderController` |
| Result fields | `InvoiceResult`: normal invoice fields plus optional locked detail fields |
| Config | `_Order/DTOs/Settings/LockedInvoiceSettings.cs` → `RegisterSetting` in `ControllerServiceCollectionExtensions.cs`; docker env in `docker-compose.yml` |
| Domain | `Invoice.Lock` (`LockDetail`), `LockState` enum — `SLT.Domain/Collections/Invoice.cs` |
| Sync | `OrderService.SyncLockedInvoice*Async`; `TransactionLogService.CreateLockedInvoice*Async` |
| Chain read | `BlockChainService.GetLockedInvoiceAsync` → `LockedInvoiceChainResult` |
| Listeners | `_BlockChain/_BlockChainWebSocket/*` — decode `LockedInvoiceCreated/Paid/Approved/Resolved` after normal invoice events |
| Frontend handover | `docs/handover/locked-invoice-frontend-handover.md` (+ `-fa.md`) |
| ADRs | `docs/adr/0001-locked-invoice-embedded-subdocument.md`, `0002-multi-step-locked-invoice-all-or-nothing.md` |

## "To add X, edit Y"

| To add… | Do this |
|---------|---------|
| A new Mongo entity | Add `SLT.Domain/Collections/X.cs` extending `BaseDocument`, tagged `[MonjoCollectionName("Xs")]`. Put domain enums in the same file. |
| A repository | Add `SLT.Domain/Repositories/Contracts/IXRepository.cs` (`: IMonjoRepository<X>`) + `XRepository.cs` (`(IMonjoConnection c) : MonjoRepository<X>(c), IXRepository, ISingletonDependency`). No DI line needed. |
| A feature/service | Add `SLT.Services/_X/IXService.cs` + `XService.cs` (`: IXService, IScopedDependency`) + a `DTOs/` tree. No DI line needed — the marker registers it. |
| A config section | Add a `*Settings` POCO under the module's `DTOs/Settings/`, then bind it via `RegisterSetting<T>` in `SLT.Api/Utilities/Configurations/ControllerServiceCollectionExtensions.cs` (`AddSettings`). Inject the POCO directly, not `IOptions<T>`. |
| An endpoint | Add an action to a `Controllers/V1/*Controller.cs` (or a new thin controller). Use `[HttpPost("[action]")]`, return the bare DTO. |
| A background job | Extend `SchedulerBase` and implement `IHostedDependency`. |
| A SignalR hub | Add the hub, map it in `Program.cs` (alongside `/hubs/prices`, `/hubs/NotifyWallet`). |
| A new exception type | Add under `SLT.Utilities/Exceptions/`, subclass `BaseException`, preset `HttpStatusCode` + `ApiResultStatusCode`. |

## Config & deploy

| File | Purpose |
|------|---------|
| `SLT.Api/appsettings.json` | All config sections (Monjo, Jwt, Firewall, BlockChain, Stake, LockedInvoiceSettings, …). **Contains committed secrets — see docs/SECURITY.md.** |
| `Dockerfile` | Runtime image (`aspnet:8.0`, runs `SLT.Api.dll` from `./publish/`). |
| `docker-compose.yml` | Injects secrets via host env vars; container `api.sltcargopay.com`, bound `127.0.0.1:3009`. |
| `deploy.sh` | build → publish → docker build → compose up → tail logs. |

## AI layer

`.claude/` (rules, commands, hooks, scripts, skill symlinks) · `.agents/skills/` (canonical skills) ·
`docs/` (ARCHITECTURE, SECURITY, WORKFLOW, adr, handover) · `CONTEXT.md` · root `CLAUDE.md` (AI entry point). See
`.claude/README.md`.
