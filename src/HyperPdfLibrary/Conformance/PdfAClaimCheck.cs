// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>What <see cref="PdfAClaimGuard"/> found when it checked a document's PDF/A claim before a save.</summary>
/// <param name="Level">The level the document claimed.</param>
/// <param name="Removed">
/// Whether the claim was removed because the document no longer meets the level. Show the user a warning when it was,
/// with the rules in <paramref name="Broken"/>.
/// </param>
/// <param name="Broken">The rules the document breaks; empty when the claim was kept.</param>
[DebuggerDisplay("PdfAClaimCheck: {Level}, removed {Removed}, {Broken.Length} rules broken")]
public sealed record PdfAClaimCheck(PdfALevel Level, bool Removed, PdfARuleResult[] Broken);
