// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The result of checking a document against a PDF/A level. HyperPdf checks most, not all, of the rules veraPDF checks;
/// <see cref="Unchecked"/> lists the rest, so a file with no failures may still break a rule this report does not cover.
/// This is a practical check, not a certification: use veraPDF for that.
/// </summary>
/// <param name="Level">The level checked.</param>
/// <param name="Failed">The rules the document breaks, in clause order.</param>
/// <param name="Checked">Every rule the validator checked, passed or failed, in clause order.</param>
/// <param name="Unchecked">The level's rules the validator does not check, in clause order.</param>
[DebuggerDisplay("PdfAValidationReport: {Level}, {Failed.Length} failed of {Checked.Length}")]
public sealed record PdfAValidationReport(PdfALevel Level, PdfARuleResult[] Failed, PdfARuleResult[] Checked, PdfARuleResult[] Unchecked)
{
    /// <summary>Gets a value indicating whether every checked rule holds.</summary>
    public bool Passed => Failed.Length == 0;
}
