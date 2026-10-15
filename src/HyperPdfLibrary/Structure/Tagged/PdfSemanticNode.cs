// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// One node of a page's reading structure: a heading, paragraph, list, table, figure, link, form field and so on, with
/// its text, its box and the page it is on, so a reader can speak it and move the focus to it. Built once and then
/// never changed.
/// </summary>
[DebuggerDisplay("PdfSemanticNode: {Role} {Level} ({Origin}) {Text}")]
public sealed class PdfSemanticNode
{
    /// <summary>The child nodes, in reading order; made when the first child is added.</summary>
    private List<PdfSemanticNode>? _children;

    /// <summary>The header cells that label this table cell, made on first use.</summary>
    private List<PdfSemanticNode>? _headers;

    /// <summary>The indices of what the node holds directly.</summary>
    private int[] _items = [];

    /// <summary>The text, made on first use when the node's text is read from its glyphs or children.</summary>
    private string? _text;

    /// <summary>The page whose glyphs the text is read from, for a content leaf that has not read it yet.</summary>
    private PdfMarkedContentPage? _glyphSource;

    /// <summary>Whether the text is the children's text joined, for an element node that has not read it yet.</summary>
    private bool _joinsChildren;

    /// <summary>Initializes a new instance of the <see cref="PdfSemanticNode"/> class.</summary>
    /// <param name="role">What the node is.</param>
    /// <param name="origin">Where the node came from.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    internal PdfSemanticNode(PdfSemanticRole role, PdfNodeOrigin origin, int pageIndex)
    {
        Role = role;
        Origin = origin;
        PageIndex = pageIndex;
    }

    /// <summary>Gets what the node is.</summary>
    public PdfSemanticRole Role { get; }

    /// <summary>Gets where the node came from: the tags, the layout or text recognition.</summary>
    public PdfNodeOrigin Origin { get; }

    /// <summary>Gets the zero based page index; the node's focus target is this page and <see cref="Bounds"/>.</summary>
    public int PageIndex { get; }

    /// <summary>Gets the parent node, or <see langword="null"/> for a top-level node.</summary>
    public PdfSemanticNode? Parent { get; private set; }

    /// <summary>Gets the child nodes, in reading order.</summary>
    public IReadOnlyList<PdfSemanticNode> Children => _children ?? (IReadOnlyList<PdfSemanticNode>)[];

    /// <summary>Gets the structure element, for a tagged node.</summary>
    public PdfStructureElement? Element { get; internal set; }

    /// <summary>Gets the heading level, 1 to 6, for a heading; zero otherwise.</summary>
    public int Level { get; internal set; }

    /// <summary>Gets the marked content id, for a <see cref="PdfSemanticRole.Content"/> node; otherwise -1.</summary>
    public int Mcid { get; internal set; } = -1;

    /// <summary>Gets the node's box in viewer space; empty when it draws nothing on the page.</summary>
    public PdfViewerRect Bounds { get; internal set; }

    /// <summary>Gets the text the node draws, in reading order.</summary>
    public string Text
    {
        get => _text ?? ReadText();
        internal set => _text = value;
    }

    /// <summary>Gets the text that replaces the drawn text (<c>/ActualText</c>), or <see langword="null"/>.</summary>
    public string? ActualText { get; internal set; }

    /// <summary>
    /// Gets the indices of what the node holds directly: glyphs of <see cref="PdfMarkedContentPage"/> for tagged and
    /// inferred nodes, words for text recognition nodes.
    /// </summary>
    public ReadOnlySpan<int> Items => _items;

    /// <summary>Gets the annotation a link, form field or annotation node refers to, or <see langword="null"/>.</summary>
    public PdfDictionary? Annotation { get; internal set; }

    /// <summary>Gets the annotation's object id; not valid when there is none or it is written directly in the page.</summary>
    public PdfObjectId AnnotationId { get; internal set; }

    /// <summary>Gets a link's web address, or <see langword="null"/>.</summary>
    public string? LinkUri { get; internal set; }

    /// <summary>Gets a form field's widget and state, or <see langword="null"/>.</summary>
    public PdfFormWidget? FormField { get; internal set; }

    /// <summary>Gets a form field's label: its <c>/TU</c>, else its name; <see langword="null"/> for other nodes.</summary>
    public string? FieldLabel { get; internal set; }

