namespace Snowoffice.Payments.CardCompleteZvt.Zvt.Responses;

public interface IResponseCardholderAuthentication
{
    string? CardholderAuthentication { get; set; }
    bool PrintoutNeeded { get; set; }
}
