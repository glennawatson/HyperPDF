// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.Core.Annotations;

/// <summary>
/// Reads, adds and changes annotations, and saves the document. Implemented by documents that support editing;
/// safe to call from any thread. Positions are in page space (points, top-left origin).
/// </summary>
public interface IAnnotationEditor
{
    /// <summary>Gets a value indicating whether the document has changes that are not saved.</summary>
    bool HasUnsavedChanges { get; }

    /// <summary>Gets or sets the name recorded as the author of new annotations and replies; the person's user name by default.</summary>
    string Author { get; set; }

    /// <summary>Appends the annotations on a page, skipping links, form fields and pop-ups.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the annotations.</param>
    void GetAnnotations(int pageIndex, List<PageAnnotation> output);

    /// <summary>Marks text with a highlight, underline, strike-out or squiggly line.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind">One of the text markup kinds.</param>
    /// <param name="lines">The line rectangles to mark, for example from a text selection.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="contents">An optional note, or an empty string.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddMarkup(int pageIndex, AnnotationKind kind, ReadOnlySpan<PageRect> lines, uint color, string contents);

    /// <summary>Adds a freehand drawing, or a drawn signature.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="points">Every point, stroke after stroke.</param>
    /// <param name="strokeLengths">The number of points in each stroke.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="width">The line width in points.</param>
    /// <param name="kind"><see cref="AnnotationKind.Ink"/> or <see cref="AnnotationKind.Signature"/>.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddInk(int pageIndex, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, uint color, float width, AnnotationKind kind);

    /// <summary>Adds a sticky note.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the note icon.</param>
    /// <param name="contents">The note text.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddNote(int pageIndex, PagePoint location, string contents, uint color);

    /// <summary>Writes text on the page, or places a typed signature.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the first line.</param>
    /// <param name="text">The text; line breaks start new lines.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="kind"><see cref="AnnotationKind.TextBox"/> or <see cref="AnnotationKind.Signature"/>.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddText(int pageIndex, PagePoint location, string text, float fontSize, uint color, AnnotationKind kind);

    /// <summary>Draws a rectangle, ellipse, arrow or line between two points.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind"><see cref="AnnotationKind.Rectangle"/>, <see cref="AnnotationKind.Ellipse"/>, <see cref="AnnotationKind.Arrow"/> or <see cref="AnnotationKind.Line"/>.</param>
    /// <param name="start">Where the drag started: a corner, or the arrow's tail.</param>
    /// <param name="end">Where the drag ended: the opposite corner, or the arrow's point.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="width">The line width in points.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddShape(int pageIndex, AnnotationKind kind, PagePoint start, PagePoint end, uint color, float width);

    /// <summary>Places a stamp: a framed word such as "APPROVED".</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the stamp.</param>
    /// <param name="label">The word on the stamp.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddStamp(int pageIndex, PagePoint location, string label, uint color);

    /// <summary>Places a picture as a stamp, keeping its transparency.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="bounds">The picture's bounds in page space.</param>
    /// <param name="pixels">Tightly packed, unpremultiplied BGRA pixels, in rows from top to bottom.</param>
    /// <param name="width">The picture width in pixels.</param>
    /// <param name="height">The picture height in pixels.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddImageStamp(int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height);

    /// <summary>Draws a polygon, a cloud or a run of lines through points.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind"><see cref="AnnotationKind.Polygon"/>, <see cref="AnnotationKind.Cloud"/> or <see cref="AnnotationKind.PolyLine"/>.</param>
    /// <param name="vertices">The corners in order; a polygon or cloud closes back to the first.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="width">The line width in points.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddPolygon(int pageIndex, AnnotationKind kind, ReadOnlySpan<PagePoint> vertices, uint color, float width);

    /// <summary>Writes text in a framed box with a line and arrow pointing at something on the page.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="target">The point the arrow points at.</param>
    /// <param name="location">The top-left corner of the text.</param>
    /// <param name="text">The text; line breaks start new lines.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddCallout(int pageIndex, PagePoint target, PagePoint location, string text, float fontSize, uint color);

    /// <summary>Appends the replies to a comment and its review status changes, oldest first.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The comment's annotation index.</param>
    /// <param name="output">The list receiving the replies.</param>
    void GetReplies(int pageIndex, int index, List<AnnotationReply> output);

    /// <summary>Replies to a comment, or records a review status for it.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The comment's annotation index.</param>
    /// <param name="contents">The reply's text; may be empty when setting a status.</param>
    /// <param name="state">The review status to record, or <see cref="ReviewState.None"/> for a plain reply.</param>
    /// <returns>The reply's annotation index, or -1.</returns>
    int AddReply(int pageIndex, int index, string contents, ReviewState state);

    /// <summary>Changes an annotation's colour.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    bool SetColor(int pageIndex, int index, uint color);

    /// <summary>Changes an annotation's note text.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="contents">The note text.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    bool SetContents(int pageIndex, int index, string contents);

    /// <summary>
    /// Moves or resizes an annotation. Drawings, lines and polygons are redrawn through their moved points; other
    /// annotations scale their appearance into the new bounds. Text markup follows its text and cannot be moved.
    /// </summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="bounds">The new bounds in page space.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    bool SetBounds(int pageIndex, int index, PageRect bounds);

    /// <summary>Changes the line width of a drawing, shape, line or polygon.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="width">The line width in points.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    bool SetLineWidth(int pageIndex, int index, float width);

    /// <summary>Changes the text size of a text box or callout written by this viewer.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    bool SetFontSize(int pageIndex, int index, float fontSize);

    /// <summary>
    /// Takes an annotation off the page, or puts it back. A removed annotation keeps its index, is no longer listed or
    /// drawn, and is left out when the document is saved, so removing can be undone even after saving.
    /// </summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="removed">Whether the annotation is removed.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    bool SetRemoved(int pageIndex, int index, bool removed);

    /// <summary>Removes an annotation for good; the indexes of later annotations on the page shift down.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns><see langword="true"/> when removed.</returns>
    bool Remove(int pageIndex, int index);

    /// <summary>
    /// Saves the document to a stream. Documents with digital signatures are saved incrementally so the signatures stay
    /// valid; others are saved compactly.
    /// </summary>
    /// <param name="destination">The stream to write to.</param>
    /// <returns><see langword="true"/> when saved.</returns>
    bool Save(Stream destination);

    /// <summary>Saves the document to a stream with cancellable output I/O.</summary>
    /// <param name="destination">The stream receiving the document.</param>
    /// <param name="cancellationToken">Cancels preparation and writing.</param>
    /// <returns>Whether the document was saved.</returns>
    ValueTask<bool> SaveAsync(Stream destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Save(destination));
    }
}
