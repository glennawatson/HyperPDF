// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Reviewed, independent expectations for selected regions of a well-formed PDF.</summary>
internal sealed record StandardsOracle
{
    /// <summary>Initializes a new instance of the <see cref="StandardsOracle"/> class.</summary>
    /// <param name="width">The expected raster width.</param>
    /// <param name="height">The expected raster height.</param>
    /// <param name="regions">The nonempty set of independently derived regions.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension or region coordinate is invalid.</exception>
    /// <exception cref="ArgumentException">Regions are empty, overlap or lack a clause.</exception>
    internal StandardsOracle(int width, int height, ReadOnlyMemory<ExpectedPixelRegion> regions)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (regions.IsEmpty)
        {
            throw new ArgumentException("An oracle must check at least one interior region.", nameof(regions));
        }

        var snapshot = regions.ToArray();
        ValidateRegions(snapshot, width, height);
        Width = width;
        Height = height;
        Regions = snapshot;
    }

    /// <summary>Gets the expected width.</summary>
    internal int Width { get; }

    /// <summary>Gets the expected height.</summary>
    internal int Height { get; }

    /// <summary>Gets the validated, disjoint expected regions.</summary>
    internal ReadOnlyMemory<ExpectedPixelRegion> Regions { get; }

    /// <summary>Checks every rectangle and prevents double-counted pixels.</summary>
    /// <param name="regions">The copied regions.</param>
    /// <param name="width">The raster width.</param>
    /// <param name="height">The raster height.</param>
    /// <exception cref="ArgumentException">Regions overlap.</exception>
    private static void ValidateRegions(ExpectedPixelRegion[] regions, int width, int height)
    {
        for (var index = 0; index < regions.Length; index++)
        {
            var region = regions[index];
            ValidateRegion(region, width, height);
            for (var previous = 0; previous < index; previous++)
            {
                if (Overlaps(region, regions[previous]))
                {
                    throw new ArgumentException("Oracle regions must not overlap.", nameof(regions));
                }
            }
        }
    }

    /// <summary>Checks a rectangle against the declared raster geometry.</summary>
    /// <param name="region">The expected rectangle.</param>
    /// <param name="width">The raster width.</param>
    /// <param name="height">The raster height.</param>
    private static void ValidateRegion(in ExpectedPixelRegion region, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(region.X);
        ArgumentOutOfRangeException.ThrowIfNegative(region.Y);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(region.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(region.Height);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(region.Width, width);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(region.Height, height);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(region.X, width - region.Width);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(region.Y, height - region.Height);
        ArgumentException.ThrowIfNullOrWhiteSpace(region.Clause);
    }

    /// <summary>Tests whether two validated rectangles intersect.</summary>
    /// <param name="first">The first rectangle.</param>
    /// <param name="second">The second rectangle.</param>
    /// <returns>Whether any pixel belongs to both rectangles.</returns>
    private static bool Overlaps(in ExpectedPixelRegion first, in ExpectedPixelRegion second) =>
        first.X < second.X + second.Width && second.X < first.X + first.Width
        && first.Y < second.Y + second.Height && second.Y < first.Y + first.Height;
}
