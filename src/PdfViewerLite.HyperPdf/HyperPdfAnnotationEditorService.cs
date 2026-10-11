// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.HyperPdf;

/// <summary>Provides IAnnotationEditor through the owning document.</summary>
internal sealed class HyperPdfAnnotationEditorService : IAnnotationEditor
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfAnnotationEditorService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfAnnotationEditorService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public bool HasUnsavedChanges { get => HyperPdfDocumentAnnotationEditing.GetHasUnsavedChanges(_owner); }

    /// <inheritdoc/>
    public string Author { get => HyperPdfDocumentAnnotationEditing.GetAuthor(_owner); set => HyperPdfDocumentAnnotationEditing.SetAuthor(_owner, value); }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetAnnotations(int pageIndex, List<PageAnnotation> output) => HyperPdfDocumentAnnotationEditing.GetAnnotations(
            _owner,
            pageIndex,
            output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddMarkup(int pageIndex, AnnotationKind kind, ReadOnlySpan<PageRect> lines, uint color, string contents) => HyperPdfDocumentAnnotationEditing.AddMarkup(
            _owner,
            pageIndex,
            kind,
            lines,
            color,
            contents);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddInk(int pageIndex, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, uint color, float width, AnnotationKind kind) => HyperPdfDocumentAnnotationEditing.AddInk(
            _owner,
            pageIndex,
            points,
            strokeLengths,
            color,
            width,
            kind);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddNote(int pageIndex, PagePoint location, string contents, uint color) => HyperPdfDocumentAnnotationEditing.AddNote(
            _owner,
            pageIndex,
            location,
            contents,
            color);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddText(int pageIndex, PagePoint location, string text, float fontSize, uint color, AnnotationKind kind) => HyperPdfDocumentAnnotationEditing.AddText(
            _owner,
            pageIndex,
            location,
            text,
            fontSize,
            color,
            kind);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddShape(int pageIndex, AnnotationKind kind, PagePoint start, PagePoint end, uint color, float width) => HyperPdfDocumentAnnotationEditing.AddShape(
            _owner,
            pageIndex,
            kind,
            start,
            end,
            color,
            width);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddStamp(int pageIndex, PagePoint location, string label, uint color) => HyperPdfDocumentAnnotationEditing.AddStamp(
            _owner,
            pageIndex,
            location,
            label,
            color);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddImageStamp(int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height) => HyperPdfDocumentAnnotationEditing.AddImageStamp(
            _owner,
            pageIndex,
            bounds,
            pixels,
            width,
            height);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddPolygon(int pageIndex, AnnotationKind kind, ReadOnlySpan<PagePoint> vertices, uint color, float width) => HyperPdfDocumentAnnotationEditing.AddPolygon(
            _owner,
            pageIndex,
            kind,
            vertices,
            color,
            width);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddCallout(int pageIndex, PagePoint target, PagePoint location, string text, float fontSize, uint color) => HyperPdfDocumentAnnotationEditing.AddCallout(
            _owner,
            pageIndex,
            target,
            location,
            text,
            fontSize,
            color);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetReplies(int pageIndex, int index, List<AnnotationReply> output) => HyperPdfDocumentAnnotationEditing.GetReplies(
            _owner,
            pageIndex,
            index,
            output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddReply(int pageIndex, int index, string contents, ReviewState state) => HyperPdfDocumentAnnotationEditing.AddReply(
            _owner,
            pageIndex,
            index,
            contents,
            state);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetColor(int pageIndex, int index, uint color) => HyperPdfDocumentAnnotationEditing.SetColor(
            _owner,
            pageIndex,
            index,
            color);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetContents(int pageIndex, int index, string contents) => HyperPdfDocumentAnnotationEditing.SetContents(
            _owner,
            pageIndex,
            index,
            contents);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetBounds(int pageIndex, int index, PageRect bounds) => HyperPdfDocumentAnnotationEditing.SetBounds(
            _owner,
            pageIndex,
            index,
            bounds);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetLineWidth(int pageIndex, int index, float width) => HyperPdfDocumentAnnotationEditing.SetLineWidth(
            _owner,
            pageIndex,
            index,
            width);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetFontSize(int pageIndex, int index, float fontSize) => HyperPdfDocumentAnnotationEditing.SetFontSize(
            _owner,
            pageIndex,
            index,
            fontSize);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetRemoved(int pageIndex, int index, bool removed) => HyperPdfDocumentAnnotationEditing.SetRemoved(
            _owner,
            pageIndex,
            index,
            removed);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(int pageIndex, int index) => HyperPdfDocumentAnnotationEditing.Remove(
            _owner,
            pageIndex,
            index);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Save(Stream destination) => HyperPdfDocumentAnnotationEditing.Save(
            _owner,
            destination);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> SaveAsync(Stream destination, CancellationToken cancellationToken) =>
        HyperPdfDocumentAnnotationEditing.SaveAsync(_owner, destination, cancellationToken);
}
