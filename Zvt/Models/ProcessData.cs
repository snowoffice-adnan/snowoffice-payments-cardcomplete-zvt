using Snowoffice.Payments.CardCompleteZvt.Zvt.Responses;

namespace Snowoffice.Payments.CardCompleteZvt.Zvt.Models;

/// <summary>
/// a class describing the result of the IReceiveHandler's data processing
/// </summary>
public class ProcessData
{
    /// <summary>
    /// Current State of the data processing
    /// </summary>
    public ProcessDataState State { get; set; }

    /// <summary>
    /// Current State of the data processing
    /// </summary>
    public IResponse Response { get; set; } = null;
}