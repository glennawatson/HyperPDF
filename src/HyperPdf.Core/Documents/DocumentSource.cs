// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Documents;

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

    /// <summary>The certificate file and password used to reopen a public-key encrypted document.</summary>
    private CertificateCredential? _certificate;

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
    public bool HasUnsavedChanges => IsOpen && (_unpublishedSave || DocumentFeature<IAnnotationEditor>.Get(_document) is { HasUnsavedChanges: true });

    /// <summary>Gets the page sizes, or an empty array before the first open.</summary>
    public PageSize[] PageSizes { get; private set; } = [];

    /// <summary>Gets the number of pages, or zero before the first open.</summary>
    public int PageCount => PageSizes.Length;

    /// <summary>Gets the cached outline, or <see langword="null"/> before the first open.</summary>
    public OutlineNode[]? Outline { get; private set; }

    /// <summary>Gets the cached metadata, or <see langword="null"/> before the first open.</summary>
    public DocumentMetadata? Metadata { get; private set; }

    /// <summary>Gets the time the document was last acquired, in <see cref="Environment.TickCount64"/> units.</summary>
    public long LastUsed { get; private set; }

    /// <summary>Uses a certificate file for this tab's next open and any cache reopen.</summary>
    /// <param name="certificatePath">The PKCS #12 file path.</param>
    /// <param name="certificatePassword">The password, held only in memory while this tab is open.</param>
    public void UseCertificate(string certificatePath, string certificatePassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
        ArgumentNullException.ThrowIfNull(certificatePassword);
        _certificate = new(certificatePath, certificatePassword);
        Reload();
    }

    /// <summary>Gets the open document, opening it when necessary.</summary>
    /// <returns>The document.</returns>
    /// <exception cref="DocumentOpenException">Thrown when the document cannot be opened.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDocument Acquire() => _pool.Acquire(this);

    /// <summary>Gets the open document, using cancellable I/O when it must be opened.</summary>
    /// <param name="owner">The sequencer of the thread that owns the pool; the open document is published there.</param>
    /// <returns>
    /// A cold signal giving the document once; a ready document is given without scheduling. Unsubscribing cancels a pending open
    /// without discarding a ready document. It fails with <see cref="DocumentOpenException"/> when the document cannot be opened and with
    /// <see cref="OperationCanceledException"/> when the source closed before publication.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<IDocument> Load(ISequencer owner) => _pool.Load(this, owner);

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

        var document = OpenWithCredentials(engine);
        _document = document;
        if (PageSizes.Length == 0)
        {
            PageSizes = document.GetPageSizes();
            Metadata = document.GetMetadata();
            Outline = document.GetOutline();
        }

        return document;
    }

    /// <summary>Opens on a cache miss and gives a ready result on a hit.</summary>
    /// <param name="engine">The engine that opens this source.</param>
    /// <param name="owner">The sequencer of the thread that owns the pool.</param>
    /// <returns>A cold signal giving the document once; unsubscribing cancels a cold open.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IObservable<IDocument> LoadIfNeeded(IDocumentEngine engine, ISequencer owner) =>
        Signal.Defer(() =>
        {
            LastUsed = Environment.TickCount64;
            return _document is { IsDisposed: false } open
                ? Signal.Return(open)
                : LoadCold(engine, owner);
        });

    /// <summary>Closes the native document, keeping cached information.</summary>
    internal void Close()
    {
        _ = Interlocked.Increment(ref _lifetimeVersion);
        var document = _document;
        _document = null;
        document?.Dispose();
    }

    /// <summary>Clears credentials when the tab is removed from its pool.</summary>
    internal void ReleaseCredentials()
    {
        Password = null;
        _certificate = null;
    }

    /// <summary>Opens the native document without blocking, and publishes it only if the source did not change meanwhile.</summary>
    /// <param name="engine">The document engine.</param>
    /// <param name="owner">The sequencer of the thread that owns the pool.</param>
    /// <returns>A cold signal giving the opened document once.</returns>
    private IObservable<IDocument> LoadCold(IDocumentEngine engine, ISequencer owner)
    {
        var version = Volatile.Read(ref _lifetimeVersion);

        // The document pool belongs to the UI thread, so the open document is brought there before it is published and trimmed.
        return LoadWithCredentials(engine)
            .ObserveOn(owner)
            .Select(document => Publish(document, version));
    }

    /// <summary>Publishes only an open that remains current after I/O and parsing finish.</summary>
    /// <param name="document">The document that was opened.</param>
    /// <param name="version">The lifetime version the open started under.</param>
    /// <returns>The document to use.</returns>
    /// <exception cref="OperationCanceledException">The source was closed while opening.</exception>
    private IDocument Publish(IDocument document, int version)
    {
        if (version != Volatile.Read(ref _lifetimeVersion))
        {
            document.Dispose();
            throw new OperationCanceledException("The document source changed while opening.");
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

    /// <summary>Uses the credential selected for this source.</summary>
    /// <param name="engine">The document engine.</param>
    /// <returns>The opened document.</returns>
    /// <exception cref="DocumentOpenException">The engine cannot use certificates.</exception>
    private IDocument OpenWithCredentials(IDocumentEngine engine)
    {
        if (_certificate is not { } credential)
        {
            return engine.Open(FilePath, Password);
        }

        return engine is ICertificateDocumentEngine certificates
            ? certificates.OpenWithCertificate(FilePath, credential.Path, credential.Password)
            : throw new DocumentOpenException(DocumentOpenError.Certificate, "This document engine cannot open certificate-encrypted files.");
    }

    /// <summary>Uses the credential selected for this source with cancellable I/O.</summary>
    /// <param name="engine">The document engine.</param>
    /// <returns>A cold signal giving the opened document once; unsubscribing cancels the open.</returns>
    private IObservable<IDocument> LoadWithCredentials(IDocumentEngine engine)
    {
        if (_certificate is not { } credential)
        {
            return engine.Load(FilePath, Password);
        }

        return engine is ICertificateDocumentEngine certificates
            ? certificates.LoadWithCertificate(FilePath, credential.Path, credential.Password)
            : Signal.Throw<IDocument>(new DocumentOpenException(DocumentOpenError.Certificate, "This document engine cannot open certificate-encrypted files."));
    }

    /// <summary>The certificate selection retained by one tab.</summary>
    /// <param name="Path">The certificate file.</param>
    /// <param name="Password">The certificate password.</param>
    private sealed record CertificateCredential(string Path, string Password)
    {
        /// <inheritdoc/>
        public override string ToString() => Path;
    }
}
