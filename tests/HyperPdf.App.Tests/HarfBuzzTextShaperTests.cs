// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using HyperPdf.Skia;
using HyperPdf.Skia.Fonts;
using SkiaSharp;

namespace HyperPdf.App.Tests;

/// <summary>Checks line breaks and text slices in the OpenType shaper.</summary>
public sealed class HarfBuzzTextShaperTests
{
    /// <summary>Verifies that CRLF shares a cluster and has no visible advance.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ShapeText_Crlf_HasZeroAdvance()
    {
        const int fontSize = 16;
        var typeface = new GlyphTypeface(new SkiaTypeface(SKTypeface.FromFamilyName(null), FontSimulations.None));
        using var lifetime = DisposeAction.Create(typeface.Dispose);
        using var shaped = new HarfBuzzTextShaper().ShapeText("\r\n".AsMemory(), new(typeface, fontSize));
        await Assert.That(shaped.Sum(static glyph => glyph.GlyphAdvance)).IsEqualTo(0);
        await Assert.That(shaped.All(static glyph => glyph.GlyphCluster == 0)).IsTrue();
    }

    /// <summary>Verifies that glyph clusters remain relative to a text slice.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ShapeText_StringSlice_UsesRelativeClusters()
    {
        const int fontSize = 16;
        const int sliceStart = 3;
        const int sliceLength = 2;
        var typeface = new GlyphTypeface(new SkiaTypeface(SKTypeface.FromFamilyName(null), FontSimulations.None));
        using var lifetime = DisposeAction.Create(typeface.Dispose);
        using var shaped = new HarfBuzzTextShaper().ShapeText("___ab___".AsMemory(sliceStart, sliceLength), new(typeface, fontSize));
        await Assert.That(shaped[0].GlyphCluster).IsEqualTo(0);
        await Assert.That(shaped[1].GlyphCluster).IsEqualTo(1);
    }
}
