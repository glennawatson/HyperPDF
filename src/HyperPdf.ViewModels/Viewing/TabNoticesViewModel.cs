// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>
/// The calm, dismissible notices of a tab: content that cannot be shown or run, told once instead of silently showing
/// fallback pages, and a file that was damaged and repaired to be shown.
/// </summary>
[DebuggerDisplay("TabNoticesViewModel: {ContentWarning} {RepairNotice}")]
public sealed partial class TabNoticesViewModel : ReactiveObject, ILocalisationChildren
{
    /// <summary>The tab's document source.</summary>
    private readonly DocumentSource _source;

    /// <summary>What the document's requirements mean for the viewer.</summary>
    private RequirementReport _requirements = RequirementReport.None;

    /// <summary>The integrity result behind an existing warning.</summary>
    private DocumentIntegrity? _integrity;

    /// <summary>The repairs already read for the expanded details.</summary>
    private IReadOnlyList<RepairNote> _repairs = [];

    /// <summary>Initializes a new instance of the <see cref="TabNoticesViewModel"/> class.</summary>
    /// <param name="source">The tab's document source.</param>
    internal TabNoticesViewModel(DocumentSource source) => _source = source;

    /// <summary>Gets what the document holds that cannot be shown or run, once checked.</summary>
    [Reactive]
    public partial UnsupportedContent UnsupportedContent { get; private set; }

    /// <summary>Gets the message about content that cannot be shown, until dismissed.</summary>
    [Reactive]
    public partial string? ContentWarning { get; private set; }

    /// <summary>Gets the calm message about a repaired file, until dismissed.</summary>
    [Reactive]
    public partial string? RepairNotice { get; private set; }

    /// <summary>Gets what was repaired, in plain words, while the reader has asked to see it.</summary>
    [Reactive]
    public partial string? RepairDetails { get; private set; }

    /// <summary>Gets the calm message about requirements the document declares and the viewer cannot meet, until dismissed.</summary>
    [Reactive]
    public partial string? RequirementNotice { get; private set; }

