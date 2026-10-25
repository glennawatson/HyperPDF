// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets.Shaping;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Fonts.Shaping;

/// <summary>Glyph substitution (OpenType, GSUB lookup types 1 to 8) as the shaper applies it to a font's tables.</summary>
public sealed class GsubShapingTests
{
    /// <summary>The lookup type of a single substitution.</summary>
    private const int Single = 1;

    /// <summary>The lookup type of a multiple substitution.</summary>
    private const int Multiple = 0x2;

    /// <summary>The lookup type of an alternate substitution.</summary>
    private const int Alternate = 0x3;

    /// <summary>The lookup type of a ligature substitution.</summary>
    private const int Ligature = 0x4;

    /// <summary>The lookup type of a contextual substitution.</summary>
    private const int Context = 0x5;

    /// <summary>The lookup type of a chaining contextual substitution.</summary>
    private const int Chain = 0x6;

    /// <summary>The lookup type of an extension subtable.</summary>
    private const int Extension = 0x7;

    /// <summary>The lookup type of a reverse chaining contextual substitution.</summary>
    private const int ReverseChain = 0x8;

    /// <summary>The lookup flag that ignores marks.</summary>
    private const int IgnoreMarks = 0x8;

    /// <summary>The shift of the mark attachment class in the lookup flags.</summary>
    private const int AttachmentShift = 0x8;

