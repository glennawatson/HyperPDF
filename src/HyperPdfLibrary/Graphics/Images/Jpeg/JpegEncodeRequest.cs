// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>What <see cref="JpegEncoder"/> is asked to encode.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Format">The layout of the source pixels.</param>
/// <param name="Options">The coding options.</param>
internal readonly record struct JpegEncodeRequest(int Width, int Height, PdfJpegPixelFormat Format, PdfJpegOptions Options)
{
    /// <summary>Gets the sampling factors of each component as a high nibble for horizontal and a low nibble for vertical, or empty to take them from <see cref="Options"/>.</summary>
    internal ReadOnlyMemory<byte> Sampling { get; init; }

    /// <summary>Gets a value indicating whether a sequential frame codes each component in its own scan instead of one interleaved scan.</summary>
    internal bool NonInterleaved { get; init; }
}
