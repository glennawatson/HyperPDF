// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks the strokes of a signature pad and the fitting of a mark, with no toolkit.</summary>
public sealed class SignaturePadStrokesTests
{
    /// <summary>The pad width.</summary>
    private const double PadWidth = 200;

    /// <summary>The pad height.</summary>
    private const double PadHeight = 100;

    /// <summary>A point beyond the pad's edge.</summary>
    private const double Outside = 500;

    /// <summary>A negative point beyond the pad's edge.</summary>
    private const double Before = -20;

    /// <summary>A point inside the pad.</summary>
    private const double Inside = 40;

    /// <summary>The points in a stroke with a start and one move.</summary>
    private const int TwoPoints = 2;

    /// <summary>How many moves are tried past the most points.</summary>
    private const int ExtraMoves = 10;

    /// <summary>The mark width in the fit test.</summary>
    private const float MarkWidth = 200F;

    /// <summary>The mark height in the fit test.</summary>
    private const float MarkHeight = 100F;

    /// <summary>The area width in the fit test.</summary>
    private const double AreaWidth = 100;

    /// <summary>The area height in the fit test.</summary>
    private const double AreaHeight = 100;

    /// <summary>The area origin in the fit test.</summary>
    private const double AreaOrigin = 10;

    /// <summary>The fitted mark's height.</summary>
    private const double FittedHeight = 50;

    /// <summary>The fitted mark's top, centred in the area.</summary>
    private const double FittedTop = 35;

    /// <summary>A stroke starts at the press, grows with moves kept inside the pad, and ends with the release.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrokeIsKeptInsideThePad()
    {
        var strokes = new SignaturePadStrokes();
        strokes.Begin(Inside, Inside);
        _ = strokes.Extend(Outside, Before, PadWidth, PadHeight);
        var current = strokes.Current.ToArray();
        var finished = strokes.Finish();

        await Assert.That(current.Length).IsEqualTo(TwoPoints);
        await Assert.That(current[1]).IsEqualTo(new((float)PadWidth, 0F));
        await Assert.That(finished).IsNotNull();
        await Assert.That(finished!.Length).IsEqualTo(TwoPoints);
        await Assert.That(strokes.Drawn.Count).IsEqualTo(1);
        await Assert.That(strokes.Current.Length).IsEqualTo(0);
        await Assert.That(strokes.IsEmpty).IsFalse();
    }

    /// <summary>A release without a press finishes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FinishWithoutAStrokeGivesNothing()
    {
        var strokes = new SignaturePadStrokes();

        await Assert.That(strokes.Finish()).IsNull();
        await Assert.That(strokes.IsEmpty).IsTrue();
    }

    /// <summary>A stroke keeps no more than the most points.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrokeStopsAtTheMostPoints()
    {
        var strokes = new SignaturePadStrokes();
        strokes.Begin(0, 0);
        var added = 0;
        for (var i = 0; i < SignaturePadStrokes.MaxPoints + ExtraMoves; i++)
        {
            added += strokes.Extend(Inside, Inside, PadWidth, PadHeight) ? 1 : 0;
        }

        await Assert.That(strokes.Current.Length).IsEqualTo(SignaturePadStrokes.MaxPoints);
        await Assert.That(added).IsEqualTo(SignaturePadStrokes.MaxPoints - 1);
        await Assert.That(strokes.CanExtend).IsFalse();
    }

    /// <summary>Clearing forgets finished strokes and the one being drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClearForgetsEverything()
    {
        var strokes = new SignaturePadStrokes();
        strokes.Begin(Inside, Inside);
        _ = strokes.Finish();
        strokes.Begin(Inside, Inside);

        strokes.Clear();

        await Assert.That(strokes.IsEmpty).IsTrue();
        await Assert.That(strokes.Drawn.Count).IsEqualTo(0);
    }

    /// <summary>A mark is scaled to fit and centred in the area, keeping its shape.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FitKeepsTheShapeAndCentres()
    {
        var mark = new SignatureMark { Width = MarkWidth, Height = MarkHeight };

        var fitted = SignaturePadStrokes.Fit(mark, new(AreaOrigin, AreaOrigin, AreaWidth, AreaHeight));

        await Assert.That(fitted.Width).IsEqualTo(AreaWidth);
        await Assert.That(fitted.Height).IsEqualTo(FittedHeight);
        await Assert.That(fitted.X).IsEqualTo(AreaOrigin);
        await Assert.That(fitted.Y).IsEqualTo(FittedTop);
    }
}
