// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks resource reuse within one service and isolation between services.</summary>
public sealed class FontResourceIsolationTests
{
    /// <summary>The predefined CMap shared on disk but parsed per owner.</summary>
    private const string CMapName = "90ms-RKSJ-H";

    /// <summary>Gets the predefined CMap name as bytes.</summary>
    private static ReadOnlySpan<byte> CMapNameBytes => "90ms-RKSJ-H"u8;

    /// <summary>Gets a one-character glyph name in the cached punctuation block.</summary>
    private static ReadOnlySpan<byte> BulletName => "bullet"u8;

    /// <summary>Parsed face and CMap caches belong to the selected resource owner.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParsedFontResourcesStayWithTheirOwner()
    {
        var leftServices = new PdfDrawingServices(new());
        var rightServices = new PdfDrawingServices(new());
        var left = leftServices.FontResources;
        var right = rightServices.FontResources;
        await BundledFaces.EnsureAsync(left, BundledFamily.Sans, false, false, CancellationToken.None);
        await BundledFaces.EnsureAsync(right, BundledFamily.Sans, false, false, CancellationToken.None);
        var leftFace = BundledFaces.Get(left, BundledFamily.Sans, false, false);
        var rightFace = BundledFaces.Get(right, BundledFamily.Sans, false, false);

        await Assert.That(leftFace).IsNotNull();
        await Assert.That(rightFace).IsNotNull();
        await Assert.That(ReferenceEquals(leftFace, BundledFaces.Get(left, BundledFamily.Sans, false, false))).IsTrue();
        await Assert.That(ReferenceEquals(leftFace, rightFace)).IsFalse();
        await Assert.That(ReferenceEquals(left, leftServices.FontResources)).IsTrue();
        await Assert.That(ReferenceEquals(left, right)).IsFalse();

        await PredefinedCMaps.EnsureAsync(left, CMapName, CancellationToken.None);
        await PredefinedCMaps.EnsureAsync(right, CMapName, CancellationToken.None);
        var leftMap = PredefinedCMaps.Get(left, CMapNameBytes);
        var rightMap = PredefinedCMaps.Get(right, CMapNameBytes);
        await Assert.That(ReferenceEquals(leftMap, PredefinedCMaps.Get(left, CMapNameBytes))).IsTrue();
        await Assert.That(ReferenceEquals(leftMap, rightMap)).IsFalse();

        await CidToUnicodeTable.EnsureAsync(left, CjkScript.Japanese, CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(right, CjkScript.Japanese, CancellationToken.None);
        var leftUnicode = CidToUnicodeTable.Get(left, CjkScript.Japanese);
        var rightUnicode = CidToUnicodeTable.Get(right, CjkScript.Japanese);
        await Assert.That(leftUnicode).IsNotNull();
        await Assert.That(rightUnicode).IsNotNull();
        await Assert.That(ReferenceEquals(leftUnicode, rightUnicode)).IsFalse();
    }

    /// <summary>One-character glyph names are cached within the selected owner.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GlyphNameStringsStayWithTheirOwner()
    {
        var left = new PdfFontResources();
        var right = new PdfFontResources();
        var leftText = GlyphNameText.TextOf(left, BulletName, false);
        var rightText = GlyphNameText.TextOf(right, BulletName, false);

        await Assert.That(leftText).IsEqualTo("•");
        await Assert.That(ReferenceEquals(leftText, GlyphNameText.TextOf(left, BulletName, false))).IsTrue();
        await Assert.That(ReferenceEquals(leftText, rightText)).IsFalse();
    }

    /// <summary>A detached font reuses the matcher held by its selected resource owner.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DetachedMatcherUsesSelectedResources()
    {
        var dictionary = new PdfDictionary(null);
        var left = new PdfFontResources();
        var right = new PdfFontResources();
        var leftMatcher = SystemFontMatcher.For(dictionary, left);

        await Assert.That(ReferenceEquals(leftMatcher, SystemFontMatcher.For(dictionary, left))).IsTrue();
        await Assert.That(ReferenceEquals(leftMatcher, SystemFontMatcher.For(dictionary, right))).IsFalse();
    }

    /// <summary>Observing a resource file advances only the selected owner's generation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AvailabilityGenerationStaysWithItsOwner()
    {
        using var cache = new FontDataTestCache();
        var left = new PdfFontResources();
        var right = new PdfFontResources();
        await FontDataResources.EnsureAsync(left.Data, cache.DirectoryPath, "CMaps", "isolation.bin", static _ => ValueTask.FromResult<byte[]>([1]), CancellationToken.None);

        await Assert.That(left.Generation).IsEqualTo(1);
        await Assert.That(right.Generation).IsEqualTo(0);

        await FontDataResources.EnsureAsync(
            right.Data,
            cache.DirectoryPath,
            "CMaps",
            "isolation.bin",
            static _ => throw new InvalidOperationException("The file already exists."),
            CancellationToken.None);
        await Assert.That(right.Generation).IsEqualTo(1);
    }
}
