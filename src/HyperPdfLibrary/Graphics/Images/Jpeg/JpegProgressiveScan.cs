// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// Codes the blocks of a progressive scan (ISO/IEC 10918-1, G.1.2): the DC first and refinement passes, and the first and
/// refinement passes over a band of AC coefficients with their end-of-band runs.
/// </summary>
internal static class JpegProgressiveScan
{
    /// <summary>The bytes that hold correction bits waiting for an end-of-band run to end.</summary>
    internal const int CorrectionCapacity = 1024;

    /// <summary>The longest end-of-band run one symbol can carry.</summary>
    private const int MaxRun = 0x7FFF;

    /// <summary>The units between checks of the cancellation token (1,024).</summary>
    private const int CancelCheckMask = 1023;

    /// <summary>The most correction bits one block adds.</summary>
    private const int BlockBits = JpegBlock.Length - 1;

    /// <summary>The fullest the correction buffer may be before a run is written out.</summary>
    private const int FlushLevel = CorrectionCapacity - BlockBits;

    /// <summary>Codes every block of a scan.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="components">The frame's components.</param>
    /// <param name="spec">The scan.</param>
    /// <param name="geometry">The frame layout.</param>
    /// <param name="restartInterval">The MCUs between restart markers, or zero.</param>
    /// <param name="correctionBits">Space for the correction bits that wait for a run to end.</param>
    /// <param name="cancellationToken">Cancels the scan between blocks.</param>
    internal static void Encode<TSink>(
        ref TSink sink,
        ReadOnlySpan<JpegEncoderComponent> components,
        in JpegScanSpec spec,
        in JpegFrameGeometry geometry,
        int restartInterval,
        Span<byte> correctionBits,
        CancellationToken cancellationToken)
        where TSink : IJpegEntropySink, allows ref struct
    {
        Span<int> predictors = stackalloc int[JpegEncoderState.MaxComponents];
        var run = new JpegBlockRun(correctionBits);
        var single = BitOperations.PopCount((uint)spec.Components) == 1 ? components[BitOperations.TrailingZeroCount(spec.Components)] : null;
        var total = single is null ? geometry.McusPerLine * geometry.McusPerColumn : single.UsedBlocksPerLine * single.UsedBlocksPerColumn;
        var restartIndex = 0;
        var untilRestart = restartInterval;
        for (var unit = 0; unit < total; unit++)
        {
            if ((unit & CancelCheckMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (restartInterval != 0 && untilRestart == 0)
            {
                FlushRun(ref sink, single, ref run);
                sink.Restart(restartIndex);
                restartIndex++;
                predictors.Clear();
                untilRestart = restartInterval;
            }

            if (single is not null)
            {
                EncodeBlock(ref sink, single, single.GetBlock(unit / single.UsedBlocksPerLine, unit % single.UsedBlocksPerLine), in spec, ref predictors[0], ref run);
            }
            else
            {
                EncodeUnit(ref sink, components, in spec, geometry, unit, predictors, ref run);
            }

            untilRestart--;
        }

        FlushRun(ref sink, single, ref run);
        sink.EndScan();
    }

    /// <summary>Codes one interleaved MCU of a DC scan.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="components">The frame's components.</param>
    /// <param name="spec">The scan.</param>
    /// <param name="geometry">The frame layout.</param>
    /// <param name="unit">The MCU number.</param>
    /// <param name="predictors">The DC predictor of each component.</param>
    /// <param name="run">The end-of-band state, unused by DC scans.</param>
    private static void EncodeUnit<TSink>(
        ref TSink sink,
        ReadOnlySpan<JpegEncoderComponent> components,
        in JpegScanSpec spec,
        in JpegFrameGeometry geometry,
        int unit,
        scoped Span<int> predictors,
        ref JpegBlockRun run)
        where TSink : IJpegEntropySink, allows ref struct
    {
        var mcuRow = unit / geometry.McusPerLine;
        var mcuColumn = unit % geometry.McusPerLine;
        for (var c = 0; c < components.Length; c++)
        {
            if ((spec.Components & (1 << c)) == 0)
            {
                continue;
            }

            var component = components[c];
            for (var v = 0; v < component.VerticalSampling; v++)
            {
                for (var h = 0; h < component.HorizontalSampling; h++)
                {
                    var block = component.GetBlock((mcuRow * component.VerticalSampling) + v, (mcuColumn * component.HorizontalSampling) + h);
                    EncodeBlock(ref sink, component, block, in spec, ref predictors[c], ref run);
                }
            }
        }
    }

    /// <summary>Codes one block for the scan's kind.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="component">The component the block belongs to.</param>
    /// <param name="block">The 64 coefficients in zigzag order.</param>
    /// <param name="spec">The scan.</param>
    /// <param name="predictor">The previous DC value of the component, updated.</param>
    /// <param name="run">The end-of-band state.</param>
    private static void EncodeBlock<TSink>(ref TSink sink, JpegEncoderComponent component, ReadOnlySpan<short> block, in JpegScanSpec spec, scoped ref int predictor, ref JpegBlockRun run)
        where TSink : IJpegEntropySink, allows ref struct
    {
        if (spec.IsDc)
        {
            EncodeDc(ref sink, component.TableSlot, block[0], in spec, ref predictor);
            return;
        }

        var table = JpegHuffmanBuilder.FirstAcSlot + component.TableSlot;
        if (spec.IsRefinement)
        {
            EncodeRefinement(ref sink, table, block, in spec, ref run);
            return;
        }

        EncodeFirst(ref sink, table, block, in spec, ref run);
    }

    /// <summary>Codes the DC coefficient of a block: the point-transformed value on the first pass, a single bit on a refinement.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="table">The DC table slot.</param>
    /// <param name="value">The coefficient.</param>
    /// <param name="spec">The scan.</param>
    /// <param name="predictor">The previous DC value of the component, updated.</param>
    private static void EncodeDc<TSink>(ref TSink sink, int table, int value, in JpegScanSpec spec, scoped ref int predictor)
        where TSink : IJpegEntropySink, allows ref struct
    {
        if (spec.IsRefinement)
        {
            sink.Bits((uint)(value >> spec.Low) & 1U, 1);
            return;
        }

        JpegSequentialScan.EncodeDc(ref sink, table, value >> spec.Low, ref predictor);
    }

    /// <summary>Codes the first pass over a band of AC coefficients: the coefficients after the point transform, with zero runs and an end-of-band run.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="table">The AC table slot.</param>
    /// <param name="block">The 64 coefficients in zigzag order.</param>
    /// <param name="spec">The scan.</param>
    /// <param name="run">The end-of-band state.</param>
    private static void EncodeFirst<TSink>(ref TSink sink, int table, ReadOnlySpan<short> block, in JpegScanSpec spec, ref JpegBlockRun run)
        where TSink : IJpegEntropySink, allows ref struct
    {
        var zeros = 0;
        for (var k = spec.Start; k <= spec.End; k++)
        {
            var value = (int)block[k];
            var magnitude = Math.Abs(value) >> spec.Low;
            if (magnitude == 0)
            {
                zeros++;
                continue;
            }

            FlushRun(ref sink, table, ref run);
            zeros = WriteZeroRuns(ref sink, table, zeros);

            // A negative value is coded as the ones' complement of its magnitude (G.1.2.2).
            var size = BitOperations.Log2((uint)magnitude) + 1;
            var bits = (uint)(value < 0 ? ~magnitude : magnitude) & ((1U << size) - 1);
            sink.Symbol(table, (zeros << JpegSequentialScan.RunShift) | size, bits, size);
            zeros = 0;
        }

        if (zeros == 0)
        {
            return;
        }

        run.Count++;
        if (run.Count == MaxRun)
        {
            FlushRun(ref sink, table, ref run);
        }
    }

    /// <summary>Codes a refinement pass over a band of AC coefficients: new coefficients that reach magnitude one, and a correction bit for each coefficient an earlier pass already coded.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="table">The AC table slot.</param>
    /// <param name="block">The 64 coefficients in zigzag order.</param>
    /// <param name="spec">The scan.</param>
    /// <param name="run">The end-of-band state.</param>
    private static void EncodeRefinement<TSink>(ref TSink sink, int table, ReadOnlySpan<short> block, in JpegScanSpec spec, ref JpegBlockRun run)
        where TSink : IJpegEntropySink, allows ref struct
    {
        Span<int> magnitudes = stackalloc int[JpegBlock.Length];
        var lastNew = MeasureBlock(block, in spec, magnitudes);
        Span<byte> pending = stackalloc byte[BlockBits];
        var pendingCount = 0;
        var zeros = 0;
        for (var k = spec.Start; k <= spec.End; k++)
        {
            var magnitude = magnitudes[k];
            if (magnitude == 0)
            {
                zeros++;
                continue;
            }

            zeros = WriteZeroRuns(ref sink, table, zeros, k <= lastNew, ref run, pending, ref pendingCount);
            if (magnitude > 1)
            {
                // A coefficient an earlier pass coded: its next bit waits until the symbol that covers it is written.
                pending[pendingCount] = (byte)(magnitude & 1);
                pendingCount++;
                continue;
            }

            FlushRun(ref sink, table, ref run);
            sink.Symbol(table, (zeros << JpegSequentialScan.RunShift) | 1);
            sink.Bits(block[k] < 0 ? 0U : 1U, 1);
            WriteBits(ref sink, pending[..pendingCount]);
            pendingCount = 0;
            zeros = 0;
        }

        JoinRun(ref sink, table, ref run, zeros, pending[..pendingCount]);
    }

    /// <summary>Reduces a band of a block to the magnitudes of this pass and finds the last coefficient that becomes nonzero in it.</summary>
    /// <param name="block">The 64 coefficients in zigzag order.</param>
    /// <param name="spec">The scan.</param>
    /// <param name="magnitudes">Receives the magnitudes after the point transform.</param>
    /// <returns>The zigzag index of the last coefficient whose magnitude is one, or zero when none is.</returns>
    private static int MeasureBlock(ReadOnlySpan<short> block, in JpegScanSpec spec, Span<int> magnitudes)
    {
        var lastNew = 0;
        for (var k = spec.Start; k <= spec.End; k++)
        {
            magnitudes[k] = Math.Abs((int)block[k]) >> spec.Low;
            if (magnitudes[k] == 1)
            {
                lastNew = k;
            }
        }

        return lastNew;
    }

    /// <summary>Writes zero-run symbols for every full run of sixteen zeros before a value.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="table">The AC table slot.</param>
    /// <param name="zeros">The zeros before the value.</param>
    /// <returns>The zeros left, fifteen at most.</returns>
    private static int WriteZeroRuns<TSink>(ref TSink sink, int table, int zeros)
        where TSink : IJpegEntropySink, allows ref struct
    {
        var remaining = zeros;
        while (remaining > JpegSequentialScan.LongestRun)
        {
            sink.Symbol(table, JpegSequentialScan.ZeroRun);
            remaining -= JpegSequentialScan.ZeroRunLength;
        }

        return remaining;
    }

    /// <summary>
    /// Writes zero-run symbols in a refinement pass. A run is only written while a new coefficient still follows, because
    /// zeros after the last one belong to the end-of-band run. The correction bits that waited go after each symbol.
    /// </summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="table">The AC table slot.</param>
    /// <param name="zeros">The zeros before the value.</param>
    /// <param name="newFollows">Whether a coefficient that becomes nonzero is still to come.</param>
    /// <param name="run">The end-of-band state.</param>
    /// <param name="pending">The waiting correction bits.</param>
    /// <param name="pendingCount">The number of waiting bits, cleared when they are written.</param>
    /// <returns>The zeros left.</returns>
    private static int WriteZeroRuns<TSink>(ref TSink sink, int table, int zeros, bool newFollows, ref JpegBlockRun run, scoped Span<byte> pending, scoped ref int pendingCount)
        where TSink : IJpegEntropySink, allows ref struct
    {
        var remaining = zeros;
        while (remaining > JpegSequentialScan.LongestRun && newFollows)
        {
            FlushRun(ref sink, table, ref run);
            sink.Symbol(table, JpegSequentialScan.ZeroRun);
            remaining -= JpegSequentialScan.ZeroRunLength;
            WriteBits(ref sink, pending[..pendingCount]);
            pendingCount = 0;
        }

        return remaining;
    }

    /// <summary>Adds the rest of a refinement block to the end-of-band run when it has zeros or correction bits left.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="table">The AC table slot.</param>
    /// <param name="run">The end-of-band state.</param>
    /// <param name="zeros">The zeros at the end of the block.</param>
    /// <param name="pending">The correction bits that wait.</param>
    private static void JoinRun<TSink>(ref TSink sink, int table, ref JpegBlockRun run, int zeros, scoped ReadOnlySpan<byte> pending)
        where TSink : IJpegEntropySink, allows ref struct
    {
        if (zeros == 0 && pending.IsEmpty)
        {
            return;
        }

        pending.CopyTo(run.Bits[run.BitCount..]);
        run.BitCount += pending.Length;
        run.Count++;
        if (run.Count == MaxRun || run.BitCount > FlushLevel)
        {
            FlushRun(ref sink, table, ref run);
        }
    }

    /// <summary>Writes the correction bits that were waiting.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="bits">The bits, one per byte.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteBits<TSink>(ref TSink sink, scoped ReadOnlySpan<byte> bits)
        where TSink : IJpegEntropySink, allows ref struct
    {
        foreach (var bit in bits)
        {
            sink.Bits(bit, 1);
        }
    }

    /// <summary>Writes a scan's pending end-of-band run, for a scan with a single component.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="single">The scan's component, or null for an interleaved DC scan, which has no run.</param>
    /// <param name="run">The end-of-band state.</param>
    private static void FlushRun<TSink>(ref TSink sink, JpegEncoderComponent? single, ref JpegBlockRun run)
        where TSink : IJpegEntropySink, allows ref struct
    {
        if (single is null)
        {
            return;
        }

        FlushRun(ref sink, JpegHuffmanBuilder.FirstAcSlot + single.TableSlot, ref run);
    }

    /// <summary>Writes the pending end-of-band run: a symbol for the run length, the length's extra bits, then the correction bits the run collected.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="table">The AC table slot.</param>
    /// <param name="run">The end-of-band state, cleared.</param>
    private static void FlushRun<TSink>(ref TSink sink, int table, ref JpegBlockRun run)
        where TSink : IJpegEntropySink, allows ref struct
    {
        if (run.Count == 0)
        {
            return;
        }

        var size = BitOperations.Log2((uint)run.Count);
        if (size == 0)
        {
            sink.Symbol(table, 0);
        }
        else
        {
            sink.Symbol(table, size << JpegSequentialScan.RunShift, (uint)run.Count & ((1U << size) - 1), size);
        }

        run.Count = 0;
        WriteBits(ref sink, run.Bits[..run.BitCount]);
        run.BitCount = 0;
    }
}
