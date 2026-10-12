// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>Lists the PDF/A rule areas the validator runs, in order.</summary>
internal static class PdfARuleAreas
{
    /// <summary>Gets every rule area.</summary>
    /// <returns>The areas.</returns>
    internal static PdfARuleArea[] All() =>
    [
        new("document", PdfADocumentRules.Rules, PdfADocumentRules.Check),
        new("graphics", PdfAGraphicsRules.Rules, PdfAGraphicsRules.Check),
        new("structure types", PdfAStructureElementRules.Rules, PdfAStructureElementRules.Check),
    ];
}
