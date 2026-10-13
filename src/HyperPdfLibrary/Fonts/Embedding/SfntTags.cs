// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>The tags of the sfnt tables and versions that embedding reads or writes.</summary>
internal static class SfntTags
{
    /// <summary>The TrueType outline version.</summary>
    internal const uint VersionOne = 0x00010000;

    /// <summary>The Apple TrueType version, spelled "true".</summary>
    internal const uint VersionTrue = 0x74727565;

    /// <summary>The OpenType CFF version, spelled "OTTO".</summary>
    internal const uint VersionOtto = 0x4F54544F;

    /// <summary>The font collection tag, spelled "ttcf".</summary>
    internal const uint Collection = 0x74746366;

    /// <summary>The font header table, "head".</summary>
    internal const uint Head = 0x68656164;

    /// <summary>The horizontal header table, "hhea".</summary>
    internal const uint Hhea = 0x68686561;

    /// <summary>The horizontal metrics table, "hmtx".</summary>
    internal const uint Hmtx = 0x686D7478;

    /// <summary>The maximum profile table, "maxp".</summary>
    internal const uint Maxp = 0x6D617870;

    /// <summary>The glyph location table, "loca".</summary>
    internal const uint Loca = 0x6C6F6361;

    /// <summary>The glyph data table, "glyf".</summary>
    internal const uint Glyf = 0x676C7966;

    /// <summary>The character map table, "cmap".</summary>
    internal const uint Cmap = 0x636D6170;

    /// <summary>The OS/2 table.</summary>
    internal const uint Os2 = 0x4F532F32;

    /// <summary>The PostScript table, "post".</summary>
    internal const uint Post = 0x706F7374;

    /// <summary>The naming table, "name".</summary>
    internal const uint Name = 0x6E616D65;

    /// <summary>The glyph substitution table, "GSUB".</summary>
    internal const uint Gsub = 0x47535542;

    /// <summary>The glyph positioning table, "GPOS".</summary>
    internal const uint Gpos = 0x47504F53;

    /// <summary>The glyph definition table, "GDEF".</summary>
    internal const uint Gdef = 0x47444546;

    /// <summary>The legacy kerning table, "kern".</summary>
    internal const uint Kern = 0x6B65726E;

    /// <summary>The compact font format table, "CFF ".</summary>
    internal const uint Cff = 0x43464620;

    /// <summary>The compact font format 2 table, "CFF2".</summary>
    internal const uint Cff2 = 0x43464632;

    /// <summary>The font variations table, "fvar".</summary>
    internal const uint Fvar = 0x66766172;

    /// <summary>The control value table, "cvt ".</summary>
    internal const uint Cvt = 0x63767420;

    /// <summary>The font program table, "fpgm".</summary>
    internal const uint Fpgm = 0x6670676D;

    /// <summary>The control value program table, "prep".</summary>
    internal const uint Prep = 0x70726570;

    /// <summary>The vertical header table, "vhea".</summary>
    internal const uint Vhea = 0x76686561;

    /// <summary>The vertical metrics table, "vmtx".</summary>
    internal const uint Vmtx = 0x766D7478;

    /// <summary>The vertical origin table, "VORG".</summary>
    internal const uint Vorg = 0x564F5247;

    /// <summary>The embedded bitmap data table, "EBDT".</summary>
    internal const uint Ebdt = 0x45424454;

    /// <summary>The colour bitmap data table, "CBDT".</summary>
    internal const uint Cbdt = 0x43424454;

    /// <summary>The offset of the checksum adjustment in the font header.</summary>
    internal const int HeadChecksumAdjustment = 8;

    /// <summary>The value the checksum of a whole font adds up to.</summary>
    internal const uint ChecksumMagic = 0xB1B0AFBA;
}
