// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// The embedded block coder of ISO 15444-1 annex D: significance propagation, magnitude refinement and cleanup passes over
/// the bit-planes of one code-block, with the bypass, reset, terminate-all, vertically causal and segmentation symbol mode
/// switches. The state is laid out as the decoder's is, four stripe rows side by side, so a column is one 32-bit read.
/// Coefficients are held as sign and magnitude.
/// </summary>
internal sealed class JpxBlockEncoder : IDisposable
{
    /// <summary>The bytes of a pass's segment that may still depend on bits the arithmetic coder has not emitted yet.</summary>
    private const int PendingBytes = 4;

    /// <summary>The byte that starts a marker.</summary>
    private const byte MarkerPrefix = 0xFF;

    /// <summary>The symbols of a segmentation marker, 1010 (D.5).</summary>
    private const int SegmentationSymbol = 0b1010;

    /// <summary>The bits of the segmentation marker.</summary>
    private const int SegmentationBits = 4;

    /// <summary>The sign bit of a sign-and-magnitude coefficient.</summary>
    private const int SignBit = int.MinValue;

    /// <summary>The highest bit-plane a magnitude may use.</summary>
    private const int MaxBits = 30;

    /// <summary>The bits of an int.</summary>
    private const int IntBits = 32;

    /// <summary>The state arrays, shared in layout with the decoder.</summary>
    private readonly JpxBlockState _state;

    /// <summary>The arithmetic and raw coder.</summary>
    private readonly JpxMqEncoder _coder = new();

    /// <summary>Initializes a new instance of the <see cref="JpxBlockEncoder"/> class.</summary>
    /// <param name="scratchPools">The bounded cache.</param>
    internal JpxBlockEncoder(ScratchPools scratchPools) => _state = new(scratchPools);

    /// <inheritdoc/>
    public void Dispose()
    {
        _state.Dispose();
        _coder.Dispose();
    }

    /// <summary>Loads integer coefficients and clears the state.</summary>
    /// <param name="source">The tile-component's coefficients.</param>
    /// <param name="stride">The coefficients between rows of <paramref name="source"/>.</param>
    /// <param name="window">Where the block is.</param>
    /// <returns>The magnitude bit-planes the block needs, from 0 for all zeros.</returns>
    internal int Load(ReadOnlySpan<int> source, int stride, in JpxBlockWindow window)
    {
        Prepare(window);
        var combined = 0;
        for (var y = 0; y < window.Height; y++)
        {
            var row = source.Slice(((window.Y + y) * stride) + window.X, window.Width);
            var index = RowStart(y);
            for (var x = 0; x < row.Length; x++)
            {
                var value = row[x];
                var magnitude = Math.Abs(value);
                combined |= magnitude;
                _state.Values[index] = value < 0 ? magnitude | SignBit : magnitude;
                index += JpxBlockLayout.StripeRows;
            }
        }

        return Bits(combined);
    }

    /// <summary>Loads float coefficients, quantizing each by dividing by the step and dropping the fraction (a dead-zone quantizer, E.1.1), and clears the state.</summary>
    /// <param name="source">The tile-component's coefficients.</param>
    /// <param name="stride">The coefficients between rows of <paramref name="source"/>.</param>
    /// <param name="window">Where the block is.</param>
    /// <param name="inverseStep">One over the quantization step of the sub-band.</param>
    /// <returns>The magnitude bit-planes the block needs, from 0 for all zeros.</returns>
    internal int Load(ReadOnlySpan<float> source, int stride, in JpxBlockWindow window, float inverseStep)
    {
        Prepare(window);
        var combined = 0;
        for (var y = 0; y < window.Height; y++)
        {
            var row = source.Slice(((window.Y + y) * stride) + window.X, window.Width);
            var index = RowStart(y);
            for (var x = 0; x < row.Length; x++)
            {
                var value = row[x];
                var magnitude = (int)Math.Min(MathF.Abs(value) * inverseStep, int.MaxValue);
                combined |= magnitude;
                _state.Values[index] = value < 0 && magnitude != 0 ? magnitude | SignBit : magnitude;
                index += JpxBlockLayout.StripeRows;
            }
        }

        return Bits(combined);
    }

