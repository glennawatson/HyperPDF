// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Tests.Fonts.Embedding;

/// <summary>Writes a glyph's outline as text so two outlines can be compared.</summary>
internal sealed class OutlineRecorder : IGlyphOutlineSink
{
    /// <summary>The outline so far.</summary>
    private readonly StringBuilder _text = new();

    /// <inheritdoc/>
    public void MoveTo(float x, float y) => Add('M', x, y);

    /// <inheritdoc/>
    public void LineTo(float x, float y) => Add('L', x, y);

    /// <inheritdoc/>
    public void QuadraticTo(float controlX, float controlY, float x, float y) => Add('Q', controlX, controlY, x, y);

    /// <inheritdoc/>
    public void CubicTo(float control1X, float control1Y, float control2X, float control2Y, float x, float y) =>
        Add('C', control1X, control1Y, control2X, control2Y, x, y);

    /// <inheritdoc/>
    public void Close() => _ = _text.Append('Z');

    /// <summary>Decodes a glyph of a CFF font to text.</summary>
    /// <param name="program">The font.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The outline as text.</returns>
    internal static string Record(CffProgram program, int glyph)
    {
        var recorder = new OutlineRecorder();
        program.DecodeGlyph(glyph, ref recorder);
        return recorder._text.ToString();
    }

    /// <summary>Adds a command and its numbers.</summary>
    /// <param name="command">The command letter.</param>
    /// <param name="values">The numbers.</param>
    private void Add(char command, params ReadOnlySpan<float> values)
    {
        _ = _text.Append(command);
        foreach (var value in values)
        {
            _ = _text.Append(value.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ');
        }
    }
}
