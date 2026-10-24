// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Export;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks writing pages as PostScript and Encapsulated PostScript.</summary>
public sealed class PostScriptExportTests
{
    /// <summary>The page width and height in points.</summary>
    private const int PageSize = 200;

    /// <summary>The font size of the test text.</summary>
    private const float FontSize = 18;

    /// <summary>The left margin and bottom of the shapes.</summary>
    private const float Margin = 10;

    /// <summary>The side of the red square in points.</summary>
    private const float Square = 40;

    /// <summary>Where the text sits on the page.</summary>
    private const float Baseline = 150;

    /// <summary>The top of the gradient band.</summary>
    private const float BandBottom = 100;

    /// <summary>The height of the gradient band.</summary>
    private const float BandHeight = 20;

    /// <summary>The left edge of the test image in points.</summary>
    private const float ImageLeft = 100;

    /// <summary>The pixels along the side of the test image.</summary>
    private const int ImageSide = 4;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The distance from the left of the square to its middle, in points.</summary>
    private const float SquareCentre = Margin + (Square / 2);

    /// <summary>The red a red pixel must have at least.</summary>
    private const int StrongChannel = 200;

    /// <summary>The most a red pixel's green and blue may have.</summary>
    private const int WeakChannel = 60;

    /// <summary>The pixels per point when Ghostscript draws the page.</summary>
    private const int Resolution = 72;

    /// <summary>A page becomes a PostScript document with a page header, balanced graphics state operators and one showpage.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageBecomesAPostScriptDocument()
    {
        using var document = SkiaTestRegistration.Open(CreateDocument(), null);
        using var renderer = new PdfPageRenderer(document);
        await using var output = new MemoryStream();

        var report = await renderer.WritePostScriptAsync([0], PdfRenderFlags.None, PdfPostScriptOptions.Document, output, CancellationToken.None);

        var text = Encoding.ASCII.GetString(output.ToArray());
        await Assert.That(text).StartsWith("%!PS-Adobe-3.0\n");
        await Assert.That(text).Contains("%%BoundingBox: 0 0 200 200");
        await Assert.That(text).Contains("%%Pages: 1");
        await Assert.That(Count(text, "showpage")).IsEqualTo(1);
        await Assert.That(Count(text, "gsave")).IsEqualTo(Count(text, "grestore"));
        await Assert.That(text).Contains("shfill");
        await Assert.That(report.PagesWritten).IsEqualTo(1);
        await Assert.That(report.IsExact).IsTrue();
    }

    /// <summary>Encapsulated PostScript says so, and does not end the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EpsHasNoShowpage()
    {
        using var document = SkiaTestRegistration.Open(CreateDocument(), null);
        using var renderer = new PdfPageRenderer(document);
        await using var output = new MemoryStream();

        _ = await renderer.WritePostScriptAsync([0], PdfRenderFlags.None, PdfPostScriptOptions.Eps, output, CancellationToken.None);

        var text = Encoding.ASCII.GetString(output.ToArray());
        await Assert.That(text).StartsWith("%!PS-Adobe-3.0 EPSF-3.0\n");
        await Assert.That(text).DoesNotContain("showpage");
    }

    /// <summary>Encapsulated PostScript holds one page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EpsWithTwoPagesIsRefused()
    {
        using var document = SkiaTestRegistration.Open(CreateDocument(), null);
        using var renderer = new PdfPageRenderer(document);
        await using var output = new MemoryStream();

        await Assert.That(async () => await renderer.WritePostScriptAsync([0, 0], PdfRenderFlags.None, PdfPostScriptOptions.Eps, output, CancellationToken.None)).Throws<ArgumentException>();
    }

    /// <summary>Ghostscript reads the file without an error and draws the red square where the page has it.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task GhostscriptDrawsTheSquareInPlace(CancellationToken cancellationToken)
    {
        using var document = SkiaTestRegistration.Open(CreateDocument(), null);
        using var renderer = new PdfPageRenderer(document);
        var folder = Path.Combine(Path.GetTempPath(), $"postscript-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        try
        {
            var source = Path.Combine(folder, "page.ps");
            await using (var file = File.Create(source))
            {
                _ = await renderer.WritePostScriptAsync([0], PdfRenderFlags.None, PdfPostScriptOptions.Document, file, cancellationToken);
            }

            var picture = Path.Combine(folder, "page.png");
            var run = await RunGhostscriptAsync(source, picture, cancellationToken).ConfigureAwait(false);
            if (run is null)
            {
                Skip.Test("Ghostscript (gs) is not on the PATH.");
                return;
            }

            await Assert.That(run).IsEqualTo(string.Empty);
            using var bitmap = SKBitmap.Decode(picture);

            // PDF y runs up from the bottom, the picture's y runs down from the top.
            var pixel = bitmap.GetPixel((int)SquareCentre, PageSize - (int)SquareCentre);
            await Assert.That(pixel.Red).IsGreaterThan((byte)StrongChannel);
            await Assert.That(pixel.Green).IsLessThan((byte)WeakChannel);
            await Assert.That(pixel.Blue).IsLessThan((byte)WeakChannel);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Counts the places a word appears in some text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The word.</param>
    /// <returns>The count.</returns>
    private static int Count(string text, string word) => text.Split(word).Length - 1;

    /// <summary>Runs Ghostscript to draw a PostScript file as a PNG.</summary>
    /// <param name="source">The PostScript file.</param>
    /// <param name="picture">The PNG to write.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The error text, empty when there was none; <see langword="null"/> when Ghostscript cannot be started.</returns>
    private static async Task<string?> RunGhostscriptAsync(string source, string picture, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("gs") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-q", "-dBATCH", "-dNOPAUSE", "-sDEVICE=png16m", $"-r{Resolution}", $"-sOutputFile={picture}", source })
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return await output.ConfigureAwait(false) + await errors.ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    /// <summary>Creates a page with text, a red square, a gradient band and a small image.</summary>
    /// <returns>The file.</returns>
    private static byte[] CreateDocument()
    {
        var creator = new PdfDocumentCreator(new());
        var font = creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Sans, false, false).Source!);
        var shading = PdfShadingWriter.AddAxial(creator.Sink, new(0, 0), new(PageSize, 0), [new(0, 1, 0, 0), new(1, 0, 0, 1)], true, true);
        var image = creator.AddRaster(new(PdfRasterFormat.Rgb24, ImageSide, ImageSide, new byte[ImageSide * ImageSide * RgbBytes]), PdfImageOptions.Default, CancellationToken.None);
        var page = creator.BeginPage(PageSize, PageSize);
        page.DrawText(font, FontSize, Margin, Baseline, "PostScript");
        page.FillRectangle(1, 0, 0, Margin, Margin, Square, Square);
        page.FillRectangleWithShading(shading, 0, BandBottom, PageSize, BandHeight);
        page.DrawImage(image, ImageLeft, Margin, Square, Square);
        _ = page.Finish();
        page.Dispose();
        return creator.ToArray();
    }
}
