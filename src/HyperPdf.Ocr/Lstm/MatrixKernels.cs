// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Ocr.Lstm;

/// <summary>
/// Multiplies batches of input steps by a weight matrix stored by input, so a row of the matrix holds one input's weight
/// for every output. Each pass over a row of weights serves several steps at once, and the outputs fill whole vectors, so
/// no sum across a vector is ever needed.
/// </summary>
internal static class MatrixKernels
{
    /// <summary>The number of steps the wide tile handles at once.</summary>
    private const int TileSteps = 4;

    /// <summary>The number of output vectors the wide tile handles at once.</summary>
    private const int TileVectors = 2;

    /// <summary>The number of output vectors a single step handles at once.</summary>
    private const int RowVectors = 8;

    /// <summary>The place of the third step of a tile.</summary>
    private const int ThirdStep = 2;

    /// <summary>The place of the fourth step of a tile.</summary>
    private const int FourthStep = 3;

    /// <summary>The place of the third vector of a row.</summary>
    private const int ThirdVector = 2;

    /// <summary>The place of the fourth vector of a row.</summary>
    private const int FourthVector = 3;

    /// <summary>The place of the fifth vector of a row.</summary>
    private const int FifthVector = 4;

    /// <summary>The place of the sixth vector of a row.</summary>
    private const int SixthVector = 5;

    /// <summary>The place of the seventh vector of a row.</summary>
    private const int SeventhVector = 6;

    /// <summary>The place of the eighth vector of a row.</summary>
    private const int EighthVector = 7;

