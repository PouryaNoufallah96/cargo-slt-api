# Locked Invoice — یکپارچه‌سازی فرانت

**Conditional Payment** حالت locked invoice داخل Order flow است. فرانت ابتدا pending order را از API می‌گیرد و سپس مستقیم contract invoice را صدا می‌زند.

**قرارداد:** BEP20 `0xbD48d27FA383B729ec455F16dEe7c30fd2e1989B` · ERC20 `0xd6d659da9F2Cf28dD53EAEE04aeFf61152B3c781`

همان auth headers و envelope استاندارد `ApiResult` استفاده می‌شود.

---

## Endpointها

| نیاز UI | Endpoint |
|---------|----------|
| create quick locked invoice | `POST /api/v1/Order/CreatePendingQuickOrder` |
| create multi-step locked order | `POST /api/v1/Order/CreatePendingMultiStepOrder` |
| order detail | `POST /api/v1/Order/GetOrderDetail` |
| approval list | `POST /api/v1/Order/GetApprovalList` |
| approval detail | `POST /api/v1/Order/GetApprovalDetail` |
| approval report | `POST /api/v1/Order/GetApprovalReport` |

برای صفحه detail سفارش از `GetOrderDetail` استفاده کنید. وقتی یکی از آیتم‌های `invoices[]` مقدار `isLocked === true` داشت، فیلدهای اختیاری lock را از همان invoice item بخوانید.

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

## قوانین Create

| Field | سطح | قانون locked |
|-------|-----|--------------|
| `isLocked` | quick/order | برای Conditional Payment برابر `true` |
| `lockDurationMonths` | quick/invoice item | اجباری؛ یکی از `1`, `3`, `6`, `12`, `18`, `24` |
| `thirdPartyApprover` | quick/order | اختیاری؛ `null` یعنی payer می‌تواند approve کند |

بدون `isLocked: true` فیلدهای `lockDurationMonths` و `thirdPartyApprover` را نفرستید.

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

invoice itemهای عادی همان فیلدهای معمول را دارند. invoice itemهای locked فیلدهای اختیاری lock را اضافه می‌کنند.

---

## Approval List

### Request برای Pending

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

### Response برای Pending

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

### Request برای Done

```json
{
  "status": "Done",
  "pagination": {
    "page": 1,
    "size": 25
  }
}
```

`Pending` یعنی approval قابل انجام: `lockState === Funded`.
`Done` شامل `Approved`، `Released` و `Refunded` است.

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

برای locked invoiceهایی استفاده شود که caller payer یا approver است.

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

`approvalProgress = doneApprovalCount / (pendingApprovalCount + doneApprovalCount) * 100` و تا 2 رقم اعشار round می‌شود.

---

## Stateها

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
| `Created` | owner روی chain ثبت می‌کند |
| `Funded` | approval قابل انجام است |
| `Approved` | approve شده و منتظر پایان lock / resolve |
| `Released` | owner پول را دریافت کرده |
| `Refunded` | payer refund گرفته |

دکمه **Approve** فقط وقتی نمایش داده شود که `isCallerAuthorizedApprover === true` و `lockState === Funded`.

---

## Contract Calls

| مرحله | caller | call |
|-------|--------|------|
| register quick | owner | `createQuickLockedInvoice(id, receiver, token, usdAmount, lockDuration, approver)` |
| register multi | owner | `createOrderedLockedInvoice(receiver, ids[], tokens[], usdAmounts[], unlockTimes[], lockDurations[], approvers[])` |
| pay | payer | token `approve` + `payInvoice(invoiceId)` |
| approve | payer یا approver | `approveLockedInvoice(invoiceId)` |
| resolve | contract/frontend path | روی `LockedInvoiceResolved`، detail را refresh کنید |

**Mapping از API به contract**

| API field | Contract value |
|-----------|----------------|
| `invoiceId` | invoice id |
| `tokenAddress` | token |
| `usdtAmountInWei` | usd amount |
| `activateDate` | `unlockTimes` به Unix seconds |
| `lockDurationMonths` | lock duration |
| `approverWallet` | آدرس approver، یا zero address وقتی null است |

---

## SignalR

Hub: `/hubs/NotifyWallet` -> `RegisterWallet`.

روی `PaymentMessage` که شامل `Locked invoice ...` است، `GetOrderDetail` را refresh کنید.

---

## Checklist فرانت

- [ ] create locked مقدار `isLocked: true` و `lockDurationMonths` معتبر می‌فرستد.
- [ ] در multi-step locked روی همه invoice itemها `lockDurationMonths` ارسال می‌شود.
- [ ] صفحه detail از `GetOrderDetail` استفاده می‌کند.
- [ ] UI مربوط به lock فقط وقتی `isLocked === true` است فیلدهای اختیاری lock را از `invoices[]` می‌خواند.
- [ ] تب‌های approval از `GetApprovalList` با `Pending` و `Done` استفاده می‌کنند.
- [ ] دکمه Approve با `isCallerAuthorizedApprover === true` و `lockState === Funded` کنترل می‌شود.
- [ ] در stateهای terminal، polling سود live متوقف می‌شود: `Released`، `Refunded`.
