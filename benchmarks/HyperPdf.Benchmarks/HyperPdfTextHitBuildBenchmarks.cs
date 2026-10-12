// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Loggers;
using HyperPdfLibrary.Text;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the cold block-index build separately from warm character queries.</summary>
public class HyperPdfTextHitBuildBenchmarks
{
    /// <summary>The original character records whose summaries are built.</summary>
    private PdfTextChar[] _characters = [];

    /// <summary>Gets or sets the page size, including both sides of the indexing threshold.</summary>
    [Params(32, 127, 128, 4096)]
    public int CharacterCount { get; set; }

    /// <summary>Creates the character records outside the measured index-build operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _characters = HyperPdfHitFixtures.CreateGrid(CharacterCount);
        _ = TextHitTesting.BuildBlocks(_characters);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var blocks = TextHitTesting.BuildBlocks(_characters);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        ConsoleLogger.Default.WriteLine(LogKind.Info, string.Create(
            CultureInfo.InvariantCulture,
            $"Index storage: {CharacterCount} characters, {blocks.Length} blocks, {Unsafe.SizeOf<TextHitBlock>()} bytes/block, "
            + $"{blocks.Length * Unsafe.SizeOf<TextHitBlock>()} payload bytes, {allocated} allocated bytes."));
    }

    /// <summary>Builds and retains the index array so its allocation remains part of the measured operation.</summary>
    /// <returns>The block index array.</returns>
    [Benchmark]
    public object BuildBlocks() => TextHitTesting.BuildBlocks(_characters);
}
