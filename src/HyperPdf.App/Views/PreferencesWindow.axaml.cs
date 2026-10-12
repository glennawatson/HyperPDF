// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.App.Views;

/// <summary>Shows the comfort preferences; each choice applies as soon as it is made.</summary>
[DebuggerDisplay("PreferencesWindow: {Title}")]
public sealed partial class PreferencesWindow : ReactiveUI.Avalonia.ReactiveWindow<PreferencesViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="PreferencesWindow"/> class.</summary>
    public PreferencesWindow()
    {
        InitializeComponent();
        FillChoices();
        _ = this.WhenActivated(Bind);
    }

    /// <summary>Explains where the chosen voice runs.</summary>
    /// <param name="azure">Whether Azure is chosen.</param>
    /// <returns>The note.</returns>
    private static string DescribeSpeech(bool azure) => azure
        ? "Each sentence read aloud is sent to your own Azure Speech resource, which bills your Azure account. The key is kept in your settings file, readable only by you."
        : "The voice runs on this computer, so nothing you read is sent anywhere. It is downloaded once, the first time you use Read Aloud.";

    /// <summary>Fills the lists of choices and links each box to its label.</summary>
    private void FillChoices()
    {
        SchemeBox.ItemsSource = PreferencesViewModel.ColorSchemeOptions;
        PageToneBox.ItemsSource = PreferencesViewModel.PageToneOptions;
        OpeningZoomBox.ItemsSource = PreferencesViewModel.OpeningZoomOptions;
        ToolbarBox.ItemsSource = PreferencesViewModel.ToolbarOptions;
        FileChangeBox.ItemsSource = PreferencesViewModel.FileChangeOptions;
        MotionBox.ItemsSource = PreferencesViewModel.MotionOptions;
        CaretBox.ItemsSource = PreferencesViewModel.CaretOptions;
        FontSizeBox.ItemsSource = PreferencesViewModel.FontSizeOptions;
        SpeechEngineBox.ItemsSource = PreferencesViewModel.SpeechEngineOptions;

        OcrLanguageList.ItemTemplate = new FuncDataTemplate<OcrLanguageItemViewModel>(static (_, _) => new OcrLanguageItemView());
        FieldLabels.Link(
            (SchemeBox, SchemeLabel),
            (PageToneBox, PageToneLabel),
            (ToolbarBox, ToolbarLabel),
            (FileChangeBox, FileChangeLabel),
            (MotionBox, MotionLabel),
            (CaretBox, CaretLabel),
            (FontSizeBox, FontSizeLabel),
            (OpeningZoomBox, OpeningZoomLabel),
            (CommentAuthorBox, CommentAuthorLabel),
            (SpeechEngineBox, SpeechEngineLabel),
            (AzureKeyBox, AzureKeyLabel),
            (AzureRegionBox, AzureRegionLabel),
            (TimestampServerBox, TimestampServerLabel));
    }

    /// <summary>Binds the choices while the window is shown.</summary>
    /// <param name="disposables">Receives the bindings.</param>
    private void Bind(MultipleDisposable disposables)
    {
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.OcrLanguages.Items, static v => v.OcrLanguageList.ItemsSource));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.OcrLanguages.Note, static v => v.OcrNote.Text));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.OcrLanguages.StatusText, static v => v.OcrStatusText.Text));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.OcrLanguages.IsDownloading, static v => v.OcrDownloadProgress.Opacity, static downloading => downloading ? 1D : 0D));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.OcrLanguages.DownloadProgress, static v => v.OcrDownloadProgress.Value));
        disposables.Add(this.BindCommand(ViewModel, static vm => vm.OcrLanguages.StopDownloadCommand, static v => v.StopLanguageDownloadButton));
        disposables.Add(this.Bind(ViewModel, static vm => vm.ColorScheme, static v => v.SchemeBox.SelectedIndex));
        disposables.Add(this.Bind(ViewModel, static vm => vm.PageTone, static v => v.PageToneBox.SelectedIndex));
        disposables.Add(this.Bind(ViewModel, static vm => vm.OpeningZoom, static v => v.OpeningZoomBox.SelectedIndex));
        disposables.Add(this.Bind(ViewModel, static vm => vm.Toolbar, static v => v.ToolbarBox.SelectedIndex));
        disposables.Add(this.Bind(ViewModel, static vm => vm.FileChange, static v => v.FileChangeBox.SelectedIndex));
        disposables.Add(this.Bind(ViewModel, static vm => vm.Motion, static v => v.MotionBox.SelectedIndex));
        disposables.Add(this.Bind(ViewModel, static vm => vm.Caret, static v => v.CaretBox.SelectedIndex));
        disposables.Add(this.Bind(ViewModel, static vm => vm.FontSize, static v => v.FontSizeBox.SelectedIndex));
        disposables.Add(this.Bind(ViewModel, static vm => vm.SpeechEngine, static v => v.SpeechEngineBox.SelectedIndex));
        disposables.Add(this.Bind(ViewModel, static vm => vm.AzureKey, static v => v.AzureKeyBox.Text));
        disposables.Add(this.Bind(ViewModel, static vm => vm.AzureRegion, static v => v.AzureRegionBox.Text));
        disposables.Add(this.Bind(ViewModel, static vm => vm.TimestampServer, static v => v.TimestampServerBox.Text));
        disposables.Add(this.Bind(ViewModel, static vm => vm.CommentAuthor, static v => v.CommentAuthorBox.Text));
        disposables.Add(this.Bind(ViewModel, static vm => vm.ReopenAtLastPage, static v => v.ReopenAtLastPageBox.IsChecked));
        disposables.Add(this.Bind(ViewModel, static vm => vm.CheckSpelling, static v => v.CheckSpellingBox.IsChecked));
        disposables.Add(this.Bind(ViewModel, static vm => vm.ShowPreviewOnlyFonts, static v => v.PreviewOnlyFontsBox.IsChecked));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.AzureKeyLabel.IsVisible));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.AzureKeyBox.IsVisible));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.AzureRegionLabel.IsVisible));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.AzureRegionBox.IsVisible));
        disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.SpeechNote.Text, DescribeSpeech));

        disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseCommand, static v => v.CloseButton));
        disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
            .SwitchMap(static closed => closed)
            .SubscribeSafe(_ => Close(), static error => Trace.TraceError(error.ToString())));

        // Start on the first choice, so the keyboard and screen readers begin at the top of the window.
        _ = SchemeBox.Focus();
    }
}
