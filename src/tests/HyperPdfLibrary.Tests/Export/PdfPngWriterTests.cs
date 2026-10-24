// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using HyperPdfLibrary.Export;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;

namespace HyperPdfLibrary.Tests.Export;

/// <summary>Tests that <see cref="PdfPngWriter"/> writes the samples and checksums a PNG decoder expects, at every width around the vector step.</summary>
public sealed class PdfPngWriterTests
{
    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int Bgra = 4;

    /// <summary>The bytes of an RGB pixel.</summary>
    private const int Rgb = 3;

    /// <summary>The index of the blue byte of a BGRA pixel.</summary>
    private const int Blue = 0;

    /// <summary>The index of the green byte of a BGRA pixel.</summary>
    private const int Green = 1;

    /// <summary>The index of the red byte of a BGRA pixel.</summary>
    private const int Red = 2;

    /// <summary>The index of the alpha byte of a BGRA pixel.</summary>
    private const int Alpha = 3;

    /// <summary>The PNG signature length.</summary>
    private const int SignatureLength = 8;

    /// <summary>The bytes a chunk adds around its data: length, type and checksum.</summary>
    private const int ChunkOverhead = 12;

    /// <summary>The bytes of a chunk's length, type and checksum fields.</summary>
    private const int FieldBytes = 4;

    /// <summary>The widest image tried one width at a time.</summary>
    private const int MaxWidth = 21;

    /// <summary>The height of the narrow images.</summary>
    private const int NarrowHeight = 5;

    /// <summary>The side of the large image.</summary>
    private const int LargeSide = 301;

    /// <summary>The colour type of 8-bit RGB.</summary>
    private const byte RgbType = 2;

    /// <summary>The colour type of 8-bit RGBA.</summary>
    private const byte RgbaType = 6;

    /// <summary>The CRC-32 polynomial, reflected.</summary>
    private const uint Polynomial = 0xEDB88320;

    /// <summary>The row whose pixels are partly transparent in the mixed image.</summary>
    private const int TransparentRow = 2;

    /// <summary>The alpha of the partly transparent pixels.</summary>
    private const byte HalfAlpha = 100;

    /// <summary>The divisor that halves a number, to round a division to the nearest.</summary>
    private const int Halve = 2;

    /// <summary>The index of the colour type in the header chunk's data, counted from its end.</summary>
    private const int TypeFromEnd = 4;

    /// <summary>The 8 byte signature every PNG file starts with.</summary>
    private static readonly byte[] FileMagic =[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Verifies an opaque image is written as RGB with the colours reordered, at every width around the vector step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesOpaqueImagesAsRgb()
    {
        for (var width = 1; width <= MaxWidth; width++)
        {
            await AssertImage(Pixels(width, NarrowHeight, false), RgbType);
        }

        await AssertImage(Pixels(LargeSide, LargeSide, false), RgbType);
    }

    /// <summary>Verifies an image with transparency is written as RGBA, opaque rows reordered and the others divided by their alpha.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesImagesWithTransparencyAsRgba()
    {
        for (var width = 1; width <= MaxWidth; width++)
        {
            await AssertImage(Pixels(width, NarrowHeight, true), RgbaType);
        }

        await AssertImage(Pixels(LargeSide, LargeSide, true), RgbaType);
    }

    /// <summary>Builds random premultiplied pixels.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="transparency">Whether one row is partly transparent and the last pixel is clear.</param>
    /// <returns>The image.</returns>
    private static PdfImageData Pixels(int width, int height, bool transparency)
    {
        var pixels = new byte[width * height * Bgra];
        RandomNumberGenerator.Fill(pixels);
        for (var i = Alpha; i < pixels.Length; i += Bgra)
        {
            pixels[i] = byte.MaxValue;
        }

        if (transparency)
        {
            MakeRowTranslucent(pixels, width, TransparentRow % height);
            pixels.AsSpan(pixels.Length - Bgra).Clear();
        }

        return new(width, height, pixels, false, false, PdfImageCodec.None);
    }

    /// <summary>Gives every pixel of a row the same partial alpha, and colours that premultiplied colours can have.</summary>
    /// <param name="pixels">The pixels of the image.</param>
    /// <param name="width">The width.</param>
    /// <param name="row">The row.</param>
    private static void MakeRowTranslucent(byte[] pixels, int width, int row)
    {
        for (var x = 0; x < width; x++)
        {
            var offset = ((row * width) + x) * Bgra;
            pixels[offset + Alpha] = HalfAlpha;
            pixels[offset + Blue] = (byte)(pixels[offset + Blue] % (HalfAlpha + 1));
            pixels[offset + Green] = (byte)(pixels[offset + Green] % (HalfAlpha + 1));
            pixels[offset + Red] = (byte)(pixels[offset + Red] % (HalfAlpha + 1));
        }
    }

    /// <summary>Checks one encoded image: the header, every chunk's checksum and every sample.</summary>
    /// <param name="image">The image.</param>
    /// <param name="type">The colour type expected.</param>
    /// <returns>A task.</returns>
    private static async Task AssertImage(PdfImageData image, byte type)
    {
        var file = PdfPngWriter.Encode(image);
        await Assert.That(file.AsSpan(0, SignatureLength).SequenceEqual(FileMagic)).IsTrue();

        var chunks = ReadChunks(file);
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(chunks.Header)).IsEqualTo(image.Width);
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(chunks.Header.AsSpan(FieldBytes))).IsEqualTo(image.Height);
        await Assert.That(chunks.Header[^TypeFromEnd]).IsEqualTo(type);

