namespace Snowoffice.Payments.CardCompleteZvt.Models;

public sealed class CardCompletePaymentResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public int ReceiptNumber { get; set; }

    public CardCompleteReceiptBundle Receipts { get; set; } = new();
}
