// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Export;

/// <summary>Writes baseline TIFF files: one uncompressed strip of 8-bit samples, with unassociated alpha when the image has any.</summary>
internal static class TiffRasterWriter
{
    /// <summary>The bytes of the file header.</summary>
    private const int HeaderBytes = 8;

    /// <summary>The bytes of one directory entry.</summary>
    private const int EntryBytes = 12;

    /// <summary>The bytes of the entry count that opens a directory.</summary>
    private const int CountBytes = 2;

    /// <summary>The bytes of the offset that closes a directory.</summary>
    private const int NextBytes = 4;

    /// <summary>The bytes of one rational.</summary>
    private const int RationalBytes = 8;

    /// <summary>The byte order mark for little-endian files.</summary>
    private const ushort LittleEndian = 0x4949;

    /// <summary>The TIFF magic number.</summary>
    private const ushort Magic = 42;

    /// <summary>The field type for a 16-bit unsigned integer.</summary>
    private const ushort TypeShort = 3;

    /// <summary>The field type for a 32-bit unsigned integer.</summary>
    private const ushort TypeLong = 4;

    /// <summary>The field type for a rational.</summary>
    private const ushort TypeRational = 5;

    /// <summary>The tag for the width.</summary>
    private const ushort TagWidth = 256;

    /// <summary>The tag for the height.</summary>
    private const ushort TagHeight = 257;

    /// <summary>The tag for the bits per sample.</summary>
    private const ushort TagBitsPerSample = 258;

    /// <summary>The tag for the compression.</summary>
    private const ushort TagCompression = 259;

    /// <summary>The tag for the photometric interpretation.</summary>
    private const ushort TagPhotometric = 262;

    /// <summary>The tag for the strip offsets.</summary>
    private const ushort TagStripOffsets = 273;

    /// <summary>The tag for the samples per pixel.</summary>
    private const ushort TagSamplesPerPixel = 277;

    /// <summary>The tag for the rows per strip.</summary>
    private const ushort TagRowsPerStrip = 278;

    /// <summary>The tag for the strip byte counts.</summary>
    private const ushort TagStripByteCounts = 279;

    /// <summary>The tag for the horizontal resolution.</summary>
    private const ushort TagXResolution = 282;

    /// <summary>The tag for the vertical resolution.</summary>
    private const ushort TagYResolution = 283;

    /// <summary>The tag for the resolution unit.</summary>
    private const ushort TagResolutionUnit = 296;

    /// <summary>The tag for the meaning of the samples after the colour ones.</summary>
    private const ushort TagExtraSamples = 338;

    /// <summary>The photometric value where zero is black.</summary>
    private const ushort BlackIsZero = 1;

    /// <summary>The photometric value for red-green-blue.</summary>
    private const ushort Rgb = 2;

    /// <summary>The extra sample value for unassociated alpha.</summary>
    private const ushort UnassociatedAlpha = 2;

    /// <summary>The resolution unit for inches.</summary>
    private const ushort Inches = 2;

    /// <summary>The resolution written in dots per inch, because TIFF asks for one and the image has none.</summary>
    private const uint DotsPerInch = 72;

    /// <summary>The bits in a byte.</summary>
    private const ushort SampleBits = 8;

    /// <summary>The directory entries of a grey or colour image.</summary>
    private const int BaseEntries = 12;

    /// <summary>The rationals written for the two resolutions.</summary>
    private const int ResolutionRationals = 2;

    /// <summary>The bytes of the two resolution rationals.</summary>
    private const int ResolutionBytes = RationalBytes * ResolutionRationals;

