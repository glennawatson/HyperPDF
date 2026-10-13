// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Reads names from the 'name' table.</summary>
internal static class SfntNames
{
    /// <summary>The name id of the family.</summary>
    internal const int FamilyId = 1;

    /// <summary>The name id of the PostScript name.</summary>
    internal const int PostScriptId = 6;

    /// <summary>The name id of the typographic family, which groups more styles than the family.</summary>
    internal const int TypographicFamilyId = 16;

    /// <summary>The size of the table header.</summary>
    private const int HeaderSize = 6;

    /// <summary>The offset of the record count.</summary>
    private const int CountOffset = 2;

    /// <summary>The offset of the string storage.</summary>
    private const int StorageOffset = 4;

    /// <summary>The size of a name record.</summary>
    private const int RecordSize = 12;

    /// <summary>The offset of a record's platform.</summary>
    private const int PlatformOffset = 0;

    /// <summary>The offset of a record's name id.</summary>
    private const int IdOffset = 6;

    /// <summary>The offset of a record's string length.</summary>
    private const int LengthOffset = 8;

    /// <summary>The offset of a record's string offset.</summary>
    private const int StringOffset = 10;

    /// <summary>The Unicode platform.</summary>
    private const int UnicodePlatform = 0;

    /// <summary>The Macintosh platform.</summary>
    private const int MacintoshPlatform = 1;

    /// <summary>The Windows platform.</summary>
    private const int WindowsPlatform = 3;

    /// <summary>The longest PostScript name PDF readers are told to expect.</summary>
    private const int MaxPostScriptLength = 63;

    /// <summary>The lowest printable ASCII character a name may hold.</summary>
    private const int FirstPrintable = 33;

    /// <summary>The highest printable ASCII character a name may hold.</summary>
    private const int LastPrintable = 126;

    /// <summary>Gets the characters PDF names and PostScript names may not hold.</summary>
    private static ReadOnlySpan<char> Delimiters => "()<>[]{}/%";

    /// <summary>Finds a name, preferring Windows and Unicode records over Macintosh ones.</summary>
    /// <param name="table">The 'name' table.</param>
    /// <param name="id">The name id.</param>
    /// <returns>The name, or <see langword="null"/> when the table has none.</returns>
    internal static string? Find(ReadOnlySpan<byte> table, int id)
    {
        var count = FontBytes.U16(table, CountOffset);
        var storage = FontBytes.U16(table, StorageOffset);
        string? mac = null;
        for (var i = 0; i < count; i++)
        {
            var record = HeaderSize + (i * RecordSize);
            if (FontBytes.U16(table, record + IdOffset) != id)
            {
                continue;
            }

            var bytes = FontBytes.Slice(table, storage + FontBytes.U16(table, record + StringOffset), FontBytes.U16(table, record + LengthOffset));
            var platform = FontBytes.U16(table, record + PlatformOffset);
            if (platform is WindowsPlatform or UnicodePlatform && !bytes.IsEmpty)
            {
                return Encoding.BigEndianUnicode.GetString(bytes);
            }

            if (platform == MacintoshPlatform && !bytes.IsEmpty)
            {
                mac ??= Encoding.Latin1.GetString(bytes);
            }
        }

        return mac;
    }

    /// <summary>Makes a PostScript name that is also a valid PDF name: printable ASCII without spaces or delimiters.</summary>
    /// <param name="name">The name, which may be <see langword="null"/>.</param>
    /// <param name="fallback">The name to use when nothing usable is left.</param>
    /// <returns>The cleaned name.</returns>
    internal static string ToPostScriptName(string? name, string fallback)
    {
        var cleaned = Clean(name);
        if (cleaned.Length == 0)
        {
            cleaned = Clean(fallback);
        }

        return cleaned.Length == 0 ? "EmbeddedFont" : cleaned;
    }

    /// <summary>Removes characters a PDF name cannot hold, and shortens the name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The cleaned name.</returns>
    private static string Clean(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(name.Length, MaxPostScriptLength));
        foreach (var c in name)
        {
            if (c is >= (char)FirstPrintable and <= (char)LastPrintable && !Delimiters.Contains(c) && builder.Length < MaxPostScriptLength)
            {
                _ = builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
