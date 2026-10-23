// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Measuring;

namespace HyperPdf.Core.Tests.Measuring;

/// <summary>Checks distances, path lengths, areas and scales.</summary>
public sealed class MeasurementTests
{
    /// <summary>The paper length written with a decimal comma.</summary>
    private const double OneAndAHalf = 1.5;

    /// <summary>The two ends of a line.</summary>
    private const int LineEnds = 2;

    /// <summary>One inch in PDF points.</summary>
    private const float Inch = 72;

    /// <summary>The tolerance for comparing lengths.</summary>
    private const double Tolerance = 1e-6;

    /// <summary>Millimetres in an inch.</summary>
    private const double MillimetresPerInch = 25.4;

    /// <summary>A right angle in degrees.</summary>
    private const double RightAngle = 90;

    /// <summary>Three sides of a square.</summary>
    private const int ThreeSides = 3;

    /// <summary>Four sides of a square.</summary>
    private const int FourSides = 4;

    /// <summary>A unit name longer than the normal stack buffer.</summary>
    private const int LongUnitLength = 300;

    /// <summary>The paper length of a centimetre-based scale, in points: 1 cm.</summary>
    private const double Centimetre = 72 / 2.54;

    /// <summary>The real length 1 cm stands for in "1 cm = 2 m".</summary>
    private const double TwoMetres = 2;

    /// <summary>The real length one millimetre on paper stands for at 1:100.</summary>
    private const double HundredMillimetres = 100;

    /// <summary>One inch at true size is 25.4 mm.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TrueSizeIsThePaper()
    {
        await Assert.That(MeasureScale.Metric.ToReal(Inch)).IsEqualTo(MillimetresPerInch).Within(Tolerance);
        await Assert.That(MeasureScale.Imperial.ToReal(Inch)).IsEqualTo(1).Within(Tolerance);
    }

    /// <summary>Scales are read as drawings write them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsWrittenScales()
    {
        await Assert.That(MeasureScale.TryParse("1 cm = 2 m", out var metric)).IsTrue();
        await Assert.That(metric.ToReal(Centimetre)).IsEqualTo(TwoMetres).Within(Tolerance);
        await Assert.That(MeasureScale.TryParse("1:100", out var ratio)).IsTrue();
        await Assert.That(ratio.ToReal(Inch / MillimetresPerInch)).IsEqualTo(HundredMillimetres).Within(Tolerance);
        await Assert.That(MeasureScale.TryParse("1,5in=3ft", out var comma)).IsTrue();
        await Assert.That(comma.PaperLength).IsEqualTo(OneAndAHalf);
        await Assert.That(MeasureScale.TryParse("1 cubit = 2 m", out _)).IsFalse();
        await Assert.That(MeasureScale.TryParse("big", out _)).IsFalse();
        await Assert.That(MeasureScale.TryParse("0 cm = 1 m", out _)).IsFalse();
    }

    /// <summary>Lengths, perimeters, areas and angles of a one inch square.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeasuresASquare()
    {
        PagePoint[] square = [new(0, 0), new(Inch, 0), new(Inch, Inch), new(0, Inch)];

        await Assert.That(Measurement.Length(square, false)).IsEqualTo(Inch * ThreeSides).Within(Tolerance);
        await Assert.That(Measurement.Length(square, true)).IsEqualTo(Inch * FourSides).Within(Tolerance);
        await Assert.That(Measurement.Area(square)).IsEqualTo(Inch * Inch).Within(Tolerance);
        await Assert.That(Measurement.Area(square.AsSpan(0, LineEnds))).IsEqualTo(0);
        await Assert.That(Measurement.Angle(new(0, Inch), new(0, 0))).IsEqualTo(RightAngle).Within(Tolerance);
    }

    /// <summary>Measurements are described with their unit, ready to show and read out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DescribesMeasurements()
    {
        PagePoint[] square = [new(0, 0), new(Inch, 0), new(Inch, Inch), new(0, Inch)];
        using var culture = new CultureScope("en-GB");

        await Assert.That(Measurement.Describe(MeasureMode.Distance, square.AsSpan(0, LineEnds), MeasureScale.Imperial)).IsEqualTo("Distance 1 in at 0°");
        await Assert.That(Measurement.Describe(MeasureMode.Perimeter, square, MeasureScale.Imperial)).IsEqualTo("Perimeter 3 in");
        await Assert.That(Measurement.Describe(MeasureMode.Area, square, MeasureScale.Metric)).IsEqualTo("Area 645.16 mm²");
        await Assert.That(Measurement.Describe(MeasureMode.Area, square.AsSpan(0, LineEnds), MeasureScale.Metric)).IsEqualTo(string.Empty);
    }

    /// <summary>Long unit names keep the complete description.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DescribesLongUnit()
    {
        PagePoint[] line = [new(0, 0), new(Inch, 0)];
        var unit = new string('x', LongUnitLength);
        var scale = new MeasureScale(1, "in", 1, unit);

        await Assert.That(Measurement.Describe(MeasureMode.Distance, line, scale)).IsEqualTo($"Distance 1 {unit} at 0°");
    }
}
