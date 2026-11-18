// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Render.Managed;
using HyperPdfLibrary.Render.Skia;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Fonts;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that a renderer told to let its recorded pages go draws the same page again afterwards, and is safe to tell at any time.</summary>
public class ReleaseRecordedPagesTests
{
    /// <summary>The font descriptor flags of a non-symbolic font.</summary>
    private const int FontFlags = 32;

    /// <summary>The pixels per point the page is rendered at.</summary>
    private const int Scale = 2;

    /// <summary>A line of text, so the page has something to record.</summary>
    private const string Text = "BT /F1 18 Tf 10.3 60.4 Td (Hello Hello Hello) Tj ET";

    /// <summary>A page drawn after its recorded pictures were released looks exactly as it did before.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageDrawsTheSameAfterItsPicturesAreReleased()
    {
        using var document = Open();
        using var renderer = new PdfPageRenderer(document);
        var before = Render(renderer, out var drawnBefore);

        renderer.ReleaseRecordedPages();
        var after = Render(renderer, out var drawnAfter);

        await Assert.That(drawnBefore).IsTrue();
        await Assert.That(drawnAfter).IsTrue();
        await Assert.That(before.AsSpan().IndexOfAnyExcept(byte.MaxValue)).IsGreaterThanOrEqualTo(0);
        await Assert.That(after.AsSpan().SequenceEqual(before)).IsTrue();
    }

    /// <summary>Releasing before anything was drawn, twice in a row, and after the renderer is disposed all do nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReleasingWithNothingRecordedOrAfterDisposeDoesNothing()
    {
        using var document = Open();
        var renderer = new PdfPageRenderer(document);
        renderer.ReleaseRecordedPages();
        _ = Render(renderer, out var drawn);
        renderer.ReleaseRecordedPages();
        renderer.ReleaseRecordedPages();
        renderer.Dispose();
        renderer.ReleaseRecordedPages();

        await Assert.That(drawn).IsTrue();
    }

    /// <summary>Opens a one-page document with a line of text.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument Open()
    {
        var pdf = FontTestDocument.Build(new() { Subtype = "TrueType", Program = TestFont.Create(), Flags = FontFlags, Entries = "/Encoding /WinAnsiEncoding", Content = Text });
        var registration = new PdfRenderBackendRegistration().UseSkia().UseManaged();
        return PdfDocumentReader.OpenWith(pdf, new PdfOpenOptions { FontFactory = PdfFontLoader.Load, DrawingServices = new(registration) });
    }

    /// <summary>Renders the first page.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="drawn">Whether the page was drawn.</param>
    /// <returns>The pixels.</returns>
    private static byte[] Render(PdfPageRenderer renderer, out bool drawn)
    {
        const int width = FontTestDocument.PageWidth * Scale;
        const int height = FontTestDocument.PageHeight * Scale;
        var pixels = new byte[width * height * RenderedImage.BytesPerPixel];
        drawn = renderer.Render(new(0, Scale, 0, 0, 0, PdfRenderFlags.None), new(pixels, width, height, width * RenderedImage.BytesPerPixel));
        return pixels;
    }
}
