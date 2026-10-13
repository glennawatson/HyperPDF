// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Text;

/// <summary>Owns the reusable buffers and the running values of a text page build.</summary>
[DebuggerDisplay("TextPageBuildState: {Chars.Count} chars")]
internal sealed class TextPageBuildState
{
    /// <summary>Initializes a new instance of the <see cref="TextPageBuildState"/> class.</summary>
    internal TextPageBuildState()
    {
        Runs = Device.Runs;
        Glyphs = Device.Glyphs;
        Unicode = Device.Unicode;
    }

    /// <summary>Gets the characters of the page, in reading order.</summary>
    internal List<TextBuildChar> Chars { get; } = [];

    /// <summary>Gets the characters of the line being built, in the order they sit on the page.</summary>
    internal List<TextBuildChar> LineChars { get; } = [];

    /// <summary>Gets the characters of the line being built, in logical order after bidirectional reordering.</summary>
    internal List<TextBuildChar> LineLogical { get; } = [];

    /// <summary>Gets the runs of the line being built, in the order they sit on the page.</summary>
    internal List<int> Order { get; } = [];

    /// <summary>Gets the glyphs most recently added to the line, for finding glyphs drawn twice.</summary>
    internal List<RecentGlyph> Recent { get; } = [];

    /// <summary>Gets the frame of each run.</summary>
    internal List<TextRunFrame> Frames { get; } = [];

    /// <summary>Gets the index in <see cref="LineChars"/> where each glyph group starts, and a final end index.</summary>
    internal List<int> GroupStarts { get; } = [];

    /// <summary>Gets the bidirectional class of each glyph group.</summary>
    internal List<TextBidiClass> GroupTypes { get; } = [];

    /// <summary>Gets the embedding level of each glyph group.</summary>
    internal List<byte> GroupLevels { get; } = [];

    /// <summary>Gets the display order of the glyph groups.</summary>
    internal List<int> GroupOrder { get; } = [];

    /// <summary>Gets the share of the font size each font's space is wide, cached by font.</summary>
    internal Dictionary<PdfFont, float> SpaceWidths { get; } = [];

    /// <summary>Gets the replacement-text sequences already placed on the page.</summary>
    internal HashSet<int> PlacedSequences { get; } = [];

    /// <summary>Gets the runs collected by <see cref="Device"/>.</summary>
    internal List<TextRun> Runs { get; }

    /// <summary>Gets the glyphs collected by <see cref="Device"/>.</summary>
    internal List<TextGlyph> Glyphs { get; }

    /// <summary>Gets the Unicode characters collected by <see cref="Device"/>.</summary>
    internal List<char> Unicode { get; }

    /// <summary>Gets the device that collects the page's glyphs.</summary>
    internal TextDevice Device { get; } = new();

    /// <summary>Gets or sets a value indicating whether the document asks for right-to-left reading order.</summary>
    internal bool RightToLeft { get; set; }

    /// <summary>Gets or sets the advance direction of the line being built.</summary>
    internal Vector2 LineAxis { get; set; } = Vector2.UnitX;

    /// <summary>Gets or sets the index of the line being built.</summary>
    internal int LineIndex { get; set; }

    /// <summary>Gets or sets the number of glyph groups started in the line being built.</summary>
    internal int GroupCount { get; set; }

    /// <summary>Gets or sets a value indicating whether the line being built has a glyph whose end can be measured from.</summary>
    internal bool HasPrevious { get; set; }

    /// <summary>Gets or sets the pen position after the last glyph of the line being built.</summary>
    internal Vector2 PreviousEnd { get; set; }

    /// <summary>Gets or sets the font size in user space of the last glyph of the line being built.</summary>
    internal float PreviousEm { get; set; }

    /// <summary>Gets or sets the font of the last glyph of the line being built.</summary>
    internal PdfFont? PreviousFont { get; set; }

    /// <summary>Resets the values and buffers of a build.</summary>
    internal void Clear()
    {
        Chars.Clear();
        LineChars.Clear();
        LineLogical.Clear();
        Order.Clear();
        Recent.Clear();
        Frames.Clear();
        GroupStarts.Clear();
        GroupTypes.Clear();
        GroupLevels.Clear();
        GroupOrder.Clear();
        SpaceWidths.Clear();
        PlacedSequences.Clear();
        Device.Clear();
        RightToLeft = false;
        LineAxis = Vector2.UnitX;
        LineIndex = 0;
        GroupCount = 0;
        HasPrevious = false;
        PreviousEnd = default;
        PreviousEm = 0;
        PreviousFont = null;
    }
}
