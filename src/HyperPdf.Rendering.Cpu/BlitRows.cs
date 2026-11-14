// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Cpu;

/// <summary>The rows of a frame that a blit touches, after clipping to the frame.</summary>
/// <param name="Left">The first column.</param>
/// <param name="Top">The first row.</param>
/// <param name="Right">One past the last column.</param>
/// <param name="Bottom">One past the last row.</param>
[DebuggerDisplay("BlitRows ({Left}, {Top}) to ({Right}, {Bottom})")]
internal readonly record struct BlitRows(int Left, int Top, int Right, int Bottom)
{
    /// <summary>Gets a value indicating whether nothing is left to draw.</summary>
    internal bool IsEmpty => Left >= Right || Top >= Bottom;

    /// <summary>Clips a request to a frame.</summary>
    /// <param name="request">The request.</param>
    /// <param name="frame">The frame shape.</param>
    /// <returns>The clipped rows.</returns>
    internal static BlitRows Clip(in BlitRequest request, in PixelGrid frame) => new(
        Math.Max(request.X, 0),
        Math.Max(request.Y, 0),
        Math.Min(request.X + request.Width, frame.Width),
        Math.Min(request.Y + request.Height, frame.Height));
}
