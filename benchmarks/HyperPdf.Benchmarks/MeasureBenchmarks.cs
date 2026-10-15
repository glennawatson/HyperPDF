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
    /// <summary>The characters the description buffer holds.</summary>
    private const int DescriptionCapacity = 256;

    /// <summary>The scale text typed into the measuring tool.</summary>
    private const string ScaleText = "1 cm = 2 m";

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

    /// <summary>Describes the measurement for the bar and the label, as happens on every pointer move.</summary>
    /// <returns>The characters written.</returns>
    [Benchmark]
    public int Describe()
    {
        Span<char> text = stackalloc char[DescriptionCapacity];
        _ = Measurement.TryDescribe(MeasureMode.Area, _corners, MeasureScale.Metric, text, out var written);
        return written;
    }

    /// <summary>Reads the typed scale, as happens on every pointer move.</summary>
    /// <returns>Whether the text is a scale.</returns>
    [Benchmark]
    public bool ParseScale() => MeasureScale.TryParse(ScaleText, out _);

    /// <summary>Reads a page's declared scale from its viewport.</summary>
    /// <returns>Whether a scale was found.</returns>
    [Benchmark]
    public bool ReadScale() => PdfViewports.ReadScale(_drawing, 0).HasValue;
}
