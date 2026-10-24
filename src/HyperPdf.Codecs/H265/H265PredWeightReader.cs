// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H265;

/// <summary>Reads the weighted prediction parameters of a slice, pred_weight_table() (7.3.6.3 and 7.4.7.3).</summary>
internal static class H265PredWeightReader
{
    /// <summary>The largest base 2 logarithm of a weight denominator.</summary>
    private const int MaxDenom = 7;

    /// <summary>The half range of an offset when high precision offsets are off.</summary>
    private const int StandardHalfRangeBits = 7;

    /// <summary>The two chroma components.</summary>
    private const int ChromaComponents = 2;

    /// <summary>Reads the table.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="listSizes">The pictures in list 0 and list 1; the second is 0 in a P slice.</param>
    /// <returns>The weights.</returns>
    /// <exception cref="InvalidDataException">A denominator is out of range.</exception>
    internal static H265PredWeights Read(ref MsbBitReader reader, H265Sps sps, ReferenceListCounts listSizes)
    {
        var lumaDenom = (int)ExpGolomb.ReadUe(ref reader);
        var hasChroma = sps.ChromaArrayType != ChromaSampling.Monochrome;
        var chromaDenom = hasChroma ? lumaDenom + ExpGolomb.ReadSe(ref reader) : lumaDenom;
        if (lumaDenom is < 0 or > MaxDenom || chromaDenom is < 0 or > MaxDenom)
        {
            throw new InvalidDataException("A weighted prediction denominator is out of range.");
        }

        var halves = new LumaChroma(HalfRange(sps, sps.BitDepthLuma), HalfRange(sps, sps.BitDepthChroma));
        var denoms = new LumaChroma(lumaDenom, chromaDenom);
        var list0 = ReadList(ref reader, listSizes.L0, hasChroma, denoms, halves);
        var list1 = listSizes.L1 > 0 ? ReadList(ref reader, listSizes.L1, hasChroma, denoms, halves) : null;
        return new(lumaDenom, chromaDenom, list0, list1);
    }

    /// <summary>Gets the half range of the offsets (the WpOffsetHalfRange variables).</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="bitDepth">The bit depth of the component.</param>
    /// <returns>The half range.</returns>
    private static int HalfRange(H265Sps sps, int bitDepth) => 1 << (sps.Range.HighPrecisionOffsets ? bitDepth - 1 : StandardHalfRangeBits);

    /// <summary>Reads the weights of one list.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="count">The pictures in the list.</param>
    /// <param name="hasChroma">Whether chroma weights are present.</param>
    /// <param name="denoms">The weight denominators.</param>
    /// <param name="halves">The half ranges of the offsets.</param>
    /// <returns>The weights.</returns>
    private static H265WeightList ReadList(ref MsbBitReader reader, int count, bool hasChroma, LumaChroma denoms, LumaChroma halves)
    {
        var lumaFlags = new bool[count];
        var chromaFlags = new bool[count];
        for (var i = 0; i < count; i++)
        {
            lumaFlags[i] = reader.ReadBit();
        }

        if (hasChroma)
        {
            for (var i = 0; i < count; i++)
            {
                chromaFlags[i] = reader.ReadBit();
            }
        }

        var lumaWeight = new int[count];
        var lumaOffset = new int[count];
        var chromaWeight = new int[count * ChromaComponents];
        var chromaOffset = new int[count * ChromaComponents];
        for (var i = 0; i < count; i++)
        {
            lumaWeight[i] = 1 << denoms.Luma;
            if (lumaFlags[i])
            {
                lumaWeight[i] += ExpGolomb.ReadSe(ref reader);
                lumaOffset[i] = ExpGolomb.ReadSe(ref reader);
            }

            ReadChroma(ref reader, chromaFlags[i], i * ChromaComponents, denoms.Chroma, halves.Chroma, chromaWeight, chromaOffset);
        }

        return new(lumaWeight, lumaOffset, chromaWeight, chromaOffset);
    }

    /// <summary>Reads the chroma weights of one picture (7-58).</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="present">Whether the picture has chroma weights.</param>
    /// <param name="first">The index of the picture's Cb entry.</param>
    /// <param name="denom">The base 2 logarithm of the chroma denominator.</param>
    /// <param name="half">The half range of the chroma offsets.</param>
    /// <param name="weights">Receives the weights.</param>
    /// <param name="offsets">Receives the offsets.</param>
    private static void ReadChroma(ref MsbBitReader reader, bool present, int first, int denom, int half, int[] weights, int[] offsets)
    {
        for (var j = 0; j < ChromaComponents; j++)
        {
            weights[first + j] = 1 << denom;
            if (!present)
            {
                continue;
            }

            weights[first + j] += ExpGolomb.ReadSe(ref reader);
            var delta = ExpGolomb.ReadSe(ref reader);
            offsets[first + j] = Math.Clamp(half + delta - ((half * weights[first + j]) >> denom), -half, half - 1);
        }
    }
}
