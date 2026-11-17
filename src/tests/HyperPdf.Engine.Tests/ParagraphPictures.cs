// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Engine.Tests;

/// <summary>Draws the area around a paragraph before and after it is retyped, to check how the edit looks: the paragraph's place changed and nothing round it did.</summary>
internal static class ParagraphPictures
{
    /// <summary>The bytes of a pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>How far, in points, the area reaches round the old paragraph to the sides and above it.</summary>
    private const int Margin = 60;

    /// <summary>How far, in points, a changed pixel may be from the old and the new paragraph and still be part of the edit; it covers the soft edge of the letters.</summary>
    private const float EdgeReach = 3F;

    /// <summary>How much further down than the old paragraph the area reaches, as a multiple of its height.</summary>
    private const float ExtraDown = 1.5F;

    /// <summary>Finds the area to draw round a paragraph.</summary>
    /// <param name="old">The box of the old paragraph.</param>
    /// <param name="page">The size of the page.</param>
    /// <returns>The area, in whole points of the page.</returns>
    internal static PictureArea AreaAround(PageRect old, PageSize page)
    {
        var left = (int)Math.Max(0F, MathF.Floor(old.Left) - Margin);
        var top = (int)Math.Max(0F, MathF.Floor(old.Top) - Margin);
        var right = (int)Math.Min(MathF.Ceiling(page.Width), MathF.Ceiling(old.Right) + Margin);
        var bottom = (int)Math.Min(MathF.Ceiling(page.Height), MathF.Ceiling(old.Bottom + (old.Height * ExtraDown)) + Margin);
        return new(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    /// <summary>Draws an area of a page at one pixel to the point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="area">The area.</param>
    /// <returns>The pixels.</returns>
    internal static byte[] Render(HyperPdfDocument document, int page, in PictureArea area)
    {
        var pixels = new byte[area.Width * area.Height * BytesPerPixel];
        var request = new PdfTileRequest(page, 1F, 0, area.X, area.Y, PdfRenderFlags.None);
        _ = HyperPdfRendering.GetRenderer(document).Render(request, new(pixels, area.Width, area.Height, area.Width * BytesPerPixel));
        return pixels;
    }

    /// <summary>Compares the pictures of an area before and after a paragraph was retyped.</summary>
    /// <param name="area">The area.</param>
    /// <param name="before">The pixels before.</param>
    /// <param name="after">The pixels after.</param>
    /// <param name="boxes">The boxes of the old and the new paragraph.</param>
    /// <returns>What is wrong with the picture, or <see langword="null"/> when nothing is.</returns>
    internal static string? Check(in PictureArea area, byte[] before, byte[] after, in EditedBoxes boxes)
    {
        var changedOutside = 0;
        var changedInside = 0;
        for (var y = 0; y < area.Height; y++)
        {
            for (var x = 0; x < area.Width; x++)
            {
                var at = ((y * area.Width) + x) * BytesPerPixel;
                if (before.AsSpan(at, BytesPerPixel).SequenceEqual(after.AsSpan(at, BytesPerPixel)))
                {
                    continue;
                }

                if (boxes.Reaches(area.X + x, area.Y + y, EdgeReach))
                {
                    changedInside++;
                }
                else
                {
                    changedOutside++;
                }
            }
        }

        if (changedOutside > 0)
        {
            return $"{changedOutside} pixels changed round the paragraph";
        }

        return changedInside == 0 ? "the picture did not change where the paragraph is" : null;
    }

    /// <summary>An area of a page, in whole points.</summary>
    /// <param name="X">The left edge.</param>
    /// <param name="Y">The top edge.</param>
    /// <param name="Width">The width.</param>
    /// <param name="Height">The height.</param>
    internal readonly record struct PictureArea(int X, int Y, int Width, int Height);

    /// <summary>The boxes of a paragraph before and after it was retyped.</summary>
    /// <param name="Old">The old box.</param>
    /// <param name="New">The new box.</param>
    internal readonly record struct EditedBoxes(PageRect Old, PageRect New)
    {
        /// <summary>Tells whether a point is in either box, or within a reach of it.</summary>
        /// <param name="x">The x of the point.</param>
        /// <param name="y">The y of the point.</param>
        /// <param name="reach">How far from a box the point may be.</param>
        /// <returns><see langword="true"/> when it is.</returns>
        internal bool Reaches(float x, float y, float reach) => Near(Old, x, y, reach) || Near(New, x, y, reach);

        /// <summary>Tells whether a point is in a box or within a reach of it.</summary>
        /// <param name="box">The box.</param>
        /// <param name="x">The x of the point.</param>
        /// <param name="y">The y of the point.</param>
        /// <param name="reach">How far from the box the point may be.</param>
        /// <returns><see langword="true"/> when it is.</returns>
        private static bool Near(in PageRect box, float x, float y, float reach) =>
            x >= box.Left - reach && x <= box.Right + reach && y >= box.Top - reach && y <= box.Bottom + reach;
    }
}