    /// <summary>Codes the loaded block, pass by pass, and appends its segments to a tile's stores.</summary>
    /// <param name="bits">The bit-planes <see cref="Load(ReadOnlySpan{int}, int, in JpxBlockWindow)"/> returned.</param>
    /// <param name="style">The mode switches.</param>
    /// <param name="code">The tile's stores.</param>
    /// <param name="block">The code-block's index in the tile.</param>
    internal void Encode(int bits, JpxBlockStyle style, JpxTileCode code, int block)
    {
        ref var coded = ref code.Blocks[block];
        coded.Bits = bits;
        coded.DataStart = code.Data.Count;
        coded.PassStart = code.PassEnds.Count;
        coded.Passes = bits == 0 ? 0 : JpxPassPlan.Passes(bits);
        _coder.ResetContexts();
        var limit = 0;
        for (var pass = 0; pass < coded.Passes; pass += limit)
        {
            limit = Math.Min(JpxPassPlan.SegmentLimit(style, limit), coded.Passes - pass);
            EncodeSegment(code, new(pass, limit, bits, style));
        }

        coded.DataLength = code.Data.Count - coded.DataStart;
    }

    /// <summary>Gets the bit-planes a combined magnitude needs.</summary>
    /// <param name="combined">The bitwise or of the magnitudes.</param>
    /// <returns>The bit-planes.</returns>
    /// <exception cref="InvalidOperationException">A magnitude is too large to code.</exception>
    private static int Bits(int combined)
    {
        var bits = IntBits - BitOperations.LeadingZeroCount((uint)combined);
        return bits <= MaxBits ? bits : throw new InvalidOperationException("A quantized coefficient is too large to code.");
    }

    /// <summary>
    /// Fits the pass ends to the segment bytes. An end that is not the last must not fall just after a 0xFF byte, as the
    /// packet body would then end in 0xFF and the next byte would look like a marker (B.10.7.1).
    /// </summary>
    /// <param name="ends">The ends of the segment's passes, in bytes from the start of the segment.</param>
    /// <param name="segment">The bytes of the segment.</param>
    /// <param name="length">The length of the segment.</param>
    private static void ClampEnds(Span<int> ends, ReadOnlySpan<byte> segment, int length)
    {
        var floor = 0;
        for (var k = 0; k < ends.Length - 1; k++)
        {
            var end = Math.Min(ends[k], length);
            while (end > floor && segment[end - 1] == MarkerPrefix)
            {
                end--;
            }

            ends[k] = end;
            floor = end;
        }

        ends[^1] = length;
    }

    /// <summary>Gets the index of the first column of a row in the stripe layout.</summary>
    /// <param name="y">The row.</param>
    /// <returns>The index of the sample at column zero, less nothing: the first sample of the row.</returns>
    private int RowStart(int y) =>
        (((y / JpxBlockLayout.StripeRows) + 1) * _state.StripeStride) + JpxBlockLayout.StripeRows + (y % JpxBlockLayout.StripeRows);

    /// <summary>Sizes and clears the state for a block.</summary>
    /// <param name="window">The block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Prepare(in JpxBlockWindow window) =>
        JpxBlockDecoder.Prepare(_state, window.Width, window.Height, window.Orientation, window.Style);

    /// <summary>Codes one codeword segment and appends its bytes and pass lengths.</summary>
    /// <param name="code">The tile's stores.</param>
    /// <param name="segment">The passes of the segment, the block's bit-planes and its mode switches.</param>
    private void EncodeSegment(JpxTileCode code, in SegmentRun segment)
    {
        var raw = JpxPassPlan.IsRaw(segment.Style, segment.First);
        if (raw)
        {
            _coder.StartRaw();
        }
        else
        {
            _coder.Start();
        }

        var firstEnd = code.PassEnds.Count;
        for (var k = 0; k < segment.Count; k++)
        {
            RunPass(segment.First + k, segment.Bits, segment.Style, raw);
            _ = code.PassEnds.Add(raw ? _coder.RawBytes : _coder.EmittedBytes + PendingBytes);
        }

        if (raw)
        {
            _coder.FinishRaw();
        }
        else
        {
            _coder.Finish();
        }

        var length = _coder.Segment.Length;
        var ends = code.PassEnds.AsSpan()[firstEnd..];
        ClampEnds(ends, _coder.Segment, length);
        code.AppendData(_coder.Segment);
    }

