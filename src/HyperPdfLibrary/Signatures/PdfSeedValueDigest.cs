// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// The digests a signature field's seed value dictionary accepts: its /DigestMethod array (ISO 32000-2, 12.7.5.5,
/// Table 237, with the SHA3-256, SHA3-384, SHA3-512 and SHAKE256 names ISO/TS 32001:2022, 5.1.2 adds).
/// </summary>
/// <param name="Methods">The accepted digests in the file's order; names not recognised are <see cref="PdfDigestMethod.Unknown"/>.</param>
/// <param name="IsRequired">Whether the seed value's /Ff bit 7 makes the list a required constraint rather than a recommendation.</param>
[DebuggerDisplay("PdfSeedValueDigest: {Methods.Length} methods, required {IsRequired}")]
public sealed record PdfSeedValueDigest(PdfDigestMethod[] Methods, bool IsRequired);
