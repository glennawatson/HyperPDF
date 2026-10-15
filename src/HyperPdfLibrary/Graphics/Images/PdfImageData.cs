// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// A decoded image: premultiplied BGRA pixels, 8-bit gray pixels when the renderer asked for compact output, or an 8-bit
/// coverage mask for a stencil image mask. Images whose codec is not decoded yet carry no pixels and name the codec in
/// <see cref="UnsupportedCodec"/>.
/// </summary>
[DebuggerDisplay("PdfImageData: {Width}x{Height}, stencil {IsStencilMask}, unsupported {UnsupportedCodec}")]
public sealed class PdfImageData
{
    /// <summary>Initializes a new instance of the <see cref="PdfImageData"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="pixels">The pixels.</param>
    /// <param name="isStencilMask">Whether the pixels are a coverage mask.</param>
    /// <param name="interpolate">Whether the image asks for smoothing when scaled.</param>
    /// <param name="unsupportedCodec">The codec that was not decoded, or none.</param>
    public PdfImageData(int width, int height, byte[] pixels, bool isStencilMask, bool interpolate, PdfImageCodec unsupportedCodec)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
        IsStencilMask = isStencilMask;
        Interpolate = interpolate;
        UnsupportedCodec = unsupportedCodec;
    }

    /// <summary>Gets the width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    public int Height { get; }

    /// <summary>
    /// Gets the pixels: premultiplied BGRA with a stride of <see cref="Width"/> * 4, one gray byte per pixel when
    /// <see cref="IsGray"/> is set, or for a stencil mask one coverage byte per pixel where 255 paints. Empty when
    /// <see cref="UnsupportedCodec"/> is set.
    /// </summary>
    public byte[] Pixels { get; }

    /// <summary>Gets a value indicating whether <see cref="Pixels"/> is a coverage mask painted with the fill colour.</summary>
    public bool IsStencilMask { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Pixels"/> holds one opaque gray byte per pixel instead of BGRA. Only the
    /// renderer's decoding path produces gray images, so greyscale and bilevel pages cost a quarter of the memory.
    /// </summary>
    public bool IsGray { get; internal init; }

    /// <summary>Gets a value indicating whether the image asks for smoothing when scaled.</summary>
    public bool Interpolate { get; }

    /// <summary>Gets the codec that was not decoded (JPEG 2000 or JBIG2), or <see cref="PdfImageCodec.None"/>.</summary>
    public PdfImageCodec UnsupportedCodec { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Pixels"/> lives on the pinned object heap, so a backend can use the array as
    /// its pixel memory without a copy.
    /// </summary>
    public bool IsPinned { get; internal init; }

    /// <summary>Gets a value indicating whether the decoder already averaged the pixels down to the display size, so no further area reduction applies.</summary>
    internal bool IsBoxReduced { get; init; }
}
