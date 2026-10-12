// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Creates tiny, explicit policy inputs without invoking a renderer.</summary>
internal static class ComparisonTestRasters
{
    /// <summary>The square edge used by the policy unit tests.</summary>
    internal const int Edge = 2;

    /// <summary>Gets opaque white.</summary>
    internal static Bgra32Color White => new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

    /// <summary>Gets opaque red.</summary>
    internal static Bgra32Color Red => new(0, 0, byte.MaxValue, byte.MaxValue);

    /// <summary>Gets opaque blue.</summary>
    internal static Bgra32Color Blue => new(byte.MaxValue, 0, 0, byte.MaxValue);

    /// <summary>Creates a solid raster.</summary>
    /// <param name="color">The BGRA32 color.</param>
    /// <returns>The owned raster.</returns>
    internal static ComparisonRaster Solid(Bgra32Color color)
    {
        const int red = 2;
        const int alpha = 3;
        var pixels = new byte[Edge * Edge * ComparisonRaster.BytesPerPixel];
        for (var offset = 0; offset < pixels.Length; offset += ComparisonRaster.BytesPerPixel)
        {
            pixels[offset] = color.Blue;
            pixels[offset + 1] = color.Green;
            pixels[offset + red] = color.Red;
            pixels[offset + alpha] = color.Alpha;
        }

        return new(Edge, Edge, pixels);
    }

    /// <summary>Requires the entire tiny raster to have an independently specified color.</summary>
    /// <param name="color">The expected BGRA32 color.</param>
    /// <param name="tolerance">The allowed quantization error.</param>
    /// <returns>The policy oracle.</returns>
    internal static StandardsOracle Oracle(Bgra32Color color, byte tolerance) =>
        new(Edge, Edge, new ExpectedPixelRegion[] { new(0, 0, Edge, Edge, color, tolerance, "Unit-test independent expectation") });
}
