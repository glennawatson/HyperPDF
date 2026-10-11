// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// An open PDF document. The file is read through a <see cref="IO.PdfByteSource"/> as needed, never loaded whole;
/// objects are parsed on first use and cached, so opening a large file reads only its cross-reference table and page
/// tree. Safe to call from any thread.
/// </summary>
[DebuggerDisplay("PdfDocument: {PageCount} pages")]
public sealed class PdfDocument : IDisposable
{
    /// <summary>The caches shared by this document's operations.</summary>
    private PdfDocumentState _state = new();

    /// <summary>Initializes a new instance of the <see cref="PdfDocument"/> class.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="fontFactory">The font loader for the document.</param>
    internal PdfDocument(PdfObjectStore objects, Func<PdfDictionary, PdfFont?>? fontFactory)
    {
        Objects = objects;
        FontFactory = fontFactory;
        DrawingServices = objects.Context?.DrawingServices ?? new(new());
        State.PageSet = PdfPageSet.Read(objects);
    }

    /// <summary>Gets the document's objects.</summary>
    public PdfObjectStore Objects { get; }

    /// <summary>Gets the document catalog.</summary>
    public PdfDictionary Catalog => Objects.Catalog;

    /// <summary>Gets the number of pages.</summary>
    public int PageCount => PdfDocumentPages.GetPageSet(this).Pages.Length;

    /// <summary>Gets a value indicating whether the document is encrypted.</summary>
    public bool IsEncrypted => Objects.Security is not null;

    /// <summary>Gets a value indicating whether the document has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref State.Disposed) != 0;

    /// <summary>Gets the drawing services selected when this document was opened.</summary>
    public PdfDrawingServices DrawingServices { get; }

    /// <summary>Gets the font loader selected for this document.</summary>
    internal Func<PdfDictionary, PdfFont?>? FontFactory { get; }

    /// <summary>Gets the cache state owned by this document.</summary>
    internal ref PdfDocumentState State => ref _state;

    /// <summary>
    /// Closes the file and empties the document's caches: the parsed objects, the page pictures of every renderer made for
    /// the document, the decoded images, fonts and other render caches, and the recent text pages. Work that is running on
    /// other threads fails with <see cref="ObjectDisposedException"/> or finishes with what it already holds; it never
    /// reads freed memory. The shared decoder scratch buffers are trimmed too.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref State.Disposed, 1) != 0)
        {
            return;
        }

        // Closing the objects first means nothing running can parse and re-fill the caches that are emptied next.
        Objects.Dispose();
        PdfDocumentRendering.ReleaseRendering(this);
        _ = Interlocked.Exchange(ref State.TextBuild, null);
        _ = Interlocked.Exchange(ref State.FontPreparations, null);
        ScratchPools.Trim();
    }

    /// <summary>Invalidates document caches when the owned object store changes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void OnObjectsChanged() => PdfDocumentEditing.InvalidateCaches(this);
}