    /// <summary>Writes a TIFF file.</summary>
    /// <param name="samples">The image.</param>
    /// <returns>The file.</returns>
    internal static byte[] Write(RasterSamples samples)
    {
        var entries = samples.HasAlpha ? BaseEntries + 1 : BaseEntries;
        var directoryEnd = HeaderBytes + CountBytes + (entries * EntryBytes) + NextBytes;
        var bitsBytes = samples.Channels > 1 ? samples.Channels * sizeof(ushort) : 0;
        var bitsOffset = directoryEnd;
        var resolutionOffset = bitsOffset + bitsBytes;
        var dataOffset = resolutionOffset + ResolutionBytes;
        var writer = new RasterByteWriter(new byte[dataOffset + samples.Data.Length]);
        writer.WriteUInt16LittleEndian(LittleEndian);
        writer.WriteUInt16LittleEndian(Magic);
        writer.WriteUInt32LittleEndian(HeaderBytes);
        writer.WriteUInt16LittleEndian((ushort)entries);
        WriteEntry(writer, TagWidth, TypeLong, 1, (uint)samples.Width);
        WriteEntry(writer, TagHeight, TypeLong, 1, (uint)samples.Height);
        WriteBitsPerSample(writer, samples, bitsOffset);
        WriteEntry(writer, TagCompression, TypeShort, 1, 1);
        WriteEntry(writer, TagPhotometric, TypeShort, 1, samples.IsGrey ? BlackIsZero : Rgb);
        WriteEntry(writer, TagStripOffsets, TypeLong, 1, (uint)dataOffset);
        WriteEntry(writer, TagSamplesPerPixel, TypeShort, 1, (uint)samples.Channels);
        WriteEntry(writer, TagRowsPerStrip, TypeLong, 1, (uint)samples.Height);
        WriteEntry(writer, TagStripByteCounts, TypeLong, 1, (uint)samples.Data.Length);
        WriteEntry(writer, TagXResolution, TypeRational, 1, (uint)resolutionOffset);
        WriteEntry(writer, TagYResolution, TypeRational, 1, (uint)(resolutionOffset + RationalBytes));
        WriteEntry(writer, TagResolutionUnit, TypeShort, 1, Inches);
        if (samples.HasAlpha)
        {
            WriteEntry(writer, TagExtraSamples, TypeShort, 1, UnassociatedAlpha);
        }

        // The next directory offset is left at zero. The bits per sample, the resolutions and the strip follow.
        writer.Skip(NextBytes);
        for (var i = 0; i < bitsBytes / sizeof(ushort); i++)
        {
            writer.WriteUInt16LittleEndian(SampleBits);
        }

        WriteResolution(writer);
        writer.WriteBytes(samples.Data);
        return writer.Buffer;
    }

    /// <summary>Writes the bits-per-sample entry: the value itself for one sample, otherwise the offset of a list.</summary>
    /// <param name="writer">The file writer.</param>
    /// <param name="samples">The image.</param>
    /// <param name="listOffset">Where the list is written when there is one.</param>
    private static void WriteBitsPerSample(RasterByteWriter writer, RasterSamples samples, int listOffset)
    {
        if (samples.Channels > 1)
        {
            WriteEntry(writer, TagBitsPerSample, TypeShort, (uint)samples.Channels, (uint)listOffset);
            return;
        }

        WriteEntry(writer, TagBitsPerSample, TypeShort, 1, SampleBits);
    }

    /// <summary>Writes the two resolution rationals, each the same dots per inch over one.</summary>
    /// <param name="writer">The file writer.</param>
    private static void WriteResolution(RasterByteWriter writer)
    {
        for (var i = 0; i < ResolutionRationals; i++)
        {
            writer.WriteUInt32LittleEndian(DotsPerInch);
            writer.WriteUInt32LittleEndian(1);
        }
    }

    /// <summary>Writes one directory entry.</summary>
    /// <param name="writer">The file writer.</param>
    /// <param name="tag">The tag.</param>
    /// <param name="type">The field type.</param>
    /// <param name="count">The number of values.</param>
    /// <param name="value">The value, or the offset of the values.</param>
    private static void WriteEntry(RasterByteWriter writer, ushort tag, ushort type, uint count, uint value)
    {
        writer.WriteUInt16LittleEndian(tag);
        writer.WriteUInt16LittleEndian(type);
        writer.WriteUInt32LittleEndian(count);
        writer.WriteUInt32LittleEndian(value);
    }
}
