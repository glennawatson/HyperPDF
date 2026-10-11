// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// Decodes CCITT Group 3 (one- and two-dimensional) and Group 4 fax data into packed 1-bit rows, most significant bit
/// first. Damaged data never throws: rows that cannot be decoded stay white, and decoding resynchronises on the next
/// end-of-line code when the data has them.
/// </summary>
internal static class CcittFaxDecoder
{
    /// <summary>The extra entries in a line buffer beyond one per column.</summary>
    private const int LineSlack = 4;

    /// <summary>The widest line decoded; wider /Columns values are clamped.</summary>
    private const int MaxColumns = 1 << 20;

    /// <summary>The shift that turns a bit count into a byte count.</summary>
    private const int ByteShift = 3;

    /// <summary>The mask of the bit index within a byte.</summary>
    private const int BitIndexMask = 7;

    /// <summary>A byte with every bit set.</summary>
    private const byte FullByte = 0xFF;

    /// <summary>The bits read into the peek window.</summary>
    private const int WindowBits = 32;

    /// <summary>Decodes fax data into packed 1-bit rows. Rows that are not decoded are left white.</summary>
    /// <param name="data">The encoded data.</param>
    /// <param name="parameters">The filter parameters.</param>
    /// <param name="width">The image width in pixels; each output row holds <c>(width + 7) / 8</c> bytes.</param>
    /// <param name="height">The rows to produce; a positive /Rows smaller than this limits the rows decoded.</param>
    /// <param name="output">The buffer receiving the rows; rows that do not fit are skipped.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Decode(ReadOnlySpan<byte> data, in CcittParameters parameters, int width, int height, Span<byte> output) =>
        _ = DecodeRows(data, parameters, width, height, output, new());

    /// <summary>Decodes fax rows with an explicitly owned scratch cache.</summary>
    /// <param name="data">The encoded data.</param>
    /// <param name="parameters">The filter parameters.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The rows to produce.</param>
    /// <param name="output">Receives packed rows.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    internal static void Decode(ReadOnlySpan<byte> data, in CcittParameters parameters, int width, int height, Span<byte> output, ScratchPools scratchPools) =>
        _ = DecodeRows(data, parameters, width, height, output, scratchPools);

    /// <summary>Decodes JBIG2 MMR data: Group 4 coding with no options, black as 1 bits. Rows that are not decoded are left white.</summary>
    /// <param name="data">The encoded data.</param>
    /// <param name="width">The image width in pixels; each output row holds <c>(width + 7) / 8</c> bytes.</param>
    /// <param name="height">The rows to decode.</param>
    /// <param name="output">The buffer receiving the rows.</param>
    /// <returns>The bits read from <paramref name="data"/>: where the last decoded row ended.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long DecodeMmr(ReadOnlySpan<byte> data, int width, int height, Span<byte> output) =>
        DecodeRows(data, new(-1, width, height, true, false, false, false), width, height, output, new());

    /// <summary>Decodes MMR rows with an explicitly owned scratch cache.</summary>
    /// <param name="data">The encoded data.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The rows to decode.</param>
    /// <param name="output">Receives packed rows.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The bits read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long DecodeMmr(ReadOnlySpan<byte> data, int width, int height, Span<byte> output, ScratchPools scratchPools) =>
        DecodeRows(data, new(-1, width, height, true, false, false, false), width, height, output, scratchPools);

