// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Cms;

namespace HyperPdfLibrary.Security;

/// <summary>The parts of a PDF MAC token's AuthenticatedData that checking needs, as slices of the token.</summary>
internal ref struct PdfMacParts
{
    /// <summary>Gets or sets the wrapped MAC key of the one PasswordRecipientInfo.</summary>
    internal ReadOnlySpan<byte> EncryptedKey { get; set; }

    /// <summary>Gets or sets the content bytes of the digest algorithm's object identifier.</summary>
    internal ReadOnlySpan<byte> DigestOid { get; set; }

    /// <summary>Gets or sets the digest algorithm.</summary>
    internal CmsDigestAlgorithm Digest { get; set; }

    /// <summary>Gets or sets the encapsulated content, a DER PdfMacIntegrityInfo.</summary>
    internal ReadOnlySpan<byte> Content { get; set; }

    /// <summary>Gets or sets the whole encoded <c>[2]</c> authenticated attributes, tag and length included.</summary>
    internal ReadOnlySpan<byte> AuthenticatedAttributes { get; set; }

    /// <summary>Gets or sets the MAC.</summary>
    internal ReadOnlySpan<byte> Mac { get; set; }
}
