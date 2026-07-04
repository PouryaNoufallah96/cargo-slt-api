# Locked Invoice — Frontend Integration

**Conditional Payment** is a locked invoice mode inside the Order flow. The frontend creates the pending order through the Order API, then calls the invoice contract directly.

**Contracts:** BEP20 `0xbD48d27FA383B729ec455F16dEe7c30fd2e1989B` · ERC20 `0xd6d659da9F2Cf28dD53EAEE04aeFf61152B3c781`

Use the normal Order auth headers and the standard `ApiResult` envelope.

---

## Endpoints

| UI need | Endpoint |
|---------|----------|
| Create quick locked invoice | `POST /api/v1/Order/CreatePendingQuickOrder` |
| Create multi-step locked order | `POST /api/v1/Order/CreatePendingMultiStepOrder` |
| Order detail | `POST /api/v1/Order/GetOrderDetail` |
| Approval list | `POST /api/v1/Order/GetApprovalList` |
| Approval detail | `POST /api/v1/Order/GetApprovalDetail` |
| Approval report | `POST /api/v1/Order/GetApprovalReport` |

Use `GetOrderDetail` for the order detail screen. When an item in `invoices[]` has `isLocked === true`, read the optional locked fields from that invoice item.

---

## Create Quick

### Request

```http
POST /api/v1/Order/CreatePendingQuickOrder
```

```json
{
  "tokenSymbol": "LUSD",
  "amount": 100,
  "description": "Milestone payment",
  "isLocked": true,
  "lockDurationMonths": 3,
  "thirdPartyApprover": null
}
```

### Response

```json
{
  "isSuccess": true,
  "data": {
    "orderId": "ORD-1001",
    "ownerWallet": "0xOwner",
    "totalAmount": 100,
    "type": "Quick",
    "state": "NotRegistered",
    "isLocked": true,
    "invoices": [
      {
        "invoiceId": "0x1111111111111111111111111111111111111111111111111111111111111111",
        "tokenSymbol": "LUSD",
        "tokenAddress": "0xToken",
        "usdtAmount": 100,
        "usdtAmountInWei": "100000000",
        "activateDate": null,
        "isLocked": true,
        "lockDurationMonths": 3,
        "approverWallet": null
      }
    ],
    "ownershipType": "Owner"
  }
}
```

---

## Create Multi-Step

### Request

```http
POST /api/v1/Order/CreatePendingMultiStepOrder
```

```json
{
  "transportation": "Sea freight",
  "totalAmount": 300,
  "isLocked": true,
  "thirdPartyApprover": "0xApprover",
  "invoices": [
    {
      "tokenSymbol": "LUSD",
      "amount": 100,
      "description": "Step 1",
      "lockDurationMonths": 1,
      "activationDate": "2026-07-10"
    },
    {
      "tokenSymbol": "LUSD",
      "amount": 200,
      "description": "Step 2",
      "lockDurationMonths": 3,
      "activationDate": "2026-08-10"
    }
  ]
}
```

### Response

```json
{
  "isSuccess": true,
  "data": {
    "orderId": "ORD-2001",
    "ownerWallet": "0xOwner",
    "totalAmount": 300,
    "transportation": "Sea freight",
    "type": "Multi",
    "state": "NotRegistered",
    "isLocked": true,
    "invoices": [
      {
        "invoiceId": "0x2222222222222222222222222222222222222222222222222222222222222222",
        "tokenSymbol": "LUSD",
        "usdtAmount": 100,
        "usdtAmountInWei": "100000000",
        "activateDate": "2026-07-10T00:00:00",
        "isLocked": true,
        "lockDurationMonths": 1,
        "approverWallet": "0xApprover"
      },
      {
        "invoiceId": "0x3333333333333333333333333333333333333333333333333333333333333333",
        "tokenSymbol": "LUSD",
        "usdtAmount": 200,
        "usdtAmountInWei": "200000000",
        "activateDate": "2026-08-10T00:00:00",
        "isLocked": true,
        "lockDurationMonths": 3,
        "approverWallet": "0xApprover"
      }
    ],
    "ownershipType": "Owner"
  }
}
```

---

## Create Rules

| Field | Level | Locked rule |
|-------|-------|-------------|
| `isLocked` | quick/order | `true` for Conditional Payment |
| `lockDurationMonths` | quick/invoice item | required; one of `1`, `3`, `6`, `12`, `18`, `24` |
| `thirdPartyApprover` | quick/order | optional wallet address; `null` means payer can approve |

Do not send `lockDurationMonths` or `thirdPartyApprover` unless `isLocked: true`.

---

## Order Detail

### Request

```http
POST /api/v1/Order/GetOrderDetail
```

```json
{
  "orderOrTransferId": "ORD-2001"
}
```

### Response

