// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>Codes the MCUs of a sequential scan (ISO/IEC 10918-1, F.1.2): the DC difference, then the AC coefficients as run and size symbols.</summary>
internal static class JpegSequentialScan
{
    /// <summary>The symbol that ends a block early.</summary>
    internal const int EndOfBlock = 0x00;

    /// <summary>The symbol that skips sixteen zero coefficients.</summary>
    internal const int ZeroRun = 0xF0;

    /// <summary>The longest run of zeros a run and size symbol carries.</summary>
    internal const int LongestRun = 15;

    /// <summary>The zeros a zero-run symbol skips.</summary>
    internal const int ZeroRunLength = 16;

    /// <summary>The bits that hold the size in a run and size symbol.</summary>
    internal const int RunShift = 4;

    /// <summary>The units between checks of the cancellation token (1,024).</summary>
    private const int CancelCheckMask = 1023;

    /// <summary>The last coefficient of a block.</summary>
    private const int LastCoefficient = JpegBlock.Length - 1;

    /// <summary>Codes every MCU of a scan.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="components">The frame's components.</param>
    /// <param name="spec">The scan.</param>
    /// <param name="geometry">The frame layout.</param>
    /// <param name="restartInterval">The MCUs between restart markers, or zero.</param>
    /// <param name="cancellationToken">Cancels the scan between MCUs.</param>
    internal static void Encode<TSink>(
        ref TSink sink,
        ReadOnlySpan<JpegEncoderComponent> components,
        in JpegScanSpec spec,
        in JpegFrameGeometry geometry,
        int restartInterval,
        CancellationToken cancellationToken)
        where TSink : IJpegEntropySink, allows ref struct
    {
        Span<int> predictors = stackalloc int[JpegEncoderState.MaxComponents];
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
                sink.Restart(restartIndex);
                restartIndex++;
                predictors.Clear();
                untilRestart = restartInterval;
            }

            if (single is not null)
            {
                EncodeBlock(ref sink, single, single.GetBlock(unit / single.UsedBlocksPerLine, unit % single.UsedBlocksPerLine), ref predictors[0]);
            }
            else
            {
                EncodeUnit(ref sink, components, spec.Components, geometry, unit, predictors);
            }

            untilRestart--;
        }

        sink.EndScan();
    }

    /// <summary>Codes a DC coefficient as the size of its difference from the previous one, then the difference's bits.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="table">The DC table slot.</param>
    /// <param name="value">The coefficient.</param>
    /// <param name="predictor">The previous coefficient, updated.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EncodeDc<TSink>(ref TSink sink, int table, int value, scoped ref int predictor)
        where TSink : IJpegEntropySink, allows ref struct
    {
        var difference = value - predictor;
        predictor = value;
        EncodeValue(ref sink, table, 0, difference);
    }

    /// <summary>Codes a nonzero value as a symbol that carries its size, then the value's bits (F.1.2.1.2: a negative value is coded as its ones' complement).</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="table">The table slot.</param>
    /// <param name="runBits">The run of zeros before the value, already shifted into the high nibble; zero for DC.</param>
    /// <param name="value">The value; zero is coded as size zero.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EncodeValue<TSink>(ref TSink sink, int table, int runBits, int value)
        where TSink : IJpegEntropySink, allows ref struct
    {
        if (value == 0)
        {
            sink.Symbol(table, runBits);
            return;
        }

        var size = BitOperations.Log2((uint)Math.Abs(value)) + 1;
        var bits = (uint)(value < 0 ? value - 1 : value) & ((1U << size) - 1);
        sink.Symbol(table, runBits | size, bits, size);
    }

    /// <summary>Codes one interleaved MCU: every block of every component in the scan.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="components">The frame's components.</param>
    /// <param name="mask">The components in the scan.</param>
    /// <param name="geometry">The frame layout.</param>
    /// <param name="unit">The MCU number.</param>
    /// <param name="predictors">The DC predictor of each component.</param>
    private static void EncodeUnit<TSink>(ref TSink sink, ReadOnlySpan<JpegEncoderComponent> components, int mask, in JpegFrameGeometry geometry, int unit, scoped Span<int> predictors)
        where TSink : IJpegEntropySink, allows ref struct
    {
        var mcuRow = unit / geometry.McusPerLine;
        var mcuColumn = unit % geometry.McusPerLine;
        for (var c = 0; c < components.Length; c++)
        {
            if ((mask & (1 << c)) == 0)
            {
                continue;
            }

            var component = components[c];
            for (var v = 0; v < component.VerticalSampling; v++)
            {
                for (var h = 0; h < component.HorizontalSampling; h++)
                {
                    var block = component.GetBlock((mcuRow * component.VerticalSampling) + v, (mcuColumn * component.HorizontalSampling) + h);
                    EncodeBlock(ref sink, component, block, ref predictors[c]);
                }
            }
        }
    }

    /// <summary>Codes one block.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="component">The component the block belongs to.</param>
    /// <param name="block">The 64 coefficients in zigzag order.</param>
    /// <param name="predictor">The previous DC value of the component, updated.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncodeBlock<TSink>(ref TSink sink, JpegEncoderComponent component, ReadOnlySpan<short> block, scoped ref int predictor)
        where TSink : IJpegEntropySink, allows ref struct
    {
        EncodeDc(ref sink, component.TableSlot, block[0], ref predictor);
        var coefficientTable = JpegHuffmanBuilder.FirstAcSlot + component.TableSlot;
        var mask = JpegBlockMask.NonZero(block) & ~1UL;
        if (mask == 0)
        {
            sink.Symbol(coefficientTable, EndOfBlock);
            return;
        }

        var previous = 0;
        while (mask != 0)
        {
            var index = BitOperations.TrailingZeroCount(mask);
            mask &= mask - 1;
            var run = index - previous - 1;
            previous = index;
            while (run > LongestRun)
            {
                sink.Symbol(coefficientTable, ZeroRun);
                run -= ZeroRunLength;
            }

            EncodeValue(ref sink, coefficientTable, run << RunShift, block[index]);
        }

        if (previous < LastCoefficient)
        {
            sink.Symbol(coefficientTable, EndOfBlock);
        }
    }
}
