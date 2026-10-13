// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>Runs significance, refinement and cleanup passes for ordinary JPEG 2000 blocks.</summary>
internal static class JpxBlockPasses
{
    /// <summary>The flag of a coefficient coded in the current bit-plane's significance pass.</summary>
    internal const byte Visited = 2;

    /// <summary>The flag of a coefficient refined at least once.</summary>
    internal const byte Refined = 4;

    /// <summary>The flag of a negative coefficient.</summary>
    internal const byte Negative = 8;

    /// <summary>The shift that turns <see cref="Negative"/> into two.</summary>
    internal const int NegativeShift = 2;

    /// <summary>The visited flags of a whole stripe column, inverted to clear them.</summary>
    internal const uint ColumnNotVisited = ~0x02020202U;

    /// <summary>The symbols of a segmentation marker.</summary>
    internal const int SegmentationSymbols = 4;

    /// <summary>The sign-table entries per horizontal contribution.</summary>
    internal const int SignRow = 3;

    /// <summary>The shift of the sign-flip bit in a sign-table entry.</summary>
    internal const int SignFlipShift = 7;

    /// <summary>Gets a coefficient's sign contribution: 1 when positive and significant, -1 when negative, else 0.</summary>
    /// <param name="flags">The neighbour's flags.</param>
    /// <returns>The contribution.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Contribution(byte flags) => (flags & JpxBlockLayout.Significant) == 0 ? 0 : 1 - ((flags & Negative) >> NegativeShift);

    /// <summary>Gets the magnitude refinement context (table D.4).</summary>
    /// <param name="flags">The coefficient's flags.</param>
    /// <param name="neighbours">The coefficient's neighbourhood code.</param>
    /// <returns>The context.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RefinementContext(byte flags, byte neighbours)
    {
        if ((flags & Refined) != 0)
        {
            return JpxContexts.LaterRefinement;
        }

        return neighbours != 0 ? JpxContexts.FirstRefinementNear : JpxContexts.FirstRefinement;
    }

    /// <summary>Runs a significance propagation pass.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="coder">The decoder.</param>
    /// <param name="plane">The bit-plane plus one.</param>
    /// <param name="raw">Whether the pass is raw bypass data.</param>
    internal static void Significance(JpxBlockState state, ref JpxMqDecoder coder, int plane, bool raw)
    {
        var value = (1 << plane) | (1 << (plane - 1));
        for (var stripe = 0; stripe < state.Stripes; stripe++)
        {
            var rows = Math.Min(JpxBlockLayout.StripeRows, state.Height - (stripe * JpxBlockLayout.StripeRows));
            var index = ((stripe + 1) * state.StripeStride) + JpxBlockLayout.StripeRows;
            for (var x = 0; x < state.Width; x++)
            {
                // A column with no significant neighbours has nothing to code in this pass.
                if (JpxBlockLayout.ReadColumn(state.Neighbours, index) != 0)
                {
                    SignificanceColumn(state, ref coder, index, rows, value, raw);
                }

                index += JpxBlockLayout.StripeRows;
            }
        }
    }

    /// <summary>Runs the significance pass over one stripe column.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="coder">The decoder.</param>
    /// <param name="index">The column's first coefficient.</param>
    /// <param name="rows">The rows of the stripe.</param>
    /// <param name="value">The value a newly significant coefficient takes.</param>
    /// <param name="raw">Whether the pass is raw bypass data.</param>
    internal static void SignificanceColumn(JpxBlockState state, ref JpxMqDecoder coder, int index, int rows, int value, bool raw)
    {
        for (var row = 0; row < rows; row++)
        {
            var i = index + row;
            var flags = state.Flags[i];
            var neighbours = state.Neighbours[i];
            if ((flags & JpxBlockLayout.Significant) != 0 || neighbours == 0)
            {
                continue;
            }

            var bit = raw ? coder.DecodeRaw() : coder.Decode(ref state.Contexts[JpxContexts.ZeroCoding[state.ZeroTable + neighbours]]);
            if (bit == 0)
            {
                state.Flags[i] = (byte)(flags | Visited);
            }
            else
            {
                var negative = raw ? coder.DecodeRaw() != 0 : DecodeSign(state, ref coder, i, row);
                MakeSignificant(state, i, row, value, negative);
            }
        }
    }

