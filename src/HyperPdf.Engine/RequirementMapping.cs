// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdfLibrary.Requirements;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine;

/// <summary>Passes the library's document requirement results and save changes on to the app's reports.</summary>
internal static class RequirementMapping
{
    /// <summary>Checks a document's requirements against what HyperPDF provides.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The report; <see cref="RequirementReport.None"/> when the document has no unmet requirement.</returns>
    internal static RequirementReport Check(HyperPdfDocument self)
    {
        if (self.IsDisposed)
        {
            return RequirementReport.None;
        }

        var assessment = PdfDocumentRequirements.Evaluate(self.Document);
        if (assessment.Decision == PdfRequirementDecision.Satisfied)
        {
            return RequirementReport.None;
        }

        var level = assessment.Decision == PdfRequirementDecision.Critical ? RequirementLevel.Critical : RequirementLevel.Fallback;
        return new(level, assessment.Summary, Details(assessment));
    }

    /// <summary>Copies the notices from a save: every requirement the save removed.</summary>
    /// <param name="writer">The library writer's report.</param>
    /// <param name="caller">The app's report, or <see langword="null"/>.</param>
    internal static void AddWarnings(PdfSaveReport writer, DocumentSaveReport? caller)
    {
        if (caller is null)
        {
            return;
        }

        foreach (var change in writer.Requirements)
        {
            caller.RequirementWarnings.Add(change.Message);
        }
    }

    /// <summary>Lists the messages of the unmet requirements.</summary>
    /// <param name="assessment">The assessment.</param>
    /// <returns>One message for each unmet requirement.</returns>
    private static string[] Details(PdfRequirementAssessment assessment)
    {
        var details = new List<string>(assessment.Results.Count);
        foreach (var result in assessment.Results)
        {
            if (!result.IsMet)
            {
                details.Add(result.Message);
            }
        }

        return [.. details];
    }
}
