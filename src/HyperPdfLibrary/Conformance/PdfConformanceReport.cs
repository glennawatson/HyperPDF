// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// What a file says about its PDF/A, PDF/UA and PDF/X conformance, and the features its drawn content uses. For a file
/// that claims a PDF/A level, <see cref="PdfAValidation"/> holds the rule-by-rule check of that level with clause numbers.
/// This is a practical report, not a certification; veraPDF stays the reference validator.
/// </summary>
/// <param name="HasXmp">Whether the catalog has a readable XMP packet.</param>
/// <param name="PdfA">The PDF/A claim from <c>pdfaid</c>, or null when the file makes none.</param>
/// <param name="PdfUaPart">The PDF/UA part from <c>pdfuaid:part</c>, or null.</param>
/// <param name="PdfXVersion">The <c>GTS_PDFXVersion</c> (Info dictionary, else XMP), for example "PDF/X-4", or null.</param>
/// <param name="OutputIntents">The catalog's output intents.</param>
/// <param name="Observations">The features seen, in <see cref="PdfConformanceFinding"/> order. Only features that were seen are listed.</param>
[DebuggerDisplay("PdfConformanceReport: PDF/A {PdfA}, {Observations.Length} observations")]
public sealed record PdfConformanceReport(
    bool HasXmp,
    PdfAClaim? PdfA,
    int? PdfUaPart,
    string? PdfXVersion,
    PdfOutputIntentSummary[] OutputIntents,
    PdfConformanceObservation[] Observations)
{
    /// <summary>
    /// Gets the rule-by-rule check of the claimed PDF/A level: each rule with its standard, clause, test number and
    /// description, and the rules not checked. It is <see langword="null"/> when the file claims no PDF/A level the
    /// standards define.
    /// </summary>
    public PdfAValidationReport? PdfAValidation { get; init; }

    /// <summary>Gets a value indicating whether any observation is forbidden by the claimed PDF/A part.</summary>
    public bool HasConflicts => Array.Exists(Observations, static observation => observation.ConflictsWithClaim);

    /// <summary>Finds an observation by kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="observation">Receives the observation.</param>
    /// <returns><see langword="true"/> when the kind was seen.</returns>
    public bool TryGet(PdfConformanceFinding kind, out PdfConformanceObservation observation)
    {
        foreach (var candidate in Observations)
        {
            if (candidate.Kind != kind)
            {
                continue;
            }

            observation = candidate;
            return true;
        }

        observation = default;
        return false;
    }
}
