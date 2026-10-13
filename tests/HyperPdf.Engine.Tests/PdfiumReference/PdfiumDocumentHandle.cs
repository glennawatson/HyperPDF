// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>Owns an <c>FPDF_DOCUMENT</c> and the native copy of the bytes PDFium reads until it is closed.</summary>
internal sealed unsafe class PdfiumDocumentHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>The native copy of the PDF bytes.</summary>
    private void* _bytes;

    /// <summary>Initializes a new instance of the <see cref="PdfiumDocumentHandle"/> class.</summary>
    private PdfiumDocumentHandle()
        : base(true)
    {
    }

    /// <summary>Opens PDF bytes with PDFium. Call while holding the PDFium lock.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <param name="error">The reason the document did not open, or <see cref="PdfiumOpenError.Success"/>.</param>
    /// <returns>The open handle, or <see langword="null"/> when PDFium cannot open the bytes.</returns>
    internal static PdfiumDocumentHandle? Open(byte[] pdf, string? password, out PdfiumOpenError error)
    {
        var bytes = NativeMemory.Alloc((nuint)Math.Max(pdf.Length, 1));
        pdf.AsSpan().CopyTo(new(bytes, pdf.Length));
        var document = LoadDocument(bytes, pdf.Length, password);
        if (document == 0)
        {
            error = (PdfiumOpenError)(int)PdfiumReferenceLibrary.NativeMethods.FPDF_GetLastError().Value;
            NativeMemory.Free(bytes);
            return null;
        }

        var handle = new PdfiumDocumentHandle { _bytes = bytes };
        handle.SetHandle(document);
        error = PdfiumOpenError.Success;
        return handle;
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            PdfiumReferenceLibrary.NativeMethods.FPDF_CloseDocument(handle);
        }

        NativeMemory.Free(_bytes);
        _bytes = null;
        return true;
    }

    /// <summary>Calls PDFium's memory loader with a NUL-terminated UTF-8 password.</summary>
    /// <param name="bytes">The native PDF bytes.</param>
    /// <param name="length">The byte count.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <returns>The document, or zero.</returns>
    private static nint LoadDocument(void* bytes, int length, string? password)
    {
        if (password is null)
        {
            return PdfiumReferenceLibrary.NativeMethods.FPDF_LoadMemDocument64(bytes, (nuint)length, null);
        }

        var terminated = new byte[Encoding.UTF8.GetByteCount(password) + 1];
        _ = Encoding.UTF8.GetBytes(password, terminated);
        fixed (byte* text = terminated)
        {
            return PdfiumReferenceLibrary.NativeMethods.FPDF_LoadMemDocument64(bytes, (nuint)length, text);
        }
    }
}