        var channels = type == RgbType ? Rgb : Bgra;
        var rows = Inflate(chunks.Data);
        await Assert.That(rows.Length).IsEqualTo(image.Height * ((image.Width * channels) + 1));
        await Assert.That(rows.AsSpan().SequenceEqual(Expected(image, type, channels))).IsTrue();
        await Assert.That(chunks.ChecksumsMatch).IsTrue();
    }

    /// <summary>Reads the chunks of a file, checking each checksum against a byte at a time reference.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The header data, the compressed scan lines and whether every checksum was right.</returns>
    private static PngChunks ReadChunks(byte[] file)
    {
        byte[] header = [];
        var matches = true;
        using var data = new MemoryStream();
        for (var offset = SignatureLength; offset + ChunkOverhead <= file.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(offset));
            var type = file.AsSpan(offset + FieldBytes, FieldBytes);
            var body = file.AsSpan(offset + FieldBytes + FieldBytes, length);
            var stored = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(offset + FieldBytes + FieldBytes + length));
            matches &= stored == ReferenceCrc([.. type, .. body]);
            if (type.SequenceEqual("IHDR"u8))
            {
                header = body.ToArray();
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                data.Write(body);
            }

            offset += ChunkOverhead + length;
        }

        return new(header, data.ToArray(), matches);
    }

    /// <summary>Inflates zlib data.</summary>
    /// <param name="compressed">The data.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Inflate(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>Builds the scan lines a PNG of an image holds, converting each pixel on its own.</summary>
    /// <param name="image">The image.</param>
    /// <param name="type">The colour type.</param>
    /// <param name="channels">The channels per pixel.</param>
    /// <returns>The scan lines, each starting with its filter byte.</returns>
    private static byte[] Expected(PdfImageData image, byte type, int channels)
    {
        var stride = (image.Width * channels) + 1;
        var rows = new byte[stride * image.Height];
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var source = ((y * image.Width) + x) * Bgra;
                var target = (y * stride) + 1 + (x * channels);
                var alpha = image.Pixels[source + Alpha];
                rows[target] = Straight(image.Pixels[source + Red], alpha);
                rows[target + Green] = Straight(image.Pixels[source + Green], alpha);
                rows[target + Red] = Straight(image.Pixels[source + Blue], alpha);
                if (type == RgbaType)
                {
                    rows[target + Rgb] = alpha;
                }
            }
        }

        return rows;
    }

    /// <summary>Divides a premultiplied channel by its alpha.</summary>
    /// <param name="value">The premultiplied channel.</param>
    /// <param name="alpha">The alpha.</param>
    /// <returns>The straight channel.</returns>
    private static byte Straight(byte value, byte alpha) => alpha switch
    {
        0 => 0,
        byte.MaxValue => value,
        _ => (byte)Math.Min(byte.MaxValue, ((value * byte.MaxValue) + (alpha / Halve)) / alpha),
    };

    /// <summary>Computes a CRC-32 one byte at a time.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The checksum.</returns>
    private static uint ReferenceCrc(byte[] bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < SignatureLength; bit++)
            {
                crc = (crc & 1) != 0 ? Polynomial ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ uint.MaxValue;
    }

    /// <summary>The parts of a PNG file the tests check.</summary>
    /// <param name="Header">The data of the header chunk.</param>
    /// <param name="Data">The compressed scan lines.</param>
    /// <param name="ChecksumsMatch">Whether every chunk's checksum was right.</param>
    private readonly record struct PngChunks(byte[] Header, byte[] Data, bool ChecksumsMatch);
}
