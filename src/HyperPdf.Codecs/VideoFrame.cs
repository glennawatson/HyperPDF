// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs;

/// <summary>One decoded picture in planar YUV 4:2:0 form with 8 bit samples.</summary>
[DebuggerDisplay("VideoFrame: {Width}x{Height} at {Time} s")]
public sealed class VideoFrame
{
    /// <summary>Initializes a new instance of the <see cref="VideoFrame"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="time">When the picture is shown, in seconds.</param>
    public VideoFrame(int width, int height, double time)
    {
        Width = width;
        Height = height;
        Time = time;
        ChromaWidth = (width + 1) >> 1;
        ChromaHeight = (height + 1) >> 1;
        Y = new byte[width * height];
        U = new byte[ChromaWidth * ChromaHeight];
        V = new byte[ChromaWidth * ChromaHeight];
    }

    /// <summary>Gets the width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the width of the colour planes in samples.</summary>
    public int ChromaWidth { get; }

    /// <summary>Gets the height of the colour planes in samples.</summary>
    public int ChromaHeight { get; }

    /// <summary>Gets or sets when the picture is shown, in seconds.</summary>
    public double Time { get; set; }

    /// <summary>Gets the luma plane, row after row with no padding.</summary>
    public byte[] Y { get; }

    /// <summary>Gets the Cb plane, row after row with no padding.</summary>
    public byte[] U { get; }

    /// <summary>Gets the Cr plane, row after row with no padding.</summary>
    public byte[] V { get; }
}
