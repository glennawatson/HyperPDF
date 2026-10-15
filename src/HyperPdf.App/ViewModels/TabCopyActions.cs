// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Archiving;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Flattening;
using HyperPdf.Core.Optimizing;
using HyperPdf.Core.Protection;
using HyperPdf.Core.Redaction;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.App.ViewModels;

/// <summary>The copy commands of a document tab: saving an optimised copy, saving a PDF/A copy and applying redactions.</summary>
internal static class TabCopyActions
{
    /// <summary>Gets saving a copy in a chosen format, for engines that can write copies.</summary>
    internal static TabCopyActionSpec<SaveCopyViewModel> SaveCopy { get; } = new(
        "Save a _Copy…",
        "Save a Copy… (needs the HyperPDF engine)",
        static document =>
            DocumentFeature<IDocumentOptimizer>.Get(document) is not null
            && DocumentFeature<IArchiveCopyWriter>.Get(document) is not null
            && DocumentFeature<IPdf20CopyWriter>.Get(document) is not null
            && DocumentFeature<IFlattenCopyWriter>.Get(document) is not null,
        CreateSaveCopy);

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

    /// <summary>Gets saving a PDF 2.0 copy, for engines that can write one.</summary>
    internal static TabCopyActionSpec<Pdf20CopyViewModel> Pdf20 { get; } = new(
        "Save as PDF 2.0 _Copy…",
        "Save as PDF 2.0 Copy… (needs the HyperPDF engine)",
        static document => DocumentFeature<IPdf20CopyWriter>.Get(document) is not null,
        static tab => DocumentFeature<IPdf20CopyWriter>.Get(tab.TryGetDocument()) is { } writer ? new(writer, tab.FilePath) : null);

    /// <summary>Gets saving a flattened copy, for engines that can write one.</summary>
    internal static TabCopyActionSpec<FlattenCopyViewModel> Flatten { get; } = new(
        "Save Flat_tened Copy…",
        "Save Flattened Copy… (needs the HyperPDF engine)",
        static document => DocumentFeature<IFlattenCopyWriter>.Get(document) is not null,
        static tab => DocumentFeature<IFlattenCopyWriter>.Get(tab.TryGetDocument()) is { } writer ? new(writer, tab.FilePath) : null);

    /// <summary>Gets saving a copy with a password and permissions, for engines that can write one.</summary>
    internal static TabCopyActionSpec<ProtectViewModel> Protect { get; } = new(
        "Protect with _Password…",
        "Protect with Password… (needs the HyperPDF engine)",
        static document => DocumentFeature<IDocumentProtector>.Get(document) is not null,
        static tab => DocumentFeature<IDocumentProtector>.Get(tab.TryGetDocument()) is { } protector ? new(protector, tab.FilePath) : null);

    /// <summary>Gets saving a copy without protection, for protected documents in engines that can write one.</summary>
    internal static TabCopyActionSpec<RemoveProtectionViewModel> Unprotect { get; } = new(
        "Remove Pro_tection…",
        "Remove Protection… (needs a protected document and the HyperPDF engine)",
        static document => DocumentFeature<IDocumentProtector>.Get(document) is { } protector && protector.GetStatus().IsProtected,
        static tab => DocumentFeature<IDocumentProtector>.Get(tab.TryGetDocument()) is { } protector ? new(protector, tab.FilePath) : null);

    /// <summary>Gets applying redactions, for engines that can remove marked content for good.</summary>
    internal static TabCopyActionSpec<RedactionViewModel> Redact { get; } = new(
        "Apply _Redactions…",
        "Apply Redactions… (needs the HyperPDF engine)",
        static document => DocumentFeature<IDocumentRedactor>.Get(document) is not null && DocumentFeature<IAnnotationEditor>.Get(document) is not null,
        static tab => DocumentFeature<IDocumentRedactor>.Get(tab.TryGetDocument()) is { } redactor ? CreateRedaction(tab, redactor) : null);

    /// <summary>Gets adding validation data or a document timestamp, for engines that can sign.</summary>
    internal static TabCopyActionSpec<ValidationDataViewModel> Validation { get; } = new(
        "Add _Validation Data or Timestamp…",
        "Add Validation Data or Timestamp… (needs the HyperPDF engine)",
        static document => DocumentFeature<IDocumentSigner>.Get(document) is not null,
        static tab => DocumentFeature<IDocumentSigner>.Get(tab.TryGetDocument()) is { } signer ? new(signer, tab.FilePath, TimestampServers.Create(tab.Services.Settings.TimestampServer)) : null);

    /// <summary>Gets signing with an external signer, for engines that can sign.</summary>
    internal static TabCopyActionSpec<ExternalSignViewModel> ExternalSign { get; } = new(
        "Sign with E_xternal Signer…",
        "Sign with External Signer… (needs the HyperPDF engine)",
        static document => DocumentFeature<IDocumentSigner>.Get(document) is not null,
        static tab => DocumentFeature<IDocumentSigner>.Get(tab.TryGetDocument()) is { } signer
            ? new(signer, tab.FilePath, Math.Max(0, tab.CurrentPageIndex), TimestampServers.Create(tab.Services.Settings.TimestampServer), tab.Services.RequestOpen)
            : null);

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

    /// <summary>Makes the unified "Save a Copy" window view model for a tab.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>The window's view model, or <see langword="null"/> when the engine cannot write copies.</returns>
    private static SaveCopyViewModel? CreateSaveCopy(DocumentTabViewModel tab)
    {
        var document = tab.TryGetDocument();
        return DocumentFeature<IDocumentOptimizer>.Get(document) is { } optimizer
            && DocumentFeature<IArchiveCopyWriter>.Get(document) is { } archive
            && DocumentFeature<IPdf20CopyWriter>.Get(document) is { } pdf20
            && DocumentFeature<IFlattenCopyWriter>.Get(document) is { } flatten
            ? new(
                new(optimizer, tab.FilePath, CultureInfo.CurrentUICulture.Name),
                new(archive, tab.FilePath),
                new(pdf20, tab.FilePath),
                new(flatten, tab.FilePath),
                tab.FilePath,
                DocumentFeature<IAnnotationEditor>.Get(document),
                tab.Source)
            : null;
    }
}
