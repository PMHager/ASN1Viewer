// Copyright (c) 2026 Peter-Michael Hager. All rights reserved.
// Proprietary and confidential. Shared solely for evaluation purposes.
// Intended to be released under an open-source license upon grant approval.

using System.Collections.Frozen;
using System.Formats.Asn1;
using System.Globalization;
using System.Text;
using static System.Math;

namespace Asn1View
{
    internal enum Class : byte
    {
        Universal = 0,                      // Defined in ITU-T X.680
        Application = 1,                    // Class for an outermost CHOICE of an application
        Context = 2,                        // Components of a SET, SEQUENCE, or CHOICE
        Private = 3,                        // Enterprise-specific extensions to standards
        invalid = byte.MaxValue
    }
    internal enum Form : byte
    {
        Primitive = 0,                      // Content octets directly encode an element value
        Constructed = 1,                    // Content octets contain tagged elements
        Indefinite = 2,                     // Content octets contain tagged elements terminated by an EndOfContents tag (EOC)
        invalid = byte.MaxValue
    }
    internal enum Universal : uint          // Tag assignments for the UNIVERSAL class in ITU-T X.680
    {
        EndOfContents = 0,                  // The reserved identifier for the End-of-Contents marker in an indefinite length encoding
        Boolean = 1,                        // The universal class tag value for Boolean: TRUE (FF) or FALSE (00)
        Integer = 2,                        // The universal class tag value for arbitrary precision Integer
        BitString = 3,                      // The universal class tag value for Bit String
        OctetString = 4,                    // The universal class tag value for Octet String (sequence of bytes)
        Null = 5,                           // The universal class tag value for Null
        ObjectIdentifier = 6,               // The universal class tag value for Object Identifier (numeric sequence representing an IRI)
        ObjectDescriptor = 7,               // The universal class tag value for Object Descriptor (human readable)
        External = 8,                       // The universal class tag value for External
        InstanceOf = External,              // The universal class tag value for Instance-Of
        Real = 9,                           // The universal class tag value for Real (Mantissa * Base^Exponent)
        Enumerated = 10,                    // The universal class tag value for Enumerated
        Embedded = 11,                      // The universal class tag value for Embedded-PDV (Presentation Data Value)
        UTF8String = 12,                    // The universal class tag value for UTF-8 String (RFC2044)
        RelativeObjectIdentifier = 13,      // The universal class tag value for Relative Object Identifier
        Time = 14,                          // The universal class tag value for Time
        Reserved15 = 15,                    // Reserved
        Sequence = 16,                      // The universal class tag value for constructed Sequence
        SequenceOf = Sequence,              // The universal class tag value for constructed Sequence-Of
        Set = 17,                           // The universal class tag value for constructed Set
        SetOf = Set,                        // The universal class tag value for constructed Set-Of
        NumericString = 18,                 // The universal class tag value for NumericString (digits only)
        PrintableString = 19,               // The universal class tag value for PrintableString
        T61String = 20,                     // The universal class tag value for T61String (TeletexString)
        TeletexString = T61String,          // The universal class tag value for TeletexString (T61String)
        VideotexString = 21,                // The universal class tag value for VideotexString
        IA5String = 22,                     // The universal class tag value for IA5String
        UtcTime = 23,                       // The universal class tag value for UTCTime
        GeneralizedTime = 24,               // The universal class tag value for GeneralizedTime
        GraphicString = 25,                 // The universal class tag value for GraphicString
        VisibleString = 26,                 // The universal class tag value for VisibleString (ISO646String)
        ISO646String = VisibleString,       // The universal class tag value for ISO646String (VisibleString)
        GeneralString = 27,                 // The universal class tag value for GeneralString
        UniversalString = 28,               // The universal class tag value for UniversalString
        UnrestrictedCharacterString = 29,   // The universal class tag value for an unrestricted character string
        BMPString = 30,                     // The universal class tag value for BMPString (Basic Multilingual Plane)
        Date = 31,                          // The universal class tag value for Date (YMD)
        TimeOfDay = 32,                     // The universal class tag value for Time-Of-Day (HMS Local-or-UTC)
        DateTime = 33,                      // The universal class tag value for Date-Time (YMD HMS Local-or-UTC)
        Duration = 34,                      // The universal class tag value for Duration of time
        ObjectIdentifierIRI = 35,           // The universal class tag value for Object Identifier Internationalized Resource Identifier (IRI)
        RelativeObjectIdentifierIRI = 36,   // The universal class tag value for Relative Object Identifier Internationalized Resource Identifier (IRI)
        invalid = uint.MaxValue
    }

