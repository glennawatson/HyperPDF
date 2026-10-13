// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>Decodes high-throughput blocks and converts their sign-magnitude coefficients.</summary>
internal static class JpxHtBlockDecoder
{
    /// <summary>The most magnitude bit-planes the 32-bit sign-magnitude samples hold.</summary>
    internal const int MaxHtMagnitude = 30;

    /// <summary>The widest code-block (ISO 15444-1 A.6.1).</summary>
    internal const int MaxBlockWidth = 1024;

    /// <summary>The bytes of a line of exponents: one per column, a zero column each side, and room for the far neighbour.</summary>
    internal const int LineBytes = MaxBlockWidth + JpxBlockLayout.StripeRows;

    /// <summary>The bytes at the end of a cleanup segment that hold Scup, and the smallest Scup.</summary>
    internal const int SuffixLengthBytes = 2;

    /// <summary>The largest MEL and VLC length, Scup.</summary>
    internal const int MaxSuffixLength = 4079;

    /// <summary>The shift of the high bits of Scup, held in the segment's last byte.</summary>
    internal const int SuffixHighShift = 4;

    /// <summary>The low nibble of the segment's second-last byte, which holds the low bits of Scup.</summary>
    internal const int SuffixLowMask = 0x0F;

    /// <summary>The passes of an HT set with both refinement passes.</summary>
    internal const int FullHtSet = 3;

    /// <summary>The fewest bit-planes that leave room for the refinement plane.</summary>
    internal const int RefinablePlanes = 2;

    /// <summary>The fill byte of the MagSgn stream.</summary>
    internal const byte MagSgnFill = 0xFF;

    /// <summary>The magnitude bits of a sign-magnitude sample.</summary>
    internal const int MagnitudeMask = int.MaxValue;

    /// <summary>Counts the passes of the first HT set that can be decoded.</summary>
    /// <param name="cleanup">The cleanup segment.</param>
    /// <param name="refinement">The refinement segment, or an empty one.</param>
    /// <param name="planes">The code-block's bit-planes.</param>
    /// <returns>1 for the cleanup pass alone, 2 with SigProp, 3 with SigProp and MagRef.</returns>
    internal static int HtPasses(JpxSegment cleanup, JpxSegment refinement, int planes)
    {
        var passes = Math.Min(cleanup.Passes + refinement.Passes, FullHtSet);

        // Our rule: refinement passes (T.814 clause 7.3.5 to 7.3.7) with no bytes, or with no bit-plane left below the cleanup pass, have nothing to refine and are dropped.
        return refinement.Length == 0 || planes < RefinablePlanes ? Math.Min(passes, 1) : passes;
    }

    /// <summary>Decodes one high-throughput code-block into its tile-component buffer.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="tile">The tile, which holds the code-block's segments and chunks.</param>
    /// <param name="block">The code-block.</param>
    /// <param name="band">The code-block's sub-band.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="target">Where the coefficients go.</param>
    internal static void DecodeHighThroughput(JpxBlockState state, JpxTile tile, JpxCodeBlock block, in JpxBandLayout band, byte[] data, in JpxBlockTarget target)
    {
        if (target.RoiShift != 0 || band.Magnitude > MaxHtMagnitude || block.Area.Width > MaxBlockWidth)
        {
            return;
        }

        var cleanup = tile.Segments[block.FirstSegment];
        var refinement = cleanup.Next >= 0 ? tile.Segments[cleanup.Next] : default;
        var passes = HtPasses(cleanup, refinement, block.BitPlanes);
        var refinementLength = passes > 1 ? refinement.Length : 0;
        if (cleanup.Length < SuffixLengthBytes || cleanup.Length + refinementLength > block.DataLength)
        {
            return;
        }

        JpxBlockDecoder.Prepare(state, block.Area.Width, block.Area.Height, band.Orientation, target.Style);
        var source = JpxBlockDecoder.Gather(state, tile, block, data, out var offset);
        var bytes = source.AsSpan(offset, cleanup.Length + refinementLength);
        if (HtCleanup(state, bytes[..cleanup.Length], block.BitPlanes, band.Magnitude) && passes > 1)
        {
            HtRefine(state, bytes[cleanup.Length..], passes, block.BitPlanes);
        }

        ToSigned(state);
        JpxBlockDecoder.Store(state, block, band, target);
    }

    /// <summary>Runs the refinement passes.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The refinement segment.</param>
    /// <param name="passes">The passes of the HT set, 2 or 3.</param>
    /// <param name="planes">The code-block's bit-planes.</param>
    internal static void HtRefine(JpxBlockState state, ReadOnlySpan<byte> segment, int passes, int planes)
    {
        if (passes == FullHtSet)
        {
            JpxHtRefinement.HtMagRef(state, segment, planes);
        }

        JpxHtRefinement.HtSigProp(state, segment, planes);
    }

    /// <summary>Turns the sign-magnitude samples into signed values.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    internal static void ToSigned(JpxBlockState state)
    {
        foreach (ref var value in state.Values.AsSpan(0, state.Length))
        {
            if (value < 0)
            {
                value = -(value & MagnitudeMask);
            }
        }
    }

    /// <summary>Reads Scup and runs the cleanup pass.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The cleanup segment.</param>
    /// <param name="planes">The code-block's bit-planes, p.</param>
    /// <param name="magnitude">The sub-band's magnitude bit-planes, Mb.</param>
    /// <returns><see langword="false"/> when the segment is malformed.</returns>
    internal static bool HtCleanup(JpxBlockState state, ReadOnlySpan<byte> segment, int planes, int magnitude)
    {
        var suffixLength = (segment[^1] << SuffixHighShift) | (segment[^SuffixLengthBytes] & SuffixLowMask);
        if (suffixLength < SuffixLengthBytes || suffixLength > segment.Length || suffixLength > MaxSuffixLength)
        {
            return false;
        }

        var streams = new JpxHtStreams(
            new(segment, suffixLength),
            JpxHtReverseReader.ForVlc(segment, suffixLength),
            new(segment[..^suffixLength], MagSgnFill));

        // Two lines of exponents, used in turn: the row above each row of quads, and that row's own lower samples.
        Span<byte> lines = stackalloc byte[LineBytes * SuffixLengthBytes];

        // A quad's exponent bound U must stay within the bit-planes the block carries (T.814 clause 7.3.4); a larger one means damaged data and is rejected, with the margin of two our own.
        var depth = new JpxHtDepth(planes, magnitude + SuffixLengthBytes - planes);
        var current = 0;
        for (var y = 0; y < state.Height; y += JpxBlockLayout.QuadWidth)
        {
            var above = lines.Slice((1 - current) * LineBytes, LineBytes);
            var below = lines.Slice(current * LineBytes, LineBytes);
            below.Clear();
            if (!JpxHtCleanup.HtQuadRow(state, ref streams, above, below, y, depth))
            {
                return false;
            }

            current = 1 - current;
        }

        return true;
    }
}
