// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Chooses where a band without pulses folds its spectrum from (RFC 6716 section 4.3.4.4): the entries of the bands below,
/// far enough back that the same content is never repeated inside one band.
/// </summary>
internal static class CeltFolding
{
    /// <summary>The distance from the first band of a frame to the end of the second band's edge.</summary>
    private const int SecondBand = 2;

    /// <summary>Works out the entry a band folds from and which short blocks of the bands it folds from had pulses.</summary>
    /// <param name="frame">The frame, whose first band and spreading are used.</param>
    /// <param name="masks">The collapse masks of the bands read so far, a byte for each band and channel.</param>
    /// <param name="context">What is known about the band.</param>
    /// <returns>The first entry to fold from, or -1 for none, and the masks of the first and second channel.</returns>
    internal static CeltFold Resolve(CeltFrame frame, ReadOnlySpan<byte> masks, in CeltFoldContext context)
    {
        var full = (1U << context.Blocks) - 1;
        var edges = CeltTables.BandEdges;
        var offset = context.LowbandOffset;
        var scale = context.Scale;
        if (offset == 0 || (context.Spread == CeltPvq.SpreadAggressive && context.Blocks <= 1 && context.TimeFrequency >= 0))
        {
            return new(-1, full, full);
        }

        var effective = Math.Max(scale * edges[frame.Start], (scale * edges[offset]) - context.Width);
        var foldStart = offset;
        do
        {
            foldStart--;
        }
        while (scale * edges[foldStart] > effective);

        var foldEnd = offset - 1;
        do
        {
            foldEnd++;
        }
        while (foldEnd < context.Band && scale * edges[foldEnd] < effective + context.Width);

        uint first = 0;
        uint last = 0;
        var channels = context.Channels;
        for (var fold = foldStart; fold < foldEnd; fold++)
        {
            first |= masks[fold * channels];
            last |= masks[(fold * channels) + channels - 1];
        }

        return new(effective, first, last);
    }

    /// <summary>Works out which band the folding should start from, moving it up as long as the bands are wide enough.</summary>
    /// <param name="frame">The frame, whose first band is used.</param>
    /// <param name="progress">What the bands before this one left.</param>
    /// <param name="band">The band.</param>
    /// <param name="layout">The scale of the frame.</param>
    /// <returns>The band to fold from, or zero for none yet.</returns>
    internal static int NextOffset(CeltFrame frame, CeltFoldProgress progress, int band, int layout)
    {
        var edges = CeltTables.BandEdges;
        var width = layout * (edges[band + 1] - edges[band]);
        var wideEnough = (layout * edges[band]) - width >= layout * edges[frame.Start] || band == frame.Start + 1;
        return wideEnough && (progress.UpdateLowband || progress.LowbandOffset == 0) ? band : progress.LowbandOffset;
    }

    /// <summary>
    /// Copies enough of the first band to let the second fold from it. The first band of a hybrid frame is narrower than the second, so
    /// without this the second would fold from entries below the start. For a frame that starts at band zero the bands are equal and nothing is copied.
    /// </summary>
    /// <param name="frame">The frame, whose first band is used.</param>
    /// <param name="scale">The short blocks in the frame, as a power of two.</param>
    /// <param name="norm">The normalised entries of a channel.</param>
    internal static void ExtendFirstBand(CeltFrame frame, int scale, Span<float> norm)
    {
        var edges = CeltTables.BandEdges;
        var first = scale * (edges[frame.Start + 1] - edges[frame.Start]);
        var second = scale * (edges[frame.Start + SecondBand] - edges[frame.Start + 1]);
        var origin = scale * edges[frame.Start];
        norm.Slice(origin + first + first - second, second - first).CopyTo(norm[(origin + first)..]);
    }
}
