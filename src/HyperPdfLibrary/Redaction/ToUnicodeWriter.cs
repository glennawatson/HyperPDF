// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Redaction;

/// <summary>Rewrites a /ToUnicode CMap without the entries of some character codes.</summary>
internal static class ToUnicodeWriter
{
    /// <summary>The entries written in one bfchar or bfrange block.</summary>
    private const int BlockSize = 100;

    /// <summary>The widest code range expanded entry by entry; wider ranges are kept as ranges.</summary>
    private const long MaxExpand = 1 << 20;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The hex digits in a byte, and the items in a low and high pair.</summary>
    private const int PairSize = 2;

    /// <summary>The part of a CMap being read.</summary>
    private enum Section
    {
        /// <summary>Outside any block.</summary>
        None = 0,

        /// <summary>Inside a bfchar block.</summary>
        Char = 1,

        /// <summary>Inside a bfrange block.</summary>
        Range = 2,

        /// <summary>Inside a codespacerange block.</summary>
        Space = 3,
    }

    /// <summary>Removes codes from a CMap.</summary>
    /// <param name="cmap">The decoded CMap.</param>
    /// <param name="removed">The codes to drop.</param>
    /// <param name="dropped">Receives the number of mapped codes dropped.</param>
    /// <returns>The new CMap, or <see langword="null"/> when none of the codes were mapped.</returns>
    internal static byte[]? Remove(ReadOnlySpan<byte> cmap, HashSet<int> removed, out int dropped)
    {
        var parsed = Parse(cmap);
        var chars = new List<CharEntry>();
        var ranges = new List<RangeEntry>();
        dropped = 0;
        foreach (var entry in parsed.Chars)
        {
            if (removed.Contains(entry.Code))
            {
                dropped++;
            }
            else
            {
                chars.Add(entry);
            }
        }

        foreach (var range in parsed.Ranges)
        {
            dropped += Split(range, removed, chars, ranges);
        }

        return dropped == 0 ? null : Build(parsed.CodeSpace, chars, ranges);
    }

    /// <summary>Reads a CMap.</summary>
    /// <param name="cmap">The bytes.</param>
    /// <returns>Its code space, single mappings and ranges.</returns>
    private static ParsedMap Parse(ReadOnlySpan<byte> cmap)
    {
        var chars = new List<CharEntry>();
        var ranges = new List<RangeEntry>();
        var space = new List<byte[]>();
        var lexer = new PdfLexer(cmap);
        var mode = Section.None;
        var pending = new List<byte[]>();
        var inArray = false;
        var arrayItems = new List<byte[]>();
        var kind = lexer.Next();
        while (kind != PdfTokenKind.EndOfData)
        {
            if (kind == PdfTokenKind.Keyword)
            {
                mode = ChangeSection(lexer.Lexeme, mode);
                pending.Clear();
            }
            else if (kind == PdfTokenKind.ArrayStart)
            {
                inArray = true;
                arrayItems = [];
            }
            else if (kind == PdfTokenKind.ArrayEnd)
            {
                inArray = false;
                pending.Add([]);
                Complete(mode, pending, chars, ranges, space, arrayItems);
            }
            else if (kind == PdfTokenKind.HexString)
            {
                var bytes = DecodeHex(lexer.Lexeme);
                if (inArray)
                {
                    arrayItems.Add(bytes);
                }
                else
                {
                    pending.Add(bytes);
                    Complete(mode, pending, chars, ranges, space, null);
                }
            }

            kind = lexer.Next();
        }

        return new(space, chars, ranges);
    }

    /// <summary>Works out the section a keyword starts or ends.</summary>
    /// <param name="keyword">The keyword.</param>
    /// <param name="current">The current section.</param>
    /// <returns>The new section.</returns>
    private static Section ChangeSection(ReadOnlySpan<byte> keyword, Section current)
    {
        if (keyword.SequenceEqual("beginbfchar"u8))
        {
            return Section.Char;
        }

        if (keyword.SequenceEqual("beginbfrange"u8))
        {
            return Section.Range;
        }

        if (keyword.SequenceEqual("begincodespacerange"u8))
        {
            return Section.Space;
        }

        return keyword.StartsWith("end"u8) ? Section.None : current;
    }

