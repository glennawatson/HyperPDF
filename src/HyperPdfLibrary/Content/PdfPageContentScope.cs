// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Content;

/// <summary>
/// Keeps the decoded content of each page of one document view while a check runs, so the readers that walk the same
/// pages (the marked content recorder, the structure reference scan and the PDF/A text walk) decode each page once.
/// The scope belongs to the check that opens it and is disposed when the check ends. It refuses to serve once the
/// document's objects change, and it keeps no page above <see cref="MaxPageBytes"/> and no more than
/// <see cref="MaxTotalBytes"/> in all; a page it does not keep is decoded again by each reader, as without a scope.
/// A disposed scope goes back to a small pool with the arrays it grew, so a check does not allocate one.
/// </summary>
[DebuggerDisplay("PdfPageContentScope: {_retained} bytes kept")]
internal sealed class PdfPageContentScope : IDisposable
{
    /// <summary>The most bytes of one page's decoded content the scope keeps.</summary>
    internal const int MaxPageBytes = 8 << 20;

    /// <summary>The most bytes of decoded content, counted by rented array size, the scope keeps in all.</summary>
    internal const long MaxTotalBytes = 64L << 20;

    /// <summary>The most pages an idle scope keeps room for.</summary>
    private const int MaxKeptPages = 1 << 12;

    /// <summary>Guards the per-page slots, which are three arrays that must change together.</summary>
    private readonly Lock _gate = new();

    /// <summary>The rented array holding each kept page's content, by page index; <see langword="null"/> when none is kept.</summary>
    private byte[]?[] _arrays = [];

    /// <summary>The bytes of content in each kept array.</summary>
    private int[] _lengths = [];

    /// <summary>The page dictionary each kept array was decoded for.</summary>
    private PdfDictionary?[] _owners = [];

    /// <summary>The document the scope reads from; <see langword="null"/> while the scope sits idle.</summary>
    private PdfDocument? _document;

    /// <summary>The number of pages the scope can keep.</summary>
    private int _pageCount;

    /// <summary>The document's revision when the scope opened; the scope serves nothing after it changes.</summary>
    private int _revision;

    /// <summary>Whether this scope is the one installed on the document.</summary>
    private bool _installed;

    /// <summary>The bytes of rented arrays kept so far.</summary>
    private long _retained;

    /// <summary>Gets a value indicating whether the scope may still serve: it is open and the document's objects have not changed.</summary>
    private bool IsServing => _document is { } document && PdfDocumentCaches.Revision(document) == _revision;

    /// <summary>Returns the kept arrays to the pool, removes the scope from the document and keeps the scope for the next check.</summary>
    public void Dispose()
    {
        PdfDocument? document;
        lock (_gate)
        {
            document = _document;
            if (document is null)
            {
                return;
            }

            _document = null;
            for (var i = 0; i < _pageCount; i++)
            {
                if (_arrays[i] is { } array)
                {
                    ArrayPool<byte>.Shared.Return(array);
                }

                _arrays[i] = null;
                _owners[i] = null;
            }

            _pageCount = 0;
            _retained = 0;
        }

        if (_installed)
        {
            _ = Interlocked.CompareExchange(ref document.State.PageContentScope, null, this);
            _installed = false;
        }

        if (_arrays.Length <= MaxKeptPages)
        {
            _ = document.Objects.ScratchPools.Slots<PdfPageContentScope>().Offer(this);
        }
    }

    /// <summary>Opens a scope on a document. When the document already has one, the new scope does nothing and the older one keeps serving.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The scope; dispose it when the check ends.</returns>
    internal static PdfPageContentScope Begin(PdfDocument document)
    {
        var scope = document.Objects.ScratchPools.Slots<PdfPageContentScope>().Take() ?? new();
        scope.Open(document);
        return scope;
    }

