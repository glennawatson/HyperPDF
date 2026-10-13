// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Cms;

/// <summary>
/// Where the parts of a point on an Edwards curve are in its limbs. A point is three field elements of
/// <c>n</c> limbs each, X then Y then Z, in projective coordinates where the affine point is (X/Z, Y/Z).
/// </summary>
internal static class EdwardsPoint
{
    /// <summary>The field elements in a point.</summary>
    internal const int Elements = 3;

    /// <summary>Gets the X element of a point.</summary>
    /// <param name="point">The point.</param>
    /// <param name="limbs">The limbs in an element.</param>
    /// <returns>The X element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<ulong> X(Span<ulong> point, int limbs) => point[..limbs];

    /// <summary>Gets the X element of a point.</summary>
    /// <param name="point">The point.</param>
    /// <param name="limbs">The limbs in an element.</param>
    /// <returns>The X element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<ulong> X(ReadOnlySpan<ulong> point, int limbs) => point[..limbs];

    /// <summary>Gets the Y element of a point.</summary>
    /// <param name="point">The point.</param>
    /// <param name="limbs">The limbs in an element.</param>
    /// <returns>The Y element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<ulong> Y(Span<ulong> point, int limbs) => point[limbs..(limbs + limbs)];

    /// <summary>Gets the Y element of a point.</summary>
    /// <param name="point">The point.</param>
    /// <param name="limbs">The limbs in an element.</param>
    /// <returns>The Y element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<ulong> Y(ReadOnlySpan<ulong> point, int limbs) => point[limbs..(limbs + limbs)];

    /// <summary>Gets the Z element of a point.</summary>
    /// <param name="point">The point.</param>
    /// <param name="limbs">The limbs in an element.</param>
    /// <returns>The Z element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<ulong> Z(Span<ulong> point, int limbs) => point[(limbs + limbs)..(limbs + limbs + limbs)];

    /// <summary>Gets the Z element of a point.</summary>
    /// <param name="point">The point.</param>
    /// <param name="limbs">The limbs in an element.</param>
    /// <returns>The Z element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<ulong> Z(ReadOnlySpan<ulong> point, int limbs) => point[(limbs + limbs)..(limbs + limbs + limbs)];

    /// <summary>Gets the limbs a point takes.</summary>
    /// <param name="limbs">The limbs in an element.</param>
    /// <returns>The limbs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Length(int limbs) => limbs + limbs + limbs;
}