    /// <summary>Gets a table cell's row, from zero; -1 for other nodes.</summary>
    public int Row { get; internal set; } = -1;

    /// <summary>Gets a table cell's column, from zero; -1 for other nodes.</summary>
    public int Column { get; internal set; } = -1;

    /// <summary>Gets a table cell's row span.</summary>
    public int RowSpan { get; internal set; } = 1;

    /// <summary>Gets a table cell's column span.</summary>
    public int ColumnSpan { get; internal set; } = 1;

    /// <summary>Gets which cells a header cell labels: from its <c>/Scope</c>, or inferred from its place.</summary>
    public PdfTableScope Scope { get; internal set; }

    /// <summary>Gets the header cells that label this table cell: its <c>/Headers</c>, or inferred from the header scopes.</summary>
    public IReadOnlyList<PdfSemanticNode> Headers => _headers ?? (IReadOnlyList<PdfSemanticNode>)[];

    /// <summary>Gets the alternate description (<c>/Alt</c>), or the description a host gave an inferred figure, or <see langword="null"/>.</summary>
    public string? AlternateText => Element?.AlternateText ?? InferredAlternateText;

    /// <summary>Gets the language, or <see langword="null"/>.</summary>
    public string? Language => Element?.Language;

    /// <summary>Gets the description a host gave an inferred figure, or <see langword="null"/>.</summary>
    public string? InferredAlternateText { get; internal set; }

    /// <summary>Gets the language guessed from the node's text when the guess was asked for, or <see langword="null"/>; never the document's own language.</summary>
    public string? InferredLanguage { get; internal set; }

    /// <summary>Gets how the lines of an inferred paragraph sit between its edges.</summary>
    public PdfTextAlignment Alignment { get; internal set; }

    /// <summary>Gets which pagination artifact a <see cref="PdfSemanticRole.Artifact"/> node is.</summary>
    public PdfPaginationKind Pagination { get; internal set; }

    /// <summary>Gets how an inferred list's labels are made.</summary>
    public PdfListNumbering ListNumbering { get; internal set; }

    /// <summary>
    /// Gets the text a reader should speak: the replacement text, else a figure's or formula's description, else the
    /// drawn text, else the description, else a form field's label.
    /// </summary>
    public string SpokenText => ActualText ?? DescribedText() ?? DrawnText();

    /// <summary>Gets a value indicating whether the node is read by its description rather than its drawn text.</summary>
    private bool IsDescribed => Role is PdfSemanticRole.Figure or PdfSemanticRole.Formula;

    /// <summary>Appends the items of the node and its descendants, in reading order.</summary>
    /// <param name="output">Receives the item indices.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void CollectItems(List<int> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.AddRange(_items);
        if (_children is not { } children)
        {
            return;
        }

