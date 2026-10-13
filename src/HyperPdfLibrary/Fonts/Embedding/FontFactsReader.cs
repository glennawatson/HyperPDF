// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Reads the metrics, style and licence of a face from its head, hhea, OS/2, post and name tables.</summary>
internal static class FontFactsReader
{
    /// <summary>The smallest /head table.</summary>
    private const int HeadSize = 54;

    /// <summary>The offset of the design units per em in /head.</summary>
    private const int HeadUnitsPerEm = 18;

    /// <summary>The offset of the left of the bounding box in /head.</summary>
    private const int HeadXMin = 36;

    /// <summary>The offset of the bottom of the bounding box in /head.</summary>
    private const int HeadYMin = 38;

    /// <summary>The offset of the right of the bounding box in /head.</summary>
    private const int HeadXMax = 40;

    /// <summary>The offset of the top of the bounding box in /head.</summary>
    private const int HeadYMax = 42;

    /// <summary>The first /OS/2 version that has the capital height.</summary>
    private const int Os2CapHeightVersion = 2;

    /// <summary>The offset of the style bits in /head.</summary>
    private const int HeadMacStyle = 44;

    /// <summary>The offset of the glyph count in /maxp.</summary>
    private const int MaxpGlyphs = 4;

    /// <summary>The offset of the ascender in /hhea.</summary>
    private const int HheaAscender = 4;

    /// <summary>The offset of the descender in /hhea.</summary>
    private const int HheaDescender = 6;

    /// <summary>The offset of the metric count in /hhea.</summary>
    private const int HheaMetricCount = 34;

    /// <summary>The offset of the weight in /OS/2.</summary>
    private const int Os2Weight = 4;

    /// <summary>The offset of the embedding flags in /OS/2.</summary>
    private const int Os2EmbeddingFlags = 8;

    /// <summary>The offset of the family class in /OS/2.</summary>
    private const int Os2FamilyClass = 30;

    /// <summary>The offset of the selection bits in /OS/2.</summary>
    private const int Os2Selection = 62;

    /// <summary>The offset of the typographic ascender in /OS/2.</summary>
    private const int Os2TypoAscender = 68;

    /// <summary>The offset of the typographic descender in /OS/2.</summary>
    private const int Os2TypoDescender = 70;

    /// <summary>The offset of the Windows ascent in /OS/2.</summary>
    private const int Os2WinAscent = 74;

    /// <summary>The offset of the Windows descent in /OS/2.</summary>
    private const int Os2WinDescent = 76;

    /// <summary>The offset of the capital height in /OS/2 version 2 and later.</summary>
    private const int Os2CapHeight = 88;

    /// <summary>The smallest /OS/2 table with the Windows metrics.</summary>
    private const int Os2WinSize = 78;

    /// <summary>The smallest /OS/2 table with the capital height.</summary>
    private const int Os2CapSize = 90;

    /// <summary>The offset of the italic angle in /post.</summary>
    private const int PostItalicAngle = 4;

    /// <summary>The offset of the fixed pitch flag in /post.</summary>
    private const int PostFixedPitch = 12;

    /// <summary>The smallest /post table that holds the fixed pitch flag.</summary>
    private const int PostSize = 16;

    /// <summary>The Mac style bit for bold.</summary>
    private const int MacBold = 1;

    /// <summary>The Mac style bit for italic.</summary>
    private const int MacItalic = 2;

    /// <summary>The selection bit for italic.</summary>
    private const int SelectionItalic = 0x01;

    /// <summary>The selection bit for bold.</summary>
    private const int SelectionBold = 0x20;

    /// <summary>The selection bit that asks for the typographic metrics.</summary>
    private const int SelectionTypoMetrics = 0x80;

    /// <summary>The first family class with serifs.</summary>
    private const int FirstSerifClass = 1;

    /// <summary>The last family class with serifs.</summary>
    private const int LastSerifClass = 7;

    /// <summary>The shift of the family class in the family class field.</summary>
    private const int FamilyClassShift = 8;

    /// <summary>The scale of a 16.16 fixed number.</summary>
    private const float FixedScale = 65536F;

    /// <summary>The weight of a regular face.</summary>
    private const int RegularWeight = 400;

    /// <summary>The weight of a bold face.</summary>
    private const int BoldWeight = 700;

    /// <summary>The smallest units per em the font format allows.</summary>
    private const int MinUnitsPerEm = 16;

    /// <summary>The largest units per em the font format allows.</summary>
    private const int MaxUnitsPerEm = 16_384;

    /// <summary>The offset of the table count in /cmap.</summary>
    private const int CmapCount = 2;

