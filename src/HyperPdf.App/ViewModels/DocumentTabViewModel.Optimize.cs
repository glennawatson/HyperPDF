// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Optimizing;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <content>Saving an optimised copy, for engines that can write one.</content>
public sealed partial class DocumentTabViewModel
{
    /// <summary>The menu text when the open document's engine can write an optimised copy.</summary>
    private const string OptimizeAvailableText = "Save _Optimised Copy…";

    /// <summary>The menu text when it cannot, which says why.</summary>
    private const string OptimizeUnavailableText = "Save Optimised Copy… (needs the HyperPDF engine)";

    /// <summary>Whether the optimised copy command can run.</summary>
    private readonly IObservable<bool> _canOptimize;

    /// <summary>Gets a value indicating whether the open document's engine can write an optimised copy.</summary>
    [Reactive]
    public partial bool CanOptimize { get; private set; }

    /// <summary>Gets the menu text for the optimised copy command; when the command is off, the text says why.</summary>
    [Reactive]
    public partial string OptimizeMenuText { get; private set; } = OptimizeUnavailableText;

    /// <summary>Gets the interaction showing the optimised copy window.</summary>
    public Interaction<OptimizeCopyViewModel, RxVoid> OptimizeInteraction { get; } = new();

    /// <summary>Works out whether the open document's engine can write an optimised copy.</summary>
    /// <param name="document">The open document.</param>
    internal void RefreshOptimize(IDocument document)
    {
        var can = DocumentFeature<IDocumentOptimizer>.Get(document) is not null;
        CanOptimize = can;
        OptimizeMenuText = can ? OptimizeAvailableText : OptimizeUnavailableText;
    }

    /// <summary>Shows the optimised copy window for this document.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canOptimize))]
    private async Task OptimizeCopyAsync()
    {
        if (DocumentFeature<IDocumentOptimizer>.Get(TryGetDocument()) is not
            {
            } optimizer)
        {
            return;
        }

        using var request = new OptimizeCopyViewModel(optimizer, FilePath, CultureInfo.CurrentUICulture.Name);
        _ = await OptimizeInteraction.Handle(request).ToTask().ConfigureAwait(true);
    }
}
