// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Works out a predicted short-term reference picture set from the set it refers to (equations 7-61 and 7-62).</summary>
/// <param name="Source">The set that is predicted from.</param>
/// <param name="DeltaRps">The offset added to every distance of the source.</param>
/// <param name="Used">The used_by_curr_pic_flag of each source entry, then of the offset itself.</param>
/// <param name="UseDelta">The use_delta_flag of each source entry, then of the offset itself.</param>
internal readonly record struct H265RpsPrediction(H265ShortTermRps Source, int DeltaRps, bool[] Used, bool[] UseDelta)
{
    /// <summary>Builds the predicted set.</summary>
    /// <returns>The set.</returns>
    internal H265ShortTermRps Derive()
    {
        var negative = new List<int>();
        var negativeUsed = new List<bool>();
        var positive = new List<int>();
        var positiveUsed = new List<bool>();
        var earlier = Source.DeltaPocS0.Length;
        var later = Source.DeltaPocS1.Length;
        var self = earlier + later;

        for (var j = later - 1; j >= 0; j--)
        {
            Add(Source.DeltaPocS1[j] + DeltaRps, earlier + j, negative, negativeUsed, positive, positiveUsed, true);
        }

        Add(DeltaRps, self, negative, negativeUsed, positive, positiveUsed, true);
        for (var j = 0; j < earlier; j++)
        {
            Add(Source.DeltaPocS0[j] + DeltaRps, j, negative, negativeUsed, positive, positiveUsed, true);
        }

        for (var j = earlier - 1; j >= 0; j--)
        {
            Add(Source.DeltaPocS0[j] + DeltaRps, j, negative, negativeUsed, positive, positiveUsed, false);
        }

        Add(DeltaRps, self, negative, negativeUsed, positive, positiveUsed, false);
        for (var j = 0; j < later; j++)
        {
            Add(Source.DeltaPocS1[j] + DeltaRps, earlier + j, negative, negativeUsed, positive, positiveUsed, false);
        }

        return new([.. negative], [.. negativeUsed], [.. positive], [.. positiveUsed]);
    }

    /// <summary>Adds an entry to one side when it has the right sign and is kept.</summary>
    /// <param name="distance">The entry's distance after the offset.</param>
    /// <param name="flagIndex">The index of the entry's flags.</param>
    /// <param name="negative">The earlier pictures.</param>
    /// <param name="negativeUsed">The used flags of the earlier pictures.</param>
    /// <param name="positive">The later pictures.</param>
    /// <param name="positiveUsed">The used flags of the later pictures.</param>
    /// <param name="forNegative"><see langword="true"/> to add to the earlier pictures, <see langword="false"/> to add to the later pictures.</param>
    private void Add(int distance, int flagIndex, List<int> negative, List<bool> negativeUsed, List<int> positive, List<bool> positiveUsed, bool forNegative)
    {
        if (!UseDelta[flagIndex])
        {
            return;
        }

        if (forNegative && distance < 0)
        {
            negative.Add(distance);
            negativeUsed.Add(Used[flagIndex]);
        }
        else if (!forNegative && distance > 0)
        {
            positive.Add(distance);
            positiveUsed.Add(Used[flagIndex]);
        }
    }
}