    /// <summary>The size of the /cmap header.</summary>
    private const int CmapHeader = 4;

    /// <summary>The size of a /cmap encoding record.</summary>
    private const int CmapRecord = 8;

    /// <summary>The Unicode platform.</summary>
    private const int UnicodePlatform = 0;

    /// <summary>The Windows platform.</summary>
    private const int WindowsPlatform = 3;

    /// <summary>The Windows Unicode BMP encoding.</summary>
    private const int WindowsBmp = 1;

    /// <summary>The Windows Unicode full repertoire encoding.</summary>
    private const int WindowsFull = 10;

    /// <summary>Reads the facts of a face.</summary>
    /// <param name="font">The face.</param>
    /// <param name="facts">Receives the facts.</param>
    /// <param name="refusal">Receives the plain reason when the face lacks a required table.</param>
    /// <returns><see langword="true"/> when the facts were read.</returns>
    internal static bool TryRead(SfntFont font, [NotNullWhen(true)] out FontFacts? facts, [NotNullWhen(false)] out string? refusal)
    {
        facts = null;
        refusal = null;
        var head = font.Table(SfntTags.Head);
        var maxp = font.Table(SfntTags.Maxp);
        var units = FontBytes.U16(head, HeadUnitsPerEm);
        if (head.Length < HeadSize || maxp.Length <= MaxpGlyphs || units is < MinUnitsPerEm or > MaxUnitsPerEm)
        {
            refusal = "The font has no valid 'head' and 'maxp' tables.";
            return false;
        }

        var os2 = font.Table(SfntTags.Os2);
        var hhea = font.Table(SfntTags.Hhea);
        var style = ReadStyle(font, head, os2);
        var name = font.Table(SfntTags.Name);
        var family = SfntNames.Find(name, SfntNames.TypographicFamilyId) ?? SfntNames.Find(name, SfntNames.FamilyId) ?? "Embedded";
        facts = new(
            units,
            FontBytes.U16(maxp, MaxpGlyphs),
            FontBytes.U16(hhea, HheaMetricCount),
            FontBytes.S16(head, HeadXMin),
            FontBytes.S16(head, HeadYMin),
            FontBytes.S16(head, HeadXMax),
            FontBytes.S16(head, HeadYMax),
            Ascent(hhea, os2),
            Descent(hhea, os2),
            CapHeight(hhea, os2),
            ReadItalicAngle(font.Table(SfntTags.Post)),
            Weight(os2, style),
            os2.Length > Os2EmbeddingFlags + 1 ? FontBytes.U16(os2, Os2EmbeddingFlags) : 0,
            style,
            SfntNames.ToPostScriptName(SfntNames.Find(name, SfntNames.PostScriptId), family),
            family);
        return true;
    }

    /// <summary>Gets the weight class, or a guess from the style when there is no /OS/2 table.</summary>
    /// <param name="os2">The /OS/2 table.</param>
    /// <param name="style">The style flags.</param>
    /// <returns>The weight.</returns>
    private static int Weight(ReadOnlySpan<byte> os2, FaceStyleFlags style)
    {
        if (os2.Length > Os2Weight + 1)
        {
            return FontBytes.U16(os2, Os2Weight);
        }

        return (style & FaceStyleFlags.Bold) != 0 ? BoldWeight : RegularWeight;
    }

    /// <summary>Gets the style flags.</summary>
    /// <param name="font">The face.</param>
    /// <param name="head">The /head table.</param>
    /// <param name="os2">The /OS/2 table.</param>
    /// <returns>The flags.</returns>
    private static FaceStyleFlags ReadStyle(SfntFont font, ReadOnlySpan<byte> head, ReadOnlySpan<byte> os2)
    {
        var style = ReadSlant(head, os2);
        var post = font.Table(SfntTags.Post);
        style |= post.Length >= PostSize && FontBytes.U32(post, PostFixedPitch) != 0 ? FaceStyleFlags.FixedPitch : FaceStyleFlags.None;
        var familyClass = os2.Length > Os2FamilyClass + 1 ? FontBytes.U16(os2, Os2FamilyClass) >> FamilyClassShift : 0;
        style |= familyClass is >= FirstSerifClass and <= LastSerifClass ? FaceStyleFlags.Serif : FaceStyleFlags.None;
        style |= HasUnicodeCmap(font.Table(SfntTags.Cmap)) ? FaceStyleFlags.UnicodeCmap : FaceStyleFlags.None;
        return style;
    }

