// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Tests.Robustness;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the first page of a large local linearized PDF through bounded and whole-file reads.</summary>
public class HyperPdfLinearizedFirstPageBenchmarks
{
    /// <summary>The comment bytes after the first-page section, keeping the file above the stream-cache budget.</summary>
    private const int PaddingBytes = 8_388_608;

    /// <summary>The owned fixture folder.</summary>
    private string _folder = string.Empty;

    /// <summary>The linearized PDF path.</summary>
    private string _path = string.Empty;

    /// <summary>Writes a large two-page file with the first page and hint stream near the start.</summary>
    /// <returns>A task for the file write.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"pdf-linearized-benchmark-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        _path = Path.Combine(_folder, "linearized.pdf");
        await File.WriteAllBytesAsync(_path, LinearizedDocuments.Create(false, PaddingBytes));
    }

    /// <summary>Removes the generated file.</summary>
    [GlobalCleanup]
    public void Cleanup() => Directory.Delete(_folder, true);

    /// <summary>Opens the file with the viewer's bounded async page cache and reads page one.</summary>
    /// <returns>The first-page object number.</returns>
    [Benchmark(Baseline = true)]
    public ValueTask<int> BoundedStreamFirstPage() => OpenFirstPageAsync(PdfSourceKind.Stream, false);

    /// <summary>Decodes the page hints before reading the first page from the bounded cache.</summary>
    /// <returns>The first-page object number.</returns>
    [Benchmark]
    public ValueTask<int> HintedStreamFirstPage() => OpenFirstPageAsync(PdfSourceKind.Stream, true);

    /// <summary>Reads the entire file before parsing its first page.</summary>
    /// <returns>The first-page object number.</returns>
    [Benchmark]
    public ValueTask<int> WholeFileFirstPage() => OpenFirstPageAsync(PdfSourceKind.Memory, false);

    /// <summary>Opens the fixture with one source and gets its first page.</summary>
    /// <param name="source">The file source kind.</param>
    /// <param name="readHints">Whether to decode the hint table before the page.</param>
    /// <returns>The first-page object number.</returns>
    private async ValueTask<int> OpenFirstPageAsync(PdfSourceKind source, bool readHints)
    {
        using var document = await PdfDocumentReader.OpenWithAsync(
            _path,
            new PdfOpenOptions { Source = source },
            CancellationToken.None,
            CancellationToken.None).ConfigureAwait(false);
        if (readHints)
        {
            _ = await PdfLinearizationHints.GetPageRangesAsync(document, CancellationToken.None).ConfigureAwait(false);
        }

        var page = await PdfDocumentPages.GetPageAsync(document, 0, CancellationToken.None).ConfigureAwait(false);
        return page.Id.Number;
    }
}
