using Microsoft.Extensions.Logging;
using Snowoffice.Payments.CardCompleteZvt.Zvt.Helpers;
using Snowoffice.Payments.CardCompleteZvt.Zvt.Models;
using Snowoffice.Payments.CardCompleteZvt.Zvt.Responses;

namespace Snowoffice.Payments.CardCompleteZvt.Zvt.Parsers;

/// <summary>
/// TlvParser
/// </summary>
public class TlvParser : ITlvParser
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, TlvInfo> _tlvInfos;

    public TlvParser(ILogger logger, TlvInfo[]? tlvInfos = null)
    {
        _logger = logger;

        _tlvInfos = new Dictionary<string, TlvInfo>(StringComparer.OrdinalIgnoreCase);

        if (tlvInfos is { Length: > 0 })
        {
            foreach (var tlvInfo in tlvInfos)
            {
                if (string.IsNullOrWhiteSpace(tlvInfo.Tag))
                {
                    _logger.LogWarning($"{nameof(TlvParser)} - Skipping TLV info with empty Tag");
                    continue;
                }

                if (_tlvInfos.ContainsKey(tlvInfo.Tag))
                {
                    throw new NotSupportedException($"Cannot add tlvInfo {tlvInfo.Tag} (duplicate key)");
                }

                _tlvInfos.Add(tlvInfo.Tag, tlvInfo);
            }
        }
    }

    /// <summary>
    /// Parse a TLV container
    /// </summary>
    public bool Parse(byte[] data, IResponse? response)
    {
        var lengthInfo = GetLength(data);
        if (!lengthInfo.Successful)
        {
            _logger.LogError($"{nameof(Parse)} - Cannot detect length of TLV Container");
            return false;
        }

        if (data.Length < lengthInfo.NumberOfBytesThatCanBeSkipped + lengthInfo.Length)
        {
            _logger.LogError($"{nameof(Parse)} - TLV data shorter than declared length");
            return false;
        }

        var tlvData = data.AsSpan().Slice(lengthInfo.NumberOfBytesThatCanBeSkipped, lengthInfo.Length);
        return ParseInternal(tlvData, response);
    }

    private bool ParseInternal(Span<byte> data, IResponse? response)
    {
        while (data.Length > 0)
        {
            var tagFieldInfo = GetTagFieldInfo(data);
            if (tagFieldInfo is null || string.IsNullOrEmpty(tagFieldInfo.Tag))
            {
                _logger.LogError($"{nameof(ParseInternal)} - Cannot parse tag field info");
                return false;
            }

            data = data.Slice(tagFieldInfo.NumberOfBytesThatCanBeSkipped);

            var tlvLengthInfo = GetLength(data);
            data = data.Slice(tlvLengthInfo.NumberOfBytesThatCanBeSkipped);

            if (!tlvLengthInfo.Successful)
            {
                _logger.LogError($"{nameof(ParseInternal)} - Cannot detect the TLV length");
                return false;
            }

            if (data.Length < tlvLengthInfo.Length)
            {
                _logger.LogError($"{nameof(ParseInternal)} - Corrupt TLV data length for tag:{tagFieldInfo.Tag}");
                return false;
            }

            var valuePart = data.Slice(0, tlvLengthInfo.Length);
            data = data.Slice(tlvLengthInfo.Length);

            // Primitive: process value
            if (tagFieldInfo.DataObjectType == TlvTagFieldDataObjectType.Primitive)
            {
                _ = ProcessTlvInfoAction(tagFieldInfo.Tag, valuePart, response);
                continue;
            }

            // Constructed: process block + recurse
            if (tagFieldInfo.DataObjectType == TlvTagFieldDataObjectType.Constructed)
            {
                _ = ProcessTlvInfoAction(tagFieldInfo.Tag, valuePart, response);

                if (!ParseInternal(valuePart, response))
                {
                    return false;
                }

                continue;
            }

            return false;
        }

        return true;
    }

    private bool ProcessTlvInfoAction(string tag, Span<byte> data, IResponse? response)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        if (!_tlvInfos.TryGetValue(tag, out var tlvInfo))
        {
            return false;
        }

        // If no action, it’s fine
        if (tlvInfo.TryProcess is null)
        {
            _logger.LogDebug($"{nameof(ProcessTlvInfoAction)} - No action defined for Tag:{tag}");
            return true;
        }

        // If response is null, we cannot invoke processors that require response
        if (response is null)
        {
            _logger.LogDebug($"{nameof(ProcessTlvInfoAction)} - Response is null; skipping TryProcess for Tag:{tag}");
            return true;
        }

        // invoke
        return tlvInfo.TryProcess(data.ToArray(), response);
    }

    public TlvTagFieldInfo? GetTagFieldInfo(Span<byte> data)
    {
        if (data.Length == 0)
        {
            return null;
        }

        var tagFieldInfo = new TlvTagFieldInfo
        {
            Tag = string.Empty,
            TagNumber = 0,
            NumberOfBytesThatCanBeSkipped = 0
        };

        var isFirstByte = true;

        foreach (var b in data)
        {
            var bits = BitHelper.GetBits(b);

            if (isFirstByte)
            {
                isFirstByte = false;

                if (bits.Bit7 && bits.Bit6)
                    tagFieldInfo.ClassType = TlvTagFieldClassType.PrivateClass;
                else if (bits.Bit7 && !bits.Bit6)
                    tagFieldInfo.ClassType = TlvTagFieldClassType.ContextSpecificClass;
                else if (!bits.Bit7 && bits.Bit6)
                    tagFieldInfo.ClassType = TlvTagFieldClassType.ApplicationClass;
                else
                    tagFieldInfo.ClassType = TlvTagFieldClassType.UniversalClass;

                tagFieldInfo.DataObjectType = bits.Bit5
                    ? TlvTagFieldDataObjectType.Constructed
                    : TlvTagFieldDataObjectType.Primitive;

                // Multi-byte tag marker (0x1F)
                if (bits.Bit4 && bits.Bit3 && bits.Bit2 && bits.Bit1 && bits.Bit0)
                {
                    tagFieldInfo.Tag += $"{b:X2}";
                    tagFieldInfo.NumberOfBytesThatCanBeSkipped++;
                    continue;
                }

                tagFieldInfo.TagNumber += NumberHelper.BoolArrayToInt(bits.Bit0, bits.Bit1, bits.Bit2, bits.Bit3);
                tagFieldInfo.Tag += $"{b:X2}";
                tagFieldInfo.NumberOfBytesThatCanBeSkipped++;
                break;
            }

            // Additional tag bytes
            if (bits.Bit7)
            {
                tagFieldInfo.TagNumber += NumberHelper.BoolArrayToInt(bits.Bit0, bits.Bit1, bits.Bit2, bits.Bit3, bits.Bit4, bits.Bit5, bits.Bit6);
                tagFieldInfo.Tag += $"{b:X2}";
                tagFieldInfo.NumberOfBytesThatCanBeSkipped++;
                continue;
            }

            // Last tag byte
            tagFieldInfo.TagNumber += NumberHelper.BoolArrayToInt(bits.Bit0, bits.Bit1, bits.Bit2, bits.Bit3, bits.Bit4, bits.Bit5, bits.Bit6);
            tagFieldInfo.Tag += $"{b:X2}";
            tagFieldInfo.NumberOfBytesThatCanBeSkipped++;
            break;
        }

        return tagFieldInfo;
    }

    public TlvLengthInfo GetLength(Span<byte> data)
    {
        if (data.Length == 0)
        {
            return new TlvLengthInfo { Successful = false };
        }

        var bits = BitHelper.GetBits(data[0]);

        if (!bits.Bit7)
        {
            var length = NumberHelper.BoolArrayToInt(bits.Bit0, bits.Bit1, bits.Bit2, bits.Bit3, bits.Bit4, bits.Bit5, bits.Bit6);
            return new TlvLengthInfo { Successful = true, Length = length, NumberOfBytesThatCanBeSkipped = 1 };
        }

        // 0x80 invalid
        if (!bits.Bit0 && !bits.Bit1 && !bits.Bit2 && !bits.Bit3 && !bits.Bit4 && !bits.Bit5 && !bits.Bit6 && bits.Bit7)
        {
            _logger.LogInformation($"{nameof(GetLength)} - Invalid value");
            return new TlvLengthInfo { Successful = false };
        }

        // 0x81 1 byte length
        if (bits.Bit0 && !bits.Bit1 && !bits.Bit2 && !bits.Bit3 && !bits.Bit4 && !bits.Bit5 && !bits.Bit6 && bits.Bit7)
        {
            if (data.Length < 2)
            {
                _logger.LogWarning($"{nameof(GetLength)} - Not enough bytes available");
                return new TlvLengthInfo { Successful = false };
            }

            return new TlvLengthInfo { Successful = true, Length = data[1], NumberOfBytesThatCanBeSkipped = 2 };
        }

        // 0x82 2 byte length
        if (!bits.Bit0 && bits.Bit1 && !bits.Bit2 && !bits.Bit3 && !bits.Bit4 && !bits.Bit5 && !bits.Bit6 && bits.Bit7)
        {
            if (data.Length < 3)
            {
                _logger.LogWarning($"{nameof(GetLength)} - Not enough bytes available");
                return new TlvLengthInfo { Successful = false };
            }

            var length = NumberHelper.ToInt16LittleEndian(data.Slice(1, 2));
            if (length < 0)
            {
                _logger.LogWarning($"{nameof(GetLength)} - Negative length detected {length}");
                return new TlvLengthInfo { Successful = false };
            }

            return new TlvLengthInfo { Successful = true, Length = length, NumberOfBytesThatCanBeSkipped = 3 };
        }

        _logger.LogWarning($"{nameof(GetLength)} - RFU - Reserved for future use");
        return new TlvLengthInfo { Successful = false };
    }
}
