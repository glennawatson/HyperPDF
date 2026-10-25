// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Security;

/// <summary>
/// MD5 (RFC 1321), which the PDF standard security handler requires for revisions 2 to 4 key derivation. It is written
/// here rather than taken from the platform because FIPS-mode systems refuse the platform MD5, and these documents must
/// still open there. It is not used for anything that relies on collision resistance.
/// </summary>
internal static class Md5
{
    /// <summary>The length of a hash.</summary>
    internal const int HashLength = 16;

    /// <summary>The length of a block.</summary>
    private const int BlockLength = 64;

    /// <summary>The number of 32-bit words in a block.</summary>
    private const int BlockWords = 16;

    /// <summary>The bytes of the length field at the end of the padding.</summary>
    private const int LengthFieldBytes = 8;

    /// <summary>The first padding byte.</summary>
    private const byte PaddingStart = 0x80;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The number of 32-bit state words.</summary>
    private const int StateWords = 4;

    /// <summary>The number of rounds of 16 steps.</summary>
    private const int Rounds = 4;

    /// <summary>The steps per round.</summary>
    private const int StepsPerRound = 16;

    /// <summary>The index of the third state word.</summary>
    private const int WordC = 2;

    /// <summary>The index of the fourth state word.</summary>
    private const int WordD = 3;

    /// <summary>The longest padded tail: two blocks.</summary>
    private const int TailLength = BlockLength * 2;

    /// <summary>The entries per round in <see cref="WordSchedule"/>: a start and a stride.</summary>
    private const int ScheduleFields = 2;

    /// <summary>The round that uses the H (parity) function.</summary>
    private const int RoundParity = 2;

    /// <summary>The bytes of a file hashed at a time, a multiple of the block length.</summary>
    private const int ChunkLength = 1 << 16;

    /// <summary>Gets the initial state.</summary>
    private static ReadOnlySpan<uint> Initial => [0x67452301, 0xEFCDAB89, 0x98BADCFE, 0x10325476];

    /// <summary>Gets the per-step constants.</summary>
    private static ReadOnlySpan<uint> Constants =>
    [
        0xD76AA478, 0xE8C7B756, 0x242070DB, 0xC1BDCEEE, 0xF57C0FAF, 0x4787C62A, 0xA8304613, 0xFD469501,
        0x698098D8, 0x8B44F7AF, 0xFFFF5BB1, 0x895CD7BE, 0x6B901122, 0xFD987193, 0xA679438E, 0x49B40821,
        0xF61E2562, 0xC040B340, 0x265E5A51, 0xE9B6C7AA, 0xD62F105D, 0x02441453, 0xD8A1E681, 0xE7D3FBC8,
        0x21E1CDE6, 0xC33707D6, 0xF4D50D87, 0x455A14ED, 0xA9E3E905, 0xFCEFA3F8, 0x676F02D9, 0x8D2A4C8A,
        0xFFFA3942, 0x8771F681, 0x6D9D6122, 0xFDE5380C, 0xA4BEEA44, 0x4BDECFA9, 0xF6BB4B60, 0xBEBFBC70,
        0x289B7EC6, 0xEAA127FA, 0xD4EF3085, 0x04881D05, 0xD9D4D039, 0xE6DB99E5, 0x1FA27CF8, 0xC4AC5665,
        0xF4292244, 0x432AFF97, 0xAB9423A7, 0xFC93A039, 0x655B59C3, 0x8F0CCC92, 0xFFEFF47D, 0x85845DD1,
        0x6FA87E4F, 0xFE2CE6E0, 0xA3014314, 0x4E0811A1, 0xF7537E82, 0xBD3AF235, 0x2AD7D2BB, 0xEB86D391,
    ];

    /// <summary>Gets the rotation of each step within a round, four per round.</summary>
    private static ReadOnlySpan<byte> Rotations =>
        [0x07, 0x0C, 0x11, 0x16, 0x05, 0x09, 0x0E, 0x14, 0x04, 0x0B, 0x10, 0x17, 0x06, 0x0A, 0x0F, 0x15];

    /// <summary>Gets the message word each step reads, as a starting index and stride per round.</summary>
    private static ReadOnlySpan<byte> WordSchedule => [0x00, 0x01, 0x01, 0x05, 0x05, 0x03, 0x00, 0x07];

    /// <summary>Hashes data.</summary>
    /// <param name="source">The data.</param>
    /// <param name="destination">The 16-byte hash.</param>
    /// <returns>The number of bytes written.</returns>
    internal static int HashData(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        Span<uint> state = stackalloc uint[StateWords];
        Initial.CopyTo(state);
        var whole = source.Length - (source.Length % BlockLength);
        TransformBlocks(state, source[..whole]);
        Finish(state, source[whole..], source.Length, destination);
        return HashLength;
    }

