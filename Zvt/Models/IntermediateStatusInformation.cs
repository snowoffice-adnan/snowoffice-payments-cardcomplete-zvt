using Snowoffice.Payments.CardCompleteZvt.Zvt.Responses;

namespace Snowoffice.Payments.CardCompleteZvt.Zvt.Models;

public class IntermediateStatusInformation : IResponse, IResponseErrorMessage
{
    public string ErrorMessage { get; set; }
}
