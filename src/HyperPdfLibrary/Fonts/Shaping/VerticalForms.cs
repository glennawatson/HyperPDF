// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Finds the form a font draws a glyph in when text is written vertically: its GSUB <c>vert</c> and <c>vrt2</c> substitutions turn
/// brackets, punctuation and the long vowel mark into the upright forms that suit a column of text (OpenType specification, <c>vert</c>).
/// </summary>
[DebuggerDisplay("VerticalForms: {_layout.Substitution.LookupCount} substitutions")]
internal sealed class VerticalForms
{
    /// <summary>The script that decides the substitutions for characters that belong to none, such as brackets and punctuation.</summary>
    private static readonly ShapingScript FallbackScript = UnicodeScripts.Named("Hani");

    /// <summary>The font's layout tables.</summary>
    private readonly OtFontLayout _layout;

    /// <summary>The reusable glyph buffer and layout engines.</summary>
    private ShapingSession? _spare;

    /// <summary>Initializes a new instance of the <see cref="VerticalForms"/> class.</summary>
    /// <param name="substitution">The font's GSUB table.</param>
    /// <param name="definitions">The font's GDEF table, or empty.</param>
    internal VerticalForms(ReadOnlyMemory<byte> substitution, ReadOnlyMemory<byte> definitions) => _layout = new(substitution, definitions);

    /// <summary>Gets a value indicating whether the font has any substitution to apply.</summary>
    internal bool IsPresent => _layout.Substitution.IsPresent;

    /// <summary>Finds a glyph's vertical form.</summary>
    /// <param name="glyph">The glyph drawn for horizontal text.</param>
    /// <param name="codePoint">The character the glyph shows, which picks the script's substitutions.</param>
    /// <returns>The vertical form, or <paramref name="glyph"/> when the font has none.</returns>
    internal int Substitute(int glyph, int codePoint)
    {
        var script = UnicodeScripts.Of(codePoint) is { IsCommon: false } own ? own : FallbackScript;
        var plan = _layout.PlanFor(script, 0, false, true, null);
        var session = ShapingSession.Rent(ref _spare);
        try
        {
            var buffer = session.Buffer;
            buffer.Clear(1);
            var definitions = _layout.Definitions;
            buffer.Add(new(glyph, 0, codePoint) { Mask = ShapeMasks.Global, GlyphClass = definitions.HasGlyphClasses ? definitions.GlyphClass(glyph) : 1 });
            var engine = session.Substitute(_layout.Substitution, definitions, false);
            foreach (var stage in plan.Substitution)
            {
                engine.RunStage(stage);
            }

            return buffer.Count == 1 ? buffer.Items[0].Glyph : glyph;
        }
        finally
        {
            session.Return(ref _spare);
        }
    }
}