    /// <summary>Runs a magnitude refinement pass.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="coder">The decoder.</param>
    /// <param name="plane">The bit-plane plus one.</param>
    /// <param name="raw">Whether the pass is raw bypass data.</param>
    internal static void Refinement(JpxBlockState state, ref JpxMqDecoder coder, int plane, bool raw)
    {
        var half = 1 << (plane - 1);
        for (var stripe = 0; stripe < state.Stripes; stripe++)
        {
            var rows = Math.Min(JpxBlockLayout.StripeRows, state.Height - (stripe * JpxBlockLayout.StripeRows));
            var index = ((stripe + 1) * state.StripeStride) + JpxBlockLayout.StripeRows;
            for (var x = 0; x < state.Width; x++)
            {
                if ((JpxBlockLayout.ReadColumn(state.Flags, index) & JpxBlockLayout.ColumnSignificant) != 0)
                {
                    RefinementColumn(state, ref coder, index, rows, half, raw);
                }

                index += JpxBlockLayout.StripeRows;
            }
        }
    }

    /// <summary>Runs the refinement pass over one stripe column.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="coder">The decoder.</param>
    /// <param name="index">The column's first coefficient.</param>
    /// <param name="rows">The rows of the stripe.</param>
    /// <param name="half">Half the bit-plane's weight, added or taken away.</param>
    /// <param name="raw">Whether the pass is raw bypass data.</param>
    internal static void RefinementColumn(JpxBlockState state, ref JpxMqDecoder coder, int index, int rows, int half, bool raw)
    {
        for (var row = 0; row < rows; row++)
        {
            var i = index + row;
            var flags = state.Flags[i];
            if ((flags & (JpxBlockLayout.Significant | Visited)) != JpxBlockLayout.Significant)
            {
                continue;
            }

            var bit = raw ? coder.DecodeRaw() : coder.Decode(ref state.Contexts[RefinementContext(flags, state.Neighbours[i])]);
            var current = state.Values[i];
            state.Values[i] = current + ((bit ^ (current < 0 ? 1 : 0)) != 0 ? half : -half);
            state.Flags[i] = (byte)(flags | Refined);
        }
    }

    /// <summary>Runs a cleanup pass, then reads the segmentation symbol when the mode asks for one.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="coder">The decoder.</param>
    /// <param name="plane">The bit-plane plus one.</param>
    /// <param name="segmentation">Whether a segmentation symbol follows.</param>
    internal static void Cleanup(JpxBlockState state, ref JpxMqDecoder coder, int plane, bool segmentation)
    {
        var value = (1 << plane) | (1 << (plane - 1));
        for (var stripe = 0; stripe < state.Stripes; stripe++)
        {
            var rows = Math.Min(JpxBlockLayout.StripeRows, state.Height - (stripe * JpxBlockLayout.StripeRows));
            var index = ((stripe + 1) * state.StripeStride) + JpxBlockLayout.StripeRows;
            for (var x = 0; x < state.Width; x++)
            {
                CleanupColumn(state, ref coder, index, rows, value);
                index += JpxBlockLayout.StripeRows;
            }
        }

        if (!segmentation)
        {
            return;
        }

        // The segmentation symbol should be 1010 (annex D.5). Checking it is optional error detection, and we read the symbol without failing so damaged blocks still show what decoded.
        for (var i = 0; i < SegmentationSymbols; i++)
        {
            _ = coder.Decode(ref state.Contexts[JpxContexts.Uniform]);
        }
    }

