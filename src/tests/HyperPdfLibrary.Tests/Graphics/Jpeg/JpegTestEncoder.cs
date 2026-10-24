// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>
/// Writes tiny baseline JPEGs whose 8x8 blocks are each one flat value per component (DC coefficient only), with an
/// optional Adobe marker and restart interval. Every component is sampled 1x1.
/// </summary>
internal static class JpegTestEncoder
{
    /// <summary>The pixels in a block side.</summary>
    internal const int BlockSide = 8;

    /// <summary>The Adobe transform value that means no marker is written.</summary>
    internal const int NoAdobe = -1;

    /// <summary>The value of a mid-gray sample.</summary>
    private const int Center = 128;

    /// <summary>The DC coefficient per sample level: eight times the level shift.</summary>
    private const int DcScale = 8;

    /// <summary>The bits of a DC size code: sixteen codes fill a five-bit space without using the all-ones code.</summary>
    private const int SizeCodeBits = 5;

    /// <summary>The bytes of a DNL segment including its length field.</summary>
    private const byte DnlLength = 4;

    /// <summary>The sample precision of an extended sequential frame.</summary>
    private const int ExtendedPrecision = 12;

    /// <summary>The level shift of 12-bit samples.</summary>
    private const int ExtendedCenter = 2048;

    /// <summary>The shift from an 8-bit test value to a 12-bit sample.</summary>
    private const int ExtendedShift = 4;

    /// <summary>The bytes of a DRI segment including its length field.</summary>
    private const byte RestartLength = 4;

    /// <summary>The size categories that have a code.</summary>
    private const int SizeCategories = 16;

    /// <summary>The coefficients in a quantization table.</summary>
    private const int TableLength = 64;

    /// <summary>The length of the DHT counts.</summary>
    private const int CountBytes = 16;

    /// <summary>The marker prefix.</summary>
    private const byte Prefix = 0xFF;

    /// <summary>The first restart marker.</summary>
    private const byte FirstRestart = 0xD0;

    /// <summary>The restart markers cycle through eight values.</summary>
    private const int RestartCycle = 8;

    /// <summary>The version word written in the Adobe marker.</summary>
    private const byte AdobeVersion = 100;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes of the Adobe marker payload including the length field.</summary>
    private const int AdobeLength = 14;

    /// <summary>The bytes of the length field and the table class byte, in a DHT with one table.</summary>
    private const int HuffmanOverhead = 3;

    /// <summary>The bytes of a quantization segment: length, precision byte and the table.</summary>
    private const int QuantLength = 67;

    /// <summary>The bytes of a start-of-scan header before its component entries.</summary>
    private const int ScanFixedLength = 6;

    /// <summary>The bytes of a frame header before its component entries.</summary>
    private const int FrameFixedLength = 8;

    /// <summary>The bytes of a component entry in a frame header.</summary>
    private const int FrameEntryLength = 3;

    /// <summary>The bytes of a component entry in a scan header.</summary>
    private const int ScanEntryLength = 2;

    /// <summary>The sampling byte for 1x1.</summary>
    private const byte OneByOne = 0x11;

    /// <summary>The last zigzag index, written as the end of the spectral band.</summary>
    private const byte LastCoefficient = 63;

    /// <summary>Encodes a JPEG.</summary>
    /// <param name="layout">The block layout and markers.</param>
    /// <param name="values">One value per block and component, block by block.</param>
    /// <returns>The JPEG bytes.</returns>
    internal static byte[] Encode(in JpegTestLayout layout, ReadOnlySpan<byte> values)
    {
        var output = new List<byte> { Prefix, 0xD8 };
        if (layout.AdobeTransform != NoAdobe)
        {
            WriteAdobe(output, layout.AdobeTransform);
        }

        WriteQuant(output);
        WriteFrame(output, layout);
        WriteHuffman(output, 0x00, CountsOf(SizeCategories, SizeCodeBits), Sequence(SizeCategories));
        WriteHuffman(output, 0x10, CountsOf(1, 1), [0]);
        if (layout.RestartInterval != 0)
        {
            var interval = layout.RestartInterval;
            output.AddRange([Prefix, 0xDD, 0, RestartLength, (byte)(interval >> ByteBits), (byte)(interval & byte.MaxValue)]);
        }

        WriteScan(output, layout, values);
        if (layout.UseDnl)
        {
            var height = layout.BlocksDown * BlockSide;
            output.AddRange([Prefix, 0xDC, 0, DnlLength, (byte)(height >> ByteBits), (byte)(height & byte.MaxValue)]);
        }

        output.AddRange([Prefix, 0xD9]);
        return [.. output];
    }

    /// <summary>Builds the 16 Huffman counts with a number of codes of one length.</summary>
    /// <param name="count">The number of codes.</param>
    /// <param name="length">The code length.</param>
    /// <returns>The counts.</returns>
    private static byte[] CountsOf(int count, int length)
    {
        var counts = new byte[CountBytes];
        counts[length - 1] = (byte)count;
        return counts;
    }

    /// <summary>Builds the symbols 0 up to a count.</summary>
    /// <param name="count">The count.</param>
    /// <returns>The symbols.</returns>
    private static byte[] Sequence(int count)
    {
        var symbols = new byte[count];
        for (var i = 0; i < count; i++)
        {
            symbols[i] = (byte)i;
        }

        return symbols;
    }

    /// <summary>Writes the Adobe APP14 marker.</summary>
    /// <param name="output">The output.</param>
    /// <param name="transform">The transform byte.</param>
    private static void WriteAdobe(List<byte> output, int transform)
    {
        output.AddRange([Prefix, 0xEE, 0, AdobeLength]);
        output.AddRange("Adobe"u8.ToArray());
        output.AddRange([0, AdobeVersion, 0, 0, 0, 0, (byte)transform]);
    }

