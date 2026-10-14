// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// The lifting steps of the forward wavelet (ISO 15444-1 F.4) over runs of samples. The vertical pass applies a step to
/// whole rows at once, so these are vector loops; the horizontal pass applies them to a line with a stride of two.
/// </summary>
internal static class JpxLiftRows
{
    /// <summary>The shift that halves a sum.</summary>
    private const int HalfShift = 1;

    /// <summary>The shift that quarters a sum.</summary>
    private const int QuarterShift = 2;

    /// <summary>The rounding term of the quarter sum (equation F-5).</summary>
    private const int QuarterRounding = 2;

    /// <summary>Subtracts floor((first + second) / 2) from each target sample: the 5/3 high-pass step (equation F-5).</summary>
    /// <param name="target">The samples changed in place.</param>
    /// <param name="first">The sample on one side of each target sample.</param>
    /// <param name="second">The sample on the other side.</param>
    internal static void SubtractHalfSum(Span<int> target, ReadOnlySpan<int> first, ReadOnlySpan<int> second)
    {
        var count = target.Length;
        ref var t = ref MemoryMarshal.GetReference(target);
        ref var a = ref MemoryMarshal.GetReference(first[..count]);
        ref var b = ref MemoryMarshal.GetReference(second[..count]);
        var i = 0;
        if (Vector.IsHardwareAccelerated)
        {
            for (; i <= count - Vector<int>.Count; i += Vector<int>.Count)
            {
                var sum = Vector.LoadUnsafe(ref a, (nuint)i) + Vector.LoadUnsafe(ref b, (nuint)i);
                (Vector.LoadUnsafe(ref t, (nuint)i) - Vector.ShiftRightArithmetic(sum, HalfShift)).StoreUnsafe(ref t, (nuint)i);
            }
        }

        for (; i < count; i++)
        {
            target[i] -= (first[i] + second[i]) >> HalfShift;
        }
    }

    /// <summary>Adds floor((first + second + 2) / 4) to each target sample: the 5/3 low-pass step (equation F-6).</summary>
    /// <param name="target">The samples changed in place.</param>
    /// <param name="first">The high-pass sample on one side of each target sample.</param>
    /// <param name="second">The high-pass sample on the other side.</param>
    internal static void AddQuarterSum(Span<int> target, ReadOnlySpan<int> first, ReadOnlySpan<int> second)
    {
        var count = target.Length;
        ref var t = ref MemoryMarshal.GetReference(target);
        ref var a = ref MemoryMarshal.GetReference(first[..count]);
        ref var b = ref MemoryMarshal.GetReference(second[..count]);
        var i = 0;
        if (Vector.IsHardwareAccelerated)
        {
            var rounding = new Vector<int>(QuarterRounding);
            for (; i <= count - Vector<int>.Count; i += Vector<int>.Count)
            {
                var sum = Vector.LoadUnsafe(ref a, (nuint)i) + Vector.LoadUnsafe(ref b, (nuint)i) + rounding;
                (Vector.LoadUnsafe(ref t, (nuint)i) + Vector.ShiftRightArithmetic(sum, QuarterShift)).StoreUnsafe(ref t, (nuint)i);
            }
        }

        for (; i < count; i++)
        {
            target[i] += (first[i] + second[i] + QuarterRounding) >> QuarterShift;
        }
    }

    /// <summary>Multiplies each sample by a factor.</summary>
    /// <param name="samples">The samples changed in place.</param>
    /// <param name="factor">The factor.</param>
    internal static void Scale(Span<float> samples, float factor)
    {
        var count = samples.Length;
        ref var s = ref MemoryMarshal.GetReference(samples);
        var i = 0;
        if (Vector.IsHardwareAccelerated)
        {
            var scale = new Vector<float>(factor);
            for (; i <= count - Vector<float>.Count; i += Vector<float>.Count)
            {
                (Vector.LoadUnsafe(ref s, (nuint)i) * scale).StoreUnsafe(ref s, (nuint)i);
            }
        }

        for (; i < count; i++)
        {
            samples[i] *= factor;
        }
    }

    /// <summary>Adds (first + second) * factor to each target sample, multiplying before adding so no fused multiply-add changes the result.</summary>
    /// <param name="target">The samples changed in place.</param>
    /// <param name="first">The sample on one side of each target sample.</param>
    /// <param name="second">The sample on the other side.</param>
    /// <param name="factor">The lifting coefficient.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AddScaledSum(Span<float> target, ReadOnlySpan<float> first, ReadOnlySpan<float> second, float factor) =>
        JpxIrreversibleStep.Apply(target, first, second, factor);
}
