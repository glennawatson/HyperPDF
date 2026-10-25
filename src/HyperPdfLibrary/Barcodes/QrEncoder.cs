// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Barcodes;

/// <summary>Encodes text as a QR Code in a single segment of numbers, capital letters or bytes, whichever is smallest.</summary>
internal static class QrEncoder
{
    /// <summary>The mode indicator for numbers.</summary>
    private const int NumericMode = 1;

    /// <summary>The most error correction bytes a QR Code block holds, which sizes the buffer kept on the stack.</summary>
    private const int MaximumEccLength = 30;

    /// <summary>The mode indicator for capital letters, digits and a few symbols.</summary>
    private const int AlphanumericMode = 2;

    /// <summary>The mode indicator for bytes.</summary>
    private const int ByteMode = 4;

    /// <summary>The bits of a mode indicator.</summary>
    private const int ModeBits = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The first padding byte.</summary>
    private const int PadEven = 0xEC;

    /// <summary>The second padding byte.</summary>
    private const int PadOdd = 0x11;

    /// <summary>The polynomial of the field.</summary>
    private const int FieldPolynomial = 0x11D;

    /// <summary>The highest version of the first band of count field sizes.</summary>
    private const int FirstBandEnd = 9;

    /// <summary>The highest version of the second band of count field sizes.</summary>
    private const int SecondBandEnd = 26;

    /// <summary>The digits packed into one group.</summary>
    private const int DigitsPerGroup = 3;

    /// <summary>The characters packed into one pair.</summary>
    private const int Pair = 2;

    /// <summary>The bits a group of three digits takes.</summary>
    private const int BitsPerDigitGroup = 10;

    /// <summary>The bits a pair of alphanumeric characters takes.</summary>
    private const int BitsPerPair = 11;

    /// <summary>The bits a single alphanumeric character takes.</summary>
    private const int BitsPerSingle = 6;

    /// <summary>The base of an alphanumeric pair.</summary>
    private const int AlphanumericBase = 45;

    /// <summary>The characters in the alphanumeric set, whose position is their value.</summary>
    private const string AlphanumericSet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

    /// <summary>The bits of the character count for numbers, by band of versions.</summary>
    private static readonly int[] NumericCountBits = [10, 12, 14];

    /// <summary>The bits of the character count for alphanumeric text, by band of versions.</summary>
    private static readonly int[] AlphanumericCountBits = [9, 11, 13];

    /// <summary>The bits of the byte count for bytes, by band of versions.</summary>
    private static readonly int[] ByteCountBits = [8, 16, 16];

