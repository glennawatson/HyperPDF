// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.Tests;

/// <summary>Counts the inked pixels in an area of a rendered page, to show a mark is really drawn there.</summary>
internal static class PageInk
{
    /// <summary>The bytes per BGRA pixel.</summary>
    private const int Channels = 4;

    /// <summary>The darkest channel value still counted as paper.</summary>
    private const int Paper = 200;

    /// <summary>The red channel's offset in BGRA.</summary>
    private const int RedOffset = 2;

    /// <summary>Renders an area of a page at one pixel per point and counts the pixels darker than paper.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="area">The area, in page space.</param>
    /// <param name="flags">How to render: with annotations, and for printing when checking printed output.</param>
    /// <returns>The inked pixels, or -1 when the page could not be rendered.</returns>
    internal static int Count(IDocument document, int page, PageRect area, RenderFlags flags)
    {
        var width = Math.Max(1, (int)Math.Ceiling(area.Width));
        var height = Math.Max(1, (int)Math.Ceiling(area.Height));
        var pixels = new byte[width * height * Channels];
        if (!document.Render(new(page, 1, PageRotation.None, (int)area.Left, (int)area.Top, flags), new(pixels, width, height, width * Channels)))
        {
            return -1;
        }

        var inked = 0;
        for (var offset = 0; offset < pixels.Length; offset += Channels)
        {
            if (Math.Min(pixels[offset], Math.Min(pixels[offset + 1], pixels[offset + RedOffset])) < Paper)
            {
                inked++;
            }
        }

        return inked;
    }
}
