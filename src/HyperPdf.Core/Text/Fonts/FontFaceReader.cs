// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Core.Text.Fonts;

/// <summary>
/// Reads what a font file holds: each face's family, style, weight, slant, classification and licence. Only the
/// table directory and the few small tables needed are read, so scanning many fonts stays quick.
/// </summary>
internal static class FontFaceReader
{
    /// <summary>The bytes read first: enough for a collection header with many faces, or most font directories.</summary>
    private const int HeadBytes = 4096;

    /// <summary>The largest naming table read.</summary>
    private const int MaxNameBytes = 256 * 1024;

    /// <summary>The bytes of a naming table header.</summary>
    private const int NameHeaderBytes = 6;

    /// <summary>The bytes of a name record.</summary>
    private const int NameRecordBytes = 12;

    /// <summary>The offset of the record count in the naming table.</summary>
    private const int NameCount = 2;

    /// <summary>The offset of the string storage offset in the naming table.</summary>
    private const int NameStorage = 4;

    /// <summary>The offset of the language in a name record.</summary>
    private const int RecordLanguage = 4;

    /// <summary>The offset of the name ID in a name record.</summary>
    private const int RecordNameId = 6;

    /// <summary>The offset of the string length in a name record.</summary>
    private const int RecordLength = 8;

    /// <summary>The offset of the string offset in a name record.</summary>
    private const int RecordOffset = 10;

    /// <summary>The rank of a US English Windows name, the best.</summary>
    private const int RankEnglish = 0;

    /// <summary>The rank of another Windows name.</summary>
    private const int RankWindows = 1;

    /// <summary>The rank of a Unicode platform name.</summary>
    private const int RankUnicode = 2;

    /// <summary>The rank of a Macintosh English name.</summary>
    private const int RankMac = 3;

    /// <summary>The rank of any other name, which is not used.</summary>
    private const int RankNone = int.MaxValue;

    /// <summary>The family name ID.</summary>
    private const int FamilyId = 1;

    /// <summary>The style name ID.</summary>
    private const int StyleId = 2;

    /// <summary>The typographic family name ID.</summary>
    private const int TypographicFamilyId = 16;

    /// <summary>The typographic style name ID.</summary>
    private const int TypographicStyleId = 17;

    /// <summary>The Unicode platform.</summary>
    private const int UnicodePlatform = 0;

    /// <summary>The Macintosh platform.</summary>
    private const int MacPlatform = 1;

    /// <summary>The Windows platform.</summary>
    private const int WindowsPlatform = 3;

    /// <summary>The Windows language ID of US English.</summary>
    private const int EnglishUs = 0x0409;

    /// <summary>The offset of the weight in the OS/2 table.</summary>
    private const int Os2Weight = 4;

    /// <summary>The offset of the embedding flags in the OS/2 table.</summary>
    private const int Os2Type = 8;

    /// <summary>The offset of the family class in the OS/2 table.</summary>
    private const int Os2FamilyClass = 30;

    /// <summary>The offset of the selection flags in the OS/2 table.</summary>
    private const int Os2Selection = 62;

    /// <summary>The OS/2 table's bytes up to and including the selection flags.</summary>
    private const int Os2Bytes = 64;

    /// <summary>The italic bit of the OS/2 selection flags.</summary>
    private const int ItalicSelection = 1;

    /// <summary>The offset of the style flags in the font header.</summary>
    private const int HeadMacStyle = 44;

    /// <summary>The font header's bytes up to and including the style flags.</summary>
    private const int HeadStyleBytes = 46;

    /// <summary>The italic bit of the header's style flags.</summary>
    private const int ItalicMacStyle = 2;

    /// <summary>The bold bit of the header's style flags.</summary>
    private const int BoldMacStyle = 1;

    /// <summary>The offset of the fixed pitch flag in the PostScript table.</summary>
    private const int PostFixedPitch = 12;

    /// <summary>The PostScript table's bytes up to and including the fixed pitch flag.</summary>
    private const int PostBytes = 16;

    /// <summary>The weight given to faces marked bold without an OS/2 table.</summary>
    private const int BoldWeight = 700;

    /// <summary>The regular weight.</summary>
    private const int RegularWeight = 400;

    /// <summary>The highest IBM family class with serifs; sans serif is 8.</summary>
    private const int LastSerifClass = 7;

    /// <summary>The IBM family class of ornamentals, which has no serifs.</summary>
    private const int OrnamentalClass = 6;

    /// <summary>The bits of the class in the IBM family class.</summary>
    private const int ClassShift = 8;

