// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>Reads what PDFium reports about a document's encryption, for parity tests.</summary>
internal static partial class PdfiumReferenceSecurity
{
    /// <summary>Gets the permission word PDFium reports for the password the document was opened with.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The permission bits: <c>/P</c> as PDFium returns it, or all bits set when the document is not encrypted.</returns>
    internal static uint GetPermissions(PdfiumReferenceDocument document)
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            return unchecked((uint)NativeMethods.FPDF_GetDocPermissions(document.Handle).Value);
        }
    }

    /// <summary>Gets the standard security handler revision PDFium reports.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The revision, or -1 when the document is not encrypted.</returns>
    internal static int GetRevision(PdfiumReferenceDocument document)
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            return NativeMethods.FPDF_GetSecurityHandlerRevision(document.Handle);
        }
    }

    /// <summary>PDFium's security functions.</summary>
    internal static partial class NativeMethods
    {
        /// <summary>The native library name, resolved from the bblanchon.PDFium runtime packages.</summary>
        private const string Library = "pdfium";

        /// <summary>Gets the permissions of the document.</summary>
        /// <param name="document">The document.</param>
        /// <returns>The permission bits.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDF_GetDocPermissions(PdfiumDocumentHandle document);

        /// <summary>Gets the revision of the standard security handler.</summary>
        /// <param name="document">The document.</param>
        /// <returns>The revision, or -1.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDF_GetSecurityHandlerRevision(PdfiumDocumentHandle document);
    }
}
