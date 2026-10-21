// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Presentation;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the toolkit-free maths the page canvas runs on every frame, pointer move or key press.</summary>
public class CanvasMathBenchmarks
{
    /// <summary>The frames in a wheel zoom or a page transition.</summary>
    private const int Frames = 30;

    /// <summary>The milliseconds between frames.</summary>
    private const int FrameMilliseconds = 5;

    /// <summary>The seconds a page transition takes.</summary>
    private const double TransitionSeconds = 1;

    /// <summary>The zoom a wheel zoom starts from.</summary>
    private const double FromZoom = 1;

    /// <summary>The zoom a wheel zoom aims for.</summary>
    private const double ToZoom = 2.5;

    /// <summary>The page width in points.</summary>
    private const float PageWidth = 612F;

    /// <summary>The page height in points.</summary>
    private const float PageHeight = 792F;

    /// <summary>Canvas units per point.</summary>
    private const double Scale = 1.5;

    /// <summary>Where the page is on the canvas.</summary>
    private const double PageOrigin = 50;

    /// <summary>The left of the rectangle whose handles are probed.</summary>
    private const double AreaLeft = 100;

    /// <summary>The top of the rectangle whose handles are probed.</summary>
    private const double AreaTop = 100;

    /// <summary>The width of the rectangle whose handles are probed.</summary>
    private const double AreaWidth = 300;

    /// <summary>The height of the rectangle whose handles are probed.</summary>
    private const double AreaHeight = 200;

    /// <summary>How far from a corner the pointer is for a probe that should hit.</summary>
    private const double Inside = 1;

    /// <summary>The share of the way to the middle.</summary>
    private const double Half = 0.5;

    /// <summary>How close to a corner counts as on its handle.</summary>
    private const double Reach = 8;

    /// <summary>The share of a second a reveal has played.</summary>
    private const int HalfASecond = 2;

    /// <summary>The width of the area the curtain covers.</summary>
    private const double CurtainWidth = 1200;

    /// <summary>The height of the area the curtain covers.</summary>
    private const double CurtainHeight = 800;

    /// <summary>A point on the page.</summary>
    private static readonly PagePoint Point = new(120F, 340F);

    /// <summary>The rectangles a curtain is written into.</summary>
    private readonly LayoutRect[] _curtain = new LayoutRect[TransitionCurtain.MaxRectangles];

    /// <summary>The page transform for each rotation.</summary>
    private PageTransform[] _transforms = [];

    /// <summary>The pointer positions probed: each corner just inside, and the middle.</summary>
    private CanvasPoint[] _probes = [];

    /// <summary>The rectangle whose handles are probed.</summary>
    private LayoutRect _area;

    /// <summary>A reveal of each effect, each half way through.</summary>
    private TransitionRun[] _reveals = [];

    /// <summary>Fills the transforms and probes.</summary>
    [GlobalSetup]
    public void Setup()
    {
        PageRotation[] rotations = [PageRotation.None, PageRotation.Rotate90, PageRotation.Rotate180, PageRotation.Rotate270];
        _transforms = new PageTransform[rotations.Length];
        for (var i = 0; i < rotations.Length; i++)
        {
            _transforms[i] = new(new LayoutRect(PageOrigin, PageOrigin, PageWidth * Scale, PageHeight * Scale), new PageSize(PageWidth, PageHeight), rotations[i], Scale);
        }

        _area = new(AreaLeft, AreaTop, AreaWidth, AreaHeight);
        _reveals = CreateReveals();
        _probes =
        [
            new(AreaLeft + Inside, AreaTop + Inside),
            new(AreaLeft + AreaWidth - Inside, AreaTop + Inside),
            new(AreaLeft + AreaWidth - Inside, AreaTop + AreaHeight - Inside),
            new(AreaLeft + Inside, AreaTop + AreaHeight - Inside),
            new(AreaLeft + (AreaWidth * Half), AreaTop + (AreaHeight * Half)),
        ];
    }

    /// <summary>Works out the zoom of every frame of a wheel zoom.</summary>
    /// <returns>The last zoom.</returns>
    [Benchmark]
    public double WheelZoomFrames()
    {
        var zoom = FromZoom;
        for (var frame = 1; frame <= Frames; frame++)
        {
            var progress = WheelZoomCurve.GetProgress(TimeSpan.FromMilliseconds(frame * FrameMilliseconds));
            zoom = WheelZoomCurve.GetZoom(FromZoom, ToZoom, progress);
        }

        return zoom;
    }

    /// <summary>Works out the progress of every frame of a page transition.</summary>
    /// <returns>The last progress.</returns>
    [Benchmark]
    public double TransitionFrames()
    {
        var progress = 0.0;
        for (var frame = 1; frame <= Frames; frame++)
        {
            progress = TransitionTiming.GetProgress(TransitionSeconds, frame / (double)Frames);
        }

        return progress;
    }

    /// <summary>Maps a page point to the canvas and back for each rotation.</summary>
    /// <returns>The last page point's x.</returns>
    [Benchmark]
    public float PageTransformRoundTrips()
    {
        var x = 0F;
        foreach (var transform in _transforms)
        {
            var canvas = transform.MapToCanvas(Point);
            x = transform.MapToPage(canvas.X, canvas.Y).X;
        }

        return x;
    }

    /// <summary>Looks for a resize handle under the pointer at each corner and in the middle.</summary>
    /// <returns>How many handles were found.</returns>
    [Benchmark]
    public int ResizeHandleGrabs()
    {
        var found = 0;
        foreach (var probe in _probes)
        {
            found += ResizeHandles.TryGrab(_area, probe.X, probe.Y, Reach, out _) ? 1 : 0;
        }

        return found;
    }

    /// <summary>Works out the curtain of every reveal effect half way through, as every frame of a transition does.</summary>
    /// <returns>How many rectangles all the curtains are made of.</returns>
    [Benchmark]
    public int CurtainRectangles()
    {
        var area = new LayoutRect(0, 0, CurtainWidth, CurtainHeight);
        var count = 0;
        foreach (var reveal in _reveals)
        {
            count += TransitionCurtain.GetRectangles(reveal, area, _curtain, out _);
        }

        return count;
    }

    /// <summary>Makes a reveal of each effect, half way through.</summary>
    /// <returns>The reveals.</returns>
    private static TransitionRun[] CreateReveals() =>
    [
        Reveal(TransitionEffect.Fade, false),
        Reveal(TransitionEffect.Wipe, false),
        Reveal(TransitionEffect.Split, false),
        Reveal(TransitionEffect.Split, true),
        Reveal(TransitionEffect.Box, false),
        Reveal(TransitionEffect.Box, true),
    ];

    /// <summary>Makes a reveal that is partly played.</summary>
    /// <param name="effect">The effect.</param>
    /// <param name="flag">Whether it moves inward and runs vertically.</param>
    /// <returns>The run.</returns>
    private static TransitionRun Reveal(TransitionEffect effect, bool flag) =>
        new(true, new(1, TransitionStyle.Wipe, effect, 1, flag, flag, 0, false, null), Stopwatch.GetTimestamp() - (Stopwatch.Frequency / HalfASecond));
}
