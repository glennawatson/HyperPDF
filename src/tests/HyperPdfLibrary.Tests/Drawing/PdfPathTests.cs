// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Tests.Drawing;

/// <summary>Checks managed path construction and immutable snapshots.</summary>
public sealed class PdfPathTests
{
    /// <summary>The number of commands in the curve outline.</summary>
    private const int ExpectedCommands = 5;

    /// <summary>The number of commands in a two-point path.</summary>
    private const int TwoCommands = 2;

    /// <summary>The number of endpoints and control points in the curve outline.</summary>
    private const int ExpectedPointCount = 7;

    /// <summary>The index of the move command.</summary>
    private const int MoveIndex = 0;

    /// <summary>The index of the line command.</summary>
    private const int LineIndex = 1;

    /// <summary>The index of the quadratic command.</summary>
    private const int QuadraticIndex = 2;

    /// <summary>The index of the cubic command.</summary>
    private const int CubicIndex = 3;

    /// <summary>The index of the close command.</summary>
    private const int CloseIndex = 4;

    /// <summary>The origin coordinate.</summary>
    private const float Origin = 0;

    /// <summary>The first coordinate used in the commands.</summary>
    private const float First = 1;

    /// <summary>The second coordinate used in the commands.</summary>
    private const float Second = 2;

    /// <summary>The third coordinate used in the commands.</summary>
    private const float Third = 3;

    /// <summary>The fourth coordinate used in the commands.</summary>
    private const float Fourth = 4;

    /// <summary>The horizontal translation used by the transform test.</summary>
    private const float TranslateX = 5;

    /// <summary>The vertical translation used by the transform test.</summary>
    private const float TranslateY = 6;

    /// <summary>The distance of a 3-4 right triangle.</summary>
    private const float TriangleDistance = 5;

    /// <summary>Stores all font outline commands and their control points.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BuilderStoresCurveCommandsAndPointCount()
    {
        var builder = new PdfPathBuilder();
        builder.MoveTo(Origin, Origin);
        builder.LineTo(First, First);
        builder.QuadTo(Second, Second, Third, Third);
        builder.CubicTo(First, Second, Third, Fourth, Fourth, First);
        builder.Close();
        var path = builder.Detach();

        using (Assert.Multiple())
        {
            await Assert.That(path.Commands.Length).IsEqualTo(ExpectedCommands);
            await Assert.That(path.PointCount).IsEqualTo(ExpectedPointCount);
            await Assert.That(path.Commands[MoveIndex].Kind).IsEqualTo(PdfPathCommandKind.MoveTo);
            await Assert.That(path.Commands[QuadraticIndex].Control1).IsEqualTo(new(Second, Second));
            await Assert.That(path.Commands[CubicIndex].Control2).IsEqualTo(new(Third, Fourth));
            await Assert.That(path.Commands[CloseIndex].Kind).IsEqualTo(PdfPathCommandKind.Close);
            await Assert.That(path.IsEmpty).IsFalse();
        }
    }

    /// <summary>Detaching gives the path an independent command array.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DetachCreatesAnIndependentPath()
    {
        var builder = new PdfPathBuilder();
        builder.MoveTo(Origin, Origin);
        builder.LineTo(First, First);
        var path = builder.Detach();
        builder.MoveTo(Second, Second);
        var next = builder.Detach();

        using (Assert.Multiple())
        {
            await Assert.That(path.Commands.Length).IsEqualTo(TwoCommands);
            await Assert.That(path.Commands[LineIndex].Point).IsEqualTo(new(First, First));
            await Assert.That(next.Commands.Length).IsEqualTo(1);
            await Assert.That(next.Commands[MoveIndex].Point).IsEqualTo(new(Second, Second));
        }
    }

    /// <summary>Transforms produce independent points and update path bounds.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TransformReturnsIndependentPathWithUpdatedBounds()
    {
        var builder = new PdfPathBuilder();
        builder.MoveTo(Origin, Origin);
        builder.LineTo(Second, Third);
        var path = builder.Detach();
        var transformed = path.Transform(Matrix3x2.CreateTranslation(TranslateX, TranslateY));

        using (Assert.Multiple())
        {
            await Assert.That(path.Commands[LineIndex].Point).IsEqualTo(new(Second, Third));
            await Assert.That(transformed.Commands[LineIndex].Point).IsEqualTo(new(Second + TranslateX, Third + TranslateY));
            await Assert.That(transformed.Bounds).IsEqualTo(new(TranslateX, TranslateY, Second + TranslateX, Third + TranslateY));
        }
    }

    /// <summary>AddRect emits a closed contour and preserves the fill rule.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddRectCreatesClosedContourWithFillRule()
    {
        var builder = new PdfPathBuilder { FillRule = PdfPathFillRule.EvenOdd };
        builder.AddRect(new(Origin, Origin, Second, Third));
        var path = builder.Detach();

        using (Assert.Multiple())
        {
            await Assert.That(path.Commands.Length).IsEqualTo(ExpectedCommands);
            await Assert.That(path.PointCount).IsEqualTo(ExpectedCommands - 1);
            await Assert.That(path.Commands[CloseIndex].Kind).IsEqualTo(PdfPathCommandKind.Close);
            await Assert.That(path.FillRule).IsEqualTo(PdfPathFillRule.EvenOdd);
            await Assert.That(path.Bounds).IsEqualTo(new(Origin, Origin, Second, Third));
        }
    }

    /// <summary>Builder reset and point and rectangle helpers preserve their geometry contracts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResetDistanceAndInflateWork()
    {
        var builder = new PdfPathBuilder { FillRule = PdfPathFillRule.EvenOdd };
        builder.MoveTo(Origin, Origin);
        builder.Reset();
        var empty = builder.Detach();
        var inflated = new PdfRect(Origin, Origin, Second, Third).Inflate(First, First);

        using (Assert.Multiple())
        {
            await Assert.That(empty.IsEmpty).IsTrue();
            await Assert.That(empty.FillRule).IsEqualTo(PdfPathFillRule.Winding);
            await Assert.That(PdfPoint.Distance(new(Origin, Origin), new(Third, Fourth))).IsEqualTo(TriangleDistance);
            await Assert.That(inflated).IsEqualTo(new(-First, -First, Third, Fourth));
        }
    }
}
