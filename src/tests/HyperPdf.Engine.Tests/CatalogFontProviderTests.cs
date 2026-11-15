// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Text.Fonts;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests finding installed fonts for text whose font is not embedded, without a native font library.</summary>
public sealed class CatalogFontProviderTests
{
    /// <summary>The letter looked up in each face.</summary>
    private const int Letter = 'A';

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
        if (catalog.Faces.Count == 0)
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
}