    /// <summary>Gets the unmet requirements, one line each, while the reader has asked to see them.</summary>
    [Reactive]
    public partial string? RequirementDetails { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the document's requirements stop its processing (ISO 32000-2 §12.11.6): the pages
    /// are shown as a static copy, and form filling, buttons and actions stay off for the whole time the tab is open.
    /// Dismissing the notice does not turn them back on.
    /// </summary>
    public bool InteractionBlocked => _requirements.StopsProcessing;

    /// <summary>Gets the message about an encrypted document whose integrity protection (PDF MAC) failed or could not be checked, until dismissed.</summary>
    [Reactive]
    public partial string? IntegrityNotice { get; private set; }

    /// <summary>Gets a value indicating whether the repair notice has been shown for this open, so it is never shown twice.</summary>
    internal bool RepairNoticeShown { get; private set; }

    /// <summary>
    /// Checks the requirements the document declares, before any of its actions run. No script is run: the check reads
    /// the requirement list and compares it with what the viewer provides.
    /// </summary>
    /// <param name="document">The document.</param>
    internal void CheckRequirements(IDocument document)
    {
        if (DocumentFeature<IDocumentRequirements>.Get(document) is not { } requirements)
        {
            return;
        }

        _requirements = requirements.Check();
        RequirementNotice = RequirementNotices.Describe(_requirements);
        RequirementDetails = null;
    }

    /// <summary>
    /// Shows a notice when the document's integrity protection does not hold, is missing, or cannot be checked here. A
    /// document whose protection was not checked is never described as intact.
    /// </summary>
    /// <param name="metadata">The document's metadata, or <see langword="null"/>.</param>
    internal void CheckIntegrity(DocumentMetadata? metadata)
    {
        _integrity = metadata?.Integrity;
        IntegrityNotice = IntegrityWarnings.Describe(_integrity);
    }

    /// <summary>Translates visible notices without restoring dismissed notices or changing their expanded state.</summary>
    /// <param name="visited">Objects already refreshed in the current traversal.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => RefreshCulture();

    /// <summary>Translates visible notices without restoring dismissed notices or changing their expanded state.</summary>
    internal void RefreshCulture()
    {
        if (ContentWarning is not null)
        {
            ContentWarning = ContentWarnings.Describe(UnsupportedContent);
        }

        if (RepairNotice is not null)
        {
            RepairNotice = RepairWarnings.Summary;
        }

        if (RepairDetails is not null)
        {
            RepairDetails = RepairWarnings.Describe(_repairs);
        }

        if (RequirementNotice is not null)
        {
            RequirementNotice = RequirementNotices.Describe(_requirements);
        }

        if (RequirementDetails is not null)
        {
            RequirementDetails = RequirementNotices.DescribeDetails(_requirements);
        }

        if (IntegrityNotice is not null)
        {
            IntegrityNotice = IntegrityWarnings.Describe(_integrity);
        }
    }

    /// <summary>Checks the whole document on a background thread, a page at a time so pages keep drawing meanwhile.</summary>
    /// <param name="document">The document.</param>
    /// <param name="attachments">The number of attached files.</param>
    /// <returns>A task.</returns>
    internal async Task CheckContentAsync(IDocument document, int attachments)
    {
        if (DocumentFeature<IContentCheck>.Get(document) is not { } check)
        {
            return;
        }

        // Task.Run has no state-passing form, so the check reads the values it needs from this closure.
        var found = await Task.Run(() => Check(check, document, attachments)).ConfigureAwait(true);
        UnsupportedContent = found;
        ContentWarning = ContentWarnings.Describe(found);

        // The scan above read every page's annotations, which may have shown damage the open did not.
        CheckRepairs(document);
    }

    /// <summary>Shows the repair notice the first time the document reports that it was repaired.</summary>
    /// <param name="document">The document.</param>
    internal void CheckRepairs(IDocument document)
    {
        if (RepairNoticeShown || DocumentFeature<IRepairReport>.Get(document) is not { WasRepaired: true })
        {
            return;
        }

        RepairNoticeShown = true;
        RepairNotice = RepairWarnings.Summary;
    }

    /// <summary>Checks the document and each page.</summary>
    /// <param name="check">The content check.</param>
    /// <param name="document">The document.</param>
    /// <param name="attachments">The attached files.</param>
    /// <returns>What cannot be shown or run.</returns>
    private static UnsupportedContent Check(IContentCheck check, IDocument document, int attachments)
    {
        var content = check.CheckDocument();
        var pages = document.GetPageSizes().Length;

        // A portfolio whose collection data HyperPDF reads is shown in the attachments panel, so it needs no warning.
        if (pages > 0 && attachments > 0 && DocumentFeature<IAttachmentSource>.Get(document)?.GetPortfolioSummary() is null
            && ContentWarnings.LooksLikePortfolio(attachments, document.GetText(0, 0, document.GetCharacterCount(0))))
        {
            content |= UnsupportedContent.Portfolio;
        }

        // Once a page has shown every kind a page can hold, the rest of the pages cannot add anything.
        const UnsupportedContent pageKinds = UnsupportedContent.Multimedia | UnsupportedContent.ThreeD | UnsupportedContent.JavaScript | UnsupportedContent.UnreadableImages;
        for (var page = 0; page < pages && !document.IsDisposed && (content & pageKinds) != pageKinds; page++)
        {
            content |= check.CheckPage(page);
        }

        return content;
    }

    /// <summary>Puts the integrity notice away.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DismissIntegrityNotice() => IntegrityNotice = null;

    /// <summary>Puts the content warning away.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DismissContentWarning() => ContentWarning = null;

    /// <summary>Shows or hides the list of repairs.</summary>
    [ReactiveCommand]
    private void ToggleRepairDetails()
    {
        if (RepairDetails is not null)
        {
            RepairDetails = null;
            return;
        }

        _repairs = DocumentFeature<IRepairReport>.Get(_source.Acquire()) is { } report ? report.GetRepairs() : [];
        RepairDetails = RepairWarnings.Describe(_repairs);
    }

    /// <summary>Shows or hides the list of unmet requirements.</summary>
    [ReactiveCommand]
    private void ToggleRequirementDetails() => RequirementDetails = RequirementDetails is null ? RequirementNotices.DescribeDetails(_requirements) : null;

    /// <summary>Puts the requirements notice away. A document that stops processing keeps its forms and actions off.</summary>
    [ReactiveCommand]
    private void DismissRequirementNotice()
    {
        RequirementNotice = null;
        RequirementDetails = null;
    }

    /// <summary>Puts the repair notice away.</summary>
    [ReactiveCommand]
    private void DismissRepairNotice()
    {
        RepairNotice = null;
        RepairDetails = null;
    }
}
