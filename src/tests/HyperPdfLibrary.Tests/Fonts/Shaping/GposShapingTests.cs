// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets.Shaping;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Fonts.Shaping;

/// <summary>Glyph positioning (OpenType, GPOS lookup types 1 to 6) and the legacy kern table, as the shaper applies them.</summary>
public sealed class GposShapingTests
{
    /// <summary>The lookup type of a single adjustment.</summary>
    private const int Single = 1;

    /// <summary>The lookup type of a pair adjustment.</summary>
    private const int Pair = 0x2;

    /// <summary>The lookup type of a cursive attachment.</summary>
    private const int Cursive = 0x3;

    /// <summary>The lookup type of a mark-to-base attachment.</summary>
    private const int MarkToBase = 0x4;

    /// <summary>The lookup type of a mark-to-ligature attachment.</summary>
    private const int MarkToLigature = 0x5;

    /// <summary>The lookup type of a mark-to-mark attachment.</summary>
    private const int MarkToMark = 0x6;

    /// <summary>The lookup type of a contextual positioning.</summary>
    private const int ContextPositioning = 0x7;

    /// <summary>The lookup type of a chaining contextual positioning.</summary>
    private const int ChainPositioning = 0x8;

    /// <summary>The lookup type of a ligature substitution.</summary>
    private const int SubstituteLigature = 0x4;

    /// <summary>The advances of two untouched glyphs.</summary>
    private const string Unchanged = "500,500";

    /// <summary>The lookup flag that ignores marks.</summary>
    private const int IgnoreMarks = 0x8;

    /// <summary>A single adjustment changes the advance and placement of the glyphs it covers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SingleAdjustmentMovesAndWidensGlyphs()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("ab");
        ValueAdjustment value = new(0x32, 0x64, 0x96);
        var lookup = fixture.Gpos.AddLookup(Single, 0, GposSubtables.Single([fixture.Id('a')], value));
        fixture.UseGpos("kern", lookup);

        var shaped = fixture.Shape("ab");

