// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Text;

/// <summary>
/// A run's writing frame in user space: the unit vector glyphs advance along (the text matrix's x axis, or its negated y
/// axis for vertical writing), the unit vector across it, and the font size in user space units.
/// </summary>
/// <param name="Axis">The direction glyphs advance in.</param>
/// <param name="Normal">The direction across the line, a quarter turn anticlockwise from <paramref name="Axis"/>.</param>
/// <param name="Start">The pen position of the run's first glyph.</param>
/// <param name="Em">The font size in user space units along the advance direction.</param>
[DebuggerDisplay("TextRunFrame: {Start}, axis {Axis}")]
internal readonly record struct TextRunFrame(Vector2 Axis, Vector2 Normal, Vector2 Start, float Em)
{
    /// <summary>The smallest em used as a tolerance, so zero-size text still groups by position.</summary>
    private const float MinimumEm = 0.01F;

    /// <summary>Gets the em as a tolerance base that is never zero.</summary>
    internal float SafeEm => MathF.Max(MathF.Abs(Em), MinimumEm);

    /// <summary>Gets the frame of a run.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The frame.</returns>
    internal static TextRunFrame Of(in TextRun run)
    {
        var matrix = run.Matrix;
        var raw = run.Font.IsVertical ?new Vector2(-matrix.M21, -matrix.M22) : new Vector2(matrix.M11, matrix.M12);

        // A negative font size turns the glyphs half a turn, so they advance against the matrix's axis.
        raw = run.FontSize < 0 ? -raw : raw;
        var length = raw.Length();
        var axis = length > 0 && float.IsFinite(length) ? raw / length : Vector2.UnitX;
        return new(axis, new(-axis.Y, axis.X), matrix.Translation, run.FontSize * (length > 0 && float.IsFinite(length) ? length : 1));
    }
}
