// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Tests that page content normalisation joins content streams and moves large inline images out, and that pages draw the same.</summary>
public sealed class ContentNormalisationTests
{
    /// <summary>The bytes in a rendered pixel: red, green, blue and alpha.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The differing pixels a failure lists.</summary>
    private const int SampleCount = 6;

    /// <summary>The number of content streams the sample page starts with.</summary>
    private const int SourceStreams = 3;

    /// <summary>The number of inline images in the sample page.</summary>
    private const int InlineImages = 3;

    /// <summary>A pixel in the middle of the first inline image of the sample page, which draws a picture there.</summary>
    private const int ImageCentre = 100;

    /// <summary>The bits per component of the sample images.</summary>
    private const int BitsPerComponent = 8;

    /// <summary>The position of the image that names a page colour space, among the XObjects.</summary>
    private const int NamedColorSpaceImage = 2;

    /// <summary>A threshold above the size of every inline image in the sample page.</summary>
    private const int HugeThreshold = 100_000;

    /// <summary>Joining the content streams leaves one stream with every part, and the page draws the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CoalescingJoinsContentStreamsAndKeepsTheRendering()
    {
        var source = WriterControlSamples.ContentPage();

        var saved = Save(source, PdfContentNormalisation.CoalesceContents);

        using var document = PdfDocumentReader.Open(saved, null);
        var contents = PageDictionary(document).Get(KnownName.Contents);
        await Assert.That(contents.AsStream()).IsNotNull();
        var text = Encoding.Latin1.GetString(contents.AsStream()!.DecodeToArray());
        await Assert.That(text.Contains("10 10 50 50 re f", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("150 10 40 40 re f", StringComparison.Ordinal)).IsTrue();
        await AssertSameRendering(source, saved);
    }

    /// <summary>Large inline images become image XObjects with full keys, filters and colour spaces, and the page draws the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExternalisingMovesInlineImagesToXObjectsAndKeepsTheRendering()
    {
        var source = WriterControlSamples.ContentPage();

        var saved = Save(source, PdfContentNormalisation.All);

        using var document = PdfDocumentReader.Open(saved, null);
        var page = PageDictionary(document);
        var content = Encoding.Latin1.GetString(page.GetStream(KnownName.Contents)!.DecodeToArray());
        var images = page.GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.XObject)!;
        await Assert.That(content.Contains("BI", StringComparison.Ordinal)).IsFalse();
        await Assert.That(content.Contains("HyperInlineImage1 Do", StringComparison.Ordinal)).IsTrue();
        await Assert.That(images.Count).IsEqualTo(InlineImages);
        await AssertImage(images, 0, "DeviceGray", "FlateDecode");
        await AssertImage(images, 1, "DeviceRGB", "FlateDecode");
        await Assert.That(images.GetStream(images.GetKeyAt(NamedColorSpaceImage))!.Dictionary.GetArray(KnownName.ColorSpace)!.GetName(0).ToKnownName()).IsNotEqualTo(KnownName.DeviceRGB);
        await AssertSameRendering(source, saved);
    }

    /// <summary>An image no larger than the threshold stays inline.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmallInlineImagesStayInline()
    {
        var source = WriterControlSamples.ContentPage();
        using var store = StoreOpening.Open(source, null);
        var options = PdfCompactOptions.Classic with { Normalisation = PdfContentNormalisation.ExternaliseInlineImages, InlineImageThreshold = HugeThreshold };

        var saved = PdfCompactWriter.Save(store, options);

        using var document = PdfDocumentReader.Open(saved, null);
        var contents = PageDictionary(document).Get(KnownName.Contents);
        await Assert.That(contents.AsArray()?.Count).IsEqualTo(SourceStreams);
        await AssertSameRendering(source, saved);
    }

    /// <summary>The document call changes the pages once; a second call finds nothing to do, and the change can be undone.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentCallChangesPagesOnceAndCanBeUndone(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(WriterControlSamples.ContentPage(), null);

        var first = PdfDocumentContentNormalisation.Normalise(document, PdfContentNormalisation.All, 0, cancellationToken);
        var second = PdfDocumentContentNormalisation.Normalise(document, PdfContentNormalisation.All, 0, cancellationToken);
        var changed = PageDictionary(document).Get(KnownName.Contents).AsStream();
        var history = StoreTransactions.GetHistory(document.Objects);
        _ = history.Undo();
        var undone = PageDictionary(document).Get(KnownName.Contents).AsArray();

