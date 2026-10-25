// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets.Shaping;

/// <summary>A positioning adjustment (a GPOS value record), in font units.</summary>
/// <param name="XPlacement">The shift of the drawn glyph to the right.</param>
/// <param name="YPlacement">The shift of the drawn glyph upward.</param>
/// <param name="XAdvance">The change to the advance.</param>
public readonly record struct ValueAdjustment(int XPlacement, int YPlacement, int XAdvance)
{
    /// <summary>Gets an adjustment that changes only the advance, as kerning does.</summary>
    /// <param name="advance">The change to the advance.</param>
    /// <returns>The adjustment.</returns>
    public static ValueAdjustment Advance(int advance) => new(0, 0, advance);

    /// <summary>Gets the value format bits for the fields this adjustment sets.</summary>
    /// <returns>The format.</returns>
    internal int Format() => (XPlacement != 0 ? 1 : 0) | (YPlacement != 0 ? 0x2 : 0) | (XAdvance != 0 ? 0x4 : 0);

    /// <summary>Writes the fields named by a value format.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="format">The value format.</param>
    internal void Write(ByteBuffer buffer, int format)
    {
        if ((format & 1) != 0)
        {
            buffer.Write(XPlacement & 0xFFFF);
        }

        if ((format & 0x2) != 0)
        {
            buffer.Write(YPlacement & 0xFFFF);
        }

        if ((format & 0x4) != 0)
        {
            buffer.Write(XAdvance & 0xFFFF);
        }
    }
}
