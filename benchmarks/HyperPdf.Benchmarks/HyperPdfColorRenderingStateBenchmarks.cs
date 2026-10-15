// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Runs a page of ICC CMYK colour changes through the content interpreter with a device that draws nothing. One content
/// stream switches the rendering intent and /UseBlackPtComp before every colour, so each <c>gs</c> and <c>ri</c>
/// converts the current colours again; the other runs as many <c>gs</c> and <c>ri</c> operators that leave the rendering
/// as it is.
/// </summary>
public class HyperPdfColorRenderingStateBenchmarks
{
    /// <summary>The repetitions of the colour change in the content.</summary>
    private const int Repeats = 2000;

    /// <summary>The device that draws nothing.</summary>
    private readonly NullDevice _device = new();

    /// <summary>The page the content runs on.</summary>
    private PdfPage _page = null!;

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The render cache the interpreter reads resources through.</summary>
    private PdfRenderCache _cache = null!;

    /// <summary>Gets or sets a value indicating whether the content changes the rendering before every colour.</summary>
    [Params(false, true)]
    public bool Switching { get; set; }

    /// <summary>Builds the document and runs the page once to load its resources.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var profile = Encoding.Latin1.GetString(HyperPdfIccRenderingBenchmarks.BuildLookUpProfile());
        var step = Switching
            ? "/On gs 1 0.5 0.25 1 scn /Off gs /Perceptual ri 0.5 1 0.25 0 scn /RelativeColorimetric ri\n"
            : "/Same gs 1 0.5 0.25 1 scn /Same gs /RelativeColorimetric ri 0.5 1 0.25 0 scn /RelativeColorimetric ri\n";
        var content = new StringBuilder("/CS0 cs\n");
        for (var i = 0; i < Repeats; i++)
        {
            _ = content.Append(step);
        }

        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << /ColorSpace << /CS0 [/ICCBased 5 0 R] >> "
            + "/ExtGState << /On << /UseBlackPtComp /ON /RI /Saturation >> /Off << /UseBlackPtComp /OFF >> /Same << /LW 1 >> >> >> /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, content.ToString()),
            MiniPdf.Stream("/N 4", profile));
        _document = PdfDocumentReader.Open(bytes, null);
        _page = PdfDocumentPages.GetPage(_document, 0);
        _cache = PdfDocumentRendering.GetRenderCache(_document);

        // The first run parses the colour space and builds each rendering's conversion table.
        _ = Run();
    }

    /// <summary>Releases the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Runs the page's content.</summary>
    /// <returns>The last fill colour.</returns>
    [Benchmark]
    public uint Run()
    {
        using var interpreter = ContentInterpreter.Rent(_cache, _device, 0);
        ContentExecution.RunPage(interpreter, _page);
        return interpreter.State.Fill.Rgb;
    }

    /// <summary>A device that draws nothing.</summary>
    [DebuggerDisplay("NullDevice")]
    private sealed class NullDevice : IContentDevice
    {
        /// <inheritdoc/>
        public bool RetainsPaths => false;

        /// <inheritdoc/>
        public void Save()
        {
            // Nothing is drawn.
        }

        /// <inheritdoc/>
        public void Restore()
        {
            // Nothing is drawn.
        }

        /// <inheritdoc/>
        public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state)
        {
            // Nothing is drawn.
        }

        /// <inheritdoc/>
        public void Stroke(PdfPath path, ref GraphicsState state)
        {
            // Nothing is drawn.
        }

        /// <inheritdoc/>
        public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm)
        {
            // Nothing is drawn.
        }

        /// <inheritdoc/>
        public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state)
        {
            // Nothing is drawn.
        }

        /// <inheritdoc/>
        public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
        {
            // Nothing is drawn.
        }

        /// <inheritdoc/>
        public void PaintShading(PdfShading shading, ref GraphicsState state)
        {
            // Nothing is drawn.
        }

        /// <inheritdoc/>
        public void BeginGroup(in GroupInfo group)
        {
            // Nothing is drawn.
        }

        /// <inheritdoc/>
        public void EndGroup(in GroupInfo group)
        {
            // Nothing is drawn.
        }

        /// <inheritdoc/>
        public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
        {
            // Nothing is drawn.
        }

        /// <inheritdoc/>
        public void EndMarkedContent()
        {
            // Nothing is drawn.
        }

        /// <inheritdoc/>
        public IPictureDevice CreatePictureDevice(PdfRect cull) => throw new NotSupportedException("The content records no pictures.");
    }
}
