// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>The pieces of a subset CFF font, ready to be placed in the file.</summary>
/// <param name="Name">The Name INDEX.</param>
/// <param name="Strings">The String INDEX.</param>
/// <param name="GlobalSubrs">The Global Subr INDEX.</param>
/// <param name="Charset">The charset.</param>
/// <param name="Select">The FDSelect table; empty for a font that is not CID-keyed.</param>
/// <param name="CharStrings">The CharStrings INDEX.</param>
/// <param name="Blocks">Each output Font DICT's Private DICT followed by its local subroutines.</param>
/// <param name="Map">The output number of each source Font DICT, or -1 when it is dropped.</param>
/// <param name="CidCount">The CID count of a CID-keyed font.</param>
/// <param name="Ros">The registry SID, ordering SID and supplement of a CID-keyed font.</param>
/// <param name="IsCid">Whether the written font is CID-keyed; a renumbered subset always is, so CIDs are the glyph numbers.</param>
[DebuggerDisplay("CffSections: {Blocks.Length} private blocks")]
internal sealed record CffSections(
    byte[] Name,
    byte[] Strings,
    byte[] GlobalSubrs,
    byte[] Charset,
    byte[] Select,
    byte[] CharStrings,
    byte[][] Blocks,
    int[] Map,
    int CidCount,
    int[] Ros,
    bool IsCid);
