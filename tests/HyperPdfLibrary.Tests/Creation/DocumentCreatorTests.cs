// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Optimizing;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>New documents created from text, images and existing pages.</summary>
public sealed class DocumentCreatorTests
{
    /// <summary>The width of the test pages.</summary>
    private const float PageWidth = 200F;

    /// <summary>The height of the test pages.</summary>
    private const float PageHeight = 120F;

    /// <summary>The size of the test text.</summary>
    private const float FontSize = 14F;

    /// <summary>The left edge of drawn text.</summary>
    private const float Left = 10F;

    /// <summary>The baseline of drawn text.</summary>
    private const float Baseline = 60F;

    /// <summary>The size of a test image in pixels.</summary>
    private const int ImageSize = 8;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The offset of the red sample in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of the alpha sample in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The width of the narrow wrapping column in points.</summary>
    private const float ColumnWidth = 60F;

    /// <summary>The most a wrapped line may overrun the column because of rounding.</summary>
    private const float Rounding = 0.01F;

    /// <summary>The position of the image's left and bottom edges.</summary>
    private const float ImageOrigin = 20F;

    /// <summary>The width and height the image is drawn at.</summary>
    private const float ImageExtent = 40F;

    /// <summary>The number of pages after a page is copied beside a new one.</summary>
    private const int TwoPages = 2;

    /// <summary>The level a channel must exceed to count as fully on.</summary>
    private const int Bright = 200;

    /// <summary>The level a channel must stay under to count as off.</summary>
    private const int Dark = 60;

    /// <summary>The distance below the image's top edge of the pixel read inside it.</summary>
    private const int InsideRow = 40;

    /// <summary>The row and column of the pixel read outside the image.</summary>
    private const int OutsideRow = 100;

    /// <summary>The text drawn on the test page.</summary>
    private const string Greeting = "Hello World";

    /// <summary>The title used by the description test.</summary>
    private const string Title = "A Title";

    /// <summary>Text drawn in an embedded font extracts, is found by search, and has a subset of the font embedded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextInAnEmbeddedFontExtracts()
    {
        var creator = new PdfDocumentCreator(new() { Title = "Text", Language = "en" });
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        DrawLine(creator, font, Greeting);

        var pdf = creator.ToArray();
        CreatedFiles.Keep("text-embedded.pdf", pdf);

        await Assert.That(CreatedFiles.TextOf(pdf, 0)).Contains(Greeting);
        await Assert.That(font.IsEmbedded).IsTrue();
        await Assert.That(OptimizerTestKit.Latin1(pdf)).Contains("/FontFile2");
        await Assert.That(pdf.Length).IsLessThan(TestFont.Create().Length);
    }

    /// <summary>Text drawn in a built-in font extracts and uses no embedded program.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextInABuiltInFontExtracts()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.AddBuiltInFont(AppearanceFont.Helvetica);
        DrawLine(creator, font, Greeting);

        var pdf = creator.ToArray();

