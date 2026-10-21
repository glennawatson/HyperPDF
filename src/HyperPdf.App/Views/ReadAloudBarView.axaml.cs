// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The Read Aloud bar: play and pause, the voice and speed, and how the sentence being read is marked.</summary>
[DebuggerDisplay("ReadAloudBarView: {ViewModel}")]
public sealed partial class ReadAloudBarView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="ReadAloudBarView"/> class.</summary>
    public ReadAloudBarView()
    {
        InitializeComponent();
        VoiceBox.ItemTemplate = new FuncDataTemplate<string>(static (text, _) => new TextBlock { Text = text });
        SpeedBox.ItemTemplate = new FuncDataTemplate<string>(static (text, _) => new TextBlock { Text = text });
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.IsOpen, static v => v.ReadAloudBar.IsVisible));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ReadAloud.CloseCommand, static v => v.CloseReadAloudButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ReadAloud.PlayPauseCommand, static v => v.PlayButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ReadAloud.PlayPauseCommand, static v => v.PauseButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ReadAloud.PreviousCommand, static v => v.PreviousSentenceButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ReadAloud.NextCommand, static v => v.NextSentenceButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.IsPlaying, static v => v.PauseButton.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.IsPlaying, static v => v.PlayButton.IsVisible, static playing => !playing));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.VoiceNames, static v => v.VoiceBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.ReadAloud.VoiceIndex, static v => v.VoiceBox.SelectedIndex));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.SpeedNames, static v => v.SpeedBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.ReadAloud.SpeedIndex, static v => v.SpeedBox.SelectedIndex));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.CanDownloadVoice, static v => v.DownloadVoiceButton.IsVisible));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ReadAloud.DownloadVoiceCommand, static v => v.DownloadVoiceButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.IsDownloading, static v => v.VoiceDownloadProgress.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.DownloadProgress, static v => v.VoiceDownloadProgress.Value));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.StatusText, static v => v.ReadAloudText.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.FocusMode.WordHighlight, static v => v.WordMarkCheck.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.FocusMode.FocusBand, static v => v.FocusBandCheck.IsChecked, static on => on, static on => on == true));
        });
    }
}
