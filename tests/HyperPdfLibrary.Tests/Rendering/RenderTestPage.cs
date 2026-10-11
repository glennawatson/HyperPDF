// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>A test document with a renderer, disposed together.</summary>
[DebuggerDisplay("RenderTestPage")]
internal sealed class RenderTestPage : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="RenderTestPage"/> class.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    internal RenderTestPage(byte[] pdf)
        : this(pdf, PdfFontLoader.Load)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RenderTestPage"/> class.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="fontFactory">The document's font loader.</param>
    internal RenderTestPage(byte[] pdf, Func<PdfDictionary, PdfFont?>? fontFactory)
    {
        Document = PdfDocumentReader.OpenWith(pdf, new PdfOpenOptions { FontFactory = fontFactory, DrawingServices = SkiaTestRegistration.CreateServices() });
        Renderer = new(Document);
    }

    /// <summary>Gets the document.</summary>
    internal PdfDocument Document { get; }

    /// <summary>Gets the renderer.</summary>
    internal PdfPageRenderer Renderer { get; }

    /// <summary>Renders the whole first page at a scale.</summary>
    /// <param name="scale">Device pixels per point.</param>
    /// <param name="quarterTurns">The extra rotation in quarter turns.</param>
    /// <param name="flags">The render options.</param>
    /// <returns>The pixels.</returns>
    internal RenderedImage RenderPage(float scale, int quarterTurns, PdfRenderFlags flags)
    {
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(Document, 0), quarterTurns, scale, out var width, out var height);
        return RenderTile(new(0, scale, quarterTurns, 0, 0, flags), width, height);
    }

    /// <summary>Renders the whole first page at 1 pixel per point.</summary>
    /// <returns>The pixels.</returns>
    internal RenderedImage RenderPage() => RenderPage(1, 0, PdfRenderFlags.None);

    /// <summary>Renders a tile.</summary>
    /// <param name="request">The tile request.</param>
    /// <param name="width">The tile width.</param>
    /// <param name="height">The tile height.</param>
    /// <returns>The pixels.</returns>
    /// <exception cref="InvalidOperationException">The tile could not be rendered.</exception>
    internal RenderedImage RenderTile(PdfTileRequest request, int width, int height)
    {
        var pixels = new byte[width * height * RenderedImage.BytesPerPixel];
        if (!Renderer.Render(request, new(pixels, width, height, width * RenderedImage.BytesPerPixel)))
        {
            throw new InvalidOperationException("The tile could not be rendered.");
        }

        return new(pixels, width, height);
    }

    /// <inheritdoc/>
    void IDisposable.Dispose()
    {
        Renderer.Dispose();
        Document.Dispose();
    }
}
