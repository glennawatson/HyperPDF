// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>One text recognition language: a tick box to use it, its status, and a button to download or remove its pack.</summary>
[DebuggerDisplay("OcrLanguageItemView: {ViewModel}")]
public sealed partial class OcrLanguageItemView : ReactiveUI.Avalonia.ReactiveUserControl<OcrLanguageItemViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="OcrLanguageItemView"/> class.</summary>
    public OcrLanguageItemView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Name, static v => v.UseLanguageBox.Content));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsSelected, static v => v.UseLanguageBox.IsChecked, static on => on, IsOn));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StatusText, static v => v.LanguageStatusText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsDownloaded, static v => v.DownloadLanguageButton.IsVisible, static downloaded => !downloaded));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsDownloaded, static v => v.RemoveLanguageButton.IsVisible));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.DownloadCommand, static v => v.DownloadLanguageButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.RemoveCommand, static v => v.RemoveLanguageButton));
        });
    }

    /// <summary>Converts a nullable toggle state to a plain flag.</summary>
    /// <param name="value">The toggle state.</param>
    /// <returns><see langword="true"/> only when checked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOn(bool? value) => value == true;
}
