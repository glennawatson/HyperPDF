// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Render.Skia;
using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.HyperPdf;

/// <summary>Opens PDF documents with the managed HyperPDF engine.</summary>
[DebuggerDisplay("HyperPdfEngine: {Name}")]
public sealed class HyperPdfEngine : IDocumentEngine, ICertificateDocumentEngine
{
    /// <summary>How far into the file the signature may appear; some writers prefix junk.</summary>
    private const int SignatureSearchLength = 1024;

    /// <summary>The font loader used for documents opened by this engine.</summary>
    private readonly Func<PdfDictionary, PdfFont?>? _fontFactory;

    /// <summary>The drawing services shared by documents opened through this engine.</summary>
    private readonly PdfDrawingServices _drawingServices;

    /// <summary>The clock used for annotation dates in documents opened by this engine.</summary>
    private readonly TimeProvider _annotationClock;

    /// <summary>The annotation font cache shared by documents opened through this engine, built on first use.</summary>
    private readonly Lazy<TextBoxFontState> _fontState = new(static () => new());

    /// <summary>Initializes a new instance of the <see cref="HyperPdfEngine"/> class.</summary>
    public HyperPdfEngine()
        : this(PdfFontLoader.Load, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HyperPdfEngine"/> class.</summary>
    /// <param name="fontFactory">The loader for each document's fonts, or null to disable font loading.</param>
    public HyperPdfEngine(Func<PdfDictionary, PdfFont?>? fontFactory)
        : this(fontFactory, TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HyperPdfEngine"/> class with one bounded scratch cache shared by its documents.</summary>
    /// <param name="fontFactory">The loader for each document's fonts, or null to disable font loading.</param>
    /// <param name="annotationClock">The clock used for annotation modification dates.</param>
    public HyperPdfEngine(Func<PdfDictionary, PdfFont?>? fontFactory, TimeProvider annotationClock)
        : this(fontFactory, annotationClock, new(new PdfRenderBackendRegistration().UseSkia()) { SharedScratchPools = new() })
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HyperPdfEngine"/> class with drawing services.</summary>
    /// <param name="fontFactory">The loader for each document's fonts, or null to disable font loading.</param>
    /// <param name="annotationClock">The clock used for annotation modification dates.</param>
    /// <param name="drawingServices">The drawing services used for documents opened by this engine.</param>
    public HyperPdfEngine(Func<PdfDictionary, PdfFont?>? fontFactory, TimeProvider annotationClock, PdfDrawingServices drawingServices)
    {
        ArgumentNullException.ThrowIfNull(annotationClock);
        ArgumentNullException.ThrowIfNull(drawingServices);
        _fontFactory = fontFactory;
        _annotationClock = annotationClock;
        _drawingServices = drawingServices;
    }

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
        return OpenCore(this, Path.GetFullPath(path), password, null);
    }

    /// <inheritdoc/>
    public IDocument OpenWithCertificate(string path, string certificatePath, string certificatePassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
        ArgumentNullException.ThrowIfNull(certificatePassword);
        using var certificate = LoadCertificate(ReadCertificate(certificatePath), certificatePassword);
        return OpenCore(this, Path.GetFullPath(path), null, certificate);
    }

    /// <inheritdoc/>
    public ValueTask<IDocument> OpenAsync(string path, string? password, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        cancellationToken.ThrowIfCancellationRequested();
        return OpenCoreAsync(this, Path.GetFullPath(path), password, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<IDocument> OpenWithCertificateAsync(string path, string certificatePath, string certificatePassword, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
        ArgumentNullException.ThrowIfNull(certificatePassword);
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(certificatePath, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw CertificateFileError(certificatePath);
        }
        catch (UnauthorizedAccessException)
        {
            throw CertificateFileError(certificatePath);
        }
        catch (ArgumentException)
        {
            throw CertificateFileError(certificatePath);
        }
        catch (NotSupportedException)
        {
            throw CertificateFileError(certificatePath);
        }

        using var certificate = LoadCertificate(bytes, certificatePassword);
        return await OpenCoreAsync(this, Path.GetFullPath(path), null, certificate, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a file with either a password or a recipient certificate.</summary>
    /// <param name="self">The engine's font and drawing services.</param>
    /// <param name="fullPath">The absolute PDF path.</param>
    /// <param name="password">The document password, or null.</param>
    /// <param name="certificate">The recipient certificate, or null.</param>
    /// <returns>The opened document.</returns>
    private static HyperPdfDocument OpenCore(HyperPdfEngine self, string fullPath, string? password, X509Certificate2? certificate)
    {
        PdfDocument document;
        try
        {
            var options = new PdfOpenOptions { Password = password, Certificate = certificate, FontFactory = self._fontFactory, DrawingServices = self._drawingServices };
            document = PdfDocumentReader.OpenWith(fullPath, options);
        }
        catch (PdfException ex)
        {
            throw CreateOpenException(ex, fullPath);
        }

        return CreateOpened(document, fullPath, self._annotationClock, self._fontState);
    }

    /// <summary>Opens through an asynchronous file source without retaining the open token on the document.</summary>
    /// <param name="self">The engine's font and drawing services.</param>
    /// <param name="fullPath">The absolute PDF path.</param>
    /// <param name="password">The password, if needed.</param>
    /// <param name="certificate">The recipient certificate, if needed.</param>
    /// <param name="cancellationToken">Cancels I/O and parsing during the open.</param>
    /// <returns>The opened document.</returns>
    /// <exception cref="DocumentOpenException">The PDF cannot be opened.</exception>
    /// <exception cref="OperationCanceledException">The open was cancelled.</exception>
    private static async ValueTask<IDocument> OpenCoreAsync(
        HyperPdfEngine self,
        string fullPath,
        string? password,
        X509Certificate2? certificate,
        CancellationToken cancellationToken)
    {
        PdfDocument document;
        try
        {
            document = await PdfDocumentReader.OpenWithAsync(
                fullPath,
                new PdfOpenOptions { Password = password, Certificate = certificate, Source = PdfSourceKind.Stream, FontFactory = self._fontFactory, DrawingServices = self._drawingServices },
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

        return CreateOpened(document, fullPath, self._annotationClock, self._fontState);
    }

    /// <summary>Rejects a recovered PDF with no pages before giving its lifetime to the adapter.</summary>
    /// <param name="document">The opened managed document.</param>
    /// <param name="fullPath">The absolute PDF path.</param>
    /// <param name="annotationClock">The clock used for this document's annotation dates.</param>
    /// <param name="fontState">The annotation font cache chosen by this engine.</param>
    /// <returns>The document adapter.</returns>
    /// <exception cref="DocumentOpenException">The PDF contains no readable pages.</exception>
    private static HyperPdfDocument CreateOpened(PdfDocument document, string fullPath, TimeProvider annotationClock, Lazy<TextBoxFontState> fontState)
    {
        if (document.PageCount == 0)
        {
            // A file rebuilt into one with no pages has nothing to show, so say so, as PDFium does.
            document.Dispose();
            throw new DocumentOpenException(DocumentOpenError.Format, $"The file '{fullPath}' is not a valid PDF document.");
        }

        try
        {
            return new(document, fullPath, annotationClock, fontState);
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
        PdfError.Certificate => new(DocumentOpenError.Certificate, exception.Message),
        PdfError.File or PdfError.Unknown when exception.InnerException is IOException or UnauthorizedAccessException =>
            new(DocumentOpenError.File, $"The file '{path}' could not be found or read."),
        _ => new(DocumentOpenError.Unknown, $"The document '{path}' could not be opened."),
    };

    /// <summary>Reads a PKCS #12 file for a synchronous open.</summary>
    /// <param name="path">The certificate path.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] ReadCertificate(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            throw CertificateFileError(path);
        }
        catch (UnauthorizedAccessException)
        {
            throw CertificateFileError(path);
        }
        catch (ArgumentException)
        {
            throw CertificateFileError(path);
        }
        catch (NotSupportedException)
        {
            throw CertificateFileError(path);
        }
    }

    /// <summary>Imports a recipient's private key for the duration of an open.</summary>
    /// <param name="bytes">The PKCS #12 bytes, cleared after import.</param>
    /// <param name="password">The file password.</param>
    /// <returns>The certificate with its private key.</returns>
    /// <exception cref="DocumentOpenException">The file cannot be unlocked or has no private key.</exception>
    private static X509Certificate2 LoadCertificate(byte[] bytes, string password)
    {
        try
        {
            // macOS uses a temporary keychain for PKCS #12 private keys and rejects EphemeralKeySet.
            var keyStorage = OperatingSystem.IsMacOS() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet;
            var certificate = X509CertificateLoader.LoadPkcs12(bytes, password, keyStorage);
            if (certificate.HasPrivateKey)
            {
                return certificate;
            }

            certificate.Dispose();
            throw new DocumentOpenException(DocumentOpenError.Certificate, "This certificate has no private key.");
        }
        catch (CryptographicException)
        {
            throw new DocumentOpenException(DocumentOpenError.Certificate, "The certificate password is wrong, or the file is not a PKCS #12 certificate.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Creates the viewer error for an unreadable certificate file.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The error.</returns>
    private static DocumentOpenException CertificateFileError(string path) =>
        new(DocumentOpenError.Certificate, $"The certificate file '{path}' could not be read.");
}
