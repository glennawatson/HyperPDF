// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// Paints with a gradient along a line or between two circles. The colours between the stops are worked out once into a table, and
/// each pixel then looks its colour up by how far along the gradient it is.
/// </summary>
[DebuggerDisplay("GradientShader: radial {_radial}")]
internal sealed class GradientShader
{
    /// <summary>The largest byte, which stands for one.</summary>
    private const uint Full = 255;

    /// <summary>The centre of a pixel.</summary>
    private const float Half = 0.5F;

    /// <summary>The length below which a gradient has no direction or the circles' equation has no square term.</summary>
    private const float Tiny = 1E-6F;

    /// <summary>The gradient.</summary>
    private ManagedRenderShader? _gradient;

    /// <summary>The colours along the gradient, premultiplied.</summary>
    private uint[] _ramp = [];

    /// <summary>The colour painted before the start of the gradient.</summary>
    private uint _before;

    /// <summary>The colour painted after the end of the gradient.</summary>
    private uint _after;

    /// <summary>The matrix from device pixels to the gradient's space.</summary>
    private Matrix3x2 _toGradient;

    /// <summary>How opaque the paint is, where 255 is fully.</summary>
    private uint _alpha;

    /// <summary>Whether the gradient runs between two circles rather than along a line.</summary>
    private bool _radial;

    /// <summary>Sets what the shader paints.</summary>
    /// <param name="gradient">The gradient.</param>
    /// <param name="toGradient">The matrix from device pixels to the gradient's space.</param>
    /// <param name="alpha">How opaque the paint is, where 255 is fully.</param>
    internal void Set(ManagedRenderShader gradient, Matrix3x2 toGradient, uint alpha)
    {
        _gradient = gradient;
        _ramp = gradient.Ramp;
        _before = gradient.Before;
        _after = gradient.After;
        _toGradient = toGradient;
        _alpha = alpha;
        _radial = gradient.StartRadius >= 0;
    }

    /// <summary>Works out the colours of a run of pixels on one line.</summary>
    /// <param name="x">The first pixel of the run, in device pixels.</param>
    /// <param name="y">The line, in device pixels.</param>
    /// <param name="output">Receives the premultiplied colour of each pixel of the run.</param>
    internal void Shade(int x, int y, Span<uint> output)
    {
        if (_gradient is not { } gradient || _ramp.Length == 0)
        {
            output.Clear();
            return;
        }

        var position = Vector2.Transform(new(x + Half, y + Half), _toGradient);
        var step = new Vector2(_toGradient.M11, _toGradient.M12);
        var start = new Vector2(gradient.Start.X, gradient.Start.Y);
        var axis = new Vector2(gradient.End.X, gradient.End.Y) - start;
        var lengthSquared = axis.LengthSquared();
        for (var i = 0; i < output.Length; i++)
        {
            var along = _radial ? Radial(gradient, position - start, axis) : Linear(position - start, axis, lengthSquared);
            output[i] = Look(along, gradient.Mode);
            position += step;
        }
    }

    /// <summary>Finds how far along a line a point is.</summary>
    /// <param name="point">The point, from the start of the line.</param>
    /// <param name="axis">The line, from its start to its end.</param>
    /// <param name="lengthSquared">The square of the line's length.</param>
    /// <returns>The distance, where 0 is the start and 1 the end.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Linear(Vector2 point, Vector2 axis, float lengthSquared) => lengthSquared > Tiny ? Vector2.Dot(point, axis) / lengthSquared : 0F;

    /// <summary>Finds the circle between two circles that a point lies on.</summary>
    /// <param name="gradient">The gradient, with its two radii.</param>
    /// <param name="point">The point, from the centre of the first circle.</param>
    /// <param name="axis">The line from the first centre to the second.</param>
    /// <returns>How far between the circles that circle is, or a value that is not a number when no circle with a radius reaches the point.</returns>
    private static float Radial(ManagedRenderShader gradient, Vector2 point, Vector2 axis)
    {
        // The circle at t has centre start + t * axis and radius r0 + t * grow. The point is on it when
        // a t² - 2 b t + c = 0, and the larger t whose radius is not negative is the one painted.
        var grow = gradient.EndRadius - gradient.StartRadius;
        var a = axis.LengthSquared() - (grow * grow);
        var b = Vector2.Dot(point, axis) + (gradient.StartRadius * grow);
        var c = point.LengthSquared() - (gradient.StartRadius * gradient.StartRadius);
        if (MathF.Abs(a) < Tiny)
        {
            return MathF.Abs(b) < Tiny ? float.NaN : c / (b + b);
        }

        var root = (b * b) - (a * c);
        if (root < 0)
        {
            return float.NaN;
        }

        root = MathF.Sqrt(root);
        var far = (b + root) / a;
        var near = (b - root) / a;
        var larger = MathF.Max(far, near);
        return gradient.StartRadius + (larger * grow) >= 0 ? larger : MathF.Min(far, near);
    }

    /// <summary>Looks up the colour at a distance along the gradient.</summary>
    /// <param name="along">The distance, where 0 is the start and 1 the end.</param>
    /// <param name="mode">What is painted before the start and after the end.</param>
    /// <returns>The premultiplied colour.</returns>
    private uint Look(float along, PdfShaderTileMode mode)
    {
        if (float.IsNaN(along) || (mode == PdfShaderTileMode.Decal && (along < 0 || along > 1)))
        {
            return 0;
        }

        if (mode == PdfShaderTileMode.Repeat)
        {
            along -= MathF.Floor(along);
        }
        else if (mode == PdfShaderTileMode.Mirror)
        {
            // The gradient runs forwards, then backwards, and so on.
            along = MathF.Abs(along - ((1 + 1) * MathF.Floor((along * Half) + Half)));
        }

        // Past either end the end stop is painted. The library puts a clear stop there for a gradient that is not extended.
        var color = _ramp[(int)(Math.Clamp(along, 0F, 1F) * (_ramp.Length - 1))];
        color = along < 0 ? _before : color;
        color = along > 1 ? _after : color;
        return _alpha == Full ? color : SpanBlender.Scale(color, _alpha);
    }
}
