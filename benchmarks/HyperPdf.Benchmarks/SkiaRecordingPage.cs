// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Builds and draws the one-page documents used by the Skia recording benchmarks.</summary>
internal static class SkiaRecordingPage
{
    /// <summary>The tile size in pixels.</summary>
    internal const int Tile = 512;

    /// <summary>The bytes in a BGRA pixel.</summary>
    internal const int BytesPerPixel = 4;

    /// <summary>The page width in points.</summary>
    internal const int PageWidth = 612;

    /// <summary>The page height in points.</summary>
    internal const int PageHeight = 792;

    /// <summary>The object number of the optional image.</summary>
    private const int ImageObject = 5;

    /// <summary>Builds a one page document.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="resources">The entries inside the page's /Resources dictionary.</param>
    /// <param name="withImage">Whether the document carries a two by two grey image as object 5.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] Create(string content, string resources, bool withImage)
    {
        var page = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << {resources} >> /Contents 4 0 R >>";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            page,
            MiniPdf.Stream(string.Empty, content),
        ];

        string[] image = withImage
            ? [MiniPdf.Stream("/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /ASCIIHexDecode", "00FFFF00>")]
            : [];
        return MiniPdf.Build([.. objects, .. image]);
    }

    /// <summary>Gets the resources entry that names the image object as <c>/Im</c>.</summary>
    /// <returns>The entry.</returns>
    internal static string ImageResources() => $"/XObject << /Im {ImageObject} 0 R >>";

    /// <summary>Draws the top-left tile of the page.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="pixels">The tile pixels.</param>
    /// <returns>Whether the tile rendered.</returns>
    internal static bool Draw(PdfPageRenderer renderer, byte[] pixels) =>
        renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, Tile, Tile, Tile * BytesPerPixel));
}
