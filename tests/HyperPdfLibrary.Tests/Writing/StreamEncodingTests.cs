// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Accessibility;
using HyperPdfLibrary.Tests.Conformance;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Tests that <see cref="PdfCompactOptions.RecompressEncoding"/> picks the filters <see cref="PdfStreamMode.Recompress"/> writes, and that a PDF/A or PDF/UA claim never gets LZW.</summary>
public sealed class StreamEncodingTests
{
    /// <summary>The name of the LZW filter.</summary>
    private const string LzwFilter = "LZWDecode";

    /// <summary>The lines in the sample page content.</summary>
    private const int ContentLines = 20;

    /// <summary>The spaces after each content line, which make runs for the run-length filter.</summary>
    private const int Padding = 200;

    /// <summary>The PDF/UA part claimed by the UA sample.</summary>
    private const int PdfUaPartOne = 1;

    /// <summary>The page object of the samples.</summary>
    private const string PageObject = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R >>";

    /// <summary>Recompress writes the filters the options name, and the content decodes to what it was.</summary>
    /// <param name="encoding">The encoding asked for.</param>
    /// <param name="chain">The filters expected, joined by commas.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfStreamEncoding.Flate, "FlateDecode")]
    [Arguments(PdfStreamEncoding.Lzw, LzwFilter)]
    [Arguments(PdfStreamEncoding.RunLength, "RunLengthDecode")]
    [Arguments(PdfStreamEncoding.FlateAscii85, "ASCII85Decode,FlateDecode")]
    [Arguments(PdfStreamEncoding.FlateAsciiHex, "ASCIIHexDecode,FlateDecode")]
    [Arguments(PdfStreamEncoding.LzwAscii85, "ASCII85Decode,LZWDecode")]
    public async Task Recompress_WritesTheChosenFilters(PdfStreamEncoding encoding, string chain)
    {
        var (source, text) = RunLengthSample();

        var saved = Save(source, encoding);

        using var store = StoreOpening.Open(saved, null);
        var content = ContentStream(store);
        await Assert.That(WriterStreams.FilterOf(store, content)).IsEqualTo(chain);
        await Assert.That(Encoding.Latin1.GetString(content.DecodeToArray())).IsEqualTo(text);
    }

    /// <summary>A file that claims PDF/A is written with Flate in place of LZW, and still claims PDF/A.</summary>
    /// <param name="encoding">An encoding that uses LZW.</param>
    /// <param name="chain">The filters the claim leaves.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfStreamEncoding.Lzw, "FlateDecode")]
    [Arguments(PdfStreamEncoding.LzwAscii85, "ASCII85Decode,FlateDecode")]
    public async Task Recompress_PdfAClaim_NeverWritesLzw(PdfStreamEncoding encoding, string chain)
    {
        var saved = Save(PdfAFixtures.Create(PdfAFixtureLevel.A2B), encoding);

        using var reopened = PdfDocumentReader.Open(saved, null);
        await Assert.That(PdfDocumentConformance.GetConformance(reopened).PdfA?.Label).IsEqualTo("2B");
        await Assert.That(Chains(reopened.Objects)).DoesNotContain(LzwFilter);
        await Assert.That(Chains(reopened.Objects).Contains(chain, StringComparer.Ordinal)).IsTrue();
    }

    /// <summary>A file that claims PDF/UA is written with Flate in place of LZW.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Recompress_PdfUaClaim_NeverWritesLzw()
    {
        var packet = AccessibilityPdfs.Packet(PdfUaPartOne, true);
        var source = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Metadata 5 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            PageObject,
            MiniPdf.Stream(string.Empty, ContentText()),
            MiniPdf.Stream("/Type /Metadata /Subtype /XML", packet));

        var saved = Save(source, PdfStreamEncoding.Lzw);

        using var store = StoreOpening.Open(saved, null);
        await Assert.That(Chains(store)).DoesNotContain(LzwFilter);
        await Assert.That(WriterStreams.FilterOf(store, ContentStream(store))).IsEqualTo("FlateDecode");
    }

    /// <summary>Without a claim, LZW is written as asked, so the claim is the only thing that changes it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Recompress_NoClaim_WritesLzw()
    {
        var saved = Save(RunLengthSample().Source, PdfStreamEncoding.Lzw);

        using var store = StoreOpening.Open(saved, null);
        await Assert.That(Chains(store)).Contains(LzwFilter);
    }

    /// <summary>Makes a page whose content has long runs of spaces and repeated lines, so every filter can shrink it.</summary>
    /// <returns>The file and the content text.</returns>
    private static SampleFile RunLengthSample()
    {
        var text = ContentText();
        var source = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            PageObject,
            MiniPdf.Stream(string.Empty, text));
        return new(source, text);
    }

    /// <summary>Makes the sample page content.</summary>
    /// <returns>The content text.</returns>
    private static string ContentText() => string.Concat(Enumerable.Repeat($"0 0 1 rg 10 10 50 50 re f{new string(' ', Padding)}\n", ContentLines));

    /// <summary>Saves a file with the Recompress mode and an encoding.</summary>
    /// <param name="file">The file.</param>
    /// <param name="encoding">The encoding.</param>
    /// <returns>The saved file.</returns>
    private static byte[] Save(byte[] file, PdfStreamEncoding encoding)
    {
        using var store = StoreOpening.Open(file, null);
        return PdfCompactWriter.Save(store, PdfCompactOptions.Classic with { StreamMode = PdfStreamMode.Recompress, RecompressEncoding = encoding });
    }

    /// <summary>Finds the page content stream: the first stream with no /Subtype or /Type.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The stream.</returns>
    private static PdfStream ContentStream(PdfObjectStore store) =>
        WriterStreams.Find(store, static stream => stream.Dictionary.Get(KnownName.Subtype).IsNull && stream.Dictionary.Get(KnownName.Type).IsNull)!;

    /// <summary>Lists the filter chain of every stream.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The chains, each a comma-joined list of filter names.</returns>
    private static List<string> Chains(PdfObjectStore store) => [.. WriterStreams.All(store).Select(stream => WriterStreams.FilterOf(store, stream))];

    /// <summary>A file and the text of its page content.</summary>
    /// <param name="Source">The file.</param>
    /// <param name="Text">The content text.</param>
    private readonly record struct SampleFile(byte[] Source, string Text);
}
