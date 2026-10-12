// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Builds valid graphics fixtures and clause-specific expectations independent of any renderer.</summary>
internal static class StandardsRenderFixtures
{
    /// <summary>The page edge in points and pixels at scale one.</summary>
    internal const int Edge = 64;

    /// <summary>The official ISO 32000-1 publication containing the unchanged graphics rules.</summary>
    private const string StandardSource = "https://opensource.adobe.com/dc-acrobat-sdk-docs/standards/pdfstandards/pdf/PDF32000_2008.pdf";

    /// <summary>The ISO 32000-2 graphics errata source.</summary>
    private const string GraphicsErrata = "https://pdf-issues.pdfa.org/32000-2-2020/clause08.html";

    /// <summary>The ISO 32000-2 transparency errata source.</summary>
    private const string TransparencyErrata = "https://pdf-issues.pdfa.org/32000-2-2020/clause11.html";

    /// <summary>Creates a fixture with nonoverlapping regions away from antialiased edges.</summary>
    /// <param name="kind">The standards behavior to test.</param>
    /// <returns>The generated PDF and independent oracle.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The fixture kind is unknown.</exception>
    internal static StandardsRenderFixture Create(StandardsRenderCase kind) => kind switch
    {
        StandardsRenderCase.NonzeroFill => Fill(false),
        StandardsRenderCase.EvenOddFill => Fill(true),
        StandardsRenderCase.Clipping => Clip(),
        StandardsRenderCase.Transform => Transform(),
        StandardsRenderCase.NormalOpacity => Transparency(false),
        StandardsRenderCase.Multiply => Transparency(true),
        StandardsRenderCase.FormBoundingBox => FormBoundingBox(),
        StandardsRenderCase.NonIsolatedMultiply => NonIsolatedMultiply(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Builds the original fractional case or its opaque endpoint counterpart.</summary>
    /// <param name="fractional">Whether the group is placed at half opacity.</param>
    /// <returns>The generated non-isolated group PDF.</returns>
    internal static byte[] NonIsolatedGroup(bool fractional)
    {
        const int edge = 200;
        var pdf = new RenderTestPdf(edge, edge) { Content = fractional ? "1 1 0 rg 0 0 200 200 re f /Half gs /Fm Do" : "1 1 0 rg 0 0 200 200 re f /Fm Do" };
        var form = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Group << /S /Transparency /I false >>", "/Mul gs 0 0 1 rg 40 40 120 120 re f");
        pdf.Resources = string.Create(
            CultureInfo.InvariantCulture,
            $"/ExtGState << /Half << /Type /ExtGState /ca 0.5 /CA 0.5 >> /Mul << /Type /ExtGState /BM /Multiply >> >> /XObject << /Fm {form} 0 R >>");
        return pdf.ToBytes();
    }

    /// <summary>Builds equal-orientation nested rectangles under either filling rule.</summary>
    /// <param name="evenOdd">Whether to use f* instead of f.</param>
    /// <returns>The winding-rule fixture.</returns>
    private static StandardsRenderFixture Fill(bool evenOdd)
    {
        const int outside = 2;
        const int outsideEdge = 4;
        const int bandX = 12;
        const int bandY = 24;
        const int bandWidth = 4;
        const int bandHeight = 8;
        const int center = 28;
        const int centerEdge = 8;
        const string clause = $"ISO 32000-1 8.5.3.3 Filling, 8.5.3.3.2 Nonzero Winding Number Rule, 8.5.3.3.3 Even-Odd Rule; {StandardSource}; {GraphicsErrata}";
        var content = $"1 0 0 rg 8 8 48 48 re 24 24 16 16 re {(evenOdd ? "f*" : "f")}";
        return Build(
            content,
            string.Empty,
            [
            new(outside, outside, outsideEdge, outsideEdge, ComparisonTestRasters.White, 0, clause),
            new(bandX, bandY, bandWidth, bandHeight, ComparisonTestRasters.Red, 0, clause),
            new(center, center, centerEdge, centerEdge, evenOdd ? ComparisonTestRasters.White : ComparisonTestRasters.Red, 0, clause),
        ]);
    }

    /// <summary>Paints a clipped rectangle, then paints outside it after Q.</summary>
    /// <returns>The clipping and state-restoration fixture.</returns>
    private static StandardsRenderFixture Clip()
    {
        const int inside = 24;
        const int insideEdge = 8;
        const int outsideX = 52;
        const int outsideEdge = 4;
        const int restoredX = 6;
        const int restoredY = 54;
        const string clause = $"ISO 32000-1 8.5.4 Clipping Path Operators and 8.4.2 Graphics State Stack; {StandardSource}; {GraphicsErrata}";
        return Build(
            "q 16 16 32 32 re W n 0 0 1 rg 0 0 64 64 re f Q 1 0 0 rg 4 4 8 8 re f",
            string.Empty,
            [
            new(inside, inside, insideEdge, insideEdge, ComparisonTestRasters.Blue, 0, clause),
            new(outsideX, inside, outsideEdge, outsideEdge, ComparisonTestRasters.White, 0, clause),
            new(restoredX, restoredY, outsideEdge, outsideEdge, ComparisonTestRasters.Red, 0, clause),
        ]);
    }

    /// <summary>Translates a blue path, then verifies Q restores the original coordinates.</summary>
    /// <returns>The current-transformation-matrix fixture.</returns>
    private static StandardsRenderFixture Transform()
    {
        const int translatedX = 20;
        const int translatedY = 36;
        const int sampleEdge = 4;
        const int restoredX = 6;
        const int restoredY = 54;
        const int outsideY = 20;
        const string clause = $"ISO 32000-1 8.3.4 Transformation Matrices and 8.4.2 Graphics State Stack; {StandardSource}; {GraphicsErrata}";
        return Build(
            "q 2 0 0 2 16 16 cm 0 0 1 rg 0 0 8 8 re f Q 1 0 0 rg 4 4 8 8 re f",
            string.Empty,
            [
            new(translatedX, translatedY, sampleEdge, sampleEdge, ComparisonTestRasters.Blue, 0, clause),
            new(restoredX, restoredY, sampleEdge, sampleEdge, ComparisonTestRasters.Red, 0, clause),
            new(translatedX, outsideY, sampleEdge, sampleEdge, ComparisonTestRasters.White, 0, clause),
        ]);
    }

    /// <summary>Derives the interior result directly from the Normal or Multiply blend equation.</summary>
    /// <param name="multiply">Whether to multiply opaque blue and red.</param>
    /// <returns>The compositing fixture.</returns>
    private static StandardsRenderFixture Transparency(bool multiply)
    {
        const byte halfChannel = 128;
        const int center = 24;
        const int centerEdge = 16;
        const int outside = 4;
        const int outsideEdge = 8;
        var title = multiply ? "ISO 32000-1 11.3.5 Blend Mode and 11.3.3 Basic Compositing Formula" : "ISO 32000-1 11.3.3 Basic Compositing Formula";
        var clause = $"{title}; {StandardSource}; {TransparencyErrata}";
        var backdrop = multiply ? "1 0 0" : "0 0 1";
        var source = multiply ? "0 0 1" : "1 0 0";
        var state = multiply ? "/BM /Multiply /ca 1" : "/BM /Normal /ca 0.5";
        var expected = multiply ? new Bgra32Color(0, 0, 0, byte.MaxValue) : new(halfChannel, 0, halfChannel, byte.MaxValue);

        // Normal gives (0.5,0,0.5); one byte accounts only for rounding 127.5 to an integer channel.
        return Build(
            $"{backdrop} rg 16 16 32 32 re f q /GS gs {source} rg 16 16 32 32 re f Q",
            $"/ExtGState << /GS << /Type /ExtGState {state} >> >>",
            [
            new(center, center, centerEdge, centerEdge, expected, multiply ? (byte)0 : (byte)1, clause),
            new(outside, outside, outsideEdge, outsideEdge, ComparisonTestRasters.White, 0, clause),
        ]);
    }

    /// <summary>Verifies implicit clipping to the form's bounding box.</summary>
    /// <returns>The form clipping fixture.</returns>
    private static StandardsRenderFixture FormBoundingBox()
    {
        const int inside = 24;
        const int sampleEdge = 8;
        const int outside = 4;
        var pdf = new RenderTestPdf(Edge, Edge) { Content = "/Fm Do" };
        var form = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [16 16 48 48] /Resources << >>", "1 0 0 rg 0 0 64 64 re f");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /Fm {form} 0 R >>");
        const string clause = $"ISO 32000-1 8.10.2 Form Dictionaries, BBox clipping; {StandardSource}; {GraphicsErrata}";
        return new(pdf.ToBytes(), new(Edge, Edge, new ExpectedPixelRegion[]
        {
            new(inside, inside, sampleEdge, sampleEdge, ComparisonTestRasters.Red, 0, clause),
            new(outside, inside, sampleEdge, sampleEdge, ComparisonTestRasters.White, 0, clause),
        }));
    }

    /// <summary>Tests non-isolated Multiply with opaque endpoint colors, independent of fractional raster rounding.</summary>
    /// <returns>The non-isolated Multiply fixture.</returns>
    private static StandardsRenderFixture NonIsolatedMultiply()
    {
        const int groupEdge = 200;
        const int inside = 80;
        const int sampleEdge = 40;
        const int outside = 8;
        const int outsideEdge = 8;
        const string title = "ISO 32000-1 11.4.4 Group Compositing Computations and 11.4.8 Summary of Group Compositing Computations";
        const string clause = $"{title}; {StandardSource}; ISO 32000-2 11.4.8 errata: {TransparencyErrata}";

        // Endpoint components avoid the unspecified intermediate precision of fractional DeviceRGB compositing.
        var insideColor = new Bgra32Color(0, 0, 0, byte.MaxValue);
        var outsideColor = new Bgra32Color(0, byte.MaxValue, byte.MaxValue, byte.MaxValue);
        return new(NonIsolatedGroup(false), new(groupEdge, groupEdge, new ExpectedPixelRegion[]
        {
            new(inside, inside, sampleEdge, sampleEdge, insideColor, 0, clause),
            new(outside, outside, outsideEdge, outsideEdge, outsideColor, 0, clause),
        }));
    }

    /// <summary>Uses the existing xref-writing fixture builder with explicit resources and page bounds.</summary>
    /// <param name="content">The valid content operators.</param>
    /// <param name="resources">The resource dictionary entries.</param>
    /// <param name="regions">The independent expectations.</param>
    /// <returns>The generated well-formed fixture.</returns>
    private static StandardsRenderFixture Build(string content, string resources, ExpectedPixelRegion[] regions) =>
        new(new RenderTestPdf(Edge, Edge) { Content = content, Resources = resources }.ToBytes(), new(Edge, Edge, regions));
}
