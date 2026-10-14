// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Samples to compress as JPEG 2000. The samples are interleaved by pixel, rows from top to bottom with no padding. A
/// sample of up to 8 bits takes one byte; a sample of 9 to 16 bits takes two bytes, most significant first, as in a PDF
/// image. Values run from 0 to 2^bits - 1.
/// </summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Components">The channels per pixel, colour channels first, then the opacity channel when <see cref="HasAlpha"/> is set.</param>
/// <param name="BitsPerComponent">The bits of every sample, from 1 to 16.</param>
/// <param name="Samples">The samples.</param>
[DebuggerDisplay("PdfJpxImage: {Width}x{Height}, {Components} components of {BitsPerComponent} bits")]
public sealed record PdfJpxImage(int Width, int Height, int Components, int BitsPerComponent, ReadOnlyMemory<byte> Samples)
{
    /// <summary>The widest sample.</summary>
    internal const int MaxBitsPerComponent = 16;

    /// <summary>The most components.</summary>
    internal const int MaxComponents = 32;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes of a sample of more than eight bits.</summary>
    private const int WideBytes = 2;

    /// <summary>The fewest channels of an image with an opacity channel: one colour channel and the opacity.</summary>
    private const int MinAlphaChannels = 2;

    /// <summary>Gets a value indicating whether the last channel is opacity (not premultiplied) rather than colour. The file then declares it in a channel definition box.</summary>
    public bool HasAlpha { get; init; }

    /// <summary>Gets the colour space the file declares; the default follows the number of colour channels.</summary>
    public PdfJpxColorSpace ColorSpace { get; init; }

    /// <summary>
    /// Gets an ICC profile to declare as the colour specification in place of an enumerated colour space, or an empty value.
    /// The profile must describe as many components as the image has colour channels.
    /// </summary>
    public ReadOnlyMemory<byte> IccProfile { get; init; }

    /// <summary>Gets the bytes of one sample.</summary>
    public int BytesPerSample => BitsPerComponent > ByteBits ? WideBytes : 1;

    /// <summary>Gets the number of colour channels: the components without the opacity channel.</summary>
    public int ColourChannels => HasAlpha ? Components - 1 : Components;

    /// <summary>Checks the size, depth, colour description and sample count.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A size, depth or channel count is out of range.</exception>
    /// <exception cref="ArgumentException">The samples do not fill the image, or the colour description does not fit the channels.</exception>
    public void Validate()
    {
        ValidateShape();
        if (Samples.Length < (long)Width * Height * Components * BytesPerSample)
        {
            throw new ArgumentException("The samples do not fill the image.", nameof(Samples));
        }
    }

    /// <summary>Checks the size, depth and channels, but not the samples, for callers that pass the samples separately.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A size, depth or channel count is out of range.</exception>
    /// <exception cref="ArgumentException">The opacity flag does not fit the channels.</exception>
    internal void ValidateShape()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Components);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Components, MaxComponents);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(BitsPerComponent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(BitsPerComponent, MaxBitsPerComponent);
        if (HasAlpha && Components < MinAlphaChannels)
        {
            throw new ArgumentException("An image with an opacity channel needs at least one colour channel.", nameof(HasAlpha));
        }
    }
}
