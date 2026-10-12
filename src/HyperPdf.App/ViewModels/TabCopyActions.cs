// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Archiving;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Optimizing;
using HyperPdf.Core.Redaction;

namespace HyperPdf.App.ViewModels;

/// <summary>The copy commands of a document tab: saving an optimised copy, saving a PDF/A copy and applying redactions.</summary>
internal static class TabCopyActions
{
    /// <summary>Gets saving an optimised copy, for engines that can write one.</summary>
    internal static TabCopyActionSpec<OptimizeCopyViewModel> Optimize { get; } = new(
        "Save _Optimised Copy…",
        "Save Optimised Copy… (needs the HyperPDF engine)",
        static document => DocumentFeature<IDocumentOptimizer>.Get(document) is not null,
        static tab => DocumentFeature<IDocumentOptimizer>.Get(tab.TryGetDocument()) is { } optimizer
            ? new(optimizer, tab.FilePath, CultureInfo.CurrentUICulture.Name)
            : null);

    /// <summary>Gets saving a PDF/A copy, for engines that can write one.</summary>
    internal static TabCopyActionSpec<ArchiveCopyViewModel> Archive { get; } = new(
        "Save P_DF/A Copy…",
        "Save PDF/A Copy… (needs the HyperPDF engine)",
        static document => DocumentFeature<IArchiveCopyWriter>.Get(document) is not null,
        static tab => DocumentFeature<IArchiveCopyWriter>.Get(tab.TryGetDocument()) is { } writer ? new(writer, tab.FilePath) : null);

    /// <summary>Gets applying redactions, for engines that can remove marked content for good.</summary>
    internal static TabCopyActionSpec<RedactionViewModel> Redact { get; } = new(
        "Apply _Redactions…",
        "Apply Redactions… (needs the HyperPDF engine)",
        static document => DocumentFeature<IDocumentRedactor>.Get(document) is not null && DocumentFeature<IAnnotationEditor>.Get(document) is not null,
        static tab => DocumentFeature<IDocumentRedactor>.Get(tab.TryGetDocument()) is { } redactor ? CreateRedaction(tab, redactor) : null);

    /// <summary>Counts the areas marked for redaction in a tab's document and the pages they are on.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>The counts; zero when the engine cannot list annotations.</returns>
    internal static RedactionMarks CountRedactions(DocumentTabViewModel tab)
    {
        if (DocumentFeature<IAnnotationEditor>.Get(tab.TryGetDocument()) is not { } editor)
        {
            return default;
        }

        var areas = 0;
        var pages = 0;
        var found = new List<PageAnnotation>();
        for (var page = 0; page < tab.PageCount; page++)
        {
            found.Clear();
            editor.GetAnnotations(page, found);
            var onPage = 0;
            foreach (var annotation in found)
            {
                onPage += annotation.Kind == AnnotationKind.Redaction ? 1 : 0;
            }

            areas += onPage;
            pages += onPage > 0 ? 1 : 0;
        }

        return new(areas, pages);
    }

    /// <summary>Makes the apply redactions window for a tab, with the marks counted.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="redactor">The engine's redactor.</param>
    /// <returns>The window's view model.</returns>
    private static RedactionViewModel CreateRedaction(DocumentTabViewModel tab, IDocumentRedactor redactor)
    {
        var (areas, pages) = CountRedactions(tab);
        return new(redactor, tab.FilePath, areas, pages, tab.HasUnsavedChanges);
    }
}
