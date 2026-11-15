// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// Tables that reshape the coverage of a shape before it is blended: one that makes edges sharp by turning each pixel fully on or
/// off, and one for each level of darker text. They are built once and never change.
/// </summary>
internal static class CoverageTables
{
    /// <summary>The number of byte values.</summary>
    private const int Values = 256;

    /// <summary>The coverage from which a pixel counts as covered when edges are sharp.</summary>
    private const int Half = 128;

    /// <summary>The largest byte, which stands for one.</summary>
    private const float Full = 255F;

    /// <summary>How much each level of darker text raises the power the uncovered part is taken to.</summary>
    private const float ContrastStep = 0.6F;

    /// <summary>The darkest level of text.</summary>
    private const int Levels = 3;

    /// <summary>The table for each level of darker text, from level one.</summary>
    private static readonly byte[][] Darker = BuildDarker();

    /// <summary>Gets the table that turns each pixel fully on or off, so an edge has no grey pixels.</summary>
    internal static byte[] Sharp { get; } = BuildSharp();

    /// <summary>Gets the table for a level of darker text.</summary>
    /// <param name="level">The level, where 0 leaves text as it is.</param>
    /// <returns>The table, or <see langword="null"/> for level 0.</returns>
    internal static byte[]? Contrast(int level) => level <= 0 ? null : Darker[Math.Min(level, Levels) - 1];

    /// <summary>Reshapes a line of coverage.</summary>
    /// <param name="coverage">The coverage of each pixel, which receives the reshaped values.</param>
    /// <param name="table">The table.</param>
    internal static void Apply(Span<byte> coverage, byte[] table)
    {
        foreach (ref var value in coverage)
        {
            value = table[value];
        }
    }

    /// <summary>Builds the table for sharp edges.</summary>
    /// <returns>The table.</returns>
    private static byte[] BuildSharp()
    {
        var table = new byte[Values];
        table.AsSpan(Half).Fill(byte.MaxValue);
        return table;
    }

    /// <summary>Builds the tables for darker text. Each one shrinks the uncovered part of a pixel, so thin strokes gain weight and full pixels stay full.</summary>
    /// <returns>The tables, from level one.</returns>
    private static byte[][] BuildDarker()
    {
        var tables = new byte[Levels][];
        for (var level = 0; level < Levels; level++)
        {
            var power = 1F + (ContrastStep * (level + 1));
            var table = new byte[Values];
            for (var value = 0; value < Values; value++)
            {
                table[value] = (byte)MathF.Round(Full * (1F - MathF.Pow(1F - (value / Full), power)));
            }

            tables[level] = table;
        }

        return tables;
    }
}
