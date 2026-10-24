// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.Video;

/// <summary>
/// Turns decoded pictures into blue, green, red and alpha bytes at one fixed size. The planes are resized first, while they are small,
/// and then converted; a picture that already has the size is converted as it is. The work areas are kept between pictures.
/// </summary>
[DebuggerDisplay("VideoScaler: {Width}x{Height}")]
public sealed class VideoScaler
{
    /// <summary>The smallest side of the target, so the colour planes have a sample.</summary>
    private const int MinSide = 2;

    /// <summary>The scaled luma plane.</summary>
    private readonly byte[] _luma;

    /// <summary>The scaled Cb plane.</summary>
    private readonly byte[] _blue;

    /// <summary>The scaled Cr plane.</summary>
    private readonly byte[] _red;

    /// <summary>Resizes the luma plane of pictures of the current source size, or <see langword="null"/> before the first picture.</summary>
    private PlaneResampler? _lumaResampler;

    /// <summary>Resizes the colour planes of pictures of the current source size.</summary>
    private PlaneResampler? _colourResampler;

    /// <summary>The width of the pictures the resamplers were made for.</summary>
    private int _sourceWidth;

    /// <summary>The height of the pictures the resamplers were made for.</summary>
    private int _sourceHeight;

    /// <summary>Initializes a new instance of the <see cref="VideoScaler"/> class.</summary>
    /// <param name="width">The width of the converted pictures in pixels, at least 2.</param>
    /// <param name="height">The height of the converted pictures in pixels, at least 2.</param>
    /// <exception cref="ArgumentOutOfRangeException">A side is below 2.</exception>
    public VideoScaler(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, MinSide);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, MinSide);
        Width = width;
        Height = height;
        _luma = new byte[width * height];
        _blue = new byte[ColourWidth * ColourHeight];
        _red = new byte[ColourWidth * ColourHeight];
    }

    /// <summary>Gets the width of the converted pictures in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the height of the converted pictures in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the bytes in one converted picture.</summary>
    public int ByteCount => Width * Height * sizeof(int);

    /// <summary>Gets the width of the colour planes of the converted pictures.</summary>
    private int ColourWidth => (Width + 1) >> 1;

    /// <summary>Gets the height of the colour planes of the converted pictures.</summary>
    private int ColourHeight => (Height + 1) >> 1;

    /// <summary>Converts a picture to the target size.</summary>
    /// <param name="frame">The decoded picture.</param>
    /// <param name="bgra">Receives <see cref="ByteCount"/> bytes: blue, green, red, alpha for each pixel.</param>
    /// <exception cref="ArgumentException"><paramref name="bgra"/> is too short.</exception>
    public void Scale(VideoFrame frame, Span<byte> bgra)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var matrix = ColorMatrix.For(frame.Height);
        if (frame.Width == Width && frame.Height == Height)
        {
            YuvConverter.ToBgra(frame, matrix, bgra);
            return;
        }

        Prepare(frame.Width, frame.Height);
        _lumaResampler!.Resample(frame.Y, _luma);
        _colourResampler!.Resample(frame.U, _blue);
        _colourResampler.Resample(frame.V, _red);
        YuvConverter.ToBgra(new YuvPlanes(_luma, _blue, _red, Width, Height), matrix, bgra);
    }

    /// <summary>Makes the resamplers for a source size, unless they exist already.</summary>
    /// <param name="sourceWidth">The width of the pictures.</param>
    /// <param name="sourceHeight">The height of the pictures.</param>
    private void Prepare(int sourceWidth, int sourceHeight)
    {
        if (_lumaResampler is not null && sourceWidth == _sourceWidth && sourceHeight == _sourceHeight)
        {
            return;
        }

        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
        _lumaResampler = new(sourceWidth, sourceHeight, Width, Height);
        _colourResampler = new((sourceWidth + 1) >> 1, (sourceHeight + 1) >> 1, ColourWidth, ColourHeight);
    }
}
