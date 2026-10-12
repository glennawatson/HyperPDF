// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.Controls;

/// <summary>Calculates where the thumbnail list scrolls to show the current page, leaving it still while that page is in full view.</summary>
internal static class ThumbnailViewport
{
    /// <summary>Half, for finding a middle.</summary>
    private const double Half = 0.5;

    /// <summary>Gets the offset that shows a thumbnail: unchanged when it is fully visible, otherwise centred.</summary>
    /// <param name="offset">The current vertical offset.</param>
    /// <param name="viewport">The viewport height.</param>
    /// <param name="top">The thumbnail's top in content coordinates.</param>
    /// <param name="bottom">The thumbnail's bottom in content coordinates.</param>
    /// <param name="extent">The content height.</param>
    /// <returns>The offset.</returns>
    internal static double GetOffset(double offset, double viewport, double top, double bottom, double extent) =>
        viewport <= 0 || (top >= offset && bottom <= offset + viewport)
            ? offset
            : Math.Clamp(((top + bottom) * Half) - (viewport * Half), 0, Math.Max(0, extent - viewport));
}
