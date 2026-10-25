// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets.Shaping;

namespace HyperPdfLibrary.Tests.Fonts.Shaping;

/// <summary>Indic syllables: reordering of vowel signs and rephs, and the font's own lookups for reph, half and conjunct forms.</summary>
public sealed class IndicShapingTests
{
    /// <summary>The lookup type of a ligature substitution.</summary>
    private const int Ligature = 0x4;

    /// <summary>Devanagari ka and the vowel sign i.</summary>
    private const string KaI = "कि";

    /// <summary>Devanagari ra, virama and ka.</summary>
    private const string RaHalantKa = "र्क";

    /// <summary>A vowel sign written before its consonant moves in front of it, and the syllable shares one cluster.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VowelSignIMovesBeforeTheConsonant()
    {
        var shaped = Fixture(true, true, true).Shape(KaI);

        await Assert.That(shaped.Letters).IsEqualTo("ik");
        await Assert.That(shaped.Clusters).IsEqualTo("0,0");
    }

    /// <summary>A ra and virama before a consonant form a reph, which is written after the syllable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RaAndViramaFormARephAfterTheSyllable() => await Assert.That(Fixture(true, true, true).Shape(RaHalantKa).Letters).IsEqualTo("kR");

    /// <summary>With a vowel sign the reph still ends the syllable, and the sign is in front.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RephAndVowelSignAreBothMoved() => await Assert.That(Fixture(true, true, true).Shape($"{RaHalantKa}ि").Letters).IsEqualTo("ikR");

    /// <summary>The reph goes in front of a vowel modifier that ends the syllable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RephComesBeforeAVowelModifier() => await Assert.That(Fixture(true, true, true).Shape($"{RaHalantKa}ं").Letters).IsEqualTo("kRn");

    /// <summary>The akhand ligature of ka, virama and ssa forms a conjunct.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConjunctFormsFromTheFontsLigature() => await Assert.That(Fixture(true, true, true).Shape("क्ष").Letters).IsEqualTo("K");

    /// <summary>A consonant before the base takes its half form, and one at the end does not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyConsonantsBeforeTheBaseTakeHalfForms()
    {
        var fixture = Fixture(true, true, true);

        await Assert.That(fixture.Shape("क्त").Letters).IsEqualTo("Ht");
        await Assert.That(fixture.Shape("क्").Letters).IsEqualTo("kh");
    }

    /// <summary>When the font has no reph form the ra stays where it is, and the vowel sign goes in front of the whole syllable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WithoutARephFormTheVowelSignGoesToTheFront() => await Assert.That(Fixture(false, false, false).Shape($"{RaHalantKa}ि").Letters).IsEqualTo("irhk");

    /// <summary>A two-part vowel sign the font has no glyph for is written as its two parts, and the first part moves in front.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TwoPartVowelSignIsSplit()
    {
        var fixture = new ShapingFixture();
        _ = fixture.AddMapped('k', 0x0995, ShapingFixture.Advance);
        _ = fixture.AddMapped('e', 0x09C7, ShapingFixture.Advance);
        _ = fixture.AddMapped('a', 0x09BE, ShapingFixture.Advance);

        var shaped = fixture.Shape("কো");

        await Assert.That(shaped.Letters).IsEqualTo("eka");
        await Assert.That(shaped.Clusters).IsEqualTo("0,0,0");
    }

    /// <summary>Syllables are reordered one at a time, and text between them is left alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SyllablesAreReorderedSeparately()
    {
        var fixture = Fixture(false, false, false);
        _ = fixture.Add(' ');

        await Assert.That(fixture.Shape($"{KaI} {KaI}").Letters).IsEqualTo("ik ik");
    }

    /// <summary>Makes a Devanagari font; the lookups for reph, half and akhand forms are optional.</summary>
    /// <param name="reph">Whether the font has a reph form.</param>
    /// <param name="half">Whether the font has a half form of ka.</param>
    /// <param name="conjunct">Whether the font has the akhand ligature.</param>
    /// <returns>The fixture.</returns>
    internal static ShapingFixture Fixture(bool reph, bool half, bool conjunct)
    {
        var fixture = new ShapingFixture();
        _ = fixture.AddMapped('k', 0x0915, ShapingFixture.Advance);
        _ = fixture.AddMapped('s', 0x0937, ShapingFixture.Advance);
        _ = fixture.AddMapped('t', 0x0924, ShapingFixture.Advance);
        _ = fixture.AddMapped('r', 0x0930, ShapingFixture.Advance);
        _ = fixture.AddMapped('h', 0x094D, ShapingFixture.Advance);
        _ = fixture.AddMapped('i', 0x093F, ShapingFixture.Advance);
        _ = fixture.AddMapped('n', 0x0902, ShapingFixture.Advance);
        foreach (var form in "RHK")
        {
            _ = fixture.AddUnmapped(form);
        }

        var features = new List<int>();
        if (reph)
        {
            var lookup = fixture.Gsub.AddLookup(Ligature, 0, GsubSubtables.Ligature(fixture.Id('r'), new LigatureRule(fixture.Id('R'), [fixture.Id('h')])));
            features.Add(fixture.Gsub.AddFeature("rphf", lookup));
        }

        if (conjunct)
        {
            var lookup = fixture.Gsub.AddLookup(Ligature, 0, GsubSubtables.Ligature(fixture.Id('k'), new LigatureRule(fixture.Id('K'), fixture.Ids("hs"))));
            features.Add(fixture.Gsub.AddFeature("akhn", lookup));
        }

        if (half)
        {
            var lookup = fixture.Gsub.AddLookup(Ligature, 0, GsubSubtables.Ligature(fixture.Id('k'), new LigatureRule(fixture.Id('H'), [fixture.Id('h')])));
            features.Add(fixture.Gsub.AddFeature("half", lookup));
        }

        if (features.Count > 0)
        {
            fixture.Gsub.AddScript("dev2", [.. features]);
        }

        return fixture;
    }
}
