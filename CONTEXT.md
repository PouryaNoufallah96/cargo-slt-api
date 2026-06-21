# SLT API (CargoPay)

Crypto invoice/order + staking backend on BSC and Ethereum (dual EVM). N-tier MongoDB via **Monjo**, wallet-signature auth, Nethereum. **No Tron** in this repo.

## Domain

**Order**:
A token-denominated purchase intent with `OwnerWallet`, `PayerWallet`, `TotalAmount`. `OrderType { Quick, Multi }`, `OrderState { Pending, Completed, NotRegistered }`.
_Avoid_: purchase, transaction (generic)

**Invoice**:
A payable line tied to an order; synced to on-chain forward-sale contract fields.
_Avoid_: bill

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