    /// <summary>Gets the content of a page, decoding it once for the readers that share the scope.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="cancellationToken">Cancels stream decoding.</param>
    /// <returns>The content; dispose the lease when done reading.</returns>
    /// <exception cref="InvalidDataException">The content cannot be decoded; nothing is kept.</exception>
    internal static PageContentLease Lease(PdfDocument document, PdfPage page, CancellationToken cancellationToken)
    {
        var scope = Volatile.Read(ref document.State.PageContentScope);
        if (scope is not null && scope.TryGet(page, out var shared))
        {
            return shared;
        }

        var buffer = default(PooledBuffer);
        try
        {
            ContentExecution.DecodeContents(page, ref buffer, cancellationToken);
        }
        catch
        {
            buffer.Dispose();
            throw;
        }

        var array = buffer.TakeArray(out var length);
        return scope is null ? new(array, length) : scope.Keep(page, array, length);
    }

    /// <summary>
    /// Gets the content of a page from the scope when the document has one and the content decodes. A page whose content is
    /// damaged is left to the caller to read in its own way.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="cancellationToken">Cancels stream decoding.</param>
    /// <param name="lease">Receives the content; dispose it when done reading.</param>
    /// <returns><see langword="true"/> when <paramref name="lease"/> holds the content; <see langword="false"/> when there is no scope or the content could not be decoded.</returns>
    internal static bool TryLease(PdfDocument document, PdfPage page, CancellationToken cancellationToken, out PageContentLease lease)
    {
        lease = default;
        if (Volatile.Read(ref document.State.PageContentScope) is null)
        {
            return false;
        }

        try
        {
            lease = Lease(document, page, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ContentExecution.IsRecoverable(ex))
        {
            return false;
        }
    }

    /// <summary>Starts the scope on a document and installs it unless the document already has one.</summary>
    /// <param name="document">The document.</param>
    private void Open(PdfDocument document)
    {
        var pages = document.PageCount;
        lock (_gate)
        {
            if (_arrays.Length < pages)
            {
                _arrays = new byte[]?[pages];
                _lengths = new int[pages];
                _owners = new PdfDictionary?[pages];
            }

            _pageCount = pages;
            _revision = PdfDocumentCaches.Revision(document);
            _retained = 0;
            _document = document;
        }

        _installed = Interlocked.CompareExchange(ref document.State.PageContentScope, this, null) is null;
    }

    /// <summary>Finds the kept content of a page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="lease">Receives the content; it owns nothing.</param>
    /// <returns><see langword="true"/> when the page's current content is kept and the scope is still valid.</returns>
    private bool TryGet(PdfPage page, out PageContentLease lease)
    {
        lease = default;
        lock (_gate)
        {
            var index = page.Index;
            if (!IsServing || (uint)index >= (uint)_pageCount || !ReferenceEquals(_owners[index], page.Dictionary))
            {
                return false;
            }

            lease = new(_arrays[index], _lengths[index], false);
            return true;
        }
    }

    /// <summary>Keeps a page's freshly decoded content when it fits, so the next reader can share it.</summary>
    /// <param name="page">The page.</param>
    /// <param name="array">The rented array holding the content, or <see langword="null"/> for empty content; the scope or the lease now owns it.</param>
    /// <param name="length">The bytes of content.</param>
    /// <returns>The lease the caller reads from.</returns>
    private PageContentLease Keep(PdfPage page, byte[]? array, int length)
    {
        var index = page.Index;
        var size = array?.Length ?? 0;
        lock (_gate)
        {
            if (!IsServing || (uint)index >= (uint)_pageCount || _owners[index] is not null
                || length > MaxPageBytes || _retained + size > MaxTotalBytes)
            {
                // Another reader kept this page first, or the content does not fit: this reader owns its array.
                return new(array, length);
            }

            _arrays[index] = array;
            _lengths[index] = length;
            _owners[index] = page.Dictionary;
            _retained += size;
            return new(array, length, false);
        }
    }
}
