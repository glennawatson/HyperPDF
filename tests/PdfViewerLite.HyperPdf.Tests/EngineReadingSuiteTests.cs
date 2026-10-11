// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// The PDFium adapter's text, search, link, render, tagged structure, form and layer scenarios, run through the public
/// interfaces on both engines and compared with PDFium's results.
/// </summary>
[NotInParallel]
public sealed class EngineReadingSuiteTests
{
    /// <summary>The number of pages in the generated document.</summary>
    private const int PageCount = 5;

    /// <summary>The zero based index of the third page, the target of the internal link.</summary>
    private const int ThirdPage = 2;

    /// <summary>The zero based index of the fourth page.</summary>
    private const int FourthPage = 3;

    /// <summary>The left margin used by the generated document, in points.</summary>
    private const float Margin = 72F;

    /// <summary>Tolerance for positions, in points.</summary>
    private const float Tolerance = 1F;

    /// <summary>Tolerance for the hit test, in points.</summary>
    private const float HitTolerance = 4F;

    /// <summary>A point inside the heading's first letter.</summary>
    private const float HeadingX = 80F;

    /// <summary>A point inside the heading's first letter.</summary>
    private const float HeadingY = 64F;

    /// <summary>The number of fields in the test form.</summary>
    private const int FieldCount = 3;

    /// <summary>The index of the Blue option.</summary>
    private const int Blue = 2;

    /// <summary>The checkbox field.</summary>
    private const int CheckBox = 1;

    /// <summary>The combo box field.</summary>
    private const int ComboBox = 2;

    /// <summary>The fewest dark pixels a page with text draws.</summary>
    private const int MinimumInk = 50;

    /// <summary>The blank top-left corner of a page, in points.</summary>
    private const int BlankCorner = 60;

    /// <summary>Half a layer box, to sample its middle.</summary>
    private const int HalfBox = 50;

    /// <summary>The warm hit queries measured for managed allocation.</summary>
    private const int AllocationQueries = 1024;

    /// <summary>The name typed into the text field.</summary>
    private const string TypedName = "Glenn Watson";

    /// <summary>The page text, the hit test and the character boxes match PDFium's.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task ExtractsText(string engine)
    {
        using var test = new EngineDocument(engine, TestPdf.Create(PageCount));
        using var pdfium = new EngineDocument(EngineDocument.Pdfium, TestPdf.Create(PageCount));
        var document = test.Document;
        var count = document.GetCharacterCount(0);
        var text = document.GetText(0, 0, count);
        var index = document.GetCharacterIndexAt(0, new(HeadingX, HeadingY), HitTolerance);
        var characters = Characters(document);
        var expected = Characters(pdfium.Document);

        await Assert.That(text).Contains("Page 1");
        await Assert.That(text).Contains(TestPdf.Sentence);
        await Assert.That(text).IsEqualTo(pdfium.Document.GetText(0, 0, pdfium.Document.GetCharacterCount(0)));
        await Assert.That(document.GetText(0, index, 1)).IsEqualTo("P");
        await Assert.That(characters.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(characters[i].Value).IsEqualTo(expected[i].Value);
            await Assert.That(characters[i].Bounds.Left).IsEqualTo(expected[i].Bounds.Left).Within(Tolerance);
            await Assert.That(characters[i].Bounds.Top).IsEqualTo(expected[i].Bounds.Top).Within(Tolerance);
        }
    }

    /// <summary>The viewer adapter's warm hit query returns without managed allocation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WarmHyperPdfHitTestingDoesNotAllocate()
    {
        using var test = new EngineDocument(EngineDocument.HyperPdf, TestPdf.Create(PageCount));
        var document = test.Document;
        var point = new PagePoint(HeadingX, HeadingY);
        var expected = document.GetCharacterIndexAt(0, point, HitTolerance);
        var observed = -1;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < AllocationQueries; i++)
        {
            observed = document.GetCharacterIndexAt(0, point, HitTolerance);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(observed).IsEqualTo(expected);
        await Assert.That(allocated).IsEqualTo(0);
    }

