// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Export;

/// <summary>Writes Windows bitmaps: 8-bit grey with a grey palette, 24-bit colour, and 32-bit colour with alpha.</summary>
internal static class BmpRasterWriter
{
    /// <summary>The bytes of the file header.</summary>
    private const int FileHeaderBytes = 14;

    /// <summary>The bytes of the BITMAPINFOHEADER.</summary>
    private const int InfoHeaderBytes = 40;

    /// <summary>The bytes of the BITMAPV4HEADER, which carries the alpha mask.</summary>
    private const int V4HeaderBytes = 108;

    /// <summary>The entries of a grey palette.</summary>
    private const int PaletteEntries = 256;

    /// <summary>The bytes of a palette entry.</summary>
    private const int PaletteEntryBytes = 4;

    /// <summary>The bytes of the horizontal and vertical resolutions, left at zero.</summary>
    private const int ResolutionBytes = sizeof(uint) * 2;

    /// <summary>The rows of a bitmap are padded to a multiple of this many bytes.</summary>
    private const int RowAlignment = 4;

    /// <summary>The file signature, "BM", read as a little-endian number.</summary>
    private const ushort Signature = 0x4D42;

    /// <summary>The compression value for uncompressed pixels.</summary>
    private const uint NoCompression = 0;

    /// <summary>The compression value for pixels laid out by colour masks.</summary>
    private const uint BitFields = 3;

    /// <summary>The colour space type for sRGB, the characters "sRGB" read as a number.</summary>
    private const uint SrgbSpace = 0x73524742;

    /// <summary>The red mask of a 32-bit pixel read as a little-endian number.</summary>
    private const uint RedMask = 0x00FF0000;

    /// <summary>The green mask of a 32-bit pixel.</summary>
    private const uint GreenMask = 0x0000FF00;

    /// <summary>The blue mask of a 32-bit pixel.</summary>
    private const uint BlueMask = 0x000000FF;

    /// <summary>The alpha mask of a 32-bit pixel.</summary>
    private const uint AlphaMask = 0xFF000000;

    /// <summary>The bytes of the colour endpoints and gamma values a V4 header holds after the colour space type.</summary>
    private const int EndpointAndGammaBytes = 48;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The offset of the green sample in a pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of the blue sample in a pixel.</summary>
    private const int BlueOffset = 2;

    /// <summary>The offset of the alpha sample in a pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>Writes a bitmap.</summary>
    /// <param name="samples">The image.</param>
    /// <returns>The file.</returns>
    internal static byte[] Write(RasterSamples samples)
    {
        var headerBytes = samples.HasAlpha ? V4HeaderBytes : InfoHeaderBytes;
        var paletteBytes = samples.IsGrey ? PaletteEntries * PaletteEntryBytes : 0;
        var stride = (samples.RowBytes + RowAlignment - 1) / RowAlignment * RowAlignment;
        var pixelOffset = FileHeaderBytes + headerBytes + paletteBytes;
        var writer = new RasterByteWriter(new byte[pixelOffset + (stride * samples.Height)]);
        writer.WriteUInt16LittleEndian(Signature);
        writer.WriteUInt32LittleEndian((uint)writer.Buffer.Length);
        writer.Skip(sizeof(uint));
        writer.WriteUInt32LittleEndian((uint)pixelOffset);
        WriteInfoHeader(writer, samples, headerBytes, stride);
        if (samples.IsGrey)
        {
            WritePalette(writer);
        }

        WriteRows(writer, samples, stride);
        return writer.Buffer;
    }

    /// <summary>Writes the info header, and for an image with alpha the colour masks that follow it.</summary>
    /// <param name="writer">The file writer.</param>
    /// <param name="samples">The image.</param>
    /// <param name="headerBytes">The size of the header.</param>
    /// <param name="stride">The bytes in a padded row.</param>
    private static void WriteInfoHeader(RasterByteWriter writer, RasterSamples samples, int headerBytes, int stride)
    {
        writer.WriteUInt32LittleEndian((uint)headerBytes);
        writer.WriteUInt32LittleEndian((uint)samples.Width);
        writer.WriteUInt32LittleEndian((uint)samples.Height);
        writer.WriteUInt16LittleEndian(1);
        writer.WriteUInt16LittleEndian((ushort)(samples.Channels * ByteBits));
        writer.WriteUInt32LittleEndian(samples.HasAlpha ? BitFields : NoCompression);
        writer.WriteUInt32LittleEndian((uint)(stride * samples.Height));
        writer.Skip(ResolutionBytes);
        writer.WriteUInt32LittleEndian(samples.IsGrey ? (uint)PaletteEntries : 0U);
        writer.Skip(sizeof(uint));
        if (!samples.HasAlpha)
        {
            return;
        }

        writer.WriteUInt32LittleEndian(RedMask);
        writer.WriteUInt32LittleEndian(GreenMask);
        writer.WriteUInt32LittleEndian(BlueMask);
        writer.WriteUInt32LittleEndian(AlphaMask);
        writer.WriteUInt32LittleEndian(SrgbSpace);
        writer.Skip(EndpointAndGammaBytes);
    }

    /// <summary>Writes the grey palette: each entry is the same value in blue, green and red.</summary>
    /// <param name="writer">The file writer.</param>
    private static void WritePalette(RasterByteWriter writer)
    {
        for (var i = 0; i < PaletteEntries; i++)
        {
            writer.WriteByte((byte)i);
            writer.WriteByte((byte)i);
            writer.WriteByte((byte)i);
            writer.WriteByte(0);
        }
    }

    /// <summary>Writes the rows bottom first, each padded to a whole number of words.</summary>
    /// <param name="writer">The file writer.</param>
    /// <param name="samples">The image.</param>
    /// <param name="stride">The bytes in a padded row.</param>
    private static void WriteRows(RasterByteWriter writer, RasterSamples samples, int stride)
    {
        var data = samples.Data;
        for (var y = samples.Height - 1; y >= 0; y--)
        {
            var start = writer.Position;
            var row = y * samples.RowBytes;
            if (samples.IsGrey)
            {
                writer.WriteBytes(data.AsSpan(row, samples.RowBytes));
            }
            else
            {
                WriteColourRow(writer, samples, row);
            }

            writer.Skip(start + stride - writer.Position);
        }
    }

    /// <summary>Writes one row of colour pixels as blue, green, red and, with alpha, alpha.</summary>
    /// <param name="writer">The file writer.</param>
    /// <param name="samples">The image.</param>
    /// <param name="row">The offset of the row's first sample.</param>
    private static void WriteColourRow(RasterByteWriter writer, RasterSamples samples, int row)
    {
        var source = samples.Data.AsSpan(row, samples.RowBytes);
        var target = writer.Buffer.AsSpan(writer.Position, samples.RowBytes);

        // Whole groups of pixels are swapped a vector at a time; the few left over go one by one.
        for (var i = RedBlueSwap.Swap(source, target, samples.Channels); i < source.Length; i += samples.Channels)
        {
            target[i] = source[i + BlueOffset];
            target[i + GreenOffset] = source[i + GreenOffset];
            target[i + BlueOffset] = source[i];
            if (samples.HasAlpha)
            {
                target[i + AlphaOffset] = source[i + AlphaOffset];
            }
        }

        writer.Skip(samples.RowBytes);
    }
}
