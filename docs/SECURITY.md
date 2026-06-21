# Security — SLT API

Security findings for this repo, by category. **No secret values are reproduced here.**
Each finding names the location and the remediation. Lead with the CRITICAL items.

> This is a crypto backend: it holds a blockchain hot-wallet private key and the symmetric
> keys that mint/decrypt every JWT. Treat committed-secret findings as incidents, not chores.

## CRITICAL — committed secrets

1. **Live production secrets in `SLT.Api/appsettings.json` (tracked in git).** Includes the
   blockchain hot-wallet `BlockChainSettings.PrivateKey` (controls on-chain funds),
   `JwtServiceSettings.SignatureKey` + `EncryptionKey` (forge/decrypt any JWT),
   `ApplicationPoolSettings` per-app `PreSharedKey` + `MasterSignature` (bypass the signature
   gate), and `CallPriceSettings.ApiKey`.
   **Remediation:** rotate every one of these now → move to environment variables / a secret
   store (the `docker-compose.yml` already maps `Section__Key=$var`, so the wiring exists) →
   `git rm --cached SLT.Api/appsettings.json`, add it to `.gitignore`, commit a redacted
   `appsettings.json.example`. Rotating without purging git history still leaves the old values
   recoverable — **history purge is required, not optional** (scope below).

   > **CONFIRMED LIVE — 2026-05-31 (on-chain).** This is no longer hypothetical. The committed
   > `BlockChainSettings.PrivateKey` controls the operational signer wallet `0xD549…4940`, verified
   > read-only against BSC + Ethereum: **nonce 560 on BSC / 10 on Ethereum**, holds gas, and both
   > configured contracts (`0xaA5c…3a41`, `0x344f…6a37`) are deployed. The key has been committed since
   > the first commit (`d84d3ec`, 2026-01-26) and **never rotated in history** — so every commit holds the
   > live key. Treat as an **active incident**: deploy a fresh signer, move funds + contract roles off
   > `0xD549…4940`, then purge history (filter-repo/BFG) from Init forward. Full evidence:
   > [`docs/reviews/security-perf-review-FINAL.md`](reviews/security-perf-review-FINAL.md) §0.

2. **Hardcoded Sentry DSN in `SLT.Api/Program.cs`** (~line 46), with `SendDefaultPii = true`
   and `Debug = true`. PII is forwarded to a third party and debug telemetry runs in every
   environment.
   **Remediation:** move the DSN to config/env; set `SendDefaultPii = false`; gate `Debug` to
   Development only.

## HIGH — auth-bypass surfaces

3. **`GetTokenWithPureWalletAddress` issues a JWT with no signature proof** of wallet ownership
   (`SLT.Services/_User/UserService.cs`). Anyone can mint a (NotVerified, guest) token for any
   wallet address. Downstream protection relies entirely on `RequireActiveUser`.
   **Remediation:** confirm this path is intentional; if kept, ensure every sensitive endpoint
   requires `RequireActiveUser` and that NotVerified tokens cannot reach value-moving actions.

4. **`MasterSignature` app bypass** (`SLT.Utilities/Middlewares/SignatureMiddleware.cs`) skips
   the nonce + HMAC check entirely for any caller holding the master signature.
   **Remediation:** scope master-signature usage tightly; rotate it (see finding 1); audit who
   holds it.

## MEDIUM — configuration / hardening

5. **Default firewall rule is allow-all** (`FirewallSettings.Rules` = `IPAddresses: ["*"],
   Policy: Allow`). The IP gate is effectively off.
   **Remediation:** define explicit allow/deny rules for production, or remove the false sense
   of protection.

6. **Symmetric JWT keys are short ASCII strings** embedded in config; encryption is AES-128.
   **Remediation:** use long random keys from a secret store; consider AES-256.

## Notes

- Replay protection on the signature gate exists (`INonceService.TryUse(nonce, 5min)`); the
  weakness is the `MasterSignature` exemption above, not the nonce logic.
- `SLT.Utilities/Permissions/Permissions.cs` is **dead copied code** (Persian HR/admin roles,
  zero references) — it is not an active access-control path and should not be relied on. Real
  authorization = signature gate + JWE token + `RequireActiveUser` + wallet claims.
- There are **no tests and no CI**, so none of the above is caught automatically. A secret-scan
  step in pre-commit / pipeline would have caught findings 1–2.

## 2026-05-31 review — additional confirmed findings (new drift)

Full audit (security · leaks · dependencies · performance · architecture), multi-agent + adversarially
verified, at commit `145754f`:
[`docs/reviews/`](reviews/README.md)
(**FINAL.md** = triaged, **RAW.md** = complete). New items not previously in this file:

