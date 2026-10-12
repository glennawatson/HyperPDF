// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Printing;
using HyperPdf.Platform.Cups;

namespace HyperPdf.Benchmarks;

/// <summary>Measures building printer job options for each duplex edge.</summary>
public class CupsPrintingBenchmarks
{
    /// <summary>Gets or sets whether duplex printing is enabled.</summary>
    [Params(false, true)]
    public bool TwoSided { get; set; }

    /// <summary>Gets or sets the edge used to turn the sheet.</summary>
    [Params(DuplexBinding.LongEdge, DuplexBinding.ShortEdge)]
    public DuplexBinding Binding { get; set; }

    /// <summary>Builds the options sent with a print job.</summary>
    /// <returns>The CUPS option names and values.</returns>
    [Benchmark]
    public (string Name, string Value)[] BuildJobOptions()
    {
        var job = new PrintJobOptions("printer", 1, true, TwoSided, PaperSize.A4) { Binding = Binding };
        return CupsPrinting.BuildOptions(job);
    }
}