    /// <summary>Writes a quantization table of ones.</summary>
    /// <param name="output">The output.</param>
    private static void WriteQuant(List<byte> output)
    {
        output.AddRange([Prefix, 0xDB, 0, QuantLength, 0]);
        for (var i = 0; i < TableLength; i++)
        {
            output.Add(1);
        }
    }

    /// <summary>Writes a baseline frame header.</summary>
    /// <param name="output">The output.</param>
    /// <param name="layout">The layout.</param>
    private static void WriteFrame(List<byte> output, in JpegTestLayout layout)
    {
        var length = FrameFixedLength + (layout.Components * FrameEntryLength);
        var width = layout.BlocksAcross * BlockSide;
        var height = layout.UseDnl ? 0 : layout.BlocksDown * BlockSide;
        output.AddRange([Prefix, layout.Precision == ExtendedPrecision ? (byte)0xC1 : (byte)0xC0, 0, (byte)length, (byte)layout.Precision]);
        output.AddRange([(byte)(height >> ByteBits), (byte)(height & byte.MaxValue), (byte)(width >> ByteBits), (byte)(width & byte.MaxValue)]);
        output.Add((byte)layout.Components);
        for (var c = 0; c < layout.Components; c++)
        {
            output.AddRange([(byte)(c + 1), OneByOne, 0]);
        }
    }

    /// <summary>Writes one Huffman table.</summary>
    /// <param name="output">The output.</param>
    /// <param name="classAndId">The table class and id byte.</param>
    /// <param name="counts">The code counts.</param>
    /// <param name="symbols">The symbols.</param>
    private static void WriteHuffman(List<byte> output, byte classAndId, byte[] counts, byte[] symbols)
    {
        var length = HuffmanOverhead + counts.Length + symbols.Length;
        output.AddRange([Prefix, 0xC4, 0, (byte)length, classAndId]);
        output.AddRange(counts);
        output.AddRange(symbols);
    }

    /// <summary>Writes the scan header and the entropy-coded blocks.</summary>
    /// <param name="output">The output.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="values">The block values.</param>
    private static void WriteScan(List<byte> output, in JpegTestLayout layout, ReadOnlySpan<byte> values)
    {
        var length = ScanFixedLength + (layout.Components * ScanEntryLength);
        output.AddRange([Prefix, 0xDA, 0, (byte)length, (byte)layout.Components]);
        for (var c = 0; c < layout.Components; c++)
        {
            output.AddRange([(byte)(c + 1), 0]);
        }

        output.AddRange([0, LastCoefficient, 0]);
        var writer = new BitWriter(output);
        var predictors = new int[layout.Components];
        var blocks = layout.BlocksAcross * layout.BlocksDown;
        for (var block = 0; block < blocks; block++)
        {
            if (layout.RestartInterval != 0 && block != 0 && block % layout.RestartInterval == 0)
            {
                writer.Restart((byte)(FirstRestart + (((block / layout.RestartInterval) - 1) % RestartCycle)));
                Array.Clear(predictors);
            }

            for (var c = 0; c < layout.Components; c++)
            {
                var value = values[(block * layout.Components) + c];
                var dc = (layout.Precision == ExtendedPrecision ? (value << ExtendedShift) - ExtendedCenter : value - Center) * DcScale;
                writer.WriteDc(dc - predictors[c]);
                predictors[c] = dc;
            }
        }

        writer.Flush();
    }

    /// <summary>Packs bits into entropy-coded bytes with 0xFF stuffing.</summary>
    private sealed class BitWriter
    {
        /// <summary>The output bytes.</summary>
        private readonly List<byte> _output;

        /// <summary>The bits not yet written.</summary>
        private int _bits;

        /// <summary>The number of pending bits.</summary>
        private int _count;

        /// <summary>Initializes a new instance of the <see cref="BitWriter"/> class.</summary>
        /// <param name="output">The output.</param>
        internal BitWriter(List<byte> output) => _output = output;

        /// <summary>Writes a DC difference followed by an end-of-block code.</summary>
        /// <param name="difference">The difference.</param>
        internal void WriteDc(int difference)
        {
            var magnitude = Math.Abs(difference);
            var size = magnitude == 0 ? 0 : (sizeof(int) * ByteBits) - int.LeadingZeroCount(magnitude);
            Write(size, SizeCodeBits);
            if (size > 0)
            {
                Write(difference >= 0 ? difference : difference + (1 << size) - 1, size);
            }

            Write(0, 1);
        }

        /// <summary>Pads to a byte boundary with ones and writes a restart marker.</summary>
        /// <param name="marker">The marker byte.</param>
        internal void Restart(byte marker)
        {
            Flush();
            _output.AddRange([Prefix, marker]);
        }

        /// <summary>Pads to a byte boundary with ones.</summary>
        internal void Flush()
        {
            if (_count > 0)
            {
                Write((1 << (ByteBits - _count)) - 1, ByteBits - _count);
            }
        }

        /// <summary>Writes bits.</summary>
        /// <param name="value">The value.</param>
        /// <param name="count">The number of low bits to write.</param>
        private void Write(int value, int count)
        {
            for (var bit = count - 1; bit >= 0; bit--)
            {
                _bits = (_bits << 1) | ((value >> bit) & 1);
                _count++;
                if (_count == ByteBits)
                {
                    EmitByte();
                }
            }
        }

        /// <summary>Writes a full byte, stuffing a zero after 0xFF.</summary>
        private void EmitByte()
        {
            _output.Add((byte)_bits);
            if (_bits == byte.MaxValue)
            {
                _output.Add(0);
            }

            _bits = 0;
            _count = 0;
        }
    }
}
