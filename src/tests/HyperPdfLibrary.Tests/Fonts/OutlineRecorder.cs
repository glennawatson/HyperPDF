// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Records outline commands as text, and the bounds of every point, for assertions.</summary>
internal sealed class OutlineRecorder : IGlyphOutlineSink
{
    /// <summary>Gets the commands, such as <c>M 0 0</c> or <c>C 1 2 3 4 5 6</c>.</summary>
    internal List<string> Commands { get; } = [];

    /// <summary>Gets the smallest x of any point.</summary>
    internal float MinX { get; private set; } = float.MaxValue;

    /// <summary>Gets the smallest y of any point.</summary>
    internal float MinY { get; private set; } = float.MaxValue;

    /// <summary>Gets the largest x of any point.</summary>
    internal float MaxX { get; private set; } = float.MinValue;

    /// <summary>Gets the largest y of any point.</summary>
    internal float MaxY { get; private set; } = float.MinValue;

    /// <summary>Gets the number of closed contours.</summary>
    internal int Contours { get; private set; }

    /// <inheritdoc/>
    public void MoveTo(float x, float y) => Add("M", x, y);

    /// <inheritdoc/>
    public void LineTo(float x, float y) => Add("L", x, y);

    /// <inheritdoc/>
    public void QuadraticTo(float controlX, float controlY, float x, float y)
    {
        Include(controlX, controlY);
        Add($"Q {Format(controlX)} {Format(controlY)}", x, y);
    }

    /// <inheritdoc/>
    public void CubicTo(float control1X, float control1Y, float control2X, float control2Y, float x, float y)
    {
        Include(control1X, control1Y);
        Include(control2X, control2Y);
        Add($"C {Format(control1X)} {Format(control1Y)} {Format(control2X)} {Format(control2Y)}", x, y);
    }

    /// <inheritdoc/>
    public void Close()
    {
        Commands.Add("Z");
        Contours++;
    }

    /// <summary>Formats a coordinate without culture effects.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    private static string Format(float value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Records a command ending at a point.</summary>
    /// <param name="prefix">The command text before the point.</param>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    private void Add(string prefix, float x, float y)
    {
        Include(x, y);
        Commands.Add($"{prefix} {Format(x)} {Format(y)}");
    }

    /// <summary>Grows the bounds to include a point.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    private void Include(float x, float y)
    {
        MinX = Math.Min(MinX, x);
        MinY = Math.Min(MinY, y);
        MaxX = Math.Max(MaxX, x);
        MaxY = Math.Max(MaxY, y);
    }
}
