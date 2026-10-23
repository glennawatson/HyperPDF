// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Barcodes;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Barcodes;

/// <summary>Draws barcodes on a page and checks the rendered pixels against the modules.</summary>
public sealed class BarcodeDrawingTests
{
    /// <summary>The page side in points and pixels.</summary>
    private const int Side = 200;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of the red channel in a BGRA pixel.</summary>
    private const int RedIndex = 2;

    /// <summary>The channel level below which a pixel counts as dark.</summary>
    private const byte DarkLevel = 128;

    /// <summary>The quiet zone around a QR Code, in modules.</summary>
    private const int QrQuietZone = 4;

    /// <summary>The quiet zone at the end of a linear barcode, in modules.</summary>
    private const int LinearQuietZone = 10;

    /// <summary>The sides a quiet zone is on.</summary>
    private const int BothSides = 2;

    /// <summary>The left and bottom of the box the barcode fills.</summary>
    private const float BoxStart = 20F;

    /// <summary>The side of the box a square barcode fills.</summary>
    private const float BoxSide = 160F;

    /// <summary>The height of the box a linear barcode fills.</summary>
    private const float LinearHeight = 40F;

    /// <summary>Half, for finding the middle of a module.</summary>
    private const float Half = 0.5F;

    /// <summary>Every module of a QR Code lands in the middle of its square on the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task QrModulesLandWhereTheyShould()
    {
        var barcode = PdfBarcode.CreateQr("HELLO", PdfQrErrorCorrection.Quartile);
        var pixels = Draw(barcode, new(BoxStart, BoxStart, BoxSide, BoxSide));
        var size = BoxSide / (barcode.Columns + (BothSides * QrQuietZone));

        var wrong = 0;
        for (var row = 0; row < barcode.Rows; row++)
        {
            for (var column = 0; column < barcode.Columns; column++)
            {
                var x = BoxStart + ((column + QrQuietZone + Half) * size);
                var y = BoxStart + BoxSide - ((row + QrQuietZone + Half) * size);
                if (IsDark(pixels, x, y) != barcode.IsDark(column, row))
                {
                    wrong++;
                }
            }
        }

        await Assert.That(wrong).IsEqualTo(0);
    }

    /// <summary>The quiet zone around a barcode is painted with the background and the page stays bare outside the box.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheBackgroundCoversTheBoxOnly()
    {
        var barcode = PdfBarcode.CreateQr("HELLO", PdfQrErrorCorrection.Low);
        var creator = new PdfDocumentCreator(new());
        var canvas = creator.BeginPage(Side, Side);
        canvas.DrawBarcode(barcode, new(PdfCanvasColor.Black, PdfCanvasColor.Rgb(1F, 1F, 0F)), new(BoxStart, BoxStart, BoxSide, BoxSide));
        _ = canvas.Finish();
        canvas.Dispose();

        var pixels = Render(creator.ToArray());
        var inQuietZone = Channels(pixels, BoxStart + 1F, BoxStart + 1F);
        var outside = Channels(pixels, 1F, 1F);

        await Assert.That(inQuietZone.Blue).IsLessThan(DarkLevel);
        await Assert.That(inQuietZone.Red).IsGreaterThan(DarkLevel);
        await Assert.That(outside.Blue).IsGreaterThan(DarkLevel);
    }

    /// <summary>A linear barcode stretches its bars to the height of the box.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinearBarsFillTheBoxHeight()
    {
        var barcode = PdfBarcode.CreateCode39("A");
        var pixels = Draw(barcode, new(BoxStart, BoxStart, BoxSide, LinearHeight));
        var step = BoxSide / (barcode.Columns + (BothSides * LinearQuietZone));

        var wrong = 0;
        for (var column = 0; column < barcode.Columns; column++)
        {
            var x = BoxStart + ((column + LinearQuietZone + Half) * step);
            var dark = barcode.IsDark(column, 0);
            if (IsDark(pixels, x, BoxStart + Half) != dark || IsDark(pixels, x, BoxStart + LinearHeight - Half) != dark)
            {
                wrong++;
            }
        }

        await Assert.That(wrong).IsEqualTo(0);
    }

    /// <summary>Draws a barcode in black on white on a fresh page and renders it.</summary>
    /// <param name="barcode">The barcode.</param>
    /// <param name="box">The box it fills.</param>
    /// <returns>The premultiplied BGRA pixels.</returns>
    private static byte[] Draw(PdfBarcode barcode, PdfCanvasBox box)
    {
        var creator = new PdfDocumentCreator(new());
        var canvas = creator.BeginPage(Side, Side);
        canvas.DrawBarcode(barcode, PdfBarcodeStyle.Default, box);
        _ = canvas.Finish();
        canvas.Dispose();
        return Render(creator.ToArray());
    }

    /// <summary>Renders the first page at one pixel to the point.</summary>
    /// <param name="file">The PDF file.</param>
    /// <returns>The premultiplied BGRA pixels.</returns>
    private static byte[] Render(byte[] file)
    {
        using var document = SkiaTestRegistration.Open(file, null);
        using var renderer = new PdfPageRenderer(document);
        var pixels = new byte[Side * Side * BytesPerPixel];
        _ = renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, Side, Side, Side * BytesPerPixel));
        return pixels;
    }

    /// <summary>Determines whether a pixel is dark; the y counts up from the bottom of the page.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="x">The x in points.</param>
    /// <param name="y">The y in points from the bottom.</param>
    /// <returns><see langword="true"/> for a dark pixel.</returns>
    private static bool IsDark(byte[] pixels, float x, float y) => Channels(pixels, x, y).Blue < DarkLevel;

    /// <summary>Reads the colour of a pixel; the y given counts up from the bottom of the page.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="x">The x in points.</param>
    /// <param name="y">The y in points from the bottom.</param>
    /// <returns>The channels.</returns>
    private static PixelColor Channels(byte[] pixels, float x, float y)
    {
        var offset = (((Side - 1 - (int)y) * Side) + (int)x) * BytesPerPixel;
        return new(pixels[offset + RedIndex], pixels[offset + 1], pixels[offset]);
    }

    /// <summary>A pixel's colour.</summary>
    /// <param name="Red">The red channel.</param>
    /// <param name="Green">The green channel.</param>
    /// <param name="Blue">The blue channel.</param>
    private readonly record struct PixelColor(byte Red, byte Green, byte Blue);
}
