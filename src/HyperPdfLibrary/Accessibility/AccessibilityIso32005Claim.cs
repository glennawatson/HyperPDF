// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Metadata;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Reads the declaration of conformance to ISO/TS 32005 (clause 4, Table 1). A file that names ISO/TS 32005 in a declaration but not with the
/// address the standard gives is reported, because no reader would recognise the claim.
/// </summary>
internal static class AccessibilityIso32005Claim
{
    /// <summary>The address that declares conformance to ISO/TS 32005 (ISO/TS 32005:2023, Table 1).</summary>
    internal const string Uri = "https://pdfa.org/declarations#iso32005";

    /// <summary>The text that marks a declaration as aimed at ISO/TS 32005.</summary>
    private const string Marker = "iso32005";

    /// <summary>Reports each declaration that names ISO/TS 32005 with a wrong address.</summary>
    /// <param name="xmp">The catalog's parsed metadata, or <see langword="null"/> when there is none.</param>
    /// <param name="findings">Receives a finding for each malformed claim.</param>
    internal static void Run(XmpMetadata? xmp, AccessibilityFindings findings)
    {
        if (xmp is null)
        {
            return;
        }

        foreach (var declaration in xmp.Declarations)
        {
            if (IsMalformed(declaration.ConformsTo))
            {
                AccessibilityFindingCollection.Add(findings, PdfAccessibilityCode.Iso32005ClaimMalformed);
            }
        }
    }

    /// <summary>Determines whether an address names ISO/TS 32005 but is not the address the standard gives.</summary>
    /// <param name="conformsTo">The declaration's address, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it is a malformed claim.</returns>
    private static bool IsMalformed(string? conformsTo) =>
        conformsTo is not null && conformsTo.Contains(Marker, StringComparison.OrdinalIgnoreCase) && !string.Equals(conformsTo, Uri, StringComparison.Ordinal);
}
