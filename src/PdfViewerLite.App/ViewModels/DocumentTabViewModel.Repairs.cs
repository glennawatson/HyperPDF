// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using ReactiveUI.SourceGenerators;

namespace PdfViewerLite.App.ViewModels;

/// <content>Tells the reader, once and calmly, that the file was damaged and had to be repaired to be shown.</content>
public sealed partial class DocumentTabViewModel
{
    /// <summary>Whether the notice has been shown for this open, so it is never shown twice.</summary>
    private bool _repairNoticeShown;

    /// <summary>Gets the calm message about a repaired file, until dismissed.</summary>
    [Reactive]
    public partial string? RepairNotice { get; private set; }

    /// <summary>Gets what was repaired, in plain words, while the reader has asked to see it.</summary>
    [Reactive]
    public partial string? RepairDetails { get; private set; }

    /// <summary>Shows the notice the first time the document reports that it was repaired.</summary>
    /// <param name="document">The document.</param>
    internal void CheckRepairs(IDocument document)
    {
        if (_repairNoticeShown || DocumentFeature<IRepairReport>.Get(document) is not { WasRepaired: true })
        {
            return;
        }

        _repairNoticeShown = true;
        RepairNotice = RepairWarnings.Summary;
    }

    /// <summary>Checks an open document again, because some damage shows only once a page is read or drawn.</summary>
    private void CheckRepairsOfOpenDocument()
    {
        if (!_repairNoticeShown && IsLoaded && Source.IsOpen)
        {
            CheckRepairs(Source.Acquire());
        }
    }

    /// <summary>Shows or hides the list of repairs.</summary>
    [ReactiveCommand]
    private void ToggleRepairDetails()
    {
        if (RepairDetails is not null)
        {
            RepairDetails = null;
            return;
        }

        RepairDetails = DocumentFeature<IRepairReport>.Get(Source.Acquire()) is { } report ? RepairWarnings.Describe(report.GetRepairs()) : null;
    }

    /// <summary>Puts the repair notice away.</summary>
    [ReactiveCommand]
    private void DismissRepairNotice()
    {
        RepairNotice = null;
        RepairDetails = null;
    }
}