    /// <summary>Decodes fax data into packed 1-bit rows and reports where decoding stopped.</summary>
    /// <param name="data">The encoded data.</param>
    /// <param name="parameters">The filter parameters.</param>
    /// <param name="width">The image width in pixels; each output row holds <c>(width + 7) / 8</c> bytes.</param>
    /// <param name="height">The rows to produce; a positive /Rows smaller than this limits the rows decoded.</param>
    /// <param name="output">The buffer receiving the rows; rows that do not fit are skipped.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The bits read from <paramref name="data"/>.</returns>
    private static long DecodeRows(ReadOnlySpan<byte> data, in CcittParameters parameters, int width, int height, Span<byte> output, ScratchPools scratchPools)
    {
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        var stride = (width + BitIndexMask) >> ByteShift;
        var rows = Math.Min(height, output.Length / stride);
        output[..(rows * stride)].Fill(parameters.BlackIs1 ? (byte)0 : FullByte);
        if (parameters.Rows > 0)
        {
            rows = Math.Min(rows, parameters.Rows);
        }

        var columns = Math.Clamp(parameters.Columns > 0 ? parameters.Columns : width, 1, MaxColumns);
        var lineLength = columns + LineSlack;
        var pool = scratchPools.Get<int>();
        var rented = pool.Rent(lineLength + lineLength);
        try
        {
            var decoder = new LineDecoder(data, parameters, columns, rented.AsSpan(0, lineLength), rented.AsSpan(lineLength, lineLength));
            for (var row = 0; row < rows && decoder.DecodeNextLine(); row++)
            {
                RenderRow(decoder.CurrentChanges, output.Slice(row * stride, stride), width, !parameters.BlackIs1);
                decoder.FinishLine();
            }

            return decoder.Position;
        }
        finally
        {
            pool.Return(rented);
        }
    }

    /// <summary>Paints the black runs of one decoded line into an output row prefilled white.</summary>
    /// <param name="changes">The changing elements: positions where the colour flips, starting white.</param>
    /// <param name="row">The output row.</param>
    /// <param name="width">The pixels in the row.</param>
    /// <param name="blackIsZero">Whether black pixels are 0 bits.</param>
    private static void RenderRow(ReadOnlySpan<int> changes, Span<byte> row, int width, bool blackIsZero)
    {
        for (var i = 0; i < changes.Length; i += LineDecoder.ChangesPerRun)
        {
            var start = Math.Min(changes[i], width);
            var end = i + 1 < changes.Length ? Math.Min(changes[i + 1], width) : width;
            if (start < end)
            {
                PaintBlack(row, start, end, blackIsZero);
            }
        }
    }

    /// <summary>Sets the bits of a run of black pixels.</summary>
    /// <param name="row">The output row.</param>
    /// <param name="start">The first pixel.</param>
    /// <param name="end">The pixel after the last.</param>
    /// <param name="blackIsZero">Whether black pixels are 0 bits.</param>
    private static void PaintBlack(Span<byte> row, int start, int end, bool blackIsZero)
    {
        var first = start >> ByteShift;
        var last = (end - 1) >> ByteShift;
        var startMask = FullByte >> (start & BitIndexMask);
        var endMask = (FullByte << (BitIndexMask - ((end - 1) & BitIndexMask))) & FullByte;
        if (first == last)
        {
            ApplyMask(ref row[first], startMask & endMask, blackIsZero);
            return;
        }

        ApplyMask(ref row[first], startMask, blackIsZero);
        row[(first + 1)..last].Fill(blackIsZero ? (byte)0 : FullByte);
        ApplyMask(ref row[last], endMask, blackIsZero);
    }

    /// <summary>Paints the masked bits of one byte black.</summary>
    /// <param name="target">The byte.</param>
    /// <param name="mask">The bits to paint.</param>
    /// <param name="blackIsZero">Whether black pixels are 0 bits.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyMask(ref byte target, int mask, bool blackIsZero) =>
        target = blackIsZero ? (byte)(target & ~mask) : (byte)(target | mask);

    /// <summary>Reads bits, most significant first, reading zeros past the end of the data.</summary>
    private ref struct BitReader
    {
        /// <summary>The bits in a byte.</summary>
        private const int BitsPerByte = 8;

        /// <summary>The bytes read into the peek window.</summary>
        private const int WindowBytes = 4;

        /// <summary>The encoded data.</summary>
        private readonly ReadOnlySpan<byte> _data;

        /// <summary>The number of bits in the data.</summary>
        private readonly long _length;

        /// <summary>Initializes a new instance of the <see cref="BitReader"/> struct.</summary>
        /// <param name="data">The encoded data.</param>
        internal BitReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _length = (long)data.Length * BitsPerByte;
        }

        /// <summary>Gets or sets the position in bits.</summary>
        internal long Position { readonly get; set; }

        /// <summary>Gets a value indicating whether every bit has been read.</summary>
        internal readonly bool IsAtEnd => Position >= _length;

