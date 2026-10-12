// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>Rewrites dictionaries and arrays as text, for objects redefined in an incremental update.</summary>
internal static class PdfEditing
{
    /// <summary>The closing <c>&gt;&gt;</c> of a dictionary.</summary>
    private const int CloserLength = 2;

    /// <summary>Removes a key and its value from a dictionary.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key without its slash.</param>
    /// <returns>The dictionary without the key.</returns>
    internal static string RemoveKey(ReadOnlySpan<byte> dictionary, ReadOnlySpan<byte> key)
    {
        var value = PdfSyntax.FindKey(dictionary, 0, key);
        if (value < 0)
        {
            return Encoding.Latin1.GetString(dictionary);
        }

        var keyStart = dictionary[..value].LastIndexOf((byte)'/');
        var valueEnd = PdfSyntax.ValueEnd(dictionary, value);
        return Encoding.Latin1.GetString(dictionary[..keyStart]) + Encoding.Latin1.GetString(dictionary[valueEnd..]);
    }

    /// <summary>
    /// Adds an entry just before a dictionary's closing <c>&gt;&gt;</c>, on a line of its own so a comment ending
    /// the dictionary's last line cannot swallow it.
    /// </summary>
    /// <param name="dictionary">The dictionary text.</param>
    /// <param name="entry">The entry, for example <c>/SigFlags 3</c>.</param>
    /// <returns>The dictionary with the entry.</returns>
    internal static string AddEntry(string dictionary, string entry)
    {
        var close = dictionary.TrimEnd().Length - CloserLength;
        return $"{dictionary[..close].TrimEnd()}\n{entry}\n>>";
    }

    /// <summary>Appends an item to an array's contents, on a line of its own so a trailing comment cannot swallow it.</summary>
    /// <param name="array">The array, or empty for a new one.</param>
    /// <param name="item">The item.</param>
    /// <returns>The array text.</returns>
    internal static string Append(ReadOnlySpan<byte> array, string item)
    {
        if (array.IsEmpty || array[0] != (byte)'[')
        {
            return $"[{item}]";
        }

        var text = Encoding.Latin1.GetString(array).TrimEnd();
        return $"{text[..^1].TrimEnd()}\n{item}]";
    }

    /// <summary>Writes text as a PDF hex string in UTF-16BE with a byte order mark, safe for any characters.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The hex string, for example <c>&lt;FEFF0041&gt;</c>.</returns>
    internal static string HexText(string text) => $"<FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text))}>";
}
