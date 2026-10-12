// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Tests.Annotations;

/// <summary>Tests moving and resizing a signature mark while it is placed.</summary>
public sealed class SignatureMarkLayoutTests
{
    /// <summary>How close two sizes must be.</summary>
    private const float Tolerance = 0.01F;

    /// <summary>Half, to find a centre.</summary>
    private const float Half = 0.5F;

    /// <summary>A push far past every edge of the page.</summary>
    private const float FarAway = 5000;

    /// <summary>A scale small enough to hit the smallest size.</summary>
    private const float Tiny = 0.001F;

    /// <summary>A scale large enough to hit the largest size.</summary>
    private const float Huge = 1000;

    /// <summary>The characters in a very long typed name.</summary>
    private const int LongName = 200;

    /// <summary>The text size that fills a 25 point mark.</summary>
    private const double TextSizeFor25 = 20;

    /// <summary>The height of the mark the text size is found for.</summary>
    private const double TypedHeight = 25;

    /// <summary>A US Letter page.</summary>
    private static readonly PageSize Page = PageSize.Letter;

    /// <summary>A mark on the page.</summary>
    private static readonly PageRect Mark = new(100, 100, 50, 20);

    /// <summary>The mark after one step right and one fine step up.</summary>
    private static readonly PageRect Stepped = new(110, 99, 50, 20);

    /// <summary>A larger mark to resize.</summary>
    private static readonly PageRect Box = new(200, 300, 100, 40);

    /// <summary>The larger mark after one resize step.</summary>
    private static readonly PageRect Grown = new(187.5F, 295, 125, 50);

    /// <summary>A drawn line from corner to corner.</summary>
    private static readonly PagePoint[] Diagonal = [new(0, 0), new(10, 5)];

    /// <summary>Where the drawn line goes on the page.</summary>
    private static readonly PageRect Target = new(100, 200, 40, 20);

    /// <summary>The line's ends on the page.</summary>
    private static readonly PagePoint[] Mapped = [new(100, 200), new(140, 220)];

    /// <summary>A signature starts half an inch tall and initials a third of an inch, both centred on the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StartsCentredAtTheUsualHeight()
    {
        var signature = SignatureMarkLayout.Start(SignatureMark.Typed(SignatureMarkKind.Signature, "Glenn Watson")!, Page);
        var initials = SignatureMarkLayout.Start(SignatureMark.Typed(SignatureMarkKind.Initials, "GW")!, Page);

        await Assert.That(signature.Height).IsEqualTo(SignatureMarkLayout.SignatureHeight);
        await Assert.That(initials.Height).IsEqualTo(SignatureMarkLayout.InitialsHeight);
        await Assert.That(Math.Abs(signature.Left + (signature.Width * Half) - (Page.Width * Half))).IsLessThan(Tolerance);
        await Assert.That(Math.Abs(signature.Top + (signature.Height * Half) - (Page.Height * Half))).IsLessThan(Tolerance);
    }

    /// <summary>A very wide mark starts narrower than the page and keeps its shape.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WideMarkStartsInsideThePage()
    {
        var wide = SignatureMark.Typed(SignatureMarkKind.Signature, new('W', LongName))!;
        var bounds = SignatureMarkLayout.Start(wide, Page);

        await Assert.That(bounds.Width).IsLessThan(Page.Width);
        await Assert.That(Math.Abs((bounds.Width / bounds.Height) - wide.AspectRatio)).IsLessThan(Tolerance);
    }

    /// <summary>Moving stays on the page however far the mark is pushed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovingStaysOnThePage()
    {
        var moved = SignatureMarkLayout.Move(Mark, SignatureMarkLayout.MoveStep, -SignatureMarkLayout.FineMoveStep, Page);
        var pushed = SignatureMarkLayout.Move(Mark, -FarAway, FarAway, Page);

        await Assert.That(moved).IsEqualTo(Stepped);
        await Assert.That(pushed).IsEqualTo(Mark with { Left = 0, Top = Page.Height - Mark.Height });
    }

    /// <summary>Resizing keeps the mark's shape and centre and stops at sensible limits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResizingKeepsShapeAndCentre()
    {
        var bigger = SignatureMarkLayout.Resize(Box, SignatureMarkLayout.ResizeStep, Page);
        var tiny = SignatureMarkLayout.Resize(Box, Tiny, Page);
        var huge = SignatureMarkLayout.Resize(Box, Huge, Page);

        await Assert.That(bigger).IsEqualTo(Grown);
        await Assert.That(tiny.Height).IsEqualTo(SignatureMarkLayout.MinHeight);
        await Assert.That(Math.Abs((tiny.Width / tiny.Height) - (Box.Width / Box.Height))).IsLessThan(Tolerance);
        await Assert.That(huge.Width).IsLessThanOrEqualTo(Page.Width);
        await Assert.That(huge.Left).IsGreaterThanOrEqualTo(0F);
    }

    /// <summary>A drawn mark's points are scaled into its bounds on the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MapsDrawnPointsIntoBounds()
    {
        var mark = SignatureMark.Drawn(SignatureMarkKind.Signature, Diagonal, [Diagonal.Length])!;
        var points = new PagePoint[Diagonal.Length];

        SignatureMarkLayout.MapPoints(mark, Target, points);

        await Assert.That(points).IsEquivalentTo(Mapped);
        await Assert.That(SignatureMarkLayout.TypedFontSize(TypedHeight)).IsEqualTo(TextSizeFor25);
        await Assert.That(() => SignatureMarkLayout.MapPoints(mark, Target, new PagePoint[1])).Throws<ArgumentException>();
    }
}
