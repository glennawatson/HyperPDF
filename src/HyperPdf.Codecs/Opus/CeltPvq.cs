// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Reads the shape of a CELT band as a point of a pyramid vector quantiser (RFC 6716 section 4.3.4): an integer vector whose
/// absolute values sum to a fixed number of pulses, numbered among all such vectors, then rotated so the energy spreads and scaled to
/// unit length.
/// </summary>
internal static class CeltPvq
{
    /// <summary>The rotation spread values: none, light, normal and aggressive.</summary>
    internal const int SpreadNone = 0;

    /// <summary>The light spreading decision.</summary>
    internal const int SpreadLight = 1;

    /// <summary>The normal spreading decision.</summary>
    internal const int SpreadNormal = 2;

    /// <summary>The aggressive spreading decision.</summary>
    internal const int SpreadAggressive = 3;

    /// <summary>The tiny amount added to an energy so a silent vector can still be scaled.</summary>
    private const float Epsilon = 1e-15F;

    /// <summary>Half, the share of the rotation angle's scale.</summary>
    private const float Half = 0.5F;

    /// <summary>The two entries of a pair, and the extra entries the pulse row carries.</summary>
    private const int Pair = 2;

    /// <summary>The most entries of a band that are kept on the stack while it is read.</summary>
    private const int StackLimit = 256;

    /// <summary>The most pulses of a band whose row is kept on the stack while it is read.</summary>
    private const int RowStackLimit = 512;

    /// <summary>The fewest blocks times eight entries a vector needs before a second, wider rotation is made.</summary>
    private const int WideRotation = 8;

    /// <summary>The columns of the rotation factor table.</summary>
    private const int FactorLight = 15;

    /// <summary>The rotation factor of the normal decision.</summary>
    private const int FactorNormal = 10;

    /// <summary>The rotation factor of the aggressive decision.</summary>
    private const int FactorAggressive = 5;

    /// <summary>Reads a vector of pulses and scales it to a gain.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="shape">Receives the unit vector times the gain.</param>
    /// <param name="pulses">The number of pulses.</param>
    /// <param name="spread">The spreading decision.</param>
    /// <param name="blocks">The short blocks the band is made of.</param>
    /// <param name="gain">The gain.</param>
    /// <returns>The collapse mask: a bit for each short block that has a pulse.</returns>
    internal static uint Read(ref OpusRangeDecoder decoder, Span<float> shape, int pulses, int spread, int blocks, float gain)
    {
        var size = shape.Length;
        Span<int> vector = size <= StackLimit ? stackalloc int[size] : new int[size];
        DecodePulses(ref decoder, vector, pulses);
        float energy = 0;
        foreach (var value in vector)
        {
            energy += value * value;
        }

        var scale = gain / MathF.Sqrt(energy);
        for (var i = 0; i < size; i++)
        {
            shape[i] = scale * vector[i];
        }

        Rotate(shape, -1, blocks, pulses, spread);
        return CollapseMask(vector, blocks);
    }

    /// <summary>Scales a vector to a gain.</summary>
    /// <param name="shape">The vector, changed in place.</param>
    /// <param name="gain">The gain.</param>
    internal static void Renormalise(Span<float> shape, float gain)
    {
        var energy = Epsilon;
        foreach (var value in shape)
        {
            energy += value * value;
        }

        var scale = gain / MathF.Sqrt(energy);
        for (var i = 0; i < shape.Length; i++)
        {
            shape[i] *= scale;
        }
    }

    /// <summary>Spreads the energy of a vector over its entries by rotating pairs of them (RFC 6716 section 4.3.4.3).</summary>
    /// <param name="shape">The vector, changed in place.</param>
    /// <param name="direction">1 to rotate forwards, -1 to undo it.</param>
    /// <param name="stride">The short blocks the vector interleaves.</param>
    /// <param name="pulses">The pulses of the vector.</param>
    /// <param name="spread">The spreading decision.</param>
    internal static void Rotate(Span<float> shape, int direction, int stride, int pulses, int spread)
    {
        var length = shape.Length;
        if (Pair * pulses >= length || spread == SpreadNone)
        {
            return;
        }

        var factor = spread switch
        {
            SpreadLight => FactorLight,
            SpreadNormal => FactorNormal,
            _ => FactorAggressive,
        };
        var gain = (float)length / (length + (factor * pulses));
        var theta = Half * gain * gain;
        var c = MathF.Cos(MathF.PI * Half * theta);
        var s = MathF.Cos(MathF.PI * Half * (1F - theta));
        var stride2 = 0;
        if (length >= WideRotation * stride)
        {
            // The square root of length over stride, rounded: incrementing while (stride2 + 0.5)^2 is below length / stride.
            stride2 = 1;
            while ((((stride2 * stride2) + stride2) * stride) + (stride >> 2) < length)
            {
                stride2++;
            }
        }

        var each = length / stride;
        for (var i = 0; i < stride; i++)
        {
            var block = shape.Slice(i * each, each);
            if (direction < 0)
            {
                if (stride2 != 0)
                {
                    RotatePairs(block, stride2, s, c);
                }

                RotatePairs(block, 1, c, s);
            }
            else
            {
                RotatePairs(block, 1, c, -s);
                if (stride2 != 0)
                {
                    RotatePairs(block, stride2, s, -c);
                }
            }
        }
    }