        await Assert.That(CreatedFiles.TextOf(pdf, 0)).Contains(Greeting);
        await Assert.That(font.IsEmbedded).IsFalse();
        await Assert.That(OptimizerTestKit.Latin1(pdf)).DoesNotContain("/FontFile");
    }

    /// <summary>Text with a line break is refused by DrawText.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawTextRefusesLineBreaks()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.AddBuiltInFont(AppearanceFont.Helvetica);
        var threw = false;
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            canvas.DrawText(font, FontSize, 0, 0, "one\ntwo");
        }
        catch (ArgumentException)
        {
            threw = true;
        }
        finally
        {
            canvas.Dispose();
        }

        await Assert.That(threw).IsTrue();
    }

    /// <summary>The information dictionary, the metadata and the catalog describe the document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentIsDescribed()
    {
        var creator = new PdfDocumentCreator(new()
        {
            Title = Title,
            Author = "An Author",
            Subject = "A Subject",
            Keywords = "one, two",
            CreatorTool = "A Tool",
            Language = "en-AU",
            Clock = new FixedClock(DateTimeOffset.UnixEpoch),
        });
        DrawLine(creator, creator.AddBuiltInFont(AppearanceFont.Helvetica), Greeting);

        using var document = PdfDocumentReader.Open(creator.ToArray(), null);

        var info = PdfDocumentMetadata.GetInfo(document);
        var xmp = PdfDocumentMetadata.GetXmp(document)!;
        await Assert.That(info.Title).IsEqualTo(Title);
        await Assert.That(info.Author).IsEqualTo("An Author");
        await Assert.That(info.Subject).IsEqualTo("A Subject");
        await Assert.That(info.Producer).IsEqualTo("HyperPDF");
        await Assert.That(xmp.Title).IsEqualTo(Title);
        await Assert.That(document.Catalog.GetText(KnownName.Lang)).IsEqualTo("en-AU");
        await Assert.That(PdfDocumentViewerPreferences.GetViewerPreferences(document).DisplayDocTitle).IsTrue();
    }

    /// <summary>The same input and clock give the same bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutputIsReproducible()
    {
        var first = CreateReproducible();
        var second = CreateReproducible();

        await Assert.That(second).IsEquivalentTo(first);
    }

    /// <summary>An image is drawn where it was placed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImageIsDrawnInPlace()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var image = creator.AddRaster(CreateRedSquare(), PdfImageOptions.Default, CancellationToken.None);
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            canvas.DrawImage(image, ImageOrigin, ImageOrigin, ImageExtent, ImageExtent);
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        var pdf = creator.ToArray();
        CreatedFiles.Keep("image-page.pdf", pdf);
        var render = OptimizerTestKit.Render(pdf);

        var inside = CreationTestKit.PixelAt(render, InsideRow, (int)PageHeight - InsideRow);
        var outside = CreationTestKit.PixelAt(render, OutsideRow, (int)PageHeight - OutsideRow);
        await Assert.That(inside.Red).IsGreaterThan((byte)Bright);
        await Assert.That(inside.Green).IsLessThan((byte)Dark);
        await Assert.That(outside.Red).IsGreaterThan((byte)Bright);
        await Assert.That(outside.Green).IsGreaterThan((byte)Bright);
    }

    /// <summary>A page copied from another document keeps its text and joins the pages made here.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExistingPagesJoinNewOnes()
    {
        var source = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        DrawLine(source, source.AddBuiltInFont(AppearanceFont.Helvetica), "Copied page");
        using var original = PdfDocumentReader.Open(source.ToArray(), null);
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        DrawLine(creator, creator.AddBuiltInFont(AppearanceFont.Helvetica), Greeting);

        _ = creator.AddPage(original, 0);
        var pdf = creator.ToArray();

        await Assert.That(creator.PageCount).IsEqualTo(TwoPages);
        await Assert.That(CreatedFiles.TextOf(pdf, 0)).Contains(Greeting);
        await Assert.That(CreatedFiles.TextOf(pdf, 1)).Contains("Copied page");
    }

    /// <summary>A page index outside the source document is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingSourcePageIsRefused()
    {
        var source = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        DrawLine(source, source.AddBuiltInFont(AppearanceFont.Helvetica), Greeting);
        using var original = PdfDocumentReader.Open(source.ToArray(), null);
        var threw = false;
        try
        {
            _ = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default).AddPage(original, 1);
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    /// <summary>A finished document cannot change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FinishedDocumentCannotChange()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        DrawLine(creator, creator.AddBuiltInFont(AppearanceFont.Helvetica), Greeting);
        _ = creator.Finish();
        var threw = false;
        try
        {
            _ = creator.BeginPage(PageWidth, PageHeight);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    /// <summary>Wrapped lines fit the width, break at spaces and at line breaks, and split a word that is too long.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextWrapsToTheWidth()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        const string Text = "one two three four five six\nseven Supercalifragilisticexpialidocious";
        var lines = new List<PdfTextLine>();

        PdfTextWrapper.Wrap(font, FontSize, Text, ColumnWidth, lines);

        await Assert.That(lines.Count).IsGreaterThan(TwoPages);
        foreach (var line in lines)
        {
            var content = Text.Substring(line.Start, line.Length);
            await Assert.That(line.Width).IsLessThanOrEqualTo(ColumnWidth + Rounding);
            await Assert.That(content).IsEqualTo(content.Trim());
        }

        var joined = string.Concat(lines.Select(static line => Text.Substring(line.Start, line.Length)));
        await Assert.That(Squash(joined)).IsEqualTo(Squash(Text));
    }

    /// <summary>A created document with text and an image passes <c>qpdf --check</c>.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreatedDocumentPassesQpdf(CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new() { Title = "Mixed", Language = "en" });
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var image = creator.AddRaster(CreateRedSquare(), PdfImageOptions.Default, cancellationToken);
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            canvas.DrawText(font, FontSize, Left, Baseline, Greeting);
            canvas.DrawImage(image, Left, Left, ImageExtent, ImageExtent);
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        await CreationTestKit.CheckWithQpdfAsync("created-mixed", creator.ToArray(), cancellationToken);
    }

    /// <summary>Saving to a stream asynchronously writes the same bytes as <c>ToArray</c>.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaveAsyncWritesTheDocument(CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        DrawLine(creator, creator.AddBuiltInFont(AppearanceFont.Helvetica), Greeting);
        await using var stream = new MemoryStream();

        var report = await creator.SaveAsync(stream, cancellationToken);

        await Assert.That(stream.ToArray()).IsEquivalentTo(creator.ToArray());
        await Assert.That(report.PageCount).IsEqualTo(1);
    }

    /// <summary>Removes spaces and line breaks so wrapped text can be compared with its source.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without whitespace.</returns>
    private static string Squash(string text) => string.Concat(text.Where(static c => !char.IsWhiteSpace(c)));

    /// <summary>Builds the same small document every time.</summary>
    /// <returns>The file.</returns>
    private static byte[] CreateReproducible()
    {
        var creator = new PdfDocumentCreator(new() { Title = "Same", Clock = new FixedClock(DateTimeOffset.UnixEpoch) });
        DrawLine(creator, creator.EmbedFont(CreationTestKit.OpenFont()), Greeting);
        return creator.ToArray();
    }

    /// <summary>Draws one line on a new page and adds the page.</summary>
    /// <param name="creator">The document.</param>
    /// <param name="font">The font.</param>
    /// <param name="text">The text.</param>
    private static void DrawLine(PdfDocumentCreator creator, PdfCanvasFont font, string text)
    {
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            canvas.DrawText(font, FontSize, Left, Baseline, text);
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }
    }

    /// <summary>Makes a solid red image.</summary>
    /// <returns>The pixels.</returns>
    private static PdfRasterImage CreateRedSquare()
    {
        var pixels = new byte[ImageSize * ImageSize * BgraBytes];
        for (var i = 0; i < pixels.Length; i += BgraBytes)
        {
            pixels[i + RedOffset] = byte.MaxValue;
            pixels[i + AlphaOffset] = byte.MaxValue;
        }

        return new(PdfRasterFormat.Bgra32, ImageSize, ImageSize, pixels);
    }

    /// <summary>A clock that always reports one moment.</summary>
    private sealed class FixedClock : TimeProvider
    {
        /// <summary>The moment.</summary>
        private readonly DateTimeOffset _now;

        /// <summary>Initializes a new instance of the <see cref="FixedClock"/> class.</summary>
        /// <param name="now">The moment.</param>
        internal FixedClock(DateTimeOffset now) => _now = now;

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