```json
{
  "isSuccess": true,
  "data": {
    "orderId": "ORD-2001",
    "transferId": "TR-2001",
    "ownerWallet": "0xOwner",
    "payerWallet": "0xPayer",
    "totalAmount": 300,
    "transportation": "Sea freight",
    "type": "Multi",
    "state": "Pending",
    "isLocked": true,
    "ownershipType": "Owner",
    "invoices": [
      {
        "invoiceId": "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "ownerWallet": "0xOwner",
        "payerWallet": "0xPayer",
        "tokenSymbol": "LUSD",
        "usdtAmount": 100,
        "state": "Completed",
        "paymentHash": "0xPaymentHash",
        "isLocked": false,
        "ownershipType": "Owner"
      },
      {
        "invoiceId": "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
        "ownerWallet": "0xOwner",
        "payerWallet": "0xPayer",
        "tokenSymbol": "LUSD",
        "usdtAmount": 200,
        "state": "Locked",
        "isLocked": true,
        "lockDurationMonths": 3,
        "approverWallet": "0xApprover",
        "lockState": "Funded",
        "lockedUntilMoment": "2026-10-10T12:00:00Z",
        "principalAmount": 200,
        "principalAmountWei": "200000000",
        "livePayoutPreview": 204.9,
        "livePayoutPreviewWei": "204900000",
        "profitClaimed": 4.9,
        "profitClaimedWei": "4900000",
        "approved": false,
        "settled": false,
        "isCallerAuthorizedApprover": true,
        "ownershipType": "Owner"
      }
    ]
  }
}
```

Normal invoice items keep their usual fields. Locked invoice items add optional lock fields.

---

## Approval List

### Pending Request

```http
POST /api/v1/Order/GetApprovalList
```

```json
{
  "status": "Pending",
  "pagination": {
    "page": 1,
    "size": 25
  }
}
```

### Pending Response

```json
{
  "isSuccess": true,
  "data": {
    "totalCount": 1,
    "pageCount": 1,
    "data": [
      {
        "invoiceId": "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
        "ownerWallet": "0xOwner",
        "payerWallet": "0xPayer",
        "tokenSymbol": "LUSD",
        "usdtAmount": 100,
        "isLocked": true,
        "lockState": "Funded",
        "lockedUntilMoment": "2026-10-10T12:00:00Z",
        "isCallerAuthorizedApprover": true
      }
    ]
  }
}
```

### Done Request

```json
{
  "status": "Done",
  "pagination": {
    "page": 1,
    "size": 25
  }
}
```

`Pending` means actionable approvals: `lockState === Funded`.
`Done` includes `Approved`, `Released`, and `Refunded`.

---

## Approval Detail

### Request

```http
POST /api/v1/Order/GetApprovalDetail
```

```json
{
  "invoiceId": "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
}
```

### Response

```json
{
  "isSuccess": true,
  "data": {
    "invoiceId": "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
    "ownerWallet": "0xOwner",
    "payerWallet": "0xPayer",
    "tokenSymbol": "LUSD",
    "isLocked": true,
    "lockState": "Funded",
    "isCallerAuthorizedApprover": true
  }
}
```

Use this for locked invoices where the caller is payer or approver.

---

## Approval Report

### Request

```http
POST /api/v1/Order/GetApprovalReport
```

### Response

```json
{
  "isSuccess": true,
  "data": {
    "pendingApprovalCount": 2,
    "doneApprovalCount": 3,
    "approvalProgress": 60
  }
}
```

`approvalProgress = doneApprovalCount / (pendingApprovalCount + doneApprovalCount) * 100`, rounded to 2 decimals.

---

## States

**Invoice `state`:**

```
NotRegistered -> Locked -> Completed | Refunded
```

**Lock `lockState`:**

```
Created -> Funded -> Approved -> Released
                         -> Refunded
```

| `lockState` | UI |
|-------------|----|
| `Created` | owner registers on-chain |
| `Funded` | approval is actionable |
| `Approved` | approved, waiting for lock end / resolve |
| `Released` | owner received funds |
| `Refunded` | payer received refund |

Show **Approve** when `isCallerAuthorizedApprover === true` and `lockState === Funded`.

---

## Contract Calls

| Step | Caller | Call |
|------|--------|------|
| Register quick | owner | `createQuickLockedInvoice(id, receiver, token, usdAmount, lockDuration, approver)` |
| Register multi | owner | `createOrderedLockedInvoice(receiver, ids[], tokens[], usdAmounts[], unlockTimes[], lockDurations[], approvers[])` |
| Pay | payer | token `approve` + `payInvoice(invoiceId)` |
| Approve | payer or approver | `approveLockedInvoice(invoiceId)` |
| Resolve | contract/frontend path | refresh detail on `LockedInvoiceResolved` |

**API to contract mapping**

| API field | Contract value |
|-----------|----------------|
| `invoiceId` | invoice id |
| `tokenAddress` | token |
| `usdtAmountInWei` | usd amount |
| `activateDate` | `unlockTimes` Unix seconds |
| `lockDurationMonths` | lock duration |
| `approverWallet` | approver address, or zero address when null |

---

## SignalR

Hub: `/hubs/NotifyWallet` → `RegisterWallet`.

On `PaymentMessage` containing `Locked invoice ...`, refresh `GetOrderDetail`.

---

## Frontend Checklist

- [ ] Locked create sends `isLocked: true` and valid `lockDurationMonths`.
- [ ] Multi-step locked create sends `lockDurationMonths` on every invoice item.
- [ ] Detail screen uses `GetOrderDetail`.
- [ ] Locked UI reads optional lock fields from `invoices[]` only when `isLocked === true`.
- [ ] Approval tabs use `GetApprovalList` with `Pending` and `Done`.
- [ ] Approve button is gated by `isCallerAuthorizedApprover === true` and `lockState === Funded`.
- [ ] Terminal states stop live profit polling: `Released`, `Refunded`.
