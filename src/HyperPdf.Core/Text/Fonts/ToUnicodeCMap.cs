// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdf.Core.Text.Fonts;

/// <summary>
/// Writes the <c>ToUnicode</c> map of an embedded font: which text each glyph shows. With it, text written in shaped
/// glyphs (ligatures, joined scripts) can still be selected, searched, copied and read aloud.
/// </summary>
public static class ToUnicodeCMap
{
    /// <summary>The most entries a <c>beginbfchar</c> block may hold.</summary>
    private const int BlockEntries = 100;

    /// <summary>The characters each entry usually takes.</summary>
    private const int EntryChars = 20;

    /// <summary>The characters the map's fixed text takes.</summary>
    private const int FrameChars = 400;

    /// <summary>Writes a map from two-byte glyph codes to text.</summary>
    /// <param name="entries">Each glyph code and the text it shows; codes without text are left out.</param>
    /// <returns>The CMap program.</returns>
    public static string Write(IReadOnlyList<(ushort Code, string Text)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var builder = new StringBuilder(FrameChars + (entries.Count * EntryChars));
        _ = builder.Append("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n")
            .Append("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n")
            .Append("/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n")
            .Append("1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
        var written = 0;
        while (written < entries.Count)
        {
            var block = 0;
            for (var i = written; i < entries.Count && block < BlockEntries; i++)
            {
                block += entries[i].Text.Length > 0 ? 1 : 0;
            }

            _ = builder.Append(CultureInfo.InvariantCulture, $"{block} beginbfchar\n");
            var inBlock = 0;
            while (written < entries.Count && inBlock < BlockEntries)
            {
                var (code, text) = entries[written];
                written++;
                if (text.Length == 0)
                {
                    continue;
                }

                AppendEntry(builder, code, text);
                inBlock++;
            }

            _ = builder.Append("endbfchar\n");
        }

        return builder.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n").ToString();
    }

    /// <summary>Appends one glyph code and its UTF-16 text.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="code">The glyph code.</param>
    /// <param name="text">The text.</param>
    private static void AppendEntry(StringBuilder builder, ushort code, string text)
    {
        _ = builder.Append(CultureInfo.InvariantCulture, $"<{code:X4}> <");
        foreach (var c in text)
        {
            _ = builder.Append(CultureInfo.InvariantCulture, $"{(int)c:X4}");
        }

        _ = builder.Append(">\n");
    }
}
