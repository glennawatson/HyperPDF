// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>A PDF opened from memory with test-only native PDFium, for parity checks against HyperPDF.</summary>
internal sealed class PdfiumReferenceDocument : IDisposable
{
    /// <summary>The native document.</summary>
    private readonly PdfiumDocumentHandle _handle;

    /// <summary>Initializes a new instance of the <see cref="PdfiumReferenceDocument"/> class.</summary>
    /// <param name="handle">The open native document.</param>
    /// <param name="pageCount">The page count.</param>
    private PdfiumReferenceDocument(PdfiumDocumentHandle handle, int pageCount)
    {
        _handle = handle;
        PageCount = pageCount;
    }

    /// <summary>Gets the page count PDFium reports.</summary>
    internal int PageCount { get; }

    /// <summary>Gets the native document, which the marshaller keeps open for each call that takes it.</summary>
    /// <exception cref="ObjectDisposedException">The document is closed.</exception>
    internal PdfiumDocumentHandle Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
            return _handle;
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _handle.Dispose();

    /// <summary>Opens PDF bytes that need no password.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <returns>The document.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot open the bytes.</exception>
    internal static PdfiumReferenceDocument Open(byte[] pdf) => Open(pdf, null);

    /// <summary>Opens PDF bytes with a password.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="password">The user or owner password, or <see langword="null"/>.</param>
    /// <returns>The document.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot open the bytes.</exception>
    internal static PdfiumReferenceDocument Open(byte[] pdf, string? password)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        using (PdfiumReferenceLibrary.EnterScope())
        {
            var handle = PdfiumDocumentHandle.Open(pdf, password, out var error)
                ?? throw new InvalidDataException($"PDFium could not open the PDF: {error}.");
            return new(handle, PdfiumReferenceLibrary.NativeMethods.FPDF_GetPageCount(handle));
        }
    }

    /// <summary>Gets why PDFium cannot open PDF bytes.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <returns>The reason, or <see cref="PdfiumOpenError.Success"/> when the bytes open.</returns>
    internal static PdfiumOpenError GetOpenError(byte[] pdf, string? password)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        using (PdfiumReferenceLibrary.EnterScope())
        {
            using var handle = PdfiumDocumentHandle.Open(pdf, password, out var error);
            return error;
        }
    }
}
