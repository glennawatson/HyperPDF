// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Reads the first frame of a GIF file, placed on the file's screen, with its transparent colour if it has one.</summary>
internal static class GifFileDecoder
{
    /// <summary>The bytes of the signature and version.</summary>
    private const int SignatureBytes = 6;

    /// <summary>The bytes after the packed byte of the screen descriptor: the background index and the aspect ratio.</summary>
    private const int ScreenTailBytes = 2;

    /// <summary>The bytes of a colour table entry.</summary>
    private const int EntryBytes = 3;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The offset of the green sample in a pixel or colour table entry.</summary>
    private const int GreenIndex = 1;

    /// <summary>The offset of the blue sample in an RGB entry, or the red sample in a BGRA pixel.</summary>
    private const int BlueIndex = 2;

    /// <summary>The block that starts an extension.</summary>
    private const byte ExtensionIntroducer = 0x21;

    /// <summary>The block that starts an image.</summary>
    private const byte ImageSeparator = 0x2C;

    /// <summary>The extension label of a graphic control block.</summary>
    private const byte GraphicControl = 0xF9;

    /// <summary>The flag in a packed byte that says a colour table follows.</summary>
    private const int TableFlag = 0x80;

    /// <summary>The flag in an image's packed byte that says its rows are interlaced.</summary>
    private const int InterlaceFlag = 0x40;

    /// <summary>The mask of the table size bits in a packed byte.</summary>
    private const int TableSizeMask = 0x07;

    /// <summary>The flag in a graphic control block that says a colour is transparent.</summary>
    private const int TransparentFlag = 0x01;

    /// <summary>The offset of the transparent colour index in a graphic control block's data.</summary>
    private const int TransparentIndexOffset = 3;

    /// <summary>The bytes of a graphic control block's data.</summary>
    private const int GraphicControlBytes = 4;

    /// <summary>Gets the first row of each interlace pass.</summary>
    private static ReadOnlySpan<byte> InterlaceStart => [0x00, 0x04, 0x02, 0x01];

    /// <summary>Gets the row step of each interlace pass.</summary>
    private static ReadOnlySpan<byte> InterlaceStep => [0x08, 0x08, 0x04, 0x02];

    /// <summary>Reads a GIF file.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>The first frame.</returns>
    /// <exception cref="InvalidDataException">The file is cut short, has no image, or has damaged image data.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfRasterImage Decode(ReadOnlySpan<byte> file, CancellationToken cancellationToken)
    {
        var cursor = new GifCursor(file.ToArray());
        cursor.Skip(SignatureBytes);
        var width = cursor.ReadUInt16();
        var height = cursor.ReadUInt16();
        ImageFileLimits.CheckSize(width, height);
        var packed = cursor.ReadByte();
        cursor.Skip(ScreenTailBytes);
        var table = (packed & TableFlag) != 0 ? cursor.ReadBytes(GetTableBytes(packed)) : [];
        return ReadBlocks(cursor, new(width, height, table, -1), cancellationToken);
    }

    /// <summary>Gets the bytes of a colour table from the packed byte that describes it.</summary>
    /// <param name="packed">The packed byte.</param>
    /// <returns>The bytes.</returns>
    private static int GetTableBytes(int packed) => EntryBytes << ((packed & TableSizeMask) + 1);

    /// <summary>Reads blocks up to the first image.</summary>
    /// <param name="cursor">The reader.</param>
    /// <param name="screen">The screen so far.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">The file has no image.</exception>
    private static PdfRasterImage ReadBlocks(GifCursor cursor, GifScreen screen, CancellationToken cancellationToken)
    {
        while (!cursor.AtEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var block = cursor.ReadByte();
            if (block == ImageSeparator)
            {
                return ReadImage(cursor, screen, cancellationToken);
            }

            if (block != ExtensionIntroducer)
            {
                break;
            }

            screen = screen with { Transparent = ReadExtension(cursor, screen.Transparent) };
        }

        throw new InvalidDataException("The GIF has no image.");
    }

    /// <summary>Reads an extension block, keeping the transparent colour of a graphic control block.</summary>
    /// <param name="cursor">The reader, after the introducer.</param>
    /// <param name="transparent">The transparent colour index found so far, or -1.</param>
    /// <returns>The transparent colour index, or -1.</returns>
    private static int ReadExtension(GifCursor cursor, int transparent)
    {
        var label = cursor.ReadByte();
        var result = transparent;
        var length = cursor.ReadByte();
        while (length != 0)
        {
            if (label == GraphicControl && length >= GraphicControlBytes)
            {
                result = (cursor.Peek(0) & TransparentFlag) != 0 ? cursor.Peek(TransparentIndexOffset) : -1;
            }

            cursor.Skip(length);
            length = cursor.ReadByte();
        }

        return result;
    }

