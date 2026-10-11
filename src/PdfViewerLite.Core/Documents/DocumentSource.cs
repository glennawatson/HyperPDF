// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.Core.Documents;

/// <summary>
/// A document a tab refers to. The native document is opened on demand through its <see cref="DocumentPool"/> and may
/// be closed again when the pool needs room; page sizes, outline and metadata are kept so a closed document can still
/// be laid out and navigated.
/// </summary>
[DebuggerDisplay("DocumentSource: {FilePath} (open: {IsOpen})")]
public sealed class DocumentSource
{
    /// <summary>The last identifier handed out.</summary>
    private static int _lastId;

    /// <summary>The owning pool.</summary>
    private readonly DocumentPool _pool;

    /// <summary>The open document, if any.</summary>
    private IDocument? _document;

    /// <summary>Changes whenever a pending open must stop before publishing its result.</summary>
    private int _lifetimeVersion;

    /// <summary>True when serialized edits were not published at their destination.</summary>
    private bool _unpublishedSave;

    /// <summary>Initializes a new instance of the <see cref="DocumentSource"/> class.</summary>
    /// <param name="pool">The owning pool.</param>
    /// <param name="filePath">The file path.</param>
    /// <param name="password">The password.</param>
    internal DocumentSource(DocumentPool pool, string filePath, string? password)
    {
        _pool = pool;
        FilePath = filePath;
        Password = password;
        Id = Interlocked.Increment(ref _lastId);
    }

    /// <summary>Gets an identifier that changes whenever the file is reloaded, used to key cached tiles.</summary>
    public int Id { get; private set; }

    /// <summary>Gets the file path.</summary>
    public string FilePath { get; }

    /// <summary>Gets or sets the password used to open the document.</summary>
    public string? Password { get; set; }

    /// <summary>Gets a value indicating whether the native document is open.</summary>
    public bool IsOpen => _document is { IsDisposed: false };

    /// <summary>Gets a value indicating whether the open document has edits that are not saved; such documents are never closed to save memory.</summary>
    public bool HasUnsavedChanges => IsOpen && (_unpublishedSave || ((_document)?.GetFeature(typeof(IAnnotationEditor)) as IAnnotationEditor) is { HasUnsavedChanges: true });

    /// <summary>Gets the page sizes, or an empty array before the first open.</summary>
    public PageSize[] PageSizes { get; private set; } = [];

    /// <summary>Gets the number of pages, or zero before the first open.</summary>
    public int PageCount => PageSizes.Length;

    /// <summary>Gets the cached outline, or <see langword="null"/> before the first open.</summary>
    public IReadOnlyList<OutlineNode>? Outline { get; private set; }

    /// <summary>Gets the cached metadata, or <see langword="null"/> before the first open.</summary>
    public DocumentMetadata? Metadata { get; private set; }

    /// <summary>Gets the time the document was last acquired, in <see cref="Environment.TickCount64"/> units.</summary>
    public long LastUsed { get; private set; }

    /// <summary>Gets the open document, opening it when necessary.</summary>
    /// <returns>The document.</returns>
    /// <exception cref="DocumentOpenException">Thrown when the document cannot be opened.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDocument Acquire() => _pool.Acquire(this);

    /// <summary>Gets the open document, using cancellable I/O when it must be opened.</summary>
    /// <param name="cancellationToken">Cancels a pending open without discarding a ready document.</param>
    /// <returns>The document; a ready document completes without scheduling.</returns>
    /// <exception cref="DocumentOpenException">The document cannot be opened.</exception>
    /// <exception cref="OperationCanceledException">The open was cancelled or the source closed before publication.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<IDocument> AcquireAsync(CancellationToken cancellationToken) => _pool.AcquireAsync(this, cancellationToken);

    /// <summary>Keeps the document open when its serialized edits could not replace the destination file.</summary>
    public void MarkSaveUnpublished() => _unpublishedSave = true;

    /// <summary>Clears an earlier publication failure after the destination file was replaced.</summary>
    public void MarkSavePublished() => _unpublishedSave = false;

    /// <summary>Closes the document and forgets cached information so the next acquire reads the file again.</summary>
    public void Reload()
    {
        Close();
        _unpublishedSave = false;
        PageSizes = [];
        Outline = null;
        Metadata = null;
        Id = Interlocked.Increment(ref _lastId);
    }

    /// <summary>Refreshes page sizes and navigation after editing the open document, preserving its unsaved changes.</summary>
    public void RefreshStructure()
    {
        var document = Acquire();
        PageSizes = document.GetPageSizes();
        Outline = document.GetOutline();
        Metadata = document.GetMetadata();
        Id = Interlocked.Increment(ref _lastId);
    }

    /// <summary>Opens the native document when it is not already open.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>The document.</returns>
    internal IDocument OpenIfNeeded(IDocumentEngine engine)
    {
        LastUsed = Environment.TickCount64;
        if (_document is { IsDisposed: false } open)
        {
            return open;
        }

        var document = engine.Open(FilePath, Password);
        _document = document;
        if (PageSizes.Length == 0)
        {
            PageSizes = document.GetPageSizes();
            Metadata = document.GetMetadata();
            Outline = document.GetOutline();
        }

        return document;
    }

    /// <summary>Opens on a cache miss and returns a ready result on a hit.</summary>
    /// <param name="engine">The engine that opens this source.</param>
    /// <param name="cancellationToken">Cancels a cold open.</param>
    /// <returns>The document.</returns>
    internal ValueTask<IDocument> OpenIfNeededAsync(IDocumentEngine engine, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastUsed = Environment.TickCount64;
        return _document is { IsDisposed: false } open
            ? ValueTask.FromResult(open)
            : OpenColdAsync(engine, cancellationToken);
    }

    /// <summary>Closes the native document, keeping cached information.</summary>
    internal void Close()
    {
        _ = Interlocked.Increment(ref _lifetimeVersion);
        var document = _document;
        _document = null;
        document?.Dispose();
    }

    /// <summary>Publishes only an open that remains current after I/O and parsing finish.</summary>
    /// <param name="engine">The document engine.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The opened document.</returns>
    /// <exception cref="OperationCanceledException">The source was closed while opening.</exception>
    private async ValueTask<IDocument> OpenColdAsync(IDocumentEngine engine, CancellationToken cancellationToken)
    {
        var version = Volatile.Read(ref _lifetimeVersion);

        // The document pool belongs to the UI thread, so resume there before publishing and trimming it.
        var document = await engine.OpenAsync(FilePath, Password, cancellationToken);
        if (cancellationToken.IsCancellationRequested || version != Volatile.Read(ref _lifetimeVersion))
        {
            document.Dispose();
            throw new OperationCanceledException("The document source changed while opening.", cancellationToken);
        }

        if (_document is { IsDisposed: false } open)
        {
            document.Dispose();
            return open;
        }

        try
        {
            if (PageSizes.Length == 0)
            {
                var sizes = document.GetPageSizes();
                var metadata = document.GetMetadata();
                var outline = document.GetOutline();
                PageSizes = sizes;
                Metadata = metadata;
                Outline = outline;
            }

            _document = document;
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }
}
