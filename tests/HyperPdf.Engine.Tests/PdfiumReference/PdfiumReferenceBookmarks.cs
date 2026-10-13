// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>Reads PDFium's outline for parity tests.</summary>
internal static unsafe partial class PdfiumReferenceBookmarks
{
    /// <summary>PDFium's action type for a URI action.</summary>
    private const int UriActionType = 3;

    /// <summary>The most entries read.</summary>
    private const int MaxEntries = 100_000;

    /// <summary>The deepest level read.</summary>
    private const int MaxDepth = 32;

    /// <summary>Reads the outline PDFium finds, level by level.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The top level entries.</returns>
    internal static List<PdfiumBookmark> GetBookmarks(PdfiumReferenceDocument document)
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            var budget = MaxEntries;
            return ReadLevel(document.Handle, 0, 0, ref budget);
        }
    }

    /// <summary>Reads the children of an entry.</summary>
    /// <param name="document">The document.</param>
    /// <param name="parent">The entry, or zero for the top level.</param>
    /// <param name="depth">The depth of the level.</param>
    /// <param name="budget">The entries still allowed.</param>
    /// <returns>The entries.</returns>
    private static List<PdfiumBookmark> ReadLevel(PdfiumDocumentHandle document, nint parent, int depth, ref int budget)
    {
        var entries = new List<PdfiumBookmark>();
        var bookmark = NativeMethods.FPDFBookmark_GetFirstChild(document, parent);
        for (; bookmark != 0 && budget > 0 && depth < MaxDepth; bookmark = NativeMethods.FPDFBookmark_GetNextSibling(document, bookmark))
        {
            budget--;
            var title = PdfiumNativeStrings.ReadUtf16(bookmark, &NativeMethods.FPDFBookmark_GetTitle) ?? string.Empty;
            var children = ReadLevel(document, bookmark, depth + 1, ref budget);
            var target = ReadTarget(document, bookmark);
            entries.Add(new(title, NativeMethods.FPDFBookmark_GetCount(bookmark), target.Page, target.Uri, children));
        }

        return entries;
    }

    /// <summary>Reads where an entry leads.</summary>
    /// <param name="document">The document.</param>
    /// <param name="bookmark">The entry.</param>
    /// <returns>The page its destination reaches (or -1) and the address of its URI action (or null).</returns>
    private static BookmarkTarget ReadTarget(PdfiumDocumentHandle document, nint bookmark)
    {
        var destination = NativeMethods.FPDFBookmark_GetDest(document, bookmark);
        var page = destination == 0 ? -1 : NativeMethods.FPDFDest_GetDestPageIndex(document, destination);
        var action = NativeMethods.FPDFBookmark_GetAction(bookmark);
        if (action == 0 || (int)NativeMethods.FPDFAction_GetType(action).Value != UriActionType)
        {
            return new(page, null);
        }

        var length = checked((int)NativeMethods.FPDFAction_GetURIPath(document, action, null, default).Value);
        if (length < 1)
        {
            return new(page, null);
        }

        var buffer = new byte[length];
        fixed (byte* data = buffer)
        {
            _ = NativeMethods.FPDFAction_GetURIPath(document, action, data, new((nuint)length));
        }

        return new(page, Encoding.ASCII.GetString(buffer, 0, length - 1));
    }

    /// <summary>Where an entry leads, as PDFium reads it.</summary>
    /// <param name="Page">The page the entry's destination reaches, or -1.</param>
    /// <param name="Uri">The address of the entry's URI action, or <see langword="null"/>.</param>
    private readonly record struct BookmarkTarget(int Page, string? Uri);

    /// <summary>PDFium's outline functions.</summary>
    internal static partial class NativeMethods
    {
        /// <summary>The native library name.</summary>
        private const string Library = "pdfium";

        /// <summary>Gets the first child of an entry, or the first top level entry when the parent is zero.</summary>
        /// <param name="document">The document.</param>
        /// <param name="bookmark">The parent, or zero.</param>
        /// <returns>The entry, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFBookmark_GetFirstChild(PdfiumDocumentHandle document, nint bookmark);

        /// <summary>Gets the next entry at the same level.</summary>
        /// <param name="document">The document.</param>
        /// <param name="bookmark">The entry.</param>
        /// <returns>The next entry, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFBookmark_GetNextSibling(PdfiumDocumentHandle document, nint bookmark);

        /// <summary>Copies an entry's title as UTF-16LE with a terminator.</summary>
        /// <param name="bookmark">The entry.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDFBookmark_GetTitle(nint bookmark, void* buffer, CULong length);

        /// <summary>Gets an entry's <c>/Count</c>.</summary>
        /// <param name="bookmark">The entry.</param>
        /// <returns>The count.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFBookmark_GetCount(nint bookmark);

        /// <summary>Gets an entry's destination, following a go-to action.</summary>
        /// <param name="document">The document.</param>
        /// <param name="bookmark">The entry.</param>
        /// <returns>The destination, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFBookmark_GetDest(PdfiumDocumentHandle document, nint bookmark);

        /// <summary>Gets the zero based page a destination reaches.</summary>
        /// <param name="document">The document.</param>
        /// <param name="destination">The destination.</param>
        /// <returns>The page index, or -1.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFDest_GetDestPageIndex(PdfiumDocumentHandle document, nint destination);

        /// <summary>Gets an entry's action.</summary>
        /// <param name="bookmark">The entry.</param>
        /// <returns>The action, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFBookmark_GetAction(nint bookmark);

        /// <summary>Gets an action's type.</summary>
        /// <param name="action">The action.</param>
        /// <returns>The type: 1 go to, 2 remote go to, 3 URI, 4 launch.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDFAction_GetType(nint action);

        /// <summary>Copies a URI action's address as ASCII with a terminator.</summary>
        /// <param name="document">The document.</param>
        /// <param name="action">The action.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDFAction_GetURIPath(PdfiumDocumentHandle document, nint action, void* buffer, CULong length);
    }
}
