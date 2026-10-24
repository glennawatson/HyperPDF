// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Works out the widths of a grid's columns.</summary>
internal static class GridColumns
{
    /// <summary>Works out the width of each column in the room the table has.</summary>
    /// <param name="columns">The columns.</param>
    /// <param name="available">The width the table may take.</param>
    /// <returns>The width of each column in points.</returns>
    /// <exception cref="ArgumentException">There are no columns, or a column's width or weight is not positive.</exception>
    internal static float[] Resolve(IReadOnlyList<PdfGridColumn> columns, float available)
    {
        if (columns.Count == 0)
        {
            throw new ArgumentException("A grid needs at least one column.", nameof(columns));
        }

        var fixedSum = 0F;
        var starSum = 0F;
        foreach (var column in columns)
        {
            if (column.Value <= 0F || !float.IsFinite(column.Value))
            {
                throw new ArgumentException("A column's width or weight must be a positive number.", nameof(columns));
            }

            if (column.Kind == PdfColumnWidthKind.Fixed)
            {
                fixedSum += column.Value;
            }
            else
            {
                starSum += column.Value;
            }
        }

        var shrink = starSum == 0F && fixedSum > available ? available / fixedSum : 1F;
        var free = MathF.Max(0F, available - fixedSum);
        var widths = new float[columns.Count];
        for (var i = 0; i < widths.Length; i++)
        {
            widths[i] = columns[i].Kind == PdfColumnWidthKind.Fixed ? columns[i].Value * shrink : free * columns[i].Value / starSum;
        }

        return widths;
    }
}
