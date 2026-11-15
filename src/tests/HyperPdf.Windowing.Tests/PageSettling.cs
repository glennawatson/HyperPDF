// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;
using HyperPdf.Viewer;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// Waits for the pages of a freshly opened document to stop moving. A page fits itself to the window after it loads, and a box typed into
/// over a field closes when its field moves, so a test that acts at once on a slow machine can lose the box to that move.
/// </summary>
internal static class PageSettling
{
    /// <summary>How long the page must stay in place before it counts as settled, in seconds.</summary>
    private const double StillSeconds = 0.5;

    /// <summary>The longest the wait lasts, in seconds.</summary>
    private const double GiveUpSeconds = 30;

    /// <summary>The wait of each turn of the loop, in milliseconds.</summary>
    private const int TurnMilliseconds = 10;

    /// <summary>The size of the probe rectangle on the first page, in points.</summary>
    private const float ProbeSize = 1F;

    /// <summary>Pumps the session until the first page has stayed where it is for a while.</summary>
    /// <param name="session">The session.</param>
    internal static void Wait(ViewerSession session)
    {
        var probe = new PageRect(0, 0, ProbeSize, ProbeSize);
        var last = session.Canvas.GetWindowRect(0, probe);
        var started = Stopwatch.GetTimestamp();
        var since = started;
        while (Stopwatch.GetElapsedTime(since).TotalSeconds < StillSeconds && Stopwatch.GetElapsedTime(started).TotalSeconds < GiveUpSeconds)
        {
            _ = session.PumpOnce(TurnMilliseconds);
            var now = session.Canvas.GetWindowRect(0, probe);
            since = now == last ? since : Stopwatch.GetTimestamp();
            last = now;
        }
    }
}