    /// <summary>Hashes a whole file, reading it a chunk at a time.</summary>
    /// <param name="source">The file.</param>
    /// <param name="destination">The 16-byte hash.</param>
    /// <returns>The number of bytes written.</returns>
    internal static int HashData(PdfByteSource source, Span<byte> destination)
    {
        Span<uint> state = stackalloc uint[StateWords];
        Initial.CopyTo(state);
        var chunk = ArrayPool<byte>.Shared.Rent(ChunkLength);
        try
        {
            var position = 0L;
            var read = source.Read(position, chunk.AsSpan(0, ChunkLength));
            while (read == ChunkLength)
            {
                TransformBlocks(state, chunk.AsSpan(0, read));
                position += read;
                read = source.Read(position, chunk.AsSpan(0, ChunkLength));
            }

            var whole = read - (read % BlockLength);
            TransformBlocks(state, chunk.AsSpan(0, whole));
            Finish(state, chunk.AsSpan(whole, read - whole), position + read, destination);
            return HashLength;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }

    /// <summary>Mixes whole blocks into the state.</summary>
    /// <param name="state">The four state words.</param>
    /// <param name="blocks">The data, a multiple of the block length.</param>
    private static void TransformBlocks(Span<uint> state, ReadOnlySpan<byte> blocks)
    {
        for (var offset = 0; offset < blocks.Length; offset += BlockLength)
        {
            Transform(state, blocks.Slice(offset, BlockLength));
        }
    }

    /// <summary>Pads and mixes the last partial block, then writes the hash.</summary>
    /// <param name="state">The four state words.</param>
    /// <param name="remaining">The bytes after the last whole block.</param>
    /// <param name="totalLength">The length of all the data hashed.</param>
    /// <param name="destination">The 16-byte hash.</param>
    private static void Finish(Span<uint> state, ReadOnlySpan<byte> remaining, long totalLength, Span<byte> destination)
    {
        // The tail, a 0x80 byte, zeros and the bit length fit in one or two final blocks.
        Span<byte> tail = stackalloc byte[TailLength];
        tail.Clear();
        remaining.CopyTo(tail);
        tail[remaining.Length] = PaddingStart;
        var tailLength = remaining.Length + 1 + LengthFieldBytes <= BlockLength ? BlockLength : TailLength;
        BinaryPrimitives.WriteUInt64LittleEndian(tail[(tailLength - LengthFieldBytes)..], (ulong)totalLength * ByteBits);
        TransformBlocks(state, tail[..tailLength]);

        for (var i = 0; i < state.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination[(i * sizeof(uint))..], state[i]);
        }
    }

    /// <summary>Mixes one block into the state.</summary>
    /// <param name="state">The four state words.</param>
    /// <param name="block">The 64-byte block.</param>
    private static void Transform(Span<uint> state, ReadOnlySpan<byte> block)
    {
        Span<uint> words = stackalloc uint[BlockWords];
        for (var i = 0; i < BlockWords; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt32LittleEndian(block[(i * sizeof(uint))..]);
        }

        var a = state[0];
        var b = state[1];
        var c = state[WordC];
        var d = state[WordD];

        // Each round is its own instantiation, so the mixing function and the word order are fixed when the code is compiled.
        RunRound<RoundF>(ref a, ref b, ref c, ref d, words);
        RunRound<RoundG>(ref a, ref b, ref c, ref d, words);
        RunRound<RoundH>(ref a, ref b, ref c, ref d, words);
        RunRound<RoundI>(ref a, ref b, ref c, ref d, words);

        state[0] += a;
        state[1] += b;
        state[WordC] += c;
        state[WordD] += d;
    }

    /// <summary>Runs the 16 steps of one round.</summary>
    /// <typeparam name="TRound">The round.</typeparam>
    /// <param name="a">The first state word.</param>
    /// <param name="b">The second state word.</param>
    /// <param name="c">The third state word.</param>
    /// <param name="d">The fourth state word.</param>
    /// <param name="words">The 16 words of the block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RunRound<TRound>(ref uint a, ref uint b, ref uint c, ref uint d, ReadOnlySpan<uint> words)
        where TRound : struct, IMd5Round
    {
        var first = WordSchedule[TRound.Index * ScheduleFields];
        var stride = WordSchedule[(TRound.Index * ScheduleFields) + 1];
        var rotations = Rotations.Slice(TRound.Index * Rounds, Rounds);
        var constants = Constants.Slice(TRound.Index * StepsPerRound, StepsPerRound);
        var x = a;
        var y = b;
        var z = c;
        var w = d;
        for (var step = 0; step < StepsPerRound; step++)
        {
            var word = words[(first + (stride * step)) & (BlockWords - 1)];
            var next = y + BitOperations.RotateLeft(x + TRound.Mix(y, z, w) + constants[step] + word, rotations[step & (Rounds - 1)]);
            x = w;
            w = z;
            z = y;
            y = next;
        }

        a = x;
        b = y;
        c = z;
        d = w;
    }

    /// <summary>The first round.</summary>
    private readonly struct RoundF : IMd5Round
    {
        /// <inheritdoc/>
        public static int Index => 0;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix(uint b, uint c, uint d) => (b & c) | (~b & d);
    }

    /// <summary>The second round.</summary>
    private readonly struct RoundG : IMd5Round
    {
        /// <inheritdoc/>
        public static int Index => 1;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix(uint b, uint c, uint d) => (d & b) | (~d & c);
    }

    /// <summary>The third round.</summary>
    private readonly struct RoundH : IMd5Round
    {
        /// <inheritdoc/>
        public static int Index => RoundParity;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix(uint b, uint c, uint d) => b ^ c ^ d;
    }

    /// <summary>The fourth round.</summary>
    private readonly struct RoundI : IMd5Round
    {
        /// <inheritdoc/>
        public static int Index => RoundParity + 1;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix(uint b, uint c, uint d) => c ^ (b | ~d);
    }
}
