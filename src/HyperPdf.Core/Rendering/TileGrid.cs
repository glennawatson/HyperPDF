// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Rendering;

/// <summary>Tile maths shared by the canvas and the scheduler.</summary>
public static class TileGrid
{
    /// <summary>The quantisation steps per unit of scale.</summary>
    private const float ScaleSteps = 1024F;

    /// <summary>The tile edge length.</summary>
    private const int TileEdge = 512;

    /// <summary>The preview width.</summary>
    private const int PreviewEdge = 220;

    /// <summary>The largest zoom factor rendered at full resolution.</summary>
    private const float MaxZoom = 64F;

    /// <summary>Device independent pixels per point at 100% zoom.</summary>
    private const float PixelsPerPoint = 96F / 72F;

    /// <summary>Gets the edge length of a tile in device pixels.</summary>
    public static int TileSize => TileEdge;

    /// <summary>Gets the width, in device pixels, of page preview images used for thumbnails and as a placeholder.</summary>
    public static int PreviewWidth => PreviewEdge;

    /// <summary>Gets the largest render scale permitted, in device pixels per point (6400% at 96 DPI).</summary>
    public static float MaxScale => MaxZoom * PixelsPerPoint;

    /// <summary>Quantises a render scale so that scales which differ only by rounding share tiles.</summary>
    /// <param name="scale">Device pixels per point.</param>
    /// <returns>The scale key.</returns>
    public static int ToScaleKey(float scale) => (int)MathF.Round(scale * ScaleSteps);

    /// <summary>Converts a scale key back to a scale.</summary>
    /// <param name="scaleKey">The scale key.</param>
    /// <returns>Device pixels per point.</returns>
    public static float FromScaleKey(int scaleKey) => scaleKey / ScaleSteps;

    /// <summary>Gets the pixel size of a whole rotated page at a scale.</summary>
    /// <param name="size">The unrotated page size.</param>
    /// <param name="rotation">The rotation.</param>
    /// <param name="scale">Device pixels per point.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    public static void GetPagePixelSize(PageSize size, PageRotation rotation, float scale, out int width, out int height)
    {
        var rotated = size.Rotate(rotation);
        width = Math.Max(1, (int)MathF.Ceiling(rotated.Width * scale));
        height = Math.Max(1, (int)MathF.Ceiling(rotated.Height * scale));
    }

    /// <summary>Gets the scale used to render a preview image of a page.</summary>
    /// <param name="size">The unrotated page size.</param>
    /// <param name="rotation">The rotation.</param>
    /// <returns>Device pixels per point.</returns>
    public static float GetPreviewScale(PageSize size, PageRotation rotation)
    {
        var rotated = size.Rotate(rotation);
        return rotated.Width <= 0 ? 1F : PreviewWidth / rotated.Width;
    }

    /// <summary>Gets the number of tile columns and rows for a page image.</summary>
    /// <param name="pixelWidth">The page width in pixels.</param>
    /// <param name="pixelHeight">The page height in pixels.</param>
    /// <param name="columns">The column count.</param>
    /// <param name="rows">The row count.</param>
    public static void GetTileCounts(int pixelWidth, int pixelHeight, out int columns, out int rows)
    {
        columns = (pixelWidth + TileSize - 1) / TileSize;
        rows = (pixelHeight + TileSize - 1) / TileSize;
    }

    /// <summary>Gets the pixel size of a tile, accounting for clipped edge tiles.</summary>
    /// <param name="pixelWidth">The page width in pixels.</param>
    /// <param name="pixelHeight">The page height in pixels.</param>
    /// <param name="column">The tile column.</param>
    /// <param name="row">The tile row.</param>
    /// <param name="width">The tile width.</param>
    /// <param name="height">The tile height.</param>
    public static void GetTileSize(int pixelWidth, int pixelHeight, int column, int row, out int width, out int height)
    {
        width = Math.Min(TileSize, pixelWidth - (column * TileSize));
        height = Math.Min(TileSize, pixelHeight - (row * TileSize));
    }

    /// <summary>Gets the tiles of a page that cover an area, measured in device pixels from the page's top-left corner.</summary>
    /// <param name="pixelWidth">The page width in pixels.</param>
    /// <param name="pixelHeight">The page height in pixels.</param>
    /// <param name="left">The area's left edge.</param>
    /// <param name="top">The area's top edge.</param>
    /// <param name="right">The area's right edge.</param>
    /// <param name="bottom">The area's bottom edge.</param>
    /// <returns>The tiles, clamped to the page.</returns>
    public static TileWindow GetTileWindow(int pixelWidth, int pixelHeight, double left, double top, double right, double bottom)
    {
        GetTileCounts(pixelWidth, pixelHeight, out var columns, out var rows);
        return new(
            Math.Clamp((int)(left / TileSize), 0, columns - 1),
            Math.Clamp((int)(right / TileSize), 0, columns - 1),
            Math.Clamp((int)(top / TileSize), 0, rows - 1),
            Math.Clamp((int)(bottom / TileSize), 0, rows - 1));
    }
}
