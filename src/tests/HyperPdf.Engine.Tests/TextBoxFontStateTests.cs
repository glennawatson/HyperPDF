// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Text.Fonts;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks annotation font caches follow the viewer engine's lifetime.</summary>
public sealed class TextBoxFontStateTests
{
    /// <summary>The built-in face used by this test.</summary>
    private const string Helvetica = "Helvetica";

    /// <summary>One engine shares its font cache while separate engines keep independent caches.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontStateFollowsEngineInstance()
    {
        var bytes = TestPdf.Create(1);
        var shared = new HyperPdfEngine();
        using var first = new HyperPdfTestDocument(bytes, shared);
        using var second = new HyperPdfTestDocument(bytes, shared);
        using var separate = new HyperPdfTestDocument(bytes, new HyperPdfEngine());

        await Assert.That(ReferenceEquals(first.Editor.FontState, second.Editor.FontState)).IsTrue();
        await Assert.That(ReferenceEquals(first.Editor.FontState, separate.Editor.FontState)).IsFalse();

        var sharedShaper = StandardTextShaper.For(first.Editor.FontState, Helvetica, false, false);
        var reusedShaper = StandardTextShaper.For(second.Editor.FontState, Helvetica, false, false);
        var separateShaper = StandardTextShaper.For(separate.Editor.FontState, Helvetica, false, false);
        await Assert.That(ReferenceEquals(sharedShaper, reusedShaper)).IsTrue();
        await Assert.That(ReferenceEquals(sharedShaper, separateShaper)).IsFalse();
    }

    /// <summary>An engine uses the host's registered catalog for its documents.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontCatalogFollowsEngineRegistration()
    {
        var catalog = new Lazy<FontCatalog>(static () => FontCatalog.Empty);
        using var document = new HyperPdfTestDocument(TestPdf.Create(1), HyperPdfEngine.WithFontCatalog(catalog));

        await Assert.That(HyperPdfAnnotationReading.GetFontCatalog(document.Editor)).IsSameReferenceAs(FontCatalog.Empty);
    }
}
