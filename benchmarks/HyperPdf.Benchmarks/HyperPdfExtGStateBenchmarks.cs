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
/// Runs a page of graphics state changes and paints through the content interpreter with a device that draws nothing. The
/// scenarios cover <c>gs</c> with every device-dependent key (transfer, halftone, black generation, undercolour removal,
/// flatness, smoothness, stroke adjustment, overprint and dashes), fills under a transfer function, overprinting CMYK
/// fills, and device colours that /DefaultRGB replaces.
/// </summary>
public class HyperPdfExtGStateBenchmarks
{
    /// <summary>The repetitions of the step in the content.</summary>
    private const int Repeats = 2000;

    /// <summary>The device that draws nothing.</summary>
    private readonly NullDevice _device = new();

    /// <summary>The page the content runs on.</summary>
    private PdfPage _page = null!;

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The render cache the interpreter reads resources through.</summary>
    private PdfRenderCache _cache = null!;

    /// <summary>The scenarios the benchmark runs.</summary>
    public enum Scenario
    {
        /// <summary><c>gs</c> with every device-dependent key, with and without <c>q</c> and <c>Q</c> around it.</summary>
        EveryKey = 0,

        /// <summary>Fills and strokes under a transfer function.</summary>
        TransferPaint = 1,

        /// <summary>CMYK fills in overprint mode 1 with the preview on.</summary>
        OverprintPaint = 2,

        /// <summary>Device colour operators when the resources name default colour spaces.</summary>
        DefaultSpaces = 3,

        /// <summary>The same fills with no key set, as the baseline.</summary>
        PlainPaint = 4,

        /// <summary>The <c>d</c> operator setting the same dash before every stroke, as map and drawing exports do.</summary>
        RepeatedDash = 5,

        /// <summary>Small inline images drawn one after another.</summary>
        InlineImages = 6,
    }

    /// <summary>Gets or sets the scenario.</summary>
    [Params(Scenario.EveryKey, Scenario.TransferPaint, Scenario.OverprintPaint, Scenario.DefaultSpaces, Scenario.PlainPaint, Scenario.RepeatedDash, Scenario.InlineImages)]
    public Scenario Case { get; set; }

    /// <summary>Gets the page resources: the graphics states, the colour spaces and the default spaces.</summary>
    private string Resources =>
        "/ExtGState << /All << /FL 7 /SM 0.25 /SA true /OP true /op true /OPM 1 /TR2 << /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >> "
        + "/HT << /Type /Halftone /HalftoneType 1 /Frequency 60 /Angle 45 /SpotFunction /Round >> "
        + "/BG2 << /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 1 >> /UCR2 << /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 2 >> >> "
        + "/Inv << /TR2 << /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >> >> /Mode1 << /OP true /op true /OPM 1 >> /Dashed << /D [[3 2] 1] >> >> "
        + "/ColorSpace << /Spot [/Separation /Spot /DeviceRGB << /FunctionType 2 /Domain [0 1] /C0 [1 1 1] /C1 [0 0 1] /N 1 >>] "
        + (Case == Scenario.DefaultSpaces
            ? "/DefaultGray [/DeviceN [/G] /DeviceRGB 5 0 R] /DefaultRGB [/DeviceN [/X /Y /Z] /DeviceRGB 6 0 R] /DefaultCMYK [/DeviceN [/A /B /C /D] /DeviceRGB 7 0 R] >>"
            : ">>");

    /// <summary>Builds the document and runs the page once to load its resources and build the tables.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var step = Case switch
        {
            Scenario.EveryKey => "q /All gs Q /All gs /Dashed gs\n",
            Scenario.TransferPaint => "/Inv gs 0.25 g 0 0 10 10 re f 1 0.5 0 RG 0 0 m 10 10 l S\n",
            Scenario.OverprintPaint => "/Mode1 gs 0 1 0 0 k 0 0 10 10 re f /Spot cs 1 sc 0 0 10 10 re f\n",
            Scenario.DefaultSpaces => "0.5 g 1 0 0 rg 0 0 0 1 k\n",
            Scenario.RepeatedDash => "[3 2] 0 d 0 0 m 10 10 l S\n",
            Scenario.InlineImages => "q 10 0 0 10 0 0 cm BI /W 2 /H 2 /CS /G /BPC 8 ID abcd EI Q\n",
            _ =>"0.25 g 0 0 10 10 re f 1 0.5 0 RG 0 0 m 10 10 l S\n",
        };
        var content = new StringBuilder();
        for (var i = 0; i < Repeats; i++)
        {
            _ = content.Append(step);
        }

        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << {Resources} >> /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, content.ToString()),
            MiniPdf.Stream("/FunctionType 4 /Domain [0 1] /Range [0 1 0 1 0 1]", "{ pop 1 0 0 }"),
            MiniPdf.Stream("/FunctionType 4 /Domain [0 1 0 1 0 1] /Range [0 1 0 1 0 1]", "{ pop pop pop 0 1 0 }"),
            MiniPdf.Stream("/FunctionType 4 /Domain [0 1 0 1 0 1 0 1] /Range [0 1 0 1 0 1]", "{ pop pop pop pop 0 0 1 }"));
        _document = PdfDocumentReader.Open(bytes, null);
        _page = PdfDocumentPages.GetPage(_document, 0);
        _cache = PdfDocumentRendering.GetRenderCache(_document);
        _cache.SimulateOverprint = true;

        // The first run parses the functions, builds the tables and the default spaces.
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
