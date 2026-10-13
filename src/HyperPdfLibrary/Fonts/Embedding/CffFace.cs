// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>The parts of a CFF font a subset is built from.</summary>
/// <param name="Data">The CFF data.</param>
/// <param name="IsCid">Whether the font is CID-keyed (has a ROS entry).</param>
/// <param name="CharStrings">The CharStrings INDEX.</param>
/// <param name="GlobalSubrs">The Global Subr INDEX.</param>
/// <param name="Strings">The String INDEX.</param>
/// <param name="Charset">The SID of each glyph, or its CID in a CID-keyed font.</param>
/// <param name="FdSelect">The Font DICT of each glyph; empty when the font is not CID-keyed.</param>
/// <param name="Dicts">The Font DICTs; one entry, holding the Private DICT, when the font is not CID-keyed.</param>
/// <param name="FontBox">The <c>FontBBox</c> entry's bytes, or empty.</param>
/// <param name="FontMatrix">The <c>FontMatrix</c> entry's bytes, or empty.</param>
/// <param name="Ros">The registry SID, ordering SID and supplement of a CID-keyed font.</param>
[DebuggerDisplay("CffFace: {CharStrings.Count} glyphs, cid {IsCid}")]
internal sealed record CffFace(
    ReadOnlyMemory<byte> Data,
    bool IsCid,
    CffIndex CharStrings,
    CffIndex GlobalSubrs,
    CffIndex Strings,
    ushort[] Charset,
    byte[] FdSelect,
    CffFontDict[] Dicts,
    byte[] FontBox,
    byte[] FontMatrix,
    int[] Ros)
{
    /// <summary>Gets the number of glyphs.</summary>
    internal int GlyphCount => CharStrings.Count;

    /// <summary>Gets the Font DICT a glyph uses.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The index of the glyph's Font DICT.</returns>
    internal int DictOf(int glyph) => IsCid && (uint)glyph < (uint)FdSelect.Length ? FdSelect[glyph] : 0;
}
