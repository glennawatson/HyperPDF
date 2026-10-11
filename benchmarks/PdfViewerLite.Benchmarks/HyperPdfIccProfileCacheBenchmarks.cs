// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures warm ICC stream-cache lookups for a profile known to be unsupported.</summary>
public class HyperPdfIccProfileCacheBenchmarks
{
    /// <summary>The document that owns the stream.</summary>
    private PdfDocument _document = null!;

    /// <summary>An unsupported profile whose cached result is null.</summary>
    private PdfStream _profile = null!;

    /// <summary>An unsupported profile without a document owner.</summary>
    private PdfStream _unownedProfile = null!;

    /// <summary>Opens a document and warms the profile lookup.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(TestPdf.CreateTagged(), null);
        var dictionary = new PdfDictionary(_document.Objects);
        dictionary.Set(KnownName.N, PdfValue.FromInteger(PdfColorSpace.RgbComponents));
        _profile = new(dictionary, [0, 1, 2]);
        _unownedProfile = new(new(null), [0, 1, 2]);
        _ = IccProfile.Get(_profile, PdfColorSpace.RgbComponents);
        _ = IccProfile.Get(_unownedProfile, PdfColorSpace.RgbComponents);
    }

    /// <summary>Closes the source document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads a cached unsupported profile result.</summary>
    /// <returns>True when the profile remains unsupported.</returns>
    [Benchmark]
    public bool CachedUnsupportedProfile() => IccProfile.Get(_profile, PdfColorSpace.RgbComponents) is null;

    /// <summary>Reads the same unsupported profile without a document owner.</summary>
    /// <returns>True when the profile remains unsupported.</returns>
    [Benchmark]
    public bool UnownedUnsupportedProfile() => IccProfile.Get(_unownedProfile, PdfColorSpace.RgbComponents) is null;
}
