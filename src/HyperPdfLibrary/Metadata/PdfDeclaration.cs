// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace HyperPdfLibrary.Metadata;

/// <summary>A claim in one metadata packet, not proof; document scope excludes embedded and referenced content, and object scope is limited to that object.</summary>
/// <param name="ConformsTo">The profile URI text, or null when missing in damaged input.</param>
/// <param name="ClaimData">The associated claims, in source order despite the unordered array semantics.</param>
[DebuggerDisplay("PdfDeclaration: {ConformsTo}")]
public sealed record PdfDeclaration(string? ConformsTo, ImmutableArray<PdfDeclarationClaim> ClaimData);
