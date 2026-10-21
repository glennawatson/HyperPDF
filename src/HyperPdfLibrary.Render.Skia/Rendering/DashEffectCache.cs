// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Keeps the last dash effect a device built, so a run of strokes with one dash array, phase and scale shares one native effect.
/// The cache owns the effect: callers set it on a paint and never dispose it.
/// </summary>
[DebuggerDisplay("DashEffectCache: {_effect != null}")]
internal sealed class DashEffectCache : IDisposable
{
    /// <summary>The dash lengths the kept effect was built from.</summary>
    private ReadOnlyMemory<float> _lengths;

    /// <summary>The bits of the phase of the kept effect.</summary>
    private int _phaseBits;

    /// <summary>The bits of the scale of the kept effect.</summary>
    private int _scaleBits;

    /// <summary>The kept effect, or null when none is kept.</summary>
    private SKPathEffect? _effect;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Clear();

    /// <summary>Gets the dash effect for a stroke, building it only when it differs from the kept one.</summary>
    /// <param name="lengths">The dash lengths; empty for a solid line.</param>
    /// <param name="phase">The phase.</param>
    /// <param name="scale">The device units one unit of the path's space spans.</param>
    /// <returns>The effect, owned by this cache, or null for a solid line.</returns>
    internal SKPathEffect? Get(ReadOnlyMemory<float> lengths, float phase, float scale)
    {
        if (lengths.IsEmpty)
        {
            return null;
        }

        // The bits are compared so the same value always matches itself; a different value, even a NaN, only costs a rebuild.
        var phaseBits = BitConverter.SingleToInt32Bits(phase);
        var scaleBits = BitConverter.SingleToInt32Bits(scale);
        if (_effect is not null && _lengths.Equals(lengths) && _phaseBits == phaseBits && _scaleBits == scaleBits)
        {
            return _effect;
        }

        // A solid result (null) is not kept: it costs no native call, and the next dashed stroke replaces the kept effect.
        var created = SkiaDash.Create(lengths, phase, scale);
        Clear();
        if (created is null)
        {
            return null;
        }

        _effect = created;
        _lengths = lengths;
        _phaseBits = phaseBits;
        _scaleBits = scaleBits;
        return created;
    }

    /// <summary>Releases the kept effect and the dash array reference. A paint that still uses the effect keeps its own native reference.</summary>
    internal void Clear()
    {
        _effect?.Dispose();
        _effect = null;
        _lengths = default;
    }
}
