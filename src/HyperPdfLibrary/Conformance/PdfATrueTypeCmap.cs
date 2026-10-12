// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>One 'cmap' subtable of a TrueType program as the PDF/A rules read it: formats 0, 4 and 6 fill the map.</summary>
/// <param name="PlatformId">The platform id.</param>
/// <param name="EncodingId">The encoding id.</param>
/// <param name="Offset">The subtable offset in the 'cmap' table.</param>
[DebuggerDisplay("PdfATrueTypeCmap: ({PlatformId}, {EncodingId}), {Map.Count} codes")]
internal sealed record PdfATrueTypeCmap(int PlatformId, int EncodingId, long Offset)
{
    /// <summary>The first code mapped, or -1.</summary>
    private int _sampleCode = -1;

    /// <summary>Gets the glyph of each mapped code.</summary>
    internal Dictionary<int, int> Map { get; } = [];

    /// <summary>Gets the first code mapped while reading, or -1; veraPDF reads a (3, 0) subtable's code page from it.</summary>
    internal int SampleCode => _sampleCode;

    /// <summary>Maps a code.</summary>
    /// <param name="code">The code.</param>
    /// <param name="glyph">The glyph.</param>
    internal void Put(int code, int glyph)
    {
        if (_sampleCode == -1)
        {
            _sampleCode = code;
        }

        Map[code] = glyph;
    }

    /// <summary>Gets the glyph of a code.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The glyph, or zero when the code is not mapped.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Glyph(int code) => Map.GetValueOrDefault(code);
}
