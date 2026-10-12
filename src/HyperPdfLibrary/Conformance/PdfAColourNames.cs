// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The names the PDF/A colour and image checks use that the library has no known name for, interned once per validation.</summary>
/// <param name="DefaultGray">The <c>/DefaultGray</c> colour space resource.</param>
/// <param name="DefaultRgb">The <c>/DefaultRGB</c> colour space resource.</param>
/// <param name="DefaultCmyk">The <c>/DefaultCMYK</c> colour space resource.</param>
/// <param name="CalCmyk">The obsolete <c>/CalCMYK</c> family.</param>
/// <param name="OutputIntents">The <c>/OutputIntents</c> key.</param>
/// <param name="DestOutputProfile">The <c>/DestOutputProfile</c> key.</param>
/// <param name="DestOutputProfileRef">The <c>/DestOutputProfileRef</c> key.</param>
/// <param name="PdfAIntent">The <c>/GTS_PDFA1</c> output intent subtype.</param>
/// <param name="PdfXIntent">The <c>/GTS_PDFX</c> output intent subtype.</param>
/// <param name="HalftoneType">The <c>/HalftoneType</c> key.</param>
/// <param name="HalftoneName">The <c>/HalftoneName</c> key.</param>
/// <param name="TransferFunction">The <c>/TransferFunction</c> key.</param>
/// <param name="Default">The <c>/Default</c> halftone and colourant name.</param>
/// <param name="ThreeDReference">The <c>/3DRef</c> type of a 3D reference dictionary.</param>
/// <param name="ProcessColourants">The process colourant names a DeviceN space needs no <c>/Colorants</c> entry for: Cyan, Magenta, Yellow and Black.</param>
internal sealed record PdfAColourNames(
    PdfName DefaultGray,
    PdfName DefaultRgb,
    PdfName DefaultCmyk,
    PdfName CalCmyk,
    PdfName OutputIntents,
    PdfName DestOutputProfile,
    PdfName DestOutputProfileRef,
    PdfName PdfAIntent,
    PdfName PdfXIntent,
    PdfName HalftoneType,
    PdfName HalftoneName,
    PdfName TransferFunction,
    PdfName Default,
    PdfName ThreeDReference,
    PdfName[] ProcessColourants)
{
    /// <summary>Interns the names in a document's name table.</summary>
    /// <param name="names">The name table.</param>
    /// <returns>The names.</returns>
    internal static PdfAColourNames Create(PdfNameTable names) => new(
        names.Intern("DefaultGray"u8),
        names.Intern("DefaultRGB"u8),
        names.Intern("DefaultCMYK"u8),
        names.Intern("CalCMYK"u8),
        names.Intern("OutputIntents"u8),
        names.Intern("DestOutputProfile"u8),
        names.Intern("DestOutputProfileRef"u8),
        names.Intern("GTS_PDFA1"u8),
        names.Intern("GTS_PDFX"u8),
        names.Intern("HalftoneType"u8),
        names.Intern("HalftoneName"u8),
        names.Intern("TransferFunction"u8),
        names.Intern("Default"u8),
        names.Intern("3DRef"u8),
        [names.Intern("Cyan"u8), names.Intern("Magenta"u8), names.Intern("Yellow"u8), names.Intern("Black"u8)]);
}
