// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>The size of the area an appearance is drawn in, its rotation and its border.</summary>
/// <param name="Width">The width of the drawing area; the widget's height when it is rotated a quarter turn.</param>
/// <param name="Height">The height of the drawing area.</param>
/// <param name="Rotation">The rotation in degrees (<c>/MK /R</c>): 0, 90, 180 or 270.</param>
/// <param name="Border">The border.</param>
[DebuggerDisplay("FormFrame: {Width} x {Height} rotated {Rotation}")]
internal readonly record struct FormFrame(float Width, float Height, int Rotation, FormBorder Border)
{
    /// <summary>The rotation of a quarter turn.</summary>
    private const int QuarterTurn = 90;

    /// <summary>The rotation of a half turn.</summary>
    private const int HalfTurn = 180;

    /// <summary>The rotation of three quarter turns.</summary>
    private const int ThreeQuarterTurns = 270;

    /// <summary>The entries in a matrix.</summary>
    private const int MatrixLength = 6;

    /// <summary>The degrees in a full turn.</summary>
    private const int FullTurn = 360;

    /// <summary>Gets the padding between the border and the text.</summary>
    internal float Padding => Math.Max(1, Border.Width) + 1;

    /// <summary>Gets the width inside the border.</summary>
    internal float InnerWidth => Width - Border.Width - Border.Width;

    /// <summary>Gets the height inside the border.</summary>
    internal float InnerHeight => Height - Border.Width - Border.Width;

    /// <summary>Gets the width available to text.</summary>
    internal float PaddedWidth => Width - Padding - Padding;

    /// <summary>Gets the height available to text.</summary>
    internal float PaddedHeight => Height - Padding - Padding;

    /// <summary>Creates the frame for a widget.</summary>
    /// <param name="context">The widget.</param>
    /// <returns>The frame.</returns>
    internal static FormFrame Create(in AppearanceContext context)
    {
        var rect = context.Rect;
        var mk = context.Characteristics;
        var turn = (((mk?.GetInt32(KnownName.R) ?? 0) % FullTurn) + FullTurn) % FullTurn;
        var rotation = turn is QuarterTurn or HalfTurn or ThreeQuarterTurns ? turn : 0;
        var quarter = rotation is QuarterTurn or ThreeQuarterTurns;
        var hasColor = mk?.GetArray(KnownName.BC) is { Count: > 0 };
        return new(quarter ? rect.Height : rect.Width, quarter ? rect.Width : rect.Height, rotation, FormBorder.Read(context.Widget, hasColor));
    }

    /// <summary>Gets the matrix that turns the drawing area into the widget's rectangle.</summary>
    /// <param name="buffer">Space for the six entries.</param>
    /// <returns>The six entries, in <paramref name="buffer"/>; empty for no rotation.</returns>
    internal ReadOnlySpan<float> GetMatrix(Span<float> buffer)
    {
        switch (Rotation)
        {
            case QuarterTurn:
            {
                return Fill(buffer, 0, 1, -1, 0, Height, 0);
            }

            case HalfTurn:
            {
                return Fill(buffer, -1, 0, 0, -1, Width, Height);
            }

            case ThreeQuarterTurns:
            {
                return Fill(buffer, 0, -1, 1, 0, 0, Width);
            }

            default:
            {
                return default;
            }
        }
    }

    /// <summary>Writes a matrix into a buffer.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="a">The a entry.</param>
    /// <param name="b">The b entry.</param>
    /// <param name="c">The c entry.</param>
    /// <param name="d">The d entry.</param>
    /// <param name="e">The e entry.</param>
    /// <param name="f">The f entry.</param>
    /// <returns>The six entries.</returns>
    private static ReadOnlySpan<float> Fill(Span<float> buffer, float a, float b, float c, float d, float e, float f)
    {
        ReadOnlySpan<float> entries = [a, b, c, d, e, f];
        entries.CopyTo(buffer);
        return buffer[..MatrixLength];
    }
}