    /// <summary>Search finds the same matches as PDFium, with bounds in top-left page space.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task FindsText(string engine)
    {
        const float markerTopMin = 100F;
        const float markerTopMax = 135F;
        using var test = new EngineDocument(engine, TestPdf.Create(PageCount));
        var document = test.Document;
        var matches = new List<TextMatch>();
        document.Find(FourthPage, "page4marker", SearchOptions.None, matches);
        var bounds = new List<PageRect>();
        document.GetTextBounds(FourthPage, matches[0].Start, matches[0].Length, bounds);
        var matchCase = new List<TextMatch>();
        document.Find(1, "QUICK", SearchOptions.MatchCase, matchCase);
        var anyCase = new List<TextMatch>();
        document.Find(1, "QUICK", SearchOptions.None, anyCase);

        await Assert.That(matches.Count).IsEqualTo(1);
        await Assert.That(bounds.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(bounds[0].Top).IsBetween(markerTopMin, markerTopMax);
        await Assert.That(matchCase.Count).IsEqualTo(0);
        await Assert.That(anyCase.Count).IsEqualTo(1);
    }

    /// <summary>Link annotations and web links in the text match PDFium's, in the same order.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task ReadsLinks(string engine)
    {
        using var test = new EngineDocument(engine, TestPdf.Create(PageCount));
        using var pdfium = new EngineDocument(EngineDocument.Pdfium, TestPdf.Create(PageCount));
        var links = test.Document.GetLinks(0);
        var expected = pdfium.Document.GetLinks(0);
        var pageLink = links.Single(static l => l.Target.Kind == LinkTargetKind.Page);

        await Assert.That(pageLink.Target.PageIndex).IsEqualTo(ThirdPage);
        await Assert.That(pageLink.Bounds.Left).IsEqualTo(Margin).Within(Tolerance);
        await Assert.That(links.Where(static l => l.Target.Kind == LinkTargetKind.Uri).All(static l => l.Target.Uri == TestPdf.LinkUri)).IsTrue();
        await Assert.That(links.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(links[i].Target).IsEqualTo(expected[i].Target);
            await Assert.That(links[i].Bounds.Left).IsEqualTo(expected[i].Bounds.Left).Within(Tolerance);
            await Assert.That(links[i].Bounds.Top).IsEqualTo(expected[i].Bounds.Top).Within(Tolerance);
        }
    }

    /// <summary>A page draws ink where its text is and leaves its margin white; a tile far from the text is blank.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task RendersPages(string engine)
    {
        const int size = 64;
        const int offsetX = 500;
        const int offsetY = 700;
        using var test = new EngineDocument(engine, TestPdf.Create(PageCount));
        var page = new PagePixels(test.Document, 0, RenderFlags.Annotations);
        var tile = new byte[size * size * PagePixels.BytesPerPixel];
        var drewTile = test.Document.Render(new(0, 1, PageRotation.None, offsetX, offsetY, RenderFlags.None), new(tile, size, size, size * PagePixels.BytesPerPixel));
        var cornerDark = false;
        for (var y = 0; y < BlankCorner; y++)
        {
            for (var x = 0; x < BlankCorner; x++)
            {
                cornerDark |= page.IsDark(x, y);
            }
        }

        await Assert.That(page.Rendered).IsTrue();
        await Assert.That(page.CountDark()).IsGreaterThan(MinimumInk);
        await Assert.That(cornerDark).IsFalse();
        await Assert.That(drewTile).IsTrue();
        await Assert.That(tile.AsSpan().ContainsAnyExcept(byte.MaxValue)).IsFalse();
    }

    /// <summary>The tagged blocks and the reading view follow the tags, leave out the artifact, and match PDFium's.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task ReadsTaggedStructureInLogicalOrder(string engine)
    {
        using var test = new EngineDocument(engine, TestPdf.CreateTagged());
        using var pdfium = new EngineDocument(EngineDocument.Pdfium, TestPdf.CreateTagged());
        var page = ReadPage(test.Document);
        var expected = ReadPage(pdfium.Document);
        var blocks = new List<TaggedBlock>();
        var expectedBlocks = new List<TaggedBlock>();
        var tagged = ((ITaggedStructureSource)DocumentFeatures.CastFeature(test.Document, typeof(ITaggedStructureSource))!).GetTaggedBlocks(0, blocks);
        _ = ((ITaggedStructureSource)DocumentFeatures.CastFeature(pdfium.Document, typeof(ITaggedStructureSource))!).GetTaggedBlocks(0, expectedBlocks);
        var first = page.Blocks[1];
        var start = Array.Find(first.CharIndices, static i => i >= 0);

        await Assert.That(tagged).IsTrue();
        await Assert.That(page.Blocks.Select(static b => b.Text)).IsEquivalentTo(
            [TestPdf.TaggedHeading, TestPdf.TaggedFirst, TestPdf.TaggedSecond, TestPdf.TaggedFigure]);
        await Assert.That(page.Blocks.All(static b => b.IsTagged)).IsTrue();
        await Assert.That(page.Blocks[0].Level).IsEqualTo(1);
        await Assert.That(test.Document.GetText(0, start, TestPdf.TaggedFirst.Length)).IsEqualTo(TestPdf.TaggedFirst);
        await Assert.That(ReadingDocument.Flatten(page, out _)).IsEqualTo(ReadingDocument.Flatten(expected, out _));
        await Assert.That(blocks.Select(static b => b.Kind)).IsEquivalentTo(expectedBlocks.Select(static b => b.Kind));
        await Assert.That(blocks.Select(static b => b.Characters.Length)).IsEquivalentTo(expectedBlocks.Select(static b => b.Characters.Length));
    }

    /// <summary>Each kind of field fills, shows on the page, saves, and reads back on both engines.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task FillsDrawsAndSavesForms(string engine)
    {
        using var test = new EngineDocument(engine, TestPdf.CreateForm());
        var filler = (IFormFiller)DocumentFeatures.CastFeature(test.Document, typeof(IFormFiller))!;
        var fields = Fields(filler);
        var blank = new PagePixels(test.Document, 0, RenderFlags.Annotations).CountDark();
        var typed = filler.SetText(0, fields[0].Index, TypedName);
        var ticked = filler.SetChecked(0, fields[CheckBox].Index, true);
        var chosen = filler.SelectOption(0, fields[ComboBox].Index, Blue);
        var filled = new PagePixels(test.Document, 0, RenderFlags.Annotations).CountDark();
        var unsaved = ((IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!).HasUnsavedChanges;
        await using var stream = new MemoryStream();
        var saved = ((IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!).Save(stream);

        await Assert.That(fields.Count).IsEqualTo(FieldCount);
        await Assert.That(typed && ticked && chosen).IsTrue();
        await Assert.That(unsaved).IsTrue();
        await Assert.That(saved).IsTrue();
        await Assert.That(filled).IsGreaterThan(blank);
        await AssertFilledAsync(Fields(filler));
        foreach (var reader in TestEngines.All())
        {
            using var reopened = new EngineDocument(reader, stream.ToArray());
            await AssertFilledAsync(Fields((IFormFiller)DocumentFeatures.CastFeature(reopened.Document, typeof(IFormFiller))!));
        }
    }

    /// <summary>Layers list as the document sets them; showing and hiding them changes what the page draws, on both engines.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task ShowsAndHidesLayers(string engine)
    {
        using var test = new EngineDocument(engine, TestPdf.CreateWithLayers());
        using var pdfium = new EngineDocument(EngineDocument.Pdfium, TestPdf.CreateWithLayers());
        var source = (ILayerSource)DocumentFeatures.CastFeature(test.Document, typeof(ILayerSource))!;

        // A copy, because PDFium updates the list it handed out when a layer changes.
        var layers = source.GetLayers().ToArray();
        var before = SampleBoxes(test.Document);
        var shown = source.SetLayerVisible(layers[1].Id, true);
        var hidden = source.SetLayerVisible(layers[0].Id, false);
        var after = SampleBoxes(test.Document);

        await Assert.That(layers).IsEquivalentTo(((ILayerSource)DocumentFeatures.CastFeature(pdfium.Document, typeof(ILayerSource))!).GetLayers());
        await Assert.That(before.Left && !before.Right).IsTrue();
        await Assert.That(shown && hidden).IsTrue();
        await Assert.That(!after.Left && after.Right).IsTrue();
    }

    /// <summary>Reads page 1 using its text and tagged structure features.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The reading page.</returns>
    private static ReadingPage ReadPage(IDocument document)
    {
        var characters = (ITextLayoutSource)DocumentFeatures.CastFeature(document, typeof(ITextLayoutSource))!;
        var structure = DocumentFeatures.CastFeature(document, typeof(ITaggedStructureSource)) as ITaggedStructureSource;

        // The source callback has no state parameter, so it captures both features.
        return ReadingDocument.Create(() => new ReadingSources(characters, structure), document.GetPageSizes()).GetPage(0);
    }

    /// <summary>Reads every character of page 1.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The characters.</returns>
    private static List<PageCharacter> Characters(IDocument document)
    {
        var characters = new List<PageCharacter>();
        ((ITextLayoutSource)DocumentFeatures.CastFeature(document, typeof(ITextLayoutSource))!).GetCharacters(0, characters);
        return characters;
    }

    /// <summary>Reads the fields of page 1.</summary>
    /// <param name="filler">The filler.</param>
    /// <returns>The fields.</returns>
    private static List<FormField> Fields(IFormFiller filler)
    {
        var fields = new List<FormField>();
        filler.GetFields(0, fields);
        return fields;
    }

    /// <summary>Checks the three filled fields.</summary>
    /// <param name="fields">The fields.</param>
    /// <returns>A task.</returns>
    private static async Task AssertFilledAsync(List<FormField> fields)
    {
        await Assert.That(fields[0].Value).IsEqualTo(TypedName);
        await Assert.That(fields[CheckBox].IsChecked).IsTrue();
        await Assert.That(fields[ComboBox].Value).IsEqualTo("Blue");
        await Assert.That(fields[ComboBox].SelectedOption).IsEqualTo(Blue);
    }

    /// <summary>Renders the layered page and tells whether each box is drawn.</summary>
    /// <param name="document">The document.</param>
    /// <returns>Whether the left and right boxes are dark.</returns>
    private static LayerBoxes SampleBoxes(IDocument document)
    {
        var page = new PagePixels(document, 0, RenderFlags.None);
        var y = page.Height - (TestPdf.LayerBoxBottom + HalfBox);
        return new(page.IsDark(TestPdf.LayerBoxLeft + HalfBox, y), page.IsDark(TestPdf.LayerBoxRight + HalfBox, y));
    }

    /// <summary>Whether each layer's box is drawn.</summary>
    /// <param name="Left">The drawing layer's box.</param>
    /// <param name="Right">The notes layer's box.</param>
    private readonly record struct LayerBoxes(bool Left, bool Right);
}
