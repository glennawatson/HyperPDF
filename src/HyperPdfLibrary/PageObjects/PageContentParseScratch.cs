// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace HyperPdfLibrary.PageObjects;

/// <summary>
/// The lists and buffers one content parse works in. A document keeps one between reads, so reading a page again reuses
/// their memory; a read takes it with <see cref="System.Threading.Interlocked"/> and a read that finds it taken makes its own.
/// </summary>
[DebuggerDisplay("PageContentParseScratch: {Objects.Count} objects")]
internal sealed class PageContentParseScratch
{
    /// <summary>The most elements a list may hold between reads before it is dropped, so one huge page does not pin memory.</summary>
    private const int KeptCapacity = 1 << 16;

    /// <summary>The interpreter state, made when first used.</summary>
    private PageContentParseState? _parser;

    /// <summary>Gets the interpreter state, which a read binds to its content and which is let go of after it.</summary>
    internal PageContentParseState Parser => _parser ??= new(this);

    /// <summary>Gets the objects read so far.</summary>
    internal List<PdfPageObject> Objects { get; private set; } = [with(PageContentParse.ObjectCapacity)];

    /// <summary>Gets the saved graphics states.</summary>
    internal List<ParseState> Stack { get; } = [with(PageContentParse.StackCapacity)];

    /// <summary>Gets the marked-content marks that are open.</summary>
    internal List<PdfMark> Marks { get; } = [];

    /// <summary>Gets the segments of the path being built.</summary>
    internal List<PdfPathSegment> Segments { get; private set; } = [with(PageContentParse.SegmentCapacity)];

    /// <summary>Gets the glyphs of the text being shown.</summary>
    internal List<PdfTextGlyph> Glyphs { get; private set; } = [];

    /// <summary>Gets the code bytes of the text being shown.</summary>
    internal List<byte> CodeBytes { get; } = [];

    /// <summary>Gets the characters of the text being shown.</summary>
    internal StringBuilder Text { get; } = new();

    /// <summary>Empties everything and lets go of what is too big to keep, ready for the next read.</summary>
    internal void Clear()
    {
        _parser?.Unbind();
        Objects.Clear();
        Objects = Objects.Capacity > KeptCapacity ? [with(PageContentParse.ObjectCapacity)] : Objects;
        Stack.Clear();
        Marks.Clear();
        Segments.Clear();
        Segments = Segments.Capacity > KeptCapacity ? [with(PageContentParse.SegmentCapacity)] : Segments;
        Glyphs.Clear();
        Glyphs = Glyphs.Capacity > KeptCapacity ? [] : Glyphs;
        CodeBytes.Clear();
        _ = Text.Clear();
    }
}
