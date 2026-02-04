using Snowoffice.Payments.CardCompleteZvt.Zvt.Models;

namespace Snowoffice.Payments.CardCompleteZvt.Zvt.Parsers;

/// <summary>
/// StatusInformationParser Interface
/// </summary>
public interface IStatusInformationParser
{
    /// <summary>
    /// Parse
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    StatusInformation Parse(Span<byte> data);
}