// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine.Tests;

/// <summary>The same PDF bytes opened with HyperPDF and with the test-only PDFium reference; the temporary file is deleted on dispose.</summary>
internal sealed class EnginePair : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="EnginePair"/> class for bytes that need no password.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    internal EnginePair(byte[] bytes)
        : this(bytes, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EnginePair"/> class.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <param name="password">The password both engines open with, or <see langword="null"/>.</param>
    internal EnginePair(byte[] bytes, string? password)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        Bytes = bytes;
        FilePath = Path.Combine(Path.GetTempPath(), $"hyperpdf-pair-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(FilePath, bytes);
        PdfiumReferenceDocument? pdfium = null;
        try
        {
            pdfium = PdfiumReferenceDocument.Open(bytes, password);
            HyperPdf = (HyperPdfDocument)new HyperPdfEngine().Open(FilePath, password);
        }
        catch
        {
            pdfium?.Dispose();
            File.Delete(FilePath);
            throw;
        }

        Pdfium = pdfium;
    }

    /// <summary>Gets the PDF bytes both engines opened.</summary>
    internal byte[] Bytes { get; }

    /// <summary>Gets the temporary file HyperPDF opened.</summary>
    internal string FilePath { get; }

    /// <summary>Gets the document opened with the PDFium reference.</summary>
    internal PdfiumReferenceDocument Pdfium { get; }

    /// <summary>Gets the document opened with the HyperPDF engine adapter.</summary>
    internal HyperPdfDocument HyperPdf { get; }

    /// <summary>Gets the managed library document behind <see cref="HyperPdf"/>.</summary>
    internal PdfDocument Library => HyperPdf.Document;

    /// <inheritdoc/>
    public void Dispose()
    {
        HyperPdf.Dispose();
        Pdfium.Dispose();
        File.Delete(FilePath);
    }
}
