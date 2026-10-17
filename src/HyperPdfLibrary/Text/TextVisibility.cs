// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;

namespace HyperPdfLibrary.Text;

/// <summary>Rejects text that cannot paint, without attempting raster visibility or occlusion tests.</summary>
internal static class TextVisibility
{
    /// <summary>The paint component bits shared by render modes with and without text clipping.</summary>
    private const int PaintModeMask = 3;

    /// <summary>The render mode that strokes text.</summary>
    private const int StrokeMode = 1;

    /// <summary>The render mode that fills and strokes text.</summary>
    private const int FillStrokeMode = 2;

    /// <summary>The mask for cancellation checks while scanning path commands.</summary>
    private const int CancellationMask = 1023;

    /// <summary>Determines whether a glyph has any possible paint under its current state.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="state">The graphics state.</param>
    /// <param name="clip">The conservative clipping bounds.</param>
    /// <param name="matrix">The glyph's transformation.</param>
    /// <returns>Whether some paint remains possible.</returns>
    internal static bool CanPaint(in GlyphEvent glyph, ref GraphicsState state, in PdfRect clip, Matrix3x2 matrix)
    {
        if (glyph.FontSize == 0 || state.HorizontalScaling == 0 || clip.IsEmpty || matrix.GetDeterminant() == 0)
        {
            return false;
        }

        return (state.RenderMode & PaintModeMask) switch
        {
            0 => state.FillAlpha > 0,
            StrokeMode => state.StrokeAlpha > 0,
            FillStrokeMode => state.FillAlpha > 0 || state.StrokeAlpha > 0,
            _ => false,
        };
    }

    /// <summary>Intersects clipping bounds, also rejecting paths whose points all lie on one line per subpath.</summary>
    /// <param name="current">The previous bounds.</param>
    /// <param name="path">The new clipping path.</param>
    /// <param name="matrix">The path transformation.</param>
    /// <param name="cancellationToken">Cancels path inspection.</param>
    /// <returns>Conservative bounds, or empty when no area remains.</returns>
    internal static PdfRect Intersect(in PdfRect current, PdfPath path, Matrix3x2 matrix, CancellationToken cancellationToken)
    {
        if (current.IsEmpty || path.IsEmpty || path.Bounds.IsEmpty || matrix.GetDeterminant() == 0 || !HasArea(path.Commands, cancellationToken))
        {
            return PdfRect.Empty;
        }

        var bounds = matrix.MapRect(path.Bounds);
        return new(MathF.Max(current.Left, bounds.Left), MathF.Max(current.Top, bounds.Top), MathF.Min(current.Right, bounds.Right), MathF.Min(current.Bottom, bounds.Bottom));
    }

    /// <summary>Finds a subpath with non-collinear endpoints or control points. Curves and self-intersections may still overestimate area.</summary>
    /// <param name="commands">The path commands.</param>
    /// <param name="cancellationToken">Cancels the bounded scan.</param>
    /// <returns>Whether a filled area is possible.</returns>
    private static bool HasArea(ReadOnlySpan<PdfPathCommand> commands, CancellationToken cancellationToken)
    {
        var origin = Vector2.Zero;
        var axis = Vector2.Zero;
        for (var commandIndex = 0; commandIndex < commands.Length; commandIndex++)
        {
            if ((commandIndex & CancellationMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var command = commands[commandIndex];
            if (command.Kind == PdfPathCommandKind.MoveTo)
            {
                origin = new(command.Point.X, command.Point.Y);
                axis = Vector2.Zero;
                continue;
            }

            for (var i = 0; i < command.PointCount; i++)
            {
                var point = command.GetPoint(i);
                var direction = new Vector2(point.X, point.Y) - origin;
                if (axis == Vector2.Zero)
                {
                    axis = direction;
                }
                else if ((axis.X * direction.Y) - (axis.Y * direction.X) != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
