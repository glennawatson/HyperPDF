// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Render.Skia;
using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.HyperPdf;

/// <summary>Opens PDF documents with the managed HyperPDF engine.</summary>
[DebuggerDisplay("HyperPdfEngine: {Name}")]
public sealed class HyperPdfEngine : IDocumentEngine
{
    /// <summary>How far into the file the signature may appear; some writers prefix junk.</summary>
    private const int SignatureSearchLength = 1024;

    /// <summary>Registers the Skia drawing services used by this adapter.</summary>
    static HyperPdfEngine() => PdfDrawingServices.Register(new PdfRenderBackendRegistration().UseSkia());

    /// <inheritdoc/>
    public string Name => "HyperPDF";

    /// <summary>Gets the PDF file signature.</summary>
    private static ReadOnlySpan<byte> PdfSignature => "%PDF-"u8;

    /// <inheritdoc/>
    public bool CanOpen(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[SignatureSearchLength];
            var read = stream.ReadAtLeast(header, header.Length, false);
            return header[..read].IndexOf(PdfSignature) >= 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public IDocument Open(string path, string? password)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var fullPath = Path.GetFullPath(path);
        PdfDocument document;
        try
        {
            document = PdfDocumentReader.Open(fullPath, password);
        }
        catch (PdfException ex)
        {
            throw CreateOpenException(ex, fullPath);
        }

        return CreateOpened(document, fullPath);
    }

    /// <inheritdoc/>
    public ValueTask<IDocument> OpenAsync(string path, string? password, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        cancellationToken.ThrowIfCancellationRequested();
        return OpenCoreAsync(Path.GetFullPath(path), password, cancellationToken);
    }

    /// <summary>Opens through an asynchronous file source without retaining the open token on the document.</summary>
    /// <param name="fullPath">The absolute PDF path.</param>
    /// <param name="password">The password, if needed.</param>
    /// <param name="cancellationToken">Cancels I/O and parsing during the open.</param>
    /// <returns>The opened document.</returns>
    /// <exception cref="DocumentOpenException">The PDF cannot be opened.</exception>
    /// <exception cref="OperationCanceledException">The open was cancelled.</exception>
    private static async ValueTask<IDocument> OpenCoreAsync(string fullPath, string? password, CancellationToken cancellationToken)
    {
        PdfDocument document;
        try
        {
            document = await PdfDocumentReader.OpenWithAsync(
                fullPath,
                new PdfOpenOptions { Password = password, Source = PdfSourceKind.Stream },
                cancellationToken,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (PdfException ex)
        {
            throw CreateOpenException(ex, fullPath);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            document.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }

        return CreateOpened(document, fullPath);
    }

    /// <summary>Rejects a recovered PDF with no pages before giving its lifetime to the adapter.</summary>
    /// <param name="document">The opened managed document.</param>
    /// <param name="fullPath">The absolute PDF path.</param>
    /// <returns>The document adapter.</returns>
    /// <exception cref="DocumentOpenException">The PDF contains no readable pages.</exception>
    private static HyperPdfDocument CreateOpened(PdfDocument document, string fullPath)
    {
        if (document.PageCount == 0)
        {
            // A file rebuilt into one with no pages has nothing to show, so say so, as PDFium does.
            document.Dispose();
            throw new DocumentOpenException(DocumentOpenError.Format, $"The file '{fullPath}' is not a valid PDF document.");
        }

        try
        {
            return new(document, fullPath);
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <summary>Maps a library error to the viewer's open error.</summary>
    /// <param name="exception">The library exception.</param>
    /// <param name="path">The file path.</param>
    /// <returns>The viewer exception.</returns>
    private static DocumentOpenException CreateOpenException(PdfException exception, string path) => exception.Error switch
    {
        PdfError.Format => new(DocumentOpenError.Format, $"The file '{path}' is not a valid PDF document."),
        PdfError.Password => new(DocumentOpenError.Password, "The document is password protected."),
        PdfError.Security => new(DocumentOpenError.Security, "The document uses an unsupported security handler."),
        PdfError.Certificate => new(DocumentOpenError.Security, "The document is encrypted for a certificate and cannot be opened with a password."),
        PdfError.File or PdfError.Unknown when exception.InnerException is IOException or UnauthorizedAccessException =>
            new(DocumentOpenError.File, $"The file '{path}' could not be found or read."),
        _ => new(DocumentOpenError.Unknown, $"The document '{path}' could not be opened."),
    };
}