    /// <summary>Reads an image block and places it on the screen.</summary>
    /// <param name="cursor">The reader, after the image separator.</param>
    /// <param name="screen">The screen size, global colour table and transparent colour.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">The image is cut short or its data is damaged.</exception>
    private static PdfRasterImage ReadImage(GifCursor cursor, in GifScreen screen, CancellationToken cancellationToken)
    {
        var left = cursor.ReadUInt16();
        var top = cursor.ReadUInt16();
        var width = cursor.ReadUInt16();
        var height = cursor.ReadUInt16();
        var packed = cursor.ReadByte();
        var table = (packed & TableFlag) != 0 ? cursor.ReadBytes(GetTableBytes(packed)) : screen.Table;
        if (table.Length == 0)
        {
            throw new InvalidDataException("The GIF image has no colours.");
        }

        var minimumCodeSize = cursor.ReadByte();
        var indexes = GifLzw.Decode(cursor.ReadSubBlocks(), minimumCodeSize, width * height, cancellationToken);
        return Compose(screen, new(left, top, width, height, (packed & InterlaceFlag) != 0), table, indexes);
    }

    /// <summary>Puts a frame's colours on the screen.</summary>
    /// <param name="screen">The screen.</param>
    /// <param name="frame">Where the frame sits and how its rows run.</param>
    /// <param name="table">The colour table.</param>
    /// <param name="indexes">The frame's colour indexes, in the order they were stored.</param>
    /// <returns>The picture: RGB when the frame fills an opaque screen, otherwise BGRA with transparency.</returns>
    private static PdfRasterImage Compose(in GifScreen screen, in GifFrame frame, byte[] table, byte[] indexes)
    {
        var bgra = new byte[screen.Width * screen.Height * BgraBytes];
        for (var stored = 0; stored < frame.Height; stored++)
        {
            var y = frame.Top + (frame.Interlaced ? InterlacedRow(stored, frame.Height) : stored);
            if (y < screen.Height)
            {
                PaintRow(bgra, new(screen, table, frame, y), indexes.AsSpan(stored * frame.Width, frame.Width));
            }
        }

        var covers = frame.Left == 0 && frame.Top == 0 && frame.Width >= screen.Width && frame.Height >= screen.Height;
        return covers && screen.Transparent < 0 ? DropAlpha(screen, bgra) : new(PdfRasterFormat.Bgra32, screen.Width, screen.Height, bgra);
    }

    /// <summary>Paints one stored row of a frame.</summary>
    /// <param name="bgra">The screen's pixels.</param>
    /// <param name="place">The screen, colour table, frame and the screen row the row belongs to.</param>
    /// <param name="row">The row's colour indexes.</param>
    private static void PaintRow(byte[] bgra, in GifRowPlace place, ReadOnlySpan<byte> row)
    {
        var entries = place.Table.Length / EntryBytes;
        for (var column = 0; column < row.Length && place.Frame.Left + column < place.Screen.Width; column++)
        {
            var colour = Math.Min((int)row[column], entries - 1) * EntryBytes;
            var target = ((place.Y * place.Screen.Width) + place.Frame.Left + column) * BgraBytes;
            bgra[target] = place.Table[colour + BlueIndex];
            bgra[target + GreenIndex] = place.Table[colour + GreenIndex];
            bgra[target + BlueIndex] = place.Table[colour];
            bgra[target + EntryBytes] = row[column] == place.Screen.Transparent ? (byte)0 : byte.MaxValue;
        }
    }

    /// <summary>Drops the alpha of an opaque picture.</summary>
    /// <param name="screen">The screen.</param>
    /// <param name="bgra">The pixels.</param>
    /// <returns>The picture as RGB.</returns>
    private static PdfRasterImage DropAlpha(in GifScreen screen, byte[] bgra)
    {
        var rgb = new byte[screen.Width * screen.Height * EntryBytes];
        for (var i = 0; i < screen.Width * screen.Height; i++)
        {
            rgb[i * EntryBytes] = bgra[(i * BgraBytes) + BlueIndex];
            rgb[(i * EntryBytes) + GreenIndex] = bgra[(i * BgraBytes) + GreenIndex];
            rgb[(i * EntryBytes) + BlueIndex] = bgra[i * BgraBytes];
        }

        return new(PdfRasterFormat.Rgb24, screen.Width, screen.Height, rgb);
    }

    /// <summary>Gets the row an interlaced frame stores at a position.</summary>
    /// <param name="stored">The position among the stored rows.</param>
    /// <param name="height">The frame's height.</param>
    /// <returns>The row it belongs to.</returns>
    private static int InterlacedRow(int stored, int height)
    {
        var remaining = stored;
        for (var pass = 0; pass < InterlaceStart.Length; pass++)
        {
            var rows = (height - InterlaceStart[pass] + InterlaceStep[pass] - 1) / InterlaceStep[pass];
            if (remaining < rows)
            {
                return InterlaceStart[pass] + (remaining * InterlaceStep[pass]);
            }

            remaining -= rows;
        }

        return stored;
    }
}
