// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.PageObjects;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the tools that work on a picture of a page: saving it, cropping it and drawing another picture in its place.</summary>
public sealed class PagePictureEditTests
{
    /// <summary>The width and height of the scanned page's picture, in pixels.</summary>
    private const int Side = 16;

    /// <summary>The share of each side a crop cuts off.</summary>
    private const float Share = 0.25F;

    /// <summary>The share of a side that, cut off two opposite sides, leaves nothing.</summary>
    private const float Half = 0.5F;

    /// <summary>The place of the width in a PNG file, after its signature and the chunk's length and name.</summary>
    private const int PngWidthOffset = 16;

    /// <summary>The place of the height in a PNG file.</summary>
    private const int PngHeightOffset = 20;

    /// <summary>The bytes of a PNG signature.</summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    /// <summary>A PNG picture of one red pixel.</summary>
    private static readonly byte[] OnePixelPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    /// <summary>A picture that is not JPEG is saved as a PNG of its pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task APictureIsSavedAsAPng()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateScan(new byte[Side * Side], Side, Side));
        var editor = DocumentFeature<IPageContentEditor>.Get(test.Document)!;
        var index = await ImageIndexAsync(editor);

        var file = await editor.ExtractPictureAsync(0, index, CancellationToken.None);

        await Assert.That(file.Extension).IsEqualTo("png");
        await Assert.That(CryptographicOperations.FixedTimeEquals(file.Data.AsSpan(0, PngSignature.Length), PngSignature)).IsTrue();
    }

    /// <summary>A crop hides the part cut off: the saved page clips the picture to a smaller area.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CropMakesThePicturesBoxSmaller()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateScan(new byte[Side * Side], Side, Side));
        var editor = DocumentFeature<IPageContentEditor>.Get(test.Document)!;
        var index = await ImageIndexAsync(editor);
        var whole = ClipWidth(test);

        await editor.CropPictureAsync(0, index, new(Share, Share, Share, Share), CancellationToken.None);

        await Assert.That(ClipWidth(test)).IsLessThan(whole);
    }

    /// <summary>A crop that leaves nothing of the picture is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CropThatLeavesNothingIsRefused()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateScan(new byte[Side * Side], Side, Side));
        var editor = DocumentFeature<IPageContentEditor>.Get(test.Document)!;
        var index = await ImageIndexAsync(editor);

        await Assert.That(async () => await editor.CropPictureAsync(0, index, new(Half, 0, Half, 0), CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Another picture drawn in place of the first is what is saved afterwards.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReplacingAPictureDrawsTheNewOneInItsPlace()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateScan(new byte[Side * Side], Side, Side));
        var editor = DocumentFeature<IPageContentEditor>.Get(test.Document)!;
        var index = await ImageIndexAsync(editor);
        var before = (await editor.ReadObjectsAsync(0, CancellationToken.None))[index].Bounds;

        await editor.ReplacePictureAsync(0, index, OnePixelPng, CancellationToken.None);
        var now = await ImageIndexAsync(editor);
        var file = await editor.ExtractPictureAsync(0, now, CancellationToken.None);
        var bounds = (await editor.ReadObjectsAsync(0, CancellationToken.None))[now].Bounds;

        await Assert.That(ReadInt(file.Data, PngWidthOffset)).IsEqualTo(1);
        await Assert.That(ReadInt(file.Data, PngHeightOffset)).IsEqualTo(1);
        await Assert.That(bounds.Width).IsEqualTo(before.Width);
    }

    /// <summary>A file that is not a picture is refused with a plain error, and the page is left as it was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReplacingWithSomethingElseIsRefused()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateScan(new byte[Side * Side], Side, Side));
        var editor = DocumentFeature<IPageContentEditor>.Get(test.Document)!;
        var index = await ImageIndexAsync(editor);

        await Assert.That(async () => await editor.ReplacePictureAsync(0, index, "not a picture"u8.ToArray(), CancellationToken.None)).Throws<InvalidDataException>();
    }

    /// <summary>Reads how wide the area is that the first picture of the saved page is clipped to.</summary>
    /// <param name="test">The document.</param>
    /// <returns>The width in points.</returns>
    /// <exception cref="InvalidOperationException">The saved page has no picture.</exception>
    private static float ClipWidth(HyperPdfTestDocument test)
    {
        using var saved = PdfDocumentReader.Open(HyperPdfTestDocument.Save(test.Editor), null);
        foreach (var item in PdfDocumentPageContent.GetPageContent(saved, 0).Objects)
        {
            if (item is PdfImageObject image)
            {
                return image.ClipBounds.Right - image.ClipBounds.Left;
            }
        }

        throw new InvalidOperationException("The saved page has no picture.");
    }

    /// <summary>Finds the picture on the first page.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The picture's number.</returns>
    /// <exception cref="InvalidOperationException">The page has no picture.</exception>
    private static async Task<int> ImageIndexAsync(IPageContentEditor editor)
    {
        foreach (var item in await editor.ReadObjectsAsync(0, CancellationToken.None))
        {
            if (item.Kind == PageObjectKind.Image)
            {
                return item.Index;
            }
        }

        throw new InvalidOperationException("The test page has no picture.");
    }

    /// <summary>Reads a big endian whole number of four bytes.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="offset">Where the number starts.</param>
    /// <returns>The number.</returns>
    private static int ReadInt(byte[] data, int offset) => System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, sizeof(int)));
}
