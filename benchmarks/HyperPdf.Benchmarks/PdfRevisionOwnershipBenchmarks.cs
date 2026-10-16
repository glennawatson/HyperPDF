// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;

namespace HyperPdf.Benchmarks;

/// <summary>Measures security-store ownership while an unsigned update shadows a signed page.</summary>
public class PdfRevisionOwnershipBenchmarks
{
    /// <summary>The page's object number.</summary>
    private const int PageNumber = 3;

    /// <summary>The security store's object number.</summary>
    private const int SecurityStoreNumber = 4;

    /// <summary>The original revision.</summary>
    private PdfDocument _signed = null!;

    /// <summary>The updated revision.</summary>
    private PdfDocument _current = null!;

    /// <summary>The end of the original revision.</summary>
    private long _signedEnd;

    /// <summary>Gets or sets whether the security store references itself.</summary>
    [Params(false, true)]
    public bool Circular { get; set; }

    /// <summary>Opens the original and updated in-memory files.</summary>
    /// <returns>The asynchronous setup.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        var earlier = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>");
        var later = IncrementalPdf.Append(earlier, new Dictionary<int, string>
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R /DSS 4 0 R >>",
            [PageNumber] = SignedPageRevisions.Annotation,
            [SecurityStoreNumber] = Circular ? "<< /Data [3 0 R 4 0 R] >>" : "<< /Data [3 0 R] >>",
        });
        _signedEnd = earlier.Length;
        _signed = await PdfDocumentReader.OpenAsync(earlier, null, CancellationToken.None);
        _current = await PdfDocumentReader.OpenAsync(later, null, CancellationToken.None);
    }

    /// <summary>Classifies the page independently of its unsigned security-store ownership.</summary>
    /// <returns>The classified changes.</returns>
    [Benchmark]
    public PdfObjectChange[] CompareSecurityStoreRevision() => PdfRevisionComparer.Compare(_current.Objects, _signed.Objects, _signedEnd);

    /// <summary>Disposes both revision stores.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _current.Dispose();
        _signed.Dispose();
    }
}
