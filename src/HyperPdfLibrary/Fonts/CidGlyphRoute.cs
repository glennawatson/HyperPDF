// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// How a composite font with an embedded program turns a CID into a glyph id: through /CIDToGIDMap for TrueType
/// programs and through the charset of a CID-keyed CFF program (ISO 32000-2, 9.7.4.2).
/// </summary>
/// <param name="CidToGid">The /CIDToGIDMap stream bytes, two per CID, or <see langword="null"/>.</param>
/// <param name="CidKeyed">The CID-keyed CFF program that maps CIDs through its charset, or <see langword="null"/>.</param>
[DebuggerDisplay("CidGlyphRoute")]
internal sealed record CidGlyphRoute(byte[]? CidToGid, CffProgram? CidKeyed)
{
    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Gets the route of a font whose CIDs are glyph ids.</summary>
    internal static CidGlyphRoute Identity { get; } = new(null, null);

    /// <summary>Maps a CID to a glyph id.</summary>
    /// <param name="cid">The CID.</param>
    /// <returns>The glyph id, or -1.</returns>
    internal int GlyphOf(int cid)
    {
        if (CidToGid is { } table)
        {
            var offset = (long)cid * (1 + 1);
            return cid >= 0 && offset + 1 < table.Length ? (table[(int)offset] << ByteBits) | table[(int)offset + 1] : -1;
        }

        return CidKeyed is { } cff ? cff.GetGlyphByCid(cid) : cid;
    }
}
