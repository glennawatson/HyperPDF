// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;

namespace PdfViewerLite.HyperPdf.Tests.RenderingComparison;

/// <summary>Calls PDFium's native bitmap functions for reference pixels in rendering tests.</summary>
internal static unsafe partial class PdfiumReferenceRendering
{
    /// <summary>PDFium's BGRA bitmap format.</summary>
    private const int Bgra = 4;

    /// <summary>The bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>Serializes calls because PDFium's document and rendering APIs are not thread safe.</summary>
    private static readonly Lock Sync = new();

    /// <summary>Initializes the native reference renderer once for the test process.</summary>
    static PdfiumReferenceRendering() => NativeMethods.FPDF_InitLibrary();

    /// <summary>Gets the number of pages from the native PDFium document.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <returns>The page count.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot open the PDF.</exception>
    internal static int PageCount(byte[] pdf)
    {
        lock (Sync)
        {
            fixed (byte* data = pdf)
            {
                var document = NativeMethods.FPDF_LoadMemDocument64(data, (nuint)pdf.Length, null);
                if (document == 0)
                {
                    throw new InvalidDataException("PDFium could not open the reference PDF.");
                }

                try
                {
                    return NativeMethods.FPDF_GetPageCount(document);
                }
                finally
                {
                    NativeMethods.FPDF_CloseDocument(document);
                }
            }
        }
    }

    /// <summary>Renders a whole page using the native PDFium bitmap function.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="scale">The pixels per PDF point.</param>
    /// <returns>The reference BGRA pixels.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot open, size or render the page.</exception>
    internal static ComparisonRaster RenderPage(byte[] pdf, int pageIndex, float scale) =>
        Render(pdf, new(pageIndex, scale, PageRotation.None, 0, 0, RenderFlags.None));

    /// <summary>Renders a whole page or a tile with the native PDFium bitmap function.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="request">The page, scale, offsets and flags.</param>
    /// <param name="tileWidth">The tile width, or zero for the whole page.</param>
    /// <param name="tileHeight">The tile height, or zero for the whole page.</param>
    /// <returns>The reference BGRA pixels.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot open, size or render the page.</exception>
    internal static ComparisonRaster Render(byte[] pdf, in PageRenderInfo request, int tileWidth = 0, int tileHeight = 0)
    {
        lock (Sync)
        {
            return RenderLocked(pdf, request, tileWidth, tileHeight);
        }
    }

    /// <summary>Opens the native document for one rendering call while the PDFium lock is held.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="request">The page and region.</param>
    /// <param name="tileWidth">The tile width, or zero.</param>
    /// <param name="tileHeight">The tile height, or zero.</param>
    /// <returns>The rendered pixels.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot open, size or render the page.</exception>
    private static ComparisonRaster RenderLocked(byte[] pdf, in PageRenderInfo request, int tileWidth, int tileHeight)
    {
        fixed (byte* data = pdf)
        {
            var document = NativeMethods.FPDF_LoadMemDocument64(data, (nuint)pdf.Length, null);
            if (document == 0)
            {
                throw new InvalidDataException("PDFium could not open the reference PDF.");
            }

            try
            {
                if (NativeMethods.FPDF_GetPageSizeByIndexF(document, request.PageIndex, out var size) == 0)
                {
                    throw new InvalidDataException("PDFium could not read the reference page size.");
                }

                var page = NativeMethods.FPDF_LoadPage(document, request.PageIndex);
                if (page == 0)
                {
                    throw new InvalidDataException("PDFium could not load the reference page.");
                }

                try
                {
                    return RenderLoadedPage(page, size, request, tileWidth, tileHeight);
                }
                finally
                {
                    NativeMethods.FPDF_ClosePage(page);
                }
            }
            finally
            {
                NativeMethods.FPDF_CloseDocument(document);
            }
        }
    }

    /// <summary>Draws an open native page into an owned BGRA buffer.</summary>
    /// <param name="page">The open native page.</param>
    /// <param name="size">The PDF page size.</param>
    /// <param name="request">The page region and flags.</param>
    /// <param name="tileWidth">The tile width, or zero.</param>
    /// <param name="tileHeight">The tile height, or zero.</param>
    /// <returns>The rendered pixels.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot create the bitmap.</exception>
    private static ComparisonRaster RenderLoadedPage(nint page, NativePageSize size, in PageRenderInfo request, int tileWidth, int tileHeight)
    {
        TileGrid.GetPagePixelSize(new(size.Width, size.Height), request.Rotation, request.Scale, out var pageWidth, out var pageHeight);
        var width = tileWidth > 0 ? tileWidth : pageWidth;
        var height = tileHeight > 0 ? tileHeight : pageHeight;
        var pixels = new byte[checked(width * height * BytesPerPixel)];
        Array.Fill(pixels, byte.MaxValue);
        fixed (byte* target = pixels)
        {
            var bitmap = NativeMethods.FPDFBitmap_CreateEx(width, height, Bgra, target, width * BytesPerPixel);
            if (bitmap == 0)
            {
                throw new InvalidDataException("PDFium could not create the reference bitmap.");
            }

            try
            {
                NativeMethods.FPDF_RenderPageBitmap(bitmap, page, -request.OffsetX, -request.OffsetY, pageWidth, pageHeight, (int)request.Rotation, GetNativeFlags(request.Flags));
            }
            finally
            {
                NativeMethods.FPDFBitmap_Destroy(bitmap);
            }
        }

        return new(width, height, pixels);
    }

