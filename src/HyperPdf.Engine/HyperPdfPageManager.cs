// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Applies undoable page edits and owns the source documents retained by imported pages.</summary>
[DebuggerDisplay("HyperPdfPageManager: {UndoLabel}")]
internal sealed class HyperPdfPageManager : IPageManager, IDisposable
{
    /// <summary>The document whose caches and saved state follow each edit.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>The managed document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The lock shared with the owner's other edits and saves.</summary>
    private readonly Lock _gate;

    /// <summary>Sources kept open while imported objects or undo records can still refer to them.</summary>
    private readonly List<PdfDocument> _imports = [];

    /// <summary>The page-edit generation used to reject selections changed during asynchronous reads.</summary>
    private long _revision;

    /// <summary>Whether the manager has released its import sources.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfPageManager"/> class.</summary>
    /// <param name="owner">The document adapter.</param>
    /// <param name="gate">The lock shared with other document edits.</param>
    internal HyperPdfPageManager(HyperPdfDocument owner, Lock gate)
    {
        _owner = owner;
        _document = owner.Document;
        _gate = gate;
    }

    /// <inheritdoc/>
    public string? UndoLabel
    {
        get
        {
            lock (_gate)
            {
                return _disposed || _owner.IsDisposed ? null : PdfDocumentEditing.GetHistory(_document).UndoLabel;
            }
        }
    }

