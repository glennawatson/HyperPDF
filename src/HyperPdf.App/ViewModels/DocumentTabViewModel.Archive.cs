// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;
using HyperPdf.Core.Documents;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <content>Saving a PDF/A copy, for engines that can write one.</content>
public sealed partial class DocumentTabViewModel
{
    /// <summary>The menu text when the open document's engine can write a PDF/A copy.</summary>
    private const string ArchiveAvailableText = "Save P_DF/A Copy…";

    /// <summary>The menu text when it cannot, which says why.</summary>
    private const string ArchiveUnavailableText = "Save PDF/A Copy… (needs the HyperPDF engine)";

    /// <summary>Whether the PDF/A copy command can run.</summary>
    private readonly IObservable<bool> _canArchive;

    /// <summary>Gets a value indicating whether the open document's engine can write a PDF/A copy.</summary>
    [Reactive]
    public partial bool CanArchive { get; private set; }

    /// <summary>Gets the menu text for the PDF/A copy command; when the command is off, the text says why.</summary>
    [Reactive]
    public partial string ArchiveMenuText { get; private set; } = ArchiveUnavailableText;

    /// <summary>Gets the interaction showing the PDF/A copy window.</summary>
    public Interaction<ArchiveCopyViewModel, RxVoid> ArchiveInteraction { get; } = new();

    /// <summary>Works out whether the open document's engine can write a PDF/A copy.</summary>
    /// <param name="document">The open document.</param>
    internal void RefreshArchive(IDocument document)
    {
        var can = DocumentFeature<IArchiveCopyWriter>.Get(document) is not null;
        CanArchive = can;
        ArchiveMenuText = can ? ArchiveAvailableText : ArchiveUnavailableText;
    }

    /// <summary>Shows the PDF/A copy window for this document.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canArchive))]
    private async Task ArchiveCopyAsync()
    {
        if (DocumentFeature<IArchiveCopyWriter>.Get(TryGetDocument()) is not
            {
            } writer)
        {
            return;
        }

        using var request = new ArchiveCopyViewModel(writer, FilePath);
        _ = await ArchiveInteraction.Handle(request).ToTask().ConfigureAwait(true);
    }
}
