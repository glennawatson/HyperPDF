// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Owns one content-stream parse and its mutable interpreter state.</summary>
[DebuggerDisplay("PageContentParser: {_objects.Count} objects")]
internal sealed class PageContentParseState
{
    /// <summary>The lists and buffers the parse works in, borrowed for the read.</summary>
    private readonly PageContentParseScratch _scratch;

    /// <summary>The page's caches.</summary>
    private PdfRenderCache? _cache;

    /// <summary>The name table operands are interned in.</summary>
    private PdfNameTable? _names;

    /// <summary>The content the objects belong to.</summary>
    private PdfPageContent? _owner;

    /// <summary>The decoded content.</summary>
    private byte[]? _content;

    /// <summary>The resources named in the content.</summary>
    private PdfDictionary? _resources;

    /// <summary>The current state.</summary>
    private ParseState _state;

    /// <summary>The marks as an array, made when an object needs them and dropped when they change.</summary>
    private PdfMark[]? _markSnapshot;

    /// <summary>The offset where the current operator and its operands start.</summary>
    private int _operatorStart;

    /// <summary>The offset after the current operator.</summary>
    private int _operatorEnd;

    /// <summary>Restores that had no state to restore.</summary>
    private int _underflow;

    /// <summary>The offset where the path being built starts, or -1 while there is none.</summary>
    private int _pathStart = -1;

    /// <summary>The clip the path being built asks for when it is painted.</summary>
    private PdfClipMode _pendingClip;

    /// <summary>The current point.</summary>
    private Vector2 _current;

    /// <summary>The start of the current subpath.</summary>
    private Vector2 _subpathStart;

    /// <summary>The text matrix.</summary>
    private Matrix3x2 _tm = Matrix3x2.Identity;

    /// <summary>The text line matrix.</summary>
    private Matrix3x2 _tlm = Matrix3x2.Identity;

    /// <summary>How far the text matrix stands from the line matrix, along the text axes.</summary>
    private Vector2 _position;

    /// <summary>The <c>TJ</c> numbers read since the last glyph.</summary>
    private float _kerning;

    /// <summary>The total distance the show moved the text position along its writing direction.</summary>
    private float _showAdvance;

    /// <summary>Initializes a new instance of the <see cref = "PageContentParseState"/> class, unbound until <see cref = "Bind"/> is called.</summary>
    /// <param name = "scratch">The lists and buffers the parse works in.</param>
    internal PageContentParseState(PageContentParseScratch scratch) => _scratch = scratch;

    /// <summary>Gets the Cache state.</summary>
    internal PdfRenderCache Cache => _cache ?? throw Unbound();

    /// <summary>Gets the Names state.</summary>
    internal PdfNameTable Names => _names ?? throw Unbound();

    /// <summary>Gets the Owner state.</summary>
    internal PdfPageContent Owner => _owner ?? throw Unbound();

    /// <summary>Gets the Content state.</summary>
    internal byte[] Content => _content ?? throw Unbound();

    /// <summary>Gets the Resources state.</summary>
    internal PdfDictionary? Resources => _resources;

    /// <summary>Gets the Objects state.</summary>
    internal List<PdfPageObject> Objects => _scratch.Objects;

    /// <summary>Gets the Stack state.</summary>
    internal List<ParseState> Stack => _scratch.Stack;

    /// <summary>Gets the Marks state.</summary>
    internal List<PdfMark> Marks => _scratch.Marks;

    /// <summary>Gets the Segments state.</summary>
    internal List<PdfPathSegment> Segments => _scratch.Segments;

    /// <summary>Gets the current graphics state by reference.</summary>
    internal ref ParseState State => ref _state;

    /// <summary>Gets the MarkSnapshot state.</summary>
    internal ref PdfMark[]? MarkSnapshot => ref _markSnapshot;

    /// <summary>Gets the OperatorStart state.</summary>
    internal ref int OperatorStart => ref _operatorStart;

    /// <summary>Gets the OperatorEnd state.</summary>
    internal ref int OperatorEnd => ref _operatorEnd;

    /// <summary>Gets the UnderflowCount state.</summary>
    internal ref int UnderflowCount => ref _underflow;

    /// <summary>Gets the number of restores that had no matching save.</summary>
    internal int Underflow => _underflow;

    /// <summary>Gets the number of saves left open at the end.</summary>
    internal int Unclosed => _scratch.Stack.Count;

    /// <summary>Gets the PathStart state.</summary>
    internal ref int PathStart => ref _pathStart;

    /// <summary>Gets the PendingClip state.</summary>
    internal ref PdfClipMode PendingClip => ref _pendingClip;

    /// <summary>Gets the Current state.</summary>
    internal ref Vector2 Current => ref _current;

    /// <summary>Gets the SubpathStart state.</summary>
    internal ref Vector2 SubpathStart => ref _subpathStart;

    /// <summary>Gets the Glyphs state.</summary>
    internal List<PdfTextGlyph> Glyphs => _scratch.Glyphs;

    /// <summary>Gets the CodeBytes state.</summary>
    internal List<byte> CodeBytes => _scratch.CodeBytes;

    /// <summary>Gets the Text state.</summary>
    internal StringBuilder Text => _scratch.Text;

    /// <summary>Gets the Tm state.</summary>
    internal ref Matrix3x2 Tm => ref _tm;

    /// <summary>Gets the Tlm state.</summary>
    internal ref Matrix3x2 Tlm => ref _tlm;

    /// <summary>Gets the Position state.</summary>
    internal ref Vector2 Position => ref _position;

    /// <summary>Gets the Kerning state.</summary>
    internal ref float Kerning => ref _kerning;

    /// <summary>Gets the ShowAdvance state.</summary>
    internal ref float ShowAdvance => ref _showAdvance;

    /// <summary>Binds the state to one read and puts every interpreter value back to its start.</summary>
    /// <param name = "owner">The content the objects belong to.</param>
    /// <param name = "content">The decoded content.</param>
    /// <param name = "resources">The resources the content names, or <see langword="null"/>.</param>
    /// <param name = "start">The matrix from the content's space to user space.</param>
    /// <param name = "clip">The clip the content starts inside, or <see langword="null"/>.</param>
    internal void Bind(PdfPageContent owner, byte[] content, PdfDictionary? resources, Matrix3x2 start, ClipNode? clip)
    {
        _owner = owner;
        _cache = HyperPdfLibrary.Document.PdfDocumentRendering.GetRenderCache(owner.Document);
        _names = owner.Document.Objects.Names;
        _content = content;
        _resources = resources;
        _state = ParseState.Start(start);
        _state.Clip = clip;
        _markSnapshot = null;
        _operatorStart = 0;
        _operatorEnd = 0;
        _underflow = 0;
        _pathStart = -1;
        _pendingClip = default;
        _current = default;
        _subpathStart = default;
        _tm = Matrix3x2.Identity;
        _tlm = Matrix3x2.Identity;
        _position = default;
        _kerning = 0;
        _showAdvance = 0;
    }

    /// <summary>Lets go of the read's content, so a state kept for the next read pins none of it.</summary>
    internal void Unbind()
    {
        _owner = null;
        _cache = null;
        _names = null;
        _content = null;
        _resources = null;
        _state = default;
        _markSnapshot = null;
    }

    /// <summary>Makes the error for a parse state used while it is not bound to a read.</summary>
    /// <returns>The error.</returns>
    private static InvalidOperationException Unbound() => new("The parse state is not bound to a content read.");
}
