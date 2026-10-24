// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H265;

/// <summary>Reads short-term reference picture sets, st_ref_pic_set(), including the sets predicted from an earlier set.</summary>
internal static class H265ShortTermRpsReader
{
    /// <summary>The most pictures a set may hold on each side: the largest decoded picture buffer.</summary>
    private const int MaxPictures = 16;

    /// <summary>Reads one set.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="index">The number of the set; sets in a slice header use the count of the sets in the sequence parameter set.</param>
    /// <param name="count">The number of sets in the sequence parameter set.</param>
    /// <param name="sets">The sets read so far from the sequence parameter set.</param>
    /// <returns>The set.</returns>
    /// <exception cref="InvalidDataException">The set is damaged.</exception>
    internal static H265ShortTermRps Read(ref MsbBitReader reader, int index, int count, List<H265ShortTermRps> sets)
    {
        var predicted = index != 0 && reader.ReadBit();
        return predicted ? ReadPredicted(ref reader, index, count, sets) : ReadExplicit(ref reader);
    }

    /// <summary>Reads a set whose distances are coded directly.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The set.</returns>
    /// <exception cref="InvalidDataException">The set is too large.</exception>
    private static H265ShortTermRps ReadExplicit(ref MsbBitReader reader)
    {
        var negative = (int)ExpGolomb.ReadUe(ref reader);
        var positive = (int)ExpGolomb.ReadUe(ref reader);
        if (negative > MaxPictures || positive > MaxPictures)
        {
            throw new InvalidDataException("A short-term reference picture set is too large.");
        }

        var deltaS0 = new int[negative];
        var usedS0 = new bool[negative];
        ReadHalf(ref reader, deltaS0, usedS0, -1);
        var deltaS1 = new int[positive];
        var usedS1 = new bool[positive];
        ReadHalf(ref reader, deltaS1, usedS1, 1);
        return new(deltaS0, usedS0, deltaS1, usedS1);
    }

    /// <summary>Reads the distances of one side of a set; each is coded as the step from the one before.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="deltas">Receives the distances.</param>
    /// <param name="used">Receives the used flags.</param>
    /// <param name="direction">-1 for the earlier pictures, 1 for the later pictures.</param>
    private static void ReadHalf(ref MsbBitReader reader, int[] deltas, bool[] used, int direction)
    {
        var previous = 0;
        for (var i = 0; i < deltas.Length; i++)
        {
            previous += direction * (1 + (int)ExpGolomb.ReadUe(ref reader));
            deltas[i] = previous;
            used[i] = reader.ReadBit();
        }
    }

    /// <summary>Reads a set that is the set it refers to, with every distance moved by one offset.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="index">The number of the set.</param>
    /// <param name="count">The number of sets in the sequence parameter set.</param>
    /// <param name="sets">The sets read so far.</param>
    /// <returns>The set.</returns>
    /// <exception cref="InvalidDataException">The set refers to a set that does not exist.</exception>
    private static H265ShortTermRps ReadPredicted(ref MsbBitReader reader, int index, int count, List<H265ShortTermRps> sets)
    {
        var deltaIdx = index == count ? 1 + (int)ExpGolomb.ReadUe(ref reader) : 1;
        var referenceIndex = index - deltaIdx;
        if (referenceIndex < 0 || referenceIndex >= sets.Count)
        {
            throw new InvalidDataException("A short-term reference picture set refers to a set that does not exist.");
        }

        var negativeSign = reader.ReadBit();
        var absolute = 1 + (int)ExpGolomb.ReadUe(ref reader);
        var deltaRps = negativeSign ? -absolute : absolute;
        var source = sets[referenceIndex];
        var entries = source.Count;
        var used = new bool[entries + 1];
        var useDelta = new bool[entries + 1];
        for (var j = 0; j <= entries; j++)
        {
            used[j] = reader.ReadBit();
            useDelta[j] = used[j] || reader.ReadBit();
        }

        var predictor = new H265RpsPrediction(source, deltaRps, used, useDelta);
        return predictor.Derive();
    }
}
