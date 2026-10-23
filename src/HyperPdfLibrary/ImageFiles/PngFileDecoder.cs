// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Reads PNG files: every colour type and bit depth, interlaced or not, with palette and simple transparency.</summary>
internal static class PngFileDecoder
{
    /// <summary>The bytes of a chunk's length and type.</summary>
    private const int ChunkHead = 8;

    /// <summary>The bytes of a chunk's checksum.</summary>
    private const int ChunkTail = 4;

    /// <summary>The bytes of a chunk's length or type.</summary>
    private const int ChunkWord = 4;

    /// <summary>The bytes in the header chunk's data.</summary>
    private const int HeaderBytes = 13;

    /// <summary>The offset of the height in the header chunk's data.</summary>
    private const int HeightOffset = 4;

    /// <summary>The offset of the bit depth in the header chunk's data.</summary>
    private const int DepthOffset = 8;

    /// <summary>The offset of the colour type in the header chunk's data.</summary>
    private const int ColorTypeOffset = 9;

    /// <summary>The offset of the interlace method in the header chunk's data.</summary>
    private const int InterlaceOffset = 12;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in a 16-bit sample.</summary>
    private const int WideBits = 16;

    /// <summary>The bytes in a 16-bit sample.</summary>
    private const int WideBytes = 2;

    /// <summary>The passes of an interlaced image.</summary>
    private const int InterlacePasses = 7;

    /// <summary>Gets the signature every PNG file starts with.</summary>
    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Gets the first column of each interlace pass.</summary>
    private static ReadOnlySpan<byte> PassStartX => [0x00, 0x04, 0x00, 0x02, 0x00, 0x01, 0x00];

    /// <summary>Gets the first row of each interlace pass.</summary>
    private static ReadOnlySpan<byte> PassStartY => [0x00, 0x00, 0x04, 0x00, 0x02, 0x00, 0x01];

    /// <summary>Gets the column step of each interlace pass.</summary>
    private static ReadOnlySpan<byte> PassStepX => [0x08, 0x08, 0x04, 0x04, 0x02, 0x02, 0x01];

    /// <summary>Gets the row step of each interlace pass.</summary>
    private static ReadOnlySpan<byte> PassStepY => [0x08, 0x08, 0x08, 0x04, 0x04, 0x02, 0x02];

    /// <summary>Reads a PNG file.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="cancellationToken">Cancels the decode between rows.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">The file is damaged, truncated, or declares a size or layout the PNG standard does not allow.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfRasterImage Decode(ReadOnlySpan<byte> file, CancellationToken cancellationToken)
    {
        if (!file.StartsWith(Signature))
        {
            throw new InvalidDataException("The file is not a PNG picture.");
        }

        var parts = ReadChunks(file);
        var header = parts.Header ?? throw new InvalidDataException("The PNG has no header chunk.");
        if (PngEightBit.TryDecode(parts, cancellationToken) is { } direct)
        {
            return direct;
        }

        var raw = Inflate(parts.Data, header, cancellationToken);
        var samples = Unpack(raw, header, cancellationToken);
        return PngPixels.Convert(header, samples, parts.Palette, parts.Transparency);
    }

