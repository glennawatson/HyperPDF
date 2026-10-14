// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The bars for recognising the text of scanned pages: its progress, and the question of which language the document is in.</summary>
[DebuggerDisplay("RecognitionBarsView: {ViewModel}")]
public sealed partial class RecognitionBarsView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="RecognitionBarsView"/> class.</summary>
    public RecognitionBarsView()
    {
        InitializeComponent();
        LanguageBox.ItemTemplate = new FuncDataTemplate<string>(static (text, _) => new TextBlock { Text = text });
        LanguageBox.ItemsSource = TextRecognitionViewModel.LanguageNames;
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.TextRecognition.StopCommand, static v => v.StopRecognitionButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.IsRunning, static v => v.RecognitionBar.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.ProgressText, static v => v.RecognitionText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.Progress, static v => v.RecognitionProgress.Value));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.IsLanguageBarOpen, static v => v.LanguageBar.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.LanguageBarText, static v => v.LanguageBarText.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.TextRecognition.LanguageIndex, static v => v.LanguageBox.SelectedIndex));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.LanguageActionText, static v => v.LanguageActionButton.Content));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.IsDownloading, static v => v.LanguageDownloadProgress.Opacity, static downloading => downloading ? 1D : 0D));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.DownloadProgress, static v => v.LanguageDownloadProgress.Value));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.TextRecognition.ConfirmLanguageCommand, static v => v.LanguageActionButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.TextRecognition.CloseLanguageBarCommand, static v => v.CloseLanguageBarButton));
        });
    }
}
