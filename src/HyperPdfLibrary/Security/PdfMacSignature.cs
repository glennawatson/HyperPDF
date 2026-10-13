// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Finds a PDF MAC token that rides on a signature as an unsigned attribute of its first SignerInfo (ISO/TS 32004:2024 6.5.2).
/// </summary>
internal static class PdfMacSignature
{
    /// <summary>The context-specific tag number of the SignerInfo's unsigned attributes.</summary>
    private const int UnsignedAttributesTagNumber = 1;

    /// <summary>Gets the tag of the SignerInfo's <c>[1]</c> unsigned attributes.</summary>
    internal static Asn1Tag UnsignedAttributesTag { get; } = new(TagClass.ContextSpecific, UnsignedAttributesTagNumber, true);

    /// <summary>Finds the PDF MAC attribute in a signature container.</summary>
    /// <param name="contents">The signature's <c>/Contents</c>: a CMS ContentInfo, possibly followed by zero padding.</param>
    /// <param name="token">Receives where the token lies in <paramref name="contents"/> when found.</param>
    /// <param name="signature">Receives where the signer's signature value lies in <paramref name="contents"/> when found.</param>
    /// <returns>Whether the attribute was found, is absent, or the container could not be read.</returns>
    internal static PdfMacSignatureLookup Find(ReadOnlySpan<byte> contents, out Range token, out Range signature)
    {
        token = default;
        signature = default;
        try
        {
            return FindToken(contents, out token, out signature);
        }
        catch (AsnContentException)
        {
            return PdfMacSignatureLookup.Malformed;
        }
    }

    /// <summary>Walks to the first SignerInfo and reads its unsigned attributes.</summary>
    /// <param name="contents">The container.</param>
    /// <param name="token">Receives the token's range.</param>
    /// <param name="signature">Receives the signature value's range.</param>
    /// <returns>The lookup result.</returns>
    /// <exception cref="AsnContentException">The container is damaged.</exception>
    private static PdfMacSignatureLookup FindToken(ReadOnlySpan<byte> contents, out Range token, out Range signature)
    {
        token = default;
        signature = default;
        var source = contents;
        var contentInfo = CmsDer.ReadContents(ref source, Asn1Tag.Sequence);
        _ = CmsDer.ReadContents(ref contentInfo, Asn1Tag.ObjectIdentifier);
        var wrapper = CmsDer.ReadContents(ref contentInfo, CmsDer.Constructed0);
        var signedData = CmsDer.ReadContents(ref wrapper, Asn1Tag.Sequence);
        _ = CmsDer.ReadContents(ref signedData, Asn1Tag.Integer);
        _ = CmsDer.ReadContents(ref signedData, Asn1Tag.SetOf);
        _ = CmsDer.ReadContents(ref signedData, Asn1Tag.Sequence);
        while (!CmsDer.StartsWith(signedData, Asn1Tag.SetOf))
        {
            _ = CmsDer.ReadEncoded(ref signedData);
        }

        var signers = CmsDer.ReadContents(ref signedData, Asn1Tag.SetOf);
        var signer = CmsDer.ReadContents(ref signers, Asn1Tag.Sequence);
        _ = CmsDer.ReadContents(ref signer, Asn1Tag.Integer);
        _ = CmsDer.ReadEncoded(ref signer);
        _ = CmsDer.ReadEncoded(ref signer);
        if (CmsDer.StartsWith(signer, CmsDer.Constructed0))
        {
            _ = CmsDer.ReadEncoded(ref signer);
        }

        _ = CmsDer.ReadEncoded(ref signer);
        var value = CmsDer.ReadContents(ref signer, Asn1Tag.PrimitiveOctetString);
        signature = Locate(contents, value);
        if (!CmsDer.StartsWith(signer, UnsignedAttributesTag))
        {
            return PdfMacSignatureLookup.NotFound;
        }

        var attributes = CmsDer.ReadContents(ref signer, UnsignedAttributesTag);
        return FindAttribute(contents, attributes, out token);
    }

    /// <summary>Looks through the unsigned attributes for the PDF MAC data attribute.</summary>
    /// <param name="contents">The whole container, for locating the token.</param>
    /// <param name="attributes">The unsigned attributes' contents.</param>
    /// <param name="token">Receives the token's range.</param>
    /// <returns>Found when the attribute is there with exactly one value.</returns>
    /// <exception cref="AsnContentException">An attribute is damaged.</exception>
    private static PdfMacSignatureLookup FindAttribute(ReadOnlySpan<byte> contents, ReadOnlySpan<byte> attributes, out Range token)
    {
        token = default;
        while (!attributes.IsEmpty)
        {
            var attribute = CmsDer.ReadContents(ref attributes, Asn1Tag.Sequence);
            var oid = CmsDer.ReadContents(ref attribute, Asn1Tag.ObjectIdentifier);
            var values = CmsDer.ReadContents(ref attribute, Asn1Tag.SetOf);
            if (!PdfMacOids.Is(oid, PdfMacOids.MacData))
            {
                continue;
            }

            var value = CmsDer.ReadEncoded(ref values);
            token = Locate(contents, value);
            return values.IsEmpty ? PdfMacSignatureLookup.Found : PdfMacSignatureLookup.Malformed;
        }

        return PdfMacSignatureLookup.NotFound;
    }

    /// <summary>Gets where a slice lies in the container.</summary>
    /// <param name="contents">The container.</param>
    /// <param name="part">A slice of it.</param>
    /// <returns>The range.</returns>
    private static Range Locate(ReadOnlySpan<byte> contents, ReadOnlySpan<byte> part) =>
        contents.Overlaps(part, out var at) ? new Range(at, at + part.Length) : default;
}