    /// <summary>Maps viewer rendering flags to PDFium's native bitmap flags.</summary>
    /// <param name="flags">The viewer flags.</param>
    /// <returns>The native flags.</returns>
    private static int GetNativeFlags(RenderFlags flags)
    {
        var native = 0;
        if ((flags & RenderFlags.Annotations) != 0)
        {
            native |= 0x01;
        }

        if ((flags & RenderFlags.Grayscale) != 0)
        {
            native |= 0x08;
        }

        if ((flags & RenderFlags.Printing) != 0)
        {
            native |= 0x800;
        }

        return native;
    }

    /// <summary>The two dimensions returned by PDFium's page-size function.</summary>
    /// <param name="Width">The width in PDF points.</param>
    /// <param name="Height">The height in PDF points.</param>
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativePageSize(float Width, float Height);

    /// <summary>The native functions needed by the rendering comparisons.</summary>
    private static unsafe partial class NativeMethods
    {
        /// <summary>Initializes PDFium.</summary>
        [LibraryImport("pdfium")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDF_InitLibrary();

        /// <summary>Opens PDF bytes while the pinned buffer remains alive.</summary>
        /// <param name="data">The pinned bytes.</param>
        /// <param name="size">The byte count.</param>
        /// <param name="password">The UTF-8 password, or null.</param>
        /// <returns>The document handle.</returns>
        [LibraryImport("pdfium")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDF_LoadMemDocument64(void* data, nuint size, byte* password);

        /// <summary>Closes a document handle.</summary>
        /// <param name="document">The document handle.</param>
        [LibraryImport("pdfium")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDF_CloseDocument(nint document);

        /// <summary>Gets a document's page count.</summary>
        /// <param name="document">The document handle.</param>
        /// <returns>The page count.</returns>
        [LibraryImport("pdfium")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDF_GetPageCount(nint document);

        /// <summary>Gets a page's dimensions.</summary>
        /// <param name="document">The document handle.</param>
        /// <param name="pageIndex">The zero-based page.</param>
        /// <param name="size">The page size.</param>
        /// <returns>Nonzero on success.</returns>
        [LibraryImport("pdfium")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDF_GetPageSizeByIndexF(nint document, int pageIndex, out NativePageSize size);

        /// <summary>Loads a page for rendering.</summary>
        /// <param name="document">The document handle.</param>
        /// <param name="pageIndex">The zero-based page.</param>
        /// <returns>The page handle.</returns>
        [LibraryImport("pdfium")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDF_LoadPage(nint document, int pageIndex);

        /// <summary>Closes a page handle.</summary>
        /// <param name="page">The page handle.</param>
        [LibraryImport("pdfium")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDF_ClosePage(nint page);

        /// <summary>Wraps the caller's BGRA buffer in a PDFium bitmap.</summary>
        /// <param name="width">The bitmap width.</param>
        /// <param name="height">The bitmap height.</param>
        /// <param name="format">The native BGRA format.</param>
        /// <param name="pixels">The pinned output buffer.</param>
        /// <param name="stride">The bytes per row.</param>
        /// <returns>The bitmap handle.</returns>
        [LibraryImport("pdfium")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFBitmap_CreateEx(int width, int height, int format, void* pixels, int stride);

        /// <summary>Closes a bitmap handle.</summary>
        /// <param name="bitmap">The bitmap handle.</param>
        [LibraryImport("pdfium")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDFBitmap_Destroy(nint bitmap);

        /// <summary>Renders a page into a native bitmap.</summary>
        /// <param name="bitmap">The bitmap handle.</param>
        /// <param name="page">The page handle.</param>
        /// <param name="startX">The page's horizontal origin.</param>
        /// <param name="startY">The page's vertical origin.</param>
        /// <param name="width">The full page width in pixels.</param>
        /// <param name="height">The full page height in pixels.</param>
        /// <param name="rotation">The quarter turns.</param>
        /// <param name="flags">The native render flags.</param>
        [LibraryImport("pdfium")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDF_RenderPageBitmap(nint bitmap, nint page, int startX, int startY, int width, int height, int rotation, int flags);
    }
}
