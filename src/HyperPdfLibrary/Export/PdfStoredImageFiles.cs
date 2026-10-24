// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.Export;

/// <summary>
/// Wraps the coded data of a JBIG2 or CCITT image in a file other tools open, without decoding it: a JBIG2 image becomes a
/// standalone JBIG2 file with the global segments in front, and a CCITT image becomes a bilevel TIFF.
/// </summary>
internal static class PdfStoredImageFiles
{
    /// <summary>The numbers in a /Decode array for one component.</summary>
    private const int DecodePair = 2;

    /// <summary>The rationals written for the two resolutions.</summary>
    private const int ResolutionRationals = 2;

    /// <summary>The bytes of a TIFF short.</summary>
    private const int ShortBytes = 2;

    /// <summary>The bytes of a TIFF long.</summary>
    private const int LongBytes = 4;

    /// <summary>The offset of the field type in a directory entry.</summary>
    private const int FieldTypeOffset = 2;

    /// <summary>The offset of the value count in a directory entry.</summary>
    private const int FieldCountOffset = 4;

    /// <summary>The offset of the value in a directory entry.</summary>
    private const int FieldValueOffset = 8;

    /// <summary>The JBIG2 file header flags: sequential organisation, with the number of pages known.</summary>
    private const byte Jbig2SequentialKnownPages = 0x01;

    /// <summary>The bytes of a JBIG2 file header after the id: the flags and the page count.</summary>
    private const int Jbig2HeaderTail = 5;

    /// <summary>The number of pages a PDF image holds.</summary>
    private const int Jbig2PageCount = 1;

    /// <summary>The TIFF byte order mark for little-endian files.</summary>
    private const ushort TiffLittleEndian = 0x4949;

    /// <summary>The TIFF magic number.</summary>
    private const ushort TiffMagic = 42;

    /// <summary>The bytes of the TIFF header.</summary>
    private const int TiffHeaderBytes = 8;

    /// <summary>The bytes of one TIFF directory entry.</summary>
    private const int TiffEntryBytes = 12;

    /// <summary>The bytes of the entry count that opens a directory.</summary>
    private const int TiffCountBytes = 2;

    /// <summary>The bytes of the offset that closes a directory.</summary>
    private const int TiffNextBytes = 4;

    /// <summary>The TIFF field type for a 16-bit unsigned integer.</summary>
    private const ushort TypeShort = 3;

    /// <summary>The TIFF field type for a 32-bit unsigned integer.</summary>
    private const ushort TypeLong = 4;

    /// <summary>The TIFF field type for a rational.</summary>
    private const ushort TypeRational = 5;

    /// <summary>The TIFF tag for the width.</summary>
    private const ushort TagWidth = 256;

    /// <summary>The TIFF tag for the height.</summary>
    private const ushort TagHeight = 257;

    /// <summary>The TIFF tag for the bits per sample.</summary>
    private const ushort TagBitsPerSample = 258;

    /// <summary>The TIFF tag for the compression.</summary>
    private const ushort TagCompression = 259;

    /// <summary>The TIFF tag for the photometric interpretation.</summary>
    private const ushort TagPhotometric = 262;

    /// <summary>The TIFF tag for the strip offsets.</summary>
    private const ushort TagStripOffsets = 273;

    /// <summary>The TIFF tag for the rows per strip.</summary>
    private const ushort TagRowsPerStrip = 278;

    /// <summary>The TIFF tag for the strip byte counts.</summary>
    private const ushort TagStripByteCounts = 279;

    /// <summary>The TIFF tag for the horizontal resolution.</summary>
    private const ushort TagXResolution = 282;

    /// <summary>The TIFF tag for the vertical resolution.</summary>
    private const ushort TagYResolution = 283;

    /// <summary>The TIFF tag for the Group 3 options.</summary>
    private const ushort TagGroup3Options = 292;

    /// <summary>The TIFF tag for the resolution unit.</summary>
    private const ushort TagResolutionUnit = 296;

    /// <summary>The TIFF compression for Modified Huffman, byte aligned one-dimensional coding.</summary>
    private const ushort CompressionHuffman = 2;

