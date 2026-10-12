// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>Measures checking a document for content that cannot be shown, done once per page in the background on opening.</summary>
public class ContentCheckBenchmarks
{
    /// <summary>The document using every unsupported feature.</summary>
    private BenchmarkPdfDocument _document = null!;

    /// <summary>The document's content check.</summary>
    private IContentCheck _check = null!;

    /// <summary>Opens the document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = new(TestPdf.CreateWithUnsupportedContent());
        _check = DocumentFeature<IContentCheck>.Cast(_document.Document)!;
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Checks the form type and document scripts.</summary>
    /// <returns>What was found.</returns>
    [Benchmark]
    public UnsupportedContent CheckDocument() => _check.CheckDocument();

    /// <summary>Checks a page holding a media player, a 3D model and a scripted field.</summary>
    /// <returns>What was found.</returns>
    [Benchmark]
    public UnsupportedContent CheckPage() => _check.CheckPage(0);

    /// <summary>Puts several unsupported parts into words.</summary>
    /// <returns>The message.</returns>
    [Benchmark]
    public string? Describe() => ContentWarnings.Describe(UnsupportedContent.XfaForm | UnsupportedContent.Multimedia | UnsupportedContent.ThreeD);
}
