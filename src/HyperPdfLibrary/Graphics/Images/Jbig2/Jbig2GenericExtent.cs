// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// Finds the end of an immediate generic region whose segment data length is unknown (T.88 section 7.2.7). The data
/// ends with a two-byte marker, 0xFF 0xAC for arithmetic coding or 0x00 0x00 for MMR, and then a four-byte row count
/// that says how many rows the region really holds.
/// </summary>
internal static class Jbig2GenericExtent
{
    /// <summary>The bytes of the end marker.</summary>
    private const int MarkerBytes = 2;

    /// <summary>Gets the end marker of arithmetic-coded data.</summary>
    private static ReadOnlySpan<byte> ArithmeticMarker => [0xFF, 0xAC];

    /// <summary>Gets the end marker of MMR-coded data.</summary>
    private static ReadOnlySpan<byte> MmrMarker => [0x00, 0x00];

    /// <summary>Finds the end marker and the row count after it.</summary>
    /// <param name="data">The whole stream.</param>
    /// <param name="start">The offset of the coded data, after the region's header fields.</param>
    /// <param name="mmr">Whether the data is MMR coded.</param>
    /// <param name="markerEnd">The offset just past the end marker; the coded data runs to here.</param>
    /// <param name="rows">The row count that follows the marker.</param>
    /// <returns><see langword="false"/> when no marker with a row count after it is found.</returns>
    internal static bool TryFind(ReadOnlySpan<byte> data, int start, bool mmr, out int markerEnd, out uint rows)
    {
        markerEnd = 0;
        rows = 0;
        var index = data[Math.Clamp(start, 0, data.Length)..].IndexOf(mmr ? MmrMarker : ArithmeticMarker);
        if (index < 0)
        {
            return false;
        }

        markerEnd = start + index + MarkerBytes;
        if (data.Length - markerEnd < sizeof(uint))
        {
            return false;
        }

        rows = BinaryPrimitives.ReadUInt32BigEndian(data[markerEnd..]);
        return true;
    }
}
