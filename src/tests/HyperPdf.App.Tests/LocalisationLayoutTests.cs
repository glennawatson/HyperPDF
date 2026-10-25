// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.Views;
using HyperPdf.Core.Annotations;
using HyperPdf.ViewModels;
using ReactiveUI;

namespace HyperPdf.App.Tests;

/// <summary>Measures actual localized windows with the headless render head.</summary>
[Category("Localisation")]
public sealed class LocalisationLayoutTests
{
    /// <summary>The main window width in logical pixels.</summary>
    private const double WindowWidth = 1200;

    /// <summary>The main window height in logical pixels.</summary>
    private const double WindowHeight = 850;

    /// <summary>Subpixel allowance for glyph layout rounding.</summary>
    private const double Tolerance = 1;

    /// <summary>The render passes before measuring text.</summary>
    private const int RenderPasses = 3;

    /// <summary>The non-default signing permission retained through each culture change.</summary>
    private const int RetainedCertificationIndex = 2;

    /// <summary>The certificate choice selector name.</summary>
    private const string SigningKindName = "KindBox";

    /// <summary>The user-authored text retained through every language change.</summary>
    private const string RetainedInput = "Reader's retained input";

    /// <summary>The preferences control retaining user-authored text.</summary>
    private const string AuthorBoxName = "CommentAuthorBox";

    /// <summary>The document name retained during live culture changes.</summary>
    private const string LiveDocumentName = "live.pdf";

    /// <summary>The document name used for translated layout measurements.</summary>
    private const string LayoutDocumentName = "localized.pdf";

