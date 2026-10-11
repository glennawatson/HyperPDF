// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Redaction;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace PdfViewerLite.App.ViewModels;

/// <content>Applying redactions, for engines that can remove content for good.</content>
public sealed partial class DocumentTabViewModel
{
    /// <summary>The menu text when the open document's engine can redact.</summary>
    private const string RedactAvailableText = "Apply _Redactions…";

    /// <summary>The menu text when it cannot, which says why.</summary>
    private const string RedactUnavailableText = "Apply Redactions… (needs the HyperPDF engine)";

    /// <summary>Whether the apply redactions command can run.</summary>
    private readonly IObservable<bool> _canRedact;

    /// <summary>Gets a value indicating whether the open document's engine can remove marked content for good.</summary>
    [Reactive]
    public partial bool CanRedact { get; private set; }

    /// <summary>Gets the menu text for the apply redactions command; when the command is off, the text says why.</summary>
    [Reactive]
    public partial string RedactMenuText { get; private set; } = RedactUnavailableText;

    /// <summary>Gets the interaction showing the apply redactions window.</summary>
    public Interaction<RedactionViewModel, RxVoid> RedactInteraction { get; } = new();

    /// <summary>Works out whether the open document's engine can redact.</summary>
    /// <param name="document">The open document.</param>
    internal void RefreshRedaction(IDocument document)
    {
        var can = DocumentFeature<IDocumentRedactor>.Get(document) is not null && DocumentFeature<IAnnotationEditor>.Get(document) is not null;
        CanRedact = can;
        RedactMenuText = can ? RedactAvailableText : RedactUnavailableText;
    }

    /// <summary>Counts the areas marked for redaction and the pages they are on.</summary>
    /// <param name="areas">Receives the number of marks.</param>
    /// <param name="pages">Receives the number of pages with marks.</param>
    internal void CountRedactions(out int areas, out int pages)
    {
        areas = 0;
        pages = 0;
        if (DocumentFeature<IAnnotationEditor>.Get(TryGetDocument()) is not
            {
            } editor)
        {
            return;
        }

        var found = new List<PageAnnotation>();
        for (var page = 0; page < PageCount; page++)
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
    }

    /// <summary>Shows the apply redactions window for this document.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canRedact))]
    private async Task ApplyRedactionsAsync()
    {
        if (DocumentFeature<IDocumentRedactor>.Get(TryGetDocument()) is not
            {
            } redactor)
        {
            return;
        }

        CountRedactions(out var areas, out var pages);
        using var request = new RedactionViewModel(redactor, FilePath, areas, pages, HasUnsavedChanges);
        _ = await RedactInteraction.Handle(request).ToTask().ConfigureAwait(true);
    }
}
