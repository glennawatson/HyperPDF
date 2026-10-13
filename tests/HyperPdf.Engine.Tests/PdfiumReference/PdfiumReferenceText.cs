// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>Reads PDFium's text extraction and search results for parity tests.</summary>
internal static unsafe partial class PdfiumReferenceText
{
    /// <summary>Reads a page's text, characters and glyph boxes.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>The text page.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot load the page or its text.</exception>
    internal static PdfiumTextPage GetTextPage(PdfiumReferenceDocument document, int pageIndex)
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            using var page = PdfiumPage.Load(document, pageIndex);
            var textPage = LoadTextPage(page);
            try
            {
                var count = Math.Max(NativeMethods.FPDFText_CountChars(textPage), 0);
                var characters = new PdfiumCharacter[count];
                for (var i = 0; i < count; i++)
                {
                    characters[i] = ReadCharacter(textPage, i);
                }

                return new(ReadText(textPage, count), characters);
            }
            finally
            {
                NativeMethods.FPDFText_ClosePage(textPage);
            }
        }
    }

    /// <summary>Searches a page from its first character.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="flags">The PDFium search flags.</param>
    /// <returns>The matches in page order.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot load the page or its text.</exception>
    internal static List<PdfiumMatch> Find(PdfiumReferenceDocument document, int pageIndex, string query, PdfiumSearchFlags flags)
    {
        ArgumentException.ThrowIfNullOrEmpty(query);
        var matches = new List<PdfiumMatch>();
        using (PdfiumReferenceLibrary.EnterScope())
        {
            using var page = PdfiumPage.Load(document, pageIndex);
            var textPage = LoadTextPage(page);
            try
            {
                AddMatches(textPage, query, flags, matches);
            }
            finally
            {
                NativeMethods.FPDFText_ClosePage(textPage);
            }
        }

        return matches;
    }

    /// <summary>Builds the text page of a loaded page.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The text page.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot build the text page.</exception>
    private static nint LoadTextPage(PdfiumPage page)
    {
        var textPage = NativeMethods.FPDFText_LoadPage(page.Handle);
        return textPage == 0 ? throw new InvalidDataException("PDFium could not load the page text.") : textPage;
    }

    /// <summary>Reads one character.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="index">The character index.</param>
    /// <returns>The character's value and box.</returns>
    private static PdfiumCharacter ReadCharacter(nint textPage, int index)
    {
        var unicode = NativeMethods.FPDFText_GetUnicode(textPage, index);
        PdfiumCharBox? box = NativeMethods.FPDFText_GetCharBox(textPage, index, out var left, out var right, out var bottom, out var top) != 0
            ? new(left, right, bottom, top)
            : null;
        return new(unicode, box);
    }

    /// <summary>Reads the whole page text.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="count">The character count.</param>
    /// <returns>The text.</returns>
    private static string ReadText(nint textPage, int count)
    {
        var buffer = new char[count + 1];
        fixed (char* data = buffer)
        {
            var written = NativeMethods.FPDFText_GetText(textPage, 0, count, (ushort*)data);
            return new(buffer, 0, Math.Clamp(written - 1, 0, count));
        }
    }

    /// <summary>Runs a search to its end.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="flags">The PDFium search flags.</param>
    /// <param name="matches">The matches found.</param>
    private static void AddMatches(nint textPage, string query, PdfiumSearchFlags flags, List<PdfiumMatch> matches)
    {
        fixed (char* text = query)
        {
            var search = NativeMethods.FPDFText_FindStart(textPage, text, new((uint)flags), 0);
            if (search == 0)
            {
                return;
            }

            try
            {
                while (NativeMethods.FPDFText_FindNext(search) != 0)
                {
                    matches.Add(new(NativeMethods.FPDFText_GetSchResultIndex(search), NativeMethods.FPDFText_GetSchCount(search)));
                }
            }
            finally
            {
                NativeMethods.FPDFText_FindClose(search);
            }
        }
    }

    /// <summary>PDFium's text page and search functions.</summary>
    internal static partial class NativeMethods
    {
        /// <summary>The native library name.</summary>
        private const string Library = "pdfium";

        /// <summary>Builds the text page of a loaded page.</summary>
        /// <param name="page">The page.</param>
        /// <returns>The text page, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFText_LoadPage(nint page);

        /// <summary>Closes a text page.</summary>
        /// <param name="textPage">The text page.</param>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDFText_ClosePage(nint textPage);

        /// <summary>Counts the characters, including generated spaces and line breaks.</summary>
        /// <param name="textPage">The text page.</param>
        /// <returns>The count, or -1.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFText_CountChars(nint textPage);

        /// <summary>Gets a character's Unicode value.</summary>
        /// <param name="textPage">The text page.</param>
        /// <param name="index">The character.</param>
        /// <returns>The UTF-16 code unit PDFium stores for it.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial uint FPDFText_GetUnicode(nint textPage, int index);

        /// <summary>Gets a character's glyph box in page space.</summary>
        /// <param name="textPage">The text page.</param>
        /// <param name="index">The character.</param>
        /// <param name="left">The left edge.</param>
        /// <param name="right">The right edge.</param>
        /// <param name="bottom">The bottom edge.</param>
        /// <param name="top">The top edge.</param>
        /// <returns>Nonzero when the character has a box.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFText_GetCharBox(nint textPage, int index, out double left, out double right, out double bottom, out double top);

        /// <summary>Copies characters as UTF-16LE with a terminator.</summary>
        /// <param name="textPage">The text page.</param>
        /// <param name="start">The first character.</param>
        /// <param name="count">The characters to copy.</param>
        /// <param name="result">The buffer of <paramref name="count"/> plus one code units.</param>
        /// <returns>The code units written, including the terminator.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFText_GetText(nint textPage, int start, int count, ushort* result);

        /// <summary>Starts a search.</summary>
        /// <param name="textPage">The text page.</param>
        /// <param name="query">The NUL-terminated UTF-16LE query.</param>
        /// <param name="flags">The <c>FPDF_MATCH*</c> flags.</param>
        /// <param name="start">The character to start at.</param>
        /// <returns>The search handle, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFText_FindStart(nint textPage, char* query, CULong flags, int start);

        /// <summary>Moves to the next match.</summary>
        /// <param name="search">The search handle.</param>
        /// <returns>Nonzero when a match was found.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFText_FindNext(nint search);

        /// <summary>Gets the first character of the current match.</summary>
        /// <param name="search">The search handle.</param>
        /// <returns>The character index.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFText_GetSchResultIndex(nint search);

        /// <summary>Gets the character count of the current match.</summary>
        /// <param name="search">The search handle.</param>
        /// <returns>The character count.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFText_GetSchCount(nint search);

        /// <summary>Closes a search.</summary>
        /// <param name="search">The search handle.</param>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDFText_FindClose(nint search);
    }
}
