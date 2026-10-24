// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Checks every markup subtype without a retained lookup table.</summary>
public sealed class PdfFlattenSelectionTests
{
    /// <summary>Markup-only flattening keeps the complete standard subtype set.</summary>
    /// <param name="name">The annotation subtype.</param>
    /// <param name="markup">Whether the subtype is markup.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments((int)KnownName.Text, true)]
    [Arguments((int)KnownName.FreeText, true)]
    [Arguments((int)KnownName.Line, true)]
    [Arguments((int)KnownName.Square, true)]
    [Arguments((int)KnownName.Circle, true)]
    [Arguments((int)KnownName.Polygon, true)]
    [Arguments((int)KnownName.PolyLine, true)]
    [Arguments((int)KnownName.Highlight, true)]
    [Arguments((int)KnownName.Underline, true)]
    [Arguments((int)KnownName.Squiggly, true)]
    [Arguments((int)KnownName.StrikeOut, true)]
    [Arguments((int)KnownName.Caret, true)]
    [Arguments((int)KnownName.Stamp, true)]
    [Arguments((int)KnownName.Ink, true)]
    [Arguments((int)KnownName.FileAttachment, true)]
    [Arguments((int)KnownName.Sound, true)]
    [Arguments((int)KnownName.Redact, true)]
    [Arguments((int)KnownName.Widget, false)]
    [Arguments((int)KnownName.Link, false)]
    [Arguments((int)KnownName.Popup, false)]
    [Arguments((int)KnownName.PrinterMark, false)]
    public async Task Decide_MarkupOnly_PreservesSubtypeSet(int name, bool markup)
    {
        var annotation = new PdfDictionary(null);
        annotation.Set(KnownName.Subtype, PdfValue.FromName((KnownName)name));
        var options = new PdfFlattenOptions { Targets = PdfFlattenTargets.MarkupOnly };
        var decision = PdfFlattenSelection.Decide(options, annotation);

        await Assert.That(decision == PdfFlattenDecision.OutOfScope).IsEqualTo(!markup);
    }
}