    /// <summary>The TIFF compression for Group 3.</summary>
    private const ushort CompressionGroup3 = 3;

    /// <summary>The TIFF compression for Group 4.</summary>
    private const ushort CompressionGroup4 = 4;

    /// <summary>The Group 3 option bit for two-dimensional coding.</summary>
    private const uint TwoDimensional = 1;

    /// <summary>The Group 3 option bit for fill bits before end-of-line codes, which byte aligns each line.</summary>
    private const uint FillBits = 4;

    /// <summary>The resolution unit for inches.</summary>
    private const ushort UnitInch = 2;

    /// <summary>The resolution written, in dots per inch, because TIFF requires one and the PDF gives none.</summary>
    private const uint Resolution = 72;

    /// <summary>The bytes of one rational.</summary>
    private const int RationalBytes = 8;

    /// <summary>The directory entries written with a Group 3 options entry.</summary>
    private const int EntriesWithOptions = 12;

    /// <summary>The directory entries written without a Group 3 options entry.</summary>
    private const int EntriesWithoutOptions = 11;

    /// <summary>The 8 byte id every JBIG2 file starts with (Annex D.4.1 of the JBIG2 standard).</summary>
    private static readonly byte[] Jbig2Id = [0x97, 0x4A, 0x42, 0x32, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Makes a standalone JBIG2 file from an image's coded data.</summary>
    /// <param name="image">The image, which must use the JBIG2 filter last.</param>
    /// <returns>The file, or <see langword="null"/> when the image has no stream to read.</returns>
    internal static byte[]? TryMakeJbig2(PdfImageObject image)
    {
        if (image.Stream is not { } stream)
        {
            return null;
        }

        var data = stream.DecodeToArray();
        var globals = ImageFilters.CodecParameters(image.Dictionary)?.GetStream(KnownName.JBIG2Globals)?.DecodeToArray() ?? [];
        var file = new byte[Jbig2Id.Length + Jbig2HeaderTail + globals.Length + data.Length];
        Jbig2Id.CopyTo(file, 0);
        file[Jbig2Id.Length] = Jbig2SequentialKnownPages;
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(Jbig2Id.Length + 1), Jbig2PageCount);
        var offset = Jbig2Id.Length + Jbig2HeaderTail;
        globals.CopyTo(file, offset);
        data.CopyTo(file, offset + globals.Length);
        return file;
    }

    /// <summary>Makes a bilevel TIFF from an image's CCITT data.</summary>
    /// <param name="image">The image, which must use the CCITT filter last.</param>
    /// <returns>The file, or <see langword="null"/> when the coding cannot be written as TIFF or the image has no stream.</returns>
    internal static byte[]? TryMakeCcittTiff(PdfImageObject image)
    {
        if (image.Stream is not { } stream)
        {
            return null;
        }

        var parameters = CcittParameters.FromDictionary(ImageFilters.CodecParameters(image.Dictionary));
        if (!TryGetCompression(parameters, out var compression, out var options))
        {
            return null;
        }

        var rows = parameters.Rows > 0 ? parameters.Rows : image.Height;
        if (parameters.Columns <= 0 || rows <= 0)
        {
            return null;
        }

        // A white run is 1 when a 1 bit is white, and /Decode [1 0] swaps the two; TIFF says which value shows as white.
        var decode = image.Dictionary.GetArray(KnownName.Decode);
        var inverted = decode is { Count: >= DecodePair } && decode.GetNumber(0) > decode.GetNumber(1);
        var whiteShowsWhite = !parameters.BlackIs1 ^ inverted;
        var data = stream.DecodeToArray();
        return WriteTiff(data, parameters.Columns, rows, compression, options, whiteShowsWhite);
    }

    /// <summary>Works out the TIFF compression for CCITT parameters.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="compression">The TIFF compression.</param>
    /// <param name="options">The Group 3 options, or zero.</param>
    /// <returns><see langword="true"/> when TIFF can hold the coding as it is.</returns>
    private static bool TryGetCompression(in CcittParameters parameters, out ushort compression, out uint options)
    {
        options = 0;
        compression = CompressionGroup4;
        if (parameters.K < 0)
        {
            return true;
        }

        // TIFF Group 3 carries end-of-line codes; without them only the byte aligned one-dimensional form has a TIFF name.
        if (!parameters.EndOfLine)
        {
            compression = CompressionHuffman;
            return parameters.K == 0 && parameters.EncodedByteAlign;
        }

        compression = CompressionGroup3;
        options = (parameters.K > 0 ? TwoDimensional : 0) | (parameters.EncodedByteAlign ? FillBits : 0);
        return true;
    }

