// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>The table directory of an sfnt font: TrueType, OpenType or the first face of a collection.</summary>
/// <param name="Offset">The offset of the directory.</param>
/// <param name="TableCount">The number of table records.</param>
[DebuggerDisplay("SfntDirectory: {TableCount} tables")]
internal readonly record struct SfntDirectory(int Offset, int TableCount)
{
    /// <summary>The 'head' table tag.</summary>
    internal const uint Head = 0x68656164;

    /// <summary>The 'maxp' table tag.</summary>
    internal const uint Maxp = 0x6D617870;

    /// <summary>The 'hhea' table tag.</summary>
    internal const uint Hhea = 0x68686561;

    /// <summary>The 'hmtx' table tag.</summary>
    internal const uint Hmtx = 0x686D7478;

    /// <summary>The 'loca' table tag.</summary>
    internal const uint Loca = 0x6C6F6361;

    /// <summary>The 'glyf' table tag.</summary>
    internal const uint Glyf = 0x676C7966;

    /// <summary>The 'cmap' table tag.</summary>
    internal const uint Cmap = 0x636D6170;

    /// <summary>The 'post' table tag.</summary>
    internal const uint Post = 0x706F7374;

    /// <summary>The 'OS/2' table tag.</summary>
    internal const uint Os2 = 0x4F532F32;

    /// <summary>The 'CFF ' table tag.</summary>
    internal const uint Cff = 0x43464620;

    /// <summary>The TrueType version 1.0 sfnt tag.</summary>
    private const uint VersionOne = 0x00010000;

    /// <summary>The Apple 'true' sfnt tag.</summary>
    private const uint VersionTrue = 0x74727565;

    /// <summary>The OpenType CFF 'OTTO' sfnt tag.</summary>
    private const uint VersionOtto = 0x4F54544F;

    /// <summary>The font collection 'ttcf' tag.</summary>
    private const uint CollectionTag = 0x74746366;

    /// <summary>The offset of the first face's offset in a collection header.</summary>
    private const int CollectionFirstFace = 12;

    /// <summary>The offset of the table count in a directory.</summary>
    private const int TableCountOffset = 4;

    /// <summary>The size of the directory header.</summary>
    private const int HeaderSize = 12;

    /// <summary>The size of one table record.</summary>
    private const int RecordSize = 16;

    /// <summary>The offset of a table's offset within its record.</summary>
    private const int RecordOffset = 8;

    /// <summary>The offset of a table's length within its record.</summary>
    private const int RecordLength = 12;

    /// <summary>Reads the directory of an sfnt font or the first face of a collection.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="directory">The directory.</param>
    /// <returns><see langword="true"/> when the data starts with a known sfnt or collection tag.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    internal static bool TryRead(ReadOnlySpan<byte> data, out SfntDirectory directory)
    {
        directory = default;
        var offset = 0;
        var tag = FontBytes.U32(data, 0);
        if (tag == CollectionTag)
        {
            offset = FontBytes.Offset32(data, CollectionFirstFace);
            if (offset > data.Length - HeaderSize)
            {
                throw new PdfException(PdfError.Format, "The font collection face offset exceeds its data.");
            }

            tag = FontBytes.U32(data, offset);
        }

        if (tag is not (VersionOne or VersionTrue or VersionOtto))
        {
            return false;
        }

        var count = FontBytes.U16(data, offset + TableCountOffset);
        var available = (data.Length - offset - HeaderSize) / RecordSize;
        directory = new(offset, Math.Min(count, Math.Max(available, 0)));
        return directory.TableCount > 0;
    }

    /// <summary>Finds a table.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="tag">The table tag.</param>
    /// <returns>The table's range, clamped to the data; empty when missing.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    internal TableRange Find(ReadOnlySpan<byte> data, uint tag)
    {
        for (var i = 0; i < TableCount; i++)
        {
            var record = Offset + HeaderSize + (i * RecordSize);
            if (FontBytes.U32(data, record) != tag)
            {
                continue;
            }

            var offset = FontBytes.U32(data, record + RecordOffset);
            var length = FontBytes.U32(data, record + RecordLength);
            if (offset > data.Length || length > data.Length - offset)
            {
                throw new PdfException(PdfError.Format, "The font table range exceeds its data.");
            }

            return new((int)offset, (int)length);
        }

        return default;
    }
}
