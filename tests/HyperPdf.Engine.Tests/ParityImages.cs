// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using SkiaSharp;

namespace HyperPdf.Engine.Tests;

/// <summary>The PDFium and HyperPDF renders of one page, saved as PNGs when they differ.</summary>
/// <param name="Expected">PDFium's BGRA pixels.</param>
/// <param name="Actual">HyperPDF's BGRA pixels.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[DebuggerDisplay("ParityImages: {Width}x{Height}")]
internal sealed record ParityImages(byte[] Expected, byte[] Actual, int Width, int Height)
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The quality given to the PNG encoder; PNG ignores it.</summary>
    private const int PngQuality = 100;

    /// <summary>Saves both renders in the test output folder.</summary>
    /// <param name="name">The file name stem.</param>
    internal void Save(string name)
    {
        Save($"{name}-pdfium", Expected);
        Save($"{name}-hyperpdf", Actual);
    }

    /// <summary>Saves pixels as a PNG.</summary>
    /// <param name="name">The file name without extension.</param>
    /// <param name="pixels">The BGRA pixels.</param>
    private void Save(string name, byte[] pixels)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "render-failures");
        _ = Directory.CreateDirectory(folder);
        using var image = SKImage.FromPixelCopy(new(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul), pixels, Width * BytesPerPixel);
        using var data = image.Encode(SKEncodedImageFormat.Png, PngQuality);
        using var file = File.Create(Path.Combine(folder, $"{name}.png"));
        data.SaveTo(file);
    }
}
