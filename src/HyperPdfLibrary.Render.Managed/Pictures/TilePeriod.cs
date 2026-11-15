// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Render.Managed.Pictures;

/// <summary>
/// Makes the picture of one period of a tiling pattern. A pattern cell can be larger than the step between cells, so the period shows
/// every copy of the cell that reaches into it, each clipped to the cell's box (ISO 32000-2, 8.7.3).
/// </summary>
internal static class TilePeriod
{
    /// <summary>The most copies drawn each way, which bounds the work for a cell far larger than its step.</summary>
    private const int MaxCopies = 64;

    /// <summary>The steps one copy takes: save, clip, draw and restore.</summary>
    private const int StepsPerCopy = 4;

    /// <summary>Makes the picture of one period.</summary>
    /// <param name="cell">The pattern cell.</param>
    /// <param name="box">The cell's box, in the pattern's space.</param>
    /// <param name="tile">The period, in the pattern's space.</param>
    /// <returns>The picture.</returns>
    internal static ManagedPicture Compose(ManagedPicture cell, PdfRectangle box, PdfRect tile)
    {
        var width = tile.Right - tile.Left;
        var height = tile.Bottom - tile.Top;
        var builder = new PdfPathBuilder();
        builder.AddRect(new(box.Left, box.Bottom, box.Right, box.Top));
        var bounds = builder.Detach();
        var firstColumn = CopyIndex(tile.Left - box.Right, width, false);
        var lastColumn = CopyIndex(tile.Right - box.Left, width, true);
        var firstRow = CopyIndex(tile.Top - box.Top, height, false);
        var lastRow = CopyIndex(tile.Bottom - box.Bottom, height, true);
        var ops = new List<PictureOp>((lastColumn - firstColumn + 1) * (lastRow - firstRow + 1) * StepsPerCopy);
        for (var column = firstColumn; column <= lastColumn; column++)
        {
            for (var row = firstRow; row <= lastRow; row++)
            {
                var moved = Matrix3x2.CreateTranslation(column * width, row * height);
                ops.Add(new(PictureOpKind.Save, Matrix3x2.Identity));
                ops.Add(new(PictureOpKind.Clip, moved) { Path = bounds });
                ops.Add(new(PictureOpKind.Picture, moved) { Picture = cell });
                ops.Add(new(PictureOpKind.Restore, Matrix3x2.Identity));
            }
        }

        // The steps only place copies of the cell. The cell is a picture of its own and keeps its own style.
        return new([.. ops], default);
    }

    /// <summary>Finds the first or last copy that reaches into the period along one axis.</summary>
    /// <param name="distance">The distance from the cell's far edge to the period's near edge, or the other way round.</param>
    /// <param name="step">The step between copies.</param>
    /// <param name="last">Whether the last copy is wanted rather than the first.</param>
    /// <returns>The copy's index, where 0 is the cell itself.</returns>
    private static int CopyIndex(float distance, float step, bool last)
    {
        var index = distance / step;
        var rounded = last ? MathF.Ceiling(index) : MathF.Floor(index);
        return float.IsFinite(rounded) ? (int)Math.Clamp(rounded, -MaxCopies, MaxCopies) : 0;
    }
}
