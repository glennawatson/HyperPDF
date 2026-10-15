// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Buffers;

namespace HyperPdfLibrary.Objects;

/// <summary>Finds physical object extents in the store's source.</summary>
public static class StoreExtents
{
    /// <summary>
    /// Finds the bytes an object's definition spans in the original file: from its offset to the next object's. A compressed
    /// object spans its object stream.
    /// </summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <param name = "offset">Receives the first byte.</param>
    /// <param name = "length">Receives the number of bytes.</param>
    /// <returns><see langword="false"/> when the object is not in the file.</returns>
    internal static bool TryGetExtent(PdfObjectStore self, int number, out long offset, out long length)
    {
        offset = StoreRevisions.GetDefinitionOffset(self, number);
        length = 0;
        if (offset < 0)
        {
            return false;
        }

        var offsets = StoreExtents.SortedOffsets(self);
        var at = Array.BinarySearch(offsets, offset);
        var end = at >= 0 && at + 1 < offsets.Length ? offsets[at + 1] : self.Source.Length;
        length = Math.Max(0, end - offset);
        return length > 0;
    }

    /// <summary>Gets the sorted offsets of every object in the original file.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>The offsets.</returns>
    internal static long[] SortedOffsets(PdfObjectStore self)
    {
        if (Volatile.Read(ref self.SortedOffsetsState) is { } existing)
        {
            return existing;
        }

        var size = self.XrefSize;
        var offsets = ArrayPool<long>.Shared.Rent(size);
        try
        {
            var count = 0;
            for (var number = 1; number < size; number++)
            {
                var offset = StoreRevisions.GetDefinitionOffset(self, number);
                if (offset < 0)
                {
                    continue;
                }

                offsets[count] = offset;
                count++;
            }

            Array.Sort(offsets, 0, count);
            var sorted = offsets.AsSpan(0, count).ToArray();
            return Interlocked.CompareExchange(ref self.SortedOffsetsState, sorted, null) ?? sorted;
        }
        finally
        {
            ArrayPool<long>.Shared.Return(offsets);
        }
    }
}
