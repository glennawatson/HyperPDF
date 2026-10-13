// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>
/// A hand-written DER encoder for the few ASN.1 types a PDF MAC token uses (X.690 clause 8 and 11). The tests build tokens
/// with it so they do not share the library's ASN.1 code.
/// </summary>
internal static class MacDer
{
    /// <summary>The universal tag of INTEGER.</summary>
    internal const byte IntegerTag = 0x02;

    /// <summary>The universal tag of OCTET STRING.</summary>
    internal const byte OctetStringTag = 0x04;

    /// <summary>The universal tag of NULL.</summary>
    internal const byte NullTag = 0x05;

    /// <summary>The universal tag of OBJECT IDENTIFIER.</summary>
    internal const byte OidTag = 0x06;

    /// <summary>The universal tag of constructed SEQUENCE.</summary>
    internal const byte SequenceTag = 0x30;

    /// <summary>The universal tag of constructed SET.</summary>
    internal const byte SetTag = 0x31;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in a base-128 digit.</summary>
    private const int SevenBits = 7;

    /// <summary>The longest length that fits in one byte.</summary>
    private const int ShortLengthLimit = 128;

    /// <summary>The first arc of an object identifier is multiplied by this.</summary>
    private const int FirstArcWeight = 40;

    /// <summary>The number of arcs folded into the first byte(s) of an object identifier.</summary>
    private const int FoldedArcs = 2;

    /// <summary>The continuation bit of a base-128 digit.</summary>
    private const int Continuation = 0x80;

    /// <summary>The tag byte class bits of a context-specific value.</summary>
    private const byte ContextClass = 0x80;

    /// <summary>The tag byte bit of a constructed value.</summary>
    private const byte ConstructedBit = 0x20;

    /// <summary>Encodes a value with a tag byte.</summary>
    /// <param name="tag">The tag byte.</param>
    /// <param name="parts">The pieces of the content, in order.</param>
    /// <returns>The tag, the length and the content.</returns>
    internal static byte[] Tlv(byte tag, params byte[][] parts)
    {
        var content = parts.SelectMany(static part => part).ToArray();
        return [tag, .. Length(content.Length), .. content];
    }

    /// <summary>Encodes a SEQUENCE.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The encoding.</returns>
    internal static byte[] Sequence(params byte[][] items) => Tlv(SequenceTag, items);

    /// <summary>Encodes a SET OF, ordering the items as DER requires (X.690 11.6).</summary>
    /// <param name="items">The items.</param>
    /// <returns>The encoding.</returns>
    internal static byte[] SetOf(params byte[][] items) => Tlv(SetTag, [.. items.Order(ByteComparer.Instance)]);

    /// <summary>Encodes an INTEGER that is small and not negative.</summary>
    /// <param name="value">The value, 0 to 127.</param>
    /// <returns>The encoding.</returns>
    internal static byte[] Integer(int value) => Tlv(IntegerTag, [(byte)value]);

    /// <summary>Encodes an OCTET STRING.</summary>
    /// <param name="value">The bytes.</param>
    /// <returns>The encoding.</returns>
    internal static byte[] Octets(byte[] value) => Tlv(OctetStringTag, value);

    /// <summary>Encodes NULL.</summary>
    /// <returns>The encoding.</returns>
    internal static byte[] Null() => [NullTag, 0];

    /// <summary>Encodes an OBJECT IDENTIFIER from its dotted form.</summary>
    /// <param name="dotted">The identifier, for example <c>1.2.840.113549.1.9.3</c>.</param>
    /// <returns>The encoding.</returns>
    internal static byte[] Oid(string dotted)
    {
        var arcs = dotted.Split('.').Select(static arc => BigInteger.Parse(arc, CultureInfo.InvariantCulture)).ToArray();
        List<byte> content = [.. Base128((arcs[0] * FirstArcWeight) + arcs[1])];
        foreach (var arc in arcs.Skip(FoldedArcs))
        {
            content.AddRange(Base128(arc));
        }

        return Tlv(OidTag, [.. content]);
    }

    /// <summary>Encodes a context-specific value, such as <c>[0]</c>.</summary>
    /// <param name="number">The tag number, under 31.</param>
    /// <param name="constructed">Whether the value is constructed.</param>
    /// <param name="parts">The content pieces.</param>
    /// <returns>The encoding.</returns>
    internal static byte[] Context(int number, bool constructed, params byte[][] parts) =>
        Tlv((byte)(ContextClass | (constructed ? ConstructedBit : 0) | number), parts);

    /// <summary>Encodes a definite length (X.690 8.1.3).</summary>
    /// <param name="length">The length.</param>
    /// <returns>The length octets.</returns>
    private static byte[] Length(int length)
    {
        if (length < ShortLengthLimit)
        {
            return [(byte)length];
        }

        var bytes = new List<byte>();
        for (var rest = length; rest > 0; rest >>= ByteBits)
        {
            bytes.Insert(0, (byte)rest);
        }

        return [(byte)(Continuation | bytes.Count), .. bytes];
    }

    /// <summary>Encodes a number as a big-endian base-128 value with continuation bits.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The octets.</returns>
    private static byte[] Base128(BigInteger value)
    {
        List<byte> digits = [(byte)(value & (Continuation - 1))];
        for (value >>= SevenBits; value > 0; value >>= SevenBits)
        {
            digits.Insert(0, (byte)(Continuation | (int)(value & (Continuation - 1))));
        }

        return [.. digits];
    }

    /// <summary>Compares byte arrays the way DER orders SET OF members: byte by byte, a prefix first.</summary>
    private sealed class ByteComparer : IComparer<byte[]>
    {
        /// <summary>Gets the one instance.</summary>
        internal static ByteComparer Instance { get; } = new();

        /// <inheritdoc/>
        public int Compare(byte[]? x, byte[]? y) => x.AsSpan().SequenceCompareTo(y);
    }
}