    /// <summary>Writes a single strip bilevel TIFF.</summary>
    /// <param name="data">The coded data.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="compression">The TIFF compression.</param>
    /// <param name="options">The Group 3 options; written only for Group 3.</param>
    /// <param name="whiteShowsWhite">Whether a white run shows as white.</param>
    /// <returns>The file.</returns>
    private static byte[] WriteTiff(byte[] data, int width, int height, ushort compression, uint options, bool whiteShowsWhite)
    {
        var hasOptions = compression == CompressionGroup3;
        var entries = hasOptions ? EntriesWithOptions : EntriesWithoutOptions;
        var directoryBytes = TiffCountBytes + (entries * TiffEntryBytes) + TiffNextBytes;
        var resolutionOffset = TiffHeaderBytes + directoryBytes;
        var dataOffset = resolutionOffset + (RationalBytes * ResolutionRationals);
        var file = new byte[dataOffset + data.Length];
        var span = file.AsSpan();
        BinaryPrimitives.WriteUInt16LittleEndian(span, TiffLittleEndian);
        BinaryPrimitives.WriteUInt16LittleEndian(span[ShortBytes..], TiffMagic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[LongBytes..], TiffHeaderBytes);
        BinaryPrimitives.WriteUInt16LittleEndian(span[TiffHeaderBytes..], (ushort)entries);
        var entry = span[(TiffHeaderBytes + TiffCountBytes)..];
        WriteEntry(ref entry, TagWidth, TypeLong, (uint)width);
        WriteEntry(ref entry, TagHeight, TypeLong, (uint)height);
        WriteEntry(ref entry, TagBitsPerSample, TypeShort, 1);
        WriteEntry(ref entry, TagCompression, TypeShort, compression);
        WriteEntry(ref entry, TagPhotometric, TypeShort, whiteShowsWhite ? 0U : 1U);
        WriteEntry(ref entry, TagStripOffsets, TypeLong, (uint)dataOffset);
        WriteEntry(ref entry, TagRowsPerStrip, TypeLong, (uint)height);
        WriteEntry(ref entry, TagStripByteCounts, TypeLong, (uint)data.Length);
        WriteEntry(ref entry, TagXResolution, TypeRational, (uint)resolutionOffset);
        WriteEntry(ref entry, TagYResolution, TypeRational, (uint)(resolutionOffset + RationalBytes));
        if (hasOptions)
        {
            WriteEntry(ref entry, TagGroup3Options, TypeLong, options);
        }

        WriteEntry(ref entry, TagResolutionUnit, TypeShort, UnitInch);

        // The entry after the last is the next directory offset, left at zero, then the two resolution rationals.
        var rationals = span[resolutionOffset..];
        BinaryPrimitives.WriteUInt32LittleEndian(rationals, Resolution);
        BinaryPrimitives.WriteUInt32LittleEndian(rationals[LongBytes..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(rationals[RationalBytes..], Resolution);
        BinaryPrimitives.WriteUInt32LittleEndian(rationals[(RationalBytes + LongBytes)..], 1);
        data.CopyTo(file, dataOffset);
        return file;
    }

    /// <summary>Writes one directory entry that holds a single value, and moves past it.</summary>
    /// <param name="entry">The space for this entry and those after it.</param>
    /// <param name="tag">The tag.</param>
    /// <param name="type">The field type.</param>
    /// <param name="value">The value, or the offset of the value.</param>
    private static void WriteEntry(ref Span<byte> entry, ushort tag, ushort type, uint value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(entry, tag);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[FieldTypeOffset..], type);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[FieldCountOffset..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[FieldValueOffset..], value);
        entry = entry[TiffEntryBytes..];
    }
}
