// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Metadata;

/// <summary>The optional fields of one PDF Declaration claim, retaining missing, empty and invalid source values.</summary>
/// <param name="ClaimBy">The claimant's name, or null when absent.</param>
/// <param name="ClaimDate">The original XMP date text, or null when absent.</param>
/// <param name="ClaimCredentials">The claimant's credentials, or null when absent.</param>
/// <param name="ClaimReport">The report URL or embedded-file fragment, or null when absent.</param>
[DebuggerDisplay("PdfDeclarationClaim: {ClaimBy} on {ClaimDate}")]
public sealed record PdfDeclarationClaim(string? ClaimBy, string? ClaimDate, string? ClaimCredentials, string? ClaimReport);
