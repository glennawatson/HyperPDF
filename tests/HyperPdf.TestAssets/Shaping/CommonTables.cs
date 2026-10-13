// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets.Shaping;

/// <summary>Writes the tables GSUB, GPOS and GDEF share: Coverage and Class Definition tables.</summary>
public static class CommonTables
{
    /// <summary>Writes a format 1 Coverage table.</summary>
    /// <param name="glyphs">The glyphs, in any order.</param>
    /// <returns>The table.</returns>
    public static byte[] Coverage(IEnumerable<int> glyphs)
    {
        var sorted = glyphs.Distinct().Order().ToArray();
        return ByteBuffer.Words([1, sorted.Length, .. sorted]);
    }

    /// <summary>Writes a format 2 Class Definition table with a range for each glyph.</summary>
    /// <param name="classes">The class of each listed glyph.</param>
    /// <returns>The table.</returns>
    public static byte[] ClassDef(IReadOnlyDictionary<int, int> classes)
    {
        var glyphs = classes.Keys.Order().ToArray();
        var words = new List<int> { 0x2, glyphs.Length };
        foreach (var glyph in glyphs)
        {
            words.AddRange([glyph, glyph, classes[glyph]]);
        }

        return ByteBuffer.Words([.. words]);
    }
}
