// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the compact writer's controls: each stream mode and Flate level, keeping unreferenced objects, content
/// normalisation (joined content streams and inline images moved to XObjects), declaring a developer extension, and the
/// JSON view of the object tree.
/// </summary>
public class HyperPdfWriterControlBenchmarks
{
    /// <summary>The decoded bytes the JSON view writes for each stream.</summary>
    private const int DecodedBytes = 64;

    /// <summary>The extension level declared and undone by the extension benchmark.</summary>
    private const int ExtensionLevel = 3;

    /// <summary>A page with Flate content written at the fastest level, for the stream mode benchmarks.</summary>
    private PdfDocument _repetitive = null!;

    /// <summary>A page with split content and large inline images, for the normalisation benchmarks.</summary>
    private PdfDocument _inlineImages = null!;

    /// <summary>A page with objects no reference reaches.</summary>
    private PdfDocument _orphans = null!;

    /// <summary>Gets the layout the stream mode benchmarks start from: classic, so the cost shown is the streams.</summary>
    private static PdfCompactOptions Classic => PdfCompactOptions.Classic;

    /// <summary>Opens the documents outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _repetitive = PdfDocumentReader.Open(WriterControlSamples.RepetitivePage(), null);
        _inlineImages = PdfDocumentReader.Open(WriterControlSamples.ContentPage(), null);
        _orphans = PdfDocumentReader.Open(WriterControlSamples.WithOrphans(), null);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _repetitive.Dispose();
        _inlineImages.Dispose();
        _orphans.Dispose();
    }

    /// <summary>Saves with the default stream mode, which leaves a filtered stream as it is.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark(Baseline = true)]
    public byte[] StreamsCompress() => PdfCompactWriter.Save(_repetitive.Objects, Classic);

    /// <summary>Saves copying every stream.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark]
    public byte[] StreamsPreserve() => PdfCompactWriter.Save(_repetitive.Objects, Classic with { StreamMode = PdfStreamMode.Preserve });

    /// <summary>Decodes and compresses every stream again at the fastest level.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark]
    public byte[] RecompressFastest() =>
        PdfCompactWriter.Save(_repetitive.Objects, Classic with { StreamMode = PdfStreamMode.Recompress, CompressionLevel = PdfCompressionLevel.Fastest });

    /// <summary>Decodes and compresses every stream again at the balanced level.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark]
    public byte[] RecompressBalanced() => PdfCompactWriter.Save(_repetitive.Objects, Classic with { StreamMode = PdfStreamMode.Recompress });

    /// <summary>Decodes and compresses every stream again at the smallest level.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark]
    public byte[] RecompressSmallest() =>
        PdfCompactWriter.Save(_repetitive.Objects, Classic with { StreamMode = PdfStreamMode.Recompress, CompressionLevel = PdfCompressionLevel.Smallest });

    /// <summary>Decodes every stream and writes it with no filter.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark]
    public byte[] StreamsUncompressed() => PdfCompactWriter.Save(_repetitive.Objects, Classic with { StreamMode = PdfStreamMode.Uncompressed });

    /// <summary>Saves a document with objects no reference reaches, dropping them.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark]
    public byte[] DropUnreferenced() => PdfCompactWriter.Save(_orphans.Objects, Classic);

    /// <summary>Saves a document with objects no reference reaches, keeping them.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark]
    public byte[] KeepUnreferenced() => PdfCompactWriter.Save(_orphans.Objects, Classic with { KeepUnreferenced = true });

    /// <summary>Joins the content streams and moves the inline images out of a page, then undoes the edit so the next run does the same work.</summary>
    /// <returns>Whether the edit was undone.</returns>
    [Benchmark]
    public bool NormaliseContentAndUndo()
    {
        _ = PdfDocumentContentNormalisation.Normalise(_inlineImages, PdfContentNormalisation.All, 0, CancellationToken.None);
        return PdfDocumentEditing.Undo(_inlineImages);
    }

    /// <summary>Declares a developer extension, which raises the version, then undoes the edit.</summary>
    /// <returns>Whether the edit was undone.</returns>
    [Benchmark]
    public bool DeclareExtensionAndUndo()
    {
        _ = PdfDocumentExtensionEditing.Declare(_orphans, new("TEST", "1.7", ExtensionLevel, null));
        return PdfDocumentEditing.Undo(_orphans);
    }

    /// <summary>Writes the JSON view of a document's objects, with a few decoded bytes of each stream.</summary>
    /// <returns>The JSON length.</returns>
    [Benchmark]
    public long JsonView()
    {
        using var output = new MemoryStream();
        PdfObjectTreeJson.Write(_inlineImages, output, new() { MaxDecodedBytes = DecodedBytes, Indented = false }, CancellationToken.None);
        return output.Length;
    }
}
