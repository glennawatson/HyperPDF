// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>One packet of a tile: its layer, resolution, component and precinct, and a sort key that puts it in a progression order.</summary>
/// <param name="Layer">The quality layer.</param>
/// <param name="Resolution">The resolution level.</param>
/// <param name="Component">The component.</param>
/// <param name="Precinct">The precinct within the resolution.</param>
/// <param name="Key">The sort key of the packet in the tile's progression order.</param>
internal readonly record struct JpxPacketKey(int Layer, int Resolution, int Component, int Precinct, UInt128 Key) : IComparable<JpxPacketKey>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(JpxPacketKey other) => Key.CompareTo(other.Key);
}
