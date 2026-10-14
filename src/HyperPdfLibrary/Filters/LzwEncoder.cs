// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// Encodes LZWDecode data (ISO 32000-2 7.4.4.2): codes of 9 to 12 bits packed high bit first, starting with a clear-table
/// code and ending with the end-of-data code. Strings are looked up in an open-addressing hash table of (prefix code, next
/// byte) pairs. Each table reset bumps an epoch stored in the keys instead of clearing the table, so a reset costs nothing.
/// </summary>
internal static class LzwEncoder
{
    /// <summary>The clear-table code.</summary>
    private const int ClearCode = 256;

    /// <summary>The end-of-data code.</summary>
    private const int EndCode = 257;

    /// <summary>The narrowest code width.</summary>
    private const int MinWidth = 9;

    /// <summary>The next free code at which the table is cleared, leaving the last codes unused so no code needs 13 bits.</summary>
    private const int ClearAt = 4094;

    /// <summary>The most bits of the hash table's index; 8192 slots keep the table under half full.</summary>
    private const int MaxHashBits = 13;

    /// <summary>The fewest bits of the hash table's index, so short inputs clear and probe a small table.</summary>
    private const int MinHashBits = 8;

    /// <summary>The bits of headroom a table gets over the input length, which keeps it under half full.</summary>
    private const int HashHeadroomBits = 2;

    /// <summary>The ints in a slot: a key and a code.</summary>
    private const int IntsPerSlot = 2;

    /// <summary>The ints rented for the largest hash table.</summary>
    private const int MaxHashInts = (1 << MaxHashBits) * IntsPerSlot;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The most bytes reserved up front for the output; it grows past this when needed.</summary>
    private const int MaxOutputHint = 1 << 20;

    /// <summary>Checks cancellation after each 4096 input bytes.</summary>
    private const int CancellationCheckMask = 4095;

    /// <summary>Encodes data as LZW.</summary>
    /// <param name="input">The bytes.</param>
    /// <param name="earlyChange">The /EarlyChange value: 1 widens codes one entry early (the default), 0 as late as possible.</param>
    /// <param name="output">The buffer receiving the encoded data.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    internal static void Encode(ReadOnlySpan<byte> input, int earlyChange, ref PooledBuffer output, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var slots = ArrayPool<int>.Shared.Rent(MaxHashInts);

