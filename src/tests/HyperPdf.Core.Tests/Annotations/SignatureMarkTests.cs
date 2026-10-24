// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Tests.Annotations;

/// <summary>Tests making typed, drawn and picture signature marks.</summary>
public sealed class SignatureMarkTests
{
    /// <summary>How close a smoothed point must be to the sample it passes through.</summary>
    private const float Tolerance = 0.001F;

    /// <summary>The coordinates in each stored point.</summary>
    private const int Coordinates = 2;

    /// <summary>The zigzag's left edge.</summary>
    private const float ZigzagLeft = 10;

    /// <summary>The zigzag's top edge.</summary>
    private const float ZigzagTop = 20;

    /// <summary>The zigzag's width.</summary>
    private const float ZigzagWidth = 30;

    /// <summary>The zigzag's height.</summary>
    private const float ZigzagHeight = 20;

    /// <summary>The picture's width in pixels.</summary>
    private const int PictureWidth = 2;

    /// <summary>The points a tap is stored as.</summary>
    private const int DotPoints = 2;

    /// <summary>A zigzag stroke, away from the origin, as a pointer would draw it.</summary>
    private static readonly PagePoint[] Zigzag = [new(10, 20), new(20, 40), new(30, 20), new(40, 40)];

    /// <summary>A single tap.</summary>
    private static readonly PagePoint[] Tap = [new(5, 5)];

    /// <summary>A short straight line.</summary>
    private static readonly PagePoint[] Line = [new(0, 0), new(4, 4)];

    /// <summary>A picture: opaque ink beside soft ink.</summary>
    private static readonly byte[] Ink = [0, 0, 0, 255, 0, 0, 0, 128];

    /// <summary>Typed marks drop surrounding spaces and a blank name makes no mark.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TypedMarkTrimsAndRejectsBlank()
    {
        var mark = await Assert.That(SignatureMark.Typed(SignatureMarkKind.Initials, "  GW ")).IsNotNull();

        await Assert.That(mark!.Text).IsEqualTo("GW");
        await Assert.That(mark.Kind).IsEqualTo(SignatureMarkKind.Initials);
        await Assert.That(mark.Style).IsEqualTo(SignatureMarkStyle.Typed);
        await Assert.That(mark.IsValid).IsTrue();
        await Assert.That(SignatureMark.Typed(SignatureMarkKind.Signature, "   ")).IsNull();
        await Assert.That(SignatureMark.Typed(SignatureMarkKind.Signature, null)).IsNull();
    }

    /// <summary>A drawn stroke is smoothed into a curve through every sample and moved to the mark's corner.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawnMarkCurvesThroughEverySample()
    {
        var mark = await Assert.That(SignatureMark.Drawn(SignatureMarkKind.Signature, Zigzag, [Zigzag.Length])).IsNotNull();
        var points = mark!.Points.ToArray();
        var lengths = mark.StrokeLengths.ToArray();

        await Assert.That(lengths).IsEquivalentTo([SignatureStrokes.SmoothedLength(Zigzag.Length)]);
        await Assert.That(lengths[0]).IsGreaterThan(Zigzag.Length);
        await Assert.That(mark.Width).IsEqualTo(ZigzagWidth);
        await Assert.That(mark.Height).IsEqualTo(ZigzagHeight);
        for (var i = 0; i < Zigzag.Length; i++)
        {
            // Every pointer sample is still on the curve, moved so the mark starts at its top-left corner.
            var at = i * SignatureStrokes.Subdivisions * Coordinates;
            await Assert.That(Math.Abs(points[at] - (Zigzag[i].X - ZigzagLeft))).IsLessThan(Tolerance);
            await Assert.That(Math.Abs(points[at + 1] - (Zigzag[i].Y - ZigzagTop))).IsLessThan(Tolerance);
        }

        await Assert.That(mark.IsValid).IsTrue();
    }

    /// <summary>A tap is kept as a dot, and empty or mismatched strokes are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawnTapBecomesADot()
    {
        var mark = await Assert.That(SignatureMark.Drawn(SignatureMarkKind.Signature, Tap, [Tap.Length])).IsNotNull();

        await Assert.That(mark!.StrokeLengths.ToArray()).IsEquivalentTo([DotPoints]);
        await Assert.That(mark.IsValid).IsTrue();
        await Assert.That(SignatureMark.Drawn(SignatureMarkKind.Signature, [], [])).IsNull();
        await Assert.That(static () => SignatureMark.Drawn(SignatureMarkKind.Signature, Zigzag, [1])).Throws<ArgumentException>();
    }

    /// <summary>A picture mark keeps its pixels and size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImageMarkKeepsPixels()
    {
        var image = SignatureImage.Create(Ink, PictureWidth, 1, false)!;
        var mark = SignatureMark.FromImage(SignatureMarkKind.Signature, image);

        await Assert.That(mark.Style).IsEqualTo(SignatureMarkStyle.Image);
        await Assert.That(mark.Pixels.ToArray()).IsEquivalentTo(Ink);
        await Assert.That(mark.AspectRatio).IsEqualTo((float)PictureWidth);
        await Assert.That(mark.IsValid).IsTrue();
        await Assert.That((mark with { Width = PictureWidth + 1 }).IsValid).IsFalse();
    }

    /// <summary>Smoothing copies strokes too short to curve, and refuses a destination that is too small.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmoothingHandlesShortStrokes()
    {
        var copy = new PagePoint[SignatureStrokes.SmoothedLength(Line.Length)];
        SignatureStrokes.Smooth(Line, copy);

        await Assert.That(copy).IsEquivalentTo(Line);
        await Assert.That(static () => SignatureStrokes.Smooth(Zigzag, new PagePoint[Zigzag.Length])).Throws<ArgumentException>();
    }
}
