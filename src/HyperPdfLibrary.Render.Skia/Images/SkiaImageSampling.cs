// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using SkiaSharp;

namespace HyperPdfLibrary.Render.Skia.Images;

/// <summary>Chooses how Skia resamples an image that is drawn at another size than its own.</summary>
internal static class SkiaImageSampling
{
    /// <summary>Gets the sampling that smooths: bilinear with mipmaps, which averages a large reduction over the levels it passes.</summary>
    private static SKSamplingOptions Smoothed => new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>Gets the sampling that copies the nearest source pixel.</summary>
    private static SKSamplingOptions Nearest => new(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>Chooses the sampling for an image.</summary>
    /// <param name="imageWidth">The image width in pixels.</param>
    /// <param name="ctm">The matrix from the image's unit square to the page.</param>
    /// <param name="interpolate">Whether the image asks for smoothing when it is enlarged.</param>
    /// <param name="smoothImages">Whether scaled images are smoothed at all; when false the nearest source pixel is always used, which keeps 1-bit scans crisp.</param>
    /// <returns>The sampling options.</returns>
    internal static SKSamplingOptions Choose(int imageWidth, Matrix3x2 ctm, bool interpolate, bool smoothImages)
    {
        if (!smoothImages)
        {
            return Nearest;
        }

        var width = MathF.Sqrt((ctm.M11 * ctm.M11) + (ctm.M12 * ctm.M12));
        return interpolate || imageWidth >= width ? Smoothed : Nearest;
    }
}
