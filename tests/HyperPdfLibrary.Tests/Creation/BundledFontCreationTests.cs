// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Render.Skia;
using HyperPdfLibrary.Tests.Conformance;
using HyperPdfLibrary.Tests.Fonts;
using HyperPdfLibrary.Tests.Optimizing;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>
/// Creates documents with the fonts embedded in the library while networking is forbidden: every request fails the test,
/// and the documents must still be made, reopened, read, drawn and converted to PDF/A.
/// </summary>
public sealed class BundledFontCreationTests
{
    /// <summary>The width of the test pages.</summary>
    private const float PageWidth = 300F;

    /// <summary>The height of the test pages.</summary>
    private const float PageHeight = 200F;

    /// <summary>The size of the test text.</summary>
    private const float FontSize = 18F;

    /// <summary>The left edge of drawn text.</summary>
    private const float Left = 20F;

    /// <summary>The baseline of the first line.</summary>
    private const float Top = 150F;

    /// <summary>The distance between baselines.</summary>
    private const float LineHeight = 30F;

    /// <summary>The most bytes a document with a few subset faces may take.</summary>
    private const int SubsetSizeLimit = 200_000;

    /// <summary>The fewest dark pixels a page of drawn text has.</summary>
    private const int MinimumInkPixels = 200;

    /// <summary>The level of a channel under which a pixel counts as ink.</summary>
    private const int InkLevel = 128;

    /// <summary>The bytes of a rendered pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The offset of the green sample in a rendered pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of the red sample in a rendered pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The text the sans, serif and fixed lines show; it uses accents and Windows ANSI punctuation.</summary>
    private static readonly string[] Lines = ["Hello World: café “quoted” €5 – ñ", "Times serif line, naïve résumé ©", "Courier fixed 0123456789 [A-Z]"];

    /// <summary>A document with sans, serif and fixed text is made, saved, reopened and read without a single network request.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentWithBundledFontsIsMadeAndReadOffline(CancellationToken cancellationToken)
    {
        var handler = new NoNetworkHandler();
        var services = OfflineServices(handler);

        var pdf = Create();
        CreatedFiles.Keep("bundled-fonts.pdf", pdf);
        using var document = await PdfDocumentReader.OpenWithAsync(new MemoryPdfByteSource(pdf), true, new PdfOpenOptions { DrawingServices = services }, cancellationToken);
        var text = (await PdfDocumentText.GetTextPageAsync(document, 0, cancellationToken)).Text;

        foreach (var line in Lines)
        {
            await Assert.That(text).Contains(line);
        }

        await Assert.That(handler.Requests).IsEqualTo(0);
        await Assert.That(services.GetMissingFontData()).IsEmpty();
        await Assert.That(pdf.Length).IsLessThan(SubsetSizeLimit);
        await Assert.That(OptimizerTestKit.Latin1(pdf)).Contains("/FontFile2");
    }

    /// <summary>The same input and clock give the same bytes, with the same embedded fonts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentWithBundledFontsIsReproducible()
    {
        var first = Create();
        var second = Create();

        await Assert.That(second).IsEquivalentTo(first);
    }

    /// <summary>The page reopens, draws ink where the text is, and passes qpdf when it is installed.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentWithBundledFontsRendersAndChecks(CancellationToken cancellationToken)
    {
        var pdf = Create();

        var render = OptimizerTestKit.Render(pdf);
        var ink = 0;
        for (var at = 0; at + PixelBytes <= render.Pixels.Length; at += PixelBytes)
        {
            if (render.Pixels[at] < InkLevel && render.Pixels[at + GreenOffset] < InkLevel && render.Pixels[at + RedOffset] < InkLevel)
            {
                ink++;
            }
        }

        await Assert.That(ink).IsGreaterThan(MinimumInkPixels);
        await CreationTestKit.CheckWithQpdfAsync("bundled-fonts", pdf, cancellationToken);
    }

    /// <summary>The symbol families embed and extract their Unicode text.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SymbolFamiliesEmbed(CancellationToken cancellationToken)
    {
        const string greek = "αβγ ∑∫∞";
        const string dingbats = "✂✓❀";
        var creator = new PdfDocumentCreator(new() { Title = "Symbols", Clock = new FixedClock(DateTimeOffset.UnixEpoch) });
        var symbol = creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Symbol, false, false).Source!);
        var zapf = creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Dingbats, false, false).Source!);
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            canvas.DrawText(symbol, FontSize, Left, Top, greek);
            canvas.DrawText(zapf, FontSize, Left, Top - LineHeight, dingbats);
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        var text = await CreatedFiles.TextOfAsync(creator.ToArray(), 0);

        await Assert.That(text).Contains(greek);
        await Assert.That(text).Contains(dingbats);
    }

    /// <summary>A PDF/A conversion embeds the library's fonts for Helvetica and makes no network request.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfAConversionEmbedsBundledFontsOffline(CancellationToken cancellationToken)
    {
        var handler = new NoNetworkHandler();
        var services = OfflineServices(handler);
        using var document = await PdfDocumentReader.OpenWithAsync(
            new MemoryPdfByteSource(ConversionTestPdf.HelveticaText()),
            true,
            new PdfOpenOptions { DrawingServices = services },
            cancellationToken);
        await using var output = new MemoryStream();

        var result = await PdfAConverter.SaveAsPdfAAsync(document, PdfALevel.PdfA2U, output, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(string.Join("; ", result.Problems.Select(static p => p.Description)));

        // The font program is in a compressed object stream, so the converted file shows it by its /Length1 key.
        await Assert.That(OptimizerTestKit.Latin1(output.ToArray()).Contains("/Length1", StringComparison.Ordinal)).IsTrue();
        await Assert.That(handler.Requests).IsEqualTo(0);
    }

    /// <summary>Creates drawing services that forbid network access and never download font data.</summary>
    /// <param name="handler">The transport that fails every request.</param>
    /// <returns>The services.</returns>
    private static PdfDrawingServices OfflineServices(NoNetworkHandler handler) =>
        new(new PdfRenderBackendRegistration().UseSkia()) { FontDataMode = PdfFontDataMode.Offline, FontDataHandler = handler };

    /// <summary>Creates the document: one line each in a sans, a serif and a fixed font.</summary>
    /// <returns>The file.</returns>
    private static byte[] Create()
    {
        var creator = new PdfDocumentCreator(new() { Title = "Bundled", Language = "en", Clock = new FixedClock(DateTimeOffset.UnixEpoch) });
        var fonts = new[]
        {
            creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Sans, false, false).Source!),
            creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Serif, true, false).Source!),
            creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Fixed, false, true).Source!),
        };
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            for (var i = 0; i < Lines.Length; i++)
            {
                canvas.DrawText(fonts[i], FontSize, Left, Top - (i * LineHeight), Lines[i]);
            }

            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        return creator.ToArray();
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
