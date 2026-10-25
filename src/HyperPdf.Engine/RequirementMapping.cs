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
        return Describe(assessment);
    }

    /// <summary>Retains a completed assessment so visible notices can follow interface language changes.</summary>
    /// <param name="assessment">The completed assessment, independent of the open document.</param>
    /// <returns>The report with its typed display provider.</returns>
    internal static RequirementReport Describe(PdfRequirementAssessment assessment)
    {
        if (assessment.Decision == PdfRequirementDecision.Satisfied)
        {
            return RequirementReport.None;
        }

        var level = assessment.Decision == PdfRequirementDecision.Critical ? RequirementLevel.Critical : RequirementLevel.Fallback;
        return new(level, RequirementDisplay.Summary(assessment), Details(assessment)) { LocalisedText = new AssessmentText(assessment) };
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
            var removed = new RemovedRequirement(change.Type, PdfRequirementCapabilities.HyperPdf.Find(change.Type)?.Title);
            caller.RemovedRequirements.Add(removed);
            caller.RequirementWarnings.Add(removed.Text);
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
                details.Add(RequirementDisplay.Describe(result));
            }
        }

        return [.. details];
    }

    /// <summary>Keeps typed assessment data, without retaining or reopening the PDF document.</summary>
    /// <param name="assessment">The completed requirement assessment.</param>
    private sealed class AssessmentText(PdfRequirementAssessment assessment) : IRequirementText
    {
        /// <inheritdoc/>
        public string Summary => RequirementDisplay.Summary(assessment);

        /// <inheritdoc/>
        public IReadOnlyList<string> Details => RequirementMapping.Details(assessment);
    }
}
