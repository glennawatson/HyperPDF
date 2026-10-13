// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Text.Fonts;

/// <summary>The four byte tags of the font tables read or written, as big-endian numbers.</summary>
internal static class SfntTag
{
    /// <summary>Gets the font header.</summary>
    internal static uint Head => 0x68656164; // head

    /// <summary>Gets the horizontal header.</summary>
    internal static uint Hhea => 0x68686561; // hhea

    /// <summary>Gets the horizontal metrics.</summary>
    internal static uint Hmtx => 0x686D7478; // hmtx

    /// <summary>Gets the maximum profile.</summary>
    internal static uint Maxp => 0x6D617870; // maxp

    /// <summary>Gets the character to glyph map.</summary>
    internal static uint Cmap => 0x636D6170; // cmap

    /// <summary>Gets the naming table.</summary>
    internal static uint Name => 0x6E616D65; // name

    /// <summary>Gets the OS/2 and Windows metrics.</summary>
    internal static uint Os2 => 0x4F532F32; // OS/2

    /// <summary>Gets the PostScript table.</summary>
    internal static uint Post => 0x706F7374; // post

    /// <summary>Gets the glyph data.</summary>
    internal static uint Glyf => 0x676C7966; // glyf

    /// <summary>Gets the glyph locations.</summary>
    internal static uint Loca => 0x6C6F6361; // loca

    /// <summary>Gets the kerning table.</summary>
    internal static uint Kern => 0x6B65726E; // kern

    /// <summary>Gets the compact font format outlines.</summary>
    internal static uint Cff => 0x43464620; // "CFF "

    /// <summary>Gets the variable font outlines table, which PDF cannot embed.</summary>
    internal static uint Cff2 => 0x43464632; // "CFF2"

    /// <summary>Gets the font variations table, which marks a variable font.</summary>
    internal static uint Fvar => 0x66766172; // fvar

    /// <summary>Gets the control value table.</summary>
    internal static uint Cvt => 0x63767420; // "cvt "

    /// <summary>Gets the font program.</summary>
    internal static uint Fpgm => 0x6670676D; // fpgm

    /// <summary>Gets the control value program.</summary>
    internal static uint Prep => 0x70726570; // prep

    /// <summary>Gets the grid-fitting and scan-conversion procedure table.</summary>
    internal static uint Gasp => 0x67617370; // gasp

    /// <summary>Gets the collection header.</summary>
    internal static uint Ttcf => 0x74746366; // ttcf
}
