// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;

namespace HyperPdf.Cms;

/// <summary>
/// Finds the first signer of a CMS SignedData in its DER (RFC 5652, 5.1 and 5.3), for checks <c>SignedCms</c> cannot
/// make, such as SHAKE256 signatures.
/// </summary>
internal static class CmsSignerReader
{
    /// <summary>The <c>[0]</c> tag of explicit content, certificates and signed attributes.</summary>
    private static readonly Asn1Tag Context0 = new(TagClass.ContextSpecific, 0, true);

    /// <summary>Finds the first signer's parts.</summary>
    /// <param name="der">The CMS ContentInfo.</param>
    /// <param name="layout">Where the parts are.</param>
    /// <returns><see langword="true"/> when the container has a signer.</returns>
    internal static bool TryRead(ReadOnlySpan<byte> der, out CmsSignerLayout layout)
    {
        layout = default;
        if (!TryReadSignedData(der, out var signedData))
        {
            return false;
        }

        var offset = 0;
        if (!CmsDer.TryNext(signedData, ref offset, out _, out _, out _)
            || !CmsDer.TryNext(signedData, ref offset, out _, out _, out _)
            || !CmsDer.TryNext(signedData, ref offset, out var encapTag, out _, out var encapsulated)
            || encapTag != Asn1Tag.Sequence)
        {
            return false;
        }

        var content = ReadContent(der, encapsulated);
        return FindSignerInfos(signedData, offset, out var signers)
            && CmsDer.TryReadChild(signers, Asn1Tag.Sequence, out var signer)
            && TryReadSigner(der, signer, content, out layout);
    }

    /// <summary>Reads past ContentInfo to the SignedData's content.</summary>
    /// <param name="der">The ContentInfo.</param>
    /// <param name="signedData">The SignedData's content.</param>
    /// <returns><see langword="true"/> when found.</returns>
    private static bool TryReadSignedData(ReadOnlySpan<byte> der, out ReadOnlySpan<byte> signedData)
    {
        signedData = default;
        if (!CmsDer.TryReadChild(der, Asn1Tag.Sequence, out var contentInfo))
        {
            return false;
        }

        var offset = 0;
        return CmsDer.TryNext(contentInfo, ref offset, out _, out _, out _)
            && CmsDer.TryNext(contentInfo, ref offset, out var wrapperTag, out _, out var wrapper)
            && wrapperTag == Context0
            && CmsDer.TryReadChild(wrapper, Asn1Tag.Sequence, out signedData);
    }

    /// <summary>Reads the encapsulated content of a non-detached container.</summary>
    /// <param name="der">The container.</param>
    /// <param name="encapsulated">The EncapsulatedContentInfo's content.</param>
    /// <returns>Where the content's bytes are; empty when detached.</returns>
    private static Range ReadContent(ReadOnlySpan<byte> der, ReadOnlySpan<byte> encapsulated)
    {
        var offset = 0;
        return CmsDer.TryNext(encapsulated, ref offset, out _, out _, out _)
            && CmsDer.TryNext(encapsulated, ref offset, out var tag, out _, out var wrapper)
            && tag == Context0
            && CmsDer.TryReadChild(wrapper, Asn1Tag.PrimitiveOctetString, out var content)
            ? Locate(der, content)
            : default;
    }

    /// <summary>Skips the optional certificates and CRLs to the signerInfos SET.</summary>
    /// <param name="signedData">The SignedData's content.</param>
    /// <param name="offset">Where the values after the encapsulated content start.</param>
    /// <param name="signers">The SET's content.</param>
    /// <returns><see langword="true"/> when found.</returns>
    private static bool FindSignerInfos(ReadOnlySpan<byte> signedData, int offset, out ReadOnlySpan<byte> signers)
    {
        while (CmsDer.TryNext(signedData, ref offset, out var tag, out _, out signers))
        {
            if (tag == Asn1Tag.SetOf)
            {
                return true;
            }
        }

        signers = default;
        return false;
    }

    /// <summary>Reads a SignerInfo's parts.</summary>
    /// <param name="der">The container.</param>
    /// <param name="signer">The SignerInfo's content.</param>
    /// <param name="content">Where the encapsulated content is.</param>
    /// <param name="layout">The parts.</param>
    /// <returns><see langword="true"/> when the SignerInfo is complete.</returns>
    private static bool TryReadSigner(ReadOnlySpan<byte> der, ReadOnlySpan<byte> signer, Range content, out CmsSignerLayout layout)
    {
        layout = default;
        var offset = 0;
        if (!CmsDer.TryNext(signer, ref offset, out _, out _, out _)
            || !CmsDer.TryNext(signer, ref offset, out _, out _, out _)
            || !CmsDer.TryNext(signer, ref offset, out _, out var digestStart, out _))
        {
            return false;
        }

        var digest = Locate(der, signer[digestStart..offset]);
        if (!CmsDer.TryNext(signer, ref offset, out var tag, out var start, out _))
        {
            return false;
        }

        Range attributes = default;
        if (tag == Context0)
        {
            attributes = Locate(der, signer[start..offset]);
            if (!CmsDer.TryNext(signer, ref offset, out _, out start, out _))
            {
                return false;
            }
        }

        var algorithm = Locate(der, signer[start..offset]);
        if (!CmsDer.TryNext(signer, ref offset, out var signatureTag, out _, out var signature) || signatureTag != Asn1Tag.PrimitiveOctetString)
        {
            return false;
        }

        layout = new(digest, attributes, algorithm, Locate(der, signature), content);
        return true;
    }

    /// <summary>Gets where a slice of the container lies in it.</summary>
    /// <param name="der">The container.</param>
    /// <param name="part">A slice of it.</param>
    /// <returns>The range; empty for an empty slice.</returns>
    private static Range Locate(ReadOnlySpan<byte> der, ReadOnlySpan<byte> part) =>
        der.Overlaps(part, out var at) ? new Range(at, at + part.Length) : default;
}
