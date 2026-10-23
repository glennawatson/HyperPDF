// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets.Shaping;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Fonts.Shaping;

/// <summary>Builds a small test font with layout tables made in the test, and shapes text with the library's OpenType shaper.</summary>
internal sealed class ShapingFixture
{
    /// <summary>The default advance of a glyph, in font units.</summary>
    internal const int Advance = 500;

    /// <summary>The letter each glyph is shown as in results.</summary>
    private readonly Dictionary<int, char> _names = [];

    /// <summary>The glyph of each letter.</summary>
    private readonly Dictionary<char, int> _ids = [];

    /// <summary>The indexes of the GSUB features of the default script.</summary>
    private readonly List<int> _gsubFeatures = [];

    /// <summary>The indexes of the GPOS features of the default script.</summary>
    private readonly List<int> _gposFeatures = [];

    /// <summary>Whether the default scripts were added to the tables.</summary>
    private bool _scriptsAdded;

    /// <summary>Gets the font builder.</summary>
    internal ShapingFontBuilder Font { get; } = new();

    /// <summary>Gets the substitution table builder.</summary>
    internal LayoutTableBuilder Gsub { get; } = new();

    /// <summary>Gets the positioning table builder.</summary>
    internal LayoutTableBuilder Gpos { get; } = new();

    /// <summary>Adds a GSUB feature to the default script.</summary>
    /// <param name="tag">The feature tag.</param>
    /// <param name="lookups">The lookups it uses.</param>
    internal void UseGsub(string tag, params int[] lookups) => _gsubFeatures.Add(Gsub.AddFeature(tag, lookups));

    /// <summary>Adds a GPOS feature to the default script.</summary>
    /// <param name="tag">The feature tag.</param>
    /// <param name="lookups">The lookups it uses.</param>
    internal void UseGpos(string tag, params int[] lookups) => _gposFeatures.Add(Gpos.AddFeature(tag, lookups));

    /// <summary>Adds a glyph for a character, shown in results as that character.</summary>
    /// <param name="character">The character.</param>
    /// <returns>The glyph.</returns>
    internal int Add(char character) => AddMapped(character, character, Advance);

    /// <summary>Adds glyphs for some characters.</summary>
    /// <param name="characters">The characters.</param>
    internal void AddAll(string characters)
    {
        foreach (var character in characters)
        {
            _ = Add(character);
        }
    }

    /// <summary>Adds a glyph that no character maps to, shown in results as a letter.</summary>
    /// <param name="name">The letter the glyph is shown as.</param>
    /// <returns>The glyph.</returns>
    internal int AddUnmapped(char name) => AddMapped(name, 0, Advance);

    /// <summary>Adds a glyph with its own advance.</summary>
    /// <param name="name">The letter the glyph is shown as.</param>
    /// <param name="codePoint">The character that maps to it, or zero.</param>
    /// <param name="advance">The advance in font units.</param>
    /// <returns>The glyph.</returns>
    internal int AddMapped(char name, int codePoint, int advance)
    {
        var id = Font.AddGlyph(codePoint, advance);
        _names[id] = name;
        _ids[name] = id;
        return id;
    }

    /// <summary>Gets the glyph shown as a letter.</summary>
    /// <param name="name">The letter.</param>
    /// <returns>The glyph.</returns>
    internal int Id(char name) => _ids[name];

    /// <summary>Gets the glyphs shown as some letters.</summary>
    /// <param name="names">The letters.</param>
    /// <returns>The glyphs.</returns>
    internal int[] Ids(string names) => [.. names.Select(Id)];

    /// <summary>Writes the font file with the layout tables that have lookups.</summary>
    /// <returns>The font.</returns>
    internal byte[] Build()
    {
        if (!_scriptsAdded)
        {
            _scriptsAdded = true;
            AddDefaultScript(Gsub, _gsubFeatures);
            AddDefaultScript(Gpos, _gposFeatures);
        }

        if (!Gsub.IsEmpty)
        {
            Font.WithTable("GSUB", Gsub.Build());
        }

        if (!Gpos.IsEmpty)
        {
            Font.WithTable("GPOS", Gpos.Build());
        }

        return Font.Build();
    }

    /// <summary>Shapes text and describes the result.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The result.</returns>
    internal ShapedText Shape(string text) => Shape(text, PdfShapingOptions.Default);

    /// <summary>Shapes text and describes the result.</summary>
    /// <param name="text">The text.</param>
    /// <param name="options">The shaping options.</param>
    /// <returns>The result.</returns>
    /// <exception cref="InvalidOperationException">The font was refused.</exception>
    internal ShapedText Shape(string text, PdfShapingOptions options)
    {
        var source = PdfFontSource.Open(Build()).Source ?? throw new InvalidOperationException("The test font was refused.");
        var shaper = new PdfOpenTypeShaper(source);
        var glyphs = new List<PdfShapedGlyph>();
        var rightToLeft = shaper.Shape(text, options, glyphs);
        return new(rightToLeft, [.. glyphs], this);
    }

    /// <summary>Gets the letter a glyph is shown as.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The letter, or a question mark for a glyph that was not added.</returns>
    internal char NameOf(int glyph) => _names.GetValueOrDefault(glyph, '?');

    /// <summary>Adds the default script with some features to a table.</summary>
    /// <param name="table">The table.</param>
    /// <param name="features">The feature indexes; nothing is added when there are none.</param>
    private static void AddDefaultScript(LayoutTableBuilder table, List<int> features)
    {
        if (features.Count > 0)
        {
            table.AddScript("DFLT", [.. features]);
        }
    }
}
