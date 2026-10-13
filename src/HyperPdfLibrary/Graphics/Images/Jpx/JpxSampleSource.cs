// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The decoded components and how to read them as channels. Rows are read in order, top to bottom, and the codestream
/// bytes must stay valid while they are, because the decoder works on a block of rows when the first of them is read.
/// </summary>
internal readonly ref struct JpxSampleSource
{
    /// <summary>Initializes a new instance of the <see cref="JpxSampleSource"/> struct.</summary>
    /// <param name="image">The decoder, which gives the rows of each component.</param>
    /// <param name="data">The codestream the decoder was opened for.</param>
    /// <param name="channels">The channels.</param>
    /// <param name="palette">The palette, or <see langword="null"/>.</param>
    /// <param name="width">The output width: the first channel's plane width.</param>
    /// <param name="height">The output height: the first channel's plane height.</param>
    internal JpxSampleSource(JpxDecoder image, ReadOnlySpan<byte> data, JpxChannel[] channels, JpxPalette? palette, int width, int height)
    {
        Image = image;
        Data = data;
        Channels = channels;
        Palette = palette;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the decoder, which gives the rows of each component.</summary>
    internal JpxDecoder Image { get; }

    /// <summary>Gets the codestream the decoder was opened for.</summary>
    internal ReadOnlySpan<byte> Data { get; }

    /// <summary>Gets the channels.</summary>
    internal JpxChannel[] Channels { get; }

    /// <summary>Gets the palette, or <see langword="null"/>.</summary>
    internal JpxPalette? Palette { get; }

    /// <summary>Gets the output width.</summary>
    internal int Width { get; }

    /// <summary>Gets the output height.</summary>
    internal int Height { get; }
}
