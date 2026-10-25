// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Pixel assertions that save the rendered image when they fail, as the parity tests do.</summary>
internal static class RenderCheck
{
    /// <summary>The quality given to the PNG encoder; PNG ignores it.</summary>
    private const int PngQuality = 100;

    /// <summary>Asserts a pixel is near a colour, saving the image as a PNG in the test output when it is not.</summary>
    /// <param name="image">The rendered image.</param>
    /// <param name="x">The pixel's x.</param>
    /// <param name="y">The pixel's y.</param>
    /// <param name="expected">The expected colour.</param>
    /// <param name="tolerance">The largest channel difference accepted.</param>
    /// <param name="name">The test name, used for the saved file.</param>
    /// <returns>A task.</returns>
    internal static async Task Near(RenderedImage image, int x, int y, Rgb expected, int tolerance, string name)
    {
        var near = image.IsNear(x, y, expected, tolerance);
        if (!near)
        {
            Save(image, name);
        }

        await Assert.That(near).IsTrue().Because(image.Describe(x, y));
    }

    /// <summary>Asserts a pixel is far from a colour, saving the image when it is not.</summary>
    /// <param name="image">The rendered image.</param>
    /// <param name="x">The pixel's x.</param>
    /// <param name="y">The pixel's y.</param>
    /// <param name="unexpected">The colour the pixel must not be near.</param>
    /// <param name="tolerance">The channel difference within which the pixel counts as that colour.</param>
    /// <param name="name">The test name, used for the saved file.</param>
    /// <returns>A task.</returns>
    internal static async Task NotNear(RenderedImage image, int x, int y, Rgb unexpected, int tolerance, string name)
    {
        var near = image.IsNear(x, y, unexpected, tolerance);
        if (near)
        {
            Save(image, name);
        }

        await Assert.That(near).IsFalse().Because(image.Describe(x, y));
    }

    /// <summary>Saves an image as a PNG in the test output's render-failures folder.</summary>
    /// <param name="image">The image.</param>
    /// <param name="name">The file name without extension.</param>
    internal static void Save(RenderedImage image, string name)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "render-failures");
        _ = Directory.CreateDirectory(folder);
        using var encoded = SKImage.FromPixelCopy(new(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul), image.Pixels, image.Width * RenderedImage.BytesPerPixel);
        using var data = encoded.Encode(SKEncodedImageFormat.Png, PngQuality);
        using var file = File.Create(Path.Combine(folder, $"{name}.png"));
        data.SaveTo(file);
    }
}