        /// <summary>Gets the next bits without consuming them.</summary>
        /// <param name="count">The bits wanted, from 1 to 24.</param>
        /// <returns>The bits, right-aligned.</returns>
        internal readonly int Peek(int count)
        {
            var index = Position >> ByteShift;
            var window = index + WindowBytes <= _data.Length
                ? BinaryPrimitives.ReadUInt32BigEndian(_data[(int)index..])
                : ReadTail(index);
            window <<= (int)(Position & BitIndexMask);
            return (int)(window >> (WindowBits - count));
        }

        /// <summary>Consumes bits.</summary>
        /// <param name="count">The bits.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Skip(int count) => Position += count;

        /// <summary>Moves to the next byte boundary when the bits before it are all zero padding.</summary>
        internal void AlignOverPadding()
        {
            var remaining = (int)(-Position & BitIndexMask);
            if (remaining != 0 && Peek(remaining) == 0)
            {
                Position += remaining;
            }
        }

        /// <summary>Reads the peek window near the end of the data, padding with zeros.</summary>
        /// <param name="index">The first byte.</param>
        /// <returns>The window.</returns>
        private readonly uint ReadTail(long index)
        {
            var window = 0U;
            for (var i = 0; i < WindowBytes; i++)
            {
                var at = index + i;
                window = (window << BitsPerByte) | (at < _data.Length ? _data[(int)at] : 0U);
            }

            return window;
        }
    }

    /// <summary>Decodes lines one at a time, keeping the changing elements of the current and reference lines.</summary>
    private ref struct LineDecoder
    {
        /// <summary>The changing elements that bound one black run: where it starts and where it ends.</summary>
        internal const int ChangesPerRun = 2;

        /// <summary>The bits peeked to decode a white run code; the longest white code has 12 bits.</summary>
        private const int WhiteBits = 12;

        /// <summary>The bits peeked to decode a black run code; the longest black code has 13 bits.</summary>
        private const int BlackBits = 13;

        /// <summary>The bits peeked to decode a two-dimensional mode code; the longest has 7 bits.</summary>
        private const int ModeBits = 7;

        /// <summary>The length of an end-of-line code (eleven zeros and a one).</summary>
        private const int EolBits = 12;

        /// <summary>The value of the 12 bits of an end-of-line code.</summary>
        private const int EolCode = 1;

        /// <summary>The fewest zeros before the one that ends an end-of-line code.</summary>
        private const int MinEolZeros = 11;

        /// <summary>The shift of the code length within a lookup entry.</summary>
        private const int LengthShift = 12;

        /// <summary>The mask of the run length or mode within a lookup entry.</summary>
        private const int ValueMask = 0xFFF;

        /// <summary>The step between make-up run lengths.</summary>
        private const int MakeupStep = 64;

        /// <summary>The first extended make-up run length, shared by both colours.</summary>
        private const int ExtendedMakeupStart = 1792;

        /// <summary>Runs shorter than this are terminating codes; longer ones are make-up codes.</summary>
        private const int TerminatingLimit = 64;

        /// <summary>The value of the pass mode.</summary>
        private const int ModePass = 1;

        /// <summary>The value of the horizontal mode.</summary>
        private const int ModeHorizontal = 2;

        /// <summary>The value of the vertical mode with no offset; the other vertical modes are offsets from it.</summary>
        private const int ModeVertical0 = 6;

        /// <summary>The sentinel entries kept after the changing elements of a line.</summary>
        private const int SentinelCount = 3;

        /// <summary>The bits scanned at a time when skipping fill zeros.</summary>
        private const int ZeroScanBits = 24;

        /// <summary>Maps 12 peeked bits to a white run: the code length above <see cref="LengthShift"/>, the run below.</summary>
        private static readonly ushort[] WhiteLookup = BuildRunLookup(WhiteBits, WhiteTerminatingCodes, WhiteMakeupCodes);

        /// <summary>Maps 13 peeked bits to a black run: the code length above <see cref="LengthShift"/>, the run below.</summary>
        private static readonly ushort[] BlackLookup = BuildRunLookup(BlackBits, BlackTerminatingCodes, BlackMakeupCodes);

        /// <summary>Maps 7 peeked bits to a mode: the code length above <see cref="LengthShift"/>, the mode below.</summary>
        private static readonly ushort[] ModeLookup = BuildModeLookup();

        /// <summary>The width of a line in pixels.</summary>
        private readonly int _columns;

        /// <summary>The coding scheme: negative for Group 4, zero for one-dimensional, positive for mixed.</summary>
        private readonly int _k;

        /// <summary>Whether lines start on byte boundaries.</summary>
        private readonly bool _byteAlign;

        /// <summary>Whether the data declares end-of-line codes.</summary>
        private readonly bool _expectEol;

        /// <summary>The bit reader.</summary>
        private BitReader _reader;

        /// <summary>The changing elements of the reference (previous) line, followed by sentinels.</summary>
        private Span<int> _reference;

        /// <summary>The changing elements of the line being decoded.</summary>
        private Span<int> _current;

        /// <summary>The changing elements in <see cref="_reference"/>.</summary>
        private int _referenceCount;

        /// <summary>The changing elements in <see cref="_current"/>.</summary>
        private int _currentCount;

        /// <summary>Whether an end-of-line code has been seen.</summary>
        private bool _sawEol;

        /// <summary>Whether decoding has stopped.</summary>
        private bool _stopped;

        /// <summary>Initializes a new instance of the <see cref="LineDecoder"/> struct.</summary>
        /// <param name="data">The encoded data.</param>
        /// <param name="parameters">The filter parameters.</param>
        /// <param name="columns">The width of a line in pixels.</param>
        /// <param name="reference">The buffer for the reference line.</param>
        /// <param name="current">The buffer for the current line.</param>
        internal LineDecoder(ReadOnlySpan<byte> data, in CcittParameters parameters, int columns, Span<int> reference, Span<int> current)
        {
            _reader = new(data);
            _columns = columns;
            _k = parameters.K;
            _byteAlign = parameters.EncodedByteAlign;
            _expectEol = parameters.EndOfLine;
            _reference = reference;
            _current = current;
            SetSentinels();
        }

        /// <summary>Gets the changing elements of the line just decoded.</summary>
        internal readonly ReadOnlySpan<int> CurrentChanges => _current[.._currentCount];

        /// <summary>Gets the position in bits of the next unread bit.</summary>
        internal readonly long Position => _reader.Position;

        /// <summary>Gets the white terminating codes (T.4 table 2) for runs 0 to 63, as bit strings.</summary>
        private static ReadOnlySpan<byte> WhiteTerminatingCodes =>
            "00110101 000111 0111 1000 1011 1100 1110 1111 10011 10100 00111 01000 001000 000011 110100 110101 "u8
            + "101010 101011 0100111 0001100 0001000 0010111 0000011 0000100 0101000 0101011 0010011 0100100 "u8
            + "0011000 00000010 00000011 00011010 00011011 00010010 00010011 00010100 00010101 00010110 00010111 "u8
            + "00101000 00101001 00101010 00101011 00101100 00101101 00000100 00000101 00001010 00001011 01010010 "u8
            + "01010011 01010100 01010101 00100100 00100101 01011000 01011001 01011010 01011011 01001010 01001011 "u8
            + "00110010 00110011 00110100"u8;

        /// <summary>Gets the white make-up codes (T.4 table 3) for runs 64 to 1728, as bit strings.</summary>
        private static ReadOnlySpan<byte> WhiteMakeupCodes =>
            "11011 10010 010111 0110111 00110110 00110111 01100100 01100101 01101000 01100111 011001100 011001101 "u8
            + "011010010 011010011 011010100 011010101 011010110 011010111 011011000 011011001 011011010 011011011 "u8
            + "010011000 010011001 010011010 011000 010011011"u8;

        /// <summary>Gets the black terminating codes (T.4 table 2) for runs 0 to 63, as bit strings.</summary>
        private static ReadOnlySpan<byte> BlackTerminatingCodes =>
            "0000110111 010 11 10 011 0011 0010 00011 000101 000100 0000100 0000101 0000111 00000100 00000111 "u8
            + "000011000 0000010111 0000011000 0000001000 00001100111 00001101000 00001101100 00000110111 "u8
            + "00000101000 00000010111 00000011000 000011001010 000011001011 000011001100 000011001101 "u8
            + "000001101000 000001101001 000001101010 000001101011 000011010010 000011010011 000011010100 "u8
            + "000011010101 000011010110 000011010111 000001101100 000001101101 000011011010 000011011011 "u8
            + "000001010100 000001010101 000001010110 000001010111 000001100100 000001100101 000001010010 "u8
            + "000001010011 000000100100 000000110111 000000111000 000000100111 000000101000 000001011000 "u8
            + "000001011001 000000101011 000000101100 000001011010 000001100110 000001100111"u8;

        /// <summary>Gets the black make-up codes (T.4 table 3) for runs 64 to 1728, as bit strings.</summary>
        private static ReadOnlySpan<byte> BlackMakeupCodes =>
            "0000001111 000011001000 000011001001 000001011011 000000110011 000000110100 000000110101 "u8
            + "0000001101100 0000001101101 0000001001010 0000001001011 0000001001100 0000001001101 "u8
            + "0000001110010 0000001110011 0000001110100 0000001110101 0000001110110 0000001110111 "u8
            + "0000001010010 0000001010011 0000001010100 0000001010101 0000001011010 0000001011011 "u8
            + "0000001100100 0000001100101"u8;

        /// <summary>Gets the extended make-up codes shared by both colours (T.4 table 3) for runs 1792 to 2560, as bit strings.</summary>
        private static ReadOnlySpan<byte> ExtendedMakeupCodes =>
            "00000001000 00000001100 00000001101 000000010010 000000010011 000000010100 000000010101 000000010110 000000010111 000000011100 000000011101 000000011110 000000011111"u8;

        /// <summary>
        /// Gets the two-dimensional mode codes (T.4 table 4) as bit strings, in mode order: pass, horizontal, vertical
        /// left 3, 2 and 1, vertical 0, vertical right 1, 2 and 3.
        /// </summary>
        private static ReadOnlySpan<byte> ModeCodes => "0001 001 0000010 000010 010 1 011 000011 0000011"u8;

        /// <summary>Decodes the next line. A damaged line keeps what decoded before the damage.</summary>
        /// <returns><see langword="true"/> when a line was decoded, even partly.</returns>
        internal bool DecodeNextLine()
        {
            _currentCount = 0;
            if (_stopped || !StartLine(out var twoDimensional))
            {
                _stopped = true;
                return false;
            }

            var decoded = twoDimensional ? Decode2D() : Decode1D();
            if (!decoded)
            {
                _stopped = !Resync();
            }

            return true;
        }

        /// <summary>Makes the line just decoded the reference line.</summary>
        internal void FinishLine()
        {
            var previous = _reference;
            _reference = _current;
            _current = previous;
            _referenceCount = _currentCount;
            _currentCount = 0;
            SetSentinels();
        }

        /// <summary>Builds a run-length lookup table indexed by peeked bits.</summary>
        /// <param name="bits">The bits peeked.</param>
        /// <param name="terminating">The terminating codes.</param>
        /// <param name="makeup">The colour's make-up codes.</param>
        /// <returns>The table.</returns>
        private static ushort[] BuildRunLookup(int bits, ReadOnlySpan<byte> terminating, ReadOnlySpan<byte> makeup)
        {
            var table = new ushort[1 << bits];
            Fill(table, bits, terminating, 0, 1);
            Fill(table, bits, makeup, MakeupStep, MakeupStep);
            Fill(table, bits, ExtendedMakeupCodes, ExtendedMakeupStart, MakeupStep);
            return table;
        }

        /// <summary>Builds the mode lookup table indexed by peeked bits.</summary>
        /// <returns>The table.</returns>
        private static ushort[] BuildModeLookup()
        {
            var table = new ushort[1 << ModeBits];
            Fill(table, ModeBits, ModeCodes, ModePass, 1);
            return table;
        }

        /// <summary>Fills every table entry whose leading bits match each code.</summary>
        /// <param name="table">The table.</param>
        /// <param name="bits">The bits the table is indexed by.</param>
        /// <param name="codes">The codes as bit strings separated by spaces.</param>
        /// <param name="firstValue">The value of the first code.</param>
        /// <param name="step">The step between the values of consecutive codes.</param>
        private static void Fill(ushort[] table, int bits, ReadOnlySpan<byte> codes, int firstValue, int step)
        {
            var value = firstValue;
            foreach (var range in codes.Split((byte)' '))
            {
                var code = codes[range];
                var bitCode = 0;
                foreach (var digit in code)
                {
                    bitCode = (bitCode << 1) | (digit - (byte)'0');
                }

                var shift = bits - code.Length;
                table.AsSpan(bitCode << shift, 1 << shift).Fill((ushort)((code.Length << LengthShift) | value));
                value += step;
            }
        }

        /// <summary>Reads one run: any make-up codes followed by a terminating code.</summary>
        /// <param name="reader">The bit reader.</param>
        /// <param name="white">Whether the run is white.</param>
        /// <param name="limit">The longest run kept; longer runs are clamped.</param>
        /// <returns>The run length, or -1 when the code is invalid.</returns>
        private static int ReadRun(ref BitReader reader, bool white, int limit)
        {
            var table = white ? WhiteLookup : BlackLookup;
            var bits = white ? WhiteBits : BlackBits;
            var run = 0;
            while (true)
            {
                var entry = table[reader.Peek(bits)];
                var length = entry >> LengthShift;
                if (length == 0)
                {
                    return -1;
                }

                reader.Skip(length);
                var part = entry & ValueMask;
                run = Math.Min(run + part, limit);
                if (part < TerminatingLimit)
                {
                    return run;
                }
            }
        }

        /// <summary>Skips fill zeros and an end-of-line code, if one comes next.</summary>
        /// <param name="reader">The bit reader.</param>
        /// <returns><see langword="true"/> when an end-of-line code was skipped; otherwise the position is unchanged.</returns>
        private static bool TrySkipEol(ref BitReader reader)
        {
            var start = reader.Position;
            while (!reader.IsAtEnd)
            {
                var window = reader.Peek(ZeroScanBits);
                if (window != 0)
                {
                    reader.Skip(BitOperations.LeadingZeroCount((uint)window) - (WindowBits - ZeroScanBits));
                    break;
                }

                reader.Skip(ZeroScanBits);
            }

            if (!reader.IsAtEnd && reader.Position - start >= MinEolZeros)
            {
                reader.Skip(1);
                return true;
            }

            reader.Position = start;
            return false;
        }

        /// <summary>Determines whether an end-of-line code, after any fill zeros, comes next.</summary>
        /// <param name="reader">The bit reader, left unchanged.</param>
        /// <returns><see langword="true"/> when an end-of-line code comes next.</returns>
        private static bool PeekEol(ref BitReader reader)
        {
            var start = reader.Position;
            var found = TrySkipEol(ref reader);
            reader.Position = start;
            return found;
        }

        /// <summary>Writes the sentinels after the reference line's changing elements.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly void SetSentinels() => _reference.Slice(_referenceCount, SentinelCount).Fill(_columns);

        /// <summary>Reads what comes before a line: alignment, an end-of-line code and the mixed-mode tag bit.</summary>
        /// <param name="twoDimensional">Whether the line is two-dimensionally coded.</param>
        /// <returns><see langword="false"/> at the end of the data or an end-of-block code.</returns>
        private bool StartLine(out bool twoDimensional)
        {
            twoDimensional = _k < 0;
            if (_byteAlign && _k < 0)
            {
                _reader.AlignOverPadding();
            }

            // Fill zeros before an end-of-line code are skipped with it. Fill after the code, or without one, is
            // skipped by aligning; alignment is skipped when the bits up to the boundary are not all zero padding.
            var eol = TrySkipEol(ref _reader);
            if (_byteAlign && _k >= 0)
            {
                _reader.AlignOverPadding();
            }

            _sawEol |= eol;
            if (_k > 0)
            {
                twoDimensional = _reader.Peek(1) == 0;
                _reader.Skip(1);
            }

            // A second end-of-line code in a row is the end-of-block (Group 4) or return-to-control (Group 3) sequence.
            return !(eol && PeekEol(ref _reader)) && !_reader.IsAtEnd;
        }

        /// <summary>Decodes a one-dimensional (modified Huffman) line.</summary>
        /// <returns><see langword="false"/> when the data is damaged.</returns>
        private bool Decode1D()
        {
            var a0 = 0;
            while (a0 < _columns)
            {
                var run = ReadRun(ref _reader, (_currentCount & 1) == 0, _columns);
                if (run < 0)
                {
                    return false;
                }

                a0 = Math.Min(a0 + run, _columns);
                if (!AddChange(a0))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Decodes a two-dimensional (modified READ) line against the reference line.</summary>
        /// <returns><see langword="false"/> when the data is damaged.</returns>
        private bool Decode2D()
        {
            // a0 starts on an imaginary white pixel before the line.
            var a0 = -1;
            var referenceIndex = 0;
            while (a0 < _columns)
            {
                var entry = ModeLookup[_reader.Peek(ModeBits)];
                var length = entry >> LengthShift;
                if (length == 0)
                {
                    return false;
                }

                _reader.Skip(length);
                if (!ApplyMode(entry & ValueMask, ref a0, ref referenceIndex))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Applies one two-dimensional mode.</summary>
        /// <param name="mode">The mode.</param>
        /// <param name="a0">The current position.</param>
        /// <param name="referenceIndex">The search position on the reference line.</param>
        /// <returns><see langword="false"/> when the data is damaged.</returns>
        private bool ApplyMode(int mode, ref int a0, ref int referenceIndex)
        {
            var colour = _currentCount & 1;
            var b1 = FindB1(a0, colour, ref referenceIndex);
            if (mode == ModePass)
            {
                a0 = _reference[b1 + 1];
                return true;
            }

            if (mode == ModeHorizontal)
            {
                return Horizontal(ref a0, colour);
            }

            var a1 = _reference[b1] + (mode - ModeVertical0);
            if (a1 < Math.Max(a0, 0))
            {
                return false;
            }

            a0 = Math.Min(a1, _columns);
            return AddChange(a0);
        }

        /// <summary>Decodes horizontal mode: one run of the current colour and one of the other.</summary>
        /// <param name="a0">The current position.</param>
        /// <param name="colour">The current colour: 0 for white, 1 for black.</param>
        /// <returns><see langword="false"/> when the data is damaged.</returns>
        private bool Horizontal(ref int a0, int colour)
        {
            var first = ReadRun(ref _reader, colour == 0, _columns);
            if (first < 0)
            {
                return false;
            }

            var second = ReadRun(ref _reader, colour != 0, _columns);
            if (second < 0)
            {
                return false;
            }

            var a1 = Math.Min(Math.Max(a0, 0) + first, _columns);
            a0 = Math.Min(a1 + second, _columns);
            return AddChange(a1) && AddChange(a0);
        }

        /// <summary>Finds b1: the first changing element on the reference line right of a0 that flips to the other colour.</summary>
        /// <param name="a0">The current position.</param>
        /// <param name="colour">The current colour: 0 for white, 1 for black.</param>
        /// <param name="index">The search position, moved to b1.</param>
        /// <returns>The index of b1; b2 follows it.</returns>
        private readonly int FindB1(int a0, int colour, ref int index)
        {
            while (index > 0 && _reference[index - 1] > a0)
            {
                index--;
            }

            // Even elements flip white to black, so they are b1 candidates while the current colour is white.
            while (index < _referenceCount && (_reference[index] <= a0 || (index & 1) != colour))
            {
                index++;
            }

            return index;
        }

        /// <summary>Records a changing element on the current line.</summary>
        /// <param name="position">The position.</param>
        /// <returns><see langword="false"/> when the line has more changes than it can hold.</returns>
        private bool AddChange(int position)
        {
            if (_currentCount >= _current.Length - SentinelCount)
            {
                return false;
            }

            _current[_currentCount] = position;
            _currentCount++;
            return true;
        }

        /// <summary>Moves to the next end-of-line code after damage, when the data has them.</summary>
        /// <returns><see langword="true"/> when an end-of-line code was found.</returns>
        private bool Resync()
        {
            if (!_expectEol && !_sawEol)
            {
                return false;
            }

            while (!_reader.IsAtEnd)
            {
                if (_reader.Peek(EolBits) == EolCode)
                {
                    return true;
                }

                _reader.Skip(1);
            }

            return false;
        }
    }
}
