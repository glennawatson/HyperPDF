// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Measuring;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the measuring tool: working out a measurement as the pointer moves, and reading a drawing's scale.</summary>
public class MeasureBenchmarks
{
    /// <summary>The corners of the measured shape.</summary>
    private readonly PagePoint[] _corners = [new(100, 150), new(300, 150), new(320, 300), new(100, 310), new(80, 220)];

    /// <summary>A drawing that declares its scale.</summary>
    private byte[] _drawing = [];

    /// <summary>Creates the drawing.</summary>
    [GlobalSetup]
    public void Setup() => _drawing = TestPdf.CreateWithViewport();

    /// <summary>Works out an area at a drawing scale, as happens on every pointer move.</summary>
    /// <returns>The area in square points.</returns>
    [Benchmark]
    public double Area() => Measurement.Area(_corners) + Measurement.Length(_corners, true);

    /// <summary>Describes the measurement for the bar and the label.</summary>
    /// <returns>The description.</returns>
    [Benchmark]
    public string Describe() => Measurement.Describe(MeasureMode.Area, _corners, MeasureScale.Metric);

    /// <summary>Reads a page's declared scale from its viewport.</summary>
    /// <returns>Whether a scale was found.</returns>
    [Benchmark]
    public bool ReadScale() => PdfViewports.ReadScale(_drawing, 0).HasValue;
}
