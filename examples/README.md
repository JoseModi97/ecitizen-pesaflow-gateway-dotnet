# eCitizen / PesaFlow Gateway - Multi-Platform Examples

This folder contains clean, self-contained examples demonstrating how to integrate the **`Ecitizen.PesaflowGateway`** and **`Ecitizen.PesaflowGateway.AspNetCore`** packages across various .NET platforms and architectural styles.

> **Security Note**: None of the examples include real merchant credentials. All examples use placeholder values (`YOUR_API_CLIENT_ID`, `YOUR_API_KEY`, etc.) or read from environment variables / `appsettings.json`.

---

## Directory Index

| Platform / Template | Description | Key Features |
|---|---|---|
| [`aspnetcore-minimal-api/`](./aspnetcore-minimal-api) | ASP.NET Core Minimal API (.NET 8 & 10) | `AddEcitizenPesaflowGateway()`, `MapEcitizenWebhook()`, Pay button generation, CSRF-safe IPN handling |
| [`aspnetcore-mvc/`](./aspnetcore-mvc) | ASP.NET Core MVC Controller (.NET 8 & 10) | `PaymentController`, `EcitizenWebhookHandler.ProcessAsync()`, View/Form submission |
| [`blazor-server/`](./blazor-server) | Blazor Interactive Web App (.NET 8 & 10) | Interactive checkout UI, status polling component, server-side DI |
| [`azure-functions-worker/`](./azure-functions-worker) | Azure Functions Isolated Worker (.NET 8 & 10) | Serverless HTTP triggers for checkout generation & IPN webhook callback processing |
| [`console-script/`](./console-script) | Lightweight Console Application (.NET 8 & 10) | CLI / batch script, direct server-to-server payment prompt, HMAC test verification |

---

## Quick Configuration Checklist

To test any example with your own merchant account, supply your credentials via either:

### 1. `appsettings.json` (Recommended for Web Apps)
```json
{
  "Ecitizen": {
    "ApiClientID": "YOUR_CLIENT_ID",
    "ApiKey": "YOUR_API_KEY",
    "Secret": "YOUR_MERCHANT_SECRET",
    "ServiceID": "YOUR_SERVICE_ID",
    "Url": "https://payments.ecitizen.go.ke/PaymentAPI/iframev2.1.php",
    "Currency": "KES"
  }
}
```

### 2. Environment Variables (Recommended for Cloud / Serverless / CI)
```bash
export ECITIZEN_CLIENT_ID="YOUR_CLIENT_ID"
export ECITIZEN_API_KEY="YOUR_API_KEY"
export ECITIZEN_SECRET="YOUR_MERCHANT_SECRET"
export ECITIZEN_SERVICE_ID="YOUR_SERVICE_ID"
export ECITIZEN_GATEWAY_URL="https://payments.ecitizen.go.ke/PaymentAPI/iframev2.1.php"
export ECITIZEN_CURRENCY="KES"
```
