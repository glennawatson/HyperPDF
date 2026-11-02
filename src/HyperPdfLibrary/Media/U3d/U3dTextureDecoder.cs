// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.ImageFiles;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Decodes texture declarations and the picture data in their continuation blocks (ECMA-363 9.8.5).</summary>
internal static class U3dTextureDecoder
{
    /// <summary>The fewest bytes a continuation image description takes: compression, channels, attributes and a count.</summary>
    private const int ImageMinimum = 8;

    /// <summary>The bytes of a red, green, blue pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The offset of the second channel in a pixel.</summary>
    private const int SecondChannel = 1;

    /// <summary>The offset of the third channel in a pixel.</summary>
    private const int ThirdChannel = 2;

    /// <summary>The offset of the fourth channel in a pixel.</summary>
    private const int FourthChannel = 3;

    /// <summary>The attribute flag for an image held in another file.</summary>
    private const ushort ExternalImage = 1;

    /// <summary>The compression type of a PNG picture.</summary>
    private const byte PngCompression = 2;

    /// <summary>The compression type of a TIFF picture.</summary>
    private const byte TiffCompression = 4;

    /// <summary>The channel bit for alpha.</summary>
    private const byte AlphaChannel = 1;

    /// <summary>The channel bit for blue.</summary>
    private const byte BlueChannel = 2;

    /// <summary>The channel bit for green.</summary>
    private const byte GreenChannel = 4;

    /// <summary>The channel bit for red.</summary>
    private const byte RedChannel = 8;

    /// <summary>The channel bit for luminance, which sets red, green and blue.</summary>
    private const byte LuminanceChannel = 16;

    /// <summary>The bytes of a decoded pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The bytes of the continuation block fields after the texture name: the image index.</summary>
    private const int IndexBytes = 4;

    /// <summary>The value of an opaque alpha channel.</summary>
    private const byte Opaque = 255;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Reads a texture declaration.</summary>
    /// <param name="data">The block's data section.</param>
    /// <param name="builder">Receives the declaration.</param>
    internal static void ReadDeclaration(ReadOnlyMemory<byte> data, U3dModelBuilder builder)
    {
        var reader = builder.Open(data, null);
        var name = reader.ReadString();
        var height = reader.ReadU32();
        var width = reader.ReadU32();
        _ = reader.ReadU8();
        var count = reader.ReadU32();
        if (reader.Failed || (long)count * ImageMinimum > reader.RemainingBytes || height == 0 || width == 0)
        {
            builder.Problem("A texture declaration is cut short or declares more images than its block can hold.");
            return;
        }

        var images = new U3dContinuationImage[count];
        for (var i = 0; i < images.Length; i++)
        {
            images[i] = ReadImage(ref reader);
        }

        if (reader.Failed || (long)width * height > builder.Limits.MaxTexturePixels)
        {
            builder.Problem("A texture declaration is cut short or declares a picture larger than will be decoded.");
            return;
        }

        builder.TexturePieces[name] = new(name, (int)width, (int)height, images);
    }

    /// <summary>Takes in a piece of a picture.</summary>
    /// <param name="data">The block's data section.</param>
    /// <param name="builder">Receives the bytes.</param>
    internal static void ReadContinuation(ReadOnlyMemory<byte> data, U3dModelBuilder builder)
    {
        var reader = builder.Open(data, null);
        var name = reader.ReadString();
        var index = reader.ReadU32();
        if (reader.Failed || !builder.TexturePieces.TryGetValue(name, out var pieces) || index >= pieces.Data.Length)
        {
            builder.Problem("A texture continuation names a texture or image the file does not declare.");
            return;
        }

        var bytes = data.Span[reader.BytePosition..];
        var held = pieces.Data[index];
        if ((long)held.Count + bytes.Length > (long)builder.Limits.MaxTexturePixels * PixelBytes)
        {
            builder.Problem("A texture holds more picture data than will be read.");
            return;
        }

        held.AddRange(bytes);
    }

    /// <summary>Decodes the pictures of every declared texture.</summary>
    /// <param name="builder">Holds the declarations and the picture data, and receives problems.</param>
    /// <returns>The textures.</returns>
    internal static U3dTexture[] Finish(U3dModelBuilder builder)
    {
        var textures = new List<U3dTexture>();
        foreach (var pieces in builder.TexturePieces.Values)
        {
            textures.Add(Compose(pieces, builder));
        }

        return [.. textures];
    }

    /// <summary>Reads one continuation image description.</summary>
    /// <param name="reader">The block's reader, placed at the description.</param>
    /// <returns>The image.</returns>
    private static U3dContinuationImage ReadImage(ref U3dBitReader reader)
    {
        var compression = reader.ReadU8();
        var channels = reader.ReadU8();
        var attributes = reader.ReadU16();
        var external = (attributes & ExternalImage) != 0;
        if (external)
        {
            var urls = reader.ReadU32();
            for (uint i = 0; i < urls && !reader.Failed; i++)
            {
                _ = reader.ReadString();
            }
        }
        else
        {
            _ = reader.ReadU32();
        }

        return new(compression, channels, external);
    }

