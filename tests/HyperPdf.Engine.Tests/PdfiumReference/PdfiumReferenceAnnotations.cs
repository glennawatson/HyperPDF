// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>Reads PDFium's annotations and form field values for parity tests.</summary>
internal static unsafe partial class PdfiumReferenceAnnotations
{
    /// <summary>The bytes reserved for the zeroed <c>FPDF_FORMFILLINFO</c>, larger than any version of the structure.</summary>
    private const int FormInfoBytes = 1024;

    /// <summary>The form fill interface version: no XFA and no callbacks. PDFium checks each callback before calling it.</summary>
    private const int FormInfoVersion = 1;

    /// <summary>The field type PDFium gives an annotation that is not a field.</summary>
    private const int NoField = -1;

    /// <summary>The PDF subtype names, indexed by <c>FPDF_ANNOT_*</c> value.</summary>
    private static readonly string[] SubtypeNames =
    [
        string.Empty, "Text", "Link", "FreeText", "Line", "Square", "Circle", "Polygon", "PolyLine", "Highlight", "Underline",
        "Squiggly", "StrikeOut", "Stamp", "Caret", "Ink", "Popup", "FileAttachment", "Sound", "Movie", "Widget", "Screen",
        "PrinterMark", "TrapNet", "Watermark", "3D", "RichMedia", "XFAWidget", "Redact",
    ];

    /// <summary>Reads a page's annotations in <c>/Annots</c> order, with field names and values for widgets.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>The annotations.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot load the page.</exception>
    internal static List<PdfiumAnnotation> GetAnnotations(PdfiumReferenceDocument document, int pageIndex)
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            var handle = document.Handle;
            var info = NativeMethods.FPDF_GetFormType(handle) == 0 ? null : CreateFormInfo();
            var form = info is null ? 0 : NativeMethods.FPDFDOC_InitFormFillEnvironment(handle, info);
            try
            {
                using var page = PdfiumPage.Load(document, pageIndex);
                return ReadAnnotations(page, form);
            }
            finally
            {
                if (form != 0)
                {
                    NativeMethods.FPDFDOC_ExitFormFillEnvironment(form);
                }

                NativeMemory.Free(info);
            }
        }
    }

    /// <summary>Allocates the zeroed form fill information PDFium reads while the form environment is open.</summary>
    /// <returns>The structure; free it after the environment closes.</returns>
    internal static void* CreateFormInfo()
    {
        var info = NativeMemory.AllocZeroed(FormInfoBytes);
        *(int*)info = FormInfoVersion;
        return info;
    }

    /// <summary>Reads every annotation of a loaded page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="form">The form handle, or zero when the document has no form.</param>
    /// <returns>The annotations.</returns>
    private static List<PdfiumAnnotation> ReadAnnotations(PdfiumPage page, nint form)
    {
        var count = Math.Max(NativeMethods.FPDFPage_GetAnnotCount(page.Handle), 0);
        var annotations = new List<PdfiumAnnotation>(count);
        for (var i = 0; i < count; i++)
        {
            var annotation = NativeMethods.FPDFPage_GetAnnot(page.Handle, i);
            if (annotation == 0)
            {
                continue;
            }

            try
            {
                annotations.Add(ReadAnnotation(annotation, form));
            }
            finally
            {
                NativeMethods.FPDFPage_CloseAnnot(annotation);
            }
        }

        return annotations;
    }

    /// <summary>Reads one annotation.</summary>
    /// <param name="annotation">The native annotation.</param>
    /// <param name="form">The form handle, or zero.</param>
    /// <returns>The annotation.</returns>
    private static PdfiumAnnotation ReadAnnotation(nint annotation, nint form)
    {
        var subtype = NativeMethods.FPDFAnnot_GetSubtype(annotation);
        var name = (uint)subtype < (uint)SubtypeNames.Length ? SubtypeNames[subtype] : string.Empty;
        return form == 0
            ? new(subtype, name, NoField, null, null)
            : new(
                subtype,
                name,
                NativeMethods.FPDFAnnot_GetFormFieldType(form, annotation),
                PdfiumNativeStrings.ReadUtf16(form, annotation, &NativeMethods.FPDFAnnot_GetFormFieldName),
                PdfiumNativeStrings.ReadUtf16(form, annotation, &NativeMethods.FPDFAnnot_GetFormFieldValue));
    }

    /// <summary>PDFium's form environment, annotation and form field functions.</summary>
    internal static partial class NativeMethods
    {
        /// <summary>The native library name.</summary>
        private const string Library = "pdfium";

        /// <summary>Gets the kind of interactive form.</summary>
        /// <param name="document">The document.</param>
        /// <returns>0 for none, 1 for AcroForm, 2 or 3 for XFA.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDF_GetFormType(PdfiumDocumentHandle document);

        /// <summary>Creates the form environment that form field queries need.</summary>
        /// <param name="document">The document.</param>
        /// <param name="formInfo">The <c>FPDF_FORMFILLINFO</c>, which must outlive the environment.</param>
        /// <returns>The form handle, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFDOC_InitFormFillEnvironment(PdfiumDocumentHandle document, void* formInfo);

        /// <summary>Closes a form environment.</summary>
        /// <param name="form">The form handle.</param>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDFDOC_ExitFormFillEnvironment(nint form);

        /// <summary>Counts a page's annotations.</summary>
        /// <param name="page">The page.</param>
        /// <returns>The count.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFPage_GetAnnotCount(nint page);

        /// <summary>Opens an annotation, closed with <see cref="FPDFPage_CloseAnnot"/>.</summary>
        /// <param name="page">The page.</param>
        /// <param name="index">The annotation.</param>
        /// <returns>The annotation, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFPage_GetAnnot(nint page, int index);

        /// <summary>Closes an annotation.</summary>
        /// <param name="annotation">The annotation.</param>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial void FPDFPage_CloseAnnot(nint annotation);

        /// <summary>Gets an annotation's subtype.</summary>
        /// <param name="annotation">The annotation.</param>
        /// <returns>The <c>FPDF_ANNOT_*</c> value.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFAnnot_GetSubtype(nint annotation);

        /// <summary>Gets a widget's field type.</summary>
        /// <param name="form">The form handle.</param>
        /// <param name="annotation">The annotation.</param>
        /// <returns>The <c>FPDF_FORMFIELD_*</c> value, or -1 when it is not a field.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFAnnot_GetFormFieldType(nint form, nint annotation);

        /// <summary>Copies a widget's full field name as UTF-16LE with a terminator.</summary>
        /// <param name="form">The form handle.</param>
        /// <param name="annotation">The annotation.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes, or zero when it is not a field.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDFAnnot_GetFormFieldName(nint form, nint annotation, void* buffer, CULong length);

        /// <summary>Copies a widget's field value as UTF-16LE with a terminator.</summary>
        /// <param name="form">The form handle.</param>
        /// <param name="annotation">The annotation.</param>
        /// <param name="buffer">The buffer, or null to measure.</param>
        /// <param name="length">The buffer length in bytes.</param>
        /// <returns>The length needed in bytes, or zero when it is not a field.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CULong FPDFAnnot_GetFormFieldValue(nint form, nint annotation, void* buffer, CULong length);
    }
}
