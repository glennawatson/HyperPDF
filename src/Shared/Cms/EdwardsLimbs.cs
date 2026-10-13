// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;

namespace HyperPdf.Cms;

/// <summary>
/// Plain arithmetic on little-endian arrays of 64-bit limbs of one length, for the field and scalar arithmetic of
/// <see cref="EdwardsArithmetic"/>. It does not run in constant time, so it must never touch a private key.
/// </summary>
internal static class EdwardsLimbs
{
    /// <summary>The bits in a limb.</summary>
    private const int LimbBits = 64;

    /// <summary>The bytes in a limb.</summary>
    private const int LimbBytes = 8;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The mask of the bit position inside a byte.</summary>
    private const int ByteBitMask = 7;

    /// <summary>The number of limbs, one past a field element, that an encoded number is read into to find an overflow.</summary>
    private const int WideLimbs = EdwardsField.MaxLimbs + 1;

    /// <summary>Reads a little-endian number into limbs.</summary>
    /// <param name="littleEndian">The bytes, at most 64.</param>
    /// <param name="limbs">Receives the number.</param>
    /// <returns><see langword="false"/> when the number does not fit in the limbs.</returns>
    internal static bool TryRead(ReadOnlySpan<byte> littleEndian, Span<ulong> limbs)
    {
        if (littleEndian.Length > WideLimbs * LimbBytes)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[WideLimbs * LimbBytes];
        bytes.Clear();
        littleEndian.CopyTo(bytes);
        for (var i = 0; i < WideLimbs; i++)
        {
            var limb = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(i * LimbBytes)..]);
            if (i < limbs.Length)
            {
                limbs[i] = limb;
            }
            else if (limb != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Compares two numbers.</summary>
    /// <param name="x">The first number.</param>
    /// <param name="y">The second number.</param>
    /// <returns>Negative, zero or positive as <paramref name="x"/> is below, equal to or above <paramref name="y"/>.</returns>
    internal static int Compare(ReadOnlySpan<ulong> x, ReadOnlySpan<ulong> y)
    {
        for (var i = x.Length - 1; i >= 0; i--)
        {
            if (x[i] != y[i])
            {
                return x[i] < y[i] ? -1 : 1;
            }
        }

        return 0;
    }

    /// <summary>Determines whether a number is zero.</summary>
    /// <param name="x">The number.</param>
    /// <returns><see langword="true"/> when every limb is zero.</returns>
    internal static bool IsZero(ReadOnlySpan<ulong> x)
    {
        foreach (var limb in x)
        {
            if (limb != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Adds two numbers; the result may be either of them.</summary>
    /// <param name="result">Receives the sum, without its carry.</param>
    /// <param name="x">The first number.</param>
    /// <param name="y">The second number.</param>
    /// <returns>The carry out of the top limb: 0 or 1.</returns>
    internal static ulong Add(Span<ulong> result, ReadOnlySpan<ulong> x, ReadOnlySpan<ulong> y)
    {
        ulong carry = 0;
        for (var i = 0; i < result.Length; i++)
        {
            var sum = unchecked(x[i] + y[i]);
            var first = sum < x[i] ? 1UL : 0UL;
            var total = unchecked(sum + carry);
            carry = first | (total < sum ? 1UL : 0UL);
            result[i] = total;
        }

        return carry;
    }

    /// <summary>Subtracts a number from another; the result may be either of them.</summary>
    /// <param name="result">Receives the difference, wrapped when it is negative.</param>
    /// <param name="x">The number to subtract from.</param>
    /// <param name="y">The number to subtract.</param>
    /// <returns>The borrow out of the top limb: 1 when <paramref name="y"/> is above <paramref name="x"/>.</returns>
    internal static ulong Subtract(Span<ulong> result, ReadOnlySpan<ulong> x, ReadOnlySpan<ulong> y)
    {
        ulong borrow = 0;
        for (var i = 0; i < result.Length; i++)
        {
            var difference = unchecked(x[i] - y[i]);
            var first = x[i] < y[i] ? 1UL : 0UL;
            var total = unchecked(difference - borrow);
            borrow = first | (difference < borrow ? 1UL : 0UL);
            result[i] = total;
        }

        return borrow;
    }

    /// <summary>Doubles a number that is below half the largest the limbs hold.</summary>
    /// <param name="x">The number, doubled in place.</param>
    internal static void ShiftLeftOne(Span<ulong> x)
    {
        ulong carry = 0;
        for (var i = 0; i < x.Length; i++)
        {
            var next = x[i] >> (LimbBits - 1);
            x[i] = (x[i] << 1) | carry;
            carry = next;
        }
    }

    /// <summary>Finds the highest set bit of a number.</summary>
    /// <param name="x">The number.</param>
    /// <returns>The bit's position, or -1 when the number is zero.</returns>
    internal static int TopBit(ReadOnlySpan<ulong> x)
    {
        for (var i = x.Length - 1; i >= 0; i--)
        {
            if (x[i] != 0)
            {
                return (i * LimbBits) + (LimbBits - 1 - BitOperations.LeadingZeroCount(x[i]));
            }
        }

        return -1;
    }

    /// <summary>Tests one bit of a number.</summary>
    /// <param name="x">The number.</param>
    /// <param name="bit">The bit's position.</param>
    /// <returns><see langword="true"/> when the bit is set.</returns>
    internal static bool IsSet(ReadOnlySpan<ulong> x, int bit) => ((x[bit / LimbBits] >> (bit % LimbBits)) & 1) != 0;

    /// <summary>Reduces a little-endian number of any length modulo an order, one bit at a time from the top.</summary>
    /// <param name="littleEndian">The number's bytes.</param>
    /// <param name="order">The modulus, below half the largest number the limbs hold.</param>
    /// <param name="result">Receives the remainder.</param>
    internal static void Reduce(ReadOnlySpan<byte> littleEndian, ReadOnlySpan<ulong> order, Span<ulong> result)
    {
        result.Clear();
        for (var bit = (littleEndian.Length * ByteBits) - 1; bit >= 0; bit--)
        {
            ShiftLeftOne(result);
            if (((littleEndian[bit / ByteBits] >> (bit & ByteBitMask)) & 1) != 0)
            {
                result[0] |= 1;
            }

            if (Compare(result, order) >= 0)
            {
                _ = Subtract(result, result, order);
            }
        }
    }
}
