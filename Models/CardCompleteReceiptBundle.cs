namespace Snowoffice.Payments.CardCompleteZvt.Models;

public sealed class CardCompleteReceiptBundle
{
    public string MerchantReceipt { get; set; } = string.Empty;
    public string CardholderReceipt { get; set; } = string.Empty;

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(MerchantReceipt) &&
        string.IsNullOrWhiteSpace(CardholderReceipt);
}
