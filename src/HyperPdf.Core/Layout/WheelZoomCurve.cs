// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Layout;

/// <summary>The curve a wheel zoom follows from the zoom shown to its target.</summary>
public static class WheelZoomCurve
{
    /// <summary>How long one wheel zoom takes to ease to its target, in milliseconds.</summary>
    private const double DurationMilliseconds = 140;

    /// <summary>The power of the ease out curve.</summary>
    private const double EasePower = 3;

    /// <summary>Gets how far an ease has run, from 0 to 1.</summary>
    /// <param name="elapsed">How long the ease has been running.</param>
    /// <returns>The progress, 1 once its time is up.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetProgress(TimeSpan elapsed) => Math.Min(1, elapsed.TotalMilliseconds / DurationMilliseconds);

    /// <summary>Gets the zoom at a point in the ease: a cubic ease out applied to the zoom ratio, so each frame scales by the same proportion.</summary>
    /// <param name="from">The zoom the ease started from.</param>
    /// <param name="to">The target zoom.</param>
    /// <param name="progress">How far the ease has run, from 0 to 1.</param>
    /// <returns>The zoom.</returns>
    public static double GetZoom(double from, double to, double progress)
    {
        var eased = 1 - Math.Pow(1 - progress, EasePower);
        return from * Math.Pow(to / from, eased);
    }
}
