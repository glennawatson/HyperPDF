// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Platform.Storage;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The sound and video in the document, with the buttons that save the chosen one or open it with the system player.</summary>
[DebuggerDisplay("MediaPanelView: {ViewModel}")]
public sealed partial class MediaPanelView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="MediaPanelView"/> class.</summary>
    public MediaPanelView()
    {
        InitializeComponent();
        MediaList.ItemTemplate = new FuncDataTemplate<DocumentMedia>(static (_, _) => new MediaItemView());
        _ = this.WhenActivated(disposables =>
        {
            // Items are named as their containers are prepared, so this comes before the list is filled.
            disposables.Add(ItemAutomation.NameItems(MediaList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsMediaMode, static v => v.MediaPanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Media.Items, static v => v.MediaList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Media.Selected, static v => v.MediaList.SelectedItem, static item => item, static item => item as DocumentMedia));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Media.SaveCommand, static v => v.SaveMediaButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Media.OpenSelectedCommand, static v => v.OpenMediaButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Media.SaveInteraction).HandleWith(SaveMediaAsync));
        });
    }

    /// <summary>Asks where to save media, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveMediaAsync(IInteractionContext<string, string?> context)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            context.SetOutput(null);
            return;
        }

        var file = await storage.SaveFilePickerAsync(new() { Title = "Save Media", SuggestedFileName = context.Input });
        context.SetOutput(file?.TryGetLocalPath());
    }
}
