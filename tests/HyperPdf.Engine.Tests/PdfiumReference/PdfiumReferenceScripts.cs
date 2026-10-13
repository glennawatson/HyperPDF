// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>Reads PDFium's document-level JavaScript for parity tests. Nothing runs.</summary>
internal static unsafe partial class PdfiumReferenceScripts
{
    /// <summary>Counts the <c>/JavaScript</c> name tree entries, scripts or not.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The count, or -1 when PDFium cannot count them.</returns>
    internal static int GetDocumentScriptCount(PdfiumReferenceDocument document)
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            return NativeMethods.FPDFDoc_GetJavaScriptActionCount(document.Handle);
        }
    }

    /// <summary>Reads the entries PDFium accepts as JavaScript actions, in name tree order.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The scripts.</returns>
    internal static List<PdfiumNamedScript> GetDocumentScripts(PdfiumReferenceDocument document)
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            var handle = document.Handle;
            var count = Math.Max(NativeMethods.FPDFDoc_GetJavaScriptActionCount(handle), 0);
            var scripts = new List<PdfiumNamedScript>(count);
            for (var i = 0; i < count; i++)
            {
                var script = NativeMethods.FPDFDoc_GetJavaScriptAction(handle, i);
                if (script == 0)
                {
                    continue;
                }

                try
                {
                    scripts.Add(new(
                        i,
                        PdfiumNativeStrings.ReadUtf16(script, &NativeMethods.FPDFJavaScriptAction_GetName) ?? string.Empty,
                        PdfiumNativeStrings.ReadUtf16(script, &NativeMethods.FPDFJavaScriptAction_GetScript) ?? string.Empty));
                }
                finally
                {
                    NativeMethods.FPDFDoc_CloseJavaScriptAction(script);
                }
            }

            return scripts;
        }
    }

    /// <summary>PDFium's document JavaScript functions.</summary>
    internal static partial class NativeMethods
    {
        /// <summary>The native library name.</summary>
        private const string Library = "pdfium";

        /// <summary>Counts the <c>/JavaScript</c> name tree entries.</summary>
        /// <param name="document">The document.</param>
        /// <returns>The count, or -1.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFDoc_GetJavaScriptActionCount(PdfiumDocumentHandle document);

        /// <summary>Gets a document JavaScript action, closed with <see cref="FPDFDoc_CloseJavaScriptAction"/>.</summary>
        /// <param name="document">The document.</param>
        /// <param name="index">The entry.</param>
        /// <returns>The action, or zero when the entry is not a JavaScript action.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFDoc_GetJavaScriptAction(PdfiumDocumentHandle document, int index);

        /// <summary>Closes a document JavaScript action.</summary>
        /// <param name="script">The action.</param>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDFDoc_CloseJavaScriptAction(nint script);

        /// <summary>Copies a script's name tree key as UTF-16LE with a terminator.</summary>
        /// <param name="script">The action.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDFJavaScriptAction_GetName(nint script, void* buffer, CULong length);

        /// <summary>Copies a script's <c>/JS</c> text as UTF-16LE with a terminator.</summary>
        /// <param name="script">The action.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDFJavaScriptAction_GetScript(nint script, void* buffer, CULong length);
    }
}
