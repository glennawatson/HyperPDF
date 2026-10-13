// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Steps through BER-encoded CMS values in place: each read takes one value from the front of a span and returns its
/// contents as a slice, without copying. Damaged input throws <see cref="AsnContentException"/>.
/// </summary>
internal static class CmsDer
{
    /// <summary>The context-specific tag number of <c>suppPubInfo</c> in ECC-CMS-SharedInfo.</summary>
    private const int TagNumber2 = 2;

    /// <summary>Gets the tag of a <c>[0]</c> constructed value.</summary>
    internal static Asn1Tag Constructed0 { get; } = new(TagClass.ContextSpecific, 0, true);

    /// <summary>Gets the tag of a <c>[1]</c> constructed value.</summary>
    internal static Asn1Tag Constructed1 { get; } = new(TagClass.ContextSpecific, 1, true);

    /// <summary>Gets the tag of a <c>[2]</c> constructed value.</summary>
    internal static Asn1Tag Constructed2 { get; } = new(TagClass.ContextSpecific, TagNumber2, true);

    /// <summary>Gets the tag of a <c>[0]</c> primitive value.</summary>
    internal static Asn1Tag Primitive0 { get; } = new(TagClass.ContextSpecific, 0, false);

    /// <summary>Reads the value at the front of the source and returns its contents.</summary>
    /// <param name="source">The source, advanced past the value.</param>
    /// <param name="tag">The tag the value must have.</param>
    /// <returns>The contents.</returns>
    /// <exception cref="AsnContentException">The value is damaged or has another tag.</exception>
    internal static ReadOnlySpan<byte> ReadContents(scoped ref ReadOnlySpan<byte> source, Asn1Tag tag)
    {
        var actual = AsnDecoder.ReadEncodedValue(source, AsnEncodingRules.BER, out var offset, out var length, out var consumed);
        if (actual != tag)
        {
            throw new AsnContentException();
        }

        var contents = source.Slice(offset, length);
        source = source[consumed..];
        return contents;
    }

    /// <summary>Reads the value at the front of the source and returns all of its bytes, tag and length included.</summary>
    /// <param name="source">The source, advanced past the value.</param>
    /// <returns>The encoded value.</returns>
    /// <exception cref="AsnContentException">The value is damaged.</exception>
    internal static ReadOnlySpan<byte> ReadEncoded(scoped ref ReadOnlySpan<byte> source)
    {
        _ = AsnDecoder.ReadEncodedValue(source, AsnEncodingRules.BER, out _, out _, out var consumed);
        var encoded = source[..consumed];
        source = source[consumed..];
        return encoded;
    }

    /// <summary>Gets the tag of the value at the front of the source.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The tag.</returns>
    /// <exception cref="AsnContentException">The source does not start with a tag.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Asn1Tag PeekTag(ReadOnlySpan<byte> source) => Asn1Tag.Decode(source, out _);

    /// <summary>Determines whether the source starts with a value that has a tag.</summary>
    /// <param name="source">The source.</param>
    /// <param name="tag">The tag.</param>
    /// <returns><see langword="true"/> when the next value has the tag.</returns>
    /// <exception cref="AsnContentException">The source is not empty and does not start with a tag.</exception>
    internal static bool StartsWith(ReadOnlySpan<byte> source, Asn1Tag tag) => !source.IsEmpty && PeekTag(source) == tag;

    /// <summary>Reads an AlgorithmIdentifier: its OID contents and its parameters as encoded, empty when absent.</summary>
    /// <param name="source">The source, advanced past the value.</param>
    /// <param name="parameters">The encoded parameters, or an empty span.</param>
    /// <returns>The OID's contents.</returns>
    /// <exception cref="AsnContentException">The value is damaged.</exception>
    internal static ReadOnlySpan<byte> ReadAlgorithm(scoped ref ReadOnlySpan<byte> source, out ReadOnlySpan<byte> parameters)
    {
        var algorithm = ReadContents(ref source, Asn1Tag.Sequence);
        var oid = ReadContents(ref algorithm, Asn1Tag.ObjectIdentifier);
        parameters = algorithm.IsEmpty ? default : ReadEncoded(ref algorithm);
        return oid;
    }
}
