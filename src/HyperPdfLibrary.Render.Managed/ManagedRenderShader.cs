// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Render.Managed;

/// <summary>A gradient or an image pattern as the library describes it, kept until a shape is painted with it.</summary>
/// <param name="Start">The point a gradient starts at.</param>
/// <param name="StartRadius">The radius at the start of a radial gradient, or -1 for a gradient along a line.</param>
/// <param name="End">The point a gradient ends at.</param>
/// <param name="EndRadius">The radius at the end of a radial gradient.</param>
/// <param name="Colors">The colours of the gradient's stops.</param>
/// <param name="Positions">Where each stop is between the start and the end, 0 to 1.</param>
/// <param name="Mode">What is painted before the start and after the end.</param>
[DebuggerDisplay("ManagedRenderShader: {Colors.Length} stops")]
internal sealed record ManagedRenderShader(
    PdfPoint Start,
    float StartRadius,
    PdfPoint End,
    float EndRadius,
    PdfColor[] Colors,
    float[] Positions,
    PdfShaderTileMode Mode) : IPdfRenderShader
{
    /// <summary>The steps of the colour table.</summary>
    private const int RampSteps = 1024;

    /// <summary>The largest byte, which stands for one.</summary>
    private const float Full = 255F;

    /// <summary>The shift of the red byte of a pixel.</summary>
    private const int RedShift = 16;

    /// <summary>The shift of the green byte of a pixel.</summary>
    private const int GreenShift = 8;

    /// <summary>The shift of the alpha byte of a pixel.</summary>
    private const int AlphaShift = 24;

    /// <summary>The colours along the gradient, made the first time they are asked for.</summary>
    private uint[]? _ramp;

    /// <summary>Gets the premultiplied colours along a gradient, from its start to its end. Empty for an image pattern.</summary>
    internal uint[] Ramp => Volatile.Read(ref _ramp) ?? Interlocked.CompareExchange(ref _ramp, BuildRamp(), null) ?? Volatile.Read(ref _ramp)!;

    /// <summary>Gets the premultiplied colour painted before the start of a gradient: its first stop, which is clear when the gradient is not extended.</summary>
    internal uint Before => Colors.Length > 0 ? Mix(Colors[0], Colors[0], 0F) : 0;

    /// <summary>Gets the premultiplied colour painted after the end of a gradient: its last stop, which is clear when the gradient is not extended.</summary>
    internal uint After => Colors.Length > 0 ? Mix(Colors[^1], Colors[^1], 0F) : 0;

    /// <summary>Gets the image of an image pattern, or <see langword="null"/> for a gradient.</summary>
    internal IPdfRenderImage? Image { get; init; }

    /// <summary>Gets the matrix from an image pattern's pixels to the space the shader is used in.</summary>
    internal Matrix3x2 ImageMatrix { get; init; }

    /// <inheritdoc/>
    public void Dispose()
    {
        // The shader holds only managed objects, which the collector frees.
    }

    /// <summary>Mixes two stop colours and premultiplies the result.</summary>
    /// <param name="from">The colour at weight 0.</param>
    /// <param name="to">The colour at weight 1.</param>
    /// <param name="weight">The weight of the second colour.</param>
    /// <returns>The premultiplied pixel, with blue in the lowest byte.</returns>
    private static uint Mix(PdfColor from, PdfColor to, float weight)
    {
        var alpha = (from.Alpha + ((to.Alpha - from.Alpha) * weight)) / Full;
        var red = (uint)MathF.Round((from.Red + ((to.Red - from.Red) * weight)) * alpha);
        var green = (uint)MathF.Round((from.Green + ((to.Green - from.Green) * weight)) * alpha);
        var blue = (uint)MathF.Round((from.Blue + ((to.Blue - from.Blue) * weight)) * alpha);
        return blue | (green << GreenShift) | (red << RedShift) | ((uint)MathF.Round(alpha * Full) << AlphaShift);
    }

    /// <summary>Works out the colours along the gradient by mixing each pair of stops.</summary>
    /// <returns>The premultiplied colours, or none when the gradient has no stops.</returns>
    private uint[] BuildRamp()
    {
        if (Colors.Length == 0)
        {
            return [];
        }

        var ramp = new uint[RampSteps];
        var stop = 0;
        for (var i = 0; i < ramp.Length; i++)
        {
            var at = i / (float)(ramp.Length - 1);
            while (stop + 1 < Colors.Length - 1 && at > PositionOf(stop + 1))
            {
                stop++;
            }

            var next = Math.Min(stop + 1, Colors.Length - 1);
            var span = PositionOf(next) - PositionOf(stop);
            var weight = span > 0 ? Math.Clamp((at - PositionOf(stop)) / span, 0F, 1F) : 0F;
            ramp[i] = Mix(Colors[stop], Colors[next], weight);
        }

        return ramp;
    }

    /// <summary>Gets where a stop is along the gradient. Stops with no position are spread evenly.</summary>
    /// <param name="stop">The stop.</param>
    /// <returns>The position, 0 to 1.</returns>
    private float PositionOf(int stop) =>
        stop < Positions.Length ? Positions[stop] : stop / (float)Math.Max(1, Colors.Length - 1);
}
