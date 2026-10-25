// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Tests.Writing;

namespace HyperPdfLibrary.Tests.Fonts.Embedding;

/// <summary>Checks documents with embedded fonts with <c>qpdf --check</c>, an independent reader of the objects and streams the writer made.</summary>
public sealed class EmbeddedFontQpdfTests
{
    /// <summary>The licence flag that forbids subsetting.</summary>
    private const int NoSubsetting = 0x0100;

    /// <summary>A line of text with capitals, a composite glyph and a space.</summary>
    private const string Line = "Hello World é";

    /// <summary>Documents with subset and whole fonts, TrueType and CFF, pass the qpdf check.</summary>
    /// <param name="cff">Whether the font has CFF outlines.</param>
    /// <param name="subset">Whether the font is cut down to the glyphs used.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false, true)]
    [Arguments(false, false)]
    [Arguments(true, true)]
    [Arguments(true, false)]
    public async Task EmbeddedFontsPassQpdfCheck(bool cff, bool subset, CancellationToken cancellationToken)
    {
        var flags = subset ? 0 : NoSubsetting;
        var file = cff ? TestCffFont.Create(new TestCffOptions { EmbeddingFlags = flags }) : TestFont.Create(flags, null, false);
        var pdf = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(file, 0), PdfEmbeddedFontOptions.Default, Line);

        var outcome = await QpdfCheck.RunAsync($"font-{(cff ? "cff" : "truetype")}-{(subset ? "subset" : "whole")}", pdf, cancellationToken);

        if (outcome is not { } result)
        {
            Skip.Test("qpdf is not installed.");
            return;
        }

        await Assert.That(result.IsClean).IsTrue().Because(result.Output);
    }
}
