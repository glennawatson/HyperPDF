// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>The bits of a 16 or 32-bit pixel that hold each colour.</summary>
/// <param name="Red">The red bits.</param>
/// <param name="Green">The green bits.</param>
/// <param name="Blue">The blue bits.</param>
/// <param name="Alpha">The alpha bits; zero when the pixel has no alpha.</param>
[DebuggerDisplay("BmpMasks: R {Red:X} G {Green:X} B {Blue:X} A {Alpha:X}")]
internal readonly record struct BmpMasks(uint Red, uint Green, uint Blue, uint Alpha)
{
    /// <summary>Gets the masks of 16-bit pixels with no masks given: five bits of each colour.</summary>
    internal static BmpMasks Default16 => new(0x7C00, 0x03E0, 0x001F, 0);

    /// <summary>Gets the masks of 32-bit pixels with no masks given: eight bits of each colour.</summary>
    internal static BmpMasks Default32 => new(0x00FF0000, 0x0000FF00, 0x000000FF, 0);

    /// <summary>Takes the bits of a mask out of a pixel and scales them to eight bits.</summary>
    /// <param name="pixel">The pixel.</param>
    /// <param name="mask">The mask.</param>
    /// <returns>The value from 0 to 255; 255 when the mask is empty.</returns>
    internal static byte Extract(uint pixel, uint mask)
    {
        if (mask == 0)
        {
            return byte.MaxValue;
        }

        var shift = BitOperations.TrailingZeroCount(mask);
        return (byte)(((pixel & mask) >> shift) * byte.MaxValue / (mask >> shift));
    }
}
