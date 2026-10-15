// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// Allocates the pixel arrays of decoded images. Large arrays go on the pinned object heap, so a backend can retain them
/// without a pixel copy. The array is zeroed, because a truncated image keeps the
/// rows it did not receive as transparent black.
/// </summary>
internal static class PixelMemory
{
    /// <summary>The size from which an array is pinned: 64 KiB. Smaller images are copied, which costs less than fragmenting the pinned heap.</summary>
    internal const int PinnedFrom = 64 * 1024;

    /// <summary>Allocates a zeroed pixel array.</summary>
    /// <param name="length">The size in bytes.</param>
    /// <param name="pinned">Receives whether the array is on the pinned object heap.</param>
    /// <returns>The array.</returns>
    /// <exception cref="OverflowException">The size does not fit one array; callers keep images within the decoded byte budget first.</exception>
    internal static byte[] Allocate(long length, out bool pinned)
    {
        pinned = length >= PinnedFrom;
        return pinned ? GC.AllocateArray<byte>(checked((int)length), true) : new byte[length];
    }

    /// <summary>Allocates a pixel array without clearing it, for a caller that writes every byte before anyone reads it.</summary>
    /// <param name="length">The size in bytes.</param>
    /// <param name="pinned">Receives whether the array is on the pinned object heap.</param>
    /// <returns>The array, with undefined contents.</returns>
    /// <exception cref="OverflowException">The size does not fit one array; callers keep images within the decoded byte budget first.</exception>
    internal static byte[] AllocateUninitialized(long length, out bool pinned)
    {
        pinned = length >= PinnedFrom;
        return GC.AllocateUninitializedArray<byte>(checked((int)length), pinned);
    }
}
