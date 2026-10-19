// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks the mapping from report findings to WCAG 2.2 success criteria.</summary>
public sealed class PdfAccessibilityWcagTests
{
    /// <summary>The number of codes the mapping lists.</summary>
    private const int MappedCodes = 27;

    /// <summary>Well-known findings map to the criteria WCAG names for them.</summary>
    /// <param name="code">The finding's code.</param>
    /// <param name="expected">The criteria, separated by spaces.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfAccessibilityCode.FigureNoDescription, "1.1.1")]
    [Arguments(PdfAccessibilityCode.NoLanguage, "3.1.1")]
    [Arguments(PdfAccessibilityCode.NoTitle, "2.4.2")]
    [Arguments(PdfAccessibilityCode.FormFieldNoLabel, "1.3.1 3.3.2 4.1.2")]
    [Arguments(PdfAccessibilityCode.StructureOccurrenceViolation, "1.3.1")]
    [Arguments(PdfAccessibilityCode.FontNoUnicodeMap, "")]
    [Arguments(PdfAccessibilityCode.None, "")]
    public async Task CodesMapToCriteria(PdfAccessibilityCode code, string expected)
    {
        var criteria = PdfAccessibilityWcag.GetSuccessCriteria(code);

        await Assert.That(string.Join(' ', criteria)).IsEqualTo(expected);
    }

    /// <summary>Every code the mapping lists can be found, which also shows the rows are in order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryMappedCodeIsFound()
    {
        var mapped = 0;
        foreach (var code in Enum.GetValues<PdfAccessibilityCode>())
        {
            if (!PdfAccessibilityWcag.GetSuccessCriteria(code).IsEmpty)
            {
                mapped++;
            }
        }

        await Assert.That(mapped).IsEqualTo(MappedCodes);
    }
}
