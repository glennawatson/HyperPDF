// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Media;
using HyperPdf.Engine;
using HyperPdf.Skia;
using HyperPdf.Skia.Fonts;

namespace HyperPdf.App.Tests;

/// <summary>Checks that the font manager serves the fonts the app carries itself, whatever fonts the system has.</summary>
public sealed class BundledFontManagerTests
{
    /// <summary>The weight from which a face counts as bold.</summary>
    private const int BoldWeight = 700;

    /// <summary>Each of the app's families is found by name, without any matching system font.</summary>
    /// <param name="family">The family.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Arimo")]
    [Arguments("Tinos")]
    [Arguments("Cousine")]
    [Arguments("arimo")]
    public async Task BundledFamilyIsFound(string family)
    {
        var found = Make().TryCreateGlyphTypeface(family, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out var typeface);

        await Assert.That(found).IsTrue();
        await Assert.That(typeface!.FamilyName).IsEqualTo(BundledScreenFonts.Families.First(name => string.Equals(name, family, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Bold and italic ask for the face of that style, not a simulation of the regular one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BoldItalicUsesTheRealFace()
    {
        var found = Make().TryCreateGlyphTypeface("Arimo", FontStyle.Italic, (FontWeight)BoldWeight, FontStretch.Normal, out var typeface);

        await Assert.That(found).IsTrue();
        await Assert.That(typeface!.Weight).IsEqualTo(FontWeight.Bold);
        await Assert.That(typeface.Style).IsEqualTo(FontStyle.Italic);
        await Assert.That(typeface.FontSimulations).IsEqualTo(FontSimulations.None);
    }

    /// <summary>Asking twice gives the same shared typeface, so the font is loaded once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheTypefaceIsLoadedOnce()
    {
        var fonts = Make();
        _ = fonts.TryCreateGlyphTypeface("Tinos", FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out var first);
        _ = fonts.TryCreateGlyphTypeface("Tinos", FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out var second);

        await Assert.That(ReferenceEquals(((SkiaTypeface)first!).Typeface, ((SkiaTypeface)second!).Typeface)).IsTrue();
        await Assert.That(((SkiaTypeface)first).IsShared).IsTrue();
    }

    /// <summary>Makes a font manager that knows the app's own fonts.</summary>
    /// <returns>The font manager.</returns>
    private static FontManagerImpl Make() => new(new SkiaOptions { BundledFamilies = BundledScreenFonts.Families, BundledFontReader = BundledScreenFonts.Read });
}
