// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Tests.Writing;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Stream recompression, duplicate merging, unused objects and the file layout.</summary>
[NotInParallel]
public sealed class StructureOptimizationTests
{
    /// <summary>The pages of the multi-page sample.</summary>
    private const int PageCount = 5;

    /// <summary>The bytes of the long content stream.</summary>
    private const int LongContent = 4000;

    /// <summary>An uncompressed content stream is compressed, and its decoded bytes do not change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompressesUncompressedStreams()
    {
        var content = new StringBuilder();
        while (content.Length < LongContent)
        {
            _ = content.Append("0 0 1 rg 72 72 100 100 re f\n");
        }

        var source = MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
        MiniPdf.Stream(
        string.Empty,
        content.ToString()));
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality);
        using var before = StoreOpening.Open(source, null);
        using var after = StoreOpening.Open(result.Bytes, null);
        await Assert.That(result.Bytes.Length).IsLessThan(source.Length);
        await Assert.That(result.Report.GetSaving(PdfOptimizeCategory.Streams).Count).IsGreaterThan(0);
        await Assert.That(WritingTestDocuments.PageContents(after)[0]).IsEquivalentTo(WritingTestDocuments.PageContents(before)[0]);
    }

    /// <summary>A Flate stream with a PNG predictor is recompressed with its predictor kept, so it decodes to the same bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsPredictorsWhenRecompressing()
    {
        const int Width = 64;
        const int Height = 32;
        var samples = OptimizerSamples.Ramp(Width, Height);
        var predicted = new byte[(Width + 1) * Height];
        for (var y = 0; y < Height; y++)
        {
            // PNG "None" rows: a zero type byte, then the row.
            samples.AsSpan(y * Width, Width).CopyTo(predicted.AsSpan((y * (Width + 1)) + 1));
        }

        var entries =
            OptimizerSamples.Format(
            $"/Type /XObject /Subtype /Image /Width {Width} /Height {Height} {OptimizerSamples.Grey} /Filter /FlateDecode /DecodeParms << /Predictor 15 /Columns {Width} >>");
        var image = MiniPdf.Stream(entries, OptimizerTestKit.Latin1(OptimizerTestKit.Deflate(predicted)));
        var source = OptimizerSamples.ImagePage(image, Width);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality);
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        var stream = PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.XObject)!.GetStream(document.Objects.Names.Intern("Im1"u8))!;
        await Assert.That(stream.DecodeToArray()).IsEquivalentTo(samples);
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
    }

    /// <summary>Two identical images become one object, and both pages still draw it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MergesDuplicateImages()
    {
        var source = OptimizerSamples.Duplicates();
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality);
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        var name = document.Objects.Names.Intern("Im1"u8);
        var first = PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.XObject)!.GetRaw(name).AsReference();
        var second = PdfDocumentPages.GetPage(document, 1).Resources!.GetDictionary(KnownName.XObject)!.GetRaw(name).AsReference();
        await Assert.That(first).IsEqualTo(second);
        await Assert.That(result.Report.GetSaving(PdfOptimizeCategory.Duplicates).Count).IsGreaterThan(0);
    }

    /// <summary>Duplicate merging can be turned off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsDuplicatesWhenAsked()
    {
        var result = OptimizerTestKit.Optimize(OptimizerSamples.Duplicates(), PdfOptimizeOptions.KeepQuality with { RemoveDuplicates = false });
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        var name = document.Objects.Names.Intern("Im1"u8);
        await Assert.That(PdfDocumentPages.GetPage(
        document,
        0).Resources!.GetDictionary(KnownName.XObject)!.GetRaw(name)).IsNotEqualTo(PdfDocumentPages.GetPage(
        document,
        1).Resources!.GetDictionary(KnownName.XObject)!.GetRaw(name));
    }

    /// <summary>Objects nothing reaches are dropped and counted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DropsUnreachableObjects()
    {
        var source = MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
        "(an orphan nobody uses)");
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality);
        await Assert.That(Encoding.Latin1.GetString(result.Bytes)).DoesNotContain("orphan");
        await Assert.That(result.Report.GetSaving(PdfOptimizeCategory.UnusedObjects).Count).IsEqualTo(1);
    }

    /// <summary>Object streams and a cross-reference stream are written by default and every page's content survives.</summary>
    /// <param name="objectStreams">Whether to write object streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task KeepsEveryPage(bool objectStreams)
    {
        var source = TestPdf.Create(PageCount);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Balanced with { UseObjectStreams = objectStreams });
        using var before = StoreOpening.Open(source, null);
        using var after = StoreOpening.Open(result.Bytes, null);
        var expected = WritingTestDocuments.PageContents(before);
        var actual = WritingTestDocuments.PageContents(after);
        await Assert.That(actual.Count).IsEqualTo(PageCount);
        for (var i = 0; i < PageCount; i++)
        {
            await Assert.That(actual[i]).IsEquivalentTo(expected[i]);
        }

        await Assert.That(WritingTestDocuments.CountMissing(after)).IsEqualTo(0);
        await Assert.That(Encoding.Latin1.GetString(result.Bytes).Contains("/ObjStm", StringComparison.Ordinal)).IsEqualTo(objectStreams);
        await Assert.That(OptimizerTestKit.Text(result.Bytes)).IsEquivalentTo(OptimizerTestKit.Text(source));
    }

    /// <summary>The version is raised for object streams but never lowered.</summary>
    /// <param name="version">The source version.</param>
    /// <param name="expected">The written version.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("1.3", "1.5")]
    [Arguments("1.7", "1.7")]
    [Arguments("2.0", "2.0")]
    public async Task NeverLowersTheVersion(string version, string expected)
    {
        var source = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(TestPdf.Create(1)).Replace("%PDF-1.7", $"%PDF-{version}", StringComparison.Ordinal));
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Balanced);
        await Assert.That(Encoding.ASCII.GetString(result.Bytes, 0, expected.Length + "%PDF-".Length)).IsEqualTo($"%PDF-{expected}");
    }
}