    /// <summary>Rotates entries a stride apart, forwards then backwards through the vector.</summary>
    /// <param name="values">The vector, changed in place.</param>
    /// <param name="stride">The distance between the two entries of each pair.</param>
    /// <param name="keep">The weight each entry keeps of itself.</param>
    /// <param name="cross">The weight each entry takes from the other.</param>
    private static void RotatePairs(Span<float> values, int stride, float keep, float cross)
    {
        var length = values.Length;
        for (var i = 0; i < length - stride; i++)
        {
            var x1 = values[i];
            var x2 = values[i + stride];
            values[i + stride] = (keep * x2) + (cross * x1);
            values[i] = (keep * x1) - (cross * x2);
        }

        for (var i = length - (Pair * stride) - 1; i >= 0; i--)
        {
            var x1 = values[i];
            var x2 = values[i + stride];
            values[i + stride] = (keep * x2) + (cross * x1);
            values[i] = (keep * x1) - (cross * x2);
        }
    }

    /// <summary>Finds which short blocks of a band hold a pulse, so a block left empty can be filled with noise later.</summary>
    /// <param name="vector">The pulse vector.</param>
    /// <param name="blocks">The short blocks.</param>
    /// <returns>A bit for each block with a pulse.</returns>
    private static uint CollapseMask(ReadOnlySpan<int> vector, int blocks)
    {
        if (blocks <= 1)
        {
            return 1;
        }

        var each = vector.Length / blocks;
        uint mask = 0;
        for (var block = 0; block < blocks; block++)
        {
            for (var j = 0; j < each; j++)
            {
                if (vector[(block * each) + j] != 0)
                {
                    mask |= 1U << block;
                }
            }
        }

        return mask;
    }

    /// <summary>Reads the index of a pulse vector and turns it into the vector (RFC 6716 section 4.3.4.2).</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="vector">Receives the pulses, with their signs.</param>
    /// <param name="pulses">The number of pulses.</param>
    private static void DecodePulses(ref OpusRangeDecoder decoder, scoped Span<int> vector, int pulses)
    {
        var size = vector.Length;
        var rowLength = pulses + Pair;
        Span<uint> row = rowLength <= RowStackLimit ? stackalloc uint[rowLength] : new uint[rowLength];
        var count = BuildRow(size, pulses, row);
        var index = decoder.ReadUnsigned(count);
        var remaining = pulses;
        for (var j = 0; j < size; j++)
        {
            var above = row[remaining + 1];
            var negative = index >= above;
            if (negative)
            {
                index -= above;
            }

            var before = remaining;
            var cut = row[remaining];
            while (cut > index)
            {
                remaining--;
                cut = row[remaining];
            }

            index -= cut;
            var magnitude = before - remaining;
            vector[j] = negative ? -magnitude : magnitude;
            StepRowBack(row, remaining + Pair);
        }
    }

    /// <summary>Computes the numbers of vectors with a size and pulse count: <c>U(size, 0..pulses+1)</c>, and from them the total.</summary>
    /// <param name="size">The dimensions.</param>
    /// <param name="pulses">The pulses.</param>
    /// <param name="row">Receives <c>U(size, i)</c> for <c>i</c> from 0 to <c>pulses + 1</c>.</param>
    /// <returns>The number of vectors, <c>V(size, pulses)</c>.</returns>
    private static uint BuildRow(int size, int pulses, Span<uint> row)
    {
        var length = pulses + Pair;
        row[0] = 0;
        row[1] = 1;
        for (var k = Pair; k < length; k++)
        {
            row[k] = (uint)((k << 1) - 1);
        }

        for (var k = Pair; k < size; k++)
        {
            StepRowForward(row.Slice(1, pulses + 1));
        }

        return row[pulses] + row[pulses + 1];
    }

    /// <summary>Moves a row of the recurrence <c>u[i][j] = u[i-1][j] + u[i][j-1] + u[i-1][j-1]</c> to the next dimension.</summary>
    /// <param name="values">The row, changed in place.</param>
    private static void StepRowForward(Span<uint> values)
    {
        uint carry = 1;
        for (var j = 1; j < values.Length; j++)
        {
            var next = values[j] + values[j - 1] + carry;
            values[j - 1] = carry;
            carry = next;
        }

        values[^1] = carry;
    }

    /// <summary>Moves a row of the recurrence to the previous dimension.</summary>
    /// <param name="values">The row, changed in place; only the first <paramref name="length"/> entries are used.</param>
    /// <param name="length">The entries in use.</param>
    private static void StepRowBack(Span<uint> values, int length)
    {
        uint carry = 0;
        for (var j = 1; j < length; j++)
        {
            var next = values[j] - values[j - 1] - carry;
            values[j - 1] = carry;
            carry = next;
        }

        values[length - 1] = carry;
    }
}
