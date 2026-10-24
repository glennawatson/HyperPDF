// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Annotations;

/// <summary>Tests for <see cref="PdfPageAnnotations"/>, appearances, images, fonts and metrics.</summary>
public sealed class PageAnnotationTests
{
    /// <summary>A soft red as 0xRRGGBB.</summary>
    private const uint Clay = 0xE8BCB4;

    /// <summary>A dark blue as 0xRRGGBB.</summary>
    private const uint Ink = 0x23324A;

    /// <summary>Two annotations.</summary>
    private const int Two = 2;

    /// <summary>The Helvetica advance of 'A', in ems.</summary>
    private const float HelveticaA = 0.667F;

    /// <summary>The WinAnsi code of the euro sign.</summary>
    private const byte EuroCode = 0x80;

    /// <summary>The euro sign.</summary>
    private const char Euro = '€';

    /// <summary>A font size.</summary>
    private const float Size = 10;

    /// <summary>The picture's side in pixels.</summary>
    private const int Side = 2;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>Half opacity.</summary>
    private const byte HalfAlpha = 0x80;

    /// <summary>A rectangle.</summary>
    private static readonly PdfRectangle Box = new(72, 600, 172, 650);

    /// <summary>Appending, replacing and removing annotations change the page's array, and a compact save keeps the result.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppendsReplacesAndRemoves()
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(1), null);
        var store = document.Objects;
        var page = PdfDocumentPages.GetPage(document, 0);
        var before = PdfPageAnnotations.GetArray(store, page)?.Count ?? 0;
        var first = PdfPageAnnotations.Append(store, page, PdfAnnotations.Create(store, KnownName.Square, Box));
        var second = PdfPageAnnotations.Append(store, page, PdfAnnotations.Create(store, KnownName.Circle, Box));
        var edited = PdfPageAnnotations.Get(store, page, first)!.Clone();
        PdfAnnotations.SetColor(edited, KnownName.C, Clay);
        var replaced = PdfPageAnnotations.Replace(store, page, first, edited);
        var removed = PdfPageAnnotations.RemoveAt(store, page, second);
        var reopened = Reopen(PdfCompactWriter.Save(store, PdfCompactOptions.Default));
        await Assert.That(first).IsEqualTo(before);
        await Assert.That(second).IsEqualTo(before + 1);
        await Assert.That(PdfPageAnnotations.GetId(store, page, first).IsValid).IsTrue();
        await Assert.That(replaced && removed).IsTrue();
        await Assert.That(reopened.Count).IsEqualTo(before + 1);
        await Assert.That(reopened[^1].IsName(KnownName.Subtype, KnownName.Square)).IsTrue();
        await Assert.That(PdfAnnotations.TryGetColor(reopened[^1], KnownName.C, out var color) && color == Clay).IsTrue();
    }

    /// <summary>An appearance is added as a form, read back, recoloured and dropped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesAndRecolorsAppearances()
    {
        using var store = StoreOpening.Open(TestPdf.Create(1), null);
        var annotation = PdfAnnotations.Create(store, KnownName.Stamp, Box);
        var form = Draw(store);
        _ = PdfAnnotations.SetNormalAppearance(store, annotation, form);
        var appearance = PdfAnnotations.GetNormalAppearance(annotation)!;
        var foundFirst = PdfAppearanceColors.TryReadFirstColor(appearance, store.Names, out var first);
        var recolored = PdfAppearanceColors.Recolor(appearance, Ink)!;
        var foundAfter = PdfAppearanceColors.TryReadFirstColor(recolored, store.Names, out var after);
        var content = Encoding.ASCII.GetString(recolored.DecodeToArray());
        await Assert.That(foundFirst && first == Clay).IsTrue();
        await Assert.That(foundAfter && after == Ink).IsTrue();
        await Assert.That(content).Contains(" re");
        await Assert.That(content).DoesNotContain("0.909804");
        await Assert.That(recolored.Dictionary.IsName(KnownName.Subtype, KnownName.Form)).IsTrue();
        await Assert.That(PdfAnnotations.RemoveAppearance(annotation)).IsTrue();
        await Assert.That(PdfAnnotations.GetNormalAppearance(annotation)).IsNull();
    }

    /// <summary>The standard metrics give Adobe's advances and encode Windows Latin characters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeasuresBuiltInFonts()
    {
        var encoded = AppearanceFontMetrics.TryEncode(Euro, out var euro);
        var ink = AppearanceFontMetrics.MeasureInk(AppearanceFont.Helvetica, "AV"u8, Size);
        var advance = AppearanceFontMetrics.MeasureAdvance(AppearanceFont.Helvetica, "AV"u8, Size);
        await Assert.That(AppearanceFontMetrics.GetAdvance(AppearanceFont.Helvetica, (byte)'A')).IsEqualTo(HelveticaA);
        await Assert.That(encoded && euro == EuroCode).IsTrue();
        await Assert.That(AppearanceFontMetrics.TryEncode('中', out _)).IsFalse();
        await Assert.That(ink.Width).IsGreaterThan(0);
        await Assert.That(ink.Right).IsLessThanOrEqualTo(advance);
        await Assert.That(AppearanceFontMetrics.GetBaseFontName(AppearanceFont.TimesBoldItalic).SequenceEqual("Times-BoldItalic"u8)).IsTrue();
    }

    /// <summary>A picture keeps its transparency in a soft mask only when it has some.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsImagesWithSoftMasks()
    {
        using var store = StoreOpening.Open(TestPdf.Create(1), null);
        var pixels = new byte[Side * Side * PixelBytes];
        Array.Fill(pixels, byte.MaxValue);
        var opaque = StoreReading.GetObject(store, PdfImages.AddBgraImage(store, pixels, Side, Side)).AsStream()!;
        pixels[PixelBytes - 1] = HalfAlpha;
        var soft = StoreReading.GetObject(store, PdfImages.AddBgraImage(store, pixels, Side, Side)).AsStream()!;
        await Assert.That(opaque.Dictionary.ContainsKey(KnownName.SMask)).IsFalse();
        await Assert.That(soft.Dictionary.ContainsKey(KnownName.SMask)).IsTrue();
        await Assert.That(soft.DecodeToArray().Length).IsEqualTo(Side * Side * (PixelBytes - 1));
    }

    /// <summary>A text map lists each code with text, as UTF-16.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesTextMaps()
    {
        var map = Encoding.ASCII.GetString(PdfToUnicodeMaps.Write([string.Empty, "A", null, "fi"]));
        await Assert.That(map).Contains("2 beginbfchar");
        await Assert.That(map).Contains("<0001> <0041>");
        await Assert.That(map).Contains("<0003> <00660069>");
    }

    /// <summary>Draws a framed box in a colour.</summary>
    /// <param name="store">The document.</param>
    /// <returns>The form.</returns>
    private static PdfStream Draw(PdfObjectStore store)
    {
        var builder = default(PdfContentBuilder);
        try
        {
            PdfAppearances.SetColors(ref builder, Clay);
            PdfAppearances.SetRoundLine(ref builder, 1);
            builder.Rectangle(Box.Left, Box.Bottom, Box.Width, Box.Height);
            builder.Stroke();
            PdfAppearances.AddCloud(ref builder, [new Vector2(Box.Left, Box.Bottom), new(Box.Right, Box.Bottom), new(Box.Right, Box.Top)], 1);
            builder.Stroke();
            return builder.ToFormXObject(store, Box, null);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Opens saved bytes and reads the first page's annotations.</summary>
    /// <param name="bytes">The file.</param>
    /// <returns>The annotations.</returns>
    private static List<PdfDictionary> Reopen(byte[] bytes)
    {
        var document = PdfDocumentReader.Open(bytes, null);
        var annotations = new List<PdfDictionary>();
        var array = PdfPageAnnotations.GetArray(document.Objects, PdfDocumentPages.GetPage(document, 0));
        for (var i = 0; i < (array?.Count ?? 0); i++)
        {
            annotations.Add(array!.GetDictionary(i)!);
        }

        return annotations;
    }
}
