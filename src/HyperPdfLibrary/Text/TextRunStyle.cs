// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Text;

/// <summary>What a text page keeps of a run after it is built: the colours, matrix and font facts, without the font itself.</summary>
/// <param name="Matrix">The matrix that maps the run's text space to user space.</param>
/// <param name="FillColor">The fill colour as 0xRRGGBB.</param>
/// <param name="StrokeColor">The stroke colour as 0xRRGGBB.</param>
/// <param name="FontName">The font's name.</param>
/// <param name="FontWeight">The font's weight.</param>
/// <param name="FontFlags">The font's flags.</param>
[DebuggerDisplay("TextRunStyle: {FontName} {FontWeight}")]
internal readonly record struct TextRunStyle(
    Matrix3x2 Matrix,
    uint FillColor,
    uint StrokeColor,
    string FontName,
    int FontWeight,
    FontFlags FontFlags)
{
    /// <summary>The degrees in one radian.</summary>
    private const float DegreesPerRadian = 180 / MathF.PI;

    /// <summary>Gets the style of a character that has no run: black text on the identity matrix.</summary>
    internal static TextRunStyle None { get; } = new(Matrix3x2.Identity, 0, 0, string.Empty, TextFontFacts.None.Weight, FontFlags.None);

    /// <summary>Gets the angle of the baseline in degrees, counter-clockwise from the positive x axis.</summary>
    internal float Angle => MathF.Atan2(Matrix.M12, Matrix.M11) * DegreesPerRadian;
}
