// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signing;

namespace HyperPdf.Benchmarks;

/// <summary>Measures zero-filled reservations reused within a signing workspace and bounded size estimates.</summary>
public class PdfSignatureContentsBenchmarks
{
    /// <summary>The reservation retained across repeated requests.</summary>
    private const int Capacity = 16 * 1024;

    /// <summary>The original storage owner.</summary>
    private PdfDocument _document = null!;

    /// <summary>The private signing lifetime.</summary>
    private PdfSigningWorkspace _workspace = null!;

    /// <summary>Creates the private copy and its zero-filled reservation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(TestPdf.Create(1), null);
        _workspace = PdfSigningWorkspace.Open(_document);
        _ = _workspace.Reserve(Capacity, CancellationToken.None);
    }

    /// <summary>Ends the private copy before releasing the original storage owner.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _workspace.Dispose();
        _document.Dispose();
    }

    /// <summary>Gets a reservation without creating another buffer.</summary>
    /// <returns>The reserved bytes.</returns>
    [Benchmark]
    public int ReserveAgain() => _workspace.Reserve(Capacity, CancellationToken.None).Length;

    /// <summary>Checks and aligns an unaligned reservation before allocating output.</summary>
    /// <returns>The aligned bytes.</returns>
    [Benchmark]
    public int RoundReservation() => PdfContentsSizing.RoundUp(Capacity + 1);
}
