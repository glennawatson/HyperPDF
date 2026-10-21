// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.App.Services;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// Explains the colour separation pages of a preseparated document: each such page paints one colourant of a printed
/// page. The bar says which colourant the current page is and which printed page it belongs to, and choosing another
/// colourant shows that separation. An ordinary page of such a document is named as one; a document without separations
/// shows nothing. The page order never changes.
/// </summary>
[DebuggerDisplay("PageSeparationViewModel: {Description}")]
public sealed partial class PageSeparationViewModel : ReactiveObject, IDisposable
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Owns the colourant choice subscription.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Each separation page's printed page and position, by page index.</summary>
    private Dictionary<int, PageSeparationSlot> _slots = [];

    /// <summary>The printed page of the current page, or <see langword="null"/> for an ordinary page.</summary>
    private SeparationGroup? _group;

    /// <summary>Initializes a new instance of the <see cref="PageSeparationViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public PageSeparationViewModel(DocumentTabViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;

        // Choosing another colourant shows its separation page; choosing the page already shown does nothing.
        _subscriptions.Add(this.WhenChanged(static vm => vm.SelectedIndex)
            .Skip(1)
            .Select(PageToShow)
            .Where(static page => page >= 0)
            .SubscribeSafe(owner.ShowPage, ErrorTrace.Write));
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

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

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
        HasSeparations = slots.Count > 0;
        Summary = separations.Groups.Length == 0 ? null : string.Create(
            CultureInfo.CurrentCulture,
            $"{slots.Count} colour separation pages, making {separations.Groups.Length} printed {(separations.Groups.Length == 1 ? "page" : "pages")}");
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

            Colorants = names;
        }

        Description = SeparationText.Describe(slot.Group, slot.Group.Pages[slot.Position]);
        SelectedIndex = slot.Position;
    }

    /// <summary>Gets the page of the separation at a position in <see cref="Colorants"/>, when it is not the page shown now.</summary>
    /// <param name="index">The chosen position.</param>
    /// <returns>The page to show, or -1 when there is nothing to show.</returns>
    private int PageToShow(int index) =>
        _group is { } group && (uint)index < (uint)group.Pages.Length && group.Pages[index].PageIndex != _owner.CurrentPageIndex
            ? group.Pages[index].PageIndex
            : -1;
}
