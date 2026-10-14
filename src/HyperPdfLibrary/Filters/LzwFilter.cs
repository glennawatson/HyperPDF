// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// Decodes LZWDecode data. Each table entry is the previous entry's output plus one byte, and that output already sits
/// in the decoded data, so entries are stored as an offset and length into the output instead of as strings. The table
/// lives on the stack, so decoding allocates nothing beyond the pooled output.
/// </summary>
internal static class LzwFilter
{
    /// <summary>The widest code width.</summary>
    private const int MaxWidth = 12;

    /// <summary>The number of table entries.</summary>
    private const int TableSize = 1 << MaxWidth;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Checks cancellation after each 4096 encoded bytes.</summary>
    private const int CancellationCheckMask = 4095;

    /// <summary>Decodes LZW data.</summary>
    /// <param name="input">The encoded data.</param>
    /// <param name="earlyChange">The /EarlyChange value: 1 widens codes one entry early.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Decode(ReadOnlySpan<byte> input, int earlyChange, ref PooledBuffer output, CancellationToken cancellationToken = default) =>
        _ = TryDecode(input, earlyChange, ref output, cancellationToken);

    /// <summary>Decodes LZW data and says whether every code was valid.</summary>
    /// <param name="input">The encoded data.</param>
    /// <param name="earlyChange">The /EarlyChange value: 1 widens codes one entry early.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns><see langword="false"/> when a code was invalid, so the data is damaged; data that simply ends without an end code is fine.</returns>
    internal static bool TryDecode(ReadOnlySpan<byte> input, int earlyChange, ref PooledBuffer output, CancellationToken cancellationToken = default)
    {
        var offsets = ArrayPool<int>.Shared.Rent(TableSize);
        var lengths = ArrayPool<int>.Shared.Rent(TableSize);
        try
        {
            // Table 8 allows only 0 and 1; any other value counts as 1, like the default.
            var decoder = new Decoder(offsets, lengths, earlyChange == 0 ? 0 : 1);
            Run(input, ref decoder, ref output, cancellationToken);
            return !decoder.Damaged;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(offsets);
            ArrayPool<int>.Shared.Return(lengths);
        }
    }

    /// <summary>Reads codes and feeds them to the decoder.</summary>
    /// <param name="input">The encoded data.</param>
    /// <param name="decoder">The decoder.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void Run(ReadOnlySpan<byte> input, ref Decoder decoder, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bitBuffer = 0U;
        var bitCount = 0;
        var read = 0;
        foreach (var b in input)
        {
            read++;
            if ((read & CancellationCheckMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            bitBuffer = (bitBuffer << ByteBits) | b;
            bitCount += ByteBits;
            while (bitCount >= decoder.Width)
            {
                bitCount -= decoder.Width;
                var code = (int)((bitBuffer >> bitCount) & ((1U << decoder.Width) - 1));
                if (!decoder.Step(code, ref output))
                {
                    return;
                }
            }
        }
    }

    /// <summary>The decoder's table and state between codes.</summary>
    /// <param name="offsets">The entries' output offsets.</param>
    /// <param name="lengths">The entries' lengths.</param>
    /// <param name="earlyChange">The /EarlyChange value.</param>
    private struct Decoder(int[] offsets, int[] lengths, int earlyChange)
    {
        /// <summary>The clear-table code.</summary>
        private const int ClearCode = 256;

        /// <summary>The end-of-data code.</summary>
        private const int EndCode = 257;

        /// <summary>The first code added to the table.</summary>
        private const int FirstCode = 258;

        /// <summary>The narrowest code width.</summary>
        private const int MinWidth = 9;

        /// <summary>The entries' output offsets.</summary>
        private readonly int[] _offsets = offsets;

        /// <summary>The entries' lengths.</summary>
        private readonly int[] _lengths = lengths;

        /// <summary>The next free table entry.</summary>
        private int _next = FirstCode;

        /// <summary>The previous code, or -1 after a clear.</summary>
        private int _previous = -1;

        /// <summary>The output offset of the previous code's string.</summary>
        private int _previousStart;

        /// <summary>The length of the previous code's string.</summary>
        private int _previousLength;

        /// <summary>Gets the code width.</summary>
        public int Width { get; private set; } = MinWidth;

        /// <summary>Gets a value indicating whether decoding stopped at a code that is not in the table.</summary>
        public bool Damaged { get; private set; }

        /// <summary>Handles one code.</summary>
        /// <param name="code">The code.</param>
        /// <param name="output">The decoded data.</param>
        /// <returns><see langword="false"/> at the end of the data or on damage.</returns>
        public bool Step(int code, scoped ref PooledBuffer output)
        {
            if (code == ClearCode)
            {
                Width = MinWidth;
                _next = FirstCode;
                _previous = -1;
                return true;
            }

            if (code == EndCode)
            {
                return false;
            }

            if (code > _next || (code == _next && _previous < 0))
            {
                Damaged = true;
                return false;
            }

            var start = output.Length;
            var length = code < ClearCode ? WriteLiteral(code, ref output) : WriteEntry(code, ref output);
            AddEntry();
            _previous = code;
            _previousStart = start;
            _previousLength = length;
            if (_next + earlyChange >= (1 << Width) && Width < MaxWidth)
            {
                Width++;
            }

            return true;
        }

        /// <summary>Writes a single-byte code.</summary>
        /// <param name="code">The code.</param>
        /// <param name="output">The decoded data.</param>
        /// <returns>The length written.</returns>
        private static int WriteLiteral(int code, scoped ref PooledBuffer output)
        {
            output.WriteByte((byte)code);
            return 1;
        }

        /// <summary>Copies a table entry's string from earlier output; the code just past the table repeats the previous string plus its first byte.</summary>
        /// <param name="code">The code.</param>
        /// <param name="output">The decoded data.</param>
        /// <returns>The length written.</returns>
        private readonly int WriteEntry(int code, scoped ref PooledBuffer output)
        {
            var known = code < _next;
            var source = known ? _offsets[code] : _previousStart;
            var length = known ? _lengths[code] : _previousLength + 1;
            var span = output.GetSpan(length);
            var written = output.Array!;
            written.AsSpan(source, known ? length : length - 1).CopyTo(span);
            if (!known)
            {
                span[length - 1] = written[source];
            }

            output.Advance(length);
            return length;
        }

        /// <summary>Adds the entry formed by the previous string and the first byte of the current one.</summary>
        private void AddEntry()
        {
            if (_previous < 0 || _next >= TableSize)
            {
                return;
            }

            _offsets[_next] = _previousStart;
            _lengths[_next] = _previousLength + 1;
            _next++;
        }
    }
}
