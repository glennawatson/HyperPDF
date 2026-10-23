// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Reads the file header and the info header of a Windows bitmap.</summary>
internal static class BmpHeaderReader
{
    /// <summary>The bytes of the file header.</summary>
    internal const int FileHeaderBytes = 14;

    /// <summary>The compression value for uncompressed pixels.</summary>
    internal const int CompressionNone = 0;

    /// <summary>The compression value for 8-bit run-length coding.</summary>
    internal const int CompressionRle8 = 1;

    /// <summary>The compression value for 4-bit run-length coding.</summary>
    internal const int CompressionRle4 = 2;

    /// <summary>The compression value for colour masks.</summary>
    internal const int CompressionBitfields = 3;

    /// <summary>The compression value for colour masks that include alpha.</summary>
    internal const int CompressionAlphaBitfields = 6;

    /// <summary>The size of the oldest, core, header.</summary>
    private const int CoreHeaderBytes = 12;

    /// <summary>The size of the standard info header.</summary>
    private const int InfoHeaderBytes = 40;

    /// <summary>The offset in the file of the pixel data's offset.</summary>
    private const int DataOffsetField = 10;

    /// <summary>The offset in a header of the width.</summary>
    private const int WidthOffset = 4;

    /// <summary>The offset in a core header of the height.</summary>
    private const int CoreHeightOffset = 6;

    /// <summary>The offset in an info header of the height.</summary>
    private const int InfoHeightOffset = 8;

    /// <summary>The offset in a core header of the bits in a pixel.</summary>
    private const int CoreBitsOffset = 10;

    /// <summary>The offset in an info header of the bits in a pixel.</summary>
    private const int InfoBitsOffset = 14;

    /// <summary>The offset in an info header of the compression.</summary>
    private const int CompressionOffset = 16;

    /// <summary>The offset in an info header of the number of colours used.</summary>
    private const int ColorsUsedOffset = 32;

    /// <summary>The offset in an info header of the colour masks.</summary>
    private const int MasksOffset = 40;

    /// <summary>The bytes of the red, green and blue masks.</summary>
    private const int ThreeMasksBytes = 12;

    /// <summary>The bytes of all four masks.</summary>
    private const int FourMasksBytes = 16;

    /// <summary>The bytes in a palette entry of an info header.</summary>
    private const int PaletteEntryBytes = 4;

    /// <summary>The bytes in a palette entry of a core header.</summary>
    private const int CorePaletteEntryBytes = 3;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in a 16-bit pixel.</summary>
    private const int HalfWordBits = 16;

    /// <summary>The bits in a 24-bit pixel.</summary>
    private const int TrueColourBits = 24;

    /// <summary>The bits in a 32-bit pixel.</summary>
    private const int WordBits = 32;

    /// <summary>The bits in a two-bit pixel.</summary>
    private const int TwoBits = 2;

    /// <summary>The bits in a four-bit pixel.</summary>
    private const int FourBits = 4;

    /// <summary>The compression value that is not used.</summary>
    private const int UnusedCompressionFour = 4;

    /// <summary>The compression value that is not used.</summary>
    private const int UnusedCompressionFive = 5;

    /// <summary>Reads the headers.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <returns>What the headers say.</returns>
    /// <exception cref="InvalidDataException">The headers are cut short or declare something this reader cannot handle.</exception>
    internal static BmpHeader Read(ReadOnlySpan<byte> file)
    {
        if (file.Length < FileHeaderBytes + CoreHeaderBytes)
        {
            throw new InvalidDataException("The bitmap is too short.");
        }

        var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(file[FileHeaderBytes..]);
        var fields = size == CoreHeaderBytes ? ReadCore(file) : ReadInfo(file, size);
        CheckFields(fields);
        var core = size == CoreHeaderBytes;
        var entries = fields.Bits <= ByteBits ? GetEntries(fields) : fields.Colors;
        return new(
            fields.Width,
            Math.Abs(fields.RawHeight),
            fields.RawHeight < 0,
            fields.Bits,
            fields.Compression,
            FileHeaderBytes + size,
            entries,
            core ? CorePaletteEntryBytes : PaletteEntryBytes,
            (int)BinaryPrimitives.ReadUInt32LittleEndian(file[DataOffsetField..]),
            ReadMasks(file, fields));
    }

