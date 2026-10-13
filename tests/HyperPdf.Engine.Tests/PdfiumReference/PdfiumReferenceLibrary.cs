// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>Initializes test-only native PDFium once and serializes every call into it.</summary>
internal static unsafe partial class PdfiumReferenceLibrary
{
    /// <summary>Serializes calls because PDFium's document, text and rendering APIs are not thread safe.</summary>
    private static readonly Lock Sync = new();

    /// <summary>Initializes PDFium for the test process.</summary>
    static PdfiumReferenceLibrary() => NativeMethods.FPDF_InitLibrary();

    /// <summary>Holds the PDFium lock until the returned scope is disposed. Hold it for every native call.</summary>
    /// <returns>The lock scope.</returns>
    internal static Lock.Scope EnterScope() => Sync.EnterScope();

    /// <summary>PDFium's library, document and page functions.</summary>
    internal static partial class NativeMethods
    {
        /// <summary>The native library name, resolved from the bblanchon.PDFium runtime packages.</summary>
        private const string Library = "pdfium";

        /// <summary>Initializes PDFium.</summary>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDF_InitLibrary();

        /// <summary>Opens PDF bytes that must stay valid until the document is closed.</summary>
        /// <param name="data">The bytes.</param>
        /// <param name="size">The byte count.</param>
        /// <param name="password">The NUL-terminated UTF-8 password, or null.</param>
        /// <returns>The document, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDF_LoadMemDocument64(void* data, nuint size, byte* password);

        /// <summary>Gets the reason the last open failed.</summary>
        /// <returns>The <c>FPDF_ERR_*</c> code.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDF_GetLastError();

        /// <summary>Closes a document.</summary>
        /// <param name="document">The raw document, released by its owning handle.</param>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDF_CloseDocument(nint document);

        /// <summary>Gets the page count.</summary>
        /// <param name="document">The document.</param>
        /// <returns>The page count.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDF_GetPageCount(PdfiumDocumentHandle document);

        /// <summary>Loads a page.</summary>
        /// <param name="document">The document.</param>
        /// <param name="pageIndex">The zero-based page.</param>
        /// <returns>The page, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDF_LoadPage(PdfiumDocumentHandle document, int pageIndex);

        /// <summary>Closes a page.</summary>
        /// <param name="page">The page.</param>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDF_ClosePage(nint page);
    }
}
