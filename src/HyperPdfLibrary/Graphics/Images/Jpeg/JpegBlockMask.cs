// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>Finds which coefficients of a block are not zero, so an entropy coder visits only those.</summary>
internal static class JpegBlockMask
{
    /// <summary>The shorts in a 128-bit vector.</summary>
    private const int VectorShorts = 8;

    /// <summary>Builds a mask with a bit set for every nonzero coefficient.</summary>
    /// <param name="block">The 64 coefficients in zigzag order.</param>
    /// <returns>A mask whose bit i is set when coefficient i is not zero.</returns>
    internal static ulong NonZero(ReadOnlySpan<short> block)
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            return NonZeroScalar(block);
        }

        ref var origin = ref MemoryMarshal.GetReference(block);
        ulong zeros = 0;
        for (var i = 0; i < JpegBlock.Length; i += VectorShorts)
        {
            var equal = Vector128.Equals(Vector128.LoadUnsafe(ref origin, (nuint)i), Vector128<short>.Zero);
            zeros |= (ulong)equal.ExtractMostSignificantBits() << i;
        }

        return ~zeros;
    }

    /// <summary>Builds the mask one coefficient at a time.</summary>
    /// <param name="block">The 64 coefficients in zigzag order.</param>
    /// <returns>The mask.</returns>
    internal static ulong NonZeroScalar(ReadOnlySpan<short> block)
    {
        ulong mask = 0;
        for (var i = 0; i < JpegBlock.Length; i++)
        {
            if (block[i] != 0)
            {
                mask |= 1UL << i;
            }
        }

        return mask;
    }
}
