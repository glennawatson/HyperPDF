// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Tests.Layout;

/// <summary>Checks the curve a wheel zoom follows to its target.</summary>
public sealed class WheelZoomCurveTests
{
    /// <summary>The zoom an ease starts from.</summary>
    private const double From = 1;

    /// <summary>The zoom an ease aims for.</summary>
    private const double To = 4;

    /// <summary>How close two zooms must be to be equal.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>How many durations after the start counts as long finished.</summary>
    private const int Overrun = 2;

    /// <summary>The progress at the halfway point.</summary>
    private const double Half = 0.5;

    /// <summary>Progress runs from nothing to done over the duration, and stays done after it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProgressRunsOverTheDuration()
    {
        await Assert.That(WheelZoomCurve.GetProgress(TimeSpan.Zero)).IsEqualTo(0);
        await Assert.That(WheelZoomCurve.GetProgress(WheelZoomCurve.Duration)).IsEqualTo(1);
        await Assert.That(WheelZoomCurve.GetProgress(WheelZoomCurve.Duration * Overrun)).IsEqualTo(1);
    }

    /// <summary>The ease starts at the start zoom and ends at the target.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EaseSpansStartToTarget()
    {
        await Assert.That(WheelZoomCurve.GetZoom(From, To, 0)).IsEqualTo(From).Within(Tolerance);
        await Assert.That(WheelZoomCurve.GetZoom(From, To, 1)).IsEqualTo(To).Within(Tolerance);
    }

    /// <summary>The ease is an ease out: halfway through time it is further than halfway in the zoom ratio.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EaseOutIsAheadAtTheMiddle()
    {
        var middle = WheelZoomCurve.GetZoom(From, To, Half);

        await Assert.That(middle).IsGreaterThan(Math.Sqrt(From * To));
        await Assert.That(middle).IsLessThan(To);
    }
}
