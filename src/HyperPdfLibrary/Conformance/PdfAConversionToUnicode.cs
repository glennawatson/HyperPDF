// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Writes a ToUnicode CMap for a simple font with one-byte codes, so every character has text (ISO 32000-1 §9.10.3;
/// required at PDF/A levels A and U by ISO 19005-2 §6.2.11.7.2).
/// </summary>
internal static class PdfAConversionToUnicode
{
    /// <summary>The most entries a <c>beginbfchar</c> block may hold.</summary>
    private const int BlockEntries = 100;

    /// <summary>Gets the CMap's opening.</summary>
    private static ReadOnlySpan<byte> Header => """
        /CIDInit /ProcSet findresource begin
        12 dict begin
        begincmap
        /CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def
        /CMapName /Adobe-Identity-UCS def
        /CMapType 2 def
        1 begincodespacerange
        <00> <FF>
        endcodespacerange

        """u8;

    /// <summary>Gets the CMap's closing.</summary>
    private static ReadOnlySpan<byte> Footer => "endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n"u8;

    /// <summary>Writes the map.</summary>
    /// <param name="texts">The text of each code, by code.</param>
    /// <returns>The CMap bytes.</returns>
    internal static byte[] Write(SortedDictionary<int, string> texts)
    {
        var builder = new StringBuilder(Encoding.ASCII.GetString(Header));
        var block = new List<KeyValuePair<int, string>>(BlockEntries);
        foreach (var entry in texts)
        {
            block.Add(entry);
            if (block.Count < BlockEntries)
            {
                continue;
            }

            AppendBlock(builder, block);
            block.Clear();
        }

        if (block.Count > 0)
        {
            AppendBlock(builder, block);
        }

        _ = builder.Append(Encoding.ASCII.GetString(Footer));
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    /// <summary>Appends one <c>beginbfchar</c> block.</summary>
    /// <param name="builder">The CMap text.</param>
    /// <param name="block">The entries, at most 100.</param>
    private static void AppendBlock(StringBuilder builder, List<KeyValuePair<int, string>> block)
    {
        _ = builder.Append(CultureInfo.InvariantCulture, $"{block.Count} beginbfchar\n");
        foreach (var (code, text) in block)
        {
            _ = builder.Append(CultureInfo.InvariantCulture, $"<{code:X2}> <");
            foreach (var unit in text)
            {
                _ = builder.Append(CultureInfo.InvariantCulture, $"{(int)unit:X4}");
            }

            _ = builder.Append(">\n");
        }

        _ = builder.Append("endbfchar\n");
    }
}
