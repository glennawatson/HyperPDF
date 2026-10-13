// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// A 9/7 lifting step: <c>x = x + (a + b) * c</c>, multiplied then added in that order (no fused multiply-add) so the
/// result does not depend on whether the processor has that instruction, and every platform decodes the same pixels.
/// </summary>
internal readonly record struct JpxIrreversibleStep : IJpxLiftStep<float>
{
    /// <inheritdoc/>
    public static void Apply(Span<float> target, ReadOnlySpan<float> first, ReadOnlySpan<float> second, float factor)
    {
        var count = target.Length;
        ref var t = ref MemoryMarshal.GetReference(target);
        ref var a = ref MemoryMarshal.GetReference(first[..count]);
        ref var b = ref MemoryMarshal.GetReference(second[..count]);
        var i = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            var c = Vector256.Create(factor);
            for (; i <= count - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                var sum = Vector256.LoadUnsafe(ref a, (nuint)i) + Vector256.LoadUnsafe(ref b, (nuint)i);
                (Vector256.LoadUnsafe(ref t, (nuint)i) + (sum * c)).StoreUnsafe(ref t, (nuint)i);
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            var c = Vector128.Create(factor);
            for (; i <= count - Vector128<float>.Count; i += Vector128<float>.Count)
            {
                var sum = Vector128.LoadUnsafe(ref a, (nuint)i) + Vector128.LoadUnsafe(ref b, (nuint)i);
                (Vector128.LoadUnsafe(ref t, (nuint)i) + (sum * c)).StoreUnsafe(ref t, (nuint)i);
            }
        }

        for (; i < count; i++)
        {
            target[i] += (first[i] + second[i]) * factor;
        }
    }
}