- **HIGH — Unauthenticated cross-wallet eavesdrop (SignalR IDOR).** `WalletNotifyHub.RegisterWallet(walletAddress)`
  joins the caller to a group named by the raw, caller-supplied address with no ownership check; the hub is
  signature-exempt and has no auth, and invoice-created/paid notifications are pushed to those groups. Any client
  can subscribe to any wallet's financial events. Note: the custom `[Authorize]` is an MVC filter and **does not
  run on hubs** — validate identity inside the hub. (`WalletNotifyHub.cs:13-16`.)
- **HIGH — On-chain event dedup race.** Non-atomic check-then-insert with **no unique index**, while a WebSocket
  *and* a polling listener run concurrently per chain → an event can be processed twice (invoice double-advance;
  withdrawal totals inflated → premature stake-Finished). Add a unique index on `(Hash, InvoiceId/DepositId,
  EventType)`. (`TransactionLogService.cs:36-53`.)
- **HIGH — `InvoicePaid` completes an order without validating the on-chain pay amount / token** against the
  expected invoice. (`OrderService.cs:303-315`.)
- **HIGH — Request logger writes live secrets to Mongo.** `Authorization` (JWE), `Signature`/`MasterSignature`/
  `Nonce` headers, and raw bodies are persisted in cleartext on every `/api` call (before the auth gates, ~10-day
  retention). DB-read access yields replayable tokens + the plaintext gate-bypass credential.
  (`RequestLoggingMiddleware.cs:35`.)
- **MED — Swagger served unconditionally in all environments** (`Program.cs:61`); **global rate limiter
  implemented but never wired**; **container runs as root**; **non-constant-time HMAC compare**
  (`SignatureService.cs:18`); **nodereal.io RPC provider keys in committed URL paths** (`appsettings.json:38-40,55-57`).
- **Dependencies:** live CVE scan found 1 Critical (`System.Text.Encodings.Web 4.5.0` RCE) + Highs rooted in the
  obsolete `Microsoft.AspNetCore.Mvc 2.1.3` / `SignalR.Core 1.2.0` packages on net8.0 — fix by replacing them with
  `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. **Caveat:** several leaf CVEs are framework-shadowed
  false positives on net8.0 (see RAW.md §3).
- **Confirmed OK (not weaknesses):** JWE validation is well-hardened (`alg=none`/confusion unreachable); the
  `test/test/test` app is likely overridden by the deploy's env (`docker-compose.yml` maps `Applications__2__*`),
  so it is a source-hygiene issue, not necessarily a live prod bypass.

## Optional — deep AI security audit (deepsec)

For a periodic *deep* pass beyond the `secret-scan` / `dependency-audit` skills and the
diff-scoped `review` skill, consider [`vercel-labs/deepsec`](https://github.com/vercel-labs/deepsec)
— an AI-agent vulnerability scanner that reasons over the whole repo for auth gaps, broken
access control, crypto misuse (IV reuse, missing constant-time compares, algorithm confusion),
SSRF, and injection in *your own* logic. It has real .NET / ASP.NET Core support and is
host-agnostic (scans a local tree — no GitHub/GitLab dependency).

Use it as a **one-shot (then occasional) audit — NOT standing CI, NOT a wrapped skill** (that
would be over-engineering for these repos). It does **not** replace `secret-scan` (deepsec does
NOT catch secrets committed in `appsettings.json`) or `dependency-audit` (it does no CVE/SCA);
it complements them by finding logic/auth/crypto flaws those miss — high value on hot-wallet,
JWE, and on-chain code.

```bash
npx deepsec init                      # scaffolds .deepsec/ (config + data/<id>/INFO.md + SETUP.md)
cd .deepsec && pnpm install           # deepsec is a Node tool; this is its own dep install
# add an AI credential per .deepsec/SETUP.md (Vercel AI Gateway key or an Anthropic token)
```

Then fill `.deepsec/data/<id>/INFO.md` (<=100 lines) with the project primitives the regex layer
can't anchor — **hot-wallet key handling, the custom JWE auth flow, Monjo/MongoDB data access,
and Nethereum EVM on-chain paths. NOTE: this repo has no TRON / `tron-verifier` sidecar.** — then:

```bash
pnpm deepsec scan                     # fast, no AI
pnpm deepsec process --limit 50       # CALIBRATE cost first (Opus ≈ $25–60 / 100 files)
pnpm deepsec process --concurrency 5  # full pass once satisfied
pnpm deepsec revalidate --min-severity HIGH
pnpm deepsec export --format md-dir --out ./findings
```

Caveats: cost scales with file count (always `--limit` first); it runs a coding agent with shell
access (your source is trusted; prefer its sandbox mode if any vendored third-party code is present).
`.deepsec/` is gitignored by this repo's AI-layer setup.