    /// <summary>Completes an entry once enough values have been read for the section.</summary>
    /// <param name="mode">The section.</param>
    /// <param name="pending">The values read.</param>
    /// <param name="chars">Receives single mappings.</param>
    /// <param name="ranges">Receives ranges.</param>
    /// <param name="space">Receives code space ranges.</param>
    /// <param name="array">The array a range took its targets from, or null.</param>
    private static void Complete(Section mode, List<byte[]> pending, List<CharEntry> chars, List<RangeEntry> ranges, List<byte[]> space, List<byte[]>? array)
    {
        switch (mode)
        {
            case Section.Char when pending.Count == 2:
            {
                chars.Add(new(ToCode(pending[0]), pending[0].Length, pending[1]));
                pending.Clear();
                break;
            }

            case Section.Space when pending.Count == 2:
            {
                space.Add(pending[0]);
                space.Add(pending[1]);
                pending.Clear();
                break;
            }

            case Section.Range when pending.Count == 3:
            {
                ranges.Add(new(ToCode(pending[0]), ToCode(pending[1]), pending[0].Length, array is null ? pending[2] : [], array));
                pending.Clear();
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Splits a range around removed codes.</summary>
    /// <param name="range">The range.</param>
    /// <param name="removed">The codes to drop.</param>
    /// <param name="chars">Receives single mappings.</param>
    /// <param name="ranges">Receives ranges.</param>
    /// <returns>The number of mapped codes dropped.</returns>
    private static int Split(RangeEntry range, HashSet<int> removed, List<CharEntry> chars, List<RangeEntry> ranges)
    {
        var dropped = 0;
        var start = -1;
        for (var code = range.Low; code <= range.High && (code - range.Low) <= MaxExpand; code++)
        {
            if (removed.Contains(code))
            {
                dropped++;
                FlushRun(range, start, code - 1, chars, ranges);
                start = -1;
                continue;
            }

            if (range.Targets is not null)
            {
                FlushRun(range, start, code - 1, chars, ranges);
                start = -1;
                var index = code - range.Low;
                if (index < range.Targets.Count)
                {
                    chars.Add(new(code, range.Width, range.Targets[index]));
                }

                continue;
            }

            start = start < 0 ? code : start;
        }

        FlushRun(range, start, Math.Min(range.High, range.Low + (int)MaxExpand), chars, ranges);
        return dropped;
    }

    /// <summary>Writes a run of kept codes of a range as a range of its own.</summary>
    /// <param name="range">The original range.</param>
    /// <param name="start">The first kept code, or -1 when no run is open.</param>
    /// <param name="end">The last kept code.</param>
    /// <param name="chars">Receives single mappings.</param>
    /// <param name="ranges">Receives ranges.</param>
    private static void FlushRun(RangeEntry range, int start, int end, List<CharEntry> chars, List<RangeEntry> ranges)
    {
        if (start < 0 || end < start)
        {
            return;
        }

        var target = Add(range.Target, start - range.Low);
        if (start == end)
        {
            chars.Add(new(start, range.Width, target));
            return;
        }

        ranges.Add(new(start, end, range.Width, target, null));
    }

    /// <summary>Adds to the last bytes of a big-endian target, as a range steps through its targets.</summary>
    /// <param name="target">The target.</param>
    /// <param name="step">The steps.</param>
    /// <returns>The new target.</returns>
    private static byte[] Add(byte[] target, int step)
    {
        var result = (byte[])target.Clone();
        var carry = step;
        for (var i = result.Length - 1; i >= 0 && carry != 0; i--)
        {
            carry += result[i];
            result[i] = (byte)(carry & 0xFF);
            carry >>= ByteBits;
        }

        return result;
    }

    /// <summary>Writes a CMap.</summary>
    /// <param name="space">The code space ranges, pairs of low and high.</param>
    /// <param name="chars">The single mappings.</param>
    /// <param name="ranges">The ranges.</param>
    /// <returns>The CMap bytes.</returns>
    private static byte[] Build(List<byte[]> space, List<CharEntry> chars, List<RangeEntry> ranges)
    {
        var text = new StringBuilder();
        _ = text.Append("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n");
        AppendSpace(text, space);
        AppendChars(text, chars);
        AppendRanges(text, ranges);
        _ = text.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n");
        return Encoding.ASCII.GetBytes(text.ToString());
    }

    /// <summary>Writes the code space.</summary>
    /// <param name="text">The output.</param>
    /// <param name="space">The pairs.</param>
    private static void AppendSpace(StringBuilder text, List<byte[]> space)
    {
        if (space.Count == 0)
        {
            return;
        }

        _ = text.Append(CultureInfo.InvariantCulture, $"{space.Count / PairSize} begincodespacerange\n");
        for (var i = 0; i + 1 < space.Count; i += PairSize)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"<{Convert.ToHexString(space[i])}> <{Convert.ToHexString(space[i + 1])}>\n");
        }

        _ = text.Append("endcodespacerange\n");
    }

    /// <summary>Writes the single mappings in blocks.</summary>
    /// <param name="text">The output.</param>
    /// <param name="chars">The mappings.</param>
    private static void AppendChars(StringBuilder text, List<CharEntry> chars)
    {
        for (var start = 0; start < chars.Count; start += BlockSize)
        {
            var count = Math.Min(BlockSize, chars.Count - start);
            _ = text.Append(CultureInfo.InvariantCulture, $"{count} beginbfchar\n");
            for (var i = start; i < start + count; i++)
            {
                _ = text.Append(CultureInfo.InvariantCulture, $"<{Hex(chars[i].Code, chars[i].Width)}> <{Convert.ToHexString(chars[i].Target)}>\n");
            }

            _ = text.Append("endbfchar\n");
        }
    }

    /// <summary>Writes the ranges in blocks.</summary>
    /// <param name="text">The output.</param>
    /// <param name="ranges">The ranges.</param>
    private static void AppendRanges(StringBuilder text, List<RangeEntry> ranges)
    {
        for (var start = 0; start < ranges.Count; start += BlockSize)
        {
            var count = Math.Min(BlockSize, ranges.Count - start);
            _ = text.Append(CultureInfo.InvariantCulture, $"{count} beginbfrange\n");
            for (var i = start; i < start + count; i++)
            {
                var range = ranges[i];
                _ = text.Append(CultureInfo.InvariantCulture, $"<{Hex(range.Low, range.Width)}> <{Hex(range.High, range.Width)}> <{Convert.ToHexString(range.Target)}>\n");
            }

            _ = text.Append("endbfrange\n");
        }
    }

    /// <summary>Formats a code as hex digits of a width.</summary>
    /// <param name="code">The code.</param>
    /// <param name="bytes">The width in bytes.</param>
    /// <returns>The digits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Hex(int code, int bytes) => code.ToString(string.Create(CultureInfo.InvariantCulture, $"X{bytes * PairSize}"), CultureInfo.InvariantCulture);

    /// <summary>Reads a code from big-endian bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The code.</returns>
    private static int ToCode(byte[] bytes)
    {
        var code = 0;
        foreach (var b in bytes)
        {
            code = (code << ByteBits) | b;
        }

        return code;
    }

    /// <summary>Decodes the digits of a hex string.</summary>
    /// <param name="digits">The digits between the angle brackets.</param>
    /// <returns>The bytes.</returns>
    private static byte[] DecodeHex(ReadOnlySpan<byte> digits)
    {
        var buffer = new byte[(digits.Length / PairSize) + 1];
        var length = PdfStringDecoder.DecodeHex(digits, buffer);
        return buffer.AsSpan(0, length).ToArray();
    }

    /// <summary>A CMap's content.</summary>
    /// <param name="CodeSpace">The code space ranges, pairs of low and high.</param>
    /// <param name="Chars">The single mappings.</param>
    /// <param name="Ranges">The ranges.</param>
    private sealed record ParsedMap(List<byte[]> CodeSpace, List<CharEntry> Chars, List<RangeEntry> Ranges);

    /// <summary>One code mapped to text.</summary>
    /// <param name="Code">The code.</param>
    /// <param name="Width">The code's width in bytes.</param>
    /// <param name="Target">The UTF-16BE text.</param>
    private sealed record CharEntry(int Code, int Width, byte[] Target);

    /// <summary>A range of codes mapped to text.</summary>
    /// <param name="Low">The first code.</param>
    /// <param name="High">The last code.</param>
    /// <param name="Width">The codes' width in bytes.</param>
    /// <param name="Target">The text of the first code, for a range that steps through targets.</param>
    /// <param name="Targets">The text of each code, for a range given as an array, or null.</param>
    private sealed record RangeEntry(int Low, int High, int Width, byte[] Target, List<byte[]>? Targets);
}
