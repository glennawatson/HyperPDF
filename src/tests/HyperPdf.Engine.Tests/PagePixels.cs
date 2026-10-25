// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Engine.Tests;

/// <summary>A whole page rendered at one pixel per point, for checking what an engine draws.</summary>
[DebuggerDisplay("PagePixels: {Width}x{Height}")]
internal sealed class PagePixels
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    internal const int BytesPerPixel = 4;

    /// <summary>A channel value below this counts as dark.</summary>
    private const byte Dark = 64;

    /// <summary>A fully white channel.</summary>
    private const byte White = 0xFF;

    /// <summary>The colour channels of a pixel, without alpha.</summary>
    private const int ColorChannels = 3;

    /// <summary>Initializes a new instance of the <see cref="PagePixels"/> class by rendering a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="flags">The render flags.</param>
    internal PagePixels(IDocument document, int pageIndex, RenderFlags flags)
    {
        var size = document.GetPageSizes()[pageIndex];
        Width = (int)MathF.Ceiling(size.Width);
        Height = (int)MathF.Ceiling(size.Height);
        Pixels = new byte[Width * Height * BytesPerPixel];
        Rendered = document.Render(new(pageIndex, 1, PageRotation.None, 0, 0, flags), new(Pixels, Width, Height, Width * BytesPerPixel));
    }

    /// <summary>Gets the width in pixels.</summary>
    internal int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    internal int Height { get; }

    /// <summary>Gets the BGRA pixels.</summary>
    internal byte[] Pixels { get; }

    /// <summary>Gets a value indicating whether the engine drew the page.</summary>
    internal bool Rendered { get; }

    /// <summary>Determines whether a pixel is dark.</summary>
    /// <param name="x">The x in pixels.</param>
    /// <param name="y">The y in pixels, from the top.</param>
    /// <returns><see langword="true"/> when its blue channel is dark.</returns>
    internal bool IsDark(int x, int y) => Pixels[Offset(x, y)] < Dark;

    /// <summary>Determines whether a pixel is not white.</summary>
    /// <param name="x">The x in pixels.</param>
    /// <param name="y">The y in pixels, from the top.</param>
    /// <returns><see langword="true"/> when any colour channel is below white.</returns>
    internal bool IsTinted(int x, int y) => Pixels.AsSpan(Offset(x, y), ColorChannels).ContainsAnyExcept(White);

    /// <summary>Counts the dark pixels in the whole page.</summary>
    /// <returns>The count.</returns>
    internal int CountDark()
    {
        var count = 0;
        for (var i = 0; i < Pixels.Length; i += BytesPerPixel)
        {
            count += Pixels[i] < Dark ? 1 : 0;
        }

        return count;
    }

    /// <summary>Gets a pixel's byte offset.</summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <returns>The offset.</returns>
    private int Offset(int x, int y) => ((y * Width) + x) * BytesPerPixel;
}
