namespace Snowoffice.Payments.CardCompleteZvt.Models;

public sealed class CardCompleteEodResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public CardCompleteReceiptBundle Receipts { get; set; } = new();
}
