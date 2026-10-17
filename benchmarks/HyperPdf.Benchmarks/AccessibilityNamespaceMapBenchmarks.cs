// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the bounded namespace role-map walk independently of report construction.</summary>
public class AccessibilityNamespaceMapBenchmarks
{
    /// <summary>The namespace role-map depth limit.</summary>
    private const int ChainLength = 32;

    /// <summary>The document owning the namespace dictionaries.</summary>
    private PdfDocument _document = null!;

    /// <summary>The first namespace in the chain.</summary>
    private PdfDictionary _space = null!;

    /// <summary>Gets or sets whether the chain returns to its first namespace.</summary>
    [Params(false, true)]
    public bool Circular { get; set; }

    /// <summary>Parses and warms the same bounded chain used during measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(NamespaceRoleMapPdfs.Build(ChainLength, Circular), null);
        _space = StoreReading.GetObject(_document.Objects, new(NamespaceRoleMapPdfs.FirstNamespace, 0)).AsDictionary()!;
        _ = Read();
    }

    /// <summary>Follows a warm chain and returns its domain classification.</summary>
    /// <returns>The namespace-map problem code.</returns>
    [Benchmark]
    public int Read() => (int)AccessibilityNamespaceMap.Read(_space, "X", CancellationToken.None);

    /// <summary>Releases the owning document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();
}