    /// <summary>Runs one coding pass and the context reset the mode switches may ask for.</summary>
    /// <param name="pass">The pass, from zero.</param>
    /// <param name="bits">The block's bit-planes.</param>
    /// <param name="style">The mode switches.</param>
    /// <param name="raw">Whether the pass writes raw bits.</param>
    private void RunPass(int pass, int bits, JpxBlockStyle style, bool raw)
    {
        var plane = JpxPassPlan.Plane(pass, bits);
        switch (JpxPassPlan.Type(pass))
        {
            case JpxPassPlan.SignificancePass:
            {
                Significance(plane, raw);
                break;
            }

            case JpxPassPlan.RefinementPass:
            {
                Refinement(plane, raw);
                break;
            }

            default:
            {
                Cleanup(plane, (style & JpxBlockStyle.SegmentationSymbols) != 0);
                break;
            }
        }

        if (!raw && (style & JpxBlockStyle.Reset) != 0)
        {
            _coder.ResetContexts();
        }
    }

    /// <summary>Runs a significance propagation pass.</summary>
    /// <param name="plane">The bit-plane.</param>
    /// <param name="raw">Whether the pass writes raw bits.</param>
    private void Significance(int plane, bool raw)
    {
        for (var stripe = 0; stripe < _state.Stripes; stripe++)
        {
            var rows = Math.Min(JpxBlockLayout.StripeRows, _state.Height - (stripe * JpxBlockLayout.StripeRows));
            var index = ((stripe + 1) * _state.StripeStride) + JpxBlockLayout.StripeRows;
            for (var x = 0; x < _state.Width; x++)
            {
                // A column with no significant neighbours has nothing to code in this pass.
                if (JpxBlockLayout.ReadColumn(_state.Neighbours, index) != 0)
                {
                    SignificanceColumn(index, rows, plane, raw);
                }

                index += JpxBlockLayout.StripeRows;
            }
        }
    }

    /// <summary>Runs the significance pass over one stripe column.</summary>
    /// <param name="index">The column's first coefficient.</param>
    /// <param name="rows">The rows of the stripe.</param>
    /// <param name="plane">The bit-plane.</param>
    /// <param name="raw">Whether the pass writes raw bits.</param>
    private void SignificanceColumn(int index, int rows, int plane, bool raw)
    {
        for (var row = 0; row < rows; row++)
        {
            var i = index + row;
            var flags = _state.Flags[i];
            var neighbours = _state.Neighbours[i];
            if ((flags & JpxBlockLayout.Significant) != 0 || neighbours == 0)
            {
                continue;
            }

            var bit = (_state.Values[i] >> plane) & 1;
            if (raw)
            {
                _coder.EncodeRaw(bit);
            }
            else
            {
                _coder.Encode(bit, JpxContexts.ZeroCoding[_state.ZeroTable + neighbours]);
            }

            if (bit == 0)
            {
                _state.Flags[i] = (byte)(flags | JpxBlockPasses.Visited);
                continue;
            }

            EncodeSign(i, row, raw);
            MakeSignificant(i, row);
        }
    }

    /// <summary>Runs a magnitude refinement pass.</summary>
    /// <param name="plane">The bit-plane.</param>
    /// <param name="raw">Whether the pass writes raw bits.</param>
    private void Refinement(int plane, bool raw)
    {
        for (var stripe = 0; stripe < _state.Stripes; stripe++)
        {
            var rows = Math.Min(JpxBlockLayout.StripeRows, _state.Height - (stripe * JpxBlockLayout.StripeRows));
            var index = ((stripe + 1) * _state.StripeStride) + JpxBlockLayout.StripeRows;
            for (var x = 0; x < _state.Width; x++)
            {
                if ((JpxBlockLayout.ReadColumn(_state.Flags, index) & JpxBlockLayout.ColumnSignificant) != 0)
                {
                    RefinementColumn(index, rows, plane, raw);
                }

                index += JpxBlockLayout.StripeRows;
            }
        }
    }

