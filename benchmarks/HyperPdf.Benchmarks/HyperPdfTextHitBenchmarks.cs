// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Text;

namespace HyperPdf.Benchmarks;

/// <summary>Compares scalar and indexed queries while preserving the same character geometry and original order.</summary>
public class HyperPdfTextHitBenchmarks
{
    /// <summary>The characters and query shared by both measured paths.</summary>
    private HyperPdfHitFixture _fixture = null!;

    /// <summary>The summaries built outside the warm query measurements.</summary>
    private TextHitBlock[] _blocks = [];

    /// <summary>Gets or sets the page geometry and query position.</summary>
    [ParamsAllValues]
    public HyperPdfHitScenario Scenario { get; set; }

    /// <summary>Builds both paths over identical data and checks their results before timing.</summary>
    /// <exception cref="InvalidOperationException">The two query paths return different character indices.</exception>
    [GlobalSetup]
    public void Setup()
    {
        _fixture = HyperPdfHitFixtures.Create(Scenario);
        _blocks = TextHitTesting.BuildBlocks(_fixture.Characters);
        if (Scalar() != Indexed())
        {
            throw new InvalidOperationException("Scalar and indexed hit-test results differ.");
        }
    }

    /// <summary>Queries the direct scalar fallback over all original character records.</summary>
    /// <returns>The matching character index.</returns>
    [Benchmark(Baseline = true)]
    public int Scalar() => TextHitTesting.Find(_fixture.Characters, [], _fixture.Point, _fixture.Tolerance, _fixture.Tolerance);

    /// <summary>Queries the same records through their contiguous block summaries.</summary>
    /// <returns>The matching character index.</returns>
    [Benchmark]
    public int Indexed() => TextHitTesting.Find(_fixture.Characters, _blocks, _fixture.Point, _fixture.Tolerance, _fixture.Tolerance);
}
