using Snowoffice.Payments.CardCompleteZvt.Zvt.Responses;

namespace Snowoffice.Payments.CardCompleteZvt.Zvt.Models;

public class Abort : IResponse,
    IResponseErrorCode,
    IResponseErrorMessage
{
    public byte ErrorCode { get; set; }
    public string ErrorMessage { get; set; }
}