    /// <summary>Computes <c>output[step] (+)= sum over k of input[step][k] * weights[firstInput + k]</c> for a batch of steps.</summary>
    /// <param name="weights">The weights: a row for each input, <paramref name="paddedOutputs"/> long.</param>
    /// <param name="paddedOutputs">The length of a row of weights, a whole number of vectors.</param>
    /// <param name="input">The input steps.</param>
    /// <param name="inputStride">The distance between the starts of two input steps.</param>
    /// <param name="output">The output steps.</param>
    /// <param name="outputStride">The distance between the starts of two output steps, at least <paramref name="paddedOutputs"/>.</param>
    /// <param name="range">The steps and inputs to multiply.</param>
    internal static void Multiply(ReadOnlySpan<float> weights, int paddedOutputs, ReadOnlySpan<float> input, int inputStride, Span<float> output, int outputStride, MultiplyRange range)
    {
        if (range.Steps <= 0)
        {
            return;
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(input.Length, ((range.Steps - 1) * inputStride) + range.InputCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(output.Length, ((range.Steps - 1) * outputStride) + paddedOutputs);
        ArgumentOutOfRangeException.ThrowIfLessThan(weights.Length, (range.FirstInput + range.InputCount) * paddedOutputs);
        var span = new TileSpan(paddedOutputs, range.InputCount, inputStride, outputStride, range.Accumulate);
        ref var w = ref Unsafe.Add(ref MemoryMarshal.GetReference(weights), range.FirstInput * paddedOutputs);
        ref var x = ref MemoryMarshal.GetReference(input);
        ref var o = ref MemoryMarshal.GetReference(output);
        var vectors = paddedOutputs / Vector<float>.Count;
        var step = 0;
        for (; step + TileSteps <= range.Steps; step += TileSteps)
        {
            WideSteps(ref w, ref Unsafe.Add(ref x, step * inputStride), ref Unsafe.Add(ref o, step * outputStride), vectors, span);
        }

        for (; step < range.Steps; step++)
        {
            SingleStep(ref w, ref Unsafe.Add(ref x, step * inputStride), ref Unsafe.Add(ref o, step * outputStride), vectors, span);
        }
    }

    /// <summary>Multiplies four steps at once across all the output vectors.</summary>
    /// <param name="w">The first weight of the first input used.</param>
    /// <param name="x">The first input of the first step.</param>
    /// <param name="o">The first output of the first step.</param>
    /// <param name="vectors">The number of output vectors.</param>
    /// <param name="span">The sizes.</param>
    private static void WideSteps(ref float w, ref float x, ref float o, int vectors, TileSpan span)
    {
        var vector = 0;
        for (; vector + TileVectors <= vectors; vector += TileVectors)
        {
            Tile4x2(ref Unsafe.Add(ref w, vector * Vector<float>.Count), ref x, ref Unsafe.Add(ref o, vector * Vector<float>.Count), span);
        }

        for (; vector < vectors; vector++)
        {
            Tile4x1(ref Unsafe.Add(ref w, vector * Vector<float>.Count), ref x, ref Unsafe.Add(ref o, vector * Vector<float>.Count), span);
        }
    }

    /// <summary>Multiplies one step across all the output vectors.</summary>
    /// <param name="w">The first weight of the first input used.</param>
    /// <param name="x">The first input of the step.</param>
    /// <param name="o">The first output of the step.</param>
    /// <param name="vectors">The number of output vectors.</param>
    /// <param name="span">The sizes.</param>
    private static void SingleStep(ref float w, ref float x, ref float o, int vectors, TileSpan span)
    {
        var vector = 0;
        for (; vector + RowVectors <= vectors; vector += RowVectors)
        {
            Tile1x8(ref Unsafe.Add(ref w, vector * Vector<float>.Count), ref x, ref Unsafe.Add(ref o, vector * Vector<float>.Count), span);
        }

        for (; vector < vectors; vector++)
        {
            Tile1x1(ref Unsafe.Add(ref w, vector * Vector<float>.Count), ref x, ref Unsafe.Add(ref o, vector * Vector<float>.Count), span);
        }
    }

    /// <summary>Loads a vector, or zero when the outputs are to be replaced.</summary>
    /// <param name="o">The output.</param>
    /// <param name="offset">The offset in floats.</param>
    /// <param name="accumulate">Whether to load what the output holds.</param>
    /// <returns>The vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<float> Start(ref float o, int offset, bool accumulate) =>
        accumulate ? Vector.LoadUnsafe(ref o, (nuint)offset) : Vector<float>.Zero;

    /// <summary>Spreads one input over a whole vector.</summary>
    /// <param name="x">The first input of the step.</param>
    /// <param name="offset">The place of the input.</param>
    /// <returns>The vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<float> Spread(ref float x, int offset) => Vector.Create(Unsafe.Add(ref x, offset));

    /// <summary>Multiplies four steps by two vectors of outputs.</summary>
    /// <param name="w">The first weight of the tile.</param>
    /// <param name="x">The first input of the first step.</param>
    /// <param name="o">The first output of the first step of the tile.</param>
    /// <param name="span">The sizes.</param>
    private static void Tile4x2(ref float w, ref float x, ref float o, TileSpan span)
    {
        var width = Vector<float>.Count;
        var xs = span.InputStride;
        var os = span.OutputStride;
        var a00 = Start(ref o, 0, span.Accumulate);
        var a01 = Start(ref o, width, span.Accumulate);
        var a10 = Start(ref o, os, span.Accumulate);
        var a11 = Start(ref o, os + width, span.Accumulate);
        var a20 = Start(ref o, ThirdStep * os, span.Accumulate);
        var a21 = Start(ref o, (ThirdStep * os) + width, span.Accumulate);
        var a30 = Start(ref o, FourthStep * os, span.Accumulate);
        var a31 = Start(ref o, (FourthStep * os) + width, span.Accumulate);
        for (var k = 0; k < span.InputCount; k++)
        {
            var w0 = Vector.LoadUnsafe(ref w, (nuint)(k * span.PaddedOutputs));
            var w1 = Vector.LoadUnsafe(ref w, (nuint)((k * span.PaddedOutputs) + width));
            var x0 = Spread(ref x, k);
            var x1 = Spread(ref x, xs + k);
            var x2 = Spread(ref x, (ThirdStep * xs) + k);
            var x3 = Spread(ref x, (FourthStep * xs) + k);
            a00 = Vector.FusedMultiplyAdd(w0, x0, a00);
            a01 = Vector.FusedMultiplyAdd(w1, x0, a01);
            a10 = Vector.FusedMultiplyAdd(w0, x1, a10);
            a11 = Vector.FusedMultiplyAdd(w1, x1, a11);
            a20 = Vector.FusedMultiplyAdd(w0, x2, a20);
            a21 = Vector.FusedMultiplyAdd(w1, x2, a21);
            a30 = Vector.FusedMultiplyAdd(w0, x3, a30);
            a31 = Vector.FusedMultiplyAdd(w1, x3, a31);
        }

        Vector.StoreUnsafe(a00, ref o, 0);
        Vector.StoreUnsafe(a01, ref o, (nuint)width);
        Vector.StoreUnsafe(a10, ref o, (nuint)os);
        Vector.StoreUnsafe(a11, ref o, (nuint)(os + width));
        Vector.StoreUnsafe(a20, ref o, (nuint)(ThirdStep * os));
        Vector.StoreUnsafe(a21, ref o, (nuint)((ThirdStep * os) + width));
        Vector.StoreUnsafe(a30, ref o, (nuint)(FourthStep * os));
        Vector.StoreUnsafe(a31, ref o, (nuint)((FourthStep * os) + width));
    }

    /// <summary>Multiplies four steps by one vector of outputs.</summary>
    /// <param name="w">The first weight of the tile.</param>
    /// <param name="x">The first input of the first step.</param>
    /// <param name="o">The first output of the first step of the tile.</param>
    /// <param name="span">The sizes.</param>
    private static void Tile4x1(ref float w, ref float x, ref float o, TileSpan span)
    {
        var xs = span.InputStride;
        var os = span.OutputStride;
        var a0 = Start(ref o, 0, span.Accumulate);
        var a1 = Start(ref o, os, span.Accumulate);
        var a2 = Start(ref o, ThirdStep * os, span.Accumulate);
        var a3 = Start(ref o, FourthStep * os, span.Accumulate);
        for (var k = 0; k < span.InputCount; k++)
        {
            var weight = Vector.LoadUnsafe(ref w, (nuint)(k * span.PaddedOutputs));
            a0 = Vector.FusedMultiplyAdd(weight, Spread(ref x, k), a0);
            a1 = Vector.FusedMultiplyAdd(weight, Spread(ref x, xs + k), a1);
            a2 = Vector.FusedMultiplyAdd(weight, Spread(ref x, (ThirdStep * xs) + k), a2);
            a3 = Vector.FusedMultiplyAdd(weight, Spread(ref x, (FourthStep * xs) + k), a3);
        }

        Vector.StoreUnsafe(a0, ref o, 0);
        Vector.StoreUnsafe(a1, ref o, (nuint)os);
        Vector.StoreUnsafe(a2, ref o, (nuint)(ThirdStep * os));
        Vector.StoreUnsafe(a3, ref o, (nuint)(FourthStep * os));
    }

    /// <summary>Multiplies one step by eight vectors of outputs.</summary>
    /// <param name="w">The first weight of the tile.</param>
    /// <param name="x">The first input of the step.</param>
    /// <param name="o">The first output of the tile.</param>
    /// <param name="span">The sizes.</param>
    private static void Tile1x8(ref float w, ref float x, ref float o, TileSpan span)
    {
        var width = Vector<float>.Count;
        var a0 = Start(ref o, 0, span.Accumulate);
        var a1 = Start(ref o, width, span.Accumulate);
        var a2 = Start(ref o, ThirdVector * width, span.Accumulate);
        var a3 = Start(ref o, FourthVector * width, span.Accumulate);
        var a4 = Start(ref o, FifthVector * width, span.Accumulate);
        var a5 = Start(ref o, SixthVector * width, span.Accumulate);
        var a6 = Start(ref o, SeventhVector * width, span.Accumulate);
        var a7 = Start(ref o, EighthVector * width, span.Accumulate);
        for (var k = 0; k < span.InputCount; k++)
        {
            var value = Spread(ref x, k);
            var row = k * span.PaddedOutputs;
            a0 = Vector.FusedMultiplyAdd(Vector.LoadUnsafe(ref w, (nuint)row), value, a0);
            a1 = Vector.FusedMultiplyAdd(Vector.LoadUnsafe(ref w, (nuint)(row + width)), value, a1);
            a2 = Vector.FusedMultiplyAdd(Vector.LoadUnsafe(ref w, (nuint)(row + (ThirdVector * width))), value, a2);
            a3 = Vector.FusedMultiplyAdd(Vector.LoadUnsafe(ref w, (nuint)(row + (FourthVector * width))), value, a3);
            a4 = Vector.FusedMultiplyAdd(Vector.LoadUnsafe(ref w, (nuint)(row + (FifthVector * width))), value, a4);
            a5 = Vector.FusedMultiplyAdd(Vector.LoadUnsafe(ref w, (nuint)(row + (SixthVector * width))), value, a5);
            a6 = Vector.FusedMultiplyAdd(Vector.LoadUnsafe(ref w, (nuint)(row + (SeventhVector * width))), value, a6);
            a7 = Vector.FusedMultiplyAdd(Vector.LoadUnsafe(ref w, (nuint)(row + (EighthVector * width))), value, a7);
        }

        Vector.StoreUnsafe(a0, ref o, 0);
        Vector.StoreUnsafe(a1, ref o, (nuint)width);
        Vector.StoreUnsafe(a2, ref o, (nuint)(ThirdVector * width));
        Vector.StoreUnsafe(a3, ref o, (nuint)(FourthVector * width));
        Vector.StoreUnsafe(a4, ref o, (nuint)(FifthVector * width));
        Vector.StoreUnsafe(a5, ref o, (nuint)(SixthVector * width));
        Vector.StoreUnsafe(a6, ref o, (nuint)(SeventhVector * width));
        Vector.StoreUnsafe(a7, ref o, (nuint)(EighthVector * width));
    }

    /// <summary>Multiplies one step by one vector of outputs.</summary>
    /// <param name="w">The first weight of the tile.</param>
    /// <param name="x">The first input of the step.</param>
    /// <param name="o">The first output of the tile.</param>
    /// <param name="span">The sizes.</param>
    private static void Tile1x1(ref float w, ref float x, ref float o, TileSpan span)
    {
        var sum = Start(ref o, 0, span.Accumulate);
        for (var k = 0; k < span.InputCount; k++)
        {
            sum = Vector.FusedMultiplyAdd(Vector.LoadUnsafe(ref w, (nuint)(k * span.PaddedOutputs)), Spread(ref x, k), sum);
        }

        Vector.StoreUnsafe(sum, ref o, 0);
    }

    /// <summary>The sizes a tile works within.</summary>
    /// <param name="PaddedOutputs">The length of a row of weights.</param>
    /// <param name="InputCount">The number of inputs multiplied.</param>
    /// <param name="InputStride">The distance between input steps.</param>
    /// <param name="OutputStride">The distance between output steps.</param>
    /// <param name="Accumulate">Whether outputs are added to rather than replaced.</param>
    private readonly record struct TileSpan(int PaddedOutputs, int InputCount, int InputStride, int OutputStride, bool Accumulate);
}
