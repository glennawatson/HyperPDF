// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;

namespace HyperPdf.Benchmarks;

/// <summary>Measures finding and judging the file a link points to, done once each time a reader follows such a link.</summary>
public class LinkedFilesBenchmarks
{
    /// <summary>The document that holds the link.</summary>
    private static readonly string Document = Path.Combine(Path.GetTempPath(), "reports", "annual.pdf");

    /// <summary>Resolves a relative path written with Windows separators.</summary>
    /// <returns>The full path.</returns>
    [Benchmark]
    public string? ResolveRelative() => LinkedFiles.Resolve(Document, "appendix\\tables.pdf");

    /// <summary>Judges whether a linked file would run code.</summary>
    /// <returns>Whether it runs code.</returns>
    [Benchmark]
    public bool JudgeRunnable() => LinkedFiles.IsRunnable("quarterly-figures.xlsx");
}
