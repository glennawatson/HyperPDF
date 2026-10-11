// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Compares how HyperPDF reads a file: loaded into memory, mapped, or through a file handle with a page cache. Each
/// benchmark opens the file fresh and decodes the content of the first page, and of a page deep in the file. Uses the
/// cached ia-us-reports-341 corpus book (about 900 pages, 36 MB) when present, else a generated 200-page file.
/// </summary>
public class HyperPdfSourceBenchmarks
{
    /// <summary>The pages of the generated file.</summary>
    private const int GeneratedPages = 200;

    /// <summary>The content length of each generated page.</summary>
    private const int GeneratedContent = 64 * 1024;

    /// <summary>The page cache budget of the stream source.</summary>
    private const long CacheBytes = 4L << 20;

    /// <summary>The share of the way through the document the deep page lies, in tenths.</summary>
    private const int DeepTenths = 7;

    /// <summary>Tenths in a whole.</summary>
    private const int Tenths = 10;

    /// <summary>The corpus file used when cached.</summary>
    private const string CorpusFile = "ia-us-reports-341.pdf";

    /// <summary>The optional file path used for a local source-layout comparison.</summary>
    private const string SourceFileVariable = "PDFVIEWERLITE_SOURCE_BENCHMARK_PDF";

    /// <summary>The objects before the first page: the catalog and the page tree.</summary>
    private const int FixedObjects = 2;

    /// <summary>The objects per page: the page and its content.</summary>
    private const int ObjectsPerPage = 2;

    /// <summary>The content repeated to fill each generated page.</summary>
    private const string Filler = "q Q\n";

    /// <summary>The file read.</summary>
    private string _path = string.Empty;

    /// <summary>Whether the file was generated, and so is deleted afterwards.</summary>
    private bool _generated;

    /// <summary>Gets or sets how the file is read.</summary>
    [Params(PdfSourceKind.Automatic, PdfSourceKind.Memory, PdfSourceKind.Mapped, PdfSourceKind.Stream)]
    public PdfSourceKind Source { get; set; }

    /// <summary>Finds a configured file or the corpus book, or writes a large file.</summary>
    /// <returns>The setup task.</returns>
    /// <exception cref="FileNotFoundException">The configured source file is missing.</exception>
    [GlobalSetup]
    public async Task Setup()
    {
        if (Environment.GetEnvironmentVariable(SourceFileVariable) is { Length: > 0 } selected)
        {
            if (!File.Exists(selected))
            {
                throw new FileNotFoundException("The configured source benchmark PDF does not exist.", selected);
            }

            _path = Path.GetFullPath(selected);
            return;
        }

        var corpus = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus", CorpusFile);
        if (File.Exists(corpus))
        {
            _path = corpus;
            return;
        }

        _generated = true;
        _path = Path.Combine(Path.GetTempPath(), $"hyperpdf-source-bench-{Environment.ProcessId}.pdf");
        await File.WriteAllBytesAsync(_path, Generate());
    }

    /// <summary>Deletes a generated file.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        if (_generated)
        {
            File.Delete(_path);
        }
    }

    /// <summary>Opens the file and decodes the first page's content.</summary>
    /// <returns>The decoded length.</returns>
    [Benchmark(Baseline = true)]
    public int OpenFirstPage()
    {
        using var document = Open();
        return DecodePage(document, 0);
    }

    /// <summary>Opens the same file with asynchronous I/O, then decodes its first page.</summary>
    /// <returns>The decoded length.</returns>
    [Benchmark]
    public async ValueTask<int> OpenFirstPageAsync()
    {
        using var document = await PdfDocumentReader.OpenWithAsync(_path, new PdfOpenOptions { Source = Source, CacheBytes = CacheBytes }, CancellationToken.None).ConfigureAwait(false);
        return DecodePage(document, 0);
    }

    /// <summary>Opens the file and decodes the first page's content and a page 70% of the way through.</summary>
    /// <returns>The decoded length.</returns>
    [Benchmark]
    public int OpenFirstAndDeepPage()
    {
        using var document = Open();
        return DecodePage(document, 0) + DecodePage(document, document.PageCount * DeepTenths / Tenths);
    }

    /// <summary>Opens the file, decodes resources and records its first page.</summary>
    /// <returns>Whether the page was recorded.</returns>
    [Benchmark]
    public bool OpenAndRecordFirstPage()
    {
        using var document = Open();
        using var renderer = new PdfPageRenderer(document);
        return renderer.Prepare(new(0, 1, 0, 0, 0, PdfRenderFlags.None), CancellationToken.None);
    }

    /// <summary>Decodes a page's content, whether one stream or an array of them.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The page index.</param>
    /// <returns>The decoded length.</returns>
    private static int DecodePage(PdfDocument document, int index)
    {
        var contents = StoreReading.Resolve(document.Objects, PdfDocumentPages.GetPage(document, index).Dictionary.GetRaw(KnownName.Contents));
        if (contents.AsArray() is not { } parts)
        {
            return Decode(contents.AsStream());
        }

        var total = 0;
        for (var i = 0; i < parts.Count; i++)
        {
            total += Decode(StoreReading.Resolve(document.Objects, parts.Get(i)).AsStream());
        }

        return total;
    }

    /// <summary>Decodes one stream into a pooled buffer.</summary>
    /// <param name="stream">The stream, or <see langword="null"/>.</param>
    /// <returns>The decoded length.</returns>
    private static int Decode(PdfStream? stream)
    {
        if (stream is null)
        {
            return 0;
        }

        var output = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Builds a file of many pages, each with its own large content stream.</summary>
    /// <returns>The file.</returns>
    private static byte[] Generate()
    {
        var content = new StringBuilder().Insert(0, Filler, GeneratedContent / Filler.Length).ToString();
        var objects = new string[FixedObjects + (GeneratedPages * ObjectsPerPage)];
        var kids = new StringBuilder();
        for (var page = 0; page < GeneratedPages; page++)
        {
            var number = FixedObjects + (page * ObjectsPerPage) + 1;
            _ = kids.Append(CultureInfo.InvariantCulture, $"{number} 0 R ");
            objects[number - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents {number + 1} 0 R >>");
            objects[number] = MiniPdf.Stream(string.Empty, content);
        }

        objects[0] = "<< /Type /Catalog /Pages 2 0 R >>";
        objects[1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{kids}] /Count {GeneratedPages} >>");
        return MiniPdf.Build(objects);
    }

    /// <summary>Opens the file with the chosen source.</summary>
    /// <returns>The document.</returns>
    private PdfDocument Open() => PdfDocumentReader.OpenWith(_path, new PdfOpenOptions { Source = Source, CacheBytes = CacheBytes });
}
