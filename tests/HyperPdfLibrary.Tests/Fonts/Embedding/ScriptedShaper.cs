// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Fonts.Embedding;

/// <summary>A shaper that answers chosen texts with chosen glyphs, as a shaping engine would for ligatures, reordering and right-to-left text; other text gets one glyph per character.</summary>
internal sealed class ScriptedShaper : IPdfGlyphShaper
{
    /// <summary>The scripted answers by text.</summary>
    private readonly Dictionary<string, ScriptedRun> _runs = [];

    /// <summary>The shaper for text that is not scripted.</summary>
    private readonly PdfBasicShaper _basic;

    /// <summary>Initializes a new instance of the <see cref="ScriptedShaper"/> class.</summary>
    /// <param name="source">The font that unscripted text is shaped with.</param>
    internal ScriptedShaper(PdfFontSource source) => _basic = new(source);

    /// <inheritdoc/>
    public bool Shape(ReadOnlySpan<char> text, PdfShapingOptions options, List<PdfShapedGlyph> output)
    {
        if (!_runs.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(text, out var run))
        {
            return _basic.Shape(text, options, output);
        }

        output.AddRange(run.Glyphs);
        return run.RightToLeft;
    }

    /// <summary>Scripts the answer for a text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="rightToLeft">Whether the run reads right to left.</param>
    /// <param name="glyphs">The glyphs in drawing order.</param>
    /// <returns>This shaper.</returns>
    internal ScriptedShaper Script(string text, bool rightToLeft, params PdfShapedGlyph[] glyphs)
    {
        _runs[text] = new(glyphs, rightToLeft);
        return this;
    }

    /// <summary>One scripted answer.</summary>
    /// <param name="Glyphs">The glyphs in drawing order.</param>
    /// <param name="RightToLeft">Whether the run reads right to left.</param>
    private sealed record ScriptedRun(PdfShapedGlyph[] Glyphs, bool RightToLeft);
}
