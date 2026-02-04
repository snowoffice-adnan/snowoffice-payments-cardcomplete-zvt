using Snowoffice.Payments.CardCompleteZvt.Models;

namespace Snowoffice.Payments.CardCompleteZvt.Abstractions;

public interface ICardCompleteTerminalClient : IAsyncDisposable
{
    // Forward terminal messages to UI / VM
    event EventHandler<TerminalStatusEventArgs>? StatusInformationReceived;          // 04 0F
    event EventHandler<string>? IntermediateStatusInformationReceived;              // 04 FF text
    event EventHandler<string>? PrintLineReceived;                                  // 06 D1
    event EventHandler<CardCompleteReceiptBundle>? ReceiptUpdated;                  // receipts as they arrive

    bool IsConnected { get; }

    Task ConnectAsync(CardCompleteConnectionOptions options, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>Login/Registration (06 00)</summary>
    Task LoginAsync(CardCompleteRegistrationOptions options, CancellationToken ct = default);

    /// <summary>Purchase/Authorization (06 01)</summary>
    Task<CardCompletePaymentResult> PurchaseAsync(decimal amount, CancellationToken ct = default);

    /// <summary>Repeat receipt (06 20)</summary>
    Task<CardCompleteReceiptBundle> RepeatReceiptAsync(CancellationToken ct = default);

    /// <summary>End-of-day / closing (06 50)</summary>
    Task<CardCompleteReceiptBundle> EndOfDayAsync(CancellationToken ct = default);

    /// <summary>Abort (06 B0)</summary>
    Task AbortAsync(CancellationToken ct = default);

    void ResetBuffers();
}
