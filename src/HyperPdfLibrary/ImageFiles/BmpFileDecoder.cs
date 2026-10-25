// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Reads Windows bitmap files: 1 to 32 bits a pixel, with palettes, colour masks and run-length coding.</summary>
internal static class BmpFileDecoder
{
    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The offset of the green sample in a colour.</summary>
    private const int GreenIndex = 1;

    /// <summary>The offset of the last sample in a three-sample colour.</summary>
    private const int BlueIndex = 2;

    /// <summary>Reads a bitmap file.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="cancellationToken">Cancels the decode between rows.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">The file is cut short, or declares something this reader does not handle.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfRasterImage Decode(ReadOnlySpan<byte> file, CancellationToken cancellationToken)
    {
        var header = BmpHeaderReader.Read(file);
        if (header.BitsPerPixel > ByteBits)
        {
            return BmpPixels.FromMasks(file, header, cancellationToken);
        }

        var runLength = header.Compression is BmpHeaderReader.CompressionRle8 or BmpHeaderReader.CompressionRle4;
        var indexes = runLength ? BmpRunLength.Decode(file[header.DataOffset..], header, cancellationToken) : null;
        return BmpPixels.FromPalette(file, header, ReadPalette(file, header), indexes, cancellationToken);
    }

    /// <summary>Reads the palette of a bitmap with eight bits a pixel or fewer.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="header">The header.</param>
    /// <returns>Red, green and blue bytes for each entry; empty when there is no palette.</returns>
    /// <exception cref="InvalidDataException">The palette runs past the end of the file.</exception>
    private static byte[] ReadPalette(ReadOnlySpan<byte> file, in BmpHeader header)
    {
        if (header.PaletteEntries == 0)
        {
            return [];
        }

        if (header.PaletteOffset + (header.PaletteEntries * header.PaletteEntryBytes) > file.Length)
        {
            throw new InvalidDataException("The bitmap's palette is cut short.");
        }

        var palette = new byte[header.PaletteEntries * RgbBytes];
        for (var i = 0; i < header.PaletteEntries; i++)
        {
            var entry = header.PaletteOffset + (i * header.PaletteEntryBytes);
            palette[i * RgbBytes] = file[entry + BlueIndex];
            palette[(i * RgbBytes) + GreenIndex] = file[entry + GreenIndex];
            palette[(i * RgbBytes) + BlueIndex] = file[entry];
        }

        return palette;
    }
}
