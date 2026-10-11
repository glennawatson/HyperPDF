// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Documents;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// A tab's layers (optional content). The panel only appears for documents that have layers; ticking a layer shows it
/// and clearing it hides it, changing only how the pages look.
/// </summary>
[DebuggerDisplay("LayersViewModel: {Items.Count} layers")]
public sealed partial class LayersViewModel : ReactiveObject, IDisposable
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Follows each listed layer's check box; replaced when the list is refilled.</summary>
    private readonly SingleReplaceableDisposable _itemChanges = new();

    /// <summary>Initializes a new instance of the <see cref="LayersViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public LayersViewModel(DocumentTabViewModel owner) => _owner = owner;

    /// <summary>Gets the layers.</summary>
    public ObservableCollection<LayerItemViewModel> Items { get; } = [];

    /// <summary>Gets a value indicating whether the document has layers.</summary>
    [Reactive]
    public partial bool HasLayers { get; private set; }

    /// <summary>Lists the document's layers; called when the document loads.</summary>
    public void Refresh()
    {
        Items.Clear();
        MultipleDisposable itemChanges = [];
        if (DocumentFeature<ILayerSource>.Get(_owner.TryGetDocument()) is
            {
            } source)
        {
            foreach (var layer in source.GetLayers())
            {
                var item = new LayerItemViewModel(layer.Id, layer.Name, layer.IsVisible);
                Items.Add(item);

                // The first value is the layer's current state, which the document already has.
                itemChanges.Add(item.WhenChanged(static x => x.IsVisible)
                    .Skip(1)
                    .SubscribeSafe(visible => SetVisible(item, visible), static error => Trace.TraceError(error.ToString())));
            }
        }

        _itemChanges.Create(itemChanges);
        HasLayers = Items.Count > 0;
        if (!HasLayers && _owner.IsLayersMode)
        {
            _owner.SidebarMode = SidebarMode.Thumbnails;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _itemChanges.Dispose();

    /// <summary>Shows or hides a layer and redraws the pages.</summary>
    /// <param name="item">The layer.</param>
    /// <param name="visible">Whether to show it.</param>
    private void SetVisible(LayerItemViewModel item, bool visible)
    {
        if (DocumentFeature<ILayerSource>.Get(_owner.TryGetDocument()) is
            {
            } source && source.SetLayerVisible(item.Id, visible))
        {
            _owner.OnLayersChanged();
        }
    }
}
