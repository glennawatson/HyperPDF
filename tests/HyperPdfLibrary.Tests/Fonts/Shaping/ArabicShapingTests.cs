// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets.Shaping;

namespace HyperPdfLibrary.Tests.Fonts.Shaping;

/// <summary>Arabic joining: each letter takes its isolated, initial, medial or final form from its neighbours, and the font's features then substitute the forms.</summary>
public sealed class ArabicShapingTests
{
    /// <summary>The lookup type of a single substitution.</summary>
    private const int Single = 1;

    /// <summary>The lookup type of a ligature substitution.</summary>
    private const int Ligature = 0x4;

    /// <summary>Text of beh, beh and alef.</summary>
    private const string BehBehAlef = "ببا";

    /// <summary>Text of beh and alef.</summary>
    private const string BehAlef = "با";

    /// <summary>Text of alef and beh.</summary>
    private const string AlefBeh = "اب";

    /// <summary>A letter that joins on both sides is initial, medial and final in turn, and the glyphs come out in drawing order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DualJoiningLetterTakesItsPositionalForms()
    {
        var shaped = Fixture().Shape(BehBehAlef);

        await Assert.That(shaped.RightToLeft).IsTrue();
        await Assert.That(shaped.Letters).IsEqualTo("eMI");
        await Assert.That(shaped.Clusters).IsEqualTo("2,1,0");
    }

    /// <summary>A letter at the end of a word that joins the one before is final, and one that starts the word and joins the next is initial.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WordEndsTakeInitialAndFinalForms() => await Assert.That(Fixture().Shape(BehAlef).Letters).IsEqualTo("eI");

    /// <summary>A letter that joins nothing is isolated.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LoneLetterIsIsolated() => await Assert.That(Fixture().Shape("ا").Letters).IsEqualTo("A");

    /// <summary>A right-joining letter does not join the letter after it, so that letter starts a new joined run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RightJoiningLetterDoesNotJoinForward() => await Assert.That(Fixture().Shape(AlefBeh).Letters).IsEqualTo("SA");

    /// <summary>A zero width non-joiner stops joining and leaves no glyph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonJoinerBreaksTheJoin()
    {
        var shaped = Fixture().Shape("ب‌ب");

        await Assert.That(shaped.Letters).IsEqualTo("SS");
    }

    /// <summary>The ligature of lam and alef forms from the joined forms the font substitutes first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LamAlefLigatureFormsAfterTheJoiningForms()
    {
        var shaped = Fixture().Shape("لا");

        await Assert.That(shaped.Letters).IsEqualTo("Q");
        await Assert.That(shaped.Clusters).IsEqualTo("0");
    }

    /// <summary>Two words are drawn with the last word first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WordsAreInVisualOrder()
    {
        var shaped = Fixture().Shape($"{BehAlef} {AlefBeh}");

        await Assert.That(shaped.Letters).IsEqualTo("SA eI");
    }

    /// <summary>Makes a font with forms of beh, alef and lam and the features that substitute them.</summary>
    /// <returns>The fixture.</returns>
    internal static ShapingFixture Fixture()
    {
        var fixture = new ShapingFixture();
        _ = fixture.AddMapped('b', 0x0628, ShapingFixture.Advance);
        _ = fixture.AddMapped('a', 0x0627, ShapingFixture.Advance);
        _ = fixture.AddMapped('l', 0x0644, ShapingFixture.Advance);
        _ = fixture.Add(' ');
        foreach (var form in "SIMFAeLjfQ")
        {
            _ = fixture.AddUnmapped(form);
        }

        int[] bases = [fixture.Id('b'), fixture.Id('a'), fixture.Id('l')];
        var isolated = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleList(bases, [fixture.Id('S'), fixture.Id('A'), fixture.Id('L')]));
        var final = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleList(bases, [fixture.Id('F'), fixture.Id('e'), fixture.Id('f')]));
        var medial = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleList([fixture.Id('b')], [fixture.Id('M')]));
        var initial = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleList([fixture.Id('b'), fixture.Id('l')], [fixture.Id('I'), fixture.Id('j')]));
        var lamAlef = fixture.Gsub.AddLookup(Ligature, 0, GsubSubtables.Ligature(fixture.Id('j'), new LigatureRule(fixture.Id('Q'), [fixture.Id('e')])));
        int[] features =
        [
            fixture.Gsub.AddFeature("isol", isolated),
            fixture.Gsub.AddFeature("fina", final),
            fixture.Gsub.AddFeature("medi", medial),
            fixture.Gsub.AddFeature("init", initial),
            fixture.Gsub.AddFeature("rlig", lamAlef),
        ];
        fixture.Gsub.AddScript("arab", features);
        return fixture;
    }
}
