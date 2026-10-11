// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.HyperPdf;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures a warmed lookup of a built-in annotation font shaper.</summary>
public class HyperPdfStandardFontBenchmarks
{
    /// <summary>The built-in Helvetica face.</summary>
    private const string Family = "Helvetica";

    /// <summary>The engine's annotation font cache.</summary>
    private readonly TextBoxFontState _state = new();

    /// <summary>Creates the shaper before measurement.</summary>
    [GlobalSetup]
    public void Setup() => _ = StandardTextShaper.For(_state, Family, false, false);

    /// <summary>Gets a warmed built-in font shaper.</summary>
    /// <returns>The shaper.</returns>
    [Benchmark]
    public object Helvetica() => StandardTextShaper.For(_state, Family, false, false);
}
