// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>Writes the JP2 boxes around a codestream (ISO 15444-1 annex I), with optional palette, mapping and definition boxes.</summary>
internal static class JpxTestFileWriter
{
    /// <summary>The bytes of a box header.</summary>
    private const int BoxHeader = 8;

    /// <summary>The enumerated colour space of sRGB.</summary>
    private const int Srgb = 16;

    /// <summary>The enumerated colour space of greyscale.</summary>
    private const int Gray = 17;

    /// <summary>The enumerated colour method.</summary>
    private const byte EnumeratedMethod = 1;

    /// <summary>The bits-per-component byte of 8-bit unsigned samples.</summary>
    private const byte EightBit = 7;

    /// <summary>The compression type of JPEG 2000.</summary>
    private const byte Compression = 7;

    /// <summary>The bytes of the image header box contents.</summary>
    private const int ImageHeaderBytes = 14;

    /// <summary>The offset of the component count in the image header box.</summary>
    private const int ComponentsOffset = 8;

    /// <summary>The offset of the bits per component in the image header box.</summary>
    private const int BitsOffset = 10;

    /// <summary>The offset of the compression type in the image header box.</summary>
    private const int CompressionOffset = 11;

    /// <summary>The components of an RGB image.</summary>
    private const int RgbComponents = 3;

    /// <summary>Gets the JP2 signature box.</summary>
    private static ReadOnlySpan<byte> Signature => [0x00, 0x00, 0x00, 0x0C, 0x6A, 0x50, 0x20, 0x20, 0x0D, 0x0A, 0x87, 0x0A];

    /// <summary>Wraps a codestream with an enumerated colour box: sRGB for three components, greyscale otherwise.</summary>
    /// <param name="codestream">The codestream.</param>
    /// <param name="components">The components.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <returns>The JP2 file.</returns>
    internal static byte[] Wrap(byte[] codestream, int components, int width, int height) =>
        Wrap(codestream, components, width, height, components == RgbComponents ? Srgb : Gray, []);

    /// <summary>Wraps a codestream with an enumerated colour box and extra header boxes.</summary>
    /// <param name="codestream">The codestream.</param>
    /// <param name="components">The components.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="colourSpace">The enumerated colour space.</param>
    /// <param name="extraBoxes">Whole boxes, such as pclr, cmap and cdef, added to the JP2 header.</param>
    /// <returns>The JP2 file.</returns>
    internal static byte[] Wrap(byte[] codestream, int components, int width, int height, int colourSpace, byte[] extraBoxes)
    {
        var header = new byte[ImageHeaderBytes];
        BinaryPrimitives.WriteInt32BigEndian(header, height);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(sizeof(int)), width);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(ComponentsOffset), (ushort)components);
        header[BitsOffset] = EightBit;
        header[CompressionOffset] = Compression;
        var colour = new byte[sizeof(int) + RgbComponents];
        colour[0] = EnumeratedMethod;
        BinaryPrimitives.WriteInt32BigEndian(colour.AsSpan(RgbComponents), colourSpace);
        byte[] inner = [.. Box("ihdr", header), .. Box("colr", colour), .. extraBoxes];
        byte[] brand = [.. "jp2 "u8.ToArray(), 0, 0, 0, 0, .. "jp2 "u8.ToArray()];
        return [.. Signature, .. Box("ftyp", brand), .. Box("jp2h", inner), .. Box("jp2c", codestream)];
    }

    /// <summary>Writes a box.</summary>
    /// <param name="type">The four-character box type.</param>
    /// <param name="contents">The contents.</param>
    /// <returns>The box.</returns>
    internal static byte[] Box(string type, byte[] contents)
    {
        var box = new byte[BoxHeader + contents.Length];
        BinaryPrimitives.WriteInt32BigEndian(box, box.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(box, sizeof(int));
        contents.CopyTo(box, BoxHeader);
        return box;
    }
}
