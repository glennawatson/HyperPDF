// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The names the PDF/A graphics rules read that the known-name list does not hold, interned once per validation.</summary>
/// <param name="Subtype2">The form XObject <c>/Subtype2</c> key.</param>
/// <param name="HalftonePhase">The graphics state <c>/HTP</c> key.</param>
/// <param name="HalftoneOrigin">The graphics state <c>/HTO</c> key.</param>
/// <param name="Default">The <c>/Default</c> transfer function name.</param>
/// <param name="FillAlpha">The graphics state <c>/ca</c> key.</param>
/// <param name="DefaultGray">The <c>/DefaultGray</c> colour space name.</param>
/// <param name="DefaultRgb">The <c>/DefaultRGB</c> colour space name.</param>
/// <param name="DefaultCmyk">The <c>/DefaultCMYK</c> colour space name.</param>
/// <param name="CalCmyk">The <c>/CalCMYK</c> colour space family.</param>
/// <param name="ThreeDData">The 3D annotation <c>/3DD</c> key.</param>
/// <param name="ThreeDReference">The <c>/3DRef</c> type of a 3D reference dictionary.</param>
/// <param name="U3d">The <c>/U3D</c> 3D stream subtype.</param>
/// <param name="Prc">The <c>/PRC</c> 3D stream subtype.</param>
internal sealed record PdfAGraphicsNames(
    PdfName Subtype2,
    PdfName HalftonePhase,
    PdfName HalftoneOrigin,
    PdfName Default,
    PdfName FillAlpha,
    PdfName DefaultGray,
    PdfName DefaultRgb,
    PdfName DefaultCmyk,
    PdfName CalCmyk,
    PdfName ThreeDData,
    PdfName ThreeDReference,
    PdfName U3d,
    PdfName Prc)
{
    /// <summary>Interns the names in a document's name table.</summary>
    /// <param name="names">The name table.</param>
    /// <returns>The names.</returns>
    internal static PdfAGraphicsNames Create(PdfNameTable names) => new(
        names.Intern("Subtype2"u8),
        names.Intern("HTP"u8),
        names.Intern("HTO"u8),
        names.Intern("Default"u8),
        names.Intern("ca"u8),
        names.Intern("DefaultGray"u8),
        names.Intern("DefaultRGB"u8),
        names.Intern("DefaultCMYK"u8),
        names.Intern("CalCMYK"u8),
        names.Intern("3DD"u8),
        names.Intern("3DRef"u8),
        names.Intern("U3D"u8),
        names.Intern("PRC"u8));
}
