// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>Room for the most dash lengths a border keeps, held inline so a border value allocates nothing.</summary>
[InlineArray(PaintStroke.MaxDashes)]
internal struct DashLengths : IEquatable<DashLengths>
{
    /// <summary>The first length; the attribute makes the rest follow it.</summary>
    private float _element0;

    /// <summary>Compares two sets of lengths.</summary>
    /// <param name="left">The first set.</param>
    /// <param name="right">The second set.</param>
    /// <returns><see langword="true"/> when every length is equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(DashLengths left, DashLengths right) => left.Equals(right);

    /// <summary>Compares two sets of lengths.</summary>
    /// <param name="left">The first set.</param>
    /// <param name="right">The second set.</param>
    /// <returns><see langword="true"/> when any length differs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(DashLengths left, DashLengths right) => !left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(DashLengths other) => ((ReadOnlySpan<float>)this).SequenceEqual(other);

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is DashLengths other && Equals(other);

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        var hash = default(HashCode);
        hash.AddBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes((ReadOnlySpan<float>)this));
        return hash.ToHashCode();
    }

    /// <summary>Sets the dash pattern of the path that follows.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="lengths">The lengths; a copy, so the span below points at this frame.</param>
    /// <param name="count">How many lengths are used.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Apply(ref PdfContentBuilder builder, DashLengths lengths, int count) => builder.SetDash(((ReadOnlySpan<float>)lengths)[..count], 0);

    /// <summary>Sets the first length.</summary>
    /// <param name="value">The length.</param>
    internal void SetFirst(float value) => _element0 = value;

    /// <summary>Gets the first length.</summary>
    /// <returns>The length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly float GetFirst() => _element0;
}
