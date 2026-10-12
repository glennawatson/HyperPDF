// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Annotations;

/// <summary>A signature image with tightly packed, straight-alpha BGRA pixels.</summary>
[DebuggerDisplay("SignatureImage: {Width} x {Height}")]
public sealed record SignatureImage
{
    /// <summary>The bytes per BGRA pixel.</summary>
    private const int Channels = 4;

    /// <summary>The alpha channel offset.</summary>
    private const int AlphaOffset = 3;

    /// <summary>Initializes a new instance of the <see cref="SignatureImage"/> record.</summary>
    /// <param name="pixels">The owned pixels.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    private SignatureImage(byte[] pixels, int width, int height)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the image width.</summary>
    public int Width { get; }

    /// <summary>Gets the image height.</summary>
    public int Height { get; }

    /// <summary>Gets the pixels owned by this image.</summary>
    public ReadOnlyMemory<byte> Pixels { get; }

    /// <summary>Copies the source, optionally removes white paper, and trims transparent margins.</summary>
    /// <param name="pixels">The source BGRA pixels.</param>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="removePaper">Whether white paper should become transparent.</param>
    /// <returns>The prepared image, or null when it contains no visible pixels.</returns>
    /// <exception cref="ArgumentException">The buffer does not match the dimensions.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    /// <exception cref="OverflowException">The dimensions exceed the buffer size limit.</exception>
    public static SignatureImage? Create(ReadOnlySpan<byte> pixels, int width, int height, bool removePaper)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (pixels.Length != checked(width * height * Channels))
        {
            throw new ArgumentException("The buffer must match the image dimensions.", nameof(pixels));
        }

        var owned = pixels.ToArray();
        if (removePaper)
        {
            SignaturePixels.RemoveWhitePaper(owned);
        }

        return Trim(owned, width, height);
    }

    /// <summary>Trims transparent rows and columns.</summary>
    /// <param name="pixels">The owned pixels.</param>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    /// <returns>The visible image, or null.</returns>
    private static SignatureImage? Trim(byte[] pixels, int width, int height)
    {
        var left = width;
        var top = height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (pixels[(((y * width) + x) * Channels) + AlphaOffset] == 0)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < left)
        {
            return null;
        }

        var croppedWidth = right - left + 1;
        var croppedHeight = bottom - top + 1;
        if (croppedWidth == width && croppedHeight == height)
        {
            return new(pixels, width, height);
        }

        var stride = croppedWidth * Channels;
        var cropped = new byte[stride * croppedHeight];
        for (var row = 0; row < croppedHeight; row++)
        {
            pixels.AsSpan((((top + row) * width) + left) * Channels, stride).CopyTo(cropped.AsSpan(row * stride));
        }

        return new(cropped, croppedWidth, croppedHeight);
    }
}
