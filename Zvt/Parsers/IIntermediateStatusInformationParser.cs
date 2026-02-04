namespace Snowoffice.Payments.CardCompleteZvt.Zvt.Parsers;

/// <summary>
/// IntermediateStatusInformationParser Interface
/// </summary>
public interface IIntermediateStatusInformationParser
{
    /// <summary>
    /// GetMessage
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    string GetMessage(Span<byte> data);
}