    /// <summary>Runs the refinement pass over one stripe column.</summary>
    /// <param name="index">The column's first coefficient.</param>
    /// <param name="rows">The rows of the stripe.</param>
    /// <param name="plane">The bit-plane.</param>
    /// <param name="raw">Whether the pass writes raw bits.</param>
    private void RefinementColumn(int index, int rows, int plane, bool raw)
    {
        for (var row = 0; row < rows; row++)
        {
            var i = index + row;
            var flags = _state.Flags[i];
            if ((flags & (JpxBlockLayout.Significant | JpxBlockPasses.Visited)) != JpxBlockLayout.Significant)
            {
                continue;
            }

            var bit = (_state.Values[i] >> plane) & 1;
            if (raw)
            {
                _coder.EncodeRaw(bit);
            }
            else
            {
                _coder.Encode(bit, JpxBlockPasses.RefinementContext(flags, _state.Neighbours[i]));
            }

            _state.Flags[i] = (byte)(flags | JpxBlockPasses.Refined);
        }
    }

    /// <summary>Runs a cleanup pass, then writes the segmentation symbol when the mode asks for one.</summary>
    /// <param name="plane">The bit-plane.</param>
    /// <param name="segmentation">Whether a segmentation symbol follows.</param>
    private void Cleanup(int plane, bool segmentation)
    {
        for (var stripe = 0; stripe < _state.Stripes; stripe++)
        {
            var rows = Math.Min(JpxBlockLayout.StripeRows, _state.Height - (stripe * JpxBlockLayout.StripeRows));
            var index = ((stripe + 1) * _state.StripeStride) + JpxBlockLayout.StripeRows;
            for (var x = 0; x < _state.Width; x++)
            {
                CleanupColumn(index, rows, plane);
                index += JpxBlockLayout.StripeRows;
            }
        }

        if (!segmentation)
        {
            return;
        }

        for (var i = SegmentationBits - 1; i >= 0; i--)
        {
            _coder.Encode((SegmentationSymbol >> i) & 1, JpxContexts.Uniform);
        }
    }

    /// <summary>Runs the cleanup pass over one stripe column, using run-length coding when the whole column is quiet.</summary>
    /// <param name="index">The column's first coefficient.</param>
    /// <param name="rows">The rows of the stripe.</param>
    /// <param name="plane">The bit-plane.</param>
    private void CleanupColumn(int index, int rows, int plane)
    {
        var start = 0;
        if (rows == JpxBlockLayout.StripeRows && JpxBlockLayout.ReadColumn(_state.Flags, index) == 0 && JpxBlockLayout.ReadColumn(_state.Neighbours, index) == 0)
        {
            var run = FirstSetRow(index, plane);
            _coder.Encode(run < JpxBlockLayout.StripeRows ? 1 : 0, JpxContexts.RunLength);
            if (run == JpxBlockLayout.StripeRows)
            {
                return;
            }

            _coder.Encode(run >> 1, JpxContexts.Uniform);
            _coder.Encode(run & 1, JpxContexts.Uniform);
            EncodeSign(index + run, run, false);
            MakeSignificant(index + run, run);
            start = run + 1;
        }

        for (var row = start; row < rows; row++)
        {
            var i = index + row;
            if ((_state.Flags[i] & (JpxBlockLayout.Significant | JpxBlockPasses.Visited)) != 0)
            {
                continue;
            }

            var bit = (_state.Values[i] >> plane) & 1;
            _coder.Encode(bit, JpxContexts.ZeroCoding[_state.ZeroTable + _state.Neighbours[i]]);
            if (bit == 0)
            {
                continue;
            }

            EncodeSign(i, row, false);
            MakeSignificant(i, row);
        }

        JpxBlockLayout.WriteColumn(_state.Flags, index, JpxBlockLayout.ReadColumn(_state.Flags, index) & JpxBlockPasses.ColumnNotVisited);
    }

