// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdf.Engine.Tests.RenderingComparison;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>
/// Renders a page the way PDFium shows form widgets: PDFium draws widgets only through its form fill environment, so the page
/// is drawn with <c>FPDF_RenderPageBitmap</c> and the widgets with <c>FPDF_FFLDraw</c>.
/// </summary>
internal static unsafe partial class PdfiumReferenceFormRendering
{
    /// <summary>PDFium's BGRA bitmap format.</summary>
    private const int Bgra = 4;

    /// <summary>The bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The native flag that draws annotations.</summary>
    private const int AnnotationFlag = 0x01;

    /// <summary>Renders a page at one pixel per point with its form widgets.</summary>
    /// <param name="document">The document, opened with PDFium.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <param name="width">The page width in pixels.</param>
    /// <param name="height">The page height in pixels.</param>
    /// <returns>The BGRA pixels.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot create the form environment or the bitmap.</exception>
    internal static ComparisonRaster Render(PdfiumReferenceDocument document, int pageIndex, int width, int height)
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            var info = PdfiumReferenceAnnotations.CreateFormInfo();
            var form = PdfiumReferenceAnnotations.NativeMethods.FPDFDOC_InitFormFillEnvironment(document.Handle, info);
            try
            {
                return form == 0 ? throw new InvalidDataException("PDFium could not create the form fill environment.") : RenderWithForm(document, pageIndex, form, width, height);
            }
            finally
            {
                if (form != 0)
                {
                    PdfiumReferenceAnnotations.NativeMethods.FPDFDOC_ExitFormFillEnvironment(form);
                }

                NativeMemory.Free(info);
            }
        }
    }

    /// <summary>Draws a page and its widgets while the form environment is open.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <param name="form">The form environment.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The BGRA pixels.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot create the bitmap.</exception>
    private static ComparisonRaster RenderWithForm(PdfiumReferenceDocument document, int pageIndex, nint form, int width, int height)
    {
        var pixels = new byte[checked(width * height * BytesPerPixel)];
        Array.Fill(pixels, byte.MaxValue);
        using var page = PdfiumPage.Load(document, pageIndex);
        NativeMethods.FORM_OnAfterLoadPage(page.Handle, form);
        try
        {
            fixed (byte* target = pixels)
            {
                var bitmap = NativeMethods.FPDFBitmap_CreateEx(width, height, Bgra, target, width * BytesPerPixel);
                if (bitmap == 0)
                {
                    throw new InvalidDataException("PDFium could not create the reference bitmap.");
                }

                try
                {
                    NativeMethods.FPDF_RenderPageBitmap(bitmap, page.Handle, 0, 0, width, height, 0, AnnotationFlag);
                    NativeMethods.FPDF_FFLDraw(form, bitmap, page.Handle, 0, 0, width, height, 0, AnnotationFlag);
                }
                finally
                {
                    NativeMethods.FPDFBitmap_Destroy(bitmap);
                }
            }
        }
        finally
        {
            NativeMethods.FORM_OnBeforeClosePage(page.Handle, form);
        }

        return new(width, height, pixels);
    }

    /// <summary>PDFium's bitmap and form drawing functions.</summary>
    private static partial class NativeMethods
    {
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

        /// <summary>Tells the form environment a page was loaded, so it creates the page's widgets.</summary>
        /// <param name="page">The page handle.</param>
        /// <param name="form">The form environment.</param>
        [LibraryImport("pdfium")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FORM_OnAfterLoadPage(nint page, nint form);

        /// <summary>Tells the form environment a page is closing.</summary>
        /// <param name="page">The page handle.</param>
        /// <param name="form">The form environment.</param>
        [LibraryImport("pdfium")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FORM_OnBeforeClosePage(nint page, nint form);

        /// <summary>Draws the form widgets of a page onto a bitmap.</summary>
        /// <param name="form">The form environment.</param>
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
        internal static partial void FPDF_FFLDraw(nint form, nint bitmap, nint page, int startX, int startY, int width, int height, int rotation, int flags);
    }
}
