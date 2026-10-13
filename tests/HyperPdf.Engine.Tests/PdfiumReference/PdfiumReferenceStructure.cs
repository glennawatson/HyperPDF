// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>Reads PDFium's tagged-PDF view of a document for parity tests.</summary>
internal static unsafe partial class PdfiumReferenceStructure
{
    /// <summary>The deepest element read, so a cyclic or hostile tree cannot overflow the stack.</summary>
    private const int MaxDepth = 64;

    /// <summary>Gets whether <c>FPDFCatalog_IsTagged</c> reports <c>/MarkInfo /Marked</c>.</summary>
    /// <param name="document">The document.</param>
    /// <returns>True when PDFium treats the document as tagged.</returns>
    internal static bool IsTagged(PdfiumReferenceDocument document)
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            return NativeMethods.FPDFCatalog_IsTagged(document.Handle) != 0;
        }
    }

    /// <summary>Reads the structure tree PDFium builds for a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>The top-level elements; empty when the page has no structure tree.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot load the page.</exception>
    internal static PdfiumStructElement[] GetPageTree(PdfiumReferenceDocument document, int pageIndex)
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            using var page = PdfiumPage.Load(document, pageIndex);
            var tree = NativeMethods.FPDF_StructTree_GetForPage(page.Handle);
            if (tree == 0)
            {
                return [];
            }

            try
            {
                var count = Math.Max(NativeMethods.FPDF_StructTree_CountChildren(tree), 0);
                var roots = new List<PdfiumStructElement>(count);
                for (var i = 0; i < count; i++)
                {
                    var element = NativeMethods.FPDF_StructTree_GetChildAtIndex(tree, i);
                    if (element != 0)
                    {
                        roots.Add(ReadElement(element, 0));
                    }
                }

                return [.. roots];
            }
            finally
            {
                NativeMethods.FPDF_StructTree_Close(tree);
            }
        }
    }

    /// <summary>Reads an element and its child elements.</summary>
    /// <param name="element">The native element.</param>
    /// <param name="depth">The element's depth below the tree's top level.</param>
    /// <returns>The element.</returns>
    private static PdfiumStructElement ReadElement(nint element, int depth) => new(
        PdfiumNativeStrings.ReadUtf16(element, &NativeMethods.FPDF_StructElement_GetType) ?? string.Empty,
        PdfiumNativeStrings.ReadUtf16(element, &NativeMethods.FPDF_StructElement_GetAltText),
        PdfiumNativeStrings.ReadUtf16(element, &NativeMethods.FPDF_StructElement_GetActualText),
        PdfiumNativeStrings.ReadUtf16(element, &NativeMethods.FPDF_StructElement_GetTitle),
        PdfiumNativeStrings.ReadUtf16(element, &NativeMethods.FPDF_StructElement_GetLang),
        NativeMethods.FPDF_StructElement_GetMarkedContentID(element),
        depth < MaxDepth ? ReadChildren(element, depth + 1) : []);

    /// <summary>Reads an element's child elements, skipping marked content children.</summary>
    /// <param name="element">The native element.</param>
    /// <param name="depth">The children's depth.</param>
    /// <returns>The children.</returns>
    private static PdfiumStructElement[] ReadChildren(nint element, int depth)
    {
        var count = Math.Max(NativeMethods.FPDF_StructElement_CountChildren(element), 0);
        var children = new List<PdfiumStructElement>(count);
        for (var i = 0; i < count; i++)
        {
            var child = NativeMethods.FPDF_StructElement_GetChildAtIndex(element, i);
            if (child != 0)
            {
                children.Add(ReadElement(child, depth));
            }
        }

        return [.. children];
    }

    /// <summary>PDFium's catalog and structure tree functions. Every string is UTF-16LE with a terminator, sized in bytes.</summary>
    internal static partial class NativeMethods
    {
        /// <summary>The native library name.</summary>
        private const string Library = "pdfium";

        /// <summary>Gets whether <c>/MarkInfo /Marked</c> is set.</summary>
        /// <param name="document">The document.</param>
        /// <returns>Nonzero when the document says it is tagged.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFCatalog_IsTagged(PdfiumDocumentHandle document);

        /// <summary>Gets the structure tree of a page.</summary>
        /// <param name="page">The page.</param>
        /// <returns>The tree, closed with <see cref="FPDF_StructTree_Close"/>; zero when the page has none.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDF_StructTree_GetForPage(nint page);

        /// <summary>Closes a structure tree.</summary>
        /// <param name="tree">The tree.</param>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDF_StructTree_Close(nint tree);

        /// <summary>Counts the tree's top-level elements.</summary>
        /// <param name="tree">The tree.</param>
        /// <returns>The count.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDF_StructTree_CountChildren(nint tree);

        /// <summary>Gets a top-level element.</summary>
        /// <param name="tree">The tree.</param>
        /// <param name="index">The child.</param>
        /// <returns>The element, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDF_StructTree_GetChildAtIndex(nint tree, int index);

        /// <summary>Copies an element's type after role mapping.</summary>
        /// <param name="element">The element.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDF_StructElement_GetType(nint element, void* buffer, CULong length);

        /// <summary>Copies an element's <c>/Alt</c>.</summary>
        /// <param name="element">The element.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes, or zero when it has none.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDF_StructElement_GetAltText(nint element, void* buffer, CULong length);

        /// <summary>Copies an element's <c>/ActualText</c>.</summary>
        /// <param name="element">The element.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes, or zero when it has none.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDF_StructElement_GetActualText(nint element, void* buffer, CULong length);

        /// <summary>Copies an element's <c>/T</c> title.</summary>
        /// <param name="element">The element.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes, or zero when it has none.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDF_StructElement_GetTitle(nint element, void* buffer, CULong length);

        /// <summary>Copies an element's own <c>/Lang</c>, not inherited.</summary>
        /// <param name="element">The element.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes, or zero when it has none.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDF_StructElement_GetLang(nint element, void* buffer, CULong length);

        /// <summary>Gets the marked content ID of an element whose <c>/K</c> is an MCID.</summary>
        /// <param name="element">The element.</param>
        /// <returns>The MCID, or -1.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDF_StructElement_GetMarkedContentID(nint element);

        /// <summary>Counts an element's children, elements and marked content alike.</summary>
        /// <param name="element">The element.</param>
        /// <returns>The count.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDF_StructElement_CountChildren(nint element);

        /// <summary>Gets a child element.</summary>
        /// <param name="element">The element.</param>
        /// <param name="index">The child.</param>
        /// <returns>The child element, or zero when the child is marked content.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDF_StructElement_GetChildAtIndex(nint element, int index);
    }
}