    /// <summary>Finds the first row of a stripe column with a one in a bit-plane.</summary>
    /// <param name="index">The column's first coefficient.</param>
    /// <param name="plane">The bit-plane.</param>
    /// <returns>The row, or the rows of a stripe when none has a one.</returns>
    private int FirstSetRow(int index, int plane)
    {
        var row = 0;
        while (row < JpxBlockLayout.StripeRows && ((_state.Values[index + row] >> plane) & 1) == 0)
        {
            row++;
        }

        return row;
    }

    /// <summary>Codes the sign of a coefficient that has just become significant (D.3.2), or writes it raw.</summary>
    /// <param name="i">The coefficient.</param>
    /// <param name="row">The row within the stripe.</param>
    /// <param name="raw">Whether the pass writes raw bits.</param>
    private void EncodeSign(int i, int row, bool raw)
    {
        var negative = _state.Values[i] < 0 ? 1 : 0;
        if (raw)
        {
            _coder.EncodeRaw(negative);
            return;
        }

        var horizontal = Math.Clamp(JpxBlockPasses.Contribution(_state.Flags[i - JpxBlockLayout.StripeRows]) + JpxBlockPasses.Contribution(_state.Flags[i + JpxBlockLayout.StripeRows]), -1, 1);
        var below = _state.Causal && row == JpxBlockLayout.LastRow ? 0 : JpxBlockPasses.Contribution(_state.Flags[JpxBlockLayout.Below(_state, i, row)]);
        var vertical = Math.Clamp(JpxBlockPasses.Contribution(_state.Flags[JpxBlockLayout.Above(_state, i, row)]) + below, -1, 1);
        var entry = JpxContexts.Sign[((horizontal + 1) * JpxBlockPasses.SignRow) + vertical + 1];
        _coder.Encode(negative ^ (entry >> JpxBlockPasses.SignFlipShift), entry & JpxContexts.SignContextMask);
    }

    /// <summary>Makes a coefficient significant and tells its neighbours.</summary>
    /// <param name="i">The coefficient.</param>
    /// <param name="row">The row within the stripe.</param>
    private void MakeSignificant(int i, int row)
    {
        var negative = _state.Values[i] < 0;
        _state.Flags[i] = (byte)(_state.Flags[i] | JpxBlockLayout.Significant | JpxBlockPasses.Visited | (negative ? JpxBlockPasses.Negative : 0));
        _state.Neighbours[i - JpxBlockLayout.StripeRows] += JpxContexts.Horizontal;
        _state.Neighbours[i + JpxBlockLayout.StripeRows] += JpxContexts.Horizontal;

        // In the vertically causal mode the stripe above never sees this stripe.
        if (!_state.Causal || row != 0)
        {
            var above = JpxBlockLayout.Above(_state, i, row);
            _state.Neighbours[above] += JpxContexts.Vertical;
            _state.Neighbours[above - JpxBlockLayout.StripeRows] += JpxContexts.Diagonal;
            _state.Neighbours[above + JpxBlockLayout.StripeRows] += JpxContexts.Diagonal;
        }

        var below = JpxBlockLayout.Below(_state, i, row);
        _state.Neighbours[below] += JpxContexts.Vertical;
        _state.Neighbours[below - JpxBlockLayout.StripeRows] += JpxContexts.Diagonal;
        _state.Neighbours[below + JpxBlockLayout.StripeRows] += JpxContexts.Diagonal;
    }

    /// <summary>The passes of one codeword segment of a block.</summary>
    /// <param name="First">The first pass.</param>
    /// <param name="Count">The passes in the segment.</param>
    /// <param name="Bits">The block's bit-planes.</param>
    /// <param name="Style">The mode switches.</param>
    private readonly record struct SegmentRun(int First, int Count, int Bits, JpxBlockStyle Style);
}
