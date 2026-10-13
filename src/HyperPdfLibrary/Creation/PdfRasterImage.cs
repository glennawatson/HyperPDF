// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>Decoded pixels to store as a lossless image, rows from top to bottom.</summary>
/// <param name="Format">How the pixels are laid out.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Pixels">The pixels, packed with no padding between rows beyond the format's own.</param>
[DebuggerDisplay("PdfRasterImage: {Width}x{Height} {Format}")]
public sealed record PdfRasterImage(PdfRasterFormat Format, int Width, int Height, ReadOnlyMemory<byte> Pixels)
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes of a gray pixel.</summary>
    private const int GrayBytes = 1;

    /// <summary>The bytes of an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>Gets the bytes in one row of pixels.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The format is not known.</exception>
    public int RowBytes => Format switch
    {
        PdfRasterFormat.Gray8 => checked(Width * GrayBytes),
        PdfRasterFormat.Rgb24 => checked(Width * RgbBytes),
        PdfRasterFormat.Bgra32 => checked(Width * BgraBytes),
        PdfRasterFormat.Bilevel1 => (Width + ByteBits - 1) / ByteBits,
        _ => throw new ArgumentOutOfRangeException(nameof(Format), Format, "The pixel format is not known."),
    };

    /// <summary>Checks that the size is positive and the pixels fill it.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive or the format is not known.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image.</exception>
    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Height);
        if (Pixels.Length < (long)RowBytes * Height)
        {
            throw new ArgumentException("The pixels do not fill the image.", nameof(Pixels));
        }
    }
}
