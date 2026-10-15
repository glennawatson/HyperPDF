// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Owns unfinished fonts and compressed font programs for one document sink.</summary>
[DebuggerDisplay("Font embedding state")]
public sealed class PdfFontEmbeddingState
{
    /// <summary>The compressed font programs, published as immutable snapshots.</summary>
    private FontStreamCacheState _streams = FontStreamCacheState.Empty;

    /// <summary>The idle shaped-glyph list.</summary>
    private List<PdfShapedGlyph>? _glyphs;

    /// <summary>The idle cluster-planning list.</summary>
    private List<ClusterGroup>? _groups;

    /// <summary>The idle glyph ordering list.</summary>
    private List<int>? _glyphOrder;

    /// <summary>The idle font table list.</summary>
    private List<SfntEntry>? _fontTables;

    /// <summary>Gets the compressed-program snapshot slot.</summary>
    internal ref FontStreamCacheState Streams => ref _streams;

    /// <summary>Gets the idle shaped-glyph list slot.</summary>
    internal ref List<PdfShapedGlyph>? Glyphs => ref _glyphs;

    /// <summary>Gets the idle cluster-planning list slot.</summary>
    internal ref List<ClusterGroup>? Groups => ref _groups;

    /// <summary>Gets the idle glyph-order list slot.</summary>
    internal ref List<int>? GlyphOrder => ref _glyphOrder;

    /// <summary>Gets the idle font-table list slot.</summary>
    internal ref List<SfntEntry>? FontTables => ref _fontTables;

    /// <summary>Gets the lock guarding unfinished fonts.</summary>
    internal Lock Gate { get; } = new();

    /// <summary>Gets fonts awaiting completion.</summary>
    internal List<PdfEmbeddedFont> Pending { get; } = [];
}
