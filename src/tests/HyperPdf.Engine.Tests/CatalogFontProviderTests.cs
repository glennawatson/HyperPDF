// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Text;
using HyperPdf.Core.Text.Fonts;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests finding installed fonts for text whose font is not embedded, without a native font library.</summary>
public sealed class CatalogFontProviderTests
{
    /// <summary>The letter looked up in each face.</summary>
    private const int Letter = 'A';

    /// <summary>A Khmer letter, which no installed font is assumed to have.</summary>
    private const int KhmerLetter = 0x1780;

    /// <summary>A Han character, for which the app bundles no font.</summary>
    private const int HanLetter = 0x4E2D;

    /// <summary>A short Khmer word.</summary>
    private const string KhmerWord = "\u1780\u17D2\u179A\u17C1";

    /// <summary>The default face is the bundled one, and it draws a letter with an outline and a width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultFaceDrawsALetter()
    {
        var provider = new CatalogFontProvider(new(static () => FontCatalog.Empty));
        using var face = provider.DefaultFace;
        var glyph = face.GetGlyph(Letter);
        var outline = face.BuildOutline(glyph);

        await Assert.That(glyph).IsGreaterThan(0);
        await Assert.That(face.GetAdvance(glyph)).IsGreaterThan(0F);
        await Assert.That(face.Ascent).IsGreaterThan(0F);
        await Assert.That(outline is { IsEmpty: false }).IsTrue();
    }

    /// <summary>An installed family is found by name, and a family that is not installed gives no face.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InstalledFamilyIsFoundByName()
    {
        var catalog = FontCatalog.Scan(FontCatalog.SystemDirectories());
        if (catalog.Faces.Length == 0)
        {
            Skip.Test("This machine has no installed fonts to look up.");
        }

        var provider = new CatalogFontProvider(new(() => catalog));

        // The family of a real font file: the catalog's family list also names the standard PDF families, which have no file.
        using var found = provider.Match(catalog.Faces[0].Family, 0, false);
        using var missing = provider.Match("No Such Family 4f1c", 0, false);

        await Assert.That(found).IsNotNull();
        await Assert.That(found!.GlyphCount).IsGreaterThan(0);
        await Assert.That(missing).IsNull();
    }

    /// <summary>With no installed fonts, a character of a script the app bundles a font for still gets a face, and one it does not has none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BundledScriptFontStandsInWhenNoInstalledFontHasTheCharacter()
    {
        var provider = new CatalogFontProvider(new(static () => FontCatalog.Empty));

        using var khmer = provider.MatchCharacter(KhmerLetter);
        var han = provider.MatchCharacter(HanLetter);

        await Assert.That(khmer).IsNotNull();
        await Assert.That(khmer!.GetGlyph(KhmerLetter)).IsGreaterThan(0);
        await Assert.That(han).IsNull();
    }

    /// <summary>With no installed fonts, text box text in a bundled script gets that script's font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextBoxTextInABundledScriptGetsItsFont()
    {
        var state = new TextBoxFontState(new(static () => FontCatalog.Empty));

        var font = TextBoxFonts.Resolve(state, state.Catalog, TextFormat.Default, KhmerWord);

        await Assert.That(font?.Program).IsNotNull();
        await Assert.That(font!.Program!.GlyphFor(KhmerLetter)).IsGreaterThan((ushort)0);
    }
}
