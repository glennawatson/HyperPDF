// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Serves the decoded channels as rows of interleaved 8-bit samples, made when the row is asked for, so the colour
/// conversion never needs the whole image as one sample buffer beside its pixels. Rows must be asked for in order.
/// </summary>
internal readonly ref struct JpxImageRows : IImageRows
{
    /// <summary>The decoded planes, channels and palette.</summary>
    private readonly JpxSampleSource _source;

    /// <summary>The output plan.</summary>
    private readonly JpxOutputPlan _plan;

    /// <summary>Receives one alpha sample per pixel of the image, or is empty when the data has no soft mask.</summary>
    private readonly Span<byte> _alpha;

    /// <summary>The index of the opacity channel.</summary>
    private readonly int _alphaChannel;

    /// <summary>Whether the colour channels are premultiplied by the opacity channel.</summary>
    private readonly bool _premultiplied;

    /// <summary>Whether the YCbCr to RGB conversion runs after the rows are divided by their opacity.</summary>
    private readonly bool _convertAfter;

    /// <summary>Initializes a new instance of the <see cref="JpxImageRows"/> struct.</summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="plan">The output plan.</param>
    internal JpxImageRows(in JpxSampleSource source, JpxOutputPlan plan)
        : this(source, plan, default, 0, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JpxImageRows"/> struct for data with a soft mask: each row's opacity goes
    /// to <paramref name="alpha"/> for the caller to apply to the pixels.
    /// </summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="plan">The output plan.</param>
    /// <param name="alpha">Receives one alpha sample per pixel of the image.</param>
    /// <param name="alphaChannel">The index of the opacity channel.</param>
    /// <param name="premultiplied">Whether the colour channels are premultiplied by the opacity (<c>/SMaskInData 2</c>), so each row is divided by it.</param>
    internal JpxImageRows(in JpxSampleSource source, JpxOutputPlan plan, Span<byte> alpha, int alphaChannel, bool premultiplied)
    {
        _source = source;
        _alpha = alpha;
        _alphaChannel = alphaChannel;
        _premultiplied = premultiplied;

        // Premultiplied YCbCr is divided by its opacity before it becomes RGB, not after.
        _convertAfter = premultiplied && plan.Sycc;
        _plan = _convertAfter ? plan with { Sycc = false } : plan;
    }

    /// <inheritdoc/>
    public bool UsesBuffer => true;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetRow(int y, Span<byte> buffer)
    {
        JpxSampleWriter.WritePlanRow(_source, _plan, y, buffer);
        if (!_alpha.IsEmpty)
        {
            var alpha = _alpha.Slice(y * _source.Width, _source.Width);
            JpxSampleWriter.WriteAlphaRow(_source, _alphaChannel, y, alpha);
            if (_premultiplied)
            {
                var samples = buffer[..(_source.Width * _plan.Components)];
                JpxSampleWriter.Unpremultiply(samples, alpha, _plan.Components);
                if (_convertAfter)
                {
                    JpxSampleWriter.ConvertYcc(samples);
                }
            }
        }

        return buffer;
    }
}