        await Assert.That(first).IsEqualTo(1);
        await Assert.That(second).IsEqualTo(0);
        await Assert.That(changed).IsNotNull();
        await Assert.That(undone?.Count).IsEqualTo(SourceStreams);
    }

    /// <summary>A cancelled token stops the work before any page changes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledNormalisationChangesNothing()
    {
        using var document = PdfDocumentReader.Open(WriterControlSamples.ContentPage(), null);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Assert.That(() => PdfDocumentContentNormalisation.Normalise(document, PdfContentNormalisation.All, 0, source.Token)).Throws<OperationCanceledException>();

        await Assert.That(PageDictionary(document).Get(KnownName.Contents).AsArray()?.Count).IsEqualTo(SourceStreams);
    }

    /// <summary>Normalised output is accepted by <c>qpdf --check</c> when qpdf is installed.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task NormalisedOutputPassesQpdfCheck(CancellationToken cancellationToken)
    {
        var saved = Save(WriterControlSamples.ContentPage(), PdfContentNormalisation.All);

        var outcome = await QpdfCheck.RunAsync("normalised", saved, cancellationToken);

        if (outcome is not { } result)
        {
            Skip.Test("qpdf is not installed.");
            return;
        }

        await Assert.That(result.IsClean).IsTrue();
    }

    /// <summary>Saves a file with page content normalised.</summary>
    /// <param name="file">The file.</param>
    /// <param name="changes">The changes to make.</param>
    /// <returns>The saved file.</returns>
    private static byte[] Save(byte[] file, PdfContentNormalisation changes)
    {
        using var store = StoreOpening.Open(file, null);
        return PdfCompactWriter.Save(store, PdfCompactOptions.Default with { Normalisation = changes });
    }

    /// <summary>Gets the first page's dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary PageDictionary(PdfDocument document) => PdfDocumentPages.GetPage(document, 0).Dictionary;

    /// <summary>Checks an image XObject made from an inline image.</summary>
    /// <param name="images">The page's XObject dictionary.</param>
    /// <param name="index">The image's position in it.</param>
    /// <param name="colorSpace">The colour space name expected, without the slash.</param>
    /// <param name="filter">The filter name expected; empty for none.</param>
    /// <returns>A task.</returns>
    private static async Task AssertImage(PdfDictionary images, int index, string colorSpace, string filter)
    {
        var image = images.GetStream(images.GetKeyAt(index))!.Dictionary;
        await Assert.That(image.IsName(KnownName.Subtype, KnownName.Image)).IsTrue();
        await Assert.That(image.GetInt32(KnownName.Width)).IsEqualTo(WriterControlSamples.ImageSide);
        await Assert.That(image.GetInt32(KnownName.Height)).IsEqualTo(WriterControlSamples.ImageSide);
        await Assert.That(image.GetInt32(KnownName.BitsPerComponent)).IsEqualTo(BitsPerComponent);
        await Assert.That(image.GetName(KnownName.ColorSpace).ToKnownName().ToString()).IsEqualTo(colorSpace);
        await Assert.That(image.GetName(KnownName.Filter).ToKnownName().ToString()).IsEqualTo(filter.Length == 0 ? "None" : filter);
    }

    /// <summary>Checks that two files render the first page to the same pixels.</summary>
    /// <param name="before">The first file.</param>
    /// <param name="after">The second file.</param>
    /// <returns>A task.</returns>
    private static async Task AssertSameRendering(byte[] before, byte[] after)
    {
        using var original = new RenderTestPage(before);
        using var changed = new RenderTestPage(after);

        var expected = original.RenderPage();
        var actual = changed.RenderPage();

        await Assert.That(expected.IsNear(ImageCentre, ImageCentre, Rgb.White, 0)).IsFalse();
        await Assert.That(actual.Width).IsEqualTo(expected.Width);
        var difference = Compare(expected.Pixels, actual.Pixels, expected.Width);
        if (difference.Pixels == 0)
        {
            return;
        }

        // Drawing the same file again says whether the renderer repeats itself, which tells a changed file from an unsteady renderer.
        var again = Compare(expected.Pixels, original.RenderPage().Pixels, expected.Width);
        var againChanged = Compare(actual.Pixels, changed.RenderPage().Pixels, expected.Width);
        var detail = $"{difference.Details} Drawing the first file again differs in {again.Pixels} pixels, the second in {againChanged.Pixels}.";
        await Assert.That(difference.Pixels).IsEqualTo(0).Because($"{detail} {Describe(before, after)}");
    }

    /// <summary>Temporary: says whether the first image's decoded bytes survived externalising.</summary>
    /// <param name="before">The first file.</param>
    /// <param name="after">The second file.</param>
    /// <returns>The description.</returns>
    private static string Describe(byte[] before, byte[] after)
    {
        const string Marker = "/CS /G ID ";
        const int Peek = 16;
        using var source = PdfDocumentReader.Open(before, null);
        using var saved = PdfDocumentReader.Open(after, null);
        var contents = PageDictionary(source).Get(KnownName.Contents).AsArray()!;
        var content = new StringBuilder();
        for (var i = 0; i < contents.Count; i++)
        {
            _ = content.Append(Encoding.Latin1.GetString(contents.Get(i).AsStream()!.DecodeToArray()));
        }

        var text = content.ToString();
        var start = text.IndexOf(Marker, StringComparison.Ordinal) + Marker.Length;
        var inline = Encoding.Latin1.GetBytes(text[start..(start + (WriterControlSamples.ImageSide * WriterControlSamples.ImageSide))]);
        var images = PageDictionary(saved).GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.XObject)!;
        var decoded = images.GetStream(images.GetKeyAt(0))!.DecodeToArray();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"arch={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} same={inline.AsSpan().SequenceEqual(decoded)} "
            + $"inlineLen={inline.Length} decodedLen={decoded.Length} "
            + $"inline={Convert.ToHexString(inline.AsSpan(0, Peek))} decoded={Convert.ToHexString(decoded.AsSpan(0, Peek))}");
    }

    /// <summary>Measures how two renderings differ, so a failure says where and by how much.</summary>
    /// <param name="expected">The first rendering's bytes.</param>
    /// <param name="actual">The second rendering's bytes.</param>
    /// <param name="width">The width of the renderings in pixels.</param>
    /// <returns>The differences; a length that differs counts every byte of the longer rendering.</returns>
    private static PixelDifference Compare(byte[] expected, byte[] actual, int width)
    {
        var shared = Math.Min(expected.Length, actual.Length) / BytesPerPixel;
        var count = Math.Abs(expected.Length - actual.Length) / BytesPerPixel;
        var largest = 0;
        var box = new PixelBox(int.MaxValue, int.MaxValue, -1, -1);
        var samples = new StringBuilder();
        var sampled = 0;
        for (var pixel = 0; pixel < shared; pixel++)
        {
            var gap = LargestGap(expected, actual, pixel * BytesPerPixel);
            if (gap == 0)
            {
                continue;
            }

            var x = pixel % width;
            var y = pixel / width;
            count++;
            largest = Math.Max(largest, gap);
            box = new(Math.Min(box.Left, x), Math.Min(box.Top, y), Math.Max(box.Right, x), Math.Max(box.Bottom, y));
            if (sampled >= SampleCount)
            {
                continue;
            }

            var at = pixel * BytesPerPixel;
            _ = samples.Append(CultureInfo.InvariantCulture, $" ({x},{y}) {Convert.ToHexString(expected.AsSpan(at, BytesPerPixel))}/{Convert.ToHexString(actual.AsSpan(at, BytesPerPixel))}");
            sampled++;
        }

        return new(count, $"{count} pixels differ, by up to {largest}, within x {box.Left}-{box.Right} and y {box.Top}-{box.Bottom}; first pixels (expected/actual):{samples}");
    }

    /// <summary>Finds the largest difference between the bytes of one pixel in two renderings.</summary>
    /// <param name="expected">The first rendering's bytes.</param>
    /// <param name="actual">The second rendering's bytes.</param>
    /// <param name="at">The position of the pixel's first byte.</param>
    /// <returns>The largest difference, or 0 when the pixels are the same.</returns>
    private static int LargestGap(byte[] expected, byte[] actual, int at)
    {
        var largest = 0;
        for (var i = 0; i < BytesPerPixel; i++)
        {
            largest = Math.Max(largest, Math.Abs(expected[at + i] - actual[at + i]));
        }

        return largest;
    }

    /// <summary>How two renderings differ.</summary>
    /// <param name="Pixels">The number of pixels that differ.</param>
    /// <param name="Details">Where and by how much, in words.</param>
    private readonly record struct PixelDifference(int Pixels, string Details);

    /// <summary>The smallest box around some pixels.</summary>
    /// <param name="Left">The left column.</param>
    /// <param name="Top">The top row.</param>
    /// <param name="Right">The right column.</param>
    /// <param name="Bottom">The bottom row.</param>
    private readonly record struct PixelBox(int Left, int Top, int Right, int Bottom);
}