    /// <summary>Encodes text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="level">The error correction level.</param>
    /// <returns>The barcode.</returns>
    /// <exception cref="ArgumentException">The text is empty or too long.</exception>
    internal static PdfBarcode Encode(string text, PdfQrErrorCorrection level)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        var mode = GetMode(text);
        var bytes = mode == ByteMode ? Encoding.UTF8.GetBytes(text) : [];
        var count = mode == ByteMode ? bytes.Length : text.Length;
        var version = GetVersion(mode, count, level);
        var buffer = new BitBuffer();
        buffer.Append(mode, ModeBits);
        buffer.Append(count, GetCountBits(mode, version));
        AppendData(buffer, mode, text, bytes);
        var data = Pad(buffer, QrTables.GetDataBytes(version, level));
        var codewords = AddErrorCorrection(data, version, level);
        var modules = QrMatrix.Build(version, level, codewords);
        return new(PdfBarcodeSymbology.QrCode, text, QrTables.GetSize(version), modules);
    }

    /// <summary>Picks the smallest mode that holds every character.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The mode indicator.</returns>
    private static int GetMode(string text)
    {
        var mode = NumericMode;
        foreach (var character in text)
        {
            if (AlphanumericSet.IndexOf(character, StringComparison.Ordinal) < 0)
            {
                return ByteMode;
            }

            if (!char.IsAsciiDigit(character))
            {
                mode = AlphanumericMode;
            }
        }

        return mode;
    }

    /// <summary>Works out how many bits the data takes, not counting the mode indicator and count.</summary>
    /// <param name="mode">The mode indicator.</param>
    /// <param name="count">The characters, or bytes in byte mode.</param>
    /// <returns>The bit count.</returns>
    private static int GetDataBits(int mode, int count) => mode switch
    {
        NumericMode => (count / DigitsPerGroup * BitsPerDigitGroup) + GetLeftoverDigitBits(count % DigitsPerGroup),
        AlphanumericMode => (count / Pair * BitsPerPair) + (count % Pair * BitsPerSingle),
        _ => count * ByteBits,
    };

    /// <summary>Gets the bits that the one or two digits left after groups of three take.</summary>
    /// <param name="leftover">The digits left over: 0, 1 or 2.</param>
    /// <returns>The bit count.</returns>
    private static int GetLeftoverDigitBits(int leftover) => leftover == 0 ? 0 : (leftover * DigitsPerGroup) + 1;

    /// <summary>Gets which band of versions share a size of the character count field.</summary>
    /// <param name="version">The version.</param>
    /// <returns>0, 1 or 2.</returns>
    private static int GetBand(int version)
    {
        if (version <= FirstBandEnd)
        {
            return 0;
        }

        return version <= SecondBandEnd ? 1 : Pair;
    }

    /// <summary>Gets the bits of the character count field.</summary>
    /// <param name="mode">The mode indicator.</param>
    /// <param name="version">The version.</param>
    /// <returns>The bit count.</returns>
    private static int GetCountBits(int mode, int version)
    {
        var band = GetBand(version);
        return mode switch
        {
            NumericMode => NumericCountBits[band],
            AlphanumericMode => AlphanumericCountBits[band],
            _ => ByteCountBits[band],
        };
    }

    /// <summary>Finds the smallest version that holds the data.</summary>
    /// <param name="mode">The mode indicator.</param>
    /// <param name="count">The characters, or bytes in byte mode.</param>
    /// <param name="level">The error correction level.</param>
    /// <returns>The version.</returns>
    /// <exception cref="ArgumentException">No version holds the data.</exception>
    private static int GetVersion(int mode, int count, PdfQrErrorCorrection level)
    {
        var dataBits = GetDataBits(mode, count);
        for (var version = QrTables.MinVersion; version <= QrTables.MaxVersion; version++)
        {
            var countBits = GetCountBits(mode, version);
            if (count < (1 << countBits) && ModeBits + countBits + dataBits <= QrTables.GetDataBytes(version, level) * ByteBits)
            {
                return version;
            }
        }

        throw new ArgumentException("The text is too long for a QR Code at this error correction level.", nameof(count));
    }

    /// <summary>Adds the data bits of the text.</summary>
    /// <param name="buffer">Where the bits go.</param>
    /// <param name="mode">The mode indicator.</param>
    /// <param name="text">The text.</param>
    /// <param name="bytes">The text as UTF-8, for byte mode.</param>
    private static void AppendData(BitBuffer buffer, int mode, string text, byte[] bytes)
    {
        switch (mode)
        {
            case NumericMode:
            {
                AppendNumeric(buffer, text);
                break;
            }

            case AlphanumericMode:
            {
                AppendAlphanumeric(buffer, text);
                break;
            }

            default:
            {
                foreach (var value in bytes)
                {
                    buffer.Append(value, ByteBits);
                }

                break;
            }
        }
    }

    /// <summary>Adds digits, three to ten bits.</summary>
    /// <param name="buffer">Where the bits go.</param>
    /// <param name="text">The digits.</param>
    private static void AppendNumeric(BitBuffer buffer, string text)
    {
        for (var i = 0; i < text.Length; i += DigitsPerGroup)
        {
            var length = Math.Min(DigitsPerGroup, text.Length - i);
            var value = int.Parse(text.AsSpan(i, length), CultureInfo.InvariantCulture);
            buffer.Append(value, GetLeftoverDigitBits(length));
        }
    }

    /// <summary>Adds alphanumeric characters, two to eleven bits.</summary>
    /// <param name="buffer">Where the bits go.</param>
    /// <param name="text">The characters.</param>
    private static void AppendAlphanumeric(BitBuffer buffer, string text)
    {
        for (var i = 0; i < text.Length; i += Pair)
        {
            var first = AlphanumericSet.IndexOf(text[i], StringComparison.Ordinal);
            if (i + 1 < text.Length)
            {
                var second = AlphanumericSet.IndexOf(text[i + 1], StringComparison.Ordinal);
                buffer.Append((first * AlphanumericBase) + second, BitsPerPair);
            }
            else
            {
                buffer.Append(first, BitsPerSingle);
            }
        }
    }

    /// <summary>Ends the data with a terminator and fills the rest of the capacity with the alternating pad bytes.</summary>
    /// <param name="buffer">The bits so far.</param>
    /// <param name="capacity">The data bytes the symbol holds.</param>
    /// <returns>Exactly <paramref name="capacity"/> bytes.</returns>
    private static byte[] Pad(BitBuffer buffer, int capacity)
    {
        buffer.Append(0, Math.Min(ModeBits, (capacity * ByteBits) - buffer.Length));
        buffer.Append(0, (ByteBits - (buffer.Length % ByteBits)) % ByteBits);
        var bytes = new byte[capacity];
        buffer.ToBytes().CopyTo(bytes, 0);
        var even = true;
        for (var i = buffer.Length / ByteBits; i < capacity; i++)
        {
            bytes[i] = (byte)(even ? PadEven : PadOdd);
            even = !even;
        }

        return bytes;
    }

    /// <summary>Splits the data into blocks, adds error correction to each and interleaves them.</summary>
    /// <param name="data">The data bytes.</param>
    /// <param name="version">The version.</param>
    /// <param name="level">The error correction level.</param>
    /// <returns>The data and error correction bytes in placing order.</returns>
    private static byte[] AddErrorCorrection(byte[] data, int version, PdfQrErrorCorrection level)
    {
        var blocks = QrTables.GetBlockCount(version, level);
        var eccLength = QrTables.GetEccPerBlock(version, level);
        var rawBytes = QrTables.GetRawModules(version) / ByteBits;
        var shortCount = blocks - (rawBytes % blocks);
        var shortLength = (rawBytes / blocks) - eccLength;
        var field = new ReedSolomon(FieldPolynomial, 0);
        var result = new byte[data.Length + (blocks * eccLength)];

        // The blocks are slices of the data in order, so the data bytes go to their interleaved places directly, and each
        // block's error correction bytes go after all the data, one from each block in turn.
        var start = 0;
        Span<byte> ecc = stackalloc byte[MaximumEccLength];
        ecc = ecc[..eccLength];
        for (var block = 0; block < blocks; block++)
        {
            var length = shortLength + (block < shortCount ? 0 : 1);
            field.GetRemainder(data.AsSpan(start, length), ecc);
            for (var i = 0; i < length; i++)
            {
                // The one byte only the long blocks have comes after the round of bytes every block has.
                result[i < shortLength ? (i * blocks) + block : (shortLength * blocks) + block - shortCount] = data[start + i];
            }

            for (var i = 0; i < eccLength; i++)
            {
                result[data.Length + (i * blocks) + block] = ecc[i];
            }

            start += length;
        }

        return result;
    }
}