    /// <summary>Gets whether the face is bold or italic, from the font header and selection bits.</summary>
    /// <param name="head">The /head table.</param>
    /// <param name="os2">The /OS/2 table.</param>
    /// <returns>The bold and italic flags.</returns>
    private static FaceStyleFlags ReadSlant(ReadOnlySpan<byte> head, ReadOnlySpan<byte> os2)
    {
        var mac = FontBytes.U16(head, HeadMacStyle);
        var selection = os2.Length > Os2Selection + 1 ? FontBytes.U16(os2, Os2Selection) : 0;
        var style = (mac & MacBold) != 0 || (selection & SelectionBold) != 0 ? FaceStyleFlags.Bold : FaceStyleFlags.None;
        return style | ((mac & MacItalic) != 0 || (selection & SelectionItalic) != 0 ? FaceStyleFlags.Italic : FaceStyleFlags.None);
    }

    /// <summary>Determines whether the /cmap table has a Unicode subtable.</summary>
    /// <param name="cmap">The table.</param>
    /// <returns><see langword="true"/> when one is listed.</returns>
    private static bool HasUnicodeCmap(ReadOnlySpan<byte> cmap)
    {
        var count = FontBytes.U16(cmap, CmapCount);
        for (var i = 0; i < count; i++)
        {
            var record = CmapHeader + (i * CmapRecord);
            var platform = FontBytes.U16(cmap, record);
            var encoding = FontBytes.U16(cmap, record + FontBytes.U16Size);
            if (platform == UnicodePlatform || (platform == WindowsPlatform && encoding is WindowsBmp or WindowsFull))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the ascent: the typographic one when the face asks for it, else the horizontal header's.</summary>
    /// <param name="hhea">The /hhea table.</param>
    /// <param name="os2">The /OS/2 table.</param>
    /// <returns>The ascent.</returns>
    private static int Ascent(ReadOnlySpan<byte> hhea, ReadOnlySpan<byte> os2)
    {
        var typo = os2.Length > Os2TypoAscender + 1 ? FontBytes.S16(os2, Os2TypoAscender) : 0;
        var win = os2.Length >= Os2WinSize ? FontBytes.U16(os2, Os2WinAscent) : 0;
        var header = FontBytes.S16(hhea, HheaAscender);
        return UsesTypoMetrics(os2) && typo != 0 ? typo : FirstNonZero(header, typo, win);
    }

    /// <summary>Gets the descent, negative.</summary>
    /// <param name="hhea">The /hhea table.</param>
    /// <param name="os2">The /OS/2 table.</param>
    /// <returns>The descent.</returns>
    private static int Descent(ReadOnlySpan<byte> hhea, ReadOnlySpan<byte> os2)
    {
        var typo = os2.Length > Os2TypoDescender + 1 ? FontBytes.S16(os2, Os2TypoDescender) : 0;
        var win = os2.Length >= Os2WinSize ? -FontBytes.U16(os2, Os2WinDescent) : 0;
        var header = FontBytes.S16(hhea, HheaDescender);
        return UsesTypoMetrics(os2) && typo != 0 ? typo : FirstNonZero(header, typo, win);
    }

    /// <summary>Gets the capital height, falling back to the ascent.</summary>
    /// <param name="hhea">The /hhea table.</param>
    /// <param name="os2">The /OS/2 table.</param>
    /// <returns>The height.</returns>
    private static int CapHeight(ReadOnlySpan<byte> hhea, ReadOnlySpan<byte> os2)
    {
        var cap = os2.Length >= Os2CapSize && FontBytes.U16(os2, 0) >= Os2CapHeightVersion ? FontBytes.S16(os2, Os2CapHeight) : 0;
        return cap > 0 ? cap : Ascent(hhea, os2);
    }

    /// <summary>Determines whether the face asks readers to use its typographic metrics.</summary>
    /// <param name="os2">The /OS/2 table.</param>
    /// <returns><see langword="true"/> when the selection bit is set.</returns>
    private static bool UsesTypoMetrics(ReadOnlySpan<byte> os2) =>
        os2.Length > Os2Selection + 1 && (FontBytes.U16(os2, Os2Selection) & SelectionTypoMetrics) != 0;

    /// <summary>Gets the first of three values that is not zero.</summary>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    /// <param name="third">The third value.</param>
    /// <returns>The value, or zero.</returns>
    private static int FirstNonZero(int first, int second, int third)
    {
        if (first != 0)
        {
            return first;
        }

        return second != 0 ? second : third;
    }

    /// <summary>Gets the italic angle from /post.</summary>
    /// <param name="post">The /post table.</param>
    /// <returns>The angle in degrees, zero when the table is missing.</returns>
    private static float ReadItalicAngle(ReadOnlySpan<byte> post) =>
        post.Length >= PostSize ? (int)FontBytes.U32(post, PostItalicAngle) / FixedScale : 0F;
}
