// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using HyperPdfLibrary.Compat;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>Widens a row of chroma samples to twice its width by repeating each sample, sixteen samples at a time.</summary>
internal static class JpegUpsample
{
    /// <summary>The factor by which a row widens.</summary>
    private const int Doubling = 2;

    /// <summary>The samples one step reads.</summary>
    private const int StepSamples = 16;

    /// <summary>The bytes one half of a step writes.</summary>
    private const int HalfStepBytes = 16;

    /// <summary>Repeats each sample twice for as many whole steps as fit.</summary>
    /// <param name="source">The samples.</param>
    /// <param name="destination">Receives each sample twice.</param>
    /// <returns>The number of destination samples written, which the caller continues after.</returns>
    internal static int Double(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            return 0;
        }

        ref var input = ref MemoryMarshal.GetReference(source);
        ref var output = ref MemoryMarshal.GetReference(destination);
        var read = 0;
        for (; read + StepSamples <= source.Length && (read * Doubling) + (StepSamples * Doubling) <= destination.Length; read += StepSamples)
        {
            var bytes = Vector128.LoadUnsafe(ref input, (nuint)read);
            var at = (nuint)(read * Doubling);
            ByteLanes.RepeatLower(bytes).StoreUnsafe(ref output, at);
            ByteLanes.RepeatUpper(bytes).StoreUnsafe(ref output, at + HalfStepBytes);
        }

        return read * Doubling;
    }
}
