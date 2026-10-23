// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Export;

/// <summary>Writes uncompressed Truevision TGA files, top row first.</summary>
internal static class TgaRasterWriter
{
    /// <summary>The bytes of the file header.</summary>
    private const int HeaderBytes = 18;

    /// <summary>The image type for uncompressed colour.</summary>
    private const byte TypeColour = 2;

    /// <summary>The image type for uncompressed grey.</summary>
    private const byte TypeGrey = 3;

    /// <summary>The bytes before the image type: the length of the image id and the colour map type, both zero.</summary>
    private const int IdAndMapType = 2;

    /// <summary>The bytes from the image type to the width: the colour map description and the origin.</summary>
    private const int BeforeWidth = 9;

    /// <summary>The descriptor bit that puts the first row at the top.</summary>
    private const byte TopLeft = 0x20;

    /// <summary>The number of alpha bits a pixel carries.</summary>
    private const byte AlphaBits = 8;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The offset of the green sample in a pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of the blue sample in a pixel.</summary>
    private const int BlueOffset = 2;

    /// <summary>The offset of the alpha sample in a pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>Writes a TGA file.</summary>
    /// <param name="samples">The image.</param>
    /// <returns>The file.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The image is wider or taller than 65535 pixels.</exception>
    internal static byte[] Write(RasterSamples samples)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(samples.Width, ushort.MaxValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(samples.Height, ushort.MaxValue);
        var writer = new RasterByteWriter(new byte[HeaderBytes + samples.Data.Length]);
        writer.Skip(IdAndMapType);
        writer.WriteByte(samples.IsGrey ? TypeGrey : TypeColour);
        writer.Skip(BeforeWidth);
        writer.WriteUInt16LittleEndian((ushort)samples.Width);
        writer.WriteUInt16LittleEndian((ushort)samples.Height);
        writer.WriteByte((byte)(samples.Channels * ByteBits));
        writer.WriteByte((byte)(TopLeft | (samples.HasAlpha ? AlphaBits : 0)));
        if (samples.IsGrey)
        {
            writer.WriteBytes(samples.Data);
            return writer.Buffer;
        }

        WriteColour(writer, samples);
        return writer.Buffer;
    }

    /// <summary>Writes colour pixels in the blue, green, red, alpha order TGA uses.</summary>
    /// <param name="writer">The file writer.</param>
    /// <param name="samples">The image.</param>
    private static void WriteColour(RasterByteWriter writer, RasterSamples samples)
    {
        var data = samples.Data;
        for (var i = 0; i < data.Length; i += samples.Channels)
        {
            writer.WriteByte(data[i + BlueOffset]);
            writer.WriteByte(data[i + GreenOffset]);
            writer.WriteByte(data[i]);
            if (samples.HasAlpha)
            {
                writer.WriteByte(data[i + AlphaOffset]);
            }
        }
    }
}
