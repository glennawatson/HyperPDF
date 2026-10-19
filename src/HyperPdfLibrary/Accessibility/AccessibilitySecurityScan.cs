// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks the encryption permission ISO 14289-1 clause 7.16 requires: the 10th bit of <c>/P</c> is set.</summary>
internal static class AccessibilitySecurityScan
{
    /// <summary>The value of the 10th bit of the permission word, which lets assistive technology extract content (ISO 32000-1 Table 22).</summary>
    private const int AccessibilityBit = 1 << 9;

    /// <summary>Reports an encrypted file whose permission word forbids extraction for accessibility.</summary>
    /// <param name="document">The document.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="findings">Receives the finding.</param>
    internal static void Run(PdfDocument document, int? part, AccessibilityFindings findings)
    {
        if (part == PdfUaClaim.Part1 && document.Objects.Security is { } security && (security.Permissions & AccessibilityBit) == 0)
        {
            AccessibilityFindingCollection.Add(findings, PdfAccessibilityCode.SecurityBlocksAssistiveTechnology);
        }
    }
}
