# CardComplete ZVT .NET Client

A production-ready .NET client library for CardComplete payment terminals using the ZVT protocol over TCP/IP.

This library provides a safe, kiosk-friendly abstraction over the ZVT protocol, including transaction handling, receipt capture, intermediate status events, watchdog handling, and full evidence logging (TX/RX, receipts) required for certification, audits, and dispute resolution.

Designed for Windows kiosks, POS, and hospitality systems.

---

## Features

- TCP/IP communication with CardComplete ZVT terminals
- Registration / Login (06 00)
- Purchase transactions (06 01)
- Repeat last receipt (06 20)
- End of Day / Settlement (06 50)
- Abort command (06 B0)
- Intermediate status messages (04 FF)
- Final status information (04 0F)
- Receipt capture (merchant + cardholder)
- Full transport evidence logging (TX / RX hex dump)
- Automatic evidence folder per transaction
- T4 watchdog for stuck terminals
- Cancellation support via CancellationToken
- No UI dependencies (pure library)

---

## Target Framework

- **.NET 8.0**
- Windows
- Compatible with:
  - Console applications
  - WPF / WinUI 3
  - Windows Services
  - Kiosk applications

---

## Installation

### Via NuGet (local or private feed)

```bash
dotnet add package Snowoffice.Payments.CardComplete.Zvt
```

---

## Basic Usage

```csharp
using Snowoffice.Hosbooking.Payments.CardCompleteZvt;
using Snowoffice.Hosbooking.Payments.CardCompleteZvt.Models;

var terminal = new CardCompleteTerminalClient();

await terminal.ConnectAsync(new CardCompleteConnectionOptions
{
    IpAddress = "10.0.0.251",
    Port = 20007,
    EnableKeepAlive = true
});

await terminal.LoginAsync(new CardCompleteRegistrationOptions
{
    Password = 123456,
    Language = CardCompleteLanguage.English
});

var result = await terminal.PurchaseAsync(1.00m);

if (result.Success)
{
    // payment successful
}
```

---

## Evidence Logging (BELEG / LOGFILE)

For **every operation**, the library can create an evidence directory containing:
- `transport.log` – full TX/RX hex dump
- `receipt_merchant.txt`
- `receipt_cardholder.txt`

## Start Evidence Manually

```csharp
terminal.StartEvidence("purchase_0601");

Console.WriteLine(terminal.CurrentEvidenceDirectory);
```

## Evidence Location (default)

```csharp
%LOCALAPPDATA%\KIOSK\cardcomplete\evidence\
└── 20240118_142233_purchase_0601_XXXXXXXX
    ├── transport.log
    ├── receipt_merchant.txt
    └── receipt_cardholder.txt
```

This structure is suitable for:
- CardComplete certification
- Test case documentation
- Legal dispute evidence
- Support diagnostics

---

## Events

```csharp
terminal.IntermediateStatusInformationReceived += (_, msg) =>
{
    // 04 FF messages (e.g. "Please insert card")
};

terminal.StatusInformationReceived += (_, status) =>
{
    // 04 0F final status
};

terminal.PrintLineReceived += (_, line) =>
{
    // Lines printed by terminal
};

terminal.ReceiptUpdated += (_, receipts) =>
{
    // Merchant / cardholder receipts updated
};
```

---

## Cancellation & Watchdog

All operations support cancellation via `CancellationToken`.
The library includes a **T4 watchdog** to detect terminals that stop responding during a transaction.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
await terminal.PurchaseAsync(1.00m, cts.Token);
```

---

## Recommended Usage Pattern (Kiosk / POS)
1. Connect
2. Register/Login
3. Start evidence
4. Execute transaction
5. Save receipts
6. Disconnect

```CONNECT → 0600 → 0601 → RECEIPTS → DISCONNECT```

---

## Logging Integration

The library uses `Microsoft.Extensions.Logging`.

You can plug in:
- Serilog
- NLog
- Application Insights
- File / EventLog

Example with Serilog:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/cardcomplete-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
```

---

## License

MIT License

© Snowoffice
