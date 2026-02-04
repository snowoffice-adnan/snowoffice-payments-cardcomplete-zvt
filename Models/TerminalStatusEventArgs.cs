namespace Snowoffice.Payments.CardCompleteZvt.Models;

public sealed class TerminalStatusEventArgs : EventArgs
{
    public int ResultCode { get; }
    public string? ErrorMessage { get; }
    public string? AdditionalText { get; }
    public decimal Amount { get; }
    public string? CardName { get; }
    public int ReceiptNumber { get; }

    public TerminalStatusEventArgs(
        int resultCode,
        string? errorMessage,
        string? additionalText,
        decimal amount,
        string? cardName,
        int receiptNumber)
    {
        ResultCode = resultCode;
        ErrorMessage = errorMessage;
        AdditionalText = additionalText;
        Amount = amount;
        CardName = cardName;
        ReceiptNumber = receiptNumber;
    }
}
