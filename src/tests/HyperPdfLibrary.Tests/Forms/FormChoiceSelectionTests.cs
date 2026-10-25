// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Forms;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Checks choice selection duplicates and equivalent PDF text encodings.</summary>
public sealed class FormChoiceSelectionTests
{
    /// <summary>Selected indices must match every value, including its multiplicity.</summary>
    /// <param name="entries">The field entries.</param>
    /// <param name="expected">Whether the selected indices are consistent.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/Opt [(A) (A)] /I [0 1] /V [(A) (A)]", true)]
    [Arguments("/Opt [(A) (B)] /I [0 1] /V [(A) (A)]", false)]
    [Arguments("/Opt [(A) (B)] /I [0 1] /V [(B) (A)]", true)]
    [Arguments("/Opt [(A)] /I [0] /V <FEFF0041>", true)]
    [Arguments("/Opt [(A)] /I [0] /V <EFBBBF41>", true)]
    [Arguments("/Opt [(A)] /I [0] /V <FEFF0042>", false)]
    [Arguments("/Opt [(A)] /I [0] /V [42]", false)]
    [Arguments("/Opt [] /I [] /V []", true)]
    public async Task MatchesValuesAndIndices(string entries, bool expected)
    {
        using var document = FormTestPdf.Open(FormTestPdf.Helvetica, $"/FT /Ch /T (Choice) {entries}");

        await Assert.That(FormChoices.UsesSelectedIndices(FormTestPdf.Annotation(document))).IsEqualTo(expected);
    }
}
