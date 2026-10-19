// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>Darkens the edges of text: a coverage value is raised to a power below one, which pulls partly covered pixels towards full cover.</summary>
internal static class GlyphMaskContrast
{
    /// <summary>The values of a coverage byte.</summary>
    private const int Levels = 256;

    /// <summary>The most a coverage byte can be.</summary>
    private const int Max = 255;

    /// <summary>The powers for each contrast level: level 0 leaves coverage as it is.</summary>
    private static readonly double[] Powers = [1, 0.8, 0.65, 0.5];

    /// <summary>The table for each contrast level, built once.</summary>
    private static readonly byte[][] Tables = BuildTables();

    /// <summary>Changes coverage values in place.</summary>
    /// <param name="coverage">The coverage bytes.</param>
    /// <param name="contrast">The contrast level, 0 to 3.</param>
    internal static void Apply(Span<byte> coverage, int contrast)
    {
        if (contrast <= 0 || contrast >= Tables.Length)
        {
            return;
        }

        var table = Tables[contrast];
        for (var i = 0; i < coverage.Length; i++)
        {
            coverage[i] = table[coverage[i]];
        }
    }

    /// <summary>Builds the table of every level.</summary>
    /// <returns>The tables.</returns>
    private static byte[][] BuildTables()
    {
        var tables = new byte[Powers.Length][];
        for (var level = 0; level < tables.Length; level++)
        {
            var table = new byte[Levels];
            for (var value = 0; value < Levels; value++)
            {
                table[value] = (byte)Math.Round(Max * Math.Pow(value / (double)Max, Powers[level]));
            }

            tables[level] = table;
        }

        return tables;
    }
}
