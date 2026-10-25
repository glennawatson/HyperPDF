// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets.Shaping;

/// <summary>Writes a GDEF table (OpenType, "GDEF"): glyph classes, mark attachment classes and mark glyph sets.</summary>
public static class GdefTable
{
    /// <summary>Gets the GDEF class of base glyphs.</summary>
    public static int Base { get; } = 1;

    /// <summary>Gets the GDEF class of ligature glyphs.</summary>
    public static int Ligature { get; } = 0x2;

    /// <summary>Gets the GDEF class of mark glyphs.</summary>
    public static int Mark { get; } = 0x3;

    /// <summary>Writes the table.</summary>
    /// <param name="glyphClasses">The class of each classified glyph.</param>
    /// <param name="markAttachClasses">The mark attachment class of each mark, or <see langword="null"/> for none.</param>
    /// <param name="markSets">The glyphs of each mark glyph set, or <see langword="null"/> for none.</param>
    /// <returns>The table.</returns>
    public static byte[] Build(IReadOnlyDictionary<int, int> glyphClasses, IReadOnlyDictionary<int, int>? markAttachClasses, int[][]? markSets)
    {
        var buffer = new ByteBuffer();
        buffer.Write(1, markSets is null ? 0 : 0x2);
        var classDef = buffer.Reserve();
        buffer.Write(0, 0);
        var attachDef = buffer.Reserve();
        var setsAt = markSets is null ? -1 : buffer.Reserve();
        buffer.AttachChild(classDef, CommonTables.ClassDef(glyphClasses));
        if (markAttachClasses is not null)
        {
            buffer.AttachChild(attachDef, CommonTables.ClassDef(markAttachClasses));
        }

        if (markSets is not null)
        {
            buffer.AttachChild(setsAt, MarkSets(markSets));
        }

        return buffer.ToArray();
    }

    /// <summary>Writes a Mark Glyph Sets table, whose offsets are 32 bits wide.</summary>
    /// <param name="sets">The glyphs of each set.</param>
    /// <returns>The table.</returns>
    private static byte[] MarkSets(int[][] sets)
    {
        var buffer = new ByteBuffer();
        buffer.Write(1, sets.Length);
        var slots = new List<int>();
        foreach (var set in sets)
        {
            slots.Add(buffer.Reserve());
            buffer.Write(0);
        }

        for (var i = 0; i < sets.Length; i++)
        {
            var at = buffer.Length;
            buffer.WriteBytes(CommonTables.Coverage(sets[i]));
            buffer.Patch(slots[i], at >> 0x10);
            buffer.Patch(slots[i] + 0x2, at & 0xFFFF);
        }

        return buffer.ToArray();
    }
}