    /// <summary>Reads and checks the header chunk.</summary>
    /// <param name="body">The chunk's data.</param>
    /// <returns>The header.</returns>
    /// <exception cref="InvalidDataException">The header is short or declares something PNG does not allow.</exception>
    internal static PngHeader ReadHeader(ReadOnlySpan<byte> body)
    {
        if (body.Length < HeaderBytes)
        {
            throw new InvalidDataException("The PNG header chunk is too short.");
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(body);
        var height = BinaryPrimitives.ReadUInt32BigEndian(body[HeightOffset..]);
        ImageFileLimits.CheckSize(width, height);
        var header = new PngHeader((int)width, (int)height, body[DepthOffset], body[ColorTypeOffset], body[InterlaceOffset] != 0);
        if (!header.IsValid)
        {
            throw new InvalidDataException("The PNG declares a bit depth its colour type does not allow.");
        }

        return header;
    }

    /// <summary>Reads the chunks of a file.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <returns>The header, the palette, the transparency chunk and the joined image data.</returns>
    /// <exception cref="InvalidDataException">A chunk is cut short or the header is not valid.</exception>
    private static PngParts ReadChunks(ReadOnlySpan<byte> file)
    {
        var builder = new PngBuilder();
        var position = Signature.Length;
        while (position + ChunkHead + ChunkTail <= file.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(file[position..]);
            var dataStart = position + ChunkHead;
            if (length > file.Length - dataStart - ChunkTail)
            {
                throw new InvalidDataException("A PNG chunk runs past the end of the file.");
            }

            if (!builder.Add(file.Slice(position + ChunkWord, ChunkWord), file.Slice(dataStart, (int)length)))
            {
                break;
            }

            position = dataStart + (int)length + ChunkTail;
        }

        return builder.ToParts();
    }

    /// <summary>Decompresses the image data and checks it holds every row.</summary>
    /// <param name="data">The joined image data chunks.</param>
    /// <param name="header">The header.</param>
    /// <param name="cancellationToken">Cancels the decompression.</param>
    /// <returns>The filtered rows.</returns>
    /// <exception cref="InvalidDataException">The data is damaged or too short.</exception>
    private static byte[] Inflate(byte[] data, PngHeader header, CancellationToken cancellationToken)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = ZLibCodec.Decompress(data, false, ref output, cancellationToken);
            var raw = output.ToArray();
            if (raw.Length < GetFilteredLength(header))
            {
                throw new InvalidDataException("The PNG's image data is shorter than its size needs.");
            }

            return raw;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Gets the bytes the filtered rows of all passes take.</summary>
    /// <param name="header">The header.</param>
    /// <returns>The length.</returns>
    private static long GetFilteredLength(PngHeader header)
    {
        if (!header.Interlaced)
        {
            return (long)header.Height * (1 + header.GetStride(header.Width));
        }

        long total = 0;
        for (var pass = 0; pass < InterlacePasses; pass++)
        {
            var size = GetPassSize(header, pass);
            if (size.Height > 0 && size.Width > 0)
            {
                total += (long)size.Height * (1 + header.GetStride(size.Width));
            }
        }

        return total;
    }

    /// <summary>Gets the size of one interlace pass.</summary>
    /// <param name="header">The header.</param>
    /// <param name="pass">The pass from 0 to 6.</param>
    /// <returns>The pass's width and height in pixels.</returns>
    private static PassSize GetPassSize(PngHeader header, int pass) => new(
        (header.Width - PassStartX[pass] + PassStepX[pass] - 1) / PassStepX[pass],
        (header.Height - PassStartY[pass] + PassStepY[pass] - 1) / PassStepY[pass]);

    /// <summary>Undoes the row filters and lays the samples out as one array, a pass at a time for an interlaced image.</summary>
    /// <param name="raw">The filtered rows.</param>
    /// <param name="header">The header.</param>
    /// <param name="cancellationToken">Cancels the unpacking between rows.</param>
    /// <returns>The samples, row by row, in the picture's size.</returns>
    private static ushort[] Unpack(byte[] raw, PngHeader header, CancellationToken cancellationToken)
    {
        var samples = new ushort[(long)header.Width * header.Height * header.Channels];
        var offset = 0;
        var passes = header.Interlaced ? InterlacePasses : 1;
        for (var pass = 0; pass < passes; pass++)
        {
            var size = header.Interlaced ? GetPassSize(header, pass) : new PassSize(header.Width, header.Height);
            if (size.Width == 0 || size.Height == 0)
            {
                continue;
            }

            var layout = header.Interlaced
                ? new PassLayout(PassStartX[pass], PassStartY[pass], PassStepX[pass], PassStepY[pass])
                : new PassLayout(0, 0, 1, 1);
            offset = UnpackPass(new(raw, offset, header, samples), size, layout, cancellationToken);
        }

        return samples;
    }

    /// <summary>Unfilters one pass and scatters its samples into the picture.</summary>
    /// <param name="source">The filtered rows, where this pass starts in them, the header and the samples to fill.</param>
    /// <param name="size">The pass's size in pixels.</param>
    /// <param name="layout">Where the pass's pixels sit in the picture.</param>
    /// <param name="cancellationToken">Cancels between rows.</param>
    /// <returns>Where the next pass starts in the filtered rows.</returns>
    private static int UnpackPass(in PngPassSource source, in PassSize size, in PassLayout layout, CancellationToken cancellationToken)
    {
        var header = source.Header;
        var stride = header.GetStride(size.Width);
        var bytesPerPixel = Math.Max(1, header.BitsPerPixel / ByteBits);
        var prior = new byte[stride];
        var offset = source.Offset;
        for (var row = 0; row < size.Height; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = source.Raw.AsSpan(offset + 1, stride);
            PngUnfilter.Apply(source.Raw[offset], line, prior, bytesPerPixel);
            ScatterRow(line, source, new(size.Width, layout.StartY + (row * layout.StepY)), layout);
            line.CopyTo(prior);
            offset += stride + 1;
        }

        return offset;
    }

    /// <summary>Places the samples of an unfiltered row in the picture.</summary>
    /// <param name="line">The unfiltered row.</param>
    /// <param name="source">The filtered rows, the header and the samples to fill.</param>
    /// <param name="row">The row's pixel count and its row in the picture.</param>
    /// <param name="layout">Where the pass's pixels sit in the picture.</param>
    private static void ScatterRow(ReadOnlySpan<byte> line, in PngPassSource source, in RowPlace row, in PassLayout layout)
    {
        var header = source.Header;
        var channels = header.Channels;
        for (var column = 0; column < row.Pixels; column++)
        {
            var target = (((long)row.Row * header.Width) + layout.StartX + (column * layout.StepX)) * channels;
            for (var channel = 0; channel < channels; channel++)
            {
                source.Samples[target + channel] = ReadSample(line, header.Depth, (column * channels) + channel);
            }
        }
    }

    /// <summary>Reads one sample from an unfiltered row.</summary>
    /// <param name="line">The row.</param>
    /// <param name="depth">The bits in a sample.</param>
    /// <param name="index">The sample's index in the row.</param>
    /// <returns>The sample.</returns>
    private static ushort ReadSample(ReadOnlySpan<byte> line, int depth, int index)
    {
        if (depth == ByteBits)
        {
            return line[index];
        }

        if (depth == WideBits)
        {
            return BinaryPrimitives.ReadUInt16BigEndian(line[(index * WideBytes)..]);
        }

        var bit = index * depth;
        return (ushort)((line[bit / ByteBits] >> (ByteBits - depth - (bit % ByteBits))) & ((1 << depth) - 1));
    }
}
