// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>Reads PDFium's XFA packets for parity tests. Nothing is interpreted.</summary>
internal static unsafe partial class PdfiumReferenceXfa
{
    /// <summary>Reads the <c>/AcroForm /XFA</c> packets in array order.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The packets; empty when the document has no XFA.</returns>
    internal static List<PdfiumXfaPacket> GetPackets(PdfiumReferenceDocument document)
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            var handle = document.Handle;
            var count = Math.Max(NativeMethods.FPDF_GetXFAPacketCount(handle), 0);
            var packets = new List<PdfiumXfaPacket>(count);
            for (var i = 0; i < count; i++)
            {
                packets.Add(new(ReadName(handle, i), ReadContent(handle, i)));
            }

            return packets;
        }
    }

    /// <summary>Copies a packet name's string bytes.</summary>
    /// <param name="document">The native document.</param>
    /// <param name="index">The packet.</param>
    /// <returns>The name, one character per byte.</returns>
    private static string ReadName(PdfiumDocumentHandle document, int index)
    {
        var length = checked((int)NativeMethods.FPDF_GetXFAPacketName(document, index, null, default).Value);
        if (length <= 1)
        {
            return string.Empty;
        }

        var buffer = new byte[length];
        fixed (byte* data = buffer)
        {
            _ = NativeMethods.FPDF_GetXFAPacketName(document, index, data, new((nuint)length));
        }

        return Encoding.Latin1.GetString(buffer, 0, length - 1);
    }

    /// <summary>Copies a packet's decoded bytes.</summary>
    /// <param name="document">The native document.</param>
    /// <param name="index">The packet.</param>
    /// <returns>The bytes; empty when PDFium gives none.</returns>
    private static byte[] ReadContent(PdfiumDocumentHandle document, int index)
    {
        if (NativeMethods.FPDF_GetXFAPacketContent(document, index, null, default, out var needed) == 0)
        {
            return [];
        }

        var data = new byte[checked((int)needed.Value)];
        fixed (byte* buffer = data)
        {
            _ = NativeMethods.FPDF_GetXFAPacketContent(document, index, buffer, new((nuint)data.Length), out _);
        }

        return data;
    }

    /// <summary>PDFium's XFA packet functions.</summary>
    internal static partial class NativeMethods
    {
        /// <summary>The native library name.</summary>
        private const string Library = "pdfium";

        /// <summary>Counts the <c>/AcroForm /XFA</c> packets; a single stream counts as one.</summary>
        /// <param name="document">The document.</param>
        /// <returns>The count, or -1.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDF_GetXFAPacketCount(PdfiumDocumentHandle document);

        /// <summary>Copies a packet's name string bytes with a terminator; a single stream's name is empty.</summary>
        /// <param name="document">The document.</param>
        /// <param name="index">The packet.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes, or zero for a bad index.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDF_GetXFAPacketName(PdfiumDocumentHandle document, int index, void* buffer, CULong length);

        /// <summary>Copies a packet's decoded stream bytes.</summary>
        /// <param name="document">The document.</param>
        /// <param name="index">The packet.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <param name="needed">The packet length in bytes.</param>
        /// <returns>Nonzero on success.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDF_GetXFAPacketContent(PdfiumDocumentHandle document, int index, void* buffer, CULong length, out CULong needed);
    }
}
