using Snowoffice.Payments.CardCompleteZvt.Zvt.Models;

namespace Snowoffice.Payments.CardCompleteZvt.Zvt.Parsers;

/// <summary>
/// PrintLineParser Interface
/// </summary>
public interface IPrintLineParser
{
    /// <summary>
    /// Parse
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    PrintLineInfo Parse(Span<byte> data);
}