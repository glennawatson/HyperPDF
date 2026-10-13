// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Signatures;

/// <summary>An RFC 3161 timestamp: on a signature, or a document timestamp.</summary>
/// <param name="Time">The time the authority stated.</param>
/// <param name="IsValid">Whether the token stamps the right data and its signature is good.</param>
/// <param name="Authority">The authority's certificate, or <see langword="null"/> when it cannot be found.</param>
/// <param name="Chain">The authority's chain, or <see langword="null"/> when there is no certificate.</param>
[DebuggerDisplay("PdfTimestampResult: {Time} valid {IsValid}")]
public sealed record PdfTimestampResult(DateTimeOffset Time, bool IsValid, X509Certificate2? Authority, PdfChainResult? Chain)
{
    /// <summary>
    /// Gets whether the token could be checked: <see cref="PdfCmsStatus.Unsupported"/> when its digest cannot be
    /// computed here and <see cref="PdfCmsStatus.DigestNotAllowed"/> when PDF does not allow it.
    /// </summary>
    public PdfCmsStatus Status { get; init; }
}
