// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Reuses exact-length stop arrays within a backend drawing session for factories that copy them into native shaders.</summary>
internal sealed class SkiaGradientStops
{
    /// <summary>The lengths kept per thread; a length uses the slot of its remainder.</summary>
    private const int SlotCount = 4;

    /// <summary>The native colour arrays by length slot.</summary>
    private SKColor[]?[]? _colorSlots;

    /// <summary>The position arrays by length slot.</summary>
    private float[]?[]? _positionSlots;

    /// <summary>Converts gradient colours into this thread's array of the same length.</summary>
    /// <param name="colors">The managed colours.</param>
    /// <returns>An array that holds the native colours until the next call for this length on this thread.</returns>
    internal SKColor[] Colors(ReadOnlySpan<PdfColor> colors)
    {
        var slots = _colorSlots ??= new SKColor[]?[SlotCount];
        var slot = colors.Length % SlotCount;
        var result = slots[slot];
        if (result is null || result.Length != colors.Length)
        {
            result = new SKColor[colors.Length];
            slots[slot] = result;
        }

        for (var index = 0; index < result.Length; index++)
        {
            result[index] = SkiaConversions.ToSkColor(colors[index]);
        }

        return result;
    }

    /// <summary>Copies gradient positions into this thread's array of the same length.</summary>
    /// <param name="positions">The stop positions.</param>
    /// <returns>An array that holds the positions until the next call for this length on this thread.</returns>
    internal float[] Positions(ReadOnlySpan<float> positions)
    {
        var slots = _positionSlots ??= new float[]?[SlotCount];
        var slot = positions.Length % SlotCount;
        var result = slots[slot];
        if (result is null || result.Length != positions.Length)
        {
            result = new float[positions.Length];
            slots[slot] = result;
        }

        positions.CopyTo(result);
        return result;
    }
}
