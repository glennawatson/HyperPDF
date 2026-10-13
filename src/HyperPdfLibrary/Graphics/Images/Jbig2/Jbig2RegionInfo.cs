// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The region segment information field (T.88 section 7.4.1).</summary>
/// <param name="Width">The region width.</param>
/// <param name="Height">The region height.</param>
/// <param name="X">The region's left column on the page.</param>
/// <param name="Y">The region's top row on the page.</param>
/// <param name="Flags">The flags, whose low three bits are the external combination operator.</param>
[DebuggerDisplay("Jbig2RegionInfo: {Width}x{Height} at ({X}, {Y})")]
internal readonly record struct Jbig2RegionInfo(int Width, int Height, int X, int Y, byte Flags)
{
    /// <summary>The mask of the combination operator bits.</summary>
    private const int OperatorMask = 0x07;

    /// <summary>Gets the external combination operator.</summary>
    internal Jbig2ComposeOperator Operator => ToOperator(Flags);

    /// <summary>Reads the information field.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="info">The information.</param>
    /// <returns><see langword="false"/> when the data ends.</returns>
    internal static bool TryRead(ref Jbig2Reader reader, out Jbig2RegionInfo info)
    {
        info = default;
        if (!reader.TryReadInt32(out var width) || !reader.TryReadInt32(out var height) || !reader.TryReadInt32(out var x)
            || !reader.TryReadInt32(out var y) || !reader.TryReadByte(out var flags))
        {
            return false;
        }

        info = new(width, height, x, y, flags);
        return true;
    }

    /// <summary>
    /// Maps the three operator bits (T.88 section 7.4.1.5): 0 to 4 are OR, AND, XOR, XNOR and REPLACE. Our rule for the
    /// reserved values 5 to 7 is OR, the operator that never erases black pixels already drawn.
    /// </summary>
    /// <param name="bits">The bits.</param>
    /// <returns>The operator.</returns>
    internal static Jbig2ComposeOperator ToOperator(int bits)
    {
        var value = bits & OperatorMask;
        return value <= (int)Jbig2ComposeOperator.Replace ? (Jbig2ComposeOperator)value : Jbig2ComposeOperator.Or;
    }
}
