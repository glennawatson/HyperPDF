// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Skia.Geometry;

namespace HyperPdf.App.Tests;

/// <summary>Checks fill paths when geometry contains different figure types.</summary>
public sealed class StreamGeometryImplTests
{
    /// <summary>The FigureSize used by the test workload.</summary>
    private const int FigureSize = 10;

    /// <summary>The UnfilledStart used by the test workload.</summary>
    private const int UnfilledStart = 20;

    /// <summary>The UnfilledEnd used by the test workload.</summary>
    private const int UnfilledEnd = 30;

    /// <summary>The FilledCenter used by the test workload.</summary>
    private const int FilledCenter = 5;

    /// <summary>The UnfilledCenter used by the test workload.</summary>
    private const int UnfilledCenter = 25;

    /// <summary>Verifies that an unfilled figure does not discard or extend an earlier fill.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Open_MixedFigures_PreservesOnlyFilledFigure()
    {
        using var geometry = new StreamGeometryImpl();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new(0, 0));
            context.LineTo(new(FigureSize, 0));
            context.LineTo(new(FigureSize, FigureSize));
            context.LineTo(new(0, FigureSize));
            context.EndFigure(true);
            context.BeginFigure(new(UnfilledStart, 0), false);
            context.LineTo(new(UnfilledEnd, 0));
            context.LineTo(new(UnfilledEnd, FigureSize));
            context.LineTo(new(UnfilledStart, FigureSize));
            context.EndFigure(true);
        }

        await Assert.That(geometry.FillPath!.Contains(FilledCenter, FilledCenter)).IsTrue();
        await Assert.That(geometry.FillPath.Contains(UnfilledCenter, FilledCenter)).IsFalse();
    }

    /// <summary>Verifies that a non-stroked edge remains part of the fill contour.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Open_UnstrokedSegment_PreservesFillPrefix()
    {
        using var geometry = new StreamGeometryImpl();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new(0, 0));
            context.LineTo(new(FigureSize, 0));
            context.LineTo(new(FigureSize, FigureSize), false);
            context.LineTo(new(0, FigureSize));
            context.EndFigure(true);
        }

        await Assert.That(geometry.FillPath!.Contains(FilledCenter, FilledCenter)).IsTrue();
    }
}
