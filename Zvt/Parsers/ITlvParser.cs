using Snowoffice.Payments.CardCompleteZvt.Zvt.Models;
using Snowoffice.Payments.CardCompleteZvt.Zvt.Responses;

namespace Snowoffice.Payments.CardCompleteZvt.Zvt.Parsers;

public interface ITlvParser
{
    bool Parse(byte[] data, IResponse response);
    TlvLengthInfo GetLength(Span<byte> data);

}