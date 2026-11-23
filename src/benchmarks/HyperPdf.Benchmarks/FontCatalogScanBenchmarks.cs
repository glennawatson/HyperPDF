// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Text.Fonts;

namespace HyperPdf.Benchmarks;

/// <summary>Measures reading the installed fonts, which the viewer does once when it starts.</summary>
public class FontCatalogScanBenchmarks
{
    /// <summary>The folders the system keeps fonts in.</summary>
    private List<string> _directories = [];

    /// <summary>Finds the system's font folders.</summary>
    [GlobalSetup]
    public void Setup() => _directories = FontCatalog.SystemDirectories();

    /// <summary>Reads every installed font file.</summary>
    /// <returns>The families found.</returns>
    [Benchmark]
    public int ScanSystemFonts() => FontCatalog.Scan(_directories).Families.Length;
}