    /// <summary>A single substitution by a delta and from a list both replace glyphs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SingleSubstitutionsReplaceGlyphs()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abcde");
        var byDelta = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('a')], 1));
        var byList = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleList([fixture.Id('c'), fixture.Id('d')], [fixture.Id('e'), fixture.Id('a')]));
        fixture.UseGsub("liga", byDelta, byList);

        await Assert.That(fixture.Shape("acd").Letters).IsEqualTo("bea");
    }

    /// <summary>A multiple substitution turns a glyph into several that share its cluster.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MultipleSubstitutionSplitsAGlyph()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("xyabc");
        var lookup = fixture.Gsub.AddLookup(Multiple, 0, GsubSubtables.Multiple(new Dictionary<int, int[]> { [fixture.Id('a')] = fixture.Ids("bc") }));
        fixture.UseGsub("ccmp", lookup);

        var shaped = fixture.Shape("xay");

        await Assert.That(shaped.Letters).IsEqualTo("xbcy");
        await Assert.That(shaped.Clusters).IsEqualTo("0,1,1,2");
    }

    /// <summary>An alternate substitution picks the alternate the feature's value names, and does nothing when the feature is off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AlternateSubstitutionFollowsTheFeatureValue()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abcd");
        var lookup = fixture.Gsub.AddLookup(Alternate, 0, GsubSubtables.Alternate(new Dictionary<int, int[]> { [fixture.Id('a')] = fixture.Ids("bcd") }));
        fixture.Gsub.AddScript("DFLT", fixture.Gsub.AddFeature("salt", lookup));

        var first = fixture.Shape("a", new(PdfWritingMode.Horizontal) { Features = [PdfFontFeature.On("salt")] });
        var second = fixture.Shape("a", new(PdfWritingMode.Horizontal) { Features = [new("salt", 0x2)] });
        var off = fixture.Shape("a");

        await Assert.That(first.Letters).IsEqualTo("b");
        await Assert.That(second.Letters).IsEqualTo("c");
        await Assert.That(off.Letters).IsEqualTo("a");
    }

    /// <summary>A ligature replaces its components, tries longer ligatures first when the font lists them first, and keeps the clusters of what follows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LigatureReplacesItsComponents()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("filx");
        _ = fixture.AddUnmapped('F');
        _ = fixture.AddUnmapped('L');
        LigatureRule triple = new(fixture.Id('L'), fixture.Ids("il"));
        LigatureRule pair = new(fixture.Id('F'), fixture.Ids("i"));
        var lookup = fixture.Gsub.AddLookup(Ligature, 0, GsubSubtables.Ligature(fixture.Id('f'), triple, pair));
        fixture.UseGsub("liga", lookup);

        var fi = fixture.Shape("fix");
        var fil = fixture.Shape("fil");

        await Assert.That(fi.Letters).IsEqualTo("Fx");
        await Assert.That(fi.Clusters).IsEqualTo("0,2");
        await Assert.That(fil.Letters).IsEqualTo("L");
    }

    /// <summary>A lookup that ignores marks forms a ligature across a mark, which stays after the ligature; one that does not leaves the glyphs alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LigatureSkipsMarksOnlyWhenTheLookupIgnoresThem()
    {
        var withFlag = LigatureAcrossMark(IgnoreMarks);
        var withoutFlag = LigatureAcrossMark(0);

        await Assert.That(withFlag.Shape("fmi").Letters).IsEqualTo("Fm");
        await Assert.That(withoutFlag.Shape("fmi").Letters).IsEqualTo("fmi");
    }

    /// <summary>A lookup with a mark filtering set ignores marks that are not in the set.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkFilteringSetDecidesWhichMarksAreIgnored()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("fimn");
        _ = fixture.AddUnmapped('F');
        var lookup = fixture.Gsub.AddLookupWithMarkSet(Ligature, 0, 0, [GsubSubtables.Ligature(fixture.Id('f'), new LigatureRule(fixture.Id('F'), fixture.Ids("i")))]);
        fixture.UseGsub("liga", lookup);
        var classes = new Dictionary<int, int> { [fixture.Id('f')] = GdefTable.Base, [fixture.Id('i')] = GdefTable.Base, [fixture.Id('m')] = GdefTable.Mark, [fixture.Id('n')] = GdefTable.Mark };
        fixture.Font.WithTable("GDEF", GdefTable.Build(classes, null, [[fixture.Id('m')]]));

        await Assert.That(fixture.Shape("fni").Letters).IsEqualTo("Fn");
        await Assert.That(fixture.Shape("fmi").Letters).IsEqualTo("fmi");
    }

    /// <summary>A lookup with a mark attachment type ignores the marks of other attachment classes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkAttachmentTypeLimitsWhichMarksAreSeen()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("fimn");
        _ = fixture.AddUnmapped('F');
        var lookup = fixture.Gsub.AddLookup(Ligature, 0x2 << AttachmentShift, GsubSubtables.Ligature(fixture.Id('f'), new LigatureRule(fixture.Id('F'), fixture.Ids("i"))));
        fixture.UseGsub("liga", lookup);
        var classes = new Dictionary<int, int> { [fixture.Id('f')] = GdefTable.Base, [fixture.Id('i')] = GdefTable.Base, [fixture.Id('m')] = GdefTable.Mark, [fixture.Id('n')] = GdefTable.Mark };
        var attachment = new Dictionary<int, int> { [fixture.Id('m')] = 1, [fixture.Id('n')] = 0x2 };
        fixture.Font.WithTable("GDEF", GdefTable.Build(classes, attachment, null));

        await Assert.That(fixture.Shape("fmi").Letters).IsEqualTo("Fm");
        await Assert.That(fixture.Shape("fni").Letters).IsEqualTo("fni");
    }

    /// <summary>A glyph-based contextual rule runs its lookup only where the whole sequence matches.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContextualRuleMatchesGlyphs()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abc");
        var inner = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('b')], 1));
        var outer = fixture.Gsub.AddLookup(Context, 0, GsubSubtables.ContextGlyphs(fixture.Id('a'), [fixture.Id('b')], new LookupRecord(1, inner)));
        fixture.UseGsub("calt", outer);

        await Assert.That(fixture.Shape("ab").Letters).IsEqualTo("ac");
        await Assert.That(fixture.Shape("bb").Letters).IsEqualTo("bb");
    }

    /// <summary>A class-based contextual rule matches glyphs by their classes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContextualRuleMatchesClasses()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abcd");
        var inner = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('c')], 1));
        var classes = new Dictionary<int, int> { [fixture.Id('a')] = 1, [fixture.Id('b')] = 0x2, [fixture.Id('c')] = 0x2 };
        var outer = fixture.Gsub.AddLookup(Context, 0, GsubSubtables.ContextClasses([fixture.Id('a')], classes, 1, [0x2], new LookupRecord(1, inner)));
        fixture.UseGsub("calt", outer);

        await Assert.That(fixture.Shape("ac").Letters).IsEqualTo("ad");
        await Assert.That(fixture.Shape("cc").Letters).IsEqualTo("cc");
    }

    /// <summary>A coverage-based contextual rule matches each position against a set of glyphs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContextualRuleMatchesCoverage()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abcd");
        var inner = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('c')], 1));
        var outer = fixture.Gsub.AddLookup(Context, 0, GsubSubtables.ContextCoverage([[fixture.Id('a'), fixture.Id('b')], [fixture.Id('c')]], new LookupRecord(1, inner)));
        fixture.UseGsub("calt", outer);

        await Assert.That(fixture.Shape("bc").Letters).IsEqualTo("bd");
        await Assert.That(fixture.Shape("dc").Letters).IsEqualTo("dc");
    }

    /// <summary>A chaining rule on glyphs needs the glyphs before and after to match too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChainingRuleMatchesGlyphsAround()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abxy");
        var inner = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('a')], 1));
        var outer = fixture.Gsub.AddLookup(Chain, 0, GsubSubtables.ChainGlyphs(fixture.Id('a'), [fixture.Id('x')], [], [fixture.Id('y')], new LookupRecord(0, inner)));
        fixture.UseGsub("calt", outer);

        await Assert.That(fixture.Shape("xay").Letters).IsEqualTo("xby");
        await Assert.That(fixture.Shape("ay").Letters).IsEqualTo("ay");
        await Assert.That(fixture.Shape("xa").Letters).IsEqualTo("xa");
    }

    /// <summary>A chaining rule on classes matches the glyphs around by class.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChainingRuleMatchesClassesAround()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abxy");
        var inner = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('a')], 1));
        var classes = new Dictionary<int, int> { [fixture.Id('a')] = 1, [fixture.Id('x')] = 0x2, [fixture.Id('y')] = 0x3 };
        var outer = fixture.Gsub.AddLookup(Chain, 0, GsubSubtables.ChainClasses([fixture.Id('a')], classes, 1, [0x2], [], [0x3], new LookupRecord(0, inner)));
        fixture.UseGsub("calt", outer);

        await Assert.That(fixture.Shape("xay").Letters).IsEqualTo("xby");
        await Assert.That(fixture.Shape("yay").Letters).IsEqualTo("yay");
    }

    /// <summary>A chaining rule on coverage matches sets of glyphs around.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChainingRuleMatchesCoverageAround()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abxy");
        var inner = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('a')], 1));
        var outer = fixture.Gsub.AddLookup(Chain, 0, GsubSubtables.ChainCoverage([[fixture.Id('x')]], [[fixture.Id('a')]], [[fixture.Id('y'), fixture.Id('b')]], new LookupRecord(0, inner)));
        fixture.UseGsub("calt", outer);

        await Assert.That(fixture.Shape("xab").Letters).IsEqualTo("xbb");
        await Assert.That(fixture.Shape("xax").Letters).IsEqualTo("xax");
    }

    /// <summary>A nested lookup that adds glyphs does not throw the later records of the rule off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NestedLookupThatAddsGlyphsKeepsLaterRecordsInPlace()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abcdez");
        var split = fixture.Gsub.AddLookup(Multiple, 0, GsubSubtables.Multiple(new Dictionary<int, int[]> { [fixture.Id('a')] = fixture.Ids("zz") }));
        var bump = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('c')], 1));
        var outer = fixture.Gsub.AddLookup(Context, 0, GsubSubtables.ContextGlyphs(fixture.Id('a'), fixture.Ids("bc"), new LookupRecord(0, split), new LookupRecord(0x2, bump)));
        fixture.UseGsub("calt", outer);

        await Assert.That(fixture.Shape("abc").Letters).IsEqualTo("zzbd");
    }

    /// <summary>A reverse chaining substitution looks at the glyphs after as they were before any change, going from the end.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReverseChainingSubstitutionRunsBackward()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abcd");
        var lookup = fixture.Gsub.AddLookup(ReverseChain, 0, GsubSubtables.ReverseChain([fixture.Id('a')], [[fixture.Id('b')]], [[fixture.Id('c')]], [fixture.Id('d')]));
        fixture.UseGsub("calt", lookup);

        await Assert.That(fixture.Shape("bac").Letters).IsEqualTo("bdc");
        await Assert.That(fixture.Shape("ac").Letters).IsEqualTo("ac");
    }

    /// <summary>An extension subtable stands in for the subtable it wraps.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtensionSubtableIsFollowed()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("ab");
        var wrapped = GsubSubtables.Extension(Single, GsubSubtables.SingleDelta([fixture.Id('a')], 1));
        var lookup = fixture.Gsub.AddLookup(Extension, 0, wrapped);
        fixture.UseGsub("liga", lookup);

        await Assert.That(fixture.Shape("a").Letters).IsEqualTo("b");
    }

    /// <summary>A feature the caller turns off is not applied.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CallerCanTurnADefaultFeatureOff()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("ab");
        var lookup = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('a')], 1));
        fixture.UseGsub("liga", lookup);

        await Assert.That(fixture.Shape("a").Letters).IsEqualTo("b");
        await Assert.That(fixture.Shape("a", new(PdfWritingMode.Horizontal) { Features = [PdfFontFeature.Off("liga")] }).Letters).IsEqualTo("a");
    }

    /// <summary>Lookups run in lookup list order, whichever feature asks for them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LookupsRunInListOrder()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abc");
        var first = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('a')], 1));
        var second = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('b')], 1));
        fixture.UseGsub("clig", second);
        fixture.UseGsub("liga", first);

        await Assert.That(fixture.Shape("a").Letters).IsEqualTo("c");
    }

    /// <summary>The font's script entry for the text is used, then the language system the caller names, then the defaults.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScriptAndLanguageSelectTheFeatures()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abcd");
        var general = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('a')], 1));
        var latin = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('a')], 0x2));
        var turkish = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('a')], 0x3));
        var defaultFeature = fixture.Gsub.AddFeature("liga", general);
        var latinFeature = fixture.Gsub.AddFeature("liga", latin);
        var turkishFeature = fixture.Gsub.AddFeature("liga", turkish);
        fixture.Gsub.AddScript("DFLT", defaultFeature);
        fixture.Gsub.AddScript("latn", latinFeature);
        fixture.Gsub.AddLanguage("latn", "TRK", turkishFeature);

        await Assert.That(fixture.Shape("a").Letters).IsEqualTo("c");
        await Assert.That(fixture.Shape("a", new(PdfWritingMode.Horizontal) { Language = "tr" }).Letters).IsEqualTo("d");
        await Assert.That(fixture.Shape("a", new(PdfWritingMode.Horizontal) { Script = "Zzzz" }).Letters).IsEqualTo("b");
    }

    /// <summary>Vertical text gets the font's vertical alternates and the vertical advance.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VerticalWritingUsesVerticalFormsAndAdvances()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("ab");
        var lookup = fixture.Gsub.AddLookup(Single, 0, GsubSubtables.SingleDelta([fixture.Id('a')], 1));
        fixture.UseGsub("vert", lookup);
        fixture.Font.WithVerticalMetrics(0x3E8);

        var shaped = fixture.Shape("a", new(PdfWritingMode.Vertical));

        await Assert.That(shaped.Letters).IsEqualTo("b");
        await Assert.That(shaped.Advances).IsEqualTo("1000");
        await Assert.That(fixture.Shape("a").Letters).IsEqualTo("a");
    }

    /// <summary>Makes a font that forms the ligature "fi" with a mark between its letters.</summary>
    /// <param name="flags">The lookup flags.</param>
    /// <returns>The fixture.</returns>
    private static ShapingFixture LigatureAcrossMark(int flags)
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("fim");
        _ = fixture.AddUnmapped('F');
        var lookup = fixture.Gsub.AddLookup(Ligature, flags, GsubSubtables.Ligature(fixture.Id('f'), new LigatureRule(fixture.Id('F'), fixture.Ids("i"))));
        fixture.UseGsub("liga", lookup);
        var classes = new Dictionary<int, int> { [fixture.Id('f')] = GdefTable.Base, [fixture.Id('i')] = GdefTable.Base, [fixture.Id('m')] = GdefTable.Mark };
        fixture.Font.WithTable("GDEF", GdefTable.Build(classes, null, null));
        return fixture;
    }
}