    /// <inheritdoc/>
    public string? RedoLabel
    {
        get
        {
            lock (_gate)
            {
                return _disposed || _owner.IsDisposed ? null : PdfDocumentEditing.GetHistory(_document).RedoLabel;
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask ApplyAsync(PageEditRequest request, CancellationToken cancellationToken) => ApplyCoreAsync(request, null, cancellationToken);

    /// <inheritdoc/>
    public ValueTask ApplyAsync(PageEditRequest request, PageEditReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        return ApplyCoreAsync(request, report, cancellationToken);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask InsertAsync(int index, string[] files, CancellationToken cancellationToken) =>
        InsertCoreAsync(index, files, null, cancellationToken);

    /// <inheritdoc/>
    public ValueTask InsertAsync(int index, string[] files, PageEditReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        return InsertCoreAsync(index, files, report, cancellationToken);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask InsertImagesAsync(int index, PageImageSource[] images, CancellationToken cancellationToken) =>
        InsertImagesCoreAsync(index, images, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask ExtractAsync(ReadOnlyMemory<int> pages, Stream destination, CancellationToken cancellationToken) =>
        ExtractCoreAsync(pages, destination, null, cancellationToken);

    /// <inheritdoc/>
    public ValueTask ExtractAsync(ReadOnlyMemory<int> pages, Stream destination, PageEditReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        return ExtractCoreAsync(pages, destination, report, cancellationToken);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> UndoAsync(CancellationToken cancellationToken) => new(StepHistory(true, cancellationToken));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> RedoAsync(CancellationToken cancellationToken) => new(StepHistory(false, cancellationToken));

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            using var access = HyperPdfNavigation.EnterPageWrite(_owner);
            foreach (var source in _imports)
            {
                source.Dispose();
            }

            _imports.Clear();
        }
    }

    /// <summary>Gets the history label of a supported edit.</summary>
    /// <param name="kind">The requested operation.</param>
    /// <returns>The undo description.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The operation is unsupported.</exception>
    private static string LabelFor(PageEditKind kind) => kind switch
    {
        PageEditKind.Delete => "Delete pages",
        PageEditKind.Rotate => "Rotate pages",
        PageEditKind.Move => "Move pages",
        PageEditKind.Duplicate => "Duplicate pages",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Applies a page edit inside the caller's transaction.</summary>
    /// <param name="document">The document being edited.</param>
    /// <param name="request">The operation, its frozen page selection and its turn or destination index.</param>
    /// <param name="report">Receives what a deletion or copy could not keep, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels page copying.</param>
    private static void ApplyCore(PdfDocument document, in PageEditRequest request, PdfPageEditReport? report, CancellationToken cancellationToken)
    {
        var pages = request.Pages.Span;
        var value = request.Value;
        switch (request.Kind)
        {
            case PageEditKind.Delete:
                {
                    PdfDocumentPageOperations.DeletePages(document, pages, report);
                    break;
                }

            case PageEditKind.Rotate:
                {
                    PdfDocumentPageOperations.RotatePages(document, pages, value);
                    break;
                }

            case PageEditKind.Move:
                {
                    PdfDocumentPageOperations.MovePages(document, pages, value);
                    break;
                }

            case PageEditKind.Duplicate:
                {
                    PdfDocumentPageOperations.InsertPages(document, value, document, pages, report, cancellationToken);
                    break;
                }
        }
    }

    /// <summary>Loads selected pages and the catalog structures that page operations carry or prune.</summary>
    /// <param name="document">The source document.</param>
    /// <param name="pages">The selected page indexes.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task completing when resources are ready.</returns>
    private static async ValueTask PrefetchAsync(PdfDocument document, ReadOnlyMemory<int> pages, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await PdfPrefetcher.PrefetchAsync(document.Objects, document.Catalog, PdfPrefetchKind.Page, cancellationToken).ConfigureAwait(false);
        await HyperPdfEditPrefetch.OutlineAsync(document, cancellationToken).ConfigureAwait(false);
        await PrefetchFormsAsync(document, cancellationToken).ConfigureAwait(false);
        _ = await PdfDocumentSeparations.GetSeparationsAsync(document, cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < pages.Length; index++)
        {
            _ = await PdfDocumentPages.GetPageAsync(document, pages.Span[index], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Loads every form-field subtree, including trees deeper than the page prefetch limit.</summary>
    /// <param name="document">The source document.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task completing when the field tree has been visited.</returns>
    private static async ValueTask PrefetchFormsAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        var fields = document.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields);
        if (fields is null)
        {
            return;
        }

        var pending = new List<PdfArray> { fields };
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        for (var index = 0; index < pending.Count; index++)
        {
            var children = pending[index];
            for (var child = 0; child < children.Count; child++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (children.GetDictionary(child) is not { } field || !visited.Add(field))
                {
                    continue;
                }

                await PdfPrefetcher.PrefetchAsync(document.Objects, field, PdfPrefetchKind.Page, cancellationToken).ConfigureAwait(false);
                if (field.GetArray(KnownName.Kids) is { } kids)
                {
                    pending.Add(kids);
                }
            }
        }
    }

    /// <summary>Opens and preloads one file, transferring ownership to the pending import list.</summary>
    /// <param name="path">The PDF path.</param>
    /// <param name="pending">The owned sources prepared so far.</param>
    /// <param name="cancellationToken">Cancels opening and resource loading.</param>
    /// <returns>A task completing after the source is ready.</returns>
    private static async ValueTask PrepareImportAsync(string path, List<ImportDocument> pending, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var source = await PdfDocumentReader.OpenAsync(path, null, cancellationToken).ConfigureAwait(false);
        try
        {
            ArgumentOutOfRangeException.ThrowIfZero(source.PageCount, nameof(path));
            var pages = new int[source.PageCount];
            for (var index = 0; index < pages.Length; index++)
            {
                pages[index] = index;
            }

            await PrefetchAsync(source, pages, cancellationToken).ConfigureAwait(false);
            pending.Add(new(source, pages));
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    /// <summary>Makes a PDF with a page for each picture and opens it as an import covering every page.</summary>
    /// <param name="images">The pictures.</param>
    /// <param name="cancellationToken">Cancels building the pages.</param>
    /// <returns>The import, owned by the caller.</returns>
    private static ImportDocument BuildImageImport(PageImageSource[] images, CancellationToken cancellationToken)
    {
        var source = PdfDocumentReader.Open(HyperPdfPictureDocuments.Create(images, cancellationToken), null);
        var pages = new int[source.PageCount];
        for (var index = 0; index < pages.Length; index++)
        {
            pages[index] = index;
        }

        return new(source, pages);
    }

    /// <summary>Commits every prepared source as one edit and retains its objects for saving and redo.</summary>
    /// <param name="index">The first inserted page's index.</param>
    /// <param name="pending">The sources whose ownership transfers on commit.</param>
    /// <param name="report">Receives what the copy could not keep, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels before committing.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InsertPrepared(int index, List<ImportDocument> pending, PdfPageEditReport? report, CancellationToken cancellationToken) =>
        InsertPrepared(index, pending, report, "Insert pages", cancellationToken);

    /// <summary>Commits every prepared source as one edit with the given undo label and retains its objects for saving and redo.</summary>
    /// <param name="index">The first inserted page's index.</param>
    /// <param name="pending">The sources whose ownership transfers on commit.</param>
    /// <param name="report">Receives what the copy could not keep, or <see langword="null"/>.</param>
    /// <param name="label">The undo description.</param>
    /// <param name="cancellationToken">Cancels before committing.</param>
    private void InsertPrepared(int index, List<ImportDocument> pending, PdfPageEditReport? report, string label, CancellationToken cancellationToken)
    {
        _ = _imports.EnsureCapacity(checked(_imports.Count + pending.Count));
        using var transaction = HyperPdfEditTransactions.Begin(_document, label);
        foreach (var source in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PdfDocumentPageOperations.InsertPages(_document, index, source.Document, source.Pages, report, cancellationToken);
            index = checked(index + source.Pages.Length);
        }

        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        foreach (var source in pending)
        {
            _imports.Add(source.Document);
        }

        pending.Clear();
        Changed();
    }

    /// <summary>Inserts every page of the files as one undo step.</summary>
    /// <param name="index">The first inserted page's zero-based index.</param>
    /// <param name="files">PDF files in insertion order.</param>
    /// <param name="report">Receives what the copy could not keep, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels opening and importing the files.</param>
    /// <returns>A task completing after the document has changed.</returns>
    private async ValueTask InsertCoreAsync(int index, string[] files, PageEditReport? report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        var changes = report is null ? null : new PdfPageEditReport();
        var paths = (string[])files.Clone();
        var revision = Snapshot(cancellationToken);
        lock (_gate)
        {
            ValidateRevision(revision, cancellationToken);
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _document.PageCount);
        }

        if (paths.Length == 0)
        {
            return;
        }

        var pending = new List<ImportDocument>(paths.Length);
        try
        {
            foreach (var path in paths)
            {
                await PrepareImportAsync(path, pending, cancellationToken).ConfigureAwait(false);
            }

            await PrefetchAsync(_document, ReadOnlyMemory<int>.Empty, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                ValidateRevision(revision, cancellationToken);
                using var access = HyperPdfNavigation.EnterPageWrite(_owner);
                InsertPrepared(index, pending, changes, cancellationToken);
            }

            HyperPdfSeparations.CopyReport(changes, report);
        }
        finally
        {
            foreach (var source in pending)
            {
                source.Document.Dispose();
            }
        }
    }

    /// <summary>Makes a page of each picture and inserts them as one undo step.</summary>
    /// <param name="index">The first inserted page's zero-based index.</param>
    /// <param name="images">The pictures, one page each.</param>
    /// <param name="cancellationToken">Cancels building and importing the pages.</param>
    /// <returns>A task completing after the document has changed.</returns>
    private async ValueTask InsertImagesCoreAsync(int index, PageImageSource[] images, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(images);
        var pictures = (PageImageSource[])images.Clone();
        var revision = Snapshot(cancellationToken);
        lock (_gate)
        {
            ValidateRevision(revision, cancellationToken);
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _document.PageCount);
        }

        if (pictures.Length == 0)
        {
            return;
        }

        var import = BuildImageImport(pictures, cancellationToken);
        var pending = new List<ImportDocument>(1) { import };
        try
        {
            await PrefetchAsync(import.Document, import.Pages, cancellationToken).ConfigureAwait(false);
            await PrefetchAsync(_document, ReadOnlyMemory<int>.Empty, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                ValidateRevision(revision, cancellationToken);
                using var access = HyperPdfNavigation.EnterPageWrite(_owner);
                InsertPrepared(index, pending, null, "Insert image pages", cancellationToken);
            }
        }
        finally
        {
            foreach (var source in pending)
            {
                source.Document.Dispose();
            }
        }
    }

    /// <summary>Applies a page edit as one undo step.</summary>
    /// <param name="request">The operation and zero-based indexes.</param>
    /// <param name="report">Receives what the edit could not keep, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels page resource reads and the edit.</param>
    /// <returns>A task completing after the document has changed.</returns>
    private async ValueTask ApplyCoreAsync(PageEditRequest request, PageEditReport? report, CancellationToken cancellationToken)
    {
        var label = LabelFor(request.Kind);
        ArgumentOutOfRangeException.ThrowIfZero(request.Pages.Length, nameof(request));

        // The selection is copied into a pooled array, so the caller's memory can change while pages are read.
        var rented = ArrayPool<int>.Shared.Rent(request.Pages.Length);
        try
        {
            request.Pages.Span.CopyTo(rented);
            var pages = rented.AsMemory(0, request.Pages.Length);
            var revision = Snapshot(cancellationToken);
            await PrefetchAsync(_document, pages, cancellationToken).ConfigureAwait(false);
            var changes = report is null ? null : new PdfPageEditReport();
            lock (_gate)
            {
                ValidateRevision(revision, cancellationToken);
                using var access = HyperPdfNavigation.EnterPageWrite(_owner);
                using var transaction = HyperPdfEditTransactions.Begin(_document, label);
                ApplyCore(_document, new(request.Kind, pages, request.Value), changes, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                transaction.Commit();
                Changed();
            }

            HyperPdfSeparations.CopyReport(changes, report);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rented);
        }
    }

    /// <summary>Writes the selected pages to a new PDF.</summary>
    /// <param name="pages">Zero-based indexes in output order.</param>
    /// <param name="destination">The stream receiving the new PDF.</param>
    /// <param name="report">Receives what the copy could not keep, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels page reads and writing.</param>
    /// <returns>A task completing after the file has been written.</returns>
    private async ValueTask ExtractCoreAsync(ReadOnlyMemory<int> pages, Stream destination, PageEditReport? report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfZero(pages.Length, nameof(pages));

        // The selection is copied into a pooled array, so the caller's memory can change while pages are read.
        var rented = ArrayPool<int>.Shared.Rent(pages.Length);
        try
        {
            pages.Span.CopyTo(rented);
            var selected = rented.AsMemory(0, pages.Length);
            var revision = Snapshot(cancellationToken);
            await PrefetchAsync(_document, selected, cancellationToken).ConfigureAwait(false);
            var changes = report is null ? null : new PdfPageEditReport();
            byte[] bytes;
            lock (_gate)
            {
                ValidateRevision(revision, cancellationToken);
                using var access = HyperPdfNavigation.EnterPageWrite(_owner);
                bytes = PdfDocumentPageOperations.ExtractPages(_document, selected.Span, changes, cancellationToken);
            }

            await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            HyperPdfSeparations.CopyReport(changes, report);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rented);
        }
    }

    /// <summary>Captures the page generation while checking lifetime and cancellation.</summary>
    /// <param name="cancellationToken">Cancels before loading.</param>
    /// <returns>The current page generation.</returns>
    private long Snapshot(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            return _revision;
        }
    }

    /// <summary>Rejects page selections made stale while resources were loading.</summary>
    /// <param name="revision">The generation before loading.</param>
    /// <param name="cancellationToken">Cancels before editing.</param>
    /// <exception cref="InvalidOperationException">The selection became stale while loading.</exception>
    private void ValidateRevision(long revision, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (_revision != revision)
        {
            throw new InvalidOperationException("The pages changed while preparing this action. Select the pages again.");
        }
    }

    /// <summary>Applies a history step under the document's shared edit lock.</summary>
    /// <param name="undo">Whether to revert rather than reapply.</param>
    /// <param name="cancellationToken">Cancels before changing history.</param>
    /// <returns>Whether a history entry was applied.</returns>
    private bool StepHistory(bool undo, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            using var access = HyperPdfNavigation.EnterPageWrite(_owner);
            var changed = HyperPdfEditTransactions.Step(_document, undo);
            if (changed)
            {
                Changed();
            }

            return changed;
        }
    }

    /// <summary>Publishes a committed edit to the adapter's page caches and unsaved state.</summary>
    private void Changed()
    {
        _revision++;
        HyperPdfNavigation.PagesEdited(_owner);
    }

    /// <summary>Rejects operations after either owner has been disposed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed || _owner.IsDisposed, this);

    /// <summary>A prepared source whose lifetime transfers when insertion commits.</summary>
    /// <param name="Document">The source kept open for its imported objects.</param>
    /// <param name="Pages">Its pages in insertion order.</param>
    [DebuggerDisplay("ImportDocument: {Pages.Length} pages")]
    private sealed record ImportDocument(PdfDocument Document, int[] Pages);
}
