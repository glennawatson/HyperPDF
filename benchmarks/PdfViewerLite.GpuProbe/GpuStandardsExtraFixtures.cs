// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Conformance;
using HyperPdfLibrary.Tests.Rendering;

namespace PdfViewerLite.GpuProbe;

/// <summary>Builds portable PDF fixtures for graphics families absent from the core standards set.</summary>
internal static class GpuStandardsExtraFixtures
{
    /// <summary>The page edge in points and pixels at scale one.</summary>
    private const int Edge = 64;

    /// <summary>Gets synthetic fixtures covering text, shading, masks, annotations and colour handling.</summary>
    /// <returns>The generated fixtures.</returns>
    internal static StandardsParityFixture[] Create() =>
    [
        Text(),
        AxialGradient(),
        TilingPattern(),
        TransparencyGroup(),
        SoftMask(),
        Annotation(),
        OutputIntent(),
        Overprint(),
    ];

    /// <summary>Uses a standard PDF base font and text operators.</summary>
    /// <returns>The fixture.</returns>
    private static StandardsParityFixture Text()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = "BT /F1 22 Tf 1 0 0 1 4 24 Tm (GPU PDF) Tj ET", Resources = "/Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >>" };
        return new("text", pdf.ToBytes(), Edge, Edge, PdfRenderFlags.None, false, null);
    }

    /// <summary>Uses a Type 2 axial shading.</summary>
    /// <returns>The fixture.</returns>
    private static StandardsParityFixture AxialGradient()
    {
        var pdf = new RenderTestPdf(Edge, Edge)
        {
            Content = "/S1 sh",
            Resources = "/Shading << /S1 << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 64 0] "
                + "/Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >> /Extend [true true] >> >>",
        };
        return new("axial-gradient", pdf.ToBytes(), Edge, Edge, PdfRenderFlags.None, false, null);
    }

    /// <summary>Repeats a painted Type 1 pattern cell.</summary>
    /// <returns>The fixture.</returns>
    private static StandardsParityFixture TilingPattern()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = "/Pattern cs /P1 scn 0 0 64 64 re f" };
        var cell = pdf.AddStream(
            "/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 16 16] /XStep 16 /YStep 16 /Resources << >>",
            "1 0 0 rg 0 0 8 16 re f 0 0 1 rg 8 0 8 16 re f");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Pattern << /P1 {cell} 0 R >>");
        return new("tiling-pattern", pdf.ToBytes(), Edge, Edge, PdfRenderFlags.None, false, null);
    }

    /// <summary>Paints a partially transparent isolated form over a blue backdrop.</summary>
    /// <returns>The fixture.</returns>
    private static StandardsParityFixture TransparencyGroup()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = "0 0 1 rg 0 0 64 64 re f q /Half gs /Fm Do Q" };
        var form = pdf.AddStream(
            "/Type /XObject /Subtype /Form /BBox [8 8 56 56] /Group << /S /Transparency /I true >> /Resources << >>",
            "1 0 0 rg 8 8 48 48 re f");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << /Half << /ca 0.5 >> >> /XObject << /Fm {form} 0 R >>");
        return new("transparency-group", pdf.ToBytes(), Edge, Edge, PdfRenderFlags.None, false, null);
    }

    /// <summary>Uses a luminosity soft mask that halves red coverage.</summary>
    /// <returns>The fixture.</returns>
    private static StandardsParityFixture SoftMask()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = "q /Masked gs 1 0 0 rg 0 0 64 64 re f Q" };
        var mask = pdf.AddStream(
            "/Type /XObject /Subtype /Form /BBox [0 0 64 64] /Group << /S /Transparency /CS /DeviceGray >> /Resources << >>",
            "0.5 g 0 0 64 64 re f");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << /Masked << /SMask << /S /Luminosity /G {mask} 0 R >> >> >>");
        return new("soft-mask", pdf.ToBytes(), Edge, Edge, PdfRenderFlags.None, false, null);
    }

    /// <summary>Uses a square annotation with its own appearance stream.</summary>
    /// <returns>The fixture.</returns>
    private static StandardsParityFixture Annotation()
    {
        var pdf = new RenderTestPdf(Edge, Edge);
        var appearance = pdf.AddStream(
            "/Type /XObject /Subtype /Form /BBox [0 0 32 32] /Resources << >>",
            "1 0 0 rg 0 0 32 32 re f");
        var annotation = pdf.AddObject(string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Annot /Subtype /Square /Rect [16 16 48 48] /F 4 /AP << /N {appearance} 0 R >> >>"));
        pdf.PageEntries = string.Create(CultureInfo.InvariantCulture, $"/Annots [{annotation} 0 R]");
        return new(nameof(annotation), pdf.ToBytes(), Edge, Edge, PdfRenderFlags.Annotations, false, null);
    }

    /// <summary>Converts a DeviceCMYK fill through a synthetic CMYK output intent.</summary>
    /// <returns>The fixture.</returns>
    private static StandardsParityFixture OutputIntent()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = "1 0 0 0.5 k 0 0 64 64 re f" };
        ConformancePdf.Intent(pdf, ConformancePdf.PdfASubtype, ConformancePdf.CmykProfile(), ConformancePdf.CmykComponents);
        return new("icc-output-intent", pdf.ToBytes(), Edge, Edge, PdfRenderFlags.OutputIntent, false, null);
    }

    /// <summary>Simulates DeviceCMYK image overprint against a yellow page.</summary>
    /// <returns>The fixture.</returns>
    private static StandardsParityFixture Overprint()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = "1 1 0 rg 0 0 64 64 re f q /OP gs 64 0 0 64 0 0 cm /Im Do Q" };
        var image = pdf.AddStream(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceCMYK /BitsPerComponent 8",
            [byte.MaxValue, 0, 0, 0]);
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << /OP << /OP true /op true /OPM 0 >> >> /XObject << /Im {image} 0 R >>");
        return new("cmyk-overprint", pdf.ToBytes(), Edge, Edge, PdfRenderFlags.None, true, null);
    }
}
