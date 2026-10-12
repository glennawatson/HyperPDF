// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdf.Platform.Linux.Spelling;

/// <summary>
/// Hashes a word's first seven letters with none, one or two of them deleted. The letters sit in one 128-bit vector,
/// and each delete is a single byte shuffle that closes the gap, so no letters are copied one by one.
/// </summary>
internal static class DeleteHasher
{
    /// <summary>The leading letters hashed; a 128-bit vector holds eight.</summary>
    internal const int PrefixLength = 7;

    /// <summary>The letters a 128-bit vector holds.</summary>
    private const int VectorLetters = 8;

    /// <summary>The bytes in a letter.</summary>
    private const int LetterBytes = sizeof(char);

    /// <summary>The letters deleted to make each pair delete.</summary>
    private const int PairDeleted = 2;

    /// <summary>A shuffle index past the vector's end, which gives a zero byte.</summary>
    private const byte Zero = 0xFF;

    /// <summary>The first multiplier of the 64-bit finaliser (MurmurHash3's fmix64).</summary>
    private const ulong MixFirst = 0xFF51_AFD7_ED55_8CCD;

    /// <summary>The second multiplier of the 64-bit finaliser (MurmurHash3's fmix64).</summary>
    private const ulong MixSecond = 0xC4CE_B9FE_1A85_EC53;

    /// <summary>The finaliser's shift.</summary>
    private const int MixShift = 33;

    /// <summary>The rotation that mixes the upper half into the lower before finalising.</summary>
    private const int HalfRotation = 29;

    /// <summary>The shuffles that delete each single letter.</summary>
    private static readonly Vector128<byte>[] Singles = MakeSingles();

    /// <summary>The shuffles that delete each pair of letters, ordered by the later letter so a prefix's pairs come first.</summary>
    private static readonly Vector128<byte>[] Pairs = MakePairs();

    /// <summary>Writes the hashes of a prefix with a number of its letters deleted; repeats are left in.</summary>
    /// <param name="prefix">The prefix, no longer than <see cref="PrefixLength"/>.</param>
    /// <param name="level">The letters deleted: none, one or two.</param>
    /// <param name="hashes">Receives the hashes.</param>
    /// <returns>How many were written.</returns>
    internal static int Write(ReadOnlySpan<char> prefix, int level, Span<int> hashes)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(prefix.Length, PrefixLength);
        Span<ushort> padded = stackalloc ushort[VectorLetters];
        MemoryMarshal.Cast<char, ushort>(prefix).CopyTo(padded);
        var letters = Vector128.Create(padded).AsByte();
        var length = prefix.Length;
        if (level == 0)
        {
            hashes[0] = Hash(letters, length);
            return 1;
        }

        if (level == 1)
        {
            for (var i = 0; i < length; i++)
            {
                hashes[i] = Hash(Vector128.Shuffle(letters, Singles[i]), length - 1);
            }

            return length;
        }

        // Pairs whose later letter is inside the prefix come first.
        var count = length * (length - 1) / PairDeleted;
        for (var i = 0; i < count; i++)
        {
            hashes[i] = Hash(Vector128.Shuffle(letters, Pairs[i]), length - PairDeleted);
        }

        return count;
    }

    /// <summary>Mixes the letters and their count into a hash.</summary>
    /// <param name="letters">The letters, zero past the end.</param>
    /// <param name="length">How many letters.</param>
    /// <returns>The hash.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Hash(Vector128<byte> letters, int length)
    {
        var halves = letters.AsUInt64();
        var hash = halves.GetElement(0) ^ BitOperations.RotateLeft(halves.GetElement(1) * MixSecond, HalfRotation) ^ (ulong)length;
        hash ^= hash >> MixShift;
        hash *= MixFirst;
        hash ^= hash >> MixShift;
        hash *= MixSecond;
        hash ^= hash >> MixShift;
        return (int)hash;
    }

    /// <summary>Makes the shuffles that delete one letter.</summary>
    /// <returns>One shuffle per letter.</returns>
    private static Vector128<byte>[] MakeSingles()
    {
        var shuffles = new Vector128<byte>[PrefixLength];
        for (var i = 0; i < PrefixLength; i++)
        {
            shuffles[i] = Deleting(i, -1);
        }

        return shuffles;
    }

    /// <summary>Makes the shuffles that delete two letters.</summary>
    /// <returns>One shuffle per pair, ordered by the later letter.</returns>
    private static Vector128<byte>[] MakePairs()
    {
        var shuffles = new Vector128<byte>[PrefixLength * (PrefixLength - 1) / PairDeleted];
        var next = 0;
        for (var later = 1; later < PrefixLength; later++)
        {
            for (var earlier = 0; earlier < later; earlier++)
            {
                shuffles[next] = Deleting(earlier, later);
                next++;
            }
        }

        return shuffles;
    }

    /// <summary>Makes a shuffle that moves the letters after the deleted ones down and zeroes the rest.</summary>
    /// <param name="first">The first deleted letter.</param>
    /// <param name="second">The second deleted letter, or -1 for none.</param>
    /// <returns>The shuffle.</returns>
    private static Vector128<byte> Deleting(int first, int second)
    {
        Span<byte> indices = stackalloc byte[VectorLetters * LetterBytes];
        indices.Fill(Zero);
        var target = 0;
        for (var source = 0; source < VectorLetters; source++)
        {
            if (source == first || source == second)
            {
                continue;
            }

            indices[target * LetterBytes] = (byte)(source * LetterBytes);
            indices[(target * LetterBytes) + 1] = (byte)((source * LetterBytes) + 1);
            target++;
        }

        return Vector128.Create(indices);
    }
}
