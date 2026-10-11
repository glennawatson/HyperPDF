// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>Sizes pooled code-block buffers and addresses their stripe-ordered samples.</summary>
internal static class JpxBlockLayout
{
    /// <summary>The rows of a stripe.</summary>
    internal const int StripeRows = 4;

    /// <summary>The last row of a stripe.</summary>
    internal const int LastRow = StripeRows - 1;

    /// <summary>The smallest state buffer rented.</summary>
    internal const int MinimumState = 4096;

    /// <summary>The padding columns, and padding stripes, around a code-block's state: one on each side.</summary>
    internal const int Border = 2;

    /// <summary>The flag of a significant coefficient.</summary>
    internal const byte Significant = 1;

    /// <summary>The significant flags of a whole stripe column.</summary>
    internal const uint ColumnSignificant = 0x01010101;

    /// <summary>The flag of a sample that became significant in the SigProp pass.</summary>
    internal const byte NewlySignificant = 2;

    /// <summary>The columns of a quad.</summary>
    internal const int QuadWidth = 2;

    /// <summary>The shift that puts the sign bit at the top of a sign-magnitude sample.</summary>
    internal const int SignShift = 31;

    /// <summary>Returns a rented array.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array, emptied.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    internal static void Return<T>(ref T[] array, ScratchPools scratchPools)
    {
        if (array.Length > 0)
        {
            scratchPools.Get<T>().Return(array);
        }

        array = [];
    }

    /// <summary>Makes sure a rented array holds enough elements.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array, replaced when too small.</param>
    /// <param name="length">The elements needed.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    internal static void Ensure<T>(ref T[] array, int length, ScratchPools scratchPools)
    {
        if (array.Length >= length)
        {
            return;
        }

        Return(ref array, scratchPools);
        array = scratchPools.Get<T>().Rent(Math.Max(length, MinimumState));
    }

    /// <summary>Reads the four bytes of a stripe column.</summary>
    /// <param name="buffer">The state buffer.</param>
    /// <param name="index">The column's first byte.</param>
    /// <returns>The four bytes as one value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ReadColumn(byte[] buffer, int index) =>
        Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), index));

    /// <summary>Writes the four bytes of a stripe column.</summary>
    /// <param name="buffer">The state buffer.</param>
    /// <param name="index">The column's first byte.</param>
    /// <param name="value">The four bytes as one value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteColumn(byte[] buffer, int index, uint value) =>
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), index), value);

    /// <summary>Gets the coefficient above another in the stripe layout.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="i">The coefficient.</param>
    /// <param name="row">The row within the stripe.</param>
    /// <returns>The index of the coefficient above.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Above(JpxBlockState state, int i, int row) => row > 0 ? i - 1 : i - state.StripeStride + LastRow;

    /// <summary>Gets the coefficient below another in the stripe layout.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="i">The coefficient.</param>
    /// <param name="row">The row within the stripe.</param>
    /// <returns>The index of the coefficient below.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Below(JpxBlockState state, int i, int row) => row < LastRow ? i + 1 : i + state.StripeStride - LastRow;

    /// <summary>Gets a sample's index in the stripe-ordered state buffers.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int HtIndex(JpxBlockState state, int x, int y) => (((y / StripeRows) + 1) * state.StripeStride) + ((x + 1) * StripeRows) + (y % StripeRows);
}
