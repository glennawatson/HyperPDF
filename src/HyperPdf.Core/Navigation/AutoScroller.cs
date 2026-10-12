// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Navigation;

/// <summary>
/// Works out how far auto-scroll moves the pages. Smooth scrolling moves a little every frame at a calm, steady speed.
/// When movement is reduced the same average speed is kept, but the pages step one whole line at a steady interval
/// instead of gliding.
/// </summary>
[DebuggerDisplay("AutoScroller: {_pending}")]
public sealed class AutoScroller
{
    /// <summary>The slowest speed.</summary>
    public static readonly int MinSpeed = 1;

    /// <summary>The fastest speed.</summary>
    public static readonly int MaxSpeed = 9;

    /// <summary>The speed auto-scroll starts at: slow enough to read along.</summary>
    public static readonly int DefaultSpeed = 3;

    /// <summary>How far one line step moves, in device independent pixels; the same as one arrow key press.</summary>
    public static readonly double LineDistance = 48;

    /// <summary>How many device independent pixels per second each speed step adds.</summary>
    private const double PixelsPerSecondPerSpeed = 20;

    /// <summary>The longest time one frame may cover, so a pause in frames never turns into a jump.</summary>
    private const double MaxFrameSeconds = 0.25;

    /// <summary>The distance owed but not moved yet.</summary>
    private double _pending;

    /// <summary>Gets the scrolling speed for a speed setting.</summary>
    /// <param name="speed">The speed setting, from <see cref="MinSpeed"/> to <see cref="MaxSpeed"/>.</param>
    /// <returns>Device independent pixels per second.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetPixelsPerSecond(int speed) => ClampSpeed(speed) * PixelsPerSecondPerSpeed;

    /// <summary>Keeps a speed setting within the allowed range.</summary>
    /// <param name="speed">The speed setting.</param>
    /// <returns>The speed, from <see cref="MinSpeed"/> to <see cref="MaxSpeed"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ClampSpeed(int speed) => Math.Clamp(speed, MinSpeed, MaxSpeed);

    /// <summary>Forgets any distance owed, for example when auto-scroll starts again.</summary>
    public void Reset() => _pending = 0;

    /// <summary>Moves time on and gets how far to scroll now.</summary>
    /// <param name="seconds">The time since the last call.</param>
    /// <param name="speed">The speed setting.</param>
    /// <param name="stepByLine">Whether movement is reduced, so whole lines are stepped at a steady interval.</param>
    /// <returns>The distance to scroll down now, in device independent pixels; 0 while a line step is not due.</returns>
    public double Advance(double seconds, int speed, bool stepByLine)
    {
        if (!(seconds > 0))
        {
            return 0;
        }

        _pending += Math.Min(seconds, MaxFrameSeconds) * GetPixelsPerSecond(speed);
        if (!stepByLine)
        {
            var distance = _pending;
            _pending = 0;
            return distance;
        }

        if (_pending < LineDistance)
        {
            return 0;
        }

        var lines = Math.Floor(_pending / LineDistance);
        _pending -= lines * LineDistance;
        return lines * LineDistance;
    }
}