    /// <summary>Existing controls translate in place while document coordinates and edited state survive.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExistingWindows_SwitchLanguageWithoutReplacingState()
    {
        var previous = UiText.UICulture;
        var app = Application.Current!;
        var styleCount = app.Styles.Count;
        InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
        CP.Localisation.Avalonia.LocalisationWindows.Refresh(app, UiText, CultureInfo.CurrentUICulture);
        CP.Localisation.Avalonia.LocalisationWindows.ApplyDirection(app, UiText);
        using var services = new TestServices();
        using var main = new MainViewModel(services.Services);
        await TestServices.OpenAndWaitAsync(main, [services.CreateDocument(LiveDocumentName, 1)]);
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, services.Services);
        using var model = new PreferencesViewModel(services.Services);
        var preferences = new PreferencesWindow { ViewModel = model };
        var print = new PrintPreviewWindow { ViewModel = preview };
        var document = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        var prompt = CreatePrompt();
        using var signingModel = new CertificateSignViewModel([]) { CertificationIndex = RetainedCertificationIndex };
        var signing = new CertificateSignWindow { ViewModel = signingModel };
        using var exportModel = new ExportPagesViewModel(new FakePageFileExporter(), LiveDocumentName, 1, 0);
        var export = new ExportPagesWindow { ViewModel = exportModel };
        Window[] windows = [preferences, print, document, prompt, signing, export];
        try
        {
            ShowWindows(windows);
            await UiWait.PumpAsync(RenderPasses);
            _ = await UiWait.UntilAsync(() => preview.IsValid && !preview.IsBuilding);
            var canvas = document.GetVisualDescendants().OfType<PageCanvasView>().Single();
            var author = preferences.FindControl<TextBox>(AuthorBoxName)!;
            author.Text = RetainedInput;
            var scheme = preferences.FindControl<ComboBox>("SchemeBox")!;
            var selected = new ChoiceState(scheme.ItemsSource, scheme.SelectedItem, scheme.SelectedIndex);
            var page = main.SelectedTab!.TryGetDocument();
            var printPath = preview.PreviewPath;
            var signingChoices = signing.FindControl<ComboBox>(SigningKindName)!.ItemsSource;
            for (var index = 1; index < PreferencesViewModel.InterfaceLanguageOptions.Count; index++)
            {
                model.InterfaceLanguage = index;
                var culture = CultureInfo.GetCultureInfo(PreferencesViewModel.GetInterfaceLanguageCode(index));
                CP.Localisation.Avalonia.LocalisationWindows.Refresh(app, UiText, culture);
                RefreshWindows(windows);
                signingModel.RaisePropertyChanged(nameof(CertificateSignViewModel.CertificationChoices));

                await UiWait.PumpAsync(RenderPasses);
                await AssertLiveLabelsAsync(preferences, print, prompt, canvas, culture, document, signing);
                await AssertExportLabelsAsync(export, exportModel);
                await AssertRetainedPreferencesAsync(preferences, model, selected);
                await Assert.That(ReferenceEquals(main.SelectedTab.TryGetDocument(), page)).IsTrue();
                await Assert.That(preview.PreviewPath).IsEqualTo(printPath);
                await Assert.That(ReferenceEquals(signing.FindControl<ComboBox>(SigningKindName)!.ItemsSource, signingChoices)).IsTrue();
            }
        }
        finally
        {
            CloseWindows(windows, app, styleCount, previous);
        }
    }

    /// <summary>Menus, preferences and print labels fit their arranged text bounds.</summary>
    /// <param name="culture">The interface culture to lay out.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("en")]
    [Arguments("fr")]
    [Arguments("de")]
    [Arguments("zh-Hans")]
    [Arguments("ar")]
    [Arguments("es")]
    [Arguments("pt")]
    public async Task Windows_LocalizedLabelsFit(string culture)
    {
        var previous = UiText.UICulture;
        var previousManager = CP.Localisation.Avalonia.CultureManager.UICulture;
        InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo(culture));
        CP.Localisation.Avalonia.CultureManager.UICulture = CultureInfo.CurrentUICulture;
        var app = Application.Current!;
        var styleCount = app.Styles.Count;
        CP.Localisation.Avalonia.LocalisationWindows.ApplyDirection(app, UiText);
        using var services = new TestServices();
        using var main = new MainViewModel(services.Services);
        await TestServices.OpenAndWaitAsync(main, [services.CreateDocument(LayoutDocumentName, 1)]);
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, services.Services);
        using var settings = new PreferencesViewModel(services.Services);
        var preferences = new PreferencesWindow { ViewModel = settings };
        var print = new PrintPreviewWindow { ViewModel = preview };
        using var exportModel = new ExportPagesViewModel(new FakePageFileExporter(), LayoutDocumentName, 1, 0);
        Window[] windows =
        [
            new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight },
            preferences,
            print,
            new ExportPagesWindow { ViewModel = exportModel },
        ];
        try
        {
            ShowWindows(windows);
            await UiWait.PumpAsync(RenderPasses);
            await Assert.That(preferences.Title).IsEqualTo(UiText.Get("Preferences"));
            await Assert.That(print.Title).IsEqualTo(UiText.Format("Print {0}", LayoutDocumentName));
            await AssertAuthorHintAsync(preferences);
            await Assert.That(string.Join(Environment.NewLine, windows.SelectMany(window => LabelProblems(window, culture)))).IsEqualTo(string.Empty);
        }
        finally
        {
            foreach (var window in windows)
            {
                window.Close();
            }

            while (app.Styles.Count > styleCount)
            {
                app.Styles.RemoveAt(app.Styles.Count - 1);
            }

            InterfaceCulture.Apply(UiText, previous);
            CP.Localisation.Avalonia.CultureManager.UICulture = previousManager;
        }
    }

    /// <summary>Arabic reverses the interface while the PDF canvas keeps its document coordinates.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Arabic_InterfaceIsRightToLeftAndDocumentCanvasIsLeftToRight()
    {
        var previous = UiText.UICulture;
        var app = Application.Current!;
        var count = app.Styles.Count;
        var canvas = new PageCanvasView();
        var thumbnail = new ThumbnailItemView();
        var window = new Window { Content = new StackPanel { Children = { canvas, thumbnail } } };
        using var request = new SignatureMarkViewModel(SignatureMarkKind.Signature, null, SignatureImageFile.Load);
        var signature = new SignatureMarkWindow { ViewModel = request };
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("ar"));
            CP.Localisation.Avalonia.LocalisationWindows.ApplyDirection(app, UiText);
            window.Show();
            signature.Show();
            await UiWait.PumpAsync(RenderPasses);
            await Assert.That(window.FlowDirection).IsEqualTo(FlowDirection.RightToLeft);
            await Assert.That(canvas.FlowDirection).IsEqualTo(FlowDirection.RightToLeft);
            await Assert.That(canvas.GetVisualDescendants().OfType<PageCanvas>().Single().FlowDirection).IsEqualTo(FlowDirection.LeftToRight);
            await Assert.That(canvas.FindControl<FocusView>("FocusPane")!.FlowDirection).IsEqualTo(FlowDirection.RightToLeft);
            await Assert.That(canvas.GetVisualDescendants().OfType<DocumentLockView>().Single().FlowDirection).IsEqualTo(FlowDirection.RightToLeft);
            await Assert.That(thumbnail.FindControl<PageThumbnail>("Thumbnail")!.FlowDirection).IsEqualTo(FlowDirection.LeftToRight);
            await Assert.That(signature.FlowDirection).IsEqualTo(FlowDirection.RightToLeft);
            await Assert.That(signature.FindControl<SignaturePad>("Pad")!.FlowDirection).IsEqualTo(FlowDirection.LeftToRight);
            await Assert.That(signature.FindControl<SignatureMarkPreview>("Preview")!.FlowDirection).IsEqualTo(FlowDirection.LeftToRight);
            await Assert.That(signature.FindControl<RadioButton>("TypeOption")!.FlowDirection).IsEqualTo(FlowDirection.RightToLeft);
        }
        finally
        {
            window.Close();
            signature.Close();
            while (app.Styles.Count > count)
            {
                app.Styles.RemoveAt(app.Styles.Count - 1);
            }

            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>Checks translated export content without changing the retained page range.</summary>
    /// <param name="window">The existing export window.</param>
    /// <param name="model">The existing export model.</param>
    /// <returns>A task.</returns>
    private static async Task AssertExportLabelsAsync(ExportPagesWindow window, ExportPagesViewModel model)
    {
        await Assert.That(window.Title).IsEqualTo(UiText.Get("Export Pages"));
        await Assert.That(window.FindControl<TextBlock>("HeadingText")!.Text).IsEqualTo(UiText.Format("Export pages of {0}", LiveDocumentName));
        await Assert.That(model.PagesText).IsEqualTo("1");
        var labels = window.FindControl<ListBox>("FormatList")!.GetVisualDescendants().OfType<TextBlock>()
            .Select(static label => label.Text).ToArray();
        await Assert.That(labels).Contains(UiText.Get("PNG picture"));
    }

    /// <summary>Checks that culture refresh keeps the preferences model, selection and user text.</summary>
    /// <param name="window">The existing preferences window.</param>
    /// <param name="model">The existing preferences model.</param>
    /// <param name="selected">The original choice identities.</param>
    /// <returns>A task.</returns>
    private static async Task AssertRetainedPreferencesAsync(PreferencesWindow window, PreferencesViewModel model, ChoiceState selected)
    {
        var scheme = window.FindControl<ComboBox>("SchemeBox")!;
        await Assert.That(ReferenceEquals(window.DataContext, model)).IsTrue();
        await Assert.That(ReferenceEquals(scheme.ItemsSource, selected.Source)).IsTrue();
        await Assert.That(scheme.SelectedIndex).IsEqualTo(selected.Index);
        await AssertSelectedChoiceAsync(scheme, PreferencesViewModel.ColorSchemeOptions[selected.Index]);
        await Assert.That(ReferenceEquals(scheme.SelectedItem, selected.Item)).IsTrue();
        await Assert.That(model.ColorScheme).IsEqualTo(selected.Index);
        await Assert.That(window.FindControl<TextBox>(AuthorBoxName)!.Text).IsEqualTo(RetainedInput);
    }

    /// <summary>Checks live window labels, composed accessibility help and document direction.</summary>
    /// <param name="preferences">The existing preferences.</param>
    /// <param name="print">The existing print preview.</param>
    /// <param name="prompt">The existing text prompt.</param>
    /// <param name="canvas">The unchanged document control.</param>
    /// <param name="culture">The newly selected culture.</param>
    /// <param name="document">The existing main document window.</param>
    /// <param name="signing">The existing certificate selector.</param>
    /// <returns>A task.</returns>
    private static async Task AssertLiveLabelsAsync(
        PreferencesWindow preferences,
        PrintPreviewWindow print,
        PromptWindow prompt,
        PageCanvasView canvas,
        CultureInfo culture,
        MainWindow document,
        CertificateSignWindow signing)
    {
        await Assert.That(UiText.UICulture.Name).IsEqualTo(culture.Name);
        await Assert.That(preferences.Title).IsEqualTo(UiText.Get("Preferences", culture));
        await Assert.That(print.Title).IsEqualTo(UiText.Format("Print {0}", "live.pdf"));
        await AssertPrintChoicesAsync(print);
        var kind = signing.FindControl<ComboBox>(SigningKindName)!;
        await Assert.That(kind.SelectedIndex).IsEqualTo(RetainedCertificationIndex);
        await AssertSelectedChoiceAsync(kind, signing.ViewModel!.CertificationChoices[RetainedCertificationIndex]);
        await Assert.That(prompt.Title).IsEqualTo(UiText.Get("Note", culture));
        await Assert.That(prompt.FindControl<TextBlock>("LabelText")!.Text).IsEqualTo(UiText.Get("Note text", culture));
        await Assert.That(prompt.FindControl<Button>("ConfirmButton")!.Content).IsEqualTo(UiText.Get("Save", culture));
        await Assert.That(prompt.FindControl<TextBox>("InputBox")!.Text).IsEqualTo(RetainedInput);
        await Assert.That(preferences.FlowDirection).IsEqualTo(culture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight);
        await Assert.That(document.FlowDirection).IsEqualTo(preferences.FlowDirection);
        await Assert.That(document.Title).IsEqualTo(document.ViewModel!.WindowTitle);
        await Assert.That(canvas.GetVisualDescendants().OfType<PageCanvas>().Single().FlowDirection).IsEqualTo(FlowDirection.LeftToRight);
        var language = preferences.FindControl<ComboBox>("InterfaceLanguageBox")!;
        await Assert.That(ControlHelp.GetText(language)).IsEqualTo(UiText.Get("Choose the interface language. Changes apply immediately.", culture));
        await Assert.That(AutomationProperties.GetHelpText(language)).IsEqualTo(ControlHelp.GetText(language));
        var sharpness = preferences.FindControl<ComboBox>("SharpnessPresetBox")!;
        await Assert.That(ControlHelp.GetText(sharpness)).IsEqualTo(Descriptions.SharpnessPreset);
        await Assert.That(AutomationProperties.GetHelpText(sharpness)).IsEqualTo(Descriptions.SharpnessPreset);
        await AssertAuthorHintAsync(preferences);
        foreach (var window in new Window[] { preferences, print, prompt, document })
        {
            await Assert.That(string.Join(Environment.NewLine, LabelProblems(window, culture.Name))).IsEqualTo(string.Empty);
        }
    }

    /// <summary>Creates the editable prompt whose typed labels must update without replacing its input.</summary>
    /// <returns>The existing prompt fixture.</returns>
    private static PromptWindow CreatePrompt()
    {
        var request = new TextPrompt(LocalizedMessage.Get(UiText, "Note"), LocalizedMessage.Get(UiText, "Note text"), RetainedInput, LocalizedMessage.Get(UiText, "Save"), false);
        return new PromptWindow { ViewModel = new(request) };
    }

    /// <summary>Runs the production refresh over existing windows hosted without a desktop lifetime.</summary>
    /// <param name="windows">The existing windows.</param>
    private static void RefreshWindows(Window[] windows)
    {
        foreach (var window in windows)
        {
            CP.Localisation.Avalonia.LocalisationWindows.Refresh(window);
        }
    }

    /// <summary>Shows the real windows before the test measures their controls.</summary>
    /// <param name="windows">The windows to arrange.</param>
    private static void ShowWindows(Window[] windows)
    {
        foreach (var window in windows)
        {
            window.Show();
        }
    }

    /// <summary>Checks displayed print choices resolve in the latest culture without rebuilding their items.</summary>
    /// <param name="print">The existing preview window.</param>
    /// <returns>A task.</returns>
    private static async Task AssertPrintChoicesAsync(PrintPreviewWindow print)
    {
        var sheets = print.GetVisualDescendants().OfType<PrintSheetSettingsView>().Single();
        var layout = sheets.FindControl<ComboBox>("LayoutBox")!;
        await AssertSelectedChoiceAsync(layout, PrintPreviewViewModel.LayoutChoices[layout.SelectedIndex]);
        var printer = print.GetVisualDescendants().OfType<PrintPrinterSettingsView>().Single();
        var destination = printer.FindControl<ComboBox>("DestinationBox")!;
        var labels = destination.GetVisualDescendants().OfType<TextBlock>().Where(static label => label.IsEffectivelyVisible).Select(static label => label.Text).ToArray();
        await Assert.That(labels).Contains(UiText.Get("Save as PDF"));
        await Assert.That(print.FindControl<Button>("PrintButton")!.Content).IsEqualTo(print.ViewModel!.Destination.ButtonText);
    }

    /// <summary>Checks visible captions and the text-search and accessibility fallback of stable selected items.</summary>
    /// <param name="choice">The existing selector.</param>
    /// <param name="expected">The translated caption.</param>
    /// <returns>A task.</returns>
    private static async Task AssertSelectedChoiceAsync(ComboBox choice, string expected)
    {
        await Assert.That(choice.SelectedItem).IsNotNull();
        await Assert.That(choice.SelectedItem?.ToString()).IsEqualTo(expected);
        var labels = choice.GetVisualDescendants().OfType<TextBlock>().Where(static label => label.IsEffectivelyVisible).Select(static label => label.Text).ToArray();
        await Assert.That(labels).Contains(expected);
    }

    /// <summary>Closes the test windows and restores shared interface culture and styles.</summary>
    /// <param name="windows">The windows opened by the test.</param>
    /// <param name="application">The shared headless application.</param>
    /// <param name="styleCount">The style count before the test.</param>
    /// <param name="previous">The previous interface culture.</param>
    private static void CloseWindows(Window[] windows, Application application, int styleCount, CultureInfo previous)
    {
        foreach (var window in windows)
        {
            window.Close();
        }

        while (application.Styles.Count > styleCount)
        {
            application.Styles.RemoveAt(application.Styles.Count - 1);
        }

        InterfaceCulture.Apply(UiText, previous);
        CP.Localisation.Avalonia.LocalisationWindows.Refresh(application, UiText, previous);
    }

    /// <summary>Checks every visible label against its actual arranged text geometry.</summary>
    /// <param name="window">The laid out window.</param>
    /// <param name="culture">The culture used for useful failure messages.</param>
    /// <returns>Every visible clipping problem in the window.</returns>
    private static IEnumerable<string> LabelProblems(Window window, string culture)
    {
        var labels = window.GetVisualDescendants().OfType<TextBlock>()
            .Where(static label => label.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(label.Text)).ToArray();
        if (labels.Length == 0)
        {
            yield return $"{culture}: {window.GetType().Name} has no visible labels";
        }

        foreach (var label in labels)
        {
            var layout = label.TextLayout;
            if (layout.TextLines.Any(static line => line.HasCollapsed))
            {
                yield return $"{culture}: {window.GetType().Name}: '{label.Text}' is ellipsized";
            }

            if (layout.Width > label.Bounds.Width + Tolerance)
            {
                yield return $"{culture}: {window.GetType().Name}: '{label.Text}' width {layout.Width} exceeds {label.Bounds.Width}";
            }

            if (layout.Height > label.Bounds.Height + Tolerance)
            {
                yield return $"{culture}: {window.GetType().Name}: '{label.Text}' height {layout.Height} exceeds {label.Bounds.Height}";
            }
        }
    }

    /// <summary>Checks the complete author guidance remains visible beside the input.</summary>
    /// <param name="preferences">The laid out preferences window.</param>
    /// <returns>A task.</returns>
    private static async Task AssertAuthorHintAsync(PreferencesWindow preferences)
    {
        var hint = preferences.FindControl<TextBlock>("CommentAuthorHint")!;
        await Assert.That(hint.IsEffectivelyVisible).IsTrue();
        await Assert.That(hint.Text).IsEqualTo(UiText.Get("Leave empty to use your user name"));
        await Assert.That(hint.TextWrapping).IsEqualTo(TextWrapping.Wrap);
        await Assert.That(string.IsNullOrEmpty(preferences.FindControl<TextBox>(AuthorBoxName)!.PlaceholderText)).IsTrue();
    }

    /// <summary>The exact selector state retained through a culture change.</summary>
    /// <param name="Source">The existing source.</param>
    /// <param name="Item">The selected identity.</param>
    /// <param name="Index">The selected index.</param>
    [System.Diagnostics.DebuggerDisplay("ChoiceState: {Index}")]
    private sealed record ChoiceState(object? Source, object? Item, int Index);
}
