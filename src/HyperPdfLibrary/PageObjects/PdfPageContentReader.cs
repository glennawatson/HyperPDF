// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Reads page and form content streams into page objects.</summary>
public static class PdfPageContentReader
{
    /// <summary>The nesting of forms read into objects.</summary>
    internal const int MaxFormDepth = 16;

    /// <summary>Reads a page's content into objects.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <returns>The content.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "document"/> is <see langword="null"/>.</exception>
    /// <exception cref = "ArgumentOutOfRangeException"><paramref name = "pageIndex"/> is not a page.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfPageContent Read(PdfDocument document, int pageIndex) => PdfPageContentReader.Read(document, pageIndex, CancellationToken.None);

    /// <summary>Reads a page's content into objects.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "cancellationToken">Cancels the read before it starts.</param>
    /// <returns>The content.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "document"/> is <see langword="null"/>.</exception>
    /// <exception cref = "ArgumentOutOfRangeException"><paramref name = "pageIndex"/> is not a page.</exception>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<PdfPageContent> ReadAsync(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(PdfPageContentReader.Read(document, pageIndex, cancellationToken));
    }

    /// <summary>Reads a page's content into objects, checking a token while it walks the operators.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "cancellationToken">Cancels the read.</param>
    /// <returns>The content.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "document"/> is <see langword="null"/>.</exception>
    /// <exception cref = "ArgumentOutOfRangeException"><paramref name = "pageIndex"/> is not a page.</exception>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    internal static PdfPageContent Read(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        var page = PdfDocumentPages.GetPage(document, pageIndex);

        // The page object may predate edits made in the open transaction, so the current dictionary is the authority.
        var current = PdfPageAnnotations.GetPageDictionary(document.Objects, page);
        var buffer = default(PooledBuffer);
        try
        {
            ContentExecution.DecodeContents(current.Get(KnownName.Contents), ref buffer, cancellationToken);
            var content = new PdfPageContent(document, page, null, current.GetDictionary(KnownName.Resources) ?? page.Resources, Matrix3x2.Identity, buffer.ToArray(), 0);
            PdfPageContentReader.Load(content, null, cancellationToken);
            return content;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Reads the content of a form object into objects whose matrices include the form's matrix.</summary>
    /// <param name = "owner">The content that paints the form.</param>
    /// <param name = "form">The form object.</param>
    /// <returns>The form's content.</returns>
    /// <exception cref = "InvalidOperationException">The forms nest too deeply.</exception>
    internal static PdfPageContent ReadForm(PdfPageContent owner, PdfFormObject form)
    {
        if (owner.Depth >= PdfPageContentReader.MaxFormDepth)
        {
            throw new InvalidOperationException("The forms nest too deeply.");
        }

        var resources = form.Stream.Dictionary.GetDictionary(KnownName.Resources) ?? owner.Resources;
        var start = form.FormMatrix * form.Matrix;
        var content = new PdfPageContent(owner.Document, null, form, resources, start, form.Stream.DecodeToArray(), owner.Depth + 1);
        PdfPageContentReader.Load(content, PdfPageContentReader.FormClip(form, start), CancellationToken.None);
        return content;
    }

    /// <summary>Gets the clip a form's bounding box puts on its content, inside the clip it is painted with.</summary>
    /// <param name = "form">The form object.</param>
    /// <param name = "start">The matrix from the form's space to user space.</param>
    /// <returns>The innermost clip, or <see langword="null"/> when there is none.</returns>
    internal static ClipNode? FormClip(PdfFormObject form, Matrix3x2 start)
    {
        var parent = form.ClipChain;
        if (form.BoundingBox is not { } box)
        {
            return parent;
        }

        PdfPathSegment[] outline = [new(PdfPathSegmentKind.Rectangle, box.Left, box.Bottom, box.Width, box.Height, 0, 0)];
        var bounds = PathBounds.Measure(outline, start, 0);
        var clipped = parent is null ? bounds : parent.Bounds.Intersect(bounds);
        return new(parent, new(outline, start, false, bounds), clipped);
    }

    /// <summary>Parses the content into objects.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "clip">The clip the content starts inside, or <see langword="null"/>.</param>
    /// <param name = "cancellationToken">Cancels the walk.</param>
    internal static void Load(PdfPageContent state, ClipNode? clip, CancellationToken cancellationToken)
    {
        // The document keeps one set of parse lists between reads; a read that finds it taken by another thread makes its own.
        ref var slot = ref state.Document.State;
        var scratch = Interlocked.Exchange(ref slot.ParseScratch, null) ?? new PageContentParseScratch();
        try
        {
            var parser = scratch.Parser;
            parser.Bind(state, state.SourceBytes, state.Resources, state.BaseMatrix, clip);
            state.ObjectItems.AddRange(PageContentParse.Parse(parser, cancellationToken));
            state.Underflow = parser.Underflow;
            state.Unclosed = parser.Unclosed;
        }
        finally
        {
            scratch.Clear();
            Volatile.Write(ref slot.ParseScratch, scratch);
        }
    }
}
