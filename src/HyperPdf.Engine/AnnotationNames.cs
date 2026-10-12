// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>
/// The names annotation editing uses that the library does not know, interned once in a document's name table. The
/// <c>PVL</c> keys are the ones the PDFium engine writes, so either engine reads what the other wrote.
/// </summary>
/// <param name="Subject">The <c>/Subj</c> key.</param>
/// <param name="Text">The key holding the text of a text box or callout written here.</param>
/// <param name="FontSize">The key holding the text size of a text box or callout written here.</param>
/// <param name="Format">The key holding a formatted text box's format record.</param>
/// <param name="Removed">The key marking an annotation removed but kept; its value is the flags to restore.</param>
/// <param name="Anchor">The key holding a callout's text corner.</param>
/// <param name="PendingReply">The key under which the PDFium engine names a reply's comment until saving.</param>
/// <param name="Callout">The <c>/FreeTextCallout</c> intent.</param>
/// <param name="TypeWriter">The <c>/FreeTextTypeWriter</c> intent.</param>
/// <param name="PolygonCloud">The <c>/PolygonCloud</c> intent.</param>
[DebuggerDisplay("AnnotationNames")]
internal sealed record AnnotationNames(
    PdfName Subject,
    PdfName Text,
    PdfName FontSize,
    PdfName Format,
    PdfName Removed,
    PdfName Anchor,
    PdfName PendingReply,
    PdfName Callout,
    PdfName TypeWriter,
    PdfName PolygonCloud)
{
    /// <summary>Interns the names in a document's name table.</summary>
    /// <param name="names">The name table.</param>
    /// <returns>The names.</returns>
    internal static AnnotationNames Create(PdfNameTable names) => new(
        names.Intern("Subj"u8),
        names.Intern("PVLText"u8),
        names.Intern("PVLFontSize"u8),
        names.Intern("PVLFormat"u8),
        names.Intern("PVLRemoved"u8),
        names.Intern("PVLAnchor"u8),
        names.Intern("PVLInReplyTo"u8),
        names.Intern("FreeTextCallout"u8),
        names.Intern("FreeTextTypeWriter"u8),
        names.Intern("PolygonCloud"u8));
}
