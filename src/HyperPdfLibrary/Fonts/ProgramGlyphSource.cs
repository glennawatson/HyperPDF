// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts;

/// <summary>Glyphs from a font program embedded in the PDF, decoded by the managed font parsers.</summary>
[DebuggerDisplay("ProgramGlyphSource: {Program}")]
internal sealed class ProgramGlyphSource : GlyphSource
{
    /// <summary>The glyph space units per em.</summary>
    private const float GlyphUnits = 1000F;

    /// <summary>The transform from font units to glyph space.</summary>
    private readonly FontMatrix _toGlyphSpace;

    /// <summary>A builder waiting for the next glyph; taken by one thread at a time.</summary>
    private PdfPathBuilder? _spare;

    /// <summary>Initializes a new instance of the <see cref="ProgramGlyphSource"/> class.</summary>
    /// <param name="program">The font program.</param>
    internal ProgramGlyphSource(FontProgram program)
    {
        Program = program;
        var matrix = program.FontMatrix;
        if (matrix.A == 0 && matrix.B == 0 && matrix.C == 0 && matrix.D == 0)
        {
            matrix = FontMatrix.Default;
        }

        _toGlyphSpace = FontMatrix.FromScale(GlyphUnits).Multiply(matrix);
    }

    /// <summary>Gets the font program.</summary>
    internal FontProgram Program { get; }

    /// <inheritdoc/>
    internal override int GlyphCount => Program.GlyphCount;

    /// <inheritdoc/>
    internal override float Ascent => _toGlyphSpace.TransformY(0, Program.Ascent);

    /// <inheritdoc/>
    internal override float Descent => _toGlyphSpace.TransformY(0, Program.Descent);

    /// <inheritdoc/>
    internal override bool IsSubstitute => false;

    /// <inheritdoc/>
    internal override PdfPath? BuildOutline(int glyph, Matrix3x2 transform)
    {
        if ((uint)glyph >= (uint)Program.GlyphCount)
        {
            return null;
        }

        // The builder keeps the room it grew to, so only the finished path's own commands are allocated.
        var builder = Interlocked.Exchange(ref _spare, null) ?? new PdfPathBuilder();
        var sink = new PathOutlineSink(builder, _toGlyphSpace);
        Program.DecodeGlyph(glyph, ref sink);
        if (transform != Matrix3x2.Identity)
        {
            builder.Transform(transform);
        }

        var path = builder.Detach();
        builder.Reset();
        Volatile.Write(ref _spare, builder);
        return path;
    }

    /// <inheritdoc/>
    internal override float GetAdvance(int glyph) => _toGlyphSpace.TransformX(Program.GetAdvanceWidth(glyph), 0) - _toGlyphSpace.E;
}
