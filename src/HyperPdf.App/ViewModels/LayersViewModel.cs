// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

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

    /// <summary>Follows each listed layer's check box; replaced when the tree is refilled.</summary>
    private readonly SingleReplaceableDisposable _itemChanges = new();

    /// <summary>Follows the configuration choice for the view model's lifetime.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The listed layers by id, to bring their check boxes in step with the document.</summary>
    private readonly Dictionary<int, LayerItemViewModel> _byId = [];

    /// <summary>The id of each configuration listed in <see cref="ConfigurationNames"/>.</summary>
    private readonly List<int> _configurationIds = [];

    /// <summary>Whether the tree is being rebuilt or its check boxes synchronised, so changes are not sent to the document.</summary>
    private bool _updating;

    /// <summary>Initializes a new instance of the <see cref="LayersViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public LayersViewModel(DocumentTabViewModel owner)
    {
        _owner = owner;

        // The first value is the unselected state before the document is read.
        _subscriptions.Add(this.WhenChanged(static x => x.SelectedConfigurationIndex)
            .Skip(1)
            .SubscribeSafe(OnConfigurationChosen, static error => Trace.TraceError(error.ToString())));
    }

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

    /// <summary>Lists the layers of the visible page again, for a file whose layer list follows the visible pages.</summary>
    public void OnPageChanged()
    {
        if (DocumentFeature<ILayerSource>.Get(_owner.TryGetDocument()) is { ListMode: DocumentLayerListMode.VisiblePages })
        {
            Refresh();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscriptions.Dispose();
        _itemChanges.Dispose();
    }

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
            var onPage = source.ListMode == DocumentLayerListMode.VisiblePages ? new HashSet<int>(source.GetLayerIdsOnPage(_owner.CurrentPageIndex)) : null;
            foreach (var item in Build(source.GetLayerTree(), layers, onPage))
            {
                Items.Add(item);
                Track(item, itemChanges);
            }
        }

        _itemChanges.Create(itemChanges);
        HasLayers = layers.Count > 0;
        FillConfigurations(source);
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
    private void OnConfigurationChosen(int position)
    {
        if (_updating || (uint)position >= (uint)_configurationIds.Count)
        {
            return;
        }

        if (DocumentFeature<ILayerSource>.Get(_owner.TryGetDocument()) is not { } source || !source.SelectConfiguration(_configurationIds[position]))
        {
            return;
        }

        Refresh();
        _owner.OnLayersChanged();
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
                .SubscribeSafe(visible => SetVisible(item, visible), static error => Trace.TraceError(error.ToString())));
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
            _owner.OnLayersChanged();
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
        _owner.OnLayersChanged();
    }
}
