using Snowoffice.Payments.CardCompleteZvt.Models;

namespace Snowoffice.Payments.CardCompleteZvt.Internal;

internal sealed class ReceiptBuffer
{
    private string _merchant = string.Empty;
    private string _cardholder = string.Empty;

    public void SetMerchant(string? content) => _merchant = content ?? string.Empty;
    public void SetCardholder(string? content) => _cardholder = content ?? string.Empty;

    public CardCompleteReceiptBundle Snapshot() => new()
    {
        MerchantReceipt = _merchant,
        CardholderReceipt = _cardholder
    };

    public void Reset()
    {
        _merchant = string.Empty;
        _cardholder = string.Empty;
    }
}
