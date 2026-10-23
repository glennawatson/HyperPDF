// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Barcodes;

/// <summary>
/// Draws a <see cref="PdfBarcode"/> on a <see cref="PdfPageCanvas"/> as vector rectangles, so it stays sharp at any size. The
/// box includes the quiet zone that scanners need around the bars: 10 modules at each end of a linear barcode, 4 around a QR
/// Code and 1 around a Data Matrix. In a tagged document the call must be inside
/// <see cref="PdfPageCanvas.BeginContent(PdfTagElement)"/> or <see cref="PdfPageCanvas.BeginArtifact(PdfArtifactKind)"/>.
/// </summary>
public static class PdfCanvasBarcodeMixins
{
    /// <summary>The quiet zone at each end of a linear barcode, in modules.</summary>
    private const int LinearQuietZone = 10;

    /// <summary>The quiet zone around a QR Code, in modules.</summary>
    private const int QrQuietZone = 4;

    /// <summary>The quiet zone around a Data Matrix, in modules.</summary>
    private const int DataMatrixQuietZone = 1;

    /// <summary>The two sides a quiet zone is on.</summary>
    private const int BothSides = 2;

    /// <summary>Half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>Adds barcode drawing to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(ref PdfPageCanvas canvas)
    {
        /// <summary>Draws a barcode to fit a box. A square barcode keeps square modules and is centred; a linear barcode stretches its bars to the box's height.</summary>
        /// <param name="barcode">The barcode.</param>
        /// <param name="style">The colours.</param>
        /// <param name="box">The box to fill, quiet zone included.</param>
        /// <exception cref="ArgumentNullException"><paramref name="barcode"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The box has no width or height.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        public void DrawBarcode(PdfBarcode barcode, PdfBarcodeStyle style, PdfCanvasBox box)
        {
            ArgumentNullException.ThrowIfNull(barcode);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(box.Width, 0F);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(box.Height, 0F);
            canvas.RequireMarked();
            canvas.Content.SaveState();
            var quiet = GetQuietZone(barcode);
            var layout = GetLayout(barcode, box, quiet);
            if (style.Background is { } background)
            {
                background.ApplyFill(ref canvas.Content);
                canvas.Content.Rectangle(box.X, box.Y, box.Width, box.Height);
                canvas.Content.Fill();
            }

            style.Bars.ApplyFill(ref canvas.Content);
            AddModules(ref canvas, barcode, layout);
            canvas.Content.Fill();
            canvas.Content.RestoreState();
        }
    }

    /// <summary>Gets the quiet zone a barcode needs.</summary>
    /// <param name="barcode">The barcode.</param>
    /// <returns>The modules of quiet zone.</returns>
    private static int GetQuietZone(PdfBarcode barcode) => barcode.Symbology switch
    {
        PdfBarcodeSymbology.QrCode => QrQuietZone,
        PdfBarcodeSymbology.DataMatrix => DataMatrixQuietZone,
        _ => LinearQuietZone,
    };

    /// <summary>Works out where the first module is and how big modules are.</summary>
    /// <param name="barcode">The barcode.</param>
    /// <param name="box">The box to fill.</param>
    /// <param name="quiet">The quiet zone in modules.</param>
    /// <returns>The layout.</returns>
    private static BarcodeLayout GetLayout(PdfBarcode barcode, PdfCanvasBox box, int quiet)
    {
        var wide = barcode.Columns + (BothSides * quiet);
        if (barcode.IsLinear)
        {
            var step = box.Width / wide;
            return new(box.X + (quiet * step), box.Y, step, box.Height);
        }

        var tall = barcode.Rows + (BothSides * quiet);
        var size = MathF.Min(box.Width / wide, box.Height / tall);
        var left = box.X + ((box.Width - (size * wide)) * Half) + (quiet * size);
        var top = box.Y + box.Height - (((box.Height - (size * tall)) * Half) + (quiet * size));
        return new(left, top - (size * barcode.Rows), size, size);
    }

    /// <summary>Adds a rectangle for each run of dark modules in a row, all in one path so neighbouring runs leave no seams.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="barcode">The barcode.</param>
    /// <param name="layout">Where the modules are.</param>
    private static void AddModules(ref PdfPageCanvas canvas, PdfBarcode barcode, BarcodeLayout layout)
    {
        for (var row = 0; row < barcode.Rows; row++)
        {
            var y = layout.Y + ((barcode.Rows - 1 - row) * layout.Height);
            var column = 0;
            while (column < barcode.Columns)
            {
                if (!barcode.IsDark(column, row))
                {
                    column++;
                    continue;
                }

                var start = column;
                while (column < barcode.Columns && barcode.IsDark(column, row))
                {
                    column++;
                }

                canvas.Content.Rectangle(layout.X + (start * layout.Width), y, (column - start) * layout.Width, layout.Height);
            }
        }
    }
}
