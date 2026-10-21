// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

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
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.OcrLanguages.Items, static v => v.OcrLanguageList.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.OcrLanguages.Note, static v => v.OcrNote.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.OcrLanguages.StatusText, static v => v.OcrStatusText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.OcrLanguages.IsDownloading, static v => v.OcrDownloadProgress.Opacity, static downloading => downloading ? 1D : 0D));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.OcrLanguages.DownloadProgress, static v => v.OcrDownloadProgress.Value));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.OcrLanguages.StopDownloadCommand, static v => v.StopLanguageDownloadButton));
            disposables.Add(this.Bind(ViewModel, static vm => vm.ColorScheme, static v => v.SchemeBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.PageTone, static v => v.PageToneBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.CustomPaper, static v => v.CustomPaperBox.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.CustomInk, static v => v.CustomInkBox.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UsesCustomPageTone, static v => v.CustomToneRow.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UsesCustomPageTone, static v => v.CustomToneContrastText.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.CustomToneContrast, static v => v.CustomToneContrastText.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.OpeningZoom, static v => v.OpeningZoomBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Toolbar, static v => v.ToolbarBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.FileChange, static v => v.FileChangeBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Motion, static v => v.MotionBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Caret, static v => v.CaretBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.FontSize, static v => v.FontSizeBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.FormHighlightColor, static v => v.FormHighlightColorBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.FormHighlightOpacity, static v => v.FormHighlightOpacityBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Sharpness.Preset, static v => v.SharpnessPresetBox.SelectedIndex));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Sharpness.PresetDescription, static v => v.SharpnessPresetText.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Sharpness.TextEdges, static v => v.TextEdgesBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Sharpness.StrokeAdjustment, static v => v.StrokeAdjustmentBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Sharpness.MinimumLineWidth, static v => v.MinimumLineBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Sharpness.SmoothGraphics, static v => v.SmoothGraphicsBox.IsChecked));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Sharpness.SmoothImages, static v => v.SmoothImagesBox.IsChecked));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Sharpness.AreaImageReduction, static v => v.AreaImageReductionBox.IsChecked));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Sharpness.SmallTextMasks, static v => v.SmallTextMasksBox.IsChecked));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Sharpness.TextHinting, static v => v.TextHintingBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Sharpness.TextContrast, static v => v.TextContrastBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.SpeechEngine, static v => v.SpeechEngineBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.AzureKey, static v => v.AzureKeyBox.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.AzureRegion, static v => v.AzureRegionBox.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.TimestampServer, static v => v.TimestampServerBox.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.CommentAuthor, static v => v.CommentAuthorBox.Text));
            BindReadingChoices(disposables.Add);
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.AzureKeyLabel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.AzureKeyBox.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.AzureRegionLabel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.AzureRegionBox.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.SpeechNote.Text, DescribeSpeech));

            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseCommand, static v => v.CloseButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), ErrorTrace.Write));

            // Start on the first choice, so the keyboard and screen readers begin at the top of the window.
            _ = SchemeBox.Focus();
        });
    }

    /// <summary>Explains where the chosen voice runs.</summary>
    /// <param name="azure">Whether Azure is chosen.</param>
    /// <returns>The note.</returns>
    private static string DescribeSpeech(bool azure) => azure
        ? "Each sentence read aloud is sent to your own Azure Speech resource, which bills your Azure account. The key is kept in your settings file, readable only by you."
        : "The voice runs on this computer, so nothing you read is sent anywhere. It is downloaded once, the first time you use Read Aloud.";

    /// <summary>Binds the tick boxes for how documents open and present.</summary>
    /// <param name="add">Keeps a binding until the window is deactivated.</param>
    private void BindReadingChoices(Action<IDisposable> add)
    {
        add(this.Bind(ViewModel, static vm => vm.ReopenAtLastPage, static v => v.ReopenAtLastPageBox.IsChecked));
        add(this.Bind(ViewModel, static vm => vm.FollowOpenAction, static v => v.FollowOpenActionBox.IsChecked));
        add(this.Bind(ViewModel, static vm => vm.FollowPageMode, static v => v.FollowPageModeBox.IsChecked));
        add(this.Bind(ViewModel, static vm => vm.FollowPageLayout, static v => v.FollowPageLayoutBox.IsChecked));
        add(this.Bind(ViewModel, static vm => vm.FollowDisplayDocTitle, static v => v.FollowDisplayDocTitleBox.IsChecked));
        add(this.Bind(ViewModel, static vm => vm.FollowPrintDefaults, static v => v.FollowPrintDefaultsBox.IsChecked));
        add(this.Bind(ViewModel, static vm => vm.IgnoreOutlineColours, static v => v.IgnoreOutlineColoursBox.IsChecked));
        add(this.Bind(ViewModel, static vm => vm.FollowPageTransitions, static v => v.FollowPageTransitionsBox.IsChecked));
        add(this.Bind(ViewModel, static vm => vm.FollowPageTimings, static v => v.FollowPageTimingsBox.IsChecked));
        add(this.Bind(ViewModel, static vm => vm.CheckSpelling, static v => v.CheckSpellingBox.IsChecked));
        add(this.Bind(ViewModel, static vm => vm.KeepKeyboardCursor, static v => v.KeepKeyboardCursorBox.IsChecked));
        add(this.Bind(ViewModel, static vm => vm.ShowPreviewOnlyFonts, static v => v.PreviewOnlyFontsBox.IsChecked));
    }

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
        FormHighlightColorBox.ItemsSource = PreferencesViewModel.FormHighlightColorOptions;
        FormHighlightOpacityBox.ItemsSource = PreferencesViewModel.FormHighlightOpacityOptions;
        SpeechEngineBox.ItemsSource = PreferencesViewModel.SpeechEngineOptions;
        SharpnessPresetBox.ItemsSource = SharpnessViewModel.PresetOptions;
        TextEdgesBox.ItemsSource = SharpnessViewModel.TextEdgeOptions;
        StrokeAdjustmentBox.ItemsSource = SharpnessViewModel.StrokeAdjustmentOptions;
        MinimumLineBox.ItemsSource = SharpnessViewModel.MinimumLineOptions;
        TextHintingBox.ItemsSource = SharpnessViewModel.TextHintingOptions;
        TextContrastBox.ItemsSource = SharpnessViewModel.TextContrastOptions;

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
            (FormHighlightColorBox, FormHighlightColorLabel),
            (FormHighlightOpacityBox, FormHighlightOpacityLabel),
            (SharpnessPresetBox, SharpnessPresetLabel),
            (TextEdgesBox, TextEdgesLabel),
            (StrokeAdjustmentBox, StrokeAdjustmentLabel),
            (MinimumLineBox, MinimumLineLabel),
            (TextHintingBox, TextHintingLabel),
            (TextContrastBox, TextContrastLabel),
            (SpeechEngineBox, SpeechEngineLabel),
            (AzureKeyBox, AzureKeyLabel),
            (AzureRegionBox, AzureRegionLabel),
            (TimestampServerBox, TimestampServerLabel));
    }
}
