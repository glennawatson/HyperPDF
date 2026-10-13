// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Requirements;

/// <summary>Puts the results of checking requirements into plain words for the user.</summary>
internal static class PdfRequirementText
{
    /// <summary>The most features a summary names; the rest are counted.</summary>
    private const int MaxNamed = 5;

    /// <summary>Describes a requirement of a type the viewer knows.</summary>
    /// <param name="requirement">The requirement.</param>
    /// <param name="capability">The viewer's row for the type.</param>
    /// <param name="outcome">What the check found.</param>
    /// <returns>The message.</returns>
    internal static string Describe(PdfRequirement requirement, PdfViewerCapability capability, PdfRequirementOutcome outcome)
    {
        var title = capability.Title;
        return outcome switch
        {
            PdfRequirementOutcome.Met => $"{title}: supported.",
            PdfRequirementOutcome.PartlySupported => $"{title}: HyperPDF supports only part of this. {capability.Note}{Penalty(requirement)}",
            PdfRequirementOutcome.VersionNotSupported => $"{title}: the file asks for version {requirement.Version?.Text}, which HyperPDF does not provide.{Penalty(requirement)}",
            _ => $"{title}: HyperPDF does not support this. {capability.Note}{Penalty(requirement)}",
        };
    }

    /// <summary>Describes a requirement of a type the viewer does not know.</summary>
    /// <param name="requirement">The requirement.</param>
    /// <returns>The message.</returns>
    internal static string Unknown(PdfRequirement requirement) =>
        $"The file lists a requirement named {requirement.Type}, which HyperPDF does not know, so it cannot check it.{Penalty(requirement)}";

    /// <summary>Summarizes the results for the user.</summary>
    /// <param name="results">The results.</param>
    /// <param name="decision">What the total penalty allows.</param>
    /// <returns>The summary; empty when every requirement is met.</returns>
    internal static string Summarize(PdfRequirementResult[] results, PdfRequirementDecision decision)
    {
        if (decision == PdfRequirementDecision.Satisfied)
        {
            return string.Empty;
        }

        var names = Names(results);
        return decision == PdfRequirementDecision.Critical
            ? $"This file says it needs features HyperPDF cannot provide: {names}. It cannot look or work as its author meant."
            : $"This file uses features HyperPDF does not fully support: {names}. HyperPDF shows what it can and leaves out the rest.";
    }

    /// <summary>Names the features of the unmet requirements.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The names in a list, with the rest counted.</returns>
    private static string Names(PdfRequirementResult[] results)
    {
        var builder = new StringBuilder();
        var named = 0;
        var unmet = 0;
        foreach (var result in results)
        {
            if (result.IsMet)
            {
                continue;
            }

            unmet++;
            if (named >= MaxNamed)
            {
                continue;
            }

            _ = builder.Append(named == 0 ? string.Empty : ", ").Append(result.Feature);
            named++;
        }

        return unmet > named ? string.Create(CultureInfo.InvariantCulture, $"{builder} and {unmet - named} more") : builder.ToString();
    }

    /// <summary>Gets the sentence that says how many penalty points an unmet requirement adds.</summary>
    /// <param name="requirement">The requirement.</param>
    /// <returns>The sentence, led by a space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Penalty(PdfRequirement requirement) => string.Create(CultureInfo.InvariantCulture, $" Penalty {requirement.Penalty} of 100.");
}
