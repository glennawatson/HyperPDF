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

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>
/// A tab's layers (optional content), shown as the tree the file orders them in. The panel only appears for documents
/// that have layers; ticking a layer shows it and clearing it hides it, changing only how the pages look. Locked layers
/// are listed but cannot be changed, and Reset returns every layer to the file's own states.
/// </summary>
[DebuggerDisplay("LayersViewModel: {Items.Count} top-level entries")]
public sealed partial class LayersViewModel : ReactiveObject, IDisposable
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Follows the configuration choice and the listed layers' check boxes for the view model's lifetime.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The listed layers by id, to bring their check boxes in step with the document.</summary>
    private readonly Dictionary<int, LayerItemViewModel> _byId = [];

    /// <summary>The id of each configuration listed in <see cref="ConfigurationNames"/>.</summary>
    private readonly List<int> _configurationIds = [];

    /// <summary>The layer ids the visible page used when the tree was last listed, or <see langword="null"/> when the tree lists every layer.</summary>
    private HashSet<int>? _onPage;

    /// <summary>A set to read the visible page's layer ids into, so a page change that uses the same layers allocates nothing.</summary>
    private HashSet<int> _spareOnPage = [];

    /// <summary>Whether page changes were skipped while the panel was hidden.</summary>
    private bool _pageListStale;

    /// <summary>Whether the tree is being rebuilt or its check boxes synchronised, so changes are not sent to the document.</summary>
    private bool _updating;

    /// <summary>Initializes a new instance of the <see cref="LayersViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public LayersViewModel(DocumentTabViewModel owner)
    {
        _owner = owner;

        // The first value is the unselected state before the document is read. A choice made while the tree is rebuilt
        // comes from the rebuild, not the reader. The filter reads the view model's own state: a constructor cannot hand
        // 'this' to a state-passing operator.
        _subscriptions.Add(this.WhenChanged(static x => x.SelectedConfigurationIndex)
            .Skip(1)
            .Where(position => !_updating && (uint)position < (uint)_configurationIds.Count)
            .SubscribeSafe(UseConfiguration, ErrorTrace.Write));

        // A file whose layer list follows the visible pages lists its layers again for each page.
        _subscriptions.Add(owner.WhenChanged(static tab => tab.CurrentPageIndex)
            .Skip(1)
            .WhereWith(owner, static (tab, _) => DocumentFeature<ILayerSource>.Get(tab.TryGetDocument()) is { ListMode: DocumentLayerListMode.VisiblePages })
            .SubscribeSafe(_ => RefreshForPage(), ErrorTrace.Write));

        // Refilling the tree replaces the subscriptions to the old entries' check boxes.
        _subscriptions.Add(this.WhenChanged(static x => x.ItemChanges).OwnLatest().SubscribeSafeErrors(ErrorTrace.Write));
    }

    /// <summary>Gets the latest change to what the pages show, after layers were shown or hidden; the tab redraws every page.</summary>
    [Reactive]
    public partial PageChange LastPageChange { get; private set; }

    /// <summary>Gets the top-level entries of the layers tree.</summary>
    public ObservableCollection<LayerItemViewModel> Items { get; } = [];

    /// <summary>Gets the names of the configurations the file offers for viewing.</summary>
    public ObservableCollection<string> ConfigurationNames { get; } = [];

    /// <summary>Gets a value indicating whether the document has layers.</summary>
    [Reactive]
    public partial bool HasLayers { get; private set; }

    /// <summary>Gets a value indicating whether the file offers more than one configuration to choose from.</summary>
    [Reactive]
    public partial bool HasConfigurations { get; private set; }

    /// <summary>Gets or sets the position in <see cref="ConfigurationNames"/> of the configuration in use; -1 for none.</summary>
    [Reactive]
    public partial int SelectedConfigurationIndex { get; set; }

    /// <summary>Gets or sets the subscriptions to the listed layers' check boxes.</summary>
    [Reactive]
    private partial MultipleDisposable? ItemChanges { get; set; }

    /// <summary>Lists the document's layers; called when the document loads.</summary>
    public void Refresh()
    {
        _updating = true;
        try
        {
            Fill(DocumentFeature<ILayerSource>.Get(_owner.TryGetDocument()));
        }
        finally
        {
            _updating = false;
        }

        if (!HasLayers && _owner.IsLayersMode)
        {
            _owner.SidebarMode = SidebarMode.Thumbnails;
        }
    }

    /// <summary>Lists the layers the visible page uses when the panel opens after page changes were skipped.</summary>
    public void ShowForPage()
    {
        if (_pageListStale)
        {
            RefreshForPage();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Builds the entries of a level of the tree.</summary>
    /// <param name="nodes">The file's entries.</param>
    /// <param name="layers">The layers by id.</param>
    /// <param name="onPage">The layers a visible page uses to limit the list to, or <see langword="null"/> to list them all.</param>
    /// <returns>The entries.</returns>
    private static List<LayerItemViewModel> Build(IReadOnlyList<DocumentLayerNode> nodes, Dictionary<int, DocumentLayer> layers, HashSet<int>? onPage)
    {
        var result = new List<LayerItemViewModel>(nodes.Count);
        foreach (var node in nodes)
        {
            var children = Build(node.Children, layers, onPage);
            if (node.IsLabel && children.Count > 0)
            {
                result.Add(new(0, node.Name, false, false, string.Empty, children));
            }
            else if (!node.IsLabel && layers.TryGetValue(node.LayerId, out var layer) && (onPage is null || onPage.Contains(layer.Id) || children.Count > 0))
            {
                result.Add(new(layer.Id, layer.Name, layer.IsVisible, layer.IsLocked, LayerText.Note(layer), children));
            }
        }

        return result;
    }

    /// <summary>Rebuilds the tree and the configuration list from the document.</summary>
    /// <param name="source">The document's layers, or <see langword="null"/> when it has none.</param>
    private void Fill(ILayerSource? source)
    {
        _onPage = source is not null && source.ListMode == DocumentLayerListMode.VisiblePages ? new HashSet<int>(source.GetLayerIdsOnPage(_owner.CurrentPageIndex)) : null;
        _pageListStale = false;
        HasLayers = FillTree(source, _onPage);
        FillConfigurations(source);
    }

    /// <summary>Follows a page change: lists the layers again only when the layers the visible page uses are different.</summary>
    private void RefreshForPage()
    {
        if (!_owner.IsLayersMode)
        {
            // Nobody sees the tree; it is listed once when the panel opens.
            _pageListStale = true;
            return;
        }

        _pageListStale = false;
        if (DocumentFeature<ILayerSource>.Get(_owner.TryGetDocument()) is not { ListMode: DocumentLayerListMode.VisiblePages } source)
        {
            return;
        }

        _spareOnPage.Clear();
        _spareOnPage.UnionWith(source.GetLayerIdsOnPage(_owner.CurrentPageIndex));
        if (_onPage is not null && _onPage.SetEquals(_spareOnPage))
        {
            return;
        }

        var previous = _onPage;
        _onPage = _spareOnPage;
        _spareOnPage = previous ?? [];
        _updating = true;
        try
        {
            _ = FillTree(source, _onPage);
        }
        finally
        {
            _updating = false;
        }
    }

    /// <summary>Rebuilds the tree from the document, leaving the configuration list alone.</summary>
    /// <param name="source">The document's layers, or <see langword="null"/> when it has none.</param>
    /// <param name="onPage">The layers a visible page uses to limit the list to, or <see langword="null"/> to list them all.</param>
    /// <returns><see langword="true"/> when the document has layers.</returns>
    private bool FillTree(ILayerSource? source, HashSet<int>? onPage)
    {
        Items.Clear();
        _byId.Clear();
        var layers = new Dictionary<int, DocumentLayer>();
        MultipleDisposable itemChanges = [];
        foreach (var layer in source?.GetLayers() ?? [])
        {
            layers[layer.Id] = layer;
        }

        if (source is not null && layers.Count > 0)
        {
            foreach (var item in Build(source.GetLayerTree(), layers, onPage))
            {
                Items.Add(item);
                Track(item, itemChanges);
            }
        }

        ItemChanges = itemChanges;
        return layers.Count > 0;
    }

    /// <summary>Lists the configurations the file offers and selects the one in use.</summary>
    /// <param name="source">The document's layers, or <see langword="null"/> when it has none.</param>
    private void FillConfigurations(ILayerSource? source)
    {
        ConfigurationNames.Clear();
        _configurationIds.Clear();
        var selected = -1;
        foreach (var configuration in source?.GetConfigurations() ?? [])
        {
            selected = configuration.IsActive ? ConfigurationNames.Count : selected;
            ConfigurationNames.Add(configuration.Name);
            _configurationIds.Add(configuration.Index);
        }

        HasConfigurations = ConfigurationNames.Count > 1;
        SelectedConfigurationIndex = selected;
    }

    /// <summary>Makes the chosen configuration the one in use and lists the layers again.</summary>
    /// <param name="position">The position in <see cref="ConfigurationNames"/>.</param>
    private void UseConfiguration(int position)
    {
        if (DocumentFeature<ILayerSource>.Get(_owner.TryGetDocument()) is not { } source || !source.SelectConfiguration(_configurationIds[position]))
        {
            return;
        }

        Refresh();
        LastPageChange = LastPageChange.Next(PageChange.AllPages);
    }

    /// <summary>Follows an entry's check box and those of its sublayers.</summary>
    /// <param name="item">The entry.</param>
    /// <param name="itemChanges">Owns the subscriptions.</param>
    private void Track(LayerItemViewModel item, MultipleDisposable itemChanges)
    {
        if (!item.IsLabel)
        {
            _byId[item.Id] = item;

            // The first value is the layer's current state, which the document already has.
            itemChanges.Add(item.WhenChanged(static x => x.IsVisible)
                .Skip(1)
                .SubscribeSafe(visible => SetVisible(item, visible), ErrorTrace.Write));
        }

        foreach (var child in item.Children)
        {
            Track(child, itemChanges);
        }
    }

    /// <summary>Shows or hides a layer, redraws the pages and brings the other check boxes in step with the document.</summary>
    /// <param name="item">The layer.</param>
    /// <param name="visible">Whether to show it.</param>
    private void SetVisible(LayerItemViewModel item, bool visible)
    {
        if (_updating || DocumentFeature<ILayerSource>.Get(_owner.TryGetDocument()) is not { } source)
        {
            return;
        }

        var changed = source.SetLayerVisible(item.Id, visible);
        Synchronize(source);
        if (changed)
        {
            LastPageChange = LastPageChange.Next(PageChange.AllPages);
        }
    }

    /// <summary>Sets each check box to the document's state, as showing a layer can hide others and a locked layer refuses a change.</summary>
    /// <param name="source">The document's layers.</param>
    private void Synchronize(ILayerSource source)
    {
        _updating = true;
        try
        {
            foreach (var layer in source.GetLayers())
            {
                if (_byId.TryGetValue(layer.Id, out var item) && item.IsVisible != layer.IsVisible)
                {
                    item.IsVisible = layer.IsVisible;
                }
            }
        }
        finally
        {
            _updating = false;
        }
    }

    /// <summary>Returns every layer to the states the file sets and redraws the pages.</summary>
    [ReactiveCommand]
    private void Reset()
    {
        if (DocumentFeature<ILayerSource>.Get(_owner.TryGetDocument()) is not { } source || !source.ResetLayers())
        {
            return;
        }

        Synchronize(source);
        LastPageChange = LastPageChange.Next(PageChange.AllPages);
    }
}
