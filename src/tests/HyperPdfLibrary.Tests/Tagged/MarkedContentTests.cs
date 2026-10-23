// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Tests for recording what a page draws inside marked content.</summary>
[NotInParallel]
public sealed class MarkedContentTests
{
    /// <summary>The heading's marked content id.</summary>
    private const int HeadingMcid = 0;

    /// <summary>The first paragraph's marked content id.</summary>
    private const int FirstMcid = 1;

    /// <summary>The figure's marked content id.</summary>
    private const int FigureMcid = 3;

    /// <summary>The id given by a named property list.</summary>
    private const int NamedMcid = 4;

    /// <summary>The id given inside a form XObject.</summary>
    private const int FormMcid = 5;

    /// <summary>The heading's baseline, in PDF points from the bottom.</summary>
    private const float HeadingBaseline = 700;

    /// <summary>The page height.</summary>
    private const float PageHeight = 792;

    /// <summary>The heading's font size.</summary>
    private const float HeadingSize = 20;

    /// <summary>The share of the font size the default descent reaches below the baseline.</summary>
    private const float DefaultDescent = 0.2F;

    /// <summary>The tolerance for positions.</summary>
    private const float Tolerance = 0.5F;

    /// <summary>The figure's left edge.</summary>
    private const float FigureLeft = 72;

    /// <summary>The figure's width.</summary>
    private const float FigureWidth = 100;

    /// <summary>Each marked content id gives its glyphs, in drawing order, with their text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GroupsGlyphsByMarkedContentId()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.Basic());
        var page = PdfDocumentTagged.GetMarkedContent(document, 0);

        await Assert.That(TextOf(page, HeadingMcid)).IsEqualTo(TaggedSamples.Title);
        await Assert.That(TextOf(page, FirstMcid)).IsEqualTo(TaggedSamples.First);
        await Assert.That(page.GetGlyphs(FigureMcid).Length).IsEqualTo(0);
        await Assert.That(page.HasContent(FigureMcid)).IsTrue();
        await Assert.That(PdfDocumentTagged.GetMarkedContent(document, 0)).IsSameReferenceAs(page);
    }

    /// <summary>Glyph boxes are in viewer space, from the baseline up by the ascent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GlyphBoxesAreInViewerSpace()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.Basic());
        var page = PdfDocumentTagged.GetMarkedContent(document, 0);
        var box = page.GetBounds(HeadingMcid);
        var glyph = page.Glyphs[page.GetGlyphs(HeadingMcid)[0]];

        await Assert.That(box.Bottom).IsEqualTo(PageHeight - HeadingBaseline + (HeadingSize * DefaultDescent)).Within(Tolerance);
        await Assert.That(box.Top).IsLessThan(PageHeight - HeadingBaseline);
        await Assert.That(glyph.FontSize).IsEqualTo(HeadingSize).Within(Tolerance);
    }

    /// <summary>Text in artifact marked content is flagged and counts as neither marked nor in any id.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FlagsArtifacts()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.Basic());
        var page = PdfDocumentTagged.GetMarkedContent(document, 0);
        var artifacts = 0;
        foreach (var glyph in page.Glyphs)
        {
            artifacts += glyph.IsArtifact ? 1 : 0;
        }

        await Assert.That(artifacts).IsEqualTo(TaggedSamples.Header.Length);
        await Assert.That(page.VisibleCharacterCount - page.MarkedCharacterCount).IsEqualTo(TaggedSamples.Header.Length);
    }

    /// <summary>Paths in marked content give the id a box, so a figure has a focus target.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecordsGraphicBounds()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.Basic());
        var box = PdfDocumentTagged.GetMarkedContent(document, 0).GetBounds(FigureMcid);

        await Assert.That(box.Left).IsEqualTo(FigureLeft).Within(Tolerance);
        await Assert.That(box.Width).IsEqualTo(FigureWidth).Within(Tolerance);
    }

    /// <summary>Ids come from named property lists and from marked content inside form XObjects.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsNamedAndFormContent()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.NamedAndFormContent());
        var page = PdfDocumentTagged.GetMarkedContent(document, 0);

        await Assert.That(TextOf(page, NamedMcid)).IsEqualTo("Named");
        await Assert.That(TextOf(page, FormMcid)).IsEqualTo("Inside");
    }

    /// <summary>A page that only draws an image needs text recognition.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DetectsImageOnlyPages()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.ImageOnly());
        var page = PdfDocumentTagged.GetMarkedContent(document, 0);

        await Assert.That(page.HasImages).IsTrue();
        await Assert.That(page.IsImageOnly).IsTrue();
        await Assert.That(PdfReadingStructure.Read(document, 0).NeedsTextRecognition).IsTrue();
    }

    /// <summary>Joins the text of a marked content id's glyphs.</summary>
    /// <param name="page">The page.</param>
    /// <param name="mcid">The id.</param>
    /// <returns>The text.</returns>
    private static string TextOf(PdfMarkedContentPage page, int mcid)
    {
        var text = new StringBuilder();
        foreach (var glyph in page.GetGlyphs(mcid))
        {
            _ = text.Append(page.GetText(glyph));
        }

        return text.ToString();
    }
}