        for (var i = 0; i < children.Count; i++)
        {
            children[i].CollectItems(output);
        }
    }

    /// <summary>Adds a child node.</summary>
    /// <param name="child">The child.</param>
    internal void Add(PdfSemanticNode child)
    {
        child.Parent = this;
        (_children ??= []).Add(child);
    }

    /// <summary>Adds a paragraph to a figure or table as its caption, the first or last child.</summary>
    /// <param name="paragraph">The paragraph that reads as a caption.</param>
    /// <param name="first">Whether the caption comes before the rest of the node's content.</param>
    internal void AddCaption(PdfSemanticNode paragraph, bool first)
    {
        var caption = new PdfSemanticNode(PdfSemanticRole.Caption, paragraph.Origin, paragraph.PageIndex)
        {
            Text = paragraph.Text,
            Bounds = paragraph.Bounds,
            Alignment = paragraph.Alignment,
            InferredLanguage = paragraph.InferredLanguage,
        };
        caption._items = paragraph._items;
        caption.Parent = this;
        _children ??= [];
        _children.Insert(first ? 0 : _children.Count, caption);
    }

    /// <summary>Makes the node's text the text of its glyphs, read when the text is first asked for.</summary>
    /// <param name="content">The page the glyph indices belong to.</param>
    /// <param name="glyphs">The glyph indices.</param>
    internal void SetGlyphs(PdfMarkedContentPage content, int[] glyphs)
    {
        _items = glyphs;
        _glyphSource = content;
        _text = null;
    }

    /// <summary>Makes the node's text its children's text joined, read when the text is first asked for.</summary>
    internal void JoinChildText()
    {
        _joinsChildren = true;
        _text = null;
    }

    /// <summary>Sets the indices of what the node holds directly.</summary>
    /// <param name="items">The indices.</param>
    internal void SetItems(int[] items) => _items = items;

    /// <summary>Adds a header cell that labels this cell.</summary>
    /// <param name="header">The header cell.</param>
    internal void AddHeader(PdfSemanticNode header)
    {
        _headers ??= [];
        if (!_headers.Contains(header))
        {
            _headers.Add(header);
        }
    }

    /// <summary>Joins the text of glyphs back to back.</summary>
    /// <param name="content">The page the glyphs belong to.</param>
    /// <param name="glyphs">The glyph indices.</param>
    /// <returns>The text.</returns>
    private static string JoinGlyphs(PdfMarkedContentPage content, int[] glyphs)
    {
        var length = 0;
        foreach (var glyph in glyphs)
        {
            length += content.GetText(glyph).Length;
        }

        return length == 0 ? string.Empty : string.Create(length, new GlyphText(content, glyphs), static (destination, state) =>
        {
            var at = 0;
            foreach (var glyph in state.Glyphs)
            {
                var text = state.Content.GetText(glyph);
                text.CopyTo(destination[at..]);
                at += text.Length;
            }
        });
    }

    /// <summary>Writes the children's text joined, or only counts it when there is no destination.</summary>
    /// <param name="children">The children.</param>
    /// <param name="destination">Where to write, or empty to count.</param>
    /// <returns>The length of the joined text.</returns>
    private static int WriteChildren(List<PdfSemanticNode> children, Span<char> destination)
    {
        var length = 0;
        var last = ' ';
        for (var i = 0; i < children.Count; i++)
        {
            var next = children[i].Text;
            if (next.Length == 0)
            {
                continue;
            }

            if (length > 0 && !char.IsWhiteSpace(last) && !char.IsWhiteSpace(next[0]))
            {
                if (!destination.IsEmpty)
                {
                    destination[length] = ' ';
                }

                length++;
            }

            if (!destination.IsEmpty)
            {
                next.CopyTo(destination[length..]);
            }

            length += next.Length;
            last = next[^1];
        }

        return length;
    }

    /// <summary>Gets a figure's or formula's description, which it is read by.</summary>
    /// <returns>The description, or <see langword="null"/>.</returns>
    private string? DescribedText() => IsDescribed ? AlternateText : null;

    /// <summary>Gets the drawn text, else the description, else a form field's label.</summary>
    /// <returns>The text; empty when there is none.</returns>
    private string DrawnText() => Text.Length > 0 ? Text : AlternateText ?? FieldLabel ?? string.Empty;

    /// <summary>Reads the text from the glyphs or children it was set up to come from, and keeps it.</summary>
    /// <returns>The text; empty when the node has none.</returns>
    private string ReadText()
    {
        // Two threads may read the same text at once; both make the same string, so either one may be kept.
        var text = _glyphSource is { } content ? JoinGlyphs(content, _items) : JoinChildrenIfSet();
        _text = text;
        return text;
    }

    /// <summary>Joins the children's text when the node was set up to read it from them.</summary>
    /// <returns>The text; empty when the node was not.</returns>
    private string JoinChildrenIfSet() => _joinsChildren ? JoinChildren() : string.Empty;

    /// <summary>Joins the children's text, with a space between words that would otherwise run together.</summary>
    /// <returns>The text.</returns>
    private string JoinChildren()
    {
        if (_children is not { Count: > 0 } children)
        {
            return string.Empty;
        }

        if (children.Count == 1)
        {
            return children[0].Text;
        }

        var length = WriteChildren(children, []);
        return length == 0 ? string.Empty : string.Create(length, children, static (destination, state) => WriteChildren(state, destination));
    }

    /// <summary>The glyphs of one page whose text is joined.</summary>
    /// <param name="Content">The page.</param>
    /// <param name="Glyphs">The glyph indices.</param>
    private readonly record struct GlyphText(PdfMarkedContentPage Content, int[] Glyphs);
}
