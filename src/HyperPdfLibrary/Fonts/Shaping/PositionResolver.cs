// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Turns the attachments GPOS recorded into offsets once every lookup has run: the offset of a mark is measured from
/// the pen, which has moved on by the advances of the glyphs between the mark and the glyph it sits on.
/// </summary>
internal static class PositionResolver
{
    /// <summary>The GDEF class of mark glyphs.</summary>
    private const int MarkClass = 3;

    /// <summary>Resolves attachments into offsets.</summary>
    /// <param name="buffer">The glyphs, with positions.</param>
    /// <param name="rightToLeft">Whether the run is drawn right to left, so the glyphs are drawn in the reverse of their buffer order.</param>
    internal static void Resolve(GlyphBuffer buffer, bool rightToLeft)
    {
        var positions = buffer.Positions;
        for (var i = 0; i < positions.Length; i++)
        {
            switch (positions[i].Attach)
            {
                case AttachKind.Mark:
                {
                    PlaceMark(positions, i, rightToLeft);
                    break;
                }

                case AttachKind.Cursive:
                {
                    positions[i].YOffset += positions[i].AttachY + positions[positions[i].AttachTo].YOffset;
                    break;
                }

                default:
                {
                    break;
                }
            }
        }
    }

    /// <summary>Gives marks that GDEF classifies no advance, as the font intends marks to have none.</summary>
    /// <param name="buffer">The glyphs, with positions.</param>
    internal static void ZeroMarkAdvances(GlyphBuffer buffer)
    {
        var items = buffer.Items;
        var positions = buffer.Positions;
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i].GlyphClass == MarkClass)
            {
                positions[i].XAdvance = 0;
            }
        }
    }

    /// <summary>Places a mark on the glyph it is attached to.</summary>
    /// <param name="positions">The positions.</param>
    /// <param name="mark">The mark's index.</param>
    /// <param name="rightToLeft">Whether the run is drawn right to left.</param>
    private static void PlaceMark(Span<GlyphPosition> positions, int mark, bool rightToLeft)
    {
        var target = positions[mark].AttachTo;
        var distance = 0;
        if (rightToLeft)
        {
            for (var i = target + 1; i <= mark; i++)
            {
                distance += positions[i].XAdvance;
            }

            positions[mark].XOffset = positions[target].XOffset + positions[mark].AttachX + distance;
        }
        else
        {
            for (var i = target; i < mark; i++)
            {
                distance += positions[i].XAdvance;
            }

            positions[mark].XOffset = positions[target].XOffset + positions[mark].AttachX - distance;
        }

        positions[mark].YOffset = positions[target].YOffset + positions[mark].AttachY;
    }
}
