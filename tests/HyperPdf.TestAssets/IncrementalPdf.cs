// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdf.TestAssets;

/// <summary>Appends incremental updates to files built by <see cref="MiniPdf"/>, as a signing or editing tool would.</summary>
public static class IncrementalPdf
{
    /// <summary>Gets the length of the "/Size " key and its space.</summary>
    private static readonly int SizeKeyLength = "/Size ".Length;

    /// <summary>Gets the length of the "startxref" keyword and its line end.</summary>
    private static readonly int StartXrefLength = "startxref\n".Length;

    /// <summary>Appends an update that adds or replaces objects; object 1 stays the catalog.</summary>
    /// <param name="file">The file so far.</param>
    /// <param name="objects">The object bodies by number.</param>
    /// <returns>The file with the update appended.</returns>
    public static byte[] Append(byte[] file, IReadOnlyDictionary<int, string> objects)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(objects);
        var latin1 = Encoding.Latin1;
        var text = latin1.GetString(file);
        var previousXref = ReadNumberAfter(text, text.LastIndexOf("startxref\n", StringComparison.Ordinal) + StartXrefLength);
        var size = ReadNumberAfter(text, text.LastIndexOf("/Size ", StringComparison.Ordinal) + SizeKeyLength);
        var update = new StringBuilder();
        var offsets = new SortedDictionary<int, int>();
        foreach (var (number, body) in objects)
        {
            offsets[number] = file.Length + latin1.GetByteCount(update.ToString());
            _ = update.Append(CultureInfo.InvariantCulture, $"{number} 0 obj\n{body}\nendobj\n");
            size = Math.Max(size, number + 1);
        }

        var xref = file.Length + latin1.GetByteCount(update.ToString());
        _ = update.Append("xref\n");
        foreach (var (number, offset) in offsets)
        {
            _ = update.Append(CultureInfo.InvariantCulture, $"{number} 1\n{offset:D10} 00000 n \n");
        }

        _ = update.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {size} /Root 1 0 R /Prev {previousXref} >>\nstartxref\n{xref}\n%%EOF\n");
        return [.. file, .. latin1.GetBytes(update.ToString())];
    }

    /// <summary>Reads the decimal number at a position.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The first digit.</param>
    /// <returns>The number.</returns>
    private static int ReadNumberAfter(string text, int start)
    {
        var end = start;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        return int.Parse(text.AsSpan(start, end - start), CultureInfo.InvariantCulture);
    }
}