        await Assert.That(shaped.Advances).IsEqualTo("650,500");
        await Assert.That(shaped.OffsetsX).IsEqualTo("50,0");
        await Assert.That(shaped.OffsetsY).IsEqualTo("100,0");
    }

    /// <summary>A pair adjustment kerns two glyphs and leaves other pairs alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PairAdjustmentKernsAPair()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("avx");
        var lookup = fixture.Gpos.AddLookup(Pair, 0, GposSubtables.PairGlyphs(fixture.Id('a'), new PairRule(fixture.Id('v'), ValueAdjustment.Advance(-0x50), default)));
        fixture.UseGpos("kern", lookup);

        await Assert.That(fixture.Shape("av").Advances).IsEqualTo("420,500");
        await Assert.That(fixture.Shape("ax").Advances).IsEqualTo(Unchanged);
    }

    /// <summary>A pair adjustment can adjust the second glyph too, and then the pair after it starts at the glyph after the second.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PairAdjustmentCanAdjustTheSecondGlyph()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("avx");
        var rule = new PairRule(fixture.Id('v'), ValueAdjustment.Advance(-0x32), ValueAdjustment.Advance(0x64));
        var lookup = fixture.Gpos.AddLookup(Pair, 0, GposSubtables.PairGlyphs(fixture.Id('a'), rule));
        fixture.UseGpos("kern", lookup);

        await Assert.That(fixture.Shape("avx").Advances).IsEqualTo("450,600,500");
    }

    /// <summary>A class-based pair adjustment kerns every pair of two classes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClassPairAdjustmentKernsClassesOfGlyphs()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abxy");
        var first = new Dictionary<int, int> { [fixture.Id('a')] = 1, [fixture.Id('b')] = 1 };
        var second = new Dictionary<int, int> { [fixture.Id('x')] = 1, [fixture.Id('y')] = 0x2 };
        ValueAdjustment[][] matrix = [[default, default, default], [default, ValueAdjustment.Advance(-0x64), ValueAdjustment.Advance(-0xC8)]];
        var lookup = fixture.Gpos.AddLookup(Pair, 0, GposSubtables.PairClasses([fixture.Id('a'), fixture.Id('b')], first, second, matrix));
        fixture.UseGpos("kern", lookup);

        await Assert.That(fixture.Shape("ax").Advances).IsEqualTo("400,500");
        await Assert.That(fixture.Shape("by").Advances).IsEqualTo("300,500");
        await Assert.That(fixture.Shape("ab").Advances).IsEqualTo(Unchanged);
    }

    /// <summary>Without GPOS kerning the font's legacy kern table is used.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LegacyKernTableKernsWhenThereIsNoGposKerning()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("av");
        fixture.Font.WithTable("kern", ShapingFontBuilder.KernTable(fixture.Id('a'), fixture.Id('v'), -0x64));

        await Assert.That(fixture.Shape("av").Advances).IsEqualTo("400,500");
        await Assert.That(fixture.Shape("av", new(PdfWritingMode.Horizontal) { Features = [PdfFontFeature.Off("kern")] }).Advances).IsEqualTo(Unchanged);
    }

    /// <summary>A mark is placed so that its anchor meets the base's anchor, as an offset from where the pen is.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkToBaseMeetsTheBaseAnchor()
    {
        var fixture = MarkFixture(out var marks, out var bases);
        var lookup = fixture.Gpos.AddLookup(MarkToBase, 0, GposSubtables.MarkToBase(marks, bases, 1));
        fixture.UseGpos("mark", lookup);

        var shaped = fixture.Shape("am");

        await Assert.That(shaped.Advances).IsEqualTo("500,0");
        await Assert.That(shaped.OffsetsX).IsEqualTo("0,-300");
        await Assert.That(shaped.OffsetsY).IsEqualTo("0,500");
    }

    /// <summary>In right-to-left text the mark is drawn before its base, so its offset counts the advances the other way.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkToBaseInRightToLeftTextIsOffsetTheOtherWay()
    {
        var fixture = new ShapingFixture();
        _ = fixture.AddMapped('a', 0x5D0, ShapingFixture.Advance);
        _ = fixture.AddMapped('m', 0x5B0, 0);
        var marks = new Dictionary<int, MarkAnchor> { [fixture.Id('m')] = new(0, new(0x32, 0)) };
        var bases = new Dictionary<int, Anchor?[]> { [fixture.Id('a')] = [new Anchor(0xFA, 0x1F4)] };
        fixture.Font.WithTable("GDEF", GdefTable.Build(new Dictionary<int, int> { [fixture.Id('a')] = GdefTable.Base, [fixture.Id('m')] = GdefTable.Mark }, null, null));
        fixture.UseGpos("mark", fixture.Gpos.AddLookup(MarkToBase, 0, GposSubtables.MarkToBase(marks, bases, 1)));

        var shaped = fixture.Shape("אְ");

        await Assert.That(shaped.RightToLeft).IsTrue();
        await Assert.That(shaped.Letters).IsEqualTo("ma");
        await Assert.That(shaped.OffsetsX).IsEqualTo("200,0");
    }

    /// <summary>A mark on a ligature goes to the component it followed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkToLigatureUsesTheComponentTheMarkFollowed()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("fim");
        _ = fixture.AddMapped('F', 0, 0x3E8);
        var ligature = fixture.Gsub.AddLookup(SubstituteLigature, IgnoreMarks, GsubSubtables.Ligature(fixture.Id('f'), new LigatureRule(fixture.Id('F'), fixture.Ids("i"))));
        fixture.UseGsub("liga", ligature);
        var marks = new Dictionary<int, MarkAnchor> { [fixture.Id('m')] = new(0, new(0, 0)) };
        var ligatures = new Dictionary<int, Anchor?[][]> { [fixture.Id('F')] = [[new Anchor(0x64, 0x1F4)], [new Anchor(0x2BC, 0x1F4)]] };
        var attach = fixture.Gpos.AddLookup(MarkToLigature, 0, GposSubtables.MarkToLigature(marks, ligatures, 1));
        fixture.UseGpos("mark", attach);
        var classes = new Dictionary<int, int> { [fixture.Id('f')] = GdefTable.Base, [fixture.Id('i')] = GdefTable.Base, [fixture.Id('F')] = GdefTable.Ligature, [fixture.Id('m')] = GdefTable.Mark };
        fixture.Font.WithTable("GDEF", GdefTable.Build(classes, null, null));

        var afterFirst = fixture.Shape("fmi");
        var afterLast = fixture.Shape("fim");

        await Assert.That(afterFirst.Letters).IsEqualTo("Fm");
        await Assert.That(afterFirst.OffsetsX).IsEqualTo("0,-900");
        await Assert.That(afterLast.OffsetsX).IsEqualTo("0,-300");
    }

    /// <summary>A mark can sit on another mark.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkToMarkStacksMarks()
    {
        var fixture = MarkFixture(out var marks, out var bases);
        _ = fixture.AddMapped('n', 'n', 0);
        var secondMarks = new Dictionary<int, MarkAnchor> { [fixture.Id('n')] = new(0, new(0, 0)) };
        var firstMarks = new Dictionary<int, Anchor?[]> { [fixture.Id('m')] = [new Anchor(0, 0x64)] };
        fixture.UseGpos("mark", fixture.Gpos.AddLookup(MarkToBase, 0, GposSubtables.MarkToBase(marks, bases, 1)));
        fixture.UseGpos("mkmk", fixture.Gpos.AddLookup(MarkToMark, 0, GposSubtables.MarkToBase(secondMarks, firstMarks, 1)));
        var classes = new Dictionary<int, int> { [fixture.Id('a')] = GdefTable.Base, [fixture.Id('m')] = GdefTable.Mark, [fixture.Id('n')] = GdefTable.Mark };
        fixture.Font.WithTable("GDEF", GdefTable.Build(classes, null, null));

        var shaped = fixture.Shape("amn");

        await Assert.That(shaped.OffsetsY).IsEqualTo("0,500,600");
    }

    /// <summary>Cursive glyphs join where one's exit anchor meets the next one's entry anchor.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CursiveAttachmentJoinsGlyphs()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("ab");
        var anchors = new Dictionary<int, Anchor?[]> { [fixture.Id('a')] = [null, new Anchor(0x1CC, 0x64)], [fixture.Id('b')] = [new Anchor(0x32, 0), null] };
        fixture.UseGpos("curs", fixture.Gpos.AddLookup(Cursive, 0, GposSubtables.Cursive(anchors)));

        var shaped = fixture.Shape("ab");

        await Assert.That(shaped.Advances).IsEqualTo("460,450");
        await Assert.That(shaped.OffsetsX).IsEqualTo("0,-50");
        await Assert.That(shaped.OffsetsY).IsEqualTo("0,100");
    }

    /// <summary>A contextual positioning rule runs a positioning lookup only where the glyphs match, and a chaining rule needs the glyphs around too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContextualPositioningRunsNestedLookups()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abxy");
        var widen = fixture.Gpos.AddLookup(Single, 0, GposSubtables.Single([fixture.Id('b'), fixture.Id('x')], ValueAdjustment.Advance(0x64)));
        var context = fixture.Gpos.AddLookup(ContextPositioning, 0, GsubSubtables.ContextGlyphs(fixture.Id('a'), [fixture.Id('b')], new LookupRecord(1, widen)));
        var chain = fixture.Gpos.AddLookup(ChainPositioning, 0, GsubSubtables.ChainGlyphs(fixture.Id('x'), [fixture.Id('y')], [], [], new LookupRecord(0, widen)));
        fixture.UseGpos("kern", context, chain);

        await Assert.That(fixture.Shape("ab").Advances).IsEqualTo("500,600");
        await Assert.That(fixture.Shape("bb").Advances).IsEqualTo(Unchanged);
        await Assert.That(fixture.Shape("yx").Advances).IsEqualTo("500,600");
        await Assert.That(fixture.Shape("ax").Advances).IsEqualTo(Unchanged);
    }

    /// <summary>A GDEF mark with an advance in the font has none in the shaped run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarksHaveNoAdvance()
    {
        var fixture = new ShapingFixture();
        _ = fixture.Add('a');
        _ = fixture.AddMapped('m', 'm', 0x1F4);
        fixture.Font.WithTable("GDEF", GdefTable.Build(new Dictionary<int, int> { [fixture.Id('a')] = GdefTable.Base, [fixture.Id('m')] = GdefTable.Mark }, null, null));

        await Assert.That(fixture.Shape("am").Advances).IsEqualTo("500,0");
    }

    /// <summary>Makes a font with a base letter and a mark, and the anchors that attach the mark to the base.</summary>
    /// <param name="marks">Receives the mark anchors.</param>
    /// <param name="bases">Receives the base anchors.</param>
    /// <returns>The fixture.</returns>
    private static ShapingFixture MarkFixture(out Dictionary<int, MarkAnchor> marks, out Dictionary<int, Anchor?[]> bases)
    {
        var fixture = new ShapingFixture();
        _ = fixture.Add('a');
        _ = fixture.AddMapped('m', 'm', 0);
        marks = new Dictionary<int, MarkAnchor> { [fixture.Id('m')] = new(0, new(0x32, 0)) };
        bases = new Dictionary<int, Anchor?[]> { [fixture.Id('a')] = [new Anchor(0xFA, 0x1F4)] };
        fixture.Font.WithTable("GDEF", GdefTable.Build(new Dictionary<int, int> { [fixture.Id('a')] = GdefTable.Base, [fixture.Id('m')] = GdefTable.Mark }, null, null));
        return fixture;
    }
}