    /// <summary>Reads the fields of a core header.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <returns>The fields.</returns>
    private static BmpFields ReadCore(ReadOnlySpan<byte> file) => new(
        BinaryPrimitives.ReadUInt16LittleEndian(file[(FileHeaderBytes + WidthOffset)..]),
        BinaryPrimitives.ReadUInt16LittleEndian(file[(FileHeaderBytes + CoreHeightOffset)..]),
        BinaryPrimitives.ReadUInt16LittleEndian(file[(FileHeaderBytes + CoreBitsOffset)..]),
        CompressionNone,
        0,
        CoreHeaderBytes);

    /// <summary>Reads the fields of an info header.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="size">The header's size.</param>
    /// <returns>The fields.</returns>
    /// <exception cref="InvalidDataException">The header is not one this reader knows.</exception>
    private static BmpFields ReadInfo(ReadOnlySpan<byte> file, int size)
    {
        if (size < InfoHeaderBytes || file.Length < FileHeaderBytes + size)
        {
            throw new InvalidDataException("The bitmap's header is not one this reader knows.");
        }

        return new(
            BinaryPrimitives.ReadInt32LittleEndian(file[(FileHeaderBytes + WidthOffset)..]),
            BinaryPrimitives.ReadInt32LittleEndian(file[(FileHeaderBytes + InfoHeightOffset)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(file[(FileHeaderBytes + InfoBitsOffset)..]),
            (int)BinaryPrimitives.ReadUInt32LittleEndian(file[(FileHeaderBytes + CompressionOffset)..]),
            (int)BinaryPrimitives.ReadUInt32LittleEndian(file[(FileHeaderBytes + ColorsUsedOffset)..]),
            size);
    }

    /// <summary>Checks the size, pixel size and compression.</summary>
    /// <param name="fields">The fields.</param>
    /// <exception cref="InvalidDataException">A field is not one this reader handles.</exception>
    private static void CheckFields(in BmpFields fields)
    {
        ImageFileLimits.CheckSize(fields.Width, Math.Abs((long)fields.RawHeight));
        if (!IsKnownBits(fields.Bits) || !IsKnownCompression(fields.Compression))
        {
            throw new InvalidDataException("The bitmap's pixel layout is not one this reader knows.");
        }
    }

    /// <summary>Determines whether a pixel size is one bitmaps use.</summary>
    /// <param name="bits">The bits in a pixel.</param>
    /// <returns><see langword="true"/> for 1, 2, 4, 8, 16, 24 and 32.</returns>
    private static bool IsKnownBits(int bits) => bits is 1 or TwoBits or FourBits or ByteBits or HalfWordBits or TrueColourBits or WordBits;

    /// <summary>Determines whether a compression value is one bitmaps use.</summary>
    /// <param name="compression">The compression value.</param>
    /// <returns><see langword="true"/> for 0, 1, 2, 3 and 6.</returns>
    private static bool IsKnownCompression(int compression) =>
        compression is >= CompressionNone and <= CompressionAlphaBitfields and not (UnusedCompressionFour or UnusedCompressionFive);

    /// <summary>Gets the number of palette entries a bitmap with eight bits a pixel or fewer holds.</summary>
    /// <param name="fields">The fields.</param>
    /// <returns>The entries.</returns>
    private static int GetEntries(in BmpFields fields)
    {
        var all = 1 << fields.Bits;
        return fields.Colors == 0 || fields.Colors > all ? all : fields.Colors;
    }

    /// <summary>Reads the colour masks of a 16 or 32-bit bitmap.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="fields">The fields.</param>
    /// <returns>The masks.</returns>
    private static BmpMasks ReadMasks(ReadOnlySpan<byte> file, in BmpFields fields)
    {
        var defaults = fields.Bits == HalfWordBits ? BmpMasks.Default16 : BmpMasks.Default32;
        var start = FileHeaderBytes + MasksOffset;
        var withMasks = fields.Compression is CompressionBitfields or CompressionAlphaBitfields;
        if (fields.Bits < HalfWordBits || !withMasks || file.Length < start + ThreeMasksBytes)
        {
            return defaults;
        }

        var hasAlpha = (fields.HeaderSize > InfoHeaderBytes || fields.Compression == CompressionAlphaBitfields) && file.Length >= start + FourMasksBytes;
        return new(
            BinaryPrimitives.ReadUInt32LittleEndian(file[start..]),
            BinaryPrimitives.ReadUInt32LittleEndian(file[(start + sizeof(uint))..]),
            BinaryPrimitives.ReadUInt32LittleEndian(file[(start + sizeof(uint) + sizeof(uint))..]),
            hasAlpha ? BinaryPrimitives.ReadUInt32LittleEndian(file[(start + ThreeMasksBytes)..]) : 0);
    }
}
