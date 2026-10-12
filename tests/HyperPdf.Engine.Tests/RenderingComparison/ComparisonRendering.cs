// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Renders actual document geometry into an owned comparison raster.</summary>
internal static class ComparisonRendering
{
    /// <summary>Renders one page at one pixel per point.</summary>
    /// <param name="document">The opened single-page document.</param>
    /// <returns>The actual owned raster.</returns>
    /// <exception cref="InvalidOperationException">The renderer did not produce the requested page.</exception>
    internal static ComparisonRaster Render(IDocument document)
    {
        var sizes = document.GetPageSizes();
        if (document.PageCount != 1 || sizes.Length != 1)
        {
            throw new InvalidOperationException("A comparison fixture must expose exactly one page.");
        }

        TileGrid.GetPagePixelSize(sizes[0], PageRotation.None, 1, out var width, out var height);
        var pixels = new byte[checked(width * height * ComparisonRaster.BytesPerPixel)];
        if (!document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, width, height, width * ComparisonRaster.BytesPerPixel)))
        {
            throw new InvalidOperationException("The comparison renderer did not render the fixture.");
        }

        return new(width, height, pixels);
    }
}
