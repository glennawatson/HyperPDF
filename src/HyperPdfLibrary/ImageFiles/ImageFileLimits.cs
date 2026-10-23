// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.ImageFiles;

/// <summary>The size limits every picture reader keeps to, so a small file cannot ask for gigabytes of pixels.</summary>
internal static class ImageFileLimits
{
    /// <summary>The most pixels a picture may have.</summary>
    internal const long MaxPixels = 1L << 27;

    /// <summary>The most a picture may be wide or tall.</summary>
    internal const int MaxSide = 65_535;

    /// <summary>Checks the size a file declares.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <exception cref="InvalidDataException">The size is not positive, or is more than the limits allow.</exception>
    internal static void CheckSize(long width, long height)
    {
        if (width <= 0 || height <= 0 || width > MaxSide || height > MaxSide || width * height > MaxPixels)
        {
            throw new InvalidDataException("The picture's size is zero or larger than this library reads.");
        }
    }
}
