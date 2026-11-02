// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Media.U3d;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>Checks the small decisions of the U3D helpers that whole-scene tests do not reach.</summary>
public sealed class U3dBranchTests
{
    /// <summary>The visibility setting that draws front faces.</summary>
    private const uint FrontOnly = 1;

    /// <summary>The visibility setting that draws back faces.</summary>
    private const uint BackOnly = 2;

    /// <summary>A symbol no histogram counts.</summary>
    private const uint FarSymbol = 4_000_000_000;

    /// <summary>Half way along a unit span.</summary>
    private const double Halfway = 0.5;

    /// <summary>The control points, and blending values, of a degree one direction.</summary>
    private const int LinearValues = 2;

    /// <summary>The 64-bit value written into the cursor test data.</summary>
    private const ulong WideValue = 2;

    /// <summary>A count larger than a block can hold.</summary>
    private const long HugeSkip = long.MaxValue;

    /// <summary>A cursor over no bytes fails every read and gives zero.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ACursorOverNoBytesGivesZeroes()
    {
        var cursor = new U3dCursor([]);
        var small = cursor.ReadI16();
        var wide = cursor.ReadU64();
        var real = cursor.ReadF64();
        var text = cursor.ReadString();
        var skipped = cursor.Skip(HugeSkip);
        var failed = cursor.Failed;

        await Assert.That(small).IsEqualTo((short)0);
        await Assert.That(wide).IsEqualTo(0UL);
        await Assert.That(real).IsEqualTo(0.0);
        await Assert.That(text).IsEqualTo(string.Empty);
        await Assert.That(skipped).IsFalse();
        await Assert.That(failed).IsTrue();
    }

    /// <summary>A cursor over enough bytes reads each value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ACursorReadsWideValues()
    {
        byte[] data = [1, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xF0, 0x3F];
        var cursor = new U3dCursor(data);
        var small = cursor.ReadI16();
        var wide = cursor.ReadU64();
        var real = cursor.ReadF64();

        await Assert.That(small).IsEqualTo((short)1);
        await Assert.That(wide).IsEqualTo(WideValue);
        await Assert.That(real).IsEqualTo(1.0);
    }

    /// <summary>A histogram counts nothing for a symbol it does not hold, and puts such a symbol after every other.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AHistogramIgnoresSymbolsItDoesNotHold()
    {
        var histogram = new U3dHistogram();

        await Assert.That(histogram.CountOf(FarSymbol)).IsEqualTo(0U);
        await Assert.That(histogram.CountOf(0)).IsEqualTo(1U);
        await Assert.That(histogram.CountBelow(FarSymbol)).IsEqualTo(histogram.Total);
        await Assert.That(histogram.CountBelow(0)).IsEqualTo(0U);
    }

    /// <summary>The last block of a file may leave out its padding, but a block whose sections do not fit is cut short.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheLastBlockMayLeaveOutItsPadding()
    {
        byte[] unpadded = [0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 7];
        byte[] cutShort = [0, 0, 0, 0, 1, 0, 0, 0, 8, 0, 0, 0, 7];
        var at = 0;
        var kept = U3dFrames.Next(unpadded, ref at);
        var keptAt = at;
        at = 0;
        var cut = U3dFrames.Next(cutShort, ref at);

        await Assert.That(kept.HasValue).IsTrue();
        await Assert.That(keptAt).IsEqualTo(unpadded.Length);
        await Assert.That(cut.HasValue).IsFalse();
    }

    /// <summary>Blending functions cope with knots that have no width, and a surface of zero weights has no point.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DegenerateKnotsAndWeightsGiveNoMovement()
    {
        var values = new double[LinearValues];
        var bezier = U3dSurfaceBasis.Evaluate([0.0, 0.0], 1, LinearValues, true, 0, values);
        var spline = U3dSurfaceBasis.Evaluate([0.0, 0.0, 0.0, 0.0], 1, LinearValues, false, 0, values);
        var surface = new U3dSurface(true, 1, 1, [0.0, 1.0], [0.0, 1.0], LinearValues, LinearValues, new Vector4[LinearValues * LinearValues], 0);
        var point = U3dSurfaceMesher.Evaluate(surface, Halfway, Halfway);

        await Assert.That(bezier).IsEqualTo(0);
        await Assert.That(spline).IsEqualTo(0);
        await Assert.That(point).IsEqualTo(Vector3.Zero);
    }

    /// <summary>A face with no area is never drawn, and the others follow the visibility setting.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VisibilityFollowsTheFaceDirection()
    {
        await Assert.That(U3dScreenSpace.IsVisible(0, FrontOnly | BackOnly)).IsFalse();
        await Assert.That(U3dScreenSpace.IsVisible(-1, FrontOnly)).IsTrue();
        await Assert.That(U3dScreenSpace.IsVisible(-1, BackOnly)).IsFalse();
        await Assert.That(U3dScreenSpace.IsVisible(1, BackOnly)).IsTrue();
        await Assert.That(U3dScreenSpace.IsVisible(1, FrontOnly)).IsFalse();
    }
}