    internal sealed partial class Asn
    {
        private Class _class;
        private Form _form;
        private uint _tag;
        private RawBytes _raw;
        private readonly List<Asn> _asns = [];
        private Asn? _parent;
        private string? _name;
        private string? _valueString;
        internal Class Class
        {
            get { return _class; }
        }
        internal Form Form
        {
            get { return _form; }
        }
        internal uint Tag
        {
            get { return _tag; }
        }
        internal Universal Universal
        {
            get { return _class == Class.Universal && _form != Form.invalid ? (Universal)_tag : Universal.invalid; }
        }
        internal RawBytes Raw
        {
            get { return _raw; }
            init { _raw = value; }
        }
        internal readonly struct RawBytes(
            ReadOnlyMemory<byte> tlv,
            ushort tagLength,
            ushort lengthLength,
            int offset)
        {
            internal ReadOnlyMemory<byte> Tlv => tlv;
            internal ReadOnlyMemory<byte> Tag => tlv[..tagLength];
            internal ReadOnlyMemory<byte> Length => tlv.Slice(tagLength, lengthLength);
            internal ReadOnlyMemory<byte> Value => tlv[(tagLength + lengthLength)..];
            internal int Offset => offset;
        };
        internal List<Asn> Asns
        {
            get { return _asns; }
        }
        internal Asn? Parent
        {
            get { return _parent; }
        }
        internal string Name
        {
            get
            {
                if (_name is null)
                {
                    ReadOnlySpan<byte> span = _raw.Value.Span;
                    _name = _class switch
                    {
                        Class.Universal => (Universal)_tag switch
                        {
                            Universal.EndOfContents => "End Of Contents",
                            Universal.Boolean => "BOOLEAN",
                            Universal.Integer => $"INTEGER{span.ToBigInteger().GetBitLength().ToString(CultureInfo.InvariantCulture).ToSuperscript()}",
                            Universal.BitString => $"BIT STRING{(2 > span.Length || 7 < span[0] ? 0 : 8 * (span.Length - 1) - span[0]).ToString().ToSuperscript()}",
                            Universal.OctetString => $"OCTET STRING{span.Length.ToString().ToSuperscript()}",
                            Universal.Null => "NULL",
                            Universal.ObjectIdentifier => "OBJECT IDENTIFIER",
                            Universal.ObjectDescriptor => "ObjectDescriptor",
                            Universal.External => "EXTERNAL",
                            Universal.Real => "REAL",
                            Universal.Enumerated => "ENUMERATED",
                            Universal.Embedded => "EMBEDDED PDV",
                            Universal.UTF8String => "UTF8String",
                            Universal.RelativeObjectIdentifier => "RELATIVE-OID",
                            Universal.Time => "TIME",
                            Universal.Sequence => "SEQUENCE",
                            Universal.Set => "SET",
                            Universal.NumericString => "NumericString",
                            Universal.PrintableString => "PrintableString",
                            Universal.T61String => "T61String",
                            Universal.VideotexString => "VideotexString",
                            Universal.IA5String => "IA5String",
                            Universal.UtcTime => "UTCTime",
                            Universal.GeneralizedTime => "GeneralizedTime",
                            Universal.GraphicString => "GraphicString",
                            Universal.VisibleString => "VisibleString",
                            Universal.GeneralString => "GeneralString",
                            Universal.UniversalString => "UniversalString",
                            Universal.UnrestrictedCharacterString => "CHARACTER STRING",
                            Universal.BMPString => "BMPString",
                            Universal.Date => "DATE",
                            Universal.TimeOfDay => "TIME-OF-DAY",
                            Universal.DateTime => "DATE-TIME",
                            Universal.Duration => "DURATION",
                            Universal.ObjectIdentifierIRI => "OID-IRI",
                            Universal.RelativeObjectIdentifierIRI => "RELATIVE-OID-IRI",
                            _ => string.Format($"[UNIVERSAL {Tag}]")
                        },
                        Class.Application => string.Format($"[APPLICATION {Tag}]"),
                        Class.Context => _form switch
                        {
                            Form.Primitive => string.Format($"[{Tag}] IMPLICIT{span.Length.ToString().ToSuperscript()}"),
                            Form.Constructed or Form.Indefinite => string.Format($"[{Tag}] EXPLICIT"),
                            _ => string.Format($"[CONTEXT {Tag}]")
                        },
                        Class.Private => string.Format($"[PRIVATE {Tag}]"),
                        _ => string.Empty
                    };
                }
                return _name;
            }
            set
            {
                _name = value;
            }
        }
        internal string ValueString
        {
            get
            {
                if (_valueString is null)
                {
                    if (_raw.Tlv.IsEmpty)
                    {
                        return _valueString = string.Empty;
                    }
                    ReadOnlySpan<byte> value = _raw.Value.Span;
                    ReadOnlySpan<byte> tlv = _raw.Tlv.Span;
                    try
                    {
                        _valueString = _class switch
                        {
                            Class.Universal => (Universal)_tag switch
                            {
                                Universal.EndOfContents => Encoding.Latin1.GetString(value),
                                Universal.Boolean => 1 == value.Length ? AsnDecoder.ReadBoolean(tlv, AsnEncodingRules.BER, out _) ? "TRUE" : "FALSE" : "INVALID",
                                Universal.Integer => IntegerToString(value),
                                Universal.BitString => BitStringToString(value),
                                Universal.OctetString => OctetsToString(value),
                                Universal.Null => string.Empty,
                                Universal.ObjectIdentifier => OidToString(value),
                                Universal.ObjectDescriptor => Encoding.Latin1.GetString(value).WrapInQuotes(),
                                Universal.External => string.Empty,
                                Universal.Real => Encoding.Latin1.GetString(value).WrapInQuotes(),
                                Universal.Enumerated => IntegerToString(value),
                                Universal.Embedded => Encoding.Latin1.GetString(value).WrapInQuotes(),
                                Universal.UTF8String => AsnDecoder.ReadCharacterString(tlv, AsnEncodingRules.BER, UniversalTagNumber.UTF8String, out _).WrapInQuotes(),
                                Universal.RelativeObjectIdentifier => Encoding.Latin1.GetString(value).WrapInQuotes(),
                                Universal.Time => Encoding.Latin1.GetString(value).WrapInParentheses(),
                                Universal.Sequence => string.Empty,
                                Universal.Set => string.Empty,
                                Universal.NumericString => AsnDecoder.ReadCharacterString(tlv, AsnEncodingRules.BER, UniversalTagNumber.NumericString, out _).WrapInQuotes(),
                                Universal.PrintableString => AsnDecoder.ReadCharacterString(tlv, AsnEncodingRules.BER, UniversalTagNumber.PrintableString, out _).WrapInQuotes(),
                                Universal.T61String => AsnDecoder.ReadCharacterString(tlv, AsnEncodingRules.BER, UniversalTagNumber.T61String, out _).WrapInQuotes(),
                                Universal.VideotexString => Encoding.Latin1.GetString(value).WrapInQuotes(),
                                Universal.IA5String => AsnDecoder.ReadCharacterString(tlv, AsnEncodingRules.BER, UniversalTagNumber.IA5String, out _).WrapInQuotes(),
                                Universal.UtcTime => AsnDecoder.ReadUtcTime(tlv, AsnEncodingRules.BER, out _).LocalDateTime.ToString(CultureInfo.CurrentCulture).WrapInParentheses(),
                                Universal.GeneralizedTime => AsnDecoder.ReadGeneralizedTime(tlv, AsnEncodingRules.BER, out _).LocalDateTime.ToString(CultureInfo.CurrentCulture).WrapInParentheses(),
                                Universal.GraphicString => Encoding.Latin1.GetString(value).WrapInQuotes(),
                                Universal.VisibleString => AsnDecoder.ReadCharacterString(tlv, AsnEncodingRules.BER, UniversalTagNumber.VisibleString, out _).WrapInQuotes(),
                                Universal.GeneralString => Encoding.Latin1.GetString(value).WrapInQuotes(),
                                Universal.UniversalString => AsnDecoder.ReadCharacterString(tlv, AsnEncodingRules.BER, UniversalTagNumber.UniversalString, out _).WrapInQuotes(),
                                // Universal.UniversalString => Encoding.GetEncoding(12001).GetString(span),
                                Universal.UnrestrictedCharacterString => value.DetectEncoding().GetString(value).WrapInQuotes(),
                                Universal.BMPString => AsnDecoder.ReadCharacterString(tlv, AsnEncodingRules.BER, UniversalTagNumber.BMPString, out _).WrapInQuotes(),
                                // Universal.BMPString => Encoding.BigEndianUnicode.GetString(span),
                                Universal.Date => Encoding.Latin1.GetString(value).WrapInParentheses(),
                                Universal.TimeOfDay => Encoding.Latin1.GetString(value).WrapInParentheses(),
                                Universal.DateTime => Encoding.Latin1.GetString(value).WrapInParentheses(),
                                Universal.Duration => Encoding.Latin1.GetString(value).WrapInParentheses(),
                                Universal.ObjectIdentifierIRI => Encoding.Latin1.GetString(value).WrapInQuotes(),
                                Universal.RelativeObjectIdentifierIRI => Encoding.Latin1.GetString(value).WrapInQuotes(),
                                _ => Encoding.Latin1.GetString(value).WrapInQuotes()
                            },
                            Class.Application => Encoding.Latin1.GetString(value).WrapInQuotes(),
                            Class.Context => _form switch
                            {
                                Form.Primitive => OctetsToString(value),
                                Form.Constructed or Form.Indefinite => string.Empty,
                                _ => string.Empty
                            },
                            Class.Private => _form switch
                            {
                                Form.Primitive => Encoding.Latin1.GetString(value).WrapInQuotes(),
                                Form.Constructed or Form.Indefinite => string.Empty,
                                _ => string.Empty
                            },
                            _ => string.Empty
                        };
                    }
                    catch (Exception ex) when (ex is AsnContentException or ArgumentException)
                    {
                        _valueString = $"INVALID: '{value.ToHex()}'H>";
                    }
                }
                return _valueString;
            }
            set
            {
                _valueString = value;
            }
        }
        // internal int TotalAsnCount => _asns.Count + _asns.Sum(asn => asn.TotalAsnCount); // Annotation: 25% slower
        internal int AsnsTotalCount
        {
            get
            {
                int count = _asns.Count;
                foreach (Asn asn in _asns)
                {
                    count += asn.AsnsTotalCount;
                }
                return count;
            }
        }
        internal bool IsConstructed
        {
            get
            {
                return Form.Constructed == _form || Form.Indefinite == _form;
            }
        }
        internal bool IsEqual(Class Class,
                              Form Form,
                              uint Tag)
        {
            return Class == _class && (Form == _form || Form == Form.Constructed && Form.Indefinite == _form) && Tag == this.Tag;
        }
        internal bool IsUniversal(Form Form,
                                  Universal Universal)
        {
            return Class.Universal == _class && Form == _form && Universal == (Universal)_tag;
        }
        internal bool IsUniversal(Form Form,
                                IReadOnlySet<Universal> Universal) // HashSet & FrozenSet
        {
            return Class.Universal == _class && Form == _form && Universal.Contains((Universal)_tag);
        }
        internal Asn(byte[] octets)
        {
            int offset = 0;
            int length = octets.Length;
            Decode(octets, ref offset, ref length, parent: null);
        }
        internal Asn(ReadOnlySpan<byte> octets) : this(octets.ToArray())
        {
        }
        private Asn(byte[] octets, ref int offset, ref int length, Asn? parent)
        {
            Decode(octets, ref offset, ref length, parent);
        }
        private void Decode(byte[] octets, ref int offset, ref int length, Asn? parent)
        {
            int tlvOffset = offset;
            length = Min(length, octets.Length - offset);
            Item item = new(octets, ref offset, ref length);
            if (item.Valid)
            {
                _class = item.Class;
                _form = item.Form;
                _tag = item.Tag;
                _raw = new(
                    octets.AsMemory(tlvOffset, item.ValueLength + item.ValueOffset - tlvOffset),
                    (ushort)(item.LengthOffset - tlvOffset),
                    (ushort)(item.ValueOffset - item.LengthOffset),
                    tlvOffset);
                _parent = parent;
                bool isBitString = false;
                if (IsConstructed
                || (IsUniversal(Form.Primitive, Universal.OctetString) && 1 < item.ValueLength)
                || (isBitString = IsUniversal(Form.Primitive, Universal.BitString) && 1 < item.ValueLength && 0 == octets[item.ValueOffset])
                || (true && _class == Class.Context && _form == Form.Primitive && 1 < item.ValueLength))
                {
                    if (isBitString)
                    {
                        item.ValueOffset++;
                        item.ValueLength--;
                    }
                    while (0 < item.ValueLength)
                    {
                        int startValueOffset = item.ValueOffset;
                        Asn asn = new(octets, ref item.ValueOffset, ref item.ValueLength, this);
                        if (item.ValueOffset <= startValueOffset
                        || asn.Class == Class.invalid
                        || asn.Universal == Universal.EndOfContents && 0 < item.ValueLength
                        || asn.IsConstructed && asn.Asns.Count == 0 && !asn.Raw.Value.IsEmpty)
                        {
                            Asns.Clear();
                            break;
                        }
                        Asns.Add(asn);
                    }
                }
            }
            else
            {
                _class = Class.invalid;
                _form = Form.invalid;
                _tag = (uint)Universal.invalid;
                _raw = new(Memory<byte>.Empty, 0, 0, tlvOffset);
            }
        }
        public void Add(Asn asn)
        {
            _asns.Add(asn);
            asn._parent = this;
        }
        private static string IntegerToString(ReadOnlySpan<byte> span)
        {
            return $"({span.ToBigInteger():R})";
        }
        private static string BitStringToString(ReadOnlySpan<byte> span)
        {
            if (2 > span.Length || 7 < span[0])
                return string.Empty;
            Span<byte> bits = new byte[3 + 8 * (span.Length - 1) - span[0]];
            int i, n, b = 0;
            bits[b++] = (byte)'\'';
            for (i = 1; i < span.Length - 1; i++)
                for (n = 7; n >= 0; n--)
                    bits[b++] = (byte)('0' + (1 & (span[i] >> n)));
            for (n = 7; n >= span[0]; n--)
                bits[b++] = (byte)('0' + (1 & (span[i] >> n)));
            bits[b++] = (byte)'\'';
            bits[b++] = (byte)'B';
            return Encoding.ASCII.GetString(bits);
        }
        private static string OctetsToString(ReadOnlySpan<byte> span)
        {
            return span.Length == 1 && span[0] == 0x00 ? "FALSE"
                 : span.Length == 1 && span[0] == 0xFF ? "TRUE"
                 : span.All(b => 31 < b && b < 128) ? Encoding.Latin1.GetString(span).WrapInQuotes()
                 : $"'{span.ToHex()}'H";
        }
        private static string OidToString(ReadOnlySpan<byte> span)
        {
            const ulong ASN_DATA_FREE = 0xFE00000000000000;
            const byte ASN_LOOP_FLAG = 0x80;
            const byte ASN_DATA_MASK = 0x7F;
            const byte ASN_DATA_LSHL = 7;
            const byte CCITT = 0;
            const byte ISO = 1;
            const byte JOINT = 2;
            List<ulong> oid = [];
            ulong sid;
            int i = 0;
            while (i < span.Length)
            {
                sid = (ulong)(span[i] & ASN_DATA_MASK);        // Load first subidentifier octet
                while (0 != (span[i++] & ASN_LOOP_FLAG))
                {
                    if (i >= span.Length || 0 != (sid & ASN_DATA_FREE))
                        return $"INVALID: '{span.ToHex()}'H";
                    sid <<= ASN_DATA_LSHL;
                    sid += (ulong)(span[i] & ASN_DATA_MASK);   // Load ext. subidentifier octets
                }
                if (0 < oid.Count)
                    oid.Add(sid);               // Third and above subidentifiers
                else if (sid < 40)
                    oid = [CCITT, sid];         // First subidentifier {itu-t(0)} and second subidentifier
                else if (sid < 80)
                    oid = [ISO, sid - 40];      // First subidentifier {iso(1)} and second subidentifier
                else
                    oid = [JOINT, sid - 80];    // First  subidentifier {joint-iso-itu-t(2)} and second subidentifier
            }
            return string.Join(".", oid);
        }
        private record struct Item
        {
            private readonly bool _valid;
            private readonly Class _class;
            private readonly Form _form;
            private readonly uint _tag;
            internal int ValueOffset;
            internal int LengthOffset;
            internal int ValueLength;
            internal readonly bool Valid
            {
                get { return _valid; }
            }
            internal readonly Class Class
            {
                get { return _valid ? _class : Class.invalid; }
            }
            internal readonly Form Form
            {
                get { return _valid ? _form : Form.invalid; }
            }
            internal readonly uint Tag
            {
                get { return _valid ? _tag : (uint)Universal.invalid; }
            }
            private static readonly FrozenSet<Universal> StrictlyPrimitiveTags =
            [
                Universal.EndOfContents, Universal.Boolean, Universal.Integer, Universal.Null,
                Universal.ObjectIdentifier, Universal.ObjectDescriptor, Universal.Real, Universal.Enumerated,
                Universal.RelativeObjectIdentifier, Universal.Time, Universal.UtcTime, Universal.GeneralizedTime,
                Universal.Date, Universal.TimeOfDay, Universal.DateTime, Universal.Duration,
                Universal.ObjectIdentifierIRI, Universal.RelativeObjectIdentifierIRI
            ];
            private static readonly HashSet<Universal> StrictlyConstructedTags =
            [
                Universal.External,
                Universal.Sequence,
                Universal.Set
            ];
            internal Item(ReadOnlySpan<byte> octets,
                        ref int offset,
                        ref int length)
            {
                const byte ASN_CLAS_MASK = 0xC0;
                const byte ASN_FORM_MASK = 0x20;
                const byte ASN_TAG1_MASK = 0x1F;
                const byte ASN_CLAS_LSHR = 6;
                const byte ASN_FORM_LSHR = 5;
                const byte ASN_LOOP_FLAG = 0x80;
                const byte ASN_DATA_MASK = 0x7F;
                const byte ASN_TAGX_LSHL = 7;
                const byte ASN_LENX_LSHL = 8;
                const uint ASN_TAGX_FREE = 0x01FFFFFF;
                const uint ASN_LENX_FREE = 0x007FFFFF;
                int i, c;
                int ttlLen = Min(offset + length, octets.Length);
                ValueOffset = offset;
                if (ttlLen < ValueOffset + 2)
                    return;
                _class = (Class)((octets[ValueOffset] & ASN_CLAS_MASK) >> ASN_CLAS_LSHR);
                _form = (Form)((octets[ValueOffset] & ASN_FORM_MASK) >> ASN_FORM_LSHR);
                _tag = (uint)(octets[ValueOffset++] & ASN_TAG1_MASK);
                if (ASN_TAG1_MASK == _tag)
                {
                    _tag = 0;
                    do
                    {
                        if (ttlLen <= ValueOffset || ASN_TAGX_FREE < _tag)
                            return;
                        _tag <<= ASN_TAGX_LSHL;
                        _tag += (uint)(octets[ValueOffset] & ASN_DATA_MASK);
                    }
                    while (0 != (octets[ValueOffset++] & ASN_LOOP_FLAG));
                }
                if (ttlLen <= ValueOffset || offset + 128 < ValueOffset)
                    return;
                LengthOffset = ValueOffset;
                ValueLength = octets[ValueOffset++];
                if (ValueLength > ASN_DATA_MASK)
                {
                    c = ValueLength & ASN_DATA_MASK;
                    if (0 < c)
                    {
                        ValueLength = 0;
                        for (i = 0; i < c; i++)
                        {
                            if (ttlLen <= ValueOffset || ASN_LENX_FREE < ValueLength)
                                return;
                            ValueLength <<= ASN_LENX_LSHL;
                            ValueLength += octets[ValueOffset++];
                        }
                    }
                    else if (Form.Constructed == _form)
                    {
                        int subOfs = ValueOffset;
                        int subLen = ttlLen - offset;
                        Item item;
                        do
                        {
                            item = new(octets, ref subOfs, ref subLen);
                        }
                        while (item.Valid && Universal.EndOfContents != (Universal)item.Tag);
                        if (!item.Valid || 0 != item.ValueLength)
                            return;
                        ValueLength = subOfs - ValueOffset;
                        _form = Form.Indefinite;
                    }
                    else
                        return;
                }
                if (ttlLen < ValueOffset + ValueLength)
                    return;
                if (_class == Class.Universal)
                {
                    if (_form == Form.Primitive)
                    {
                        if (StrictlyConstructedTags.Contains((Universal)_tag)
                        || ((Universal)_tag == Universal.Null && ValueLength != 0)
                        || ((Universal)_tag == Universal.Boolean && ValueLength != 1))
                            return;
                    }
                    else if (StrictlyPrimitiveTags.Contains((Universal)_tag))
                        return;
                }
                offset = ValueOffset + ValueLength;
                length = ttlLen - offset;
                _valid = true;
                return;
            }
        }
    }
}
