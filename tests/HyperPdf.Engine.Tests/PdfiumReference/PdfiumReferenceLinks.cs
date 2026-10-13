// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>Reads PDFium's link annotations, actions and destinations for parity tests.</summary>
internal static unsafe partial class PdfiumReferenceLinks
{
    /// <summary>The page index given when a link has no destination.</summary>
    private const int NoPage = -1;

    /// <summary>Reads a page's link annotations in <c>/Annots</c> order.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>The links.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot load the page.</exception>
    internal static List<PdfiumLink> GetLinks(PdfiumReferenceDocument document, int pageIndex)
    {
        var links = new List<PdfiumLink>();
        using (PdfiumReferenceLibrary.EnterScope())
        {
            using var page = PdfiumPage.Load(document, pageIndex);
            var position = 0;
            while (NativeMethods.FPDFLink_Enumerate(page.Handle, ref position, out var link) != 0)
            {
                links.Add(ReadLink(document.Handle, link));
            }
        }

        return links;
    }

    /// <summary>Reads one link.</summary>
    /// <param name="document">The native document.</param>
    /// <param name="link">The native link.</param>
    /// <returns>The link.</returns>
    private static PdfiumLink ReadLink(PdfiumDocumentHandle document, nint link)
    {
        _ = NativeMethods.FPDFLink_GetAnnotRect(link, out var bounds);
        var action = NativeMethods.FPDFLink_GetAction(link);
        if (action == 0)
        {
            return new(bounds, PdfiumActionType.NoAction, PageOf(document, NativeMethods.FPDFLink_GetDest(document, link)), null, null);
        }

        var path = PdfiumNativeStrings.ReadBytes(action, &NativeMethods.FPDFAction_GetFilePath);
        return new(
            bounds,
            (PdfiumActionType)(int)NativeMethods.FPDFAction_GetType(action).Value,
            PageOf(document, NativeMethods.FPDFAction_GetDest(document, action)),
            ReadUri(document, action),
            path is null ? null : Encoding.UTF8.GetString(path));
    }

    /// <summary>Copies a URI action's address.</summary>
    /// <param name="document">The native document.</param>
    /// <param name="action">The native action.</param>
    /// <returns>The address, or <see langword="null"/> when the action is not a URI action.</returns>
    private static string? ReadUri(PdfiumDocumentHandle document, nint action)
    {
        var length = checked((int)NativeMethods.FPDFAction_GetURIPath(document, action, null, default).Value);
        if (length <= 1)
        {
            return null;
        }

        var buffer = new byte[length];
        fixed (byte* data = buffer)
        {
            _ = NativeMethods.FPDFAction_GetURIPath(document, action, data, new((nuint)length));
        }

        return Encoding.ASCII.GetString(buffer, 0, length - 1);
    }

    /// <summary>Gets a destination's page.</summary>
    /// <param name="document">The native document.</param>
    /// <param name="destination">The native destination, or zero.</param>
    /// <returns>The zero-based page, or -1.</returns>
    private static int PageOf(PdfiumDocumentHandle document, nint destination) =>
        destination == 0 ? NoPage : NativeMethods.FPDFDest_GetDestPageIndex(document, destination);

    /// <summary>PDFium's link annotation, action and destination functions.</summary>
    internal static partial class NativeMethods
    {
        /// <summary>The native library name.</summary>
        private const string Library = "pdfium";

        /// <summary>Gets the next link annotation of a page.</summary>
        /// <param name="page">The page.</param>
        /// <param name="start">The position to continue from; start at zero.</param>
        /// <param name="link">The link found.</param>
        /// <returns>Nonzero when a link was found.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFLink_Enumerate(nint page, ref int start, out nint link);

        /// <summary>Gets a link's <c>/A</c> action.</summary>
        /// <param name="link">The link.</param>
        /// <returns>The action, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFLink_GetAction(nint link);

        /// <summary>Gets a link's <c>/Dest</c>, or the destination of its action.</summary>
        /// <param name="document">The document.</param>
        /// <param name="link">The link.</param>
        /// <returns>The destination, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFLink_GetDest(PdfiumDocumentHandle document, nint link);

        /// <summary>Gets a link's <c>/Rect</c>.</summary>
        /// <param name="link">The link.</param>
        /// <param name="rect">The rectangle.</param>
        /// <returns>Nonzero on success.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFLink_GetAnnotRect(nint link, out PdfiumRect rect);

        /// <summary>Gets an action's type.</summary>
        /// <param name="action">The action.</param>
        /// <returns>The <c>PDFACTION_*</c> value.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDFAction_GetType(nint action);

        /// <summary>Gets a GoTo, GoToR or GoToE action's destination.</summary>
        /// <param name="document">The document.</param>
        /// <param name="action">The action.</param>
        /// <returns>The destination, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFAction_GetDest(PdfiumDocumentHandle document, nint action);

        /// <summary>Gets a destination's zero-based page.</summary>
        /// <param name="document">The document.</param>
        /// <param name="destination">The destination.</param>
        /// <returns>The page, or -1.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFDest_GetDestPageIndex(PdfiumDocumentHandle document, nint destination);

        /// <summary>Copies a URI action's address as NUL-terminated 7-bit ASCII.</summary>
        /// <param name="document">The document.</param>
        /// <param name="action">The action.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes, including the terminator.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDFAction_GetURIPath(PdfiumDocumentHandle document, nint action, void* buffer, CULong length);

        /// <summary>Copies a Launch, GoToR or GoToE action's file path as NUL-terminated UTF-8.</summary>
        /// <param name="action">The action.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes, including the terminator.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDFAction_GetFilePath(nint action, void* buffer, CULong length);
    }
}
