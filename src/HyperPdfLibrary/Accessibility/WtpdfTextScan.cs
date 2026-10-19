// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks that the human readable text strings of a tag do not use the Unicode private use area (WTPDF 1.0, clause 8.6).</summary>
internal static class WtpdfTextScan
{
    /// <summary>The first code point of the private use area of the basic plane.</summary>
    private const int PrivateUseStart = 0xE000;

    /// <summary>The last code point of the private use area of the basic plane.</summary>
    private const int PrivateUseEnd = 0xF8FF;

    /// <summary>The first code point of supplementary private use area A.</summary>
    private const int SupplementaryAStart = 0xF0000;

    /// <summary>The last code point of supplementary private use area B.</summary>
    private const int SupplementaryBEnd = 0x10FFFD;

    /// <summary>The last code point of supplementary private use area A, which is the last assigned one before area B starts.</summary>
    private const int SupplementaryAEnd = 0xFFFFD;

    /// <summary>The first code point of supplementary private use area B.</summary>
    private const int SupplementaryBStart = 0x100000;

    /// <summary>Reports a tag whose <c>Alt</c>, <c>ActualText</c>, <c>E</c> or <c>T</c> uses private use characters.</summary>
    /// <param name="element">The tag.</param>
    /// <param name="findings">Receives the finding.</param>
    internal static void Check(PdfStructureElement element, List<PdfWtpdfFinding> findings)
    {
        if (!HasPrivateUse(element.AlternateText) && !HasPrivateUse(element.ActualText) && !HasPrivateUse(element.Expansion) && !HasPrivateUse(element.Title))
        {
            return;
        }

        findings.Add(new(PdfWtpdfProfile.Reuse, "8.6", "Text meant to be read by people cannot use Unicode private use characters.", element.Id.Number));
    }

    /// <summary>Determines whether a string holds a private use code point.</summary>
    /// <param name="text">The string, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasPrivateUse(string? text)
    {
        if (text is null)
        {
            return false;
        }

        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value is >= PrivateUseStart and <= PrivateUseEnd or >= SupplementaryAStart and <= SupplementaryAEnd or >= SupplementaryBStart and <= SupplementaryBEnd)
            {
                return true;
            }
        }

        return false;
    }
}
