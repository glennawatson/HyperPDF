// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Raster;

/// <summary>Builds one-page PDFs that paint an image, for the raster report tests.</summary>
internal static class RasterPdfs
{
    /// <summary>The image width in samples.</summary>
    internal const int ImageWidth = 600;

    /// <summary>The image height in samples.</summary>
    internal const int ImageHeight = 800;

    /// <summary>The painted width in points: 6 inches.</summary>
    internal const int PaintedWidth = 432;

    /// <summary>The painted height in points: 8 inches.</summary>
    internal const int PaintedHeight = 576;

    /// <summary>The resolution those sizes give.</summary>
    internal const float ExpectedDpi = 100F;

    /// <summary>The marker comment placed before <c>startxref</c>.</summary>
    internal const string Marker = "%PDF-raster-1.0";

    /// <summary>Content that paints the image over the whole page.</summary>
    internal const string PaintImage = "q 432 0 0 576 0 0 cm /Im0 Do Q\n";

    /// <summary>The image dictionary entries for a one-bit CCITT group 4 image.</summary>
    internal const string CcittEntries =
        "/Type /XObject /Subtype /Image /Width 600 /Height 800 /BitsPerComponent 1 /ColorSpace /DeviceGray "
        + "/Filter /CCITTFaxDecode /DecodeParms << /K -1 /Columns 600 /Rows 800 >>";

    /// <summary>Builds a one-page file.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="image">The image XObject body, or its own stream.</param>
    /// <param name="withMarker">Whether to put the PDF/R comment before <c>startxref</c>.</param>
    /// <returns>The file bytes.</returns>
    internal static byte[] Page(string content, string image, bool withMarker)
    {
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 432 576] /Contents 4 0 R /Resources << /XObject << /Im0 5 0 R >> /Font << /F1 6 0 R >> >> >>",
            MiniPdf.Stream(string.Empty, content),
            image,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        if (!withMarker)
        {
            return bytes;
        }

        var text = Encoding.Latin1.GetString(bytes);
        return Encoding.Latin1.GetBytes(text.Replace("startxref", $"{Marker}\nstartxref", StringComparison.Ordinal));
    }

    /// <summary>Builds a one-page file with an image stream.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="entries">The image dictionary entries.</param>
    /// <returns>The file bytes.</returns>
    internal static byte[] Page(string content, string entries) => Page(content, MiniPdf.Stream(entries, "data"), false);

    /// <summary>Reads the raster report of a file.</summary>
    /// <param name="bytes">The file bytes.</param>
    /// <returns>The report.</returns>
    internal static HyperPdfLibrary.Raster.PdfRasterReport Report(byte[] bytes)
    {
        using var document = PdfDocumentReader.Open(bytes, null);
        return PdfDocumentRaster.GetRasterReport(document, CancellationToken.None);
    }
}
