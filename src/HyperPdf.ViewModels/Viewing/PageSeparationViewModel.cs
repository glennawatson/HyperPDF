// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>
/// Explains the colour separation pages of a preseparated document: each such page paints one colourant of a printed
/// page. The bar says which colourant the current page is and which printed page it belongs to, and choosing another
/// colourant shows that separation. An ordinary page of such a document is named as one; a document without separations
/// shows nothing. The page order never changes.
/// </summary>
[DebuggerDisplay("PageSeparationViewModel: {Description}")]
public sealed partial class PageSeparationViewModel : ReactiveObject, IDisposable, ILocalisationChildren
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Owns the colourant choice subscription.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Each separation page's printed page and position, by page index.</summary>
    private Dictionary<int, PageSeparationSlot> _slots = [];

    /// <summary>The printed page of the current page, or <see langword="null"/> for an ordinary page.</summary>
    private SeparationGroup? _group;

    /// <summary>The current colourant labels, replaced individually when their fallback text changes.</summary>
    private ObservableCollection<string> _colorantNames = [];

    /// <summary>The actual page whose separation metadata is currently shown.</summary>
    private int _shownPageIndex = -1;

    /// <summary>The number of printed pages retained for the summary.</summary>
    private int _printedPages;

    /// <summary>Initializes a new instance of the <see cref="PageSeparationViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public PageSeparationViewModel(DocumentTabViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;

        // Choosing another colourant shows its separation page; choosing the page already shown does nothing.
        _subscriptions.Add(
            this.WhenChanged(static vm => vm.SelectedIndex).Skip(1).Select(PageToShow).Where(static page => page >= 0).SubscribeSafe(owner.ShowPage, ErrorTrace.Write));
    }

    /// <summary>
    /// Gets a value indicating whether the document has colour separation pages. The bar then stays shown on every page,
    /// so the layout does not move as the reader passes an ordinary page.
    /// </summary>
    [Reactive]
    public partial bool HasSeparations { get; private set; }

    /// <summary>
    /// Gets what the current page is, for example "Separation: Cyan, part of printed page 1 (4 colourants)"; for an
    /// ordinary page of a preseparated document it says so, and it is null for a document without separations.
    /// </summary>
    [Reactive]
    public partial string? Description { get; private set; }

    /// <summary>Gets the colourants of the current page's printed page, in the file's order.</summary>
    [Reactive]
    public partial IReadOnlyList<string> Colorants { get; private set; } = [];

    /// <summary>Gets or sets the position of the shown separation in <see cref="Colorants"/>; choosing another shows that page.</summary>
    [Reactive]
    public partial int SelectedIndex { get; set; } = -1;

    /// <summary>Gets a summary for the properties dialog, or <see langword="null"/> for a document of ordinary pages.</summary>
    [Reactive]
    public partial string? Summary { get; private set; }

    /// <summary>Gets a retained description of the counts without keeping the document view model alive.</summary>
    internal LocalizedMessage SummaryMessage
    {
        get
        {
            if (_printedPages == 0)
            {
                return LocalizedMessage.Literal(null);
            }

            var unit = LocalizedMessage.Get(UiText, _printedPages == 1 ? "page" : "pages");
            return LocalizedMessage.Format(UiText, "{0} colour separation pages, making {1} printed {2}", _slots.Count, _printedPages, unit);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        RefreshCultureText();
        LocalisationRefresh.RefreshCore(_owner, visited);
    }

    /// <summary>Takes the document's printed pages, then shows the current page.</summary>
    /// <param name="separations">The printed pages.</param>
    /// <param name="pageIndex">The current page.</param>
    internal void Load(DocumentSeparations separations, int pageIndex)
    {
        var slots = new Dictionary<int, PageSeparationSlot>();
        foreach (var group in separations.Groups)
        {
            for (var i = 0; i < group.Pages.Length; i++)
            {
                slots[group.Pages[i].PageIndex] = new(group, i);
            }
        }

        _slots = slots;
        _printedPages = separations.Groups.Length;
        HasSeparations = slots.Count > 0;
        RefreshSummary();
        ShowPage(pageIndex);
    }

    /// <summary>Gets the colourant a page paints, for its thumbnail.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <returns>The colourant's name, or <see langword="null"/> for an ordinary page.</returns>
    internal string? ColorantOf(int pageIndex) =>
        _slots.TryGetValue(pageIndex, out var slot) ? SeparationText.Colorant(slot.Group.Pages[slot.Position]) : null;

    /// <summary>Follows the current page.</summary>
    /// <param name="pageIndex">The current page.</param>
    internal void ShowPage(int pageIndex)
    {
        _shownPageIndex = pageIndex;

        // A document without separations has nothing to show or clear on a page turn.
        if (!HasSeparations && _group is null)
        {
            return;
        }

        if (!_slots.TryGetValue(pageIndex, out var slot))
        {
            _group = null;
            Description = HasSeparations ? SeparationText.OrdinaryPage : null;
            Colorants = [];
            SelectedIndex = -1;
            return;
        }

        if (!ReferenceEquals(_group, slot.Group))
        {
            _group = slot.Group;
            var names = new string[slot.Group.Pages.Length];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = SeparationText.Colorant(slot.Group.Pages[i]);
            }

            _colorantNames = new(names);
            Colorants = _colorantNames;
        }

        Description = SeparationText.Describe(slot.Group, slot.Group.Pages[slot.Position]);
        SelectedIndex = slot.Position;
    }

    /// <summary>Reformats retained separation metadata without selecting or navigating a page.</summary>
    private void RefreshCultureText()
    {
        RefreshSummary();
        var ordinaryPage = HasSeparations ? SeparationText.OrdinaryPage : null;
        Description = _slots.TryGetValue(_shownPageIndex, out var slot)
            ? SeparationText.Describe(slot.Group, slot.Group.Pages[slot.Position])
            : ordinaryPage;
        if (_group is not { } group)
        {
            return;
        }

        for (var index = 0; index < group.Pages.Length; index++)
        {
            var name = SeparationText.Colorant(group.Pages[index]);
            if (!string.Equals(_colorantNames[index], name, StringComparison.Ordinal))
            {
                _colorantNames[index] = name;
            }
        }
    }

    /// <summary>Formats the document's retained separation counts.</summary>
    private void RefreshSummary() => Summary = _printedPages == 0
        ? null
        : UiText.Format(
            "{0} colour separation pages, making {1} printed {2}",
            _slots.Count,
            _printedPages,
            _printedPages == 1 ? UiText.Get("page") : UiText.Get("pages"));

    /// <summary>Gets the page of the separation at a position in <see cref="Colorants"/>, when it is not the page shown now.</summary>
    /// <param name="index">The chosen position.</param>
    /// <returns>The page to show, or -1 when there is nothing to show.</returns>
    private int PageToShow(int index) =>
        _group is { } group && (uint)index < (uint)group.Pages.Length && group.Pages[index].PageIndex != _owner.CurrentPageIndex ? group.Pages[index].PageIndex : -1;
}
