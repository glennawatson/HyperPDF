// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Layers;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the check a page recording makes for each piece of marked content: whether a group or a membership
/// dictionary is shown. The warm check must not allocate.
/// </summary>
public class HyperPdfLayerVisibilityBenchmarks
{
    /// <summary>The first group's object number.</summary>
    private const int GroupNumber = 4;

    /// <summary>The membership dictionary's object number.</summary>
    private const int MembershipNumber = 6;

    /// <summary>The threads that check at once.</summary>
    private const int Threads = 8;

    /// <summary>The checks each thread makes.</summary>
    private const int ChecksPerThread = 20_000;

    /// <summary>The open document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The document's layers.</summary>
    private PdfOptionalContent _content = null!;

    /// <summary>A reference to a plain group.</summary>
    private PdfValue _group;

    /// <summary>A reference to a membership dictionary over both groups.</summary>
    private PdfValue _membership;

    /// <summary>Opens a document with two groups and a membership dictionary.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R /OCProperties << /OCGs [4 0 R 5 0 R] /D << /OFF [5 0 R] >> >> >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>",
                "<< /Type /OCG /Name (A) >>",
                "<< /Type /OCG /Name (B) >>",
                "<< /Type /OCMD /OCGs [4 0 R 5 0 R] /P /AnyOn >>"),
            null);
        _content = PdfDocumentLayers.GetOptionalContent(_document);
        _group = PdfValue.FromReference(new(GroupNumber, 0));
        _membership = PdfValue.FromReference(new(MembershipNumber, 0));
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Checks one group.</summary>
    /// <returns>Whether it is shown.</returns>
    [Benchmark]
    public bool IsGroupVisible() => _content.IsVisible(_group);

    /// <summary>Checks a membership dictionary over two groups.</summary>
    /// <returns>Whether it is shown.</returns>
    [Benchmark]
    public bool IsMembershipVisible() => _content.IsVisible(_membership);

    /// <summary>Checks a membership dictionary from several threads at once, as the render threads of a layered page do.</summary>
    /// <returns>The number of checks that found it shown.</returns>
    [Benchmark]
    public int IsMembershipVisibleOnEightThreads()
    {
        var shown = 0;
        _ = Parallel.For(0, Threads, new ParallelOptions { MaxDegreeOfParallelism = Threads }, _ =>
        {
            var local = 0;
            for (var i = 0; i < ChecksPerThread; i++)
            {
                local += _content.IsVisible(_membership) ? 1 : 0;
            }

            _ = Interlocked.Add(ref shown, local);
        });
        return shown;
    }
}