        // An input of n bytes adds at most n entries, so a table of about 2n slots stays under half full.
        var hashBits = Math.Clamp(BitOperations.Log2((uint)Math.Min(input.Length, MaxHashInts)) + HashHeadroomBits, MinHashBits, MaxHashBits);
        slots.AsSpan(0, IntsPerSlot << hashBits).Clear();
        var codes = default(CodeWriter);
        try
        {
            codes.Reserve(Math.Min(input.Length, MaxOutputHint));
            var state = new State(slots, hashBits, earlyChange != 0 ? 1 : 0);
            codes.Write(ClearCode, MinWidth);
            Run(input, ref state, ref codes, cancellationToken);
            codes.Write(EndCode, state.Width);
            codes.Flush();
            output.Write(codes.WrittenSpan);
        }
        finally
        {
            codes.Dispose();
            ArrayPool<int>.Shared.Return(slots);
        }
    }

    /// <summary>Emits the codes for all of the input.</summary>
    /// <param name="input">The bytes.</param>
    /// <param name="state">The table and code width.</param>
    /// <param name="codes">The code writer.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void Run(ReadOnlySpan<byte> input, ref State state, ref CodeWriter codes, CancellationToken cancellationToken)
    {
        if (input.IsEmpty)
        {
            return;
        }

        var current = (int)input[0];
        for (var i = 1; i < input.Length; i++)
        {
            if ((i & CancellationCheckMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var next = input[i];
            var found = state.Find(current, next);
            if (found >= 0)
            {
                current = found;
                continue;
            }

            codes.Write(current, state.Width);
            state.Add(current, next);
            if (state.Next >= ClearAt)
            {
                codes.Write(ClearCode, state.Width);
                state.Reset();
            }

            current = next;
        }

        // The decoder adds an entry when it reads the last code, which can widen the code that follows.
        codes.Write(current, state.Width);
        state.AdvanceWithoutEntry();
    }

    /// <summary>The string table and the current code width.</summary>
    /// <param name="slots">The hash table: a key and a code for each slot.</param>
    /// <param name="hashBits">The bits of a slot index.</param>
    /// <param name="earlyChange">The early-change value, 0 or 1.</param>
    private struct State(int[] slots, int hashBits, int earlyChange)
    {
        /// <summary>The first code added to the table.</summary>
        private const int FirstCode = 258;

        /// <summary>The widest code width.</summary>
        private const int MaxWidth = 12;

        /// <summary>The multiplier that scatters keys across the table (Knuth's golden-ratio constant).</summary>
        private const uint HashMultiplier = 2_654_435_761;

        /// <summary>The bits of a key that hold the prefix code and the next byte; the epoch fills the rest.</summary>
        private const int KeyBits = 20;

        /// <summary>The bits in the hash multiplication.</summary>
        private const int HashWordBits = 32;

        /// <summary>The largest epoch, which fills the key bits above the prefix code and byte.</summary>
        private const int MaxEpoch = (1 << (HashWordBits - KeyBits)) - 1;

        /// <summary>The hash table: a key and a code for each slot.</summary>
        private readonly int[] _slots = slots;

        /// <summary>The mask of a slot index.</summary>
        private readonly int _mask = (1 << hashBits) - 1;

        /// <summary>The epoch that marks the keys of the current table.</summary>
        private int _epoch = 1;

        /// <summary>Gets the next code to add to the table.</summary>
        internal int Next { get; private set; } = FirstCode;

        /// <summary>Gets the width in bits of the next code.</summary>
        internal int Width { get; private set; } = MinWidth;

        /// <summary>Finds the code of a string made of a known string and one more byte.</summary>
        /// <param name="prefix">The code of the known string.</param>
        /// <param name="value">The byte.</param>
        /// <returns>The code, or -1 when the string is not in the table.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly int Find(int prefix, byte value)
        {
            var key = KeyOf(prefix, value);
            var slot = SlotOf(key);
            while (true)
            {
                var stored = (uint)_slots[slot * IntsPerSlot];
                if (stored == key)
                {
                    return _slots[(slot * IntsPerSlot) + 1];
                }

                if (stored >> KeyBits != (uint)_epoch)
                {
                    return -1;
                }

                slot = (slot + 1) & _mask;
            }
        }

        /// <summary>Adds the string made of a known string and one more byte under the next code.</summary>
        /// <param name="prefix">The code of the known string.</param>
        /// <param name="value">The byte.</param>
        internal void Add(int prefix, byte value)
        {
            var key = KeyOf(prefix, value);
            var slot = SlotOf(key);
            while ((uint)_slots[slot * IntsPerSlot] >> KeyBits == (uint)_epoch)
            {
                slot = (slot + 1) & _mask;
            }

            _slots[slot * IntsPerSlot] = unchecked((int)key);
            _slots[(slot * IntsPerSlot) + 1] = Next;
            AdvanceWithoutEntry();
        }

        /// <summary>
        /// Counts an entry and widens the codes when the decoder will. The decoder adds each entry one code after the
        /// encoder, so it widens when its own next code plus the early-change value reaches the width's limit.
        /// </summary>
        internal void AdvanceWithoutEntry()
        {
            Next++;
            if (Next + earlyChange > (1 << Width) && Width < MaxWidth)
            {
                Width++;
            }
        }

        /// <summary>Empties the table after a clear-table code.</summary>
        internal void Reset()
        {
            Next = FirstCode;
            Width = MinWidth;
            if (_epoch == MaxEpoch)
            {
                _slots.AsSpan(0, (_mask + 1) * IntsPerSlot).Clear();
                _epoch = 1;
                return;
            }

            _epoch++;
        }

        /// <summary>Builds a table key: the epoch above the prefix code and the byte.</summary>
        /// <param name="prefix">The code of the known string.</param>
        /// <param name="value">The byte.</param>
        /// <returns>The key, never zero.</returns>
        private readonly uint KeyOf(int prefix, byte value) => ((uint)_epoch << KeyBits) | (uint)(prefix << ByteBits) | value;

        /// <summary>Gets the first slot to try for a key.</summary>
        /// <param name="key">The key.</param>
        /// <returns>The slot.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly int SlotOf(uint key) => (int)(((key & ((1U << KeyBits) - 1)) * HashMultiplier) >> (HashWordBits - hashBits));
    }

    /// <summary>Packs variable-width codes into bytes, high bit first.</summary>
    private ref struct CodeWriter
    {
        /// <summary>The bytes written.</summary>
        private PooledBuffer _output;

        /// <summary>The bits not yet written, right-aligned.</summary>
        private ulong _pending;

        /// <summary>The number of bits in <see cref="_pending"/>, fewer than eight between calls.</summary>
        private int _count;

        /// <summary>Gets the whole bytes written.</summary>
        internal readonly ReadOnlySpan<byte> WrittenSpan => _output.WrittenSpan;

        /// <summary>Returns the buffer to the pool.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Dispose() => _output.Dispose();

        /// <summary>Makes room for an expected number of bytes, so the output does not grow by doubling.</summary>
        /// <param name="capacity">The expected size of the output in bytes.</param>
        internal void Reserve(int capacity) => _ = _output.GetSpan(capacity);

        /// <summary>Writes a code.</summary>
        /// <param name="code">The code.</param>
        /// <param name="width">The code's width in bits.</param>
        internal void Write(int code, int width)
        {
            _pending = (_pending << width) | (uint)code;
            _count += width;
            while (_count >= ByteBits)
            {
                _count -= ByteBits;
                _output.WriteByte((byte)(_pending >> _count));
            }

            _pending &= (1UL << _count) - 1;
        }

        /// <summary>Writes the last bits, padded with zeros to a whole byte.</summary>
        internal void Flush()
        {
            if (_count == 0)
            {
                return;
            }

            _output.WriteByte((byte)(_pending << (ByteBits - _count)));
            _count = 0;
            _pending = 0;
        }
    }
}
