# SLT API (CargoPay)

Crypto invoice/order + staking backend on BSC and Ethereum (dual EVM). N-tier MongoDB via **Monjo**, wallet-signature auth, Nethereum. **No Tron** in this repo.

## Domain

**Order**:
A token-denominated purchase intent with `OwnerWallet`, `PayerWallet`, `TotalAmount`. `OrderType { Quick, Multi }`, `OrderState { Pending, Completed, NotRegistered }`.
_Avoid_: purchase, transaction (generic)

**Invoice**:
A payable line tied to an order; synced to on-chain forward-sale contract fields.
_Avoid_: bill

**Locked Invoice**:
An invoice paid in a time-locked escrow mode on the same invoice contract: funds are held until maturity, then settle by the contract's approval/resolution rules. Whether the escrow accrues yield is decided by Earn Profit. The product/UI label is "Conditional Payment". Within a multi-step order, locking is all-or-nothing — every step is locked or none is. API: optional `isLocked` + lock fields on existing create DTOs; locked live detail fields are optional fields on `GetInvoiceDetailAsync`.
_Avoid_: Conditional Payment (in code/glossary), Escrow invoice

**Earn Profit**:
Yes/no on a Locked Invoice: when true, escrow accrues yield during the lock (with profit); when false, Resolution returns principal only (without profit). Chosen once for the whole order — same value on every step of a multi-step Locked Invoice. On-chain / API field is `earnProfit`. The product/UI label is "Profit Type" ("With Profit" / "Without Profit").
_Avoid_: ProfitType (as a glossary enum), interest, yield mode, Conditional Payment profit

**Approver**:
A wallet authorized to confirm a Locked Invoice. Approval is only part of the workflow when an optional third-party approver was set at creation; then **either** the payer or that third party can approve (not third-party-sole). The third party is always optional; when set on a multi-step order it is one address shared across all steps. When omitted, `Lock.ApproverWallet` stays null, no one needs approval work, approval endpoints do not list the invoice, and the invoice can release automatically at maturity without an approval event. There is no explicit reject: declining is simply not approving before maturity.
_Avoid_: Third party (alone), validator

**Lock Duration**:
The escrow term of a Locked Invoice (1/3/6/12/18/24 months, matching stake plans); fixes maturity (`lockedUntil`). Chosen per step in a multi-step order.
_Avoid_: Term, vesting

**Resolution**:
The terminal settlement of a Locked Invoice at maturity — **Released** (to the receiver) or **Refunded** (to the payer). Amount is principal plus yield when Earn Profit is true, or principal only when Earn Profit is false. With no third-party approver, maturity can release even if the payer never explicitly approved. With a third-party approver, either payer or third party approval releases; no approval refunds. Both surface on-chain as one `LockedInvoiceResolved` event, told apart by `beneficiary`.
_Avoid_: Release and Refund as two separate on-chain events

**Stake**:
Time-locked deposit with monthly profit per `StakeSetting` plan.
_Avoid_: deposit (generic banking)

**Withdrawal**:
Synced on-chain stake withdrawal or profit-withdrawn event.

**Wallet signature auth**:
Nonce + Web3 wallet sign → JWT (Nethereum `EthereumMessageSigner`). No password for customers.
_Avoid_: login, OAuth

**NetworkType**:
**BEP20** (BSC, ChainId 56) or **ERC20** (Ethereum). No TRC20.

**TransactionLog**:
Persisted on-chain log plus last-checked block per network.

## Framework

**Monjo**:
Custom MongoDB wrapper (`MonjoRepository<T>`, `IMonjoConnection`, `MonjoQuery`). Intentional spelling — never "Mongo".

**BaseDocument**, **Soft-delete**, **ApiResult**, **JWE**, **Signature gate**, **RegisterMode markers**, **\*Update/\*Result/\*Storage/\*Setting**, **Moment** — same conventions as sibling N-tier backends (see coinbank.api `CONTEXT.md` framework section if needed).
