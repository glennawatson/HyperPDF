// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Media;
using HyperPdf.Skia;
using HyperPdf.Skia.Fonts;
using HyperPdf.Skia.Rendering;
using HyperPdf.TestAssets;
using SkiaSharp;

namespace HyperPdf.App.Tests;

/// <summary>Checks managed allocation costs within a drawing session.</summary>
public sealed class DrawingContextImplTests
{
    /// <summary>Verifies that warmed solid and small-gradient rectangle fills allocate no managed memory.</summary>
    /// <param name="gradient">Whether to use a two-stop linear gradient.</param>
    /// <param name="rounded">Whether to round the corners.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task DrawRectangle_RetainedSession_AllocatesNoManagedMemory(bool gradient, bool rounded)
    {
        const int size = 32;
        const int repetitions = 1000;
        const int cornerRadius = 4;
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        using var context = new DrawingContextImpl(new DrawingContextCreateInfo { Surface = surface, Dpi = SkiaPlatform.DefaultDpi });
        IBrush brush = gradient ? new LinearGradientBrush { GradientStops = [new(Colors.Red, 0), new(Colors.Blue, 1)] } : Brushes.Blue;
        var bounds = new RoundedRect(new Rect(0, 0, size, size), rounded ? cornerRadius : 0);
        for (var i = 0; i < repetitions; i++)
        {
            context.DrawRectangle(brush, null, bounds);
        }

        var allocated = AllocationProbe.Minimum(() =>
        {
            for (var i = 0; i < repetitions; i++)
            {
                context.DrawRectangle(brush, null, bounds);
            }
        });
        await Assert.That(allocated).IsEqualTo(0);
    }

    /// <summary>Verifies that drawing a warmed glyph run allocates no managed memory.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DrawGlyphRun_RetainedSession_AllocatesNoManagedMemory()
    {
        const int size = 128;
        const int fontSize = 16;
        const int repetitions = 1000;
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        using var context = new DrawingContextImpl(new DrawingContextCreateInfo { Surface = surface, Dpi = SkiaPlatform.DefaultDpi });
        var typeface = new GlyphTypeface(new SkiaTypeface(SKTypeface.FromFamilyName(null), FontSimulations.None));
        using var lifetime = DisposeAction.Create(typeface.Dispose);
        using var shaper = new HarfBuzzTextShaper();
        using var shaped = shaper.ShapeText("A test".AsMemory(), new(typeface, fontSize));
        using var run = new GlyphRunImpl(typeface, fontSize, shaped, new(0, fontSize));
        for (var i = 0; i < repetitions; i++)
        {
            context.DrawGlyphRun(Brushes.Black, run);
        }

        var allocated = AllocationProbe.Minimum(() =>
        {
            for (var i = 0; i < repetitions; i++)
            {
                context.DrawGlyphRun(Brushes.Black, run);
            }
        });
        await Assert.That(allocated).IsEqualTo(0);
    }
}
