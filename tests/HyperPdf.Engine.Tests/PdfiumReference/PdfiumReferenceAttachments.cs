// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>Reads PDFium's document attachments for parity tests.</summary>
internal static unsafe partial class PdfiumReferenceAttachments
{
    /// <summary>Reads every <c>/EmbeddedFiles</c> entry PDFium finds, in name tree order.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The attachments.</returns>
    internal static List<PdfiumAttachment> GetAttachments(PdfiumReferenceDocument document)
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            var handle = document.Handle;
            var count = Math.Max(NativeMethods.FPDFDoc_GetAttachmentCount(handle), 0);
            var attachments = new List<PdfiumAttachment>(count);
            for (var i = 0; i < count; i++)
            {
                var attachment = NativeMethods.FPDFDoc_GetAttachment(handle, i);
                if (attachment != 0)
                {
                    attachments.Add(new(i, PdfiumNativeStrings.ReadUtf16(attachment, &NativeMethods.FPDFAttachment_GetName) ?? string.Empty, ReadFile(attachment)));
                }
            }

            return attachments;
        }
    }

    /// <summary>Copies an attachment's decoded bytes.</summary>
    /// <param name="attachment">The native attachment.</param>
    /// <returns>The bytes, or <see langword="null"/> when PDFium finds no file.</returns>
    private static byte[]? ReadFile(nint attachment)
    {
        if (NativeMethods.FPDFAttachment_GetFile(attachment, null, default, out var needed) == 0)
        {
            return null;
        }

        var data = new byte[checked((int)needed.Value)];
        fixed (byte* buffer = data)
        {
            _ = NativeMethods.FPDFAttachment_GetFile(attachment, buffer, new((nuint)data.Length), out _);
        }

        return data;
    }

    /// <summary>PDFium's embedded file functions.</summary>
    internal static partial class NativeMethods
    {
        /// <summary>The native library name.</summary>
        private const string Library = "pdfium";

        /// <summary>Counts the <c>/EmbeddedFiles</c> name tree entries.</summary>
        /// <param name="document">The document.</param>
        /// <returns>The count.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFDoc_GetAttachmentCount(PdfiumDocumentHandle document);

        /// <summary>Gets an embedded file entry, owned by the document.</summary>
        /// <param name="document">The document.</param>
        /// <param name="index">The entry.</param>
        /// <returns>The attachment, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFDoc_GetAttachment(PdfiumDocumentHandle document, int index);

        /// <summary>Copies an attachment's name as UTF-16LE with a terminator.</summary>
        /// <param name="attachment">The attachment.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDFAttachment_GetName(nint attachment, void* buffer, CULong length);

        /// <summary>Copies an attachment's decoded file bytes.</summary>
        /// <param name="attachment">The attachment.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <param name="needed">The file length in bytes.</param>
        /// <returns>Nonzero when the attachment has a file.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFAttachment_GetFile(nint attachment, void* buffer, CULong length, out CULong needed);
    }
}