    /// <summary>Builds a texture from its continuation images.</summary>
    /// <param name="pieces">The declaration and picture data.</param>
    /// <param name="builder">Receives problems.</param>
    /// <returns>The texture; its pixels are empty when no picture could be decoded.</returns>
    private static U3dTexture Compose(U3dTexturePieces pieces, U3dModelBuilder builder)
    {
        byte[] rgba = [];
        var width = pieces.Width;
        var height = pieces.Height;
        for (var i = 0; i < pieces.Images.Length; i++)
        {
            var image = Decode(pieces, i, builder);
            if (image is null)
            {
                continue;
            }

            if (rgba.Length == 0)
            {
                // The picture's own size is the texture's size; a declaration that disagrees is noted, not trusted.
                width = image.Width;
                height = image.Height;
                rgba = NewPixels(image);
                if (width != pieces.Width || height != pieces.Height)
                {
                    builder.Problem($"The texture {pieces.Name} is not the size its declaration gives; the picture's size is used.");
                }
            }

            Place(rgba, image, pieces.Images[i].Channels, builder);
        }

        return new(pieces.Name, width, height, rgba);
    }

    /// <summary>Decodes the picture of one continuation image.</summary>
    /// <param name="pieces">The declaration and picture data.</param>
    /// <param name="index">The continuation image's index.</param>
    /// <param name="builder">Receives problems.</param>
    /// <returns>The picture, or <see langword="null"/> when it is external, missing, in a format that is not decoded, or damaged.</returns>
    private static PdfRasterImage? Decode(U3dTexturePieces pieces, int index, U3dModelBuilder builder)
    {
        var image = pieces.Images[index];
        var bytes = pieces.Data[index];
        if (image.IsExternal || bytes.Count == 0)
        {
            builder.Problem($"The texture {pieces.Name} has a picture in another file or with no data, which is not loaded.");
            return null;
        }

        if (image.Compression is not (PngCompression or TiffCompression))
        {
            builder.Problem($"The texture {pieces.Name} is a JPEG picture, which is not decoded; the surface shows its material colour.");
            return null;
        }

        try
        {
            return PdfImageFiles.Decode([.. bytes], builder.Token);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            builder.Problem($"The texture {pieces.Name} holds a picture that could not be decoded: {ex.Message}");
            return null;
        }
    }

    /// <summary>Makes the pixel array of a picture's size, opaque black.</summary>
    /// <param name="image">The first decoded picture.</param>
    /// <returns>The pixels.</returns>
    private static byte[] NewPixels(PdfRasterImage image)
    {
        var pixels = new byte[checked(image.Width * image.Height * PixelBytes)];
        for (var i = PixelBytes - 1; i < pixels.Length; i += PixelBytes)
        {
            pixels[i] = Opaque;
        }

        return pixels;
    }

    /// <summary>Copies the channels a continuation image supplies into the texture's pixels.</summary>
    /// <param name="rgba">The texture's pixels.</param>
    /// <param name="image">The decoded picture.</param>
    /// <param name="channels">The channels the picture supplies.</param>
    /// <param name="builder">Receives a problem when the picture's size differs from the first picture's.</param>
    private static void Place(byte[] rgba, PdfRasterImage image, byte channels, U3dModelBuilder builder)
    {
        if ((long)image.Width * image.Height * PixelBytes != rgba.Length)
        {
            builder.Problem("The pictures of one texture differ in size, so one was left out.");
            return;
        }

        var pixels = image.Pixels.Span;
        for (var i = 0; i < image.Width * image.Height; i++)
        {
            var (r, g, b, a) = PixelAt(image, pixels, i);
            Put(rgba.AsSpan(i * PixelBytes, PixelBytes), channels, r, g, b, a);
        }
    }

    /// <summary>Sets the channels of a pixel that a continuation image supplies.</summary>
    /// <param name="pixel">The four bytes of the texture's pixel.</param>
    /// <param name="channels">The channel bits.</param>
    /// <param name="r">The picture's red.</param>
    /// <param name="g">The picture's green.</param>
    /// <param name="b">The picture's blue.</param>
    /// <param name="a">The picture's alpha.</param>
    private static void Put(Span<byte> pixel, byte channels, byte r, byte g, byte b, byte a)
    {
        var luminance = (channels & LuminanceChannel) != 0;
        pixel[0] = (channels & RedChannel) != 0 || luminance ? r : pixel[0];
        pixel[1] = (channels & GreenChannel) != 0 || luminance ? g : pixel[1];
        pixel[2] = (channels & BlueChannel) != 0 || luminance ? b : pixel[2];
        pixel[3] = (channels & AlphaChannel) != 0 ? a : pixel[3];
    }

    /// <summary>Reads a pixel of a decoded picture as red, green, blue and alpha; a picture without alpha gives its red as alpha, so a grey picture can supply alpha.</summary>
    /// <param name="image">The picture.</param>
    /// <param name="pixels">The picture's bytes.</param>
    /// <param name="index">The pixel's index in reading order.</param>
    /// <returns>The pixel.</returns>
    private static U3dPixel PixelAt(PdfRasterImage image, ReadOnlySpan<byte> pixels, int index)
    {
        var x = index % image.Width;
        var row = pixels[((index / image.Width) * image.RowBytes)..];
        switch (image.Format)
        {
            case PdfRasterFormat.Gray8:
            {
                return new(row[x], row[x], row[x], row[x]);
            }

            case PdfRasterFormat.Rgb24:
            {
                var at = x * RgbBytes;
                return new(row[at], row[at + SecondChannel], row[at + ThirdChannel], row[at]);
            }

            case PdfRasterFormat.Bgra32:
            {
                var at = x * PixelBytes;
                return new(row[at + ThirdChannel], row[at + SecondChannel], row[at], row[at + FourthChannel]);
            }

            default:
            {
                var white = (row[x / ByteBits] >> (ByteBits - 1 - (x % ByteBits))) & 1;
                var level = white == 0 ? (byte)0 : Opaque;
                return new(level, level, level, level);
            }
        }
    }
}
