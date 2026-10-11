// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using AnnotationKind = global::PdfViewerLite.Core.Annotations.AnnotationKind;

namespace PdfViewerLite.HyperPdf;

/// <summary>Implements DocumentAnnotationEditing over the document's owned state.</summary>
internal static class HyperPdfDocumentAnnotationEditing
{
    /// <summary>Gets HasUnsavedChanges.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The current value.</returns>
    internal static bool GetHasUnsavedChanges(HyperPdfDocument self) => !self.IsDisposed && Volatile.Read(ref self.EditVersion) != Volatile.Read(ref self.SavedVersion);

    /// <summary>Gets Author.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetAuthor(HyperPdfDocument self) => HyperPdfAnnotationReading.GetAuthor(HyperPdfAnnotationStateAccess.GetAnnotations(self));

    /// <summary>Sets Author.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="value">The value to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetAuthor(HyperPdfDocument self, string value) => HyperPdfAnnotationReading.SetAuthor(HyperPdfAnnotationStateAccess.GetAnnotations(self), value);

    /// <summary>Appends the annotations on a page, skipping links, form fields and pop-ups.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the annotations.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void GetAnnotations(HyperPdfDocument self, int pageIndex, List<PageAnnotation> output)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        HyperPdfAnnotationReading.GetAnnotations(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, output);
    }

    /// <summary>Marks text with a highlight, underline, strike-out or squiggly line.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind">One of the text markup kinds.</param>
    /// <param name="lines">The line rectangles to mark, for example from a text selection.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="contents">An optional note, or an empty string.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddMarkup(HyperPdfDocument self, int pageIndex, AnnotationKind kind, ReadOnlySpan<PageRect> lines, uint color, string contents)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Added(self, pageIndex, HyperPdfAnnotationShapes.AddMarkup(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, kind, lines, color, contents));
        }
    }

    /// <summary>Adds a freehand drawing, or a drawn signature.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="points">Every point, stroke after stroke.</param>
    /// <param name="strokeLengths">The number of points in each stroke.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="width">The line width in points.</param>
    /// <param name="kind"><see cref="AnnotationKind.Ink"/> or <see cref="AnnotationKind.Signature"/>.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddInk(HyperPdfDocument self, int pageIndex, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, uint color, float width, AnnotationKind kind)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Added(self, pageIndex, HyperPdfAnnotationShapes.AddInk(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, points, strokeLengths, color, width, kind));
        }
    }

    /// <summary>Adds a sticky note.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the note icon.</param>
    /// <param name="contents">The note text.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddNote(HyperPdfDocument self, int pageIndex, PagePoint location, string contents, uint color)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Added(self, pageIndex, HyperPdfAnnotationShapes.AddNote(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, location, contents, color));
        }
    }

    /// <summary>Writes text on the page, or places a typed signature.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the first line.</param>
    /// <param name="text">The text; line breaks start new lines.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="kind"><see cref="AnnotationKind.TextBox"/> or <see cref="AnnotationKind.Signature"/>.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddText(HyperPdfDocument self, int pageIndex, PagePoint location, string text, float fontSize, uint color, AnnotationKind kind)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Added(self, pageIndex, HyperPdfAnnotationText.AddText(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, location, text, fontSize, color, kind));
        }
    }

    /// <summary>Draws a rectangle, ellipse, arrow or line between two points.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind"><see cref="AnnotationKind.Rectangle"/>, <see cref="AnnotationKind.Ellipse"/>, <see cref="AnnotationKind.Arrow"/> or <see cref="AnnotationKind.Line"/>.</param>
    /// <param name="start">Where the drag started: a corner, or the arrow's tail.</param>
    /// <param name="end">Where the drag ended: the opposite corner, or the arrow's point.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="width">The line width in points.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddShape(HyperPdfDocument self, int pageIndex, AnnotationKind kind, PagePoint start, PagePoint end, uint color, float width)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Added(self, pageIndex, HyperPdfAnnotationShapes.AddShape(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, kind, start, end, color, width));
        }
    }

    /// <summary>Places a stamp: a framed word such as "APPROVED".</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the stamp.</param>
    /// <param name="label">The word on the stamp.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddStamp(HyperPdfDocument self, int pageIndex, PagePoint location, string label, uint color)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Added(self, pageIndex, HyperPdfAnnotationText.AddStamp(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, location, label, color));
        }
    }

    /// <summary>Places a picture as a stamp, keeping its transparency.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="bounds">The picture's bounds in page space.</param>
    /// <param name="pixels">Tightly packed, unpremultiplied BGRA pixels, in rows from top to bottom.</param>
    /// <param name="width">The picture width in pixels.</param>
    /// <param name="height">The picture height in pixels.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddImageStamp(HyperPdfDocument self, int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Added(self, pageIndex, HyperPdfAnnotationImages.AddImageStamp(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, bounds, pixels, width, height));
        }
    }

    /// <summary>Draws a polygon, a cloud or a run of lines through points.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind"><see cref="AnnotationKind.Polygon"/>, <see cref="AnnotationKind.Cloud"/> or <see cref="AnnotationKind.PolyLine"/>.</param>
    /// <param name="vertices">The corners in order; a polygon or cloud closes back to the first.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="width">The line width in points.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddPolygon(HyperPdfDocument self, int pageIndex, AnnotationKind kind, ReadOnlySpan<PagePoint> vertices, uint color, float width)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Added(self, pageIndex, HyperPdfAnnotationShapes.AddPolygon(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, kind, vertices, color, width));
        }
    }

    /// <summary>Writes text in a framed box with a line and arrow pointing at something on the page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="target">The point the arrow points at.</param>
    /// <param name="location">The top-left corner of the text.</param>
    /// <param name="text">The text; line breaks start new lines.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddCallout(HyperPdfDocument self, int pageIndex, PagePoint target, PagePoint location, string text, float fontSize, uint color)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Added(
                self,
                pageIndex,
                HyperPdfAnnotationCallouts.AddCallout(
                    HyperPdfAnnotationStateAccess.GetAnnotations(self),
                    pageIndex,
                    target,
                    location,
                    text,
                    fontSize,
                    color));
        }
    }

    /// <summary>Appends the replies to a comment and its review status changes, oldest first.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The comment's annotation index.</param>
    /// <param name="output">The list receiving the replies.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void GetReplies(HyperPdfDocument self, int pageIndex, int index, List<AnnotationReply> output)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        HyperPdfAnnotationReplies.GetReplies(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, index, output);
    }

    /// <summary>Replies to a comment, or records a review status for it.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The comment's annotation index.</param>
    /// <param name="contents">The reply's text; may be empty when setting a status.</param>
    /// <param name="state">The review status to record, or <see cref="F:PdfViewerLite.Core.Annotations.ReviewState.None"/> for a plain reply.</param>
    /// <returns>The reply's annotation index, or -1.</returns>
    internal static int AddReply(HyperPdfDocument self, int pageIndex, int index, string contents, ReviewState state)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Added(self, pageIndex, HyperPdfAnnotationReplies.AddReply(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, index, contents, state));
        }
    }

    /// <summary>Changes an annotation's colour.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetColor(HyperPdfDocument self, int pageIndex, int index, uint color)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Changed(self, pageIndex, HyperPdfAnnotationEditing.SetColor(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, index, color));
        }
    }

    /// <summary>Changes an annotation's note text.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="contents">The note text.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetContents(HyperPdfDocument self, int pageIndex, int index, string contents)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Changed(self, pageIndex, HyperPdfAnnotationReading.SetContents(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, index, contents));
        }
    }

    /// <summary>
    /// Moves or resizes an annotation. Drawings, lines and polygons are redrawn through their moved points; other
    /// annotations scale their appearance into the new bounds. Text markup follows its text and cannot be moved.
    /// </summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="bounds">The new bounds in page space.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetBounds(HyperPdfDocument self, int pageIndex, int index, PageRect bounds)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Changed(self, pageIndex, HyperPdfAnnotationEditing.SetBounds(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, index, bounds));
        }
    }

    /// <summary>Changes the line width of a drawing, shape, line or polygon.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="width">The line width in points.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetLineWidth(HyperPdfDocument self, int pageIndex, int index, float width)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Changed(self, pageIndex, HyperPdfAnnotationEditing.SetLineWidth(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, index, width));
        }
    }

    /// <summary>Changes the text size of a text box or callout written by this viewer.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetFontSize(HyperPdfDocument self, int pageIndex, int index, float fontSize)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Changed(self, pageIndex, HyperPdfAnnotationText.SetFontSize(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, index, fontSize));
        }
    }

    /// <summary>
    /// Takes an annotation off the page, or puts it back. A removed annotation keeps its index, is no longer listed or
    /// drawn, and is left out when the document is saved, so removing can be undone even after saving.
    /// </summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="removed">Whether the annotation is removed.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetRemoved(HyperPdfDocument self, int pageIndex, int index, bool removed)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Changed(self, pageIndex, HyperPdfAnnotationReading.SetRemoved(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, index, removed));
        }
    }

    /// <summary>Removes an annotation for good; the indexes of later annotations on the page shift down.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns><see langword="true"/> when removed.</returns>
    internal static bool Remove(HyperPdfDocument self, int pageIndex, int index)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Changed(self, pageIndex, HyperPdfAnnotationReading.Remove(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, index));
        }
    }

    /// <summary>
    /// Saves the document to a stream. Documents with digital signatures are saved incrementally so the signatures stay
    /// valid; others are saved compactly.
    /// </summary>
    /// <param name="self">The owning document.</param>
    /// <param name="destination">The stream to write to.</param>
    /// <returns><see langword="true"/> when saved.</returns>
    internal static bool Save(HyperPdfDocument self, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            if (self.IsDisposed)
            {
                return false;
            }

            var version = Volatile.Read(ref self.EditVersion);
            if (!HyperPdfAnnotationSaving.Save(HyperPdfAnnotationStateAccess.GetAnnotations(self), destination))
            {
                return false;
            }

            Volatile.Write(ref self.SavedVersion, version);
            return true;
        }
    }

    /// <summary>Builds a stable annotation snapshot, then writes it without holding edit locks.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="destination">The output stream.</param>
    /// <param name="cancellationToken">Cancels building and writing.</param>
    /// <returns>Whether the snapshot was saved.</returns>
    internal static ValueTask<bool> SaveAsync(HyperPdfDocument self, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        Task write;
        HyperPdfAnnotations state;
        long version;
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            if (self.IsDisposed)
            {
                return ValueTask.FromResult(false);
            }

            version = Volatile.Read(ref self.EditVersion);
            state = HyperPdfAnnotationStateAccess.GetAnnotations(self);
            try
            {
                if (!HyperPdfAnnotationSaving.TryStartSaveAsync(state, destination, cancellationToken, out write))
                {
                    return ValueTask.FromResult(false);
                }
            }
            catch (Exception exception) when (exception is IOException or PdfException)
            {
                return ValueTask.FromResult(false);
            }
        }

        return FinishSaveAsync(self, state, version, write);
    }

    /// <summary>Marks a snapshot saved only when no later edit replaced it.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="state">The annotation state captured for the write.</param>
    /// <param name="version">The captured edit version.</param>
    /// <param name="write">The pending output write.</param>
    /// <returns>Whether the write completed.</returns>
    private static async ValueTask<bool> FinishSaveAsync(HyperPdfDocument self, HyperPdfAnnotations state, long version, Task write)
    {
        try
        {
            await write.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or PdfException or ObjectDisposedException)
        {
            return false;
        }

        lock (self.EditGate)
        {
            if (!self.IsDisposed && Volatile.Read(ref self.EditVersion) == version)
            {
                Volatile.Write(ref state.UnsavedChanges, 0);
                Volatile.Write(ref self.SavedVersion, version);
            }
        }

        return true;
    }
}
