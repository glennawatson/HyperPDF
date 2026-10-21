// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Presentation;

/// <summary>The timing and direction of a page transition, which every render head plays the same way.</summary>
public static class TransitionTiming
{
    /// <summary>Degrees in a quarter turn, which the four straight directions of a page's transition are multiples of.</summary>
    private const int QuarterTurn = 90;

    /// <summary>Half of <see cref="QuarterTurn"/>, so a direction rounds to the nearest straight one.</summary>
    private const int HalfQuarterTurn = 45;

    /// <summary>Degrees in a full turn.</summary>
    private const int FullTurn = 360;

    /// <summary>The constant of the smooth step curve, three less two times the progress.</summary>
    private const double SmoothBase = 3;

    /// <summary>The slope of the smooth step curve.</summary>
    private const double SmoothSlope = 2;

    /// <summary>Gets how far a transition has played, from 0 to 1, eased so it starts and ends gently.</summary>
    /// <param name="durationSeconds">How long the transition takes.</param>
    /// <param name="elapsedSeconds">How long it has been playing.</param>
    /// <returns>The progress.</returns>
    public static double GetProgress(double durationSeconds, double elapsedSeconds)
    {
        var seconds = Math.Max(durationSeconds, double.Epsilon);
        var linear = Math.Min(1, elapsedSeconds / seconds);
        return linear * linear * (SmoothBase - (SmoothSlope * linear));
    }

    /// <summary>Gets how far a transition has played, from 0 to 1, eased so it starts and ends gently.</summary>
    /// <param name="durationSeconds">How long the transition takes.</param>
    /// <param name="started">The <see cref="Stopwatch.GetTimestamp"/> value when it started.</param>
    /// <returns>The progress.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetProgress(double durationSeconds, long started) =>
        GetProgress(durationSeconds, Stopwatch.GetElapsedTime(started).TotalSeconds);

    /// <summary>Reduces a direction in degrees to the nearest of the four straight ones: 0 is left to right, then anti-clockwise.</summary>
    /// <param name="degrees">The direction in degrees.</param>
    /// <returns>The nearest straight direction.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TransitionDirection GetDirection(int degrees) =>
        (TransitionDirection)((((degrees % FullTurn) + FullTurn + HalfQuarterTurn) % FullTurn) / QuarterTurn);

    /// <summary>Gets how far a sliding page is from its place.</summary>
    /// <param name="degrees">The direction of the slide in degrees.</param>
    /// <param name="progress">How far the effect has played, from 0 to 1.</param>
    /// <param name="width">The width of the area the page fills.</param>
    /// <param name="height">The height of the area the page fills.</param>
    /// <returns>The horizontal and vertical distance still to travel.</returns>
    public static SlideOffset GetSlideOffset(int degrees, double progress, double width, double height)
    {
        var remaining = 1 - progress;
        return GetDirection(degrees) switch
        {
            TransitionDirection.BottomToTop => new(0, height * remaining),
            TransitionDirection.RightToLeft => new(width * remaining, 0),
            TransitionDirection.TopToBottom => new(0, -height * remaining),
            _ => new(-width * remaining, 0),
        };
    }
}
