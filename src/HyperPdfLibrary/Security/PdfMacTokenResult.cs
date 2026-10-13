// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Cms;

namespace HyperPdfLibrary.Security;

/// <summary>What checking a PDF MAC token found.</summary>
/// <param name="Status">Whether the token's structure and MAC hold.</param>
/// <param name="Digest">The digest algorithm of the token, or unknown when it could not be read.</param>
/// <param name="DataDigest">Where the <c>dataDigest</c> lies in the token; empty unless the status is valid.</param>
/// <param name="SignatureDigest">Where the <c>signatureDigest</c> lies in the token; empty when the token has none.</param>
[DebuggerDisplay("PdfMacTokenResult: {Status}")]
internal readonly record struct PdfMacTokenResult(PdfMacTokenStatus Status, CmsDigestAlgorithm Digest, Range DataDigest, Range SignatureDigest)
{
    /// <summary>Gets a value indicating whether the token has a <c>signatureDigest</c>.</summary>
    internal bool HasSignatureDigest => !SignatureDigest.Equals(default);

    /// <summary>Creates a failed result.</summary>
    /// <param name="status">The status.</param>
    /// <param name="digest">The digest, when it was read.</param>
    /// <returns>The result.</returns>
    internal static PdfMacTokenResult Failed(PdfMacTokenStatus status, CmsDigestAlgorithm digest) => new(status, digest, default, default);
}
