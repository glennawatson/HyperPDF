// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks that bundled face programs and code maps stay within their resource owner.</summary>
public sealed class SubstituteGlyphMapCacheTests
{
    /// <summary>The code of the letter A.</summary>
    private const int CodeA = 'A';

    /// <summary>The code of the letter B.</summary>
    private const int CodeB = 'B';

    /// <summary>The code of the e acute in Windows ANSI.</summary>
    private const int CodeEAcute = 0xE9;

    /// <summary>Each resource owner keeps its own code maps, distinct from other owners and faces.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResourceSetsOwnTheStoreOfAProgram()
    {
        var left = new PdfDrawingServices(new()).FontResources;
        var right = new PdfDrawingServices(new()).FontResources;

        var leftSans = BundledFaces.Get(left, BundledFamily.Sans, false, false)!;
        var rightSans = BundledFaces.Get(right, BundledFamily.Sans, false, false)!;
        var serif = BundledFaces.Get(left, BundledFamily.Serif, false, false)!;
        var bold = BundledFaces.Get(left, BundledFamily.Sans, true, false)!;

        await Assert.That(ReferenceEquals(leftSans, rightSans)).IsFalse();
        await Assert.That(ReferenceEquals(leftSans.Maps, rightSans.Maps)).IsFalse();
        await Assert.That(ReferenceEquals(leftSans.Maps, BundledFaces.Get(left, BundledFamily.Sans, false, false)!.Maps)).IsTrue();
        await Assert.That(ReferenceEquals(leftSans.Maps, serif.Maps)).IsFalse();
        await Assert.That(ReferenceEquals(leftSans.Maps, bold.Maps)).IsFalse();
    }

    /// <summary>A map copied from the store equals one resolved from scratch, for every face and base encoding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StoredMapsEqualMapsResolvedFromScratch()
    {
        var resources = new PdfDrawingServices(new()).FontResources;
        FontEncoding[] bases = [FontEncoding.Standard, FontEncoding.WinAnsi, FontEncoding.MacRoman, FontEncoding.None];

        foreach (var family in new[] { BundledFamily.Sans, BundledFamily.Serif, BundledFamily.Fixed })
        {
            var face = BundledFaces.Get(resources, family, false, false)!;
            foreach (var baseEncoding in bases)
            {
                var plain = new SimpleEncoding(baseEncoding, null);
                var first = SubstituteGlyphMapper.Map(resources, face, plain, FontEncoding.None, false, null);
                var second = SubstituteGlyphMapper.Map(resources, face, plain, FontEncoding.None, false, null);
                var scratch = SubstituteGlyphMapper.Map(resources, face, new(baseEncoding, new byte[]?[FontEncodings.CodeCount]), FontEncoding.None, false, null);

                await Assert.That(second.Glyphs).IsEquivalentTo(scratch.Glyphs);
                await Assert.That(second.Texts).IsEquivalentTo(scratch.Texts);
                await Assert.That(first.Glyphs).IsEquivalentTo(scratch.Glyphs);
                await Assert.That(first.Texts).IsEquivalentTo(scratch.Texts);
            }
        }
    }

    /// <summary>Changing a returned map leaves the store, and the next map, untouched.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangingAMapDoesNotChangeTheNextOne()
    {
        var resources = new PdfDrawingServices(new()).FontResources;
        var face = BundledFaces.Get(resources, BundledFamily.Sans, false, false)!;
        var encoding = new SimpleEncoding(FontEncoding.WinAnsi, null);
        var expected = SubstituteGlyphMapper.Map(resources, face, encoding, FontEncoding.None, false, null);
        var glyphA = expected.Glyphs[CodeA];

        var changed = SubstituteGlyphMapper.Map(resources, face, encoding, FontEncoding.None, false, null);
        changed.Glyphs[CodeA] = SimpleGlyphMap.NoGlyph;
        changed.Texts[CodeA] = "changed";
        var next = SubstituteGlyphMapper.Map(resources, face, encoding, FontEncoding.None, false, null);

        await Assert.That(glyphA).IsNotEqualTo(SimpleGlyphMap.NoGlyph);
        await Assert.That(next.Glyphs[CodeA]).IsEqualTo(glyphA);
        await Assert.That(next.Texts[CodeA]).IsEqualTo("A");
        await Assert.That(ReferenceEquals(changed.Glyphs, next.Glyphs)).IsFalse();
    }

