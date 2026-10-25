// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Requirements;

namespace HyperPdf.Engine;

/// <summary>Formats typed requirement results for the interface without changing the reusable PDF library.</summary>
internal static class RequirementDisplay
{
    /// <summary>The most feature names shown before counting the remainder.</summary>
    private const int MaxNamed = 5;

    /// <summary>Describes a typed result in the current interface language.</summary>
    /// <param name="result">The requirement result.</param>
    /// <returns>The translated description.</returns>
    internal static string Describe(PdfRequirementResult result)
    {
        var requirement = result.Requirement;
        var capability = PdfRequirementCapabilities.HyperPdf.Find(requirement.Type);
        var title = UiText.Get(capability?.Title ?? result.Feature);
        var note = UiText.Get(capability?.Note ?? string.Empty);
        var penalty = result.IsMet ? string.Empty : UiText.Format(" Penalty {0} of 100.", requirement.Penalty);
        return result.Outcome switch
        {
            PdfRequirementOutcome.Met => UiText.Format("{0}: supported.", title),
            PdfRequirementOutcome.PartlySupported => UiText.Format("{0}: HyperPDF supports only part of this. {1}{2}", title, note, penalty),
            PdfRequirementOutcome.VersionNotSupported => UiText.Format("{0}: the file asks for version {1}, which HyperPDF does not provide.{2}", title, requirement.Version?.Text, penalty),
            PdfRequirementOutcome.UnknownType => UiText.Format("The file lists a requirement named {0}, which HyperPDF does not know, so it cannot check it.{1}", requirement.Type, penalty),
            _ => UiText.Format("{0}: HyperPDF does not support this. {1}{2}", title, note, penalty),
        };
    }

    /// <summary>Summarizes an assessment in the current interface language.</summary>
    /// <param name="assessment">The typed assessment.</param>
    /// <returns>The translated summary, or empty when satisfied.</returns>
    internal static string Summary(PdfRequirementAssessment assessment)
    {
        if (assessment.Decision == PdfRequirementDecision.Satisfied)
        {
            return string.Empty;
        }

        var names = Names(assessment.Results);
        return assessment.Decision == PdfRequirementDecision.Critical
            ? UiText.Format("This file says it needs features HyperPDF cannot provide: {0}. It cannot look or work as its author meant.", names)
            : UiText.Format("This file uses features HyperPDF does not fully support: {0}. HyperPDF shows what it can and leaves out the rest.", names);
    }

    /// <summary>Builds a translated list of unmet feature names.</summary>
    /// <param name="results">The checked requirements.</param>
    /// <returns>The names and any remaining count.</returns>
    private static string Names(IReadOnlyList<PdfRequirementResult> results)
    {
        var builder = new StringBuilder();
        var unmet = 0;
        foreach (var result in results)
        {
            if (result.IsMet)
            {
                continue;
            }

            if (unmet < MaxNamed)
            {
                _ = builder.Append(unmet == 0 ? string.Empty : UiText.Get(", ")).Append(UiText.Get(result.Feature));
            }

            unmet++;
        }

        return unmet > MaxNamed ? UiText.Format("{0} and {1} more", builder, unmet - MaxNamed) : builder.ToString();
    }
}