    /// <summary>Runs the cleanup pass over one stripe column, using run-length coding when the whole column is quiet.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="coder">The decoder.</param>
    /// <param name="index">The column's first coefficient.</param>
    /// <param name="rows">The rows of the stripe.</param>
    /// <param name="value">The value a newly significant coefficient takes.</param>
    internal static void CleanupColumn(JpxBlockState state, ref JpxMqDecoder coder, int index, int rows, int value)
    {
        var start = 0;
        if (rows == JpxBlockLayout.StripeRows && JpxBlockLayout.ReadColumn(state.Flags, index) == 0 && JpxBlockLayout.ReadColumn(state.Neighbours, index) == 0)
        {
            if (coder.Decode(ref state.Contexts[JpxContexts.RunLength]) == 0)
            {
                return;
            }

            start = coder.Decode(ref state.Contexts[JpxContexts.Uniform]) << 1;
            start |= coder.Decode(ref state.Contexts[JpxContexts.Uniform]);
            MakeSignificant(state, index + start, start, value, DecodeSign(state, ref coder, index + start, start));
            start++;
        }

        for (var row = start; row < rows; row++)
        {
            var i = index + row;
            if ((state.Flags[i] & (JpxBlockLayout.Significant | Visited)) == 0
                && coder.Decode(ref state.Contexts[JpxContexts.ZeroCoding[state.ZeroTable + state.Neighbours[i]]]) != 0)
            {
                MakeSignificant(state, i, row, value, DecodeSign(state, ref coder, i, row));
            }
        }

        JpxBlockLayout.WriteColumn(state.Flags, index, JpxBlockLayout.ReadColumn(state.Flags, index) & ColumnNotVisited);
    }

    /// <summary>Decodes the sign of a coefficient that has just become significant (D.3.2).</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="coder">The decoder.</param>
    /// <param name="i">The coefficient.</param>
    /// <param name="row">The row within the stripe.</param>
    /// <returns><see langword="true"/> when negative.</returns>
    internal static bool DecodeSign(JpxBlockState state, ref JpxMqDecoder coder, int i, int row)
    {
        var horizontal = Math.Clamp(Contribution(state.Flags[i - JpxBlockLayout.StripeRows]) + Contribution(state.Flags[i + JpxBlockLayout.StripeRows]), -1, 1);
        var below = state.Causal && row == JpxBlockLayout.LastRow ? 0 : Contribution(state.Flags[JpxBlockLayout.Below(state, i, row)]);
        var vertical = Math.Clamp(Contribution(state.Flags[JpxBlockLayout.Above(state, i, row)]) + below, -1, 1);
        var entry = JpxContexts.Sign[((horizontal + 1) * SignRow) + vertical + 1];
        var bit = coder.Decode(ref state.Contexts[entry & JpxContexts.SignContextMask]);
        return (bit ^ (entry >> SignFlipShift)) != 0;
    }

    /// <summary>Makes a coefficient significant and tells its neighbours.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="i">The coefficient.</param>
    /// <param name="row">The row within the stripe.</param>
    /// <param name="value">The magnitude it takes, at twice scale.</param>
    /// <param name="negative">Whether it is negative.</param>
    internal static void MakeSignificant(JpxBlockState state, int i, int row, int value, bool negative)
    {
        state.Values[i] = negative ? -value : value;
        state.Flags[i] = (byte)(state.Flags[i] | JpxBlockLayout.Significant | Visited | (negative ? Negative : 0));
        state.Neighbours[i - JpxBlockLayout.StripeRows] += JpxContexts.Horizontal;
        state.Neighbours[i + JpxBlockLayout.StripeRows] += JpxContexts.Horizontal;

        // In the vertically causal mode the stripe above never sees this stripe.
        if (!state.Causal || row != 0)
        {
            var above = JpxBlockLayout.Above(state, i, row);
            state.Neighbours[above] += JpxContexts.Vertical;
            state.Neighbours[above - JpxBlockLayout.StripeRows] += JpxContexts.Diagonal;
            state.Neighbours[above + JpxBlockLayout.StripeRows] += JpxContexts.Diagonal;
        }

        var below = JpxBlockLayout.Below(state, i, row);
        state.Neighbours[below] += JpxContexts.Vertical;
        state.Neighbours[below - JpxBlockLayout.StripeRows] += JpxContexts.Diagonal;
        state.Neighbours[below + JpxBlockLayout.StripeRows] += JpxContexts.Diagonal;
    }
}
