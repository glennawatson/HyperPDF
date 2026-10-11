// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Checks the rendered colour and placement of HyperPDF form highlights.</summary>
public sealed class FormHighlightRenderingTests
{
    /// <summary>The x of a point inside the empty, fillable field "Limited".</summary>
    private const int FillableX = 172;

    /// <summary>The y, from the top, of a point inside "Limited" (the page is 792 points high; its box runs 320 to 344).</summary>
    private const int FillableY = 460;

    /// <summary>The y, from the top, of a point inside the read-only field "Locked" (its box runs 360 to 384).</summary>
    private const int LockedY = 420;

    /// <summary>The x of a point outside every field.</summary>
    private const int BlankX = 500;

    /// <summary>The y, from the top, of a point outside every field.</summary>
    private const int BlankY = 100;

    /// <summary>How far one engine's channel may differ from the other's, in levels of 255.</summary>
    private const int Tolerance = 3;

    /// <summary>The tint colour the custom test sets.</summary>
    private const uint CustomColor = 0x336699U;

    /// <summary>The tint opacity the custom test sets.</summary>
    private const byte CustomAlpha = 128;

    /// <summary>The largest value of a channel.</summary>
    private const double Channel = 255;

    /// <summary>The bits to shift the red of a colour down by.</summary>
    private const int RedShift = 16;

    /// <summary>The bits to shift the green of a colour down by.</summary>
    private const int GreenShift = 8;

    /// <summary>The position of the blue channel in a BGRA pixel.</summary>
    private const int BlueSlot = 0;

    /// <summary>The position of the green channel in a BGRA pixel.</summary>
    private const int GreenSlot = 1;

    /// <summary>The position of the red channel in a BGRA pixel.</summary>
    private const int RedSlot = 2;

    /// <summary>The default tint covers the fillable field and leaves the read-only field and blank page white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultTintCoversOnlyFillableFields()
    {
        using var input = new HyperPdfTestDocument(FormSamples.CreateRichForm());
        var hyper = new PagePixels(input.Document, 0, RenderFlags.Annotations);

        await Assert.That(hyper.IsTinted(FillableX, FillableY)).IsTrue();
        await Assert.That(hyper.IsTinted(FillableX, LockedY)).IsFalse();
        await Assert.That(hyper.IsTinted(BlankX, BlankY)).IsFalse();
    }

    /// <summary>A custom colour and opacity appear with the red, green and blue in the right order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CustomTintUsesSpecifiedColorAndOpacity()
    {
        using var input = new HyperPdfTestDocument(FormSamples.CreateRichForm());
        DocumentFeature<IFormHighlight>.Cast(input.Document)!.Highlight = new(CustomColor, CustomAlpha);
        var hyper = new PagePixels(input.Document, 0, RenderFlags.Annotations);

        await Assert.That(Math.Abs(Read(hyper, FillableX, FillableY, RedSlot) - Expected(CustomColor >> RedShift))).IsLessThanOrEqualTo(Tolerance);
        await Assert.That(Math.Abs(Read(hyper, FillableX, FillableY, GreenSlot) - Expected(CustomColor >> GreenShift))).IsLessThanOrEqualTo(Tolerance);
        await Assert.That(Math.Abs(Read(hyper, FillableX, FillableY, BlueSlot) - Expected(CustomColor))).IsLessThanOrEqualTo(Tolerance);
    }

    /// <summary>An opacity of zero turns the tint off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZeroOpacityLeavesTheFieldUntinted()
    {
        using var input = new HyperPdfTestDocument(FormSamples.CreateRichForm());
        DocumentFeature<IFormHighlight>.Cast(input.Document)!.Highlight = new(CustomColor, 0);
        var hyper = new PagePixels(input.Document, 0, RenderFlags.Annotations);

        await Assert.That(hyper.IsTinted(FillableX, FillableY)).IsFalse();
    }

    /// <summary>Works out the channel a colour channel gives over white at the custom opacity.</summary>
    /// <param name="channel">The colour's channel, in the low byte.</param>
    /// <returns>The blended value.</returns>
    private static int Expected(uint channel)
    {
        var value = channel & byte.MaxValue;
        return (int)Math.Round(Channel - ((Channel - value) * (CustomAlpha / Channel)));
    }

    /// <summary>Reads one channel of a pixel.</summary>
    /// <param name="page">The page.</param>
    /// <param name="x">The x.</param>
    /// <param name="y">The y, from the top.</param>
    /// <param name="slot">The channel's position in a BGRA pixel.</param>
    /// <returns>The channel.</returns>
    private static int Read(PagePixels page, int x, int y, int slot) => page.Pixels[(((y * page.Width) + x) * PagePixels.BytesPerPixel) + slot];
}
