// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the retained options object independently of document creation.</summary>
public class TagOptionsAllocationBenchmarks
{
    /// <summary>Creates an options object with the existing note subtype.</summary>
    /// <returns>The new options object.</returns>
    [Benchmark]
    public PdfTagOptions CreateOptions() => new() { NoteType = PdfNoteType.Footnote };
}