    /// <summary>Reads every face of a font file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="output">Receives the faces.</param>
    internal static void Read(string path, List<FontFace> output)
    {
        using var file = File.OpenHandle(path);
        var length = RandomAccess.GetLength(file);
        var buffer = ArrayPool<byte>.Shared.Rent(HeadBytes);
        try
        {
            var head = buffer.AsSpan(0, RandomAccess.Read(file, buffer.AsSpan(0, HeadBytes), 0));
            var faces = SfntDirectory.FaceCount(head);
            for (var face = 0; face < faces; face++)
            {
                var offset = SfntDirectory.FaceOffset(head, face);
                if (offset >= 0 && ReadDirectory(file, offset, length) is { } directory && ReadFace(file, directory, path, face) is { } read)
                {
                    output.Add(read);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Reads a face's name, classification and licence from its tables.</summary>
    /// <param name="name">The naming table.</param>
    /// <param name="os2">The OS/2 table, or empty.</param>
    /// <param name="head">The font header, or empty.</param>
    /// <param name="post">The PostScript table, or empty.</param>
    /// <param name="location">The file and face index.</param>
    /// <returns>The face, or <see langword="null"/> when it has no family name.</returns>
    internal static FontFace? Describe(ReadOnlySpan<byte> name, ReadOnlySpan<byte> os2, ReadOnlySpan<byte> head, ReadOnlySpan<byte> post, (string Path, int FaceIndex) location)
    {
        var family = PreferredName(name, TypographicFamilyId, FamilyId);
        if (family.Length == 0)
        {
            return null;
        }

        var style = PreferredName(name, TypographicStyleId, StyleId);
        var macStyle = head.Length >= HeadStyleBytes ? BinaryPrimitives.ReadUInt16BigEndian(head[HeadMacStyle..]) : 0;
        var hasOs2 = os2.Length >= Os2Bytes;
        return new(family, style.Length == 0 ? "Regular" : style, location.Path, location.FaceIndex)
        {
            Weight = WeightOf(hasOs2 ? os2 : default, macStyle),
            IsItalic = (macStyle & ItalicMacStyle) != 0 || (hasOs2 && (BinaryPrimitives.ReadUInt16BigEndian(os2[Os2Selection..]) & ItalicSelection) != 0),
            IsMonospace = post.Length >= PostBytes && BinaryPrimitives.ReadUInt32BigEndian(post[PostFixedPitch..]) != 0,
            IsSerif = hasOs2 && IsSerifClass(BinaryPrimitives.ReadInt16BigEndian(os2[Os2FamilyClass..]) >> ClassShift),
            EmbeddingFlags = hasOs2 ? BinaryPrimitives.ReadUInt16BigEndian(os2[Os2Type..]) : 0,
        };
    }

    /// <summary>Reads one name, preferring US English, then any Windows or Unicode name, then a Macintosh name.</summary>
    /// <param name="table">The naming table.</param>
    /// <param name="nameId">The name ID.</param>
    /// <returns>The name, or an empty string.</returns>
    internal static string ReadName(ReadOnlySpan<byte> table, int nameId)
    {
        if (table.Length < NameHeaderBytes)
        {
            return string.Empty;
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(table[NameCount..]);
        var storage = BinaryPrimitives.ReadUInt16BigEndian(table[NameStorage..]);
        var best = -1;
        var bestRank = int.MaxValue;
        for (var i = 0; i < count && NameHeaderBytes + ((i + 1) * NameRecordBytes) <= table.Length; i++)
        {
            var record = table.Slice(NameHeaderBytes + (i * NameRecordBytes), NameRecordBytes);
            if (BinaryPrimitives.ReadUInt16BigEndian(record[RecordNameId..]) != nameId)
            {
                continue;
            }

            var rank = Rank(BinaryPrimitives.ReadUInt16BigEndian(record), BinaryPrimitives.ReadUInt16BigEndian(record[RecordLanguage..]));
            if (rank < bestRank)
            {
                (best, bestRank) = (i, rank);
            }
        }

        return best < 0 ? string.Empty : Decode(table, NameHeaderBytes + (best * NameRecordBytes), storage);
    }

    /// <summary>Reads a face's weight, from its OS/2 table or else its header's bold flag.</summary>
    /// <param name="os2">The OS/2 table, or empty.</param>
    /// <param name="macStyle">The header's style flags.</param>
    /// <returns>The weight.</returns>
    private static int WeightOf(ReadOnlySpan<byte> os2, int macStyle)
    {
        var weight = os2.IsEmpty ? 0 : BinaryPrimitives.ReadUInt16BigEndian(os2[Os2Weight..]);
        if (weight != 0)
        {
            return weight;
        }

        return (macStyle & BoldMacStyle) != 0 ? BoldWeight : RegularWeight;
    }

    /// <summary>Determines whether an IBM family class has serifs.</summary>
    /// <param name="familyClass">The class.</param>
    /// <returns><see langword="true"/> for the serif classes.</returns>
    private static bool IsSerifClass(int familyClass) => familyClass is > 0 and <= LastSerifClass and not OrnamentalClass;

    /// <summary>Reads a typographic name, or the plain name when the face has none.</summary>
    /// <param name="table">The naming table.</param>
    /// <param name="typographic">The typographic name ID.</param>
    /// <param name="plain">The plain name ID.</param>
    /// <returns>The name, or an empty string.</returns>
    private static string PreferredName(ReadOnlySpan<byte> table, int typographic, int plain) =>
        ReadName(table, typographic) is { Length: > 0 } name ? name : ReadName(table, plain);

    /// <summary>Ranks a name record's platform and language; lower is better.</summary>
    /// <param name="platform">The platform.</param>
    /// <param name="language">The language.</param>
    /// <returns>The rank.</returns>
    private static int Rank(int platform, int language) => platform switch
    {
        WindowsPlatform when language == EnglishUs => RankEnglish,
        WindowsPlatform => RankWindows,
        UnicodePlatform => RankUnicode,
        MacPlatform when language == 0 => RankMac,
        _ => RankNone,
    };

    /// <summary>Decodes a name record's string.</summary>
    /// <param name="table">The naming table.</param>
    /// <param name="recordAt">The record's offset.</param>
    /// <param name="storage">The string storage offset.</param>
    /// <returns>The string.</returns>
    private static string Decode(ReadOnlySpan<byte> table, int recordAt, int storage)
    {
        var record = table.Slice(recordAt, NameRecordBytes);
        var platform = BinaryPrimitives.ReadUInt16BigEndian(record);
        var length = BinaryPrimitives.ReadUInt16BigEndian(record[RecordLength..]);
        var offset = storage + BinaryPrimitives.ReadUInt16BigEndian(record[RecordOffset..]);
        if (offset + length > table.Length)
        {
            return string.Empty;
        }

        var bytes = table.Slice(offset, length);
        var text = platform == MacPlatform ? Encoding.Latin1.GetString(bytes) : Encoding.BigEndianUnicode.GetString(bytes);
        return text.Trim('\0', ' ');
    }

    /// <summary>Reads a face's table directory from the file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="offset">Where the face's offset table starts.</param>
    /// <param name="length">The file's length.</param>
    /// <returns>The directory, or <see langword="null"/>.</returns>
    private static SfntDirectory? ReadDirectory(SafeFileHandle file, long offset, long length)
    {
        Span<byte> header = stackalloc byte[SfntDirectory.HeaderBytes];
        if (RandomAccess.Read(file, header, offset) < header.Length)
        {
            return null;
        }

        var bytes = SfntDirectory.DirectoryBytes(header);
        if (bytes == 0)
        {
            return null;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(bytes);
        try
        {
            var read = RandomAccess.Read(file, buffer.AsSpan(0, bytes), offset);
            return read < bytes ? null : SfntDirectory.Read(buffer.AsSpan(0, bytes), length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Reads the small tables describing a face.</summary>
    /// <param name="file">The file.</param>
    /// <param name="directory">The face's directory.</param>
    /// <param name="path">The file's path.</param>
    /// <param name="faceIndex">The face's index.</param>
    /// <returns>The face, or <see langword="null"/>.</returns>
    private static FontFace? ReadFace(SafeFileHandle file, SfntDirectory directory, string path, int faceIndex)
    {
        var name = ReadTable(file, directory, SfntTag.Name, MaxNameBytes);
        var os2 = ReadTable(file, directory, SfntTag.Os2, Os2Bytes);
        var head = ReadTable(file, directory, SfntTag.Head, HeadStyleBytes);
        var post = ReadTable(file, directory, SfntTag.Post, PostBytes);
        var face = Describe(name, os2, head, post, (path, faceIndex));
        return face is null ? null : face with { HasTrueTypeOutlines = directory.TryFind(SfntTag.Glyf, out _, out _) && directory.TryFind(SfntTag.Loca, out _, out _) };
    }

    /// <summary>Reads the start of a table.</summary>
    /// <param name="file">The file.</param>
    /// <param name="directory">The directory.</param>
    /// <param name="tag">The table.</param>
    /// <param name="maxBytes">The most bytes read.</param>
    /// <returns>The bytes, or an empty array when the face has no such table.</returns>
    private static byte[] ReadTable(SafeFileHandle file, SfntDirectory directory, uint tag, int maxBytes)
    {
        if (!directory.TryFind(tag, out var offset, out var length) || length == 0)
        {
            return [];
        }

        var bytes = new byte[Math.Min(length, maxBytes)];
        var read = RandomAccess.Read(file, bytes, offset);
        return read == bytes.Length ? bytes : [];
    }
}
