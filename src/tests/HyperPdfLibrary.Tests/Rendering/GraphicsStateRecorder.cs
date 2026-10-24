// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>A content device that keeps a copy of the graphics state of every fill, stroke, glyph, image and shading it is given.</summary>
[DebuggerDisplay("GraphicsStateRecorder: {Paints.Count} paints")]
internal sealed class GraphicsStateRecorder : IContentDevice
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>Gets a value indicating whether the device reads the pixels of the images it draws; the default is <see langword="true"/>.</summary>
    public bool ReadsImagePixels { get; init; } = true;

    /// <summary>Gets the graphics states, in the order the paints arrived.</summary>
    internal List<GraphicsState> Paints { get; } = [];

    /// <summary>Gets the shadings painted, in order.</summary>
    internal List<PdfShading> Shadings { get; } = [];

    /// <summary>Gets the groups begun, in order.</summary>
    internal List<GroupInfo> Groups { get; } = [];

    /// <summary>Gets the number of images drawn with their pixels.</summary>
    internal int Images { get; private set; }

    /// <summary>Gets the number of images reported by their area only.</summary>
    internal int ImageAreas { get; private set; }

    /// <inheritdoc/>
    public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state) => Paints.Add(state);

    /// <inheritdoc/>
    public void Stroke(PdfPath path, ref GraphicsState state) => Paints.Add(state);

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state) => Paints.Add(state);

    /// <inheritdoc/>
    public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state)
    {
        Images++;
        Paints.Add(state);
    }

    /// <inheritdoc/>
    public void DrawImageArea(ref GraphicsState state)
    {
        ImageAreas++;
        Paints.Add(state);
    }

    /// <inheritdoc/>
    public void PaintShading(PdfShading shading, ref GraphicsState state)
    {
        Shadings.Add(shading);
        Paints.Add(state);
    }

    /// <inheritdoc/>
    public void Save()
    {
        // The recorder keeps no clip.
    }

    /// <inheritdoc/>
    public void Restore()
    {
        // The recorder keeps no clip.
    }

    /// <inheritdoc/>
    public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm)
    {
        // The recorder keeps no clip.
    }

    /// <inheritdoc/>
    public void BeginGroup(in GroupInfo group) => Groups.Add(group);

    /// <inheritdoc/>
    public void EndGroup(in GroupInfo group)
    {
        // The recorder keeps only the groups that began.
    }

    /// <inheritdoc/>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
    {
        // Marked content does not change the state.
    }

    /// <inheritdoc/>
    public void EndMarkedContent()
    {
        // Marked content does not change the state.
    }

    /// <inheritdoc/>
    public IPictureDevice CreatePictureDevice(PdfRect cull) => throw new NotSupportedException("The test content records no pictures.");

    /// <summary>Runs content on a one page document and records what it paints.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="resources">The entries of the page's /Resources dictionary.</param>
    /// <returns>The recorder.</returns>
    internal static GraphicsStateRecorder Run(string content, string resources)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = content };
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>");
        pdf.Resources = $"{resources} /Font << /F1 {font} 0 R >>";
        return Run(pdf);
    }

    /// <summary>Runs a prepared test document's first page and records what it paints.</summary>
    /// <param name="pdf">The document.</param>
    /// <returns>The recorder.</returns>
    internal static GraphicsStateRecorder Run(RenderTestPdf pdf)
    {
        using var document = PdfDocumentReader.OpenWith(pdf.ToBytes(), new PdfOpenOptions { FontFactory = static dictionary => new SquareFont(dictionary) });
        return Run(document, new());
    }

    /// <summary>Runs a prepared test document's first page with Skia drawing services, so images decode, and records what it paints.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="readsPixels">Whether the recorder reads image pixels.</param>
    /// <returns>The recorder.</returns>
    internal static GraphicsStateRecorder RunWithImages(RenderTestPdf pdf, bool readsPixels)
    {
        using var document = PdfDocumentReader.OpenWith(pdf.ToBytes(), new PdfOpenOptions { DrawingServices = SkiaTestRegistration.CreateServices() });
        return Run(document, new() { ReadsImagePixels = readsPixels });
    }

    /// <summary>Plays the first page of an opened document into a recorder.</summary>
    /// <param name="document">The document.</param>
    /// <param name="recorder">The recorder that receives the page.</param>
    /// <returns>The same recorder, after the page has played.</returns>
    private static GraphicsStateRecorder Run(PdfDocument document, GraphicsStateRecorder recorder)
    {
        using (var interpreter = new ContentInterpreter(PdfDocumentRendering.GetRenderCache(document), recorder, 0))
        {
            ContentExecution.RunPage(interpreter, PdfDocumentPages.GetPage(document, 0));
        }

        return recorder;
    }
}
