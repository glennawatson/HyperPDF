// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// A page being drawn. Text, images, forms, shadings and patterns are drawn through methods that add the resource to the
/// page's resource dictionary and write the operators. In a tagged document every drawing method must be inside
/// <see cref="BeginContent(PdfTagElement)"/> or <see cref="BeginArtifact(PdfArtifactKind)"/>, so no content is left untagged
/// (ISO 32000-2 14.8.2.2). <see cref="Finish"/> adds the page to the document; <see cref="Dispose"/> abandons a page that
/// was not finished.
/// </summary>
[DebuggerDisplay("PdfPageCanvas: {Width} x {Height}")]
public ref struct PdfPageCanvas
{
    /// <summary>The document being created.</summary>
    private readonly PdfDocumentCreator _creator;

    /// <summary>The page object, reserved before drawing starts.</summary>
    private readonly PdfObjectId _page;

    /// <summary>The page's resources, made when the first one is added.</summary>
    private PdfPageResources? _resources;

    /// <summary>The page's content.</summary>
    private PdfContentBuilder _content;

    /// <summary>How many marked content sequences are open.</summary>
    private int _open;

    /// <summary>Whether the page has been added to the document.</summary>
    private int _finished;

    /// <summary>Initializes a new instance of the <see cref="PdfPageCanvas"/> struct.</summary>
    /// <param name="creator">The document being created.</param>
    /// <param name="page">The page object, reserved before drawing starts.</param>
    /// <param name="width">The page width in points.</param>
    /// <param name="height">The page height in points.</param>
    internal PdfPageCanvas(PdfDocumentCreator creator, PdfObjectId page, float width, float height)
    {
        _creator = creator;
        _page = page;
        _resources = null;
        Width = width;
        Height = height;
        _content = default;
        _open = 0;
        _finished = 0;
        if (creator.ContentCapacityHint > 0)
        {
            _content.Reserve(creator.ContentCapacityHint);
        }
    }

    /// <summary>Gets the page width in points.</summary>
    public readonly float Width { get; }

    /// <summary>Gets the page height in points.</summary>
    public readonly float Height { get; }

    /// <summary>
    /// Gets the page's resources, for operators written through <see cref="Content"/>. The set is only valid until
    /// <see cref="Finish"/> or <see cref="Dispose"/>, which hand it to the next page, so do not keep it past either call.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The page was already finished or disposed.</exception>
    public PdfPageResources Resources
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _finished) != 0, typeof(PdfPageCanvas));
            return _resources ??= _creator.RentResources();
        }
    }

    /// <summary>Gets the content stream, for operators this canvas has no method for. Content written here is not checked for tagging.</summary>
    [UnscopedRef]
    public ref PdfContentBuilder Content => ref _content;

    /// <summary>Draws one line of text with its baseline starting at a point, in the current fill colour.</summary>
    /// <param name="font">The font; an embedded font is cut down to the glyphs drawn when the document is saved.</param>
    /// <param name="size">The font size in points.</param>
    /// <param name="x">The baseline's start x.</param>
    /// <param name="y">The baseline's y.</param>
    /// <param name="text">The text, without line breaks.</param>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The text has a line break.</exception>
    /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open, or the font was finished before the text needing a new glyph.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawText(PdfCanvasFont font, float size, float x, float y, ReadOnlySpan<char> text) =>
        DrawText(font, size, PdfRgbColor.Black, x, y, text);

    /// <summary>Draws one line of text with its baseline starting at a point, in the given RGB colour.</summary>
    /// <param name="font">The font; an embedded font is cut down to the glyphs drawn when the document is saved.</param>
    /// <param name="size">The font size in points.</param>
    /// <param name="color">The fill colour of the text.</param>
    /// <param name="x">The baseline's start x.</param>
    /// <param name="y">The baseline's y.</param>
    /// <param name="text">The text, without line breaks.</param>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The text has a line break.</exception>
    /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open, or the font was finished before the text needing a new glyph.</exception>
    public void DrawText(PdfCanvasFont font, float size, PdfRgbColor color, float x, float y, ReadOnlySpan<char> text)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (text.IndexOfAny('\r', '\n') >= 0)
        {
            throw new ArgumentException("Text drawn with DrawText must not have line breaks.", nameof(text));
        }

        RequireMarked();
        var name = Resources.AddFont(font.Id);
        var colored = !color.IsBlack;
        if (colored)
        {
            _content.SaveState();
            _content.SetFillRgb(color.Red, color.Green, color.Blue);
        }

        _content.BeginText();
        _content.SetFont(name.Utf8, size);
        _content.MoveText(x, y);
        font.Show(ref _content, text, size);
        _content.EndText();
        if (colored)
        {
            _content.RestoreState();
        }
    }

    /// <summary>Draws an image scaled to a rectangle.</summary>
    /// <param name="image">The image.</param>
    /// <param name="x">The rectangle's left edge.</param>
    /// <param name="y">The rectangle's bottom edge.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
    public void DrawImage(PdfCreatedImage image, float x, float y, float width, float height)
    {
        RequireMarked();
        var name = Resources.AddImage(image.Id);
        _content.SaveState();
        _content.Transform(width, 0, 0, height, x, y);
        _content.DrawXObject(name.Utf8);
        _content.RestoreState();
    }

    /// <summary>Draws a form XObject, such as an imported page, through a transformation matrix.</summary>
    /// <param name="form">The form's object.</param>
    /// <param name="a">The matrix's a entry.</param>
    /// <param name="b">The matrix's b entry.</param>
    /// <param name="c">The matrix's c entry.</param>
    /// <param name="d">The matrix's d entry.</param>
    /// <param name="e">The horizontal translation.</param>
    /// <param name="f">The vertical translation.</param>
    /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
    public void DrawForm(PdfObjectId form, float a, float b, float c, float d, float e, float f)
    {
        RequireMarked();
        var name = Resources.AddForm(form);
        _content.SaveState();
        _content.Transform(a, b, c, d, e, f);
        _content.DrawXObject(name.Utf8);
        _content.RestoreState();
    }

    /// <summary>Fills a rectangle with a shading, which is painted in the page's coordinates.</summary>
    /// <param name="shading">The shading dictionary's object.</param>
    /// <param name="x">The rectangle's left edge.</param>
    /// <param name="y">The rectangle's bottom edge.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
    public void FillRectangleWithShading(PdfObjectId shading, float x, float y, float width, float height)
    {
        RequireMarked();
        var name = Resources.AddShading(shading);
        _content.SaveState();
        _content.Rectangle(x, y, width, height);
        _content.Clip();
        _content.EndPath();
        _content.PaintShading(name.Utf8);
        _content.RestoreState();
    }

    /// <summary>Fills a rectangle with a pattern.</summary>
    /// <param name="pattern">The pattern's object.</param>
    /// <param name="x">The rectangle's left edge.</param>
    /// <param name="y">The rectangle's bottom edge.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
    public void FillRectangleWithPattern(PdfObjectId pattern, float x, float y, float width, float height)
    {
        RequireMarked();
        var name = Resources.AddPattern(pattern);
        _content.SaveState();
        _content.SetFillColorSpace("Pattern"u8);
        _content.SetFillColorN([], name.Utf8);
        _content.Rectangle(x, y, width, height);
        _content.Fill();
        _content.RestoreState();
    }

    /// <summary>Fills a rectangle with a colour.</summary>
    /// <param name="red">The red level from 0 to 1.</param>
    /// <param name="green">The green level from 0 to 1.</param>
    /// <param name="blue">The blue level from 0 to 1.</param>
    /// <param name="x">The rectangle's left edge.</param>
    /// <param name="y">The rectangle's bottom edge.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
    public void FillRectangle(float red, float green, float blue, float x, float y, float width, float height)
    {
        RequireMarked();
        _content.SaveState();
        _content.SetFillRgb(red, green, blue);
        _content.Rectangle(x, y, width, height);
        _content.Fill();
        _content.RestoreState();
    }

    /// <summary>Strokes a straight line between two points (ISO 32000-2 8.5.3).</summary>
    /// <param name="stroke">The width and colour.</param>
    /// <param name="x1">The start x.</param>
    /// <param name="y1">The start y.</param>
    /// <param name="x2">The end x.</param>
    /// <param name="y2">The end y.</param>
    /// <exception cref="ArgumentOutOfRangeException">The stroke width is negative.</exception>
    /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
    public void DrawLine(PdfStroke stroke, float x1, float y1, float x2, float y2)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stroke.Width);
        RequireMarked();
        _content.SaveState();
        _content.SetLineWidth(stroke.Width);
        _content.SetStrokeRgb(stroke.Red, stroke.Green, stroke.Blue);
        _content.MoveTo(x1, y1);
        _content.LineTo(x2, y2);
        _content.Stroke();
        _content.RestoreState();
    }

    /// <summary>Strokes the outline of a rectangle; the line is centred on the rectangle's edges (ISO 32000-2 8.5.3).</summary>
    /// <param name="stroke">The width and colour.</param>
    /// <param name="x">The rectangle's left edge.</param>
    /// <param name="y">The rectangle's bottom edge.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    /// <exception cref="ArgumentOutOfRangeException">The stroke width is negative.</exception>
    /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
    public void StrokeRectangle(PdfStroke stroke, float x, float y, float width, float height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stroke.Width);
        RequireMarked();
        _content.SaveState();
        _content.SetLineWidth(stroke.Width);
        _content.SetStrokeRgb(stroke.Red, stroke.Green, stroke.Blue);
        _content.Rectangle(x, y, width, height);
        _content.Stroke();
        _content.RestoreState();
    }

    /// <summary>
    /// Strokes the outline of a rectangle and vertical lines across it in one path, so the width and colour are set once
    /// (ISO 32000-2 8.5.3). The result looks the same as <see cref="StrokeRectangle"/> plus a <see cref="DrawLine"/> for each divider.
    /// </summary>
    /// <param name="stroke">The width and colour.</param>
    /// <param name="x">The rectangle's left edge.</param>
    /// <param name="top">The rectangle's top edge.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    /// <param name="dividers">The x of each vertical line, drawn from the top edge to the bottom edge.</param>
    /// <exception cref="ArgumentOutOfRangeException">The stroke width is negative.</exception>
    /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
    public void StrokeRectangleWithDividers(PdfStroke stroke, float x, float top, float width, float height, scoped ReadOnlySpan<float> dividers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stroke.Width);
        RequireMarked();
        var bottom = top - height;
        _content.SaveState();
        _content.SetLineWidth(stroke.Width);
        _content.SetStrokeRgb(stroke.Red, stroke.Green, stroke.Blue);
        _content.Rectangle(x, bottom, width, height);
        foreach (var divider in dividers)
        {
            _content.MoveTo(divider, top);
            _content.LineTo(divider, bottom);
        }

        _content.Stroke();
        _content.RestoreState();
    }

    /// <summary>Starts marked content that belongs to a structure element (ISO 32000-2 14.7.4); close it with <see cref="EndContent"/>.</summary>
    /// <param name="element">The element the content belongs to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="element"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The document is not tagged, or marked content is already open.</exception>
    public void BeginContent(PdfTagElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var tags = _creator.Tags ?? throw new InvalidOperationException("The document is not tagged.");
        if (_open > 0)
        {
            throw new InvalidOperationException("Marked content is already open; end it first.");
        }

        _content.BeginMarkedContent(element.TypeUtf8, tags.AddContent(element, _page));
        _open = 1;
    }

    /// <summary>Ends the marked content started with <see cref="BeginContent(PdfTagElement)"/>.</summary>
    /// <exception cref="InvalidOperationException">No marked content is open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndContent() => EndMarked();

    /// <summary>Starts an artifact, content that is not part of the structure, such as a page number or a rule; close it with <see cref="EndArtifact"/>.</summary>
    /// <param name="kind">The kind of artifact.</param>
    /// <exception cref="InvalidOperationException">Marked content is already open.</exception>
    public void BeginArtifact(PdfArtifactKind kind)
    {
        if (_open > 0)
        {
            throw new InvalidOperationException("Marked content is already open; end it first.");
        }

        _content.WriteRaw(kind switch
        {
            PdfArtifactKind.Pagination => "/Artifact <</Type /Pagination>> BDC\n"u8,
            PdfArtifactKind.Background => "/Artifact <</Type /Background>> BDC\n"u8,
            _ => "/Artifact <</Type /Layout>> BDC\n"u8,
        });
        _open = 1;
    }

    /// <summary>Ends the artifact started with <see cref="BeginArtifact(PdfArtifactKind)"/>.</summary>
    /// <exception cref="InvalidOperationException">No artifact is open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndArtifact() => EndContent();

    /// <summary>Writes the page's content and dictionary and adds the page to the document.</summary>
    /// <returns>The page's object.</returns>
    /// <exception cref="InvalidOperationException">Marked content is still open, or the page was already finished.</exception>
    public PdfObjectId Finish()
    {
        if (_open > 0)
        {
            throw new InvalidOperationException("Marked content is still open; end it first.");
        }

        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            throw new InvalidOperationException("The page is already finished.");
        }

        try
        {
            _creator.ContentCapacityHint = _content.Length;
            _creator.CompletePage(_page, Width, Height, _resources, _content.ToArray());
        }
        finally
        {
            _content.Dispose();
            _creator.ReturnResources(_resources);
            _resources = null;
        }

        return _page;
    }

    /// <summary>Returns the content buffer to the pool. A page that was not finished is not added to the document.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        // A flow that has not started a page holds a default canvas with no creator.
        if (_creator is null)
        {
            return;
        }

        _creator.ContentCapacityHint = _content.Length;
        _content.Dispose();
        if (_resources is null)
        {
            return;
        }

        _creator.ReturnResources(_resources);
        _resources = null;
    }

    /// <summary>Checks that a tagged document's content is inside marked content.</summary>
    /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
    private readonly void RequireMarked()
    {
        if (_creator.Tags is null || _open > 0)
        {
            return;
        }

        throw new InvalidOperationException("A tagged document's content must be inside BeginContent or BeginArtifact.");
    }

    /// <summary>Ends the open marked content.</summary>
    /// <exception cref="InvalidOperationException">No marked content is open.</exception>
    private void EndMarked()
    {
        if (_open == 0)
        {
            throw new InvalidOperationException("No marked content is open.");
        }

        _content.EndMarkedContent();
        _open = 0;
    }
}
