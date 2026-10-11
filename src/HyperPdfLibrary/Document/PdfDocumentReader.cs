// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Opens and creates PDF documents.</summary>
public static class PdfDocumentReader
{
    /// <summary>Opens a file with async I/O.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the open, including the work after the file is read.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file cannot be read, is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<PdfDocument> OpenAsync(
        string path,
        string? password,
        CancellationToken cancellationToken) =>
        PdfDocumentReader.OpenWithAsync(
        path,
        password is null ? PdfOpenOptions.Default : new PdfOpenOptions { Password = password },
        cancellationToken);

    /// <summary>Opens a document read from a seekable stream with async I/O.</summary>
    /// <param name="stream">The readable, seekable stream; it must stay open and unchanged until the document is disposed.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the open, including the work after the stream is read.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentException">The stream cannot read or seek.</exception>
    /// <exception cref="PdfException">The stream is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<PdfDocument> OpenAsync(
        Stream stream,
        string? password,
        CancellationToken cancellationToken) =>
        PdfDocumentReader.OpenWithAsync(
        stream,
        password is null ? PdfOpenOptions.Default : new PdfOpenOptions { Password = password },
        cancellationToken);

    /// <summary>Opens a document held in memory; there is nothing to wait for, so the result is ready at once.</summary>
    /// <param name="bytes">The file's bytes; kept, not copied, and must not change.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The bytes are not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<PdfDocument> OpenAsync(byte[] bytes, string? password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return PdfDocumentReader.OpenWithAsync(new MemoryPdfByteSource(bytes), true, password is null ? PdfOpenOptions.Default : new PdfOpenOptions { Password = password }, cancellationToken);
    }

    /// <summary>
    /// Opens a file with options and async I/O. <see cref="PdfOpenOptions.Source"/> chooses how the file is read:
    /// <see cref="PdfSourceKind.Memory"/> reads it whole and <see cref="PdfSourceKind.Stream"/> loads it through a page
    /// cache, both without blocking a thread; mapped and automatic sources open as <see cref="OpenWith(string, PdfOpenOptions)"/> does.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the open; it also stops later decoding of the document's streams.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file cannot be read, is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<PdfDocument> OpenWithAsync(string path, PdfOpenOptions options, CancellationToken cancellationToken) =>
        OpenWithAsync(path, options, cancellationToken, cancellationToken.CanBeCanceled ? cancellationToken : options.CancellationToken);

    /// <summary>Opens a file with a token for this open and a separate token retained by the document.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="options">The file source and recovery options.</param>
    /// <param name="cancellationToken">Cancels I/O and CPU work during the open.</param>
    /// <param name="documentCancellationToken">Cancels later document operations.</param>
    /// <returns>The opened document.</returns>
    /// <exception cref="PdfException">The file cannot be read, is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The open was cancelled.</exception>
    public static async ValueTask<PdfDocument> OpenWithAsync(string path, PdfOpenOptions options, CancellationToken cancellationToken, CancellationToken documentCancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(options);
        PdfByteSource source;
        try
        {
            source = await PdfByteSources.OpenAsync(path, options.Source, options.CacheBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            throw new PdfException($"The file '{path}' could not be read.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PdfException($"The file '{path}' could not be read.", ex);
        }

        return await PdfDocumentReader.OpenWithAsync(source, true, options, cancellationToken, documentCancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a document read from a seekable stream with options and async I/O.</summary>
    /// <param name="stream">The readable, seekable stream; it must stay open and unchanged until the document is disposed.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the open; it also stops later decoding of the document's streams.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentException">The stream cannot read or seek.</exception>
    /// <exception cref="PdfException">The stream is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<PdfDocument> OpenWithAsync(Stream stream, PdfOpenOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        return PdfDocumentReader.OpenWithAsync(new StreamPdfByteSource(stream, options.CacheBytes, false), true, options, cancellationToken);
    }

    /// <summary>
    /// Opens a document from a byte source. The cancellation token replaces the options' token, so cancelling it stops the
    /// open and any later decoding of the document's streams.
    /// </summary>
    /// <param name="source">The file.</param>
    /// <param name="ownsSource">Whether the document disposes the source, including when opening fails or is cancelled.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<PdfDocument> OpenWithAsync(PdfByteSource source, bool ownsSource, PdfOpenOptions options, CancellationToken cancellationToken) =>
        OpenWithAsync(source, ownsSource, options, cancellationToken, cancellationToken.CanBeCanceled ? cancellationToken : options.CancellationToken);

    /// <summary>Opens a byte source while keeping open cancellation separate from later document cancellation.</summary>
    /// <param name="source">The file.</param>
    /// <param name="ownsSource">Whether the document disposes the source.</param>
    /// <param name="options">The recovery and source options.</param>
    /// <param name="cancellationToken">Cancels I/O and CPU work during the open.</param>
    /// <param name="documentCancellationToken">Cancels later document operations.</param>
    /// <returns>The opened document.</returns>
    /// <exception cref="PdfException">The file is not a PDF or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The open was cancelled.</exception>
    public static async ValueTask<PdfDocument> OpenWithAsync(
        PdfByteSource source,
        bool ownsSource,
        PdfOpenOptions options,
        CancellationToken cancellationToken,
        CancellationToken documentCancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        var bound = options.CancellationToken == documentCancellationToken
            ? options
            : options with { CancellationToken = documentCancellationToken };
        PdfObjectStore objects;
        try
        {
            await PdfPrefetcher.PrefetchFileAsync(source, cancellationToken).ConfigureAwait(false);
            objects = PdfDocumentReader.OpenObjects(source, ownsSource, bound, cancellationToken);
        }
        catch
        {
            if (ownsSource)
            {
                source.Dispose();
            }

            throw;
        }

        try
        {
            if (source.NeedsPrefetch && objects.Catalog.GetDictionary(KnownName.Pages) is { } tree)
            {
                await PdfPrefetcher.PrefetchAsync(objects, tree, PdfPrefetchKind.PageTree, cancellationToken).ConfigureAwait(false);
            }

            return PdfDocumentReader.CreateScoped(objects, cancellationToken);
        }
        catch
        {
            objects.Dispose();
            throw;
        }
    }

    /// <summary>Opens a file encrypted for certificates.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="certificate">A recipient's certificate with its RSA private key.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file cannot be read, is not a PDF, or the certificate is not a recipient.</exception>
    public static PdfDocument OpenWithCertificate(string path, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        return PdfDocumentReader.OpenWith(path, new() { Certificate = certificate });
    }

    /// <summary>Opens a document held in memory that is encrypted for certificates; the bytes are kept, not copied.</summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <param name="certificate">A recipient's certificate with its RSA private key.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The bytes are not a PDF, or the certificate is not a recipient.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="certificate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfDocument OpenWithCertificate(
        byte[] bytes,
        X509Certificate2 certificate) =>
        PdfDocumentReader.OpenWith(
        bytes,
        new() { Certificate = certificate ?? throw new ArgumentNullException(nameof(certificate)) });

    /// <summary>Opens a file with options; <see cref="PdfOpenOptions.Source"/> chooses how the file is read.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="options">The options.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file cannot be read, is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The options' token was cancelled.</exception>
    public static PdfDocument OpenWith(string path, PdfOpenOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(options);
        options.CancellationToken.ThrowIfCancellationRequested();
        PdfByteSource source;
        try
        {
            source = PdfByteSources.Open(path, options.Source, options.CacheBytes);
        }
        catch (IOException ex)
        {
            throw new PdfException($"The file '{path}' could not be read.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PdfException($"The file '{path}' could not be read.", ex);
        }

        return PdfDocumentReader.OpenWith(source, true, options);
    }

    /// <summary>Opens a document held in memory with options; the bytes are kept, not copied, and must not change.</summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <param name="options">The options.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The bytes are not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The options' token was cancelled.</exception>
    public static PdfDocument OpenWith(byte[] bytes, PdfOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(options);
        return PdfDocumentReader.Create(StoreOpening.OpenWith(bytes, options));
    }

    /// <summary>
    /// Opens a document read from a seekable stream through a page cache of <see cref="PdfOpenOptions.CacheBytes"/>. The
    /// stream is not disposed with the document and must stay open and unchanged until the document is disposed.
    /// </summary>
    /// <param name="stream">The readable, seekable stream.</param>
    /// <param name="options">The options.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentException">The stream cannot read or seek.</exception>
    /// <exception cref="PdfException">The stream is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The options' token was cancelled.</exception>
    public static PdfDocument OpenWith(Stream stream, PdfOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        return PdfDocumentReader.OpenWith(new StreamPdfByteSource(stream, options.CacheBytes, false), true, options);
    }

    /// <summary>Opens a document read from a byte source.</summary>
    /// <param name="source">The file.</param>
    /// <param name="ownsSource">Whether the document disposes the source, including when opening fails.</param>
    /// <param name="options">The options.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The options' token was cancelled.</exception>
    public static PdfDocument OpenWith(PdfByteSource source, bool ownsSource, PdfOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        return PdfDocumentReader.Create(StoreOpening.OpenWith(source, ownsSource, options));
    }

    /// <summary>
    /// Opens a file. The file is mapped read-only and read as needed, never loaded whole; it falls back to reading through
    /// a page cache when it cannot be mapped. The file stays open until the document is disposed.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file cannot be read, is not a PDF, or the password is wrong.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfDocument Open(string path, string? password) => PdfDocumentReader.OpenWith(path, password is null ? PdfOpenOptions.Default : new PdfOpenOptions { Password = password });

    /// <summary>Opens a document held in memory; the bytes are kept, not copied, and must not change.</summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The bytes are not a PDF, or the password is wrong.</exception>
    public static PdfDocument Open(byte[] bytes, string? password)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return PdfDocumentReader.Create(StoreOpening.Open(bytes, password));
    }

    /// <summary>
    /// Opens a document read from a seekable stream through a bounded page cache. The stream is not disposed with the
    /// document and must stay open and unchanged until the document is disposed.
    /// </summary>
    /// <param name="stream">The readable, seekable stream.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentException">The stream cannot read or seek.</exception>
    /// <exception cref="PdfException">The stream is not a PDF, or the password is wrong.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfDocument Open(Stream stream, string? password) => PdfDocumentReader.OpenWith(stream, password is null ? PdfOpenOptions.Default : new PdfOpenOptions { Password = password });

    /// <summary>Creates a document over opened objects, disposing them if initialization fails.</summary>
    /// <param name="objects">The objects owned by the document.</param>
    /// <returns>The opened document.</returns>
    internal static PdfDocument Create(PdfObjectStore objects)
    {
        try
        {
            var document = new PdfDocument(objects);
            StoreTransactions.SetChangeCallback(objects, document.OnObjectsChanged);
            return document;
        }
        catch
        {
            objects.Dispose();
            throw;
        }
    }

    /// <summary>Opens the objects with the token in force for the synchronous core.</summary>
    /// <param name="source">The file.</param>
    /// <param name="ownsSource">Whether the objects own the source.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The objects.</returns>
    private static PdfObjectStore OpenObjects(PdfByteSource source, bool ownsSource, PdfOpenOptions options, CancellationToken cancellationToken)
    {
        using var scope = PdfCancellation.Enter(cancellationToken);
        return StoreOpening.OpenWith(source, ownsSource, options);
    }

    /// <summary>Makes the document, reading the page tree, with the token in force.</summary>
    /// <param name="objects">The objects.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The document.</returns>
    private static PdfDocument CreateScoped(PdfObjectStore objects, CancellationToken cancellationToken)
    {
        using var scope = PdfCancellation.Enter(cancellationToken);
        return PdfDocumentReader.Create(objects);
    }
}
