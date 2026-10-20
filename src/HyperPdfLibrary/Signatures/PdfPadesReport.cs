// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// The PAdES baseline level of one signature, from the checks that need no trust list or network. A level here says the
/// signature has the structure and evidence that level requires; it does not say the signer is trusted.
/// </summary>
/// <param name="FieldName">The signature field's name.</param>
/// <param name="Level">The highest level whose checks, and every lower level's, all passed.</param>
/// <param name="IsCryptographicallyValid">Whether the digest and the signer's signature checked out. It is reported apart from the level.</param>
/// <param name="Findings">Every check made, in level order.</param>
/// <param name="NotPerformed">Checks that were not made, such as trust and online revocation lookups.</param>
[DebuggerDisplay("PdfPadesReport: {FieldName} {Level}")]
public sealed record PdfPadesReport(string FieldName, PdfPadesLevel Level, bool IsCryptographicallyValid, PdfPadesFinding[] Findings, string[] NotPerformed)
{
    /// <summary>Gets the checks that failed for the level above the one reached, so a reader can see why it was not reached.</summary>
    /// <returns>The failed checks of the next level up; empty when the top level was reached.</returns>
    public PdfPadesFinding[] WhyNotNextLevel()
    {
        if (Level == PdfPadesLevel.BaselineLta)
        {
            return [];
        }

        var next = Level + 1;
        var failed = new List<PdfPadesFinding>();
        foreach (var finding in Findings)
        {
            if (finding.Level == next && !finding.Passed)
            {
                failed.Add(finding);
            }
        }

        return [.. failed];
    }
}