    /// <summary>The cached map of one base encoding is not handed to another, nor to a symbolic request.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DifferentRequestsKeepDifferentMaps()
    {
        var resources = new PdfDrawingServices(new()).FontResources;
        var face = BundledFaces.Get(resources, BundledFamily.Sans, false, false)!;

        var winAnsi = SubstituteGlyphMapper.Map(resources, face, new(FontEncoding.WinAnsi, null), FontEncoding.None, false, null);
        var macRoman = SubstituteGlyphMapper.Map(resources, face, new(FontEncoding.MacRoman, null), FontEncoding.None, false, null);
        var winAnsiAgain = SubstituteGlyphMapper.Map(resources, face, new(FontEncoding.WinAnsi, null), FontEncoding.None, false, null);
        var symbolic = SubstituteGlyphMapper.Map(resources, face, new(FontEncoding.None, null), FontEncoding.None, true, null);
        var nonsymbolic = SubstituteGlyphMapper.Map(resources, face, new(FontEncoding.None, null), FontEncoding.None, false, null);

        await Assert.That(winAnsi.Texts[CodeEAcute]).IsEqualTo("é");
        await Assert.That(macRoman.Texts[CodeEAcute]).IsNotEqualTo("é");
        await Assert.That(winAnsiAgain.Texts[CodeEAcute]).IsEqualTo("é");
        await Assert.That(winAnsiAgain.Glyphs).IsEquivalentTo(winAnsi.Glyphs);

        var blank = new byte[]?[FontEncodings.CodeCount];
        var scratchSymbolic = SubstituteGlyphMapper.Map(resources, face, new(FontEncoding.None, blank), FontEncoding.None, true, null);
        var scratchNonsymbolic = SubstituteGlyphMapper.Map(resources, face, new(FontEncoding.None, blank), FontEncoding.None, false, null);
        await Assert.That(symbolic.Glyphs).IsEquivalentTo(scratchSymbolic.Glyphs);
        await Assert.That(nonsymbolic.Glyphs).IsEquivalentTo(scratchNonsymbolic.Glyphs);
    }

    /// <summary>A map with /Differences is built for that font only and never enters the store.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DifferencesAreNotCached()
    {
        var resources = new PdfDrawingServices(new()).FontResources;
        var face = BundledFaces.Get(resources, BundledFamily.Sans, false, false)!;
        var differences = new byte[]?[FontEncodings.CodeCount];
        differences[CodeA] = "B"u8.ToArray();

        var plainBefore = SubstituteGlyphMapper.Map(resources, face, new(FontEncoding.WinAnsi, null), FontEncoding.None, false, null);
        var changed = SubstituteGlyphMapper.Map(resources, face, new(FontEncoding.WinAnsi, differences), FontEncoding.None, false, null);
        var plainAfter = SubstituteGlyphMapper.Map(resources, face, new(FontEncoding.WinAnsi, null), FontEncoding.None, false, null);

        await Assert.That(changed.Glyphs[CodeA]).IsEqualTo(plainBefore.Glyphs[CodeB]);
        await Assert.That(changed.Texts[CodeA]).IsEqualTo("B");
        await Assert.That(plainAfter.Glyphs[CodeA]).IsEqualTo(plainBefore.Glyphs[CodeA]);
        await Assert.That(plainAfter.Texts[CodeA]).IsEqualTo("A");
    }

    /// <summary>Maps requested from many threads at once equal the map built from scratch.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentRequestsAgree()
    {
        const int Requests = 64;
        var resources = new PdfDrawingServices(new()).FontResources;
        var face = BundledFaces.Get(resources, BundledFamily.Serif, true, false)!;
        var encoding = new SimpleEncoding(FontEncoding.WinAnsi, null);
        var scratch = SubstituteGlyphMapper.Map(resources, face, new(FontEncoding.WinAnsi, new byte[]?[FontEncodings.CodeCount]), FontEncoding.None, false, null);
        var results = new SimpleGlyphMap[Requests];

        await Parallel.ForAsync(0, Requests, (i, _) =>
        {
            results[i] = SubstituteGlyphMapper.Map(resources, face, encoding, FontEncoding.None, false, null);
            return ValueTask.CompletedTask;
        });

        foreach (var result in results)
        {
            await Assert.That(result.Glyphs).IsEquivalentTo(scratch.Glyphs);
            await Assert.That(result.Texts).IsEquivalentTo(scratch.Texts);
        }
    }
}
