// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks annotation classification without retained lookup tables.</summary>
public sealed class PdfAConversionAnnotationTypesTests
{
    /// <summary>Shared annotation types keep their classification across every PDF/A part.</summary>
    /// <param name="subtype">The annotation subtype.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Text")]
    [Arguments("Link")]
    [Arguments("FreeText")]
    [Arguments("Line")]
    [Arguments("Square")]
    [Arguments("Circle")]
    [Arguments("Highlight")]
    [Arguments("Underline")]
    [Arguments("Squiggly")]
    [Arguments("StrikeOut")]
    [Arguments("Stamp")]
    [Arguments("Ink")]
    [Arguments("Popup")]
    [Arguments("Widget")]
    [Arguments("PrinterMark")]
    [Arguments("TrapNet")]
    public async Task IsAllowed_CommonType_PreservesEveryPart(string subtype)
    {
        await Assert.That(PdfAConversionAnnotationTypes.IsAllowed(PdfALevel.PdfA1B, subtype)).IsTrue();
        await Assert.That(PdfAConversionAnnotationTypes.IsAllowed(PdfALevel.PdfA2B, subtype)).IsTrue();
        await Assert.That(PdfAConversionAnnotationTypes.IsAllowed(PdfALevel.PdfA3B, subtype)).IsTrue();
        await Assert.That(PdfAConversionAnnotationTypes.IsAllowed(PdfALevel.PdfA4, subtype)).IsTrue();
    }

    /// <summary>Later markup types stay excluded from PDF/A-1.</summary>
    /// <param name="subtype">The annotation subtype.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Polygon")]
    [Arguments("PolyLine")]
    [Arguments("Caret")]
    [Arguments("Watermark")]
    [Arguments("Redact")]
    public async Task IsAllowed_LaterMarkup_KeepsPart1Exclusion(string subtype)
    {
        await Assert.That(PdfAConversionAnnotationTypes.IsAllowed(PdfALevel.PdfA1B, subtype)).IsFalse();
        await Assert.That(PdfAConversionAnnotationTypes.IsAllowed(PdfALevel.PdfA2B, subtype)).IsTrue();
        await Assert.That(PdfAConversionAnnotationTypes.IsAllowed(PdfALevel.PdfA3B, subtype)).IsTrue();
        await Assert.That(PdfAConversionAnnotationTypes.IsAllowed(PdfALevel.PdfA4, subtype)).IsTrue();
    }

    /// <summary>Profile-specific and unknown types preserve their existing restrictions.</summary>
    /// <param name="level">The PDF/A level.</param>
    /// <param name="subtype">The annotation subtype.</param>
    /// <param name="expected">The expected classification.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(PdfALevel.PdfA1B, "FileAttachment", false)]
    [Arguments(PdfALevel.PdfA2B, "FileAttachment", true)]
    [Arguments(PdfALevel.PdfA3B, "FileAttachment", true)]
    [Arguments(PdfALevel.PdfA4, "FileAttachment", false)]
    [Arguments(PdfALevel.PdfA4F, "FileAttachment", true)]
    [Arguments(PdfALevel.PdfA4E, "FileAttachment", false)]
    [Arguments(PdfALevel.PdfA2B, "Projection", false)]
    [Arguments(PdfALevel.PdfA4, "Projection", true)]
    [Arguments(PdfALevel.PdfA4, "3D", false)]
    [Arguments(PdfALevel.PdfA4E, "3D", true)]
    [Arguments(PdfALevel.PdfA4, "RichMedia", false)]
    [Arguments(PdfALevel.PdfA4E, "RichMedia", true)]
    [Arguments(PdfALevel.PdfA4, "Movie", false)]
    [Arguments(PdfALevel.PdfA4, "text", false)]
    [Arguments(PdfALevel.PdfA4, "", false)]
    public async Task IsAllowed_ProfileSpecificType_PreservesRestrictions(PdfALevel level, string subtype, bool expected) =>
        await Assert.That(PdfAConversionAnnotationTypes.IsAllowed(level, subtype)).IsEqualTo(expected);
}